// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using PlainBytes.Core.Controls;
using PlainBytes.Core.Store;
using CoreStrings = PlainBytes.Core.Resources.Strings;

namespace PlainBytes.MetadataScrubber;

/// <summary>
/// MetadataScrubber application entry: GUI shell, edition/purchase UI, single-instance activation, and headless CLI.
/// </summary>
public partial class App : Application
{
    /// <summary>Shell nav id for the home (scrub) page.</summary>
    public const string NavIdHome = "home";

    /// <summary>Shell nav id for strategy / settings.</summary>
    public const string NavIdSettings = "settings";

    /// <summary>Shell nav id for the help center.</summary>
    public const string NavIdHelp = "help";

    /// <summary>Shell nav id for the about page.</summary>
    public const string NavIdAbout = "about";

    private const string ProductId = "MetadataScrubber";
    private const string ProductIconPackUri =
        "pack://application:,,,/PlainBytes.Core;component/Assets/Icons/logo_MetadataScrubber.ico";

    private MainModule? _mainModule;
    private StrategyManagementView? _strategyView;
    private AboutView? _aboutView;
    private HelpCenterView? _helpView;
    private ShellWindow? _shell;
    private MyMetadataScrubberModule? _toolModule;
    private ProductEditionUiHost? _editionHost;

    /// <summary>
    /// Starts headless CLI when requested; otherwise builds the shell, wires edition UI, and applies startup file args.
    /// </summary>
    protected override void OnStartup(StartupEventArgs e)
    {
        if (IsHeadlessCli(e.Args))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            CliResultToast.EnsureInitialized();

            var code = RunHeadlessCli(e.Args);
            Shutdown(code);
            return;
        }

