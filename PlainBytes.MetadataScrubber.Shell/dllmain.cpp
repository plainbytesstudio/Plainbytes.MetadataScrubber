// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only

#include "pch.h"
#include "resource.h"

#include <appmodel.h>
#include <algorithm>
#include <string>
#include <vector>

// Explorer context-menu shell extension for MetadataScrubber.
// Collects supported selected files and launches the app with --scrub-default
// (via package activation when packaged, otherwise CreateProcess).

namespace
{
    long g_cDllRef = 0;
    HINSTANCE g_hInst = nullptr;

    constexpr wchar_t kAppExeName[] = L"PlainBytes.MetadataScrubber.exe";
    constexpr wchar_t kAppId[] = L"App";
    constexpr wchar_t kScrubFlag[] = L"--scrub-default";
    constexpr wchar_t kListFlag[] = L"--list";
    constexpr size_t kMaxInlineArgsChars = 24000;

    enum class ShellLang
    {
        En,
        ZhHans,
        ZhHant,
        Ja,
        Ko,
        De,
        Fr,
        Ru,
        It,
        Es,
        Pt,
        Nl,
        Ar,
    };

    // Maps the user UI language to a supported shell menu language.
    ShellLang ResolveShellLang()
    {
        const LANGID lang = GetUserDefaultUILanguage();
        const WORD primary = PRIMARYLANGID(lang);
        const WORD sub = SUBLANGID(lang);

        if (primary == LANG_CHINESE)
        {
            if (sub == SUBLANG_CHINESE_TRADITIONAL
                || sub == SUBLANG_CHINESE_HONGKONG
                || sub == SUBLANG_CHINESE_MACAU)
                return ShellLang::ZhHant;
            return ShellLang::ZhHans;
        }

        switch (primary)
        {
        case LANG_JAPANESE: return ShellLang::Ja;
        case LANG_KOREAN: return ShellLang::Ko;
        case LANG_GERMAN: return ShellLang::De;
        case LANG_FRENCH: return ShellLang::Fr;
        case LANG_RUSSIAN: return ShellLang::Ru;
        case LANG_ITALIAN: return ShellLang::It;
        case LANG_SPANISH: return ShellLang::Es;
        case LANG_PORTUGUESE: return ShellLang::Pt;
        case LANG_DUTCH: return ShellLang::Nl;
        case LANG_ARABIC: return ShellLang::Ar;
        default: return ShellLang::En;
        }
    }

    // Returns the localized context-menu title for the current UI language.
    std::wstring LocalizeMenuTitle()
    {
        switch (ResolveShellLang())
        {
        case ShellLang::ZhHans: return L"PlainBytes 清除元数据";
        case ShellLang::ZhHant: return L"PlainBytes 清除中繼資料";
        case ShellLang::Ja: return L"PlainBytes メタデータを削除";
        case ShellLang::Ko: return L"PlainBytes 메타데이터 삭제";
        case ShellLang::De: return L"PlainBytes Metadaten löschen";
        case ShellLang::Fr: return L"PlainBytes Supprimer les métadonnées";
        case ShellLang::Ru: return L"PlainBytes Удалить метаданные";
        case ShellLang::It: return L"PlainBytes Cancella metadati";
        case ShellLang::Es: return L"PlainBytes Eliminar metadatos";
        case ShellLang::Pt: return L"PlainBytes Remover metadados";
        case ShellLang::Nl: return L"PlainBytes Metagegevens verwijderen";
        case ShellLang::Ar: return L"PlainBytes مسح البيانات الوصفية";
        default: return L"PlainBytes Clear Metadata";
        }
    }

