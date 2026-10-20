// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Windows;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// Ensures a single GUI process; later launches forward files over a named pipe and activate the existing window.
/// </summary>
internal static class GuiInstanceCoordinator
{
    private const string MutexName = @"Local\PlainBytes.MetadataScrubber.Gui";
    private const string PipeName = "PlainBytes.MetadataScrubber.Gui.Activation.v1";

    private static Mutex? _instanceMutex;
    private static CancellationTokenSource? _listenerCts;
    private static Action<IReadOnlyList<string>>? _onActivation;
    private static readonly ConcurrentQueue<IReadOnlyList<string>> PendingActivations = new();

    /// <summary>
    /// Tries to become the GUI primary instance.
    /// Returns false when another instance is already running (paths were forwarded; this process should exit).
    /// </summary>
    public static bool TryAcquireOrForward(IReadOnlyList<string>? startupPaths)
    {
        _instanceMutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            ForwardToRunningInstance(startupPaths ?? Array.Empty<string>());
            return false;
        }

        _listenerCts = new CancellationTokenSource();
        _ = Task.Run(() => ListenLoop(_listenerCts.Token));
        return true;
    }

    /// <summary>
    /// Registers the activation callback and drains any activations received before the UI was ready.
    /// </summary>
    public static void SetActivationHandler(Action<IReadOnlyList<string>> handler)
    {
        _onActivation = handler;
        while (PendingActivations.TryDequeue(out var pending))
            DispatchActivation(pending);
    }

    /// <summary>Restores, shows, and focuses the main window.</summary>
    public static void ActivateWindow(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;

        window.Show();
        window.Activate();
        window.Focus();
    }

    /// <summary>Stops the named-pipe listener; call from application exit.</summary>
    public static void Dispose()
    {
        _listenerCts?.Cancel();
        _listenerCts?.Dispose();
        _listenerCts = null;
    }

    /// <summary>Accepts pipe connections and dispatches forwarded file paths to the UI thread.</summary>
    private static void ListenLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    NamedPipeServerStream.MaxAllowedServerInstances,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                server.WaitForConnectionAsync(cancellationToken).GetAwaiter().GetResult();
                var paths = ReadPaths(server);
                DispatchActivation(paths);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (IOException)
            {
                // Client disconnected or pipe error; keep listening for the next activation.
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    /// <summary>Marshals activation to the WPF dispatcher, or queues until dispatcher/handler exists.</summary>
    private static void DispatchActivation(IReadOnlyList<string> paths)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            PendingActivations.Enqueue(paths);
            return;
        }

        if (dispatcher.CheckAccess())
            InvokeHandler(paths);
        else
            dispatcher.Invoke(() => InvokeHandler(paths));
    }

    /// <summary>Invokes the registered handler, or queues paths if it is not set yet.</summary>
    private static void InvokeHandler(IReadOnlyList<string> paths)
    {
        if (_onActivation is null)
        {
            PendingActivations.Enqueue(paths);
            return;
        }

        _onActivation(paths);
    }

    /// <summary>Connects to the running instance pipe and sends startup paths (with short retries).</summary>
    private static void ForwardToRunningInstance(IReadOnlyList<string> paths)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(attempt == 0 ? 800 : 200);
                WritePaths(client, paths);
                return;
            }
            catch (TimeoutException)
            {
                Thread.Sleep(50);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>Writes a count-prefixed UTF-8 path list to the pipe.</summary>
    private static void WritePaths(Stream stream, IReadOnlyList<string> paths)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(paths.Count);
        foreach (var path in paths)
        {
            var bytes = Encoding.UTF8.GetBytes(path);
            writer.Write(bytes.Length);
            writer.Write(bytes);
        }

        stream.Flush();
    }

    /// <summary>Reads a count-prefixed UTF-8 path list from the pipe.</summary>
    private static IReadOnlyList<string> ReadPaths(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        var count = reader.ReadInt32();
        if (count <= 0)
            return Array.Empty<string>();

        var paths = new List<string>(count);
        for (var i = 0; i < count; i++)
        {
            var length = reader.ReadInt32();
            if (length <= 0)
                continue;

            paths.Add(Encoding.UTF8.GetString(reader.ReadBytes(length)));
        }

        return paths;
    }
}
