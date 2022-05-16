using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LogMeOn;

public class ProcessInfo
{
    public uint ProcessId, ParentProcessId;
    public string Name, Caption;
    public string CommandLine, ExecutablePath;
    public Priority Priority;
    public ulong KernelModeTime, UserModeTime;

    public ProcessInfo(ManagementBaseObject process)
    {
        ProcessId = (uint)process["ProcessId"];
        ParentProcessId = (uint)process["ParentProcessId"];
        Name = (string)process["Name"];
        Caption = (string)process["Caption"];
        CommandLine = ((string)process["CommandLine"])?.Trim();
        ExecutablePath = (string)process["ExecutablePath"];
        Priority = (Priority)(uint)process["Priority"];
        KernelModeTime = (ulong)process["KernelModeTime"];
        UserModeTime = (ulong)process["UserModeTime"];
    }

    public override string ToString()
    {
        return $"[{ProcessId}] {Name} ({Priority}) - {CommandLine}";
    }

    public static List<ProcessInfo> GetProcesses()
    {
        var results = new List<ProcessInfo>();
        using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process"))
        using (var matches = searcher.Get())
            foreach (var match in matches)
                results.Add(new ProcessInfo(match));
        return results;
    }

    public static ProcessInfo GetProcess(uint processId)
    {
        using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Process WHERE ProcessId = {processId}"))
        using (var matches = searcher.Get())
            foreach (var match in matches)
                return new ProcessInfo(match);
        return null;
    }
}

public class ServiceInfo
{
    private ManagementObject _service;

    public uint ProcessId { get { return (uint)_service["ProcessId"]; } }
    public string Name { get { return (string)_service["Name"]; } }
    public string DisplayName { get { return (string)_service["DisplayName"]; } }
    public string Caption { get { return (string)_service["Caption"]; } }
    public bool AcceptPause { get { return (bool)_service["AcceptPause"]; } }
    public bool AcceptStop { get { return (bool)_service["AcceptStop"]; } }
    public bool DelayedAutoStart { get { return (bool)_service["DelayedAutoStart"]; } }
    public bool Started { get { return (bool)_service["Started"]; } }

    public ServiceInfo(ManagementBaseObject service)
    {
        _service = (ManagementObject)service;
    }

    public override string ToString()
    {
        return $"[{ProcessId}] {Name} ({DisplayName}) - {Caption}";
    }

    public static List<ServiceInfo> GetServices()
    {
        var results = new List<ServiceInfo>();
        using (var searcher = new ManagementObjectSearcher($"SELECT * FROM Win32_Service"))
        using (var matches = searcher.Get())
            foreach (var match in matches)
                results.Add(new ServiceInfo(match));
        return results;
    }

    public void StartService()
    {
        _service.InvokeMethod("StartService", new object[0]);
    }

    public void StopService()
    {
        _service.InvokeMethod("StopService", new object[0]);
    }
}

public enum Priority
{
    Idle = 4,
    BelowNormal = 6,
    Normal = 8,
    AboveNormal = 10,
    High = 13,
    RealTime = 24,
}

static class WinAPI
{
    /// <summary>
    ///     Enables or disables the specified privilege on the primary access token of the current process.</summary>
    /// <param name="privilege">
    ///     Privilege to enable or disable.</param>
    /// <param name="enable">
    ///     True to enable the privilege, false to disable it.</param>
    /// <returns>
    ///     True if the privilege was enabled prior to the change, false if it was disabled.</returns>
    public static bool ModifyPrivilege(PrivilegeName privilege, bool enable)
    {
        LUID luid;
        if (!LookupPrivilegeValue(null, privilege.ToString(), out luid))
            throw new Win32Exception();

        using (var identity = WindowsIdentity.GetCurrent(TokenAccessLevels.AdjustPrivileges | TokenAccessLevels.Query))
        {
            var newPriv = new TOKEN_PRIVILEGES();
            newPriv.Privileges = new LUID_AND_ATTRIBUTES[1];
            newPriv.PrivilegeCount = 1;
            newPriv.Privileges[0].Luid = luid;
            newPriv.Privileges[0].Attributes = enable ? SE_PRIVILEGE_ENABLED : 0;

            var prevPriv = new TOKEN_PRIVILEGES();
            prevPriv.Privileges = new LUID_AND_ATTRIBUTES[1];
            prevPriv.PrivilegeCount = 1;
            uint returnedBytes;

            if (!AdjustTokenPrivileges(identity.Token, false, ref newPriv, (uint)Marshal.SizeOf(prevPriv), ref prevPriv, out returnedBytes))
                throw new Win32Exception();

            return prevPriv.PrivilegeCount == 0 ? enable /* didn't make a change */ : ((prevPriv.Privileges[0].Attributes & SE_PRIVILEGE_ENABLED) != 0);
        }
    }