    // Appends one UTF-8 line to LocalAppData\...\logs\shell-yyyyMMdd.log (best effort).
    void LogLine(const wchar_t* text)
    {
        try
        {
            wchar_t localAppData[MAX_PATH] = {};
            if (GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, ARRAYSIZE(localAppData)) == 0)
                return;

            CreateDirectoryW((std::wstring(localAppData) + L"\\PlainBytes").c_str(), nullptr);
            CreateDirectoryW((std::wstring(localAppData) + L"\\PlainBytes\\MetadataScrubber").c_str(), nullptr);
            const std::wstring dir = std::wstring(localAppData) + L"\\PlainBytes\\MetadataScrubber\\logs";
            CreateDirectoryW(dir.c_str(), nullptr);

            SYSTEMTIME st{};
            GetLocalTime(&st);
            wchar_t file[MAX_PATH] = {};
            StringCchPrintfW(file, ARRAYSIZE(file),
                L"%s\\shell-%04u%02u%02u.log",
                dir.c_str(), st.wYear, st.wMonth, st.wDay);

            HANDLE h = CreateFileW(file, FILE_APPEND_DATA, FILE_SHARE_READ, nullptr, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
            if (h == INVALID_HANDLE_VALUE)
                return;

            wchar_t line[2048] = {};
            StringCchPrintfW(line, ARRAYSIZE(line), L"%02u:%02u:%02u %s\r\n", st.wHour, st.wMinute, st.wSecond, text);
            const int bytes = WideCharToMultiByte(CP_UTF8, 0, line, -1, nullptr, 0, nullptr, nullptr);
            if (bytes > 1)
            {
                std::string utf8(static_cast<size_t>(bytes - 1), '\0');
                WideCharToMultiByte(CP_UTF8, 0, line, -1, utf8.data(), bytes, nullptr, nullptr);
                DWORD written = 0;
                WriteFile(h, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
            }
            CloseHandle(h);
        }
        catch (...)
        {
        }
    }

    // True when the path extension is a supported Office/PDF type.
    bool IsSupportedExtension(PCWSTR path)
    {
        if (!path || !*path)
            return false;

        const wchar_t* ext = PathFindExtensionW(path);
        if (!ext || !*ext)
            return false;

        static const wchar_t* kExts[] = {
            L".docx", L".docm",
            L".xlsx", L".xlsm",
            L".pptx", L".pptm",
            L".pdf",
        };

        for (auto candidate : kExts)
        {
            if (_wcsicmp(ext, candidate) == 0)
                return true;
        }
        return false;
    }

    // If dir\PlainBytes.MetadataScrubber.exe exists, assigns it to outPath and returns true.
    bool TryAppendExe(wchar_t* dir, std::wstring& outPath)
    {
        wchar_t candidate[MAX_PATH] = {};
        if (FAILED(StringCchCopyW(candidate, ARRAYSIZE(candidate), dir)))
            return false;
        if (!PathAppendW(candidate, kAppExeName))
            return false;
        if (!PathFileExistsW(candidate))
            return false;
        outPath.assign(candidate);
        return true;
    }

    // Resolves the app EXE next to this DLL (or one directory up for ProjectReference layouts).
    bool GetAppExePath(std::wstring& outPath)
    {
        wchar_t modulePath[MAX_PATH] = {};
        const DWORD n = GetModuleFileNameW(g_hInst, modulePath, ARRAYSIZE(modulePath));
        if (n == 0 || n >= ARRAYSIZE(modulePath))
            return false;

        PathRemoveFileSpecW(modulePath);
        if (TryAppendExe(modulePath, outPath))
            return true;

        // ProjectReference may place the DLL in a subdirectory; also try the parent (package root).
        if (!PathRemoveFileSpecW(modulePath))
            return false;
        return TryAppendExe(modulePath, outPath);
    }

    // Builds PackageFamilyName!App AUMID when running inside an MSIX package.
    bool GetPackageAumid(std::wstring& aumid)
    {
        UINT32 length = 0;
        LONG rc = GetCurrentPackageFamilyName(&length, nullptr);
        if (rc != ERROR_INSUFFICIENT_BUFFER || length == 0)
            return false;

        std::wstring family(length, L'\0');
        rc = GetCurrentPackageFamilyName(&length, family.data());
        if (rc != ERROR_SUCCESS)
            return false;

        // API writes a null terminator into the buffer.
        while (!family.empty() && family.back() == L'\0')
            family.pop_back();

        aumid = family;
        aumid += L"!";
        aumid += kAppId;
        return !family.empty();
    }

    // Collects unique supported file paths from the shell selection; E_FAIL if none.
    HRESULT CollectPaths(IShellItemArray* items, std::vector<std::wstring>& paths)
    {
        paths.clear();
        if (!items)
            return E_INVALIDARG;

        DWORD count = 0;
        HRESULT hr = items->GetCount(&count);
        if (FAILED(hr))
            return hr;

        for (DWORD i = 0; i < count; ++i)
        {
            IShellItem* item = nullptr;
            hr = items->GetItemAt(i, &item);
            if (FAILED(hr) || !item)
                continue;

            PWSTR display = nullptr;
            hr = item->GetDisplayName(SIGDN_FILESYSPATH, &display);
            if (FAILED(hr) || !display)
            {
                if (display)
                {
                    CoTaskMemFree(display);
                    display = nullptr;
                }
                hr = item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, &display);
            }

            if (SUCCEEDED(hr) && display)
            {
                if (IsSupportedExtension(display))
                {
                    const std::wstring path(display);
                    const bool exists = std::any_of(paths.begin(), paths.end(), [&](const std::wstring& p) {
                        return _wcsicmp(p.c_str(), path.c_str()) == 0;
                    });
                    if (!exists)
                        paths.emplace_back(path);
                }
                CoTaskMemFree(display);
            }
            item->Release();
        }

        return paths.empty() ? E_FAIL : S_OK;
    }

