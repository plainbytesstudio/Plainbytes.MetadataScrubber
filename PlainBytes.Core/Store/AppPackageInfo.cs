// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;

namespace PlainBytes.Core.Store;

/// <summary>
/// MSIX / Microsoft Store package identity for the current process (packaged vs unpackaged, display version).
/// </summary>
public static class AppPackageInfo
{
    /// <summary>Returns true when the app runs inside an MSIX or Store package.</summary>
    public static bool IsRunningAsPackagedApp()
    {
        try
        {
            _ = Windows.ApplicationModel.Package.Current;
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (COMException)
        {
            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the package identity version (Major.Minor.Build.Revision), or <paramref name="unpackagedFallback"/> when not packaged.
    /// </summary>
    public static string GetDisplayVersion(string unpackagedFallback)
    {
        try
        {
            var v = Windows.ApplicationModel.Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        catch (InvalidOperationException)
        {
            // unpackaged
        }
        catch (COMException)
        {
            // unpackaged
        }
        catch
        {
            // ignore
        }

        return unpackagedFallback;
    }
}
