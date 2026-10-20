// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Diagnostics;
using System.Runtime.InteropServices;
using Windows.Management.Deployment;
using Windows.System;
using PlainBytes.Core.Catalog;

namespace PlainBytes.Core.Store;

/// <summary>
/// 检测 Store/MSIX 安装状态，并启动应用或打开 Store 产品页。
/// </summary>
public static class SuiteAppLauncher
{
    public static bool IsInstalled(SuiteProductInfo product)
    {
        if (product is null)
            return false;

        try
        {
            return TryFindInstalledPackage(product) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsInstalled(SuiteProductInfo product, string? currentProductId)
    {
        if (IsInstalled(product))
            return true;

        // 调试/非打包运行时：当前进程即该产品，视为已安装
        return !string.IsNullOrWhiteSpace(currentProductId)
            && string.Equals(product.Id, currentProductId, StringComparison.OrdinalIgnoreCase);
    }

    public static async Task OpenAsync(SuiteProductInfo product, string? currentProductId = null)
    {
        if (product is null)
            return;

        if (IsInstalled(product, currentProductId))
        {
            if (TryLaunchInstalled(product))
                return;

            // 当前进程即本产品且无法再起包时，忽略
            if (string.Equals(product.Id, currentProductId, StringComparison.OrdinalIgnoreCase))
                return;
        }

        await OpenStorePageAsync(product).ConfigureAwait(true);
    }

    public static async Task OpenStorePageAsync(SuiteProductInfo product)
    {
        if (product.HasStoreProduct)
        {
            var storeUri = new Uri($"ms-windows-store://pdp/?ProductId={product.StoreProductId}");
            if (await TryLaunchUriAsync(storeUri).ConfigureAwait(true))
                return;
        }

        // 无 ProductId 时退回商店搜索
        var query = Uri.EscapeDataString(product.Name);
        await TryLaunchUriAsync(new Uri($"ms-windows-store://search/?query={query}")).ConfigureAwait(true);
    }

    private static bool TryLaunchInstalled(SuiteProductInfo product)
    {
        var familyName = ResolvePackageFamilyName(product);
        if (string.IsNullOrWhiteSpace(familyName))
            return false;

        var aumid = $"{familyName}!{product.ApplicationId}";
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = $"shell:AppsFolder\\{aumid}",
                UseShellExecute = true,
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string? ResolvePackageFamilyName(SuiteProductInfo product)
    {
        var installed = TryFindInstalledPackage(product);
        if (installed is not null)
            return installed.Id.FamilyName;

        return string.IsNullOrWhiteSpace(product.PackageFamilyName)
            ? null
            : product.PackageFamilyName;
    }

    private static Windows.ApplicationModel.Package? TryFindInstalledPackage(SuiteProductInfo product)
    {
        if (string.IsNullOrWhiteSpace(product.PackageIdentityName))
            return null;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return null;

        var manager = new PackageManager();

        if (!string.IsNullOrWhiteSpace(product.PackageFamilyName))
        {
            foreach (var package in manager.FindPackagesForUser(string.Empty, product.PackageFamilyName))
                return package;
        }

        foreach (var package in manager.FindPackagesForUser(string.Empty))
        {
            if (string.Equals(package.Id.Name, product.PackageIdentityName, StringComparison.OrdinalIgnoreCase))
                return package;
        }

        return null;
    }

    private static async Task<bool> TryLaunchUriAsync(Uri uri)
    {
        try
        {
            return await Launcher.LaunchUriAsync(uri);
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true,
                });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