    // Writes one UTF-8 path per line to a temp scrub-list-*.txt under LocalAppData.
    bool WritePathListFile(const std::vector<std::wstring>& paths, std::wstring& outListPath)
    {
        wchar_t localAppData[MAX_PATH] = {};
        if (GetEnvironmentVariableW(L"LOCALAPPDATA", localAppData, ARRAYSIZE(localAppData)) == 0)
            return false;

        CreateDirectoryW((std::wstring(localAppData) + L"\\PlainBytes").c_str(), nullptr);
        CreateDirectoryW((std::wstring(localAppData) + L"\\PlainBytes\\MetadataScrubber").c_str(), nullptr);
        const std::wstring dir = std::wstring(localAppData) + L"\\PlainBytes\\MetadataScrubber\\temp";
        CreateDirectoryW(dir.c_str(), nullptr);

        wchar_t file[MAX_PATH] = {};
        StringCchPrintfW(file, ARRAYSIZE(file), L"%s\\scrub-list-%llu.txt", dir.c_str(), GetTickCount64());

        HANDLE h = CreateFileW(file, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (h == INVALID_HANDLE_VALUE)
            return false;

        for (const auto& path : paths)
        {
            const int bytes = WideCharToMultiByte(CP_UTF8, 0, path.c_str(), -1, nullptr, 0, nullptr, nullptr);
            if (bytes <= 1)
                continue;

            std::string utf8(static_cast<size_t>(bytes - 1), '\0');
            WideCharToMultiByte(CP_UTF8, 0, path.c_str(), -1, utf8.data(), bytes, nullptr, nullptr);
            utf8.push_back('\n');
            DWORD written = 0;
            WriteFile(h, utf8.data(), static_cast<DWORD>(utf8.size()), &written, nullptr);
        }

        CloseHandle(h);
        outListPath.assign(file);
        return true;
    }

    // Builds --scrub-default args; uses --list for multi-select or very long argument lists.
    std::wstring BuildArguments(const std::vector<std::wstring>& paths)
    {
        // Multi-select or long args: use a list file to avoid ActivateApplication truncation/parse issues.
        size_t inlineLen = wcslen(kScrubFlag);
        for (const auto& path : paths)
            inlineLen += path.size() + 3;

        const bool useList = paths.size() > 1 || inlineLen > kMaxInlineArgsChars;
        if (useList)
        {
            std::wstring listPath;
            if (!WritePathListFile(paths, listPath))
            {
                LogLine(L"WritePathListFile failed; fallback inline args");
            }
            else
            {
                wchar_t msg[512] = {};
                StringCchPrintfW(msg, ARRAYSIZE(msg), L"multi-select count=%u list=%s",
                    static_cast<unsigned>(paths.size()), listPath.c_str());
                LogLine(msg);

                std::wstring args = kScrubFlag;
                args.push_back(L' ');
                args.append(kListFlag);
                args.push_back(L' ');
                args.push_back(L'"');
                args.append(listPath);
                args.push_back(L'"');
                return args;
            }
        }

        std::wstring args = kScrubFlag;
        for (const auto& path : paths)
        {
            args.push_back(L' ');
            args.push_back(L'"');
            args.append(path);
            args.push_back(L'"');
        }
        return args;
    }

    // Launches the packaged app via IApplicationActivationManager with the given args.
    bool LaunchViaPackageActivation(const std::wstring& args)
    {
        std::wstring aumid;
        if (!GetPackageAumid(aumid))
        {
            LogLine(L"GetCurrentPackageFamilyName failed (not packaged?)");
            return false;
        }

        wchar_t msg[512] = {};
        StringCchPrintfW(msg, ARRAYSIZE(msg), L"ActivateApplication AUMID=%s", aumid.c_str());
        LogLine(msg);

        IApplicationActivationManager* activator = nullptr;
        HRESULT hr = CoCreateInstance(
            CLSID_ApplicationActivationManager,
            nullptr,
            CLSCTX_LOCAL_SERVER,
            IID_PPV_ARGS(&activator));
        if (FAILED(hr) || !activator)
        {
            StringCchPrintfW(msg, ARRAYSIZE(msg), L"CoCreateInstance ApplicationActivationManager hr=0x%08X", hr);
            LogLine(msg);
            return false;
        }

        DWORD processId = 0;
        hr = activator->ActivateApplication(aumid.c_str(), args.c_str(), AO_NONE, &processId);
        activator->Release();

        StringCchPrintfW(msg, ARRAYSIZE(msg), L"ActivateApplication hr=0x%08X pid=%u", hr, processId);
        LogLine(msg);
        return SUCCEEDED(hr);
    }

    // Unpackaged fallback: CreateProcess on the resolved EXE with the given args.
    bool LaunchViaCreateProcess(const std::wstring& args)
    {
        std::wstring exe;
        if (!GetAppExePath(exe))
        {
            LogLine(L"GetAppExePath failed");
            return false;
        }

        std::wstring cmd;
        cmd.push_back(L'"');
        cmd.append(exe);
        cmd.push_back(L'"');
        cmd.push_back(L' ');
        cmd.append(args);

        std::vector<wchar_t> mutableCmd(cmd.begin(), cmd.end());
        mutableCmd.push_back(L'\0');

        STARTUPINFOW si = { sizeof(si) };
        PROCESS_INFORMATION pi = {};
        const BOOL ok = CreateProcessW(
            exe.c_str(),
            mutableCmd.data(),
            nullptr,
            nullptr,
            FALSE,
            0,
            nullptr,
            nullptr,
            &si,
            &pi);

        if (!ok)
        {
            wchar_t msg[256] = {};
            StringCchPrintfW(msg, ARRAYSIZE(msg), L"CreateProcess failed err=%u exe=%s", GetLastError(), exe.c_str());
            LogLine(msg);
            return false;
        }

        CloseHandle(pi.hThread);
        CloseHandle(pi.hProcess);
        LogLine(L"CreateProcess ok");
        return true;
    }

    // Prefers package activation, then falls back to CreateProcess.
    bool LaunchScrub(const std::vector<std::wstring>& paths)
    {
        const std::wstring args = BuildArguments(paths);
        LogLine(args.c_str());

        // Store/MSIX must use package activation; CreateProcess often does nothing or lacks identity.
        if (LaunchViaPackageActivation(args))
            return true;

        return LaunchViaCreateProcess(args);
    }

    struct InvokePayload
    {
        std::vector<std::wstring> paths;
    };

    // Background thread entry: initializes COM, runs LaunchScrub, frees the payload.
    DWORD WINAPI InvokeThreadProc(void* param)
    {
        // ActivateApplication requires COM initialized on this thread.
        CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);

        auto* payload = static_cast<InvokePayload*>(param);
        if (payload)
        {
            LaunchScrub(payload->paths);
            delete payload;
        }

        CoUninitialize();
        return 0;
    }
}

