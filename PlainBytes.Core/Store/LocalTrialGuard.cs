// SPDX-FileCopyrightText: 2026 PlainBytes Studio
// SPDX-License-Identifier: GPL-3.0-only
using System.Runtime.InteropServices;
using System.Text;

namespace PlainBytes.Core.Store;

/// <summary>
/// Persists first-run UTC timestamp in Windows Credential Manager for calendar-day local trial counting.
/// </summary>
internal static class LocalTrialGuard
{
    private const int CredMaxUsernameLength = 513;
    private const int CredMaxCredentialDomainLength = 337;
    private const int CredMaxGenericCredentialBlobSize = 512;

    /// <summary>
    /// Returns the stored first-run instant, or records and returns <see cref="DateTimeOffset.UtcNow"/> on first access.
    /// </summary>
    /// <param name="credentialTarget">Credential Manager target (see <see cref="SuiteProductInfo.TrialCredentialTarget"/>).</param>
    public static DateTimeOffset EnsureFirstRunUtc(string credentialTarget)
    {
        if (TryReadFirstRunUtc(credentialTarget, out var existing))
            return existing;

        var now = DateTimeOffset.UtcNow;
        WriteFirstRunUtc(credentialTarget, now);
        return now;
    }

    /// <summary>
    /// Remaining trial days from <paramref name="trialDays"/> based on UTC calendar days since first run.
    /// </summary>
    public static int GetTrialDaysRemaining(string credentialTarget, int trialDays)
    {
        var firstRun = EnsureFirstRunUtc(credentialTarget);
        var firstDate = firstRun.UtcDateTime.Date;
        var today = DateTimeOffset.UtcNow.UtcDateTime.Date;
        var daysUsed = (today - firstDate).Days;
        return Math.Max(0, trialDays - daysUsed);
    }

    /// <summary>Reads ISO-8601 UTC timestamp from a generic credential blob.</summary>
    private static bool TryReadFirstRunUtc(string credentialTarget, out DateTimeOffset firstRunUtc)
    {
        firstRunUtc = default;
        if (!CredRead(credentialTarget, CRED_TYPE.GENERIC, 0, out var credPtr) || credPtr == IntPtr.Zero)
            return false;

        try
        {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr)!;
            if (cred.CredentialBlobSize <= 0 || cred.CredentialBlob == IntPtr.Zero)
                return false;

            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, bytes.Length);
            var text = Encoding.UTF8.GetString(bytes).Trim('\0', ' ', '\r', '\n', '\t');
            return DateTimeOffset.TryParse(
                text,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out firstRunUtc);
        }
        finally
        {
            CredFree(credPtr);
        }
    }

    /// <summary>Writes first-run UTC as a generic credential persisted for the local machine.</summary>
    private static void WriteFirstRunUtc(string credentialTarget, DateTimeOffset firstRunUtc)
    {
        var blob = Encoding.UTF8.GetBytes(firstRunUtc.ToString("O"));
        if (blob.Length > CredMaxGenericCredentialBlobSize)
            throw new InvalidOperationException("Trial guard payload too large.");

        var cred = new CREDENTIAL
        {
            Type = CRED_TYPE.GENERIC,
            Persist = CRED_PERSIST.LOCAL_MACHINE,
            TargetName = credentialTarget,
            UserName = Environment.UserName,
            CredentialBlobSize = (uint)blob.Length,
            CredentialBlob = Marshal.AllocCoTaskMem(blob.Length),
            Comment = "PlainBytes trial first-run UTC",
        };

        try
        {
            Marshal.Copy(blob, 0, cred.CredentialBlob, blob.Length);
            if (!CredWrite(ref cred, 0))
                throw new InvalidOperationException($"CredWrite failed: {Marshal.GetLastWin32Error()}");
        }
        finally
        {
            Marshal.FreeCoTaskMem(cred.CredentialBlob);
        }
    }

    private enum CRED_TYPE : uint
    {
        GENERIC = 1,
    }

    private enum CRED_PERSIST : uint
    {
        SESSION = 1,
        LOCAL_MACHINE = 2,
        ENTERPRISE = 3,
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public CRED_TYPE Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public CRED_PERSIST Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string UserName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint DwLowDateTime;
        public uint DwHighDateTime;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref CREDENTIAL credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, CRED_TYPE type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr cred);
}