    public static ProcessPriorityClass PriorityToClass(Priority priority)
    {
        switch (priority)
        {
            case Priority.Idle: return ProcessPriorityClass.Idle;
            case Priority.BelowNormal: return ProcessPriorityClass.BelowNormal;
            case Priority.Normal: return ProcessPriorityClass.Normal;
            case Priority.AboveNormal: return ProcessPriorityClass.AboveNormal;
            case Priority.High: return ProcessPriorityClass.High;
            case Priority.RealTime: return ProcessPriorityClass.RealTime;
            default: throw new Exception("Unrecognized priority");
        }
    }

    const uint SE_PRIVILEGE_ENABLED = 2;

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, [MarshalAs(UnmanagedType.Bool)] bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState,
        UInt32 BufferLengthInBytes, ref TOKEN_PRIVILEGES PreviousState, out UInt32 ReturnLengthInBytes);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);

    struct TOKEN_PRIVILEGES
    {
        public uint PrivilegeCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1 /*ANYSIZE_ARRAY*/)]
        public LUID_AND_ATTRIBUTES[] Privileges;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LUID_AND_ATTRIBUTES
    {
        public LUID Luid;
        public UInt32 Attributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [DllImport("shell32.dll", SetLastError = true)]
    static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine, out int pNumArgs);

    public static string[] CommandLineToArgs(string commandLine)
    {
        // http://stackoverflow.com/a/749653/33080
        int argc;
        var argv = CommandLineToArgvW(commandLine, out argc);
        if (argv == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception();
        try
        {
            var args = new string[argc];
            for (var i = 0; i < args.Length; i++)
            {
                var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                args[i] = Marshal.PtrToStringUni(p);
            }

            return args;
        }
        finally
        {
            Marshal.FreeHGlobal(argv);
        }
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool PostThreadMessage(uint threadId, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr lParam);

    public static List<IntPtr> SendMessageToTopLevelWindows(uint processId, bool sendNotPost, uint msg, IntPtr wParam, IntPtr lParam)
    {
        var results = new List<IntPtr>();
        EnumWindows((IntPtr wnd, IntPtr _) =>
        {
            uint pid;
            uint threadId = GetWindowThreadProcessId(wnd, out pid);
            if (pid == processId)
            {
                if (sendNotPost)
                    SendMessage(wnd, msg, wParam, lParam);
                else
                    PostThreadMessage(threadId, msg, wParam, lParam);
                results.Add(wnd);
            }
            return true;
        }, IntPtr.Zero);
        return results;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct SECURITY_ATTRIBUTES
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO
    {
        public Int32 cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public Int32 dwX;
        public Int32 dwY;
        public Int32 dwXSize;
        public Int32 dwYSize;
        public Int32 dwXCountChars;
        public Int32 dwYCountChars;
        public Int32 dwFillAttribute;
        public Int32 dwFlags;
        public Int16 wShowWindow;
        public Int16 cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("advapi32", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcessWithTokenW(
        IntPtr hToken,
        int dwLogonFlags,
        string lpApplicationName,
        string lpCommandLine,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        ref SECURITY_ATTRIBUTES lpProcessAttributes,
        ref SECURITY_ATTRIBUTES lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        [In] ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("user32.dll")]
    public static extern IntPtr GetShellWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint processAccess, bool bInheritHandle, uint processId);

    [DllImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public extern static bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int ImpersonationLevel,
        int TokenType,
        out IntPtr phNewToken);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool CloseHandle(IntPtr hObject);
}

enum PrivilegeName
{
    SeAssignPrimaryTokenPrivilege,
    SeAuditPrivilege,
    SeBackupPrivilege,
    SeChangeNotifyPrivilege,
    SeCreateGlobalPrivilege,
    SeCreatePagefilePrivilege,
    SeCreatePermanentPrivilege,
    SeCreateSymbolicLinkPrivilege,
    SeCreateTokenPrivilege,
    SeDebugPrivilege,
    SeEnableDelegationPrivilege,
    SeImpersonatePrivilege,
    SeIncreaseBasePriorityPrivilege,
    SeIncreaseQuotaPrivilege,
    SeIncreaseWorkingSetPrivilege,
    SeLoadDriverPrivilege,
    SeLockMemoryPrivilege,
    SeMachineAccountPrivilege,
    SeManageVolumePrivilege,
    SeProfileSingleProcessPrivilege,
    SeRelabelPrivilege,
    SeRemoteShutdownPrivilege,
    SeRestorePrivilege,
    SeSecurityPrivilege,
    SeShutdownPrivilege,
    SeSyncAgentPrivilege,
    SeSystemEnvironmentPrivilege,
    SeSystemProfilePrivilege,
    SeSystemtimePrivilege,
    SeTakeOwnershipPrivilege,
    SeTcbPrivilege,
    SeTimeZonePrivilege,
    SeTrustedCredManAccessPrivilege,
    SeUndockPrivilege,
    SeUnsolicitedInputPrivilege,
}