// Increments the DLL lock count (objects / LockServer).
void DllAddRef()
{
    InterlockedIncrement(&g_cDllRef);
}

// Decrements the DLL lock count.
void DllRelease()
{
    InterlockedDecrement(&g_cDllRef);
}

// IExplorerCommand: "Clear Metadata" verb that launches headless scrub for the selection.
class ScrubExplorerCommand final : public IExplorerCommand
{
public:
    ScrubExplorerCommand() : _cRef(1)
    {
        DllAddRef();
    }

    // COM IUnknown: QI for IExplorerCommand.
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        static const QITAB qit[] = {
            QITABENT(ScrubExplorerCommand, IExplorerCommand),
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_cRef);
    }

    // Drops the last reference and deletes this command object.
    IFACEMETHODIMP_(ULONG) Release() override
    {
        const long c = InterlockedDecrement(&_cRef);
        if (c == 0)
            delete this;
        return c;
    }

    // Localized menu title; appends "(N)" when multiple supported files are selected.
    IFACEMETHODIMP GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName) override
    {
        const std::wstring titleBase = LocalizeMenuTitle();
        std::vector<std::wstring> paths;
        if (SUCCEEDED(CollectPaths(psiItemArray, paths)) && paths.size() > 1)
        {
            wchar_t title[160] = {};
            StringCchPrintfW(title, ARRAYSIZE(title), L"%s (%u)", titleBase.c_str(), static_cast<unsigned>(paths.size()));
            return SHStrDupW(title, ppszName);
        }
        return SHStrDupW(titleBase.c_str(), ppszName);
    }

    // Returns "modulePath,-IDI_APP" for the shell to load the embedded icon.
    IFACEMETHODIMP GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon) override
    {
        *ppszIcon = nullptr;
        wchar_t modulePath[MAX_PATH] = {};
        if (GetModuleFileNameW(g_hInst, modulePath, ARRAYSIZE(modulePath)) == 0)
            return E_FAIL;

        // Use the icon resource embedded in this DLL (shell.rc IDI_APP=101).
        wchar_t iconRef[MAX_PATH + 16] = {};
        if (FAILED(StringCchPrintfW(iconRef, ARRAYSIZE(iconRef), L"%s,-%d", modulePath, IDI_APP)))
            return E_FAIL;

        return SHStrDupW(iconRef, ppszIcon);
    }

    // No tooltip implemented.
    IFACEMETHODIMP GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfotip) override
    {
        *ppszInfotip = nullptr;
        return E_NOTIMPL;
    }

    // Canonical command GUID (this class CLSID).
    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) override
    {
        *pguidCommandName = __uuidof(ScrubExplorerCommand);
        return S_OK;
    }

    // True if the selection contains at least one supported document path.
    bool HasAnySupportedFile(IShellItemArray* items)
    {
        if (!items)
            return false;

        DWORD count = 0;
        if (FAILED(items->GetCount(&count)) || count == 0)
            return false;

        for (DWORD i = 0; i < count; ++i)
        {
            IShellItem* item = nullptr;
            if (FAILED(items->GetItemAt(i, &item)) || !item)
                continue;

            PWSTR display = nullptr;
            HRESULT hr = item->GetDisplayName(SIGDN_FILESYSPATH, &display);
            if (FAILED(hr) || !display)
            {
                if (display)
                {
                    CoTaskMemFree(display);
                    display = nullptr;
                }
                hr = item->GetDisplayName(SIGDN_DESKTOPABSOLUTEPARSING, &display);
            }

            bool ok = false;
            if (SUCCEEDED(hr) && display)
            {
                ok = IsSupportedExtension(display);
                CoTaskMemFree(display);
            }
            item->Release();

            if (ok)
                return true;
        }

        return false;
    }

    // Shows the verb only when the selection includes a supported file.
    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pCmdState) override
    {
        // With Type=*, visibility is decided here: show only if selection includes a supported file.
        *pCmdState = HasAnySupportedFile(psiItemArray) ? ECS_ENABLED : ECS_HIDDEN;
        return S_OK;
    }

    // Collects paths and launches scrub on a worker thread (or inline if CreateThread fails).
    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pbc*/) override
    {
        auto* payload = new (std::nothrow) InvokePayload();
        if (!payload)
            return E_OUTOFMEMORY;

        if (FAILED(CollectPaths(psiItemArray, payload->paths)))
        {
            delete payload;
            LogLine(L"Invoke: no supported paths");
            return S_OK;
        }

        // Do not block the UI thread; activate the app on a background thread.
        const HANDLE thread = CreateThread(nullptr, 0, InvokeThreadProc, payload, 0, nullptr);
        if (!thread)
        {
            LaunchScrub(payload->paths);
            delete payload;
            return S_OK;
        }

        CloseHandle(thread);
        return S_OK;
    }

    // Default explorer command flags (no special ECF bits).
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override
    {
        *pFlags = ECF_DEFAULT;
        return S_OK;
    }

    // No subcommands.
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override
    {
        *ppEnum = nullptr;
        return E_NOTIMPL;
    }