        var startupPaths = ParseStartupFilePaths(e.Args);
        if (!GuiInstanceCoordinator.TryAcquireOrForward(startupPaths))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            base.OnStartup(e);
            Shutdown(0);
            return;
        }

        base.OnStartup(e);

        _toolModule = new MyMetadataScrubberModule();
        _shell = new ShellWindow();
        _editionHost = ProductEditionUiHost.TryCreate(
            _shell,
            ProductId,
            () => Shutdown(),
            () => _toolModule?.ToolName);

        RecreateToolViews();
        ConfigureShellNavigation();

        _shell.SetContent(_mainModule!);
        _shell.SetToolName(_toolModule.ToolName);
        _shell.SetCurrentProductId(ProductId);
        // BitmapImage defaults to the smallest ICO frame (16×16); pick the largest for taskbar.
        _shell.Icon = LoadBestIconFrame(ProductIconPackUri);
        _shell.SelectNav(NavIdHome);

        _shell.CultureChanged += OnShellCultureChanged;

        MainWindow = _shell;
        _shell.Show();

        if (_editionHost is not null)
        {
            _editionHost.AttachWindow();
            _ = _editionHost.InitializeAsync();
        }

        GuiInstanceCoordinator.SetActivationHandler(paths =>
        {
            if (paths.Count > 0)
                _mainModule!.ImportStartupFiles(paths);

            GuiInstanceCoordinator.ActivateWindow(_shell!);
        });

        if (startupPaths is { Count: > 0 })
            _mainModule!.ImportStartupFiles(startupPaths);
    }

    /// <summary>Detaches edition host and releases the single-instance mutex.</summary>
    protected override void OnExit(ExitEventArgs e)
    {
        _editionHost?.Detach();
        GuiInstanceCoordinator.Dispose();
        base.OnExit(e);
    }

    /// <summary>Loads the largest frame from a multi-resolution .ico pack URI.</summary>
    private static BitmapFrame LoadBestIconFrame(string packUri)
    {
        var decoder = BitmapDecoder.Create(
            new Uri(packUri, UriKind.Absolute),
            BitmapCreateOptions.None,
            BitmapCacheOption.OnLoad);

        return decoder.Frames
            .OrderByDescending(f => f.PixelWidth * f.PixelHeight)
            .First();
    }

    /// <summary>Rebuilds main tool views so localized strings pick up the current culture.</summary>
    private void RecreateToolViews()
    {
        _mainModule = (MainModule)_toolModule!.GetMainControl();
        _strategyView = new StrategyManagementView();
        _aboutView = new AboutView();
        _helpView = new HelpCenterView();
    }

    /// <summary>Registers top rail navigation items for home, settings, help, and about.</summary>
    private void ConfigureShellNavigation()
    {
        if (_shell is null)
            return;

        _shell.ConfigureTopNavigation(
        [
            new ShellNavItem(
                NavIdHome,
                CoreStrings.Nav_Home,
                ShellWindow.LoadCoreNavIcon("broom-w.png"),
                () => _shell.SetContent(_mainModule!)),
            new ShellNavItem(
                NavIdSettings,
                CoreStrings.Nav_Settings,
                ShellWindow.LoadCoreNavIcon("shield-w.png"),
                () => _shell.SetContent(_strategyView!)),
            new ShellNavItem(
                NavIdHelp,
                CoreStrings.Nav_Help,
                ShellWindow.LoadCoreNavIcon("help-w.png"),
                () => _shell.SetContent(_helpView!)),
            new ShellNavItem(
                NavIdAbout,
                CoreStrings.Nav_About,
                ShellWindow.LoadCoreNavIcon("about-us-w.png"),
                () => _shell.SetContent(_aboutView!)),
        ]);
    }

    /// <summary>Recreates views and navigation after UI culture change, preserving the selected page when possible.</summary>
    private void OnShellCultureChanged()
    {
        if (_shell is null || _toolModule is null)
            return;

        var selected = GetSelectedNavHint();

        RecreateToolViews();
        ConfigureShellNavigation();
        _shell.SetToolName(_toolModule.ToolName);
        _editionHost?.ApplyCurrentEditionToShell();
        _editionHost?.RefreshOverlayStrings();

        switch (selected)
        {
            case NavIdSettings:
                _shell.SelectNav(NavIdSettings);
                _shell.SetContent(_strategyView!);
                break;
            case NavIdHelp:
                _shell.SelectNav(NavIdHelp);
                _shell.SetContent(_helpView!);
                break;
            case NavIdAbout:
                _shell.SelectNav(NavIdAbout);
                _shell.SetContent(_aboutView!);
                break;
            case ShellWindow.NavIdMatrix:
            case ShellWindow.NavIdSupport:
                break;
            default:
                _shell.SelectNav(NavIdHome);
                _shell.SetContent(_mainModule!);
                break;
        }

        CliShellVerb.ApplyRegisteredLabels();
    }

    /// <summary>Infers the current nav id from the shell content type.</summary>
    private string? GetSelectedNavHint()
    {
        if (_shell?.CurrentContent is StrategyManagementView)
            return NavIdSettings;
        if (_shell?.CurrentContent is HelpCenterView)
            return NavIdHelp;
        if (_shell?.CurrentContent is AboutView)
            return NavIdAbout;
        if (_shell?.CurrentContent is SuiteProductsView)
            return ShellWindow.NavIdMatrix;
        if (_shell?.CurrentContent is SupportView)
            return ShellWindow.NavIdSupport;
        if (_shell?.CurrentContent is MainModule)
            return NavIdHome;
        return null;
    }

    /// <summary>True when args request shell-verb registration or default scrub CLI.</summary>
    private static bool IsHeadlessCli(string[] args) =>
        CliShellVerb.TryParse(args, out _) || CliDefaultScrub.IsRequested(args);

    /// <summary>Dispatches headless CLI commands; returns process exit code.</summary>
    private static int RunHeadlessCli(string[] args)
    {
        if (CliShellVerb.TryParse(args, out var register))
            return CliShellVerb.Run(register);

        if (CliDefaultScrub.TryParse(args, out var paths))
            return CliDefaultScrub.Run(paths);

        return 1;
    }

    /// <summary>
    /// Collects existing supported document paths from launch args (non-flag tokens).
    /// </summary>
    private static IReadOnlyList<string>? ParseStartupFilePaths(string[] args)
    {
        if (args is not { Length: > 0 })
            return null;

        var paths = new List<string>();
        foreach (var arg in args)
        {
            if (string.IsNullOrWhiteSpace(arg) || arg.StartsWith('-'))
                continue;

            var path = arg.Trim().Trim('"');
            if (!DocumentFileSupport.IsSupported(path) || !File.Exists(path))
                continue;

            paths.Add(path);
        }

        return paths.Count > 0 ? paths : null;
    }
}
