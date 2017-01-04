using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace LogMeOn
{
    partial class Logmeon
    {
        /// <summary>Controls a process.</summary>
        public class Process
        {
            /// <summary>Gets the friendly name of the service as used for logging purposes only.</summary>
            public string Name { get; private set; }
            /// <summary>
            ///     Gets the command line associated with the process as used for starting and locating running instances of
            ///     the process.</summary>
            public string[] Args { get; private set; }

            /// <summary>
            ///     Gets the time at which <see cref="Run"/> was invoked and completed without errors. Null if it has never
            ///     completed successfully on the current instance. Do not confuse with the start time of the underlying OS
            ///     process; this property relates only to the Logmeon concept.</summary>
            public DateTime? StartedAt { get; private set; } = null;

            /// <summary>
            ///     Gets the time at which <see cref="Kill"/> was invoked. Null if it has never been invoked on the current
            ///     instance. Do not confuse with the end time of any underlying OS process; this property relates only to the
            ///     Logmeon concept.</summary>
            public DateTime? KilledAt { get; private set; } = null;

            private TimeSpan _waitBeforeAction;
            private bool _elevated;
            private string[] _startCommand;

            /// <summary>
            ///     Creates a process controller for the specified process. Does not perform any actions whatsoever; does not
            ///     check the validity of the arguments or the existence of the specified process / executable file. By
            ///     default the process is started non-elevated; see <see cref="Elevated"/>.</summary>
            /// <param name="name">
            ///     Friendly name for this process to be used for logging purposes.</param>
            /// <param name="args">
            ///     Command line arguments associated with this process. If the process is to be started, this is the command
            ///     line executed. If running instances need to be found, they are identified by the command line - not just
            ///     the name of the executable but also the arguments. Note that all arguments other than the first one
            ///     (executable name) are case-sensitive. You must use full paths for this class to operate correctly.</param>
            public Process(string name, params string[] args)
            {
                Name = name;
                Args = args;
                _waitBeforeAction = Logmeon.WaitBeforeAction;
            }

            private List<ProcessInfo> find()
            {
                return ProcessInfo.GetProcesses().Where(p => equalCommandLine(p.CommandLine, Args)).ToList();
            }

            private static bool equalCommandLine(string commandLine, string[] args)
            {
                if (commandLine == null)
                    return false;
                var argsLine = CommandRunner.ArgsToCommandLine(args);
                if (commandLine == argsLine)
                    return true;
                var commands = WinAPI.CommandLineToArgs(commandLine);
                // first parameter is case-insensitive; the rest are case-sensitive
                return args.Zip(commands, (a, c) => new { a, c }).Select((p, i) => i == 0 ? p.a.EqualsNoCase(p.c) : (p.a == p.c)).All(x => x);
            }

            /// <summary>Overrides <see cref="Logmeon.WaitBeforeAction"/> for this specific service only. Chainable.</summary>
            public Process WaitBeforeAction(TimeSpan time)
            {
                _waitBeforeAction = time;
                return this;
            }

            /// <summary>
            ///     Configures this process to start elevated. By default new processes are started un-elevated (or, more
            ///     precisely, with the same elevation status as the user's desktop shell).</summary>
            public Process Elevated()
            {
                _elevated = true;
                return this;
            }

            /// <summary>
            ///     Configures this process to start by running a different program, such as a launcher. If specified, the
            ///     main process command is used only to detect whether the process is running.</summary>
            public Process WithStartCommand(params string[] args)
            {
                _startCommand = args;
                return this;
            }

            /// <summary>
            ///     Ensures the process is in the desired state - running or stopped. Does nothing if the process is already
            ///     in the desired state; otherwise calls <see cref="Run"/> or <see cref="Kill"/> as required. See
            ///     documentation for these two methods as well as <see cref="IsRunning"/> for the exact semantics. Chainable.</summary>
            public Process SetRunning(bool shouldBeRunning)
            {
                if (IsRunning != shouldBeRunning)
                {
                    if (shouldBeRunning)
                        Run();
                    else
                        Kill();
                }
                return this;
            }

            /// <summary>
            ///     Returns true if at least one instance of this process is running, with the exact same executable name
            ///     (case-insensitive) and command line arguments (case-sensitive); false otherwise.</summary>
            public bool IsRunning
            {
                get
                {
                    return find().Any();
                }
            }

            /// <summary>
            ///     Starts a new instance of this process unconditionally (regardless of whether one is already running). See
            ///     also <see cref="SetRunning"/>. Verifies that the executable exists and logs an error if not. Pauses for a
            ///     configurable interval before starting the process to allow the script to be interrupted if desired (see
            ///     <see cref="WaitBeforeAction"/>). To verify that the process didn't exit a few seconds later, see <see
            ///     cref="Logmeon.CheckStarted"/>. Chainable.</summary>
            public Process Run()
            {
                var args = _startCommand ?? Args;
                if (!File.Exists(args[0]))
                {
                    WriteLineColored($"{{green}}{Name}{{}}: {{red}}file not found: {{}}{{yellow}}{args[0]}{{}}");
                    Logmeon.AnyFailures = true;
                    return this;
                }
                WriteColored($"{{green}}{Name}{{}}: starting process in {_waitBeforeAction.TotalSeconds:0} seconds... ");
                Thread.Sleep(_waitBeforeAction);

                int processId;
                try
                {
                    var hToken = IntPtr.Zero;
                    var pi = new WinAPI.PROCESS_INFORMATION();
                    var hShellProcess = IntPtr.Zero;
                    var hShellToken = IntPtr.Zero;
                    try
                    {
                        var si = new WinAPI.STARTUPINFO();
                        si.cb = Marshal.SizeOf(si);
                        var processAttributes = new WinAPI.SECURITY_ATTRIBUTES();
                        var threadAttributes = new WinAPI.SECURITY_ATTRIBUTES();

                        if (_elevated)
                        {
                            WriteColored($"starting elevated... ");
                            if (!WinAPI.CreateProcess(null, CommandRunner.ArgsToCommandLine(args), ref processAttributes, ref threadAttributes, false, 0, IntPtr.Zero, null, ref si, out pi))
                                throw new Win32Exception();
                        }
                        else
                        {
                            WriteColored($"starting... ");
                            var shellWnd = WinAPI.GetShellWindow();
                            if (shellWnd == IntPtr.Zero)
                                throw new Exception("Could not find shell window");

                            uint shellProcessId;
                            WinAPI.GetWindowThreadProcessId(shellWnd, out shellProcessId);

                            hShellProcess = WinAPI.OpenProcess(0x00000400 /* QueryInformation */, false, shellProcessId);

                            if (!WinAPI.OpenProcessToken(hShellProcess, 2 /* TOKEN_DUPLICATE */, out hShellToken))
                                throw new Win32Exception();

                            uint tokenAccess = 8 /*TOKEN_QUERY*/ | 1 /*TOKEN_ASSIGN_PRIMARY*/ | 2 /*TOKEN_DUPLICATE*/ | 0x80 /*TOKEN_ADJUST_DEFAULT*/ | 0x100 /*TOKEN_ADJUST_SESSIONID*/;
                            WinAPI.DuplicateTokenEx(hShellToken, tokenAccess, IntPtr.Zero, 2 /* SecurityImpersonation */, 1 /* TokenPrimary */, out hToken);

                            if (!WinAPI.CreateProcessWithTokenW(hToken, 0, null, CommandRunner.ArgsToCommandLine(args), 0, IntPtr.Zero, null, ref si, out pi))
                                throw new Win32Exception();
                        }
                        processId = pi.dwProcessId;
                    }
                    finally
                    {
                        if (hToken != IntPtr.Zero && !WinAPI.CloseHandle(hToken))
                            throw new Win32Exception();
                        if (hShellToken != IntPtr.Zero && !WinAPI.CloseHandle(hShellToken))
                            throw new Win32Exception();
                        if (hShellProcess != IntPtr.Zero && !WinAPI.CloseHandle(hShellProcess))
                            throw new Win32Exception();
                        if (pi.hProcess != IntPtr.Zero && !WinAPI.CloseHandle(pi.hProcess))
                            throw new Win32Exception();
                        if (pi.hThread != IntPtr.Zero && !WinAPI.CloseHandle(pi.hThread))
                            throw new Win32Exception();
                    }
                }
                catch (Exception e)
                {
                    WriteLineColored($"{{green}}{Name}{{}}: {{red}}ERROR while starting process: {e.Message}{{}}");
                    AnyFailures = true;
                    return this;
                }

                WriteLineColored($"done, ID={processId}.");
                _startedProcesses.Add(this);
                StartedAt = DateTime.UtcNow;
                return this;
            }

            /// <summary>
            ///     Kills all running instances of this process (matching by <see cref="Args"/> as documented in <see
            ///     cref="IsRunning"/>). For each running instance found, also kills all child processes. For each process to
            ///     be killed, this method first attempts a graceful shutdown, escalating to less graceful shutdowns if the
            ///     process is still running after a configurable timeout (see <see cref="Logmeon.WaitForProcessShutdown"/>).
            ///     Logs a failure if any of the processes could not be verified as terminated. Chainable.</summary>
            public Process Kill()
            {
                var processes = ProcessInfo.GetProcesses().ToDictionary(p => p.ProcessId);
                foreach (var p in find())
                    kill(p.Name, p.ProcessId, processes);
                KilledAt = DateTime.UtcNow;
                return this;
            }

            private void kill(string name, uint processId, Dictionary<uint, ProcessInfo> processes)
            {
                WriteColored($"{{green}}{name}{{}}: shutting down process {{cyan}}ID {processId}{{}} nicely... ");
                WinAPI.SendMessageToTopLevelWindows(processId, true, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                bool success = true;
                if (!WaitFor(() => ProcessInfo.GetProcess(processId) == null, Logmeon.WaitForProcessShutdown))
                {
                    WriteColored($"less nicely... ");
                    WinAPI.SendMessageToTopLevelWindows(12052, false, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
                    if (!WaitFor(() => ProcessInfo.GetProcess(processId) == null, Logmeon.WaitForProcessShutdown))
                    {
                        WriteColored($"killing... ");
                        LogAndSuppressException(() => { System.Diagnostics.Process.GetProcessById((int) processId).Kill(); });
                        if (!WaitFor(() => ProcessInfo.GetProcess(processId) == null, Logmeon.WaitForProcessShutdown))
                        {
                            WriteLineColored($"{{red}}failed.{{}}");
                            success = false;
                            Logmeon.AnyFailures = true;
                        }
                    }
                }
                if (success)
                    WriteLineColored($"{{green}}done.{{}}");

                foreach (var child in processes.Values.Where(p => p.ParentProcessId == processId))
                    kill(name, child.ProcessId, processes);
            }

            /// <summary>
            ///     Sets the priority of all running instances of this process (matching by <see cref="Args"/> as documented
            ///     in <see cref="IsRunning"/>). Does not (currently) set the priority of child processes. Does not
            ///     (currently) check that the change took effect. Chainable.</summary>
            public Process Priority(Priority priority)
            {
                foreach (var p in find())
                {
                    if (p.Priority != priority)
                    {
                        WriteLineColored($"{{green}}{Name}{{}}: priority of process {{cyan}}ID {p.ProcessId}{{}} changed from {{yellow}}{p.Priority}{{}} to {{yellow}}{priority}{{}}.");
                        LogAndSuppressException(() => { System.Diagnostics.Process.GetProcessById((int) p.ProcessId).PriorityClass = WinAPI.PriorityToClass(priority); });
                    }
                }
                return this;
            }
        }
    }
}
