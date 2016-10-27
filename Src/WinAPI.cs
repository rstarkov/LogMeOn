using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace LogMeOn
{
    public class ProcessInfo
    {
        public uint ProcessId, ParentProcessId;
        public string Name, Caption;
        public string CommandLine, ExecutablePath;
        public Priority Priority;
        public ulong KernelModeTime, UserModeTime;

        public ProcessInfo(ManagementBaseObject process)
        {
            ProcessId = (uint) process["ProcessId"];
            ParentProcessId = (uint) process["ParentProcessId"];
            Name = (string) process["Name"];
            Caption = (string) process["Caption"];
            CommandLine = ((string) process["CommandLine"])?.Trim();
            ExecutablePath = (string) process["ExecutablePath"];
            Priority = (Priority) (uint) process["Priority"];
            KernelModeTime = (ulong) process["KernelModeTime"];
            UserModeTime = (ulong) process["UserModeTime"];
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

                if (!AdjustTokenPrivileges(identity.Token, false, ref newPriv, (uint) Marshal.SizeOf(prevPriv), ref prevPriv, out returnedBytes))
                    throw new Win32Exception();

                return prevPriv.PrivilegeCount == 0 ? enable /* didn't make a change */ : ((prevPriv.Privileges[0].Attributes & SE_PRIVILEGE_ENABLED) != 0);
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
            public UInt32 PrivilegeCount;
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
}