private:
    ~ScrubExplorerCommand()
    {
        DllRelease();
    }

    long _cRef;
};

// Creates a ScrubExplorerCommand and returns the requested interface.
HRESULT ScrubExplorerCommand_CreateInstance(REFIID riid, void** ppv)
{
    *ppv = nullptr;
    auto* obj = new (std::nothrow) ScrubExplorerCommand();
    if (!obj)
        return E_OUTOFMEMORY;

    const HRESULT hr = obj->QueryInterface(riid, ppv);
    obj->Release();
    return hr;
}

// COM class factory for ScrubExplorerCommand.
class ClassFactory final : public IClassFactory
{
public:
    ClassFactory() : _cRef(1)
    {
        DllAddRef();
    }

    // COM IUnknown: QI for IClassFactory.
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        static const QITAB qit[] = {
            QITABENT(ClassFactory, IClassFactory),
            { 0 },
        };
        return QISearch(this, qit, riid, ppv);
    }

    IFACEMETHODIMP_(ULONG) AddRef() override
    {
        return InterlockedIncrement(&_cRef);
    }

    IFACEMETHODIMP_(ULONG) Release() override
    {
        const long c = InterlockedDecrement(&_cRef);
        if (c == 0)
            delete this;
        return c;
    }

    // Creates ScrubExplorerCommand; aggregation is not supported.
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppv) override
    {
        *ppv = nullptr;
        if (pUnkOuter)
            return CLASS_E_NOAGGREGATION;
        return ScrubExplorerCommand_CreateInstance(riid, ppv);
    }

    // Keeps the DLL loaded while fLock is TRUE.
    IFACEMETHODIMP LockServer(BOOL fLock) override
    {
        if (fLock)
            DllAddRef();
        else
            DllRelease();
        return S_OK;
    }

private:
    ~ClassFactory()
    {
        DllRelease();
    }

    long _cRef;
};

// DLL entry: stores HINSTANCE on process attach.
BOOL APIENTRY DllMain(HMODULE hModule, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_hInst = hModule;
        DisableThreadLibraryCalls(hModule);
    }
    return TRUE;
}

// S_OK when no outstanding object/server locks remain.
STDAPI DllCanUnloadNow()
{
    return g_cDllRef > 0 ? S_FALSE : S_OK;
}

// Returns the class factory for ScrubExplorerCommand only.
STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    *ppv = nullptr;
    if (rclsid != __uuidof(ScrubExplorerCommand))
        return CLASS_E_CLASSNOTAVAILABLE;

    auto* factory = new (std::nothrow) ClassFactory();
    if (!factory)
        return E_OUTOFMEMORY;

    const HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}
