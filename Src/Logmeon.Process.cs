using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace LogMeOn
{
    partial class Logmeon
    {
        public class Process
        {
            public string Name { get; private set; }
            public string[] Args { get; private set; }

            private TimeSpan _waitBeforeAction;

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

            public Process WaitBeforeAction(TimeSpan time)
            {
                _waitBeforeAction = time;
                return this;
            }

            public Process Running(bool shouldBeRunning)
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

            public bool IsRunning
            {
                get
                {
                    return find().Any();
                }
            }

            public Process Run()
            {
                if (!File.Exists(Args[0]))
                {
                    WriteLineColored($"{{green}}{Name}{{}}: {{red}}file not found: {{}}{{yellow}}{Args[0]}{{}}");
                    Logmeon.AnyFailures = true;
                    return this;
                }
                WriteColored($"{{green}}{Name}{{}}: starting process in {_waitBeforeAction.TotalSeconds:0} seconds... ");
                Thread.Sleep(_waitBeforeAction);
                WriteLineColored($"done.");
                var runner = new CommandRunner();
                runner.SetCommand(Args);
                runner.Start();
                return this;
            }

            public Process Kill()
            {
                var processes = ProcessInfo.GetProcesses().ToDictionary(p => p.ProcessId);
                foreach (var p in find())
                    kill(p.Name, p.ProcessId, processes);
                return this;
            }

            private void kill(string name, uint processId, Dictionary<uint, ProcessInfo> processes)
            {
                WriteColored($"{{green}}{name}{{}}: shutting down process {{cyan}}ID {processId}{{}} nicely... ");
                WinAPI.SendMessageToTopLevelWindows(processId, true, 0x0010 /* WM_CLOSE */, IntPtr.Zero, IntPtr.Zero);
                bool success = true;
                if (!waitProcessDead(processId))
                {
                    WriteColored($"less nicely... ");
                    WinAPI.SendMessageToTopLevelWindows(12052, false, 0x0012 /* WM_QUIT */, IntPtr.Zero, IntPtr.Zero);
                    if (!waitProcessDead(processId))
                    {
                        WriteColored($"killing... ");
                        LogAndSuppressException(() => { System.Diagnostics.Process.GetProcessById((int) processId).Kill(); });
                        if (!waitProcessDead(processId))
                        {
                            WriteLineColored($"{{red}}failed.{{}}");
                            success = false;
                            Logmeon.AnyFailures = true;
                        }
                    }
                }
                if (success)
                    WriteLineColored($"{{red}}done.{{}}");

                foreach (var child in processes.Values.Where(p => p.ParentProcessId == processId))
                    kill(name, child.ProcessId, processes);
            }

            private bool waitProcessDead(uint processId)
            {
                var start = DateTime.UtcNow;
                while (DateTime.UtcNow < start + Logmeon.WaitProcessShutdown)
                {
                    if (ProcessInfo.GetProcess(processId) == null)
                        return true;
                    Thread.Sleep(150);
                }
                return false;
            }

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
