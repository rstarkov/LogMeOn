using System;
using System.Collections.Generic;
using System.Threading;
using RT.Util;
using RT.Util.CommandLine;
using RT.Util.Consoles;

namespace LogMeOn
{
    public static partial class Logmeon
    {
        public static TimeSpan WaitBeforeAction { get; set; } = TimeSpan.Zero;
        public static TimeSpan WaitForProcessShutdown { get; set; } = TimeSpan.FromSeconds(7);
        public static TimeSpan WaitForServiceShutdown { get; set; } = TimeSpan.FromSeconds(7);
        public static bool AnyFailures { get; set; } = false;

        public static void Initialise()
        {
            WinAPI.ModifyPrivilege(PrivilegeName.SeDebugPrivilege, true);
        }

        public static void WriteColored(string str)
        {
            ConsoleUtil.Write(CommandLineParser.Colorize(RhoML.Parse(str)));
        }

        public static void WriteLineColored(string str)
        {
            ConsoleUtil.WriteLine(CommandLineParser.Colorize(RhoML.Parse(str)));
        }

        private static List<Process> _startedProcesses = new List<Process>();
        private static List<Service> _startedServices = new List<Service>();

        /// <summary>
        ///     Verifies that all processes and services that were started during this run of the script have actually started
        ///     and are still running. This is a separate step to make it possible for the calling script to pause before
        ///     checking, to confirm that a process/service did not just start and then exit a few seconds later.</summary>
        public static void CheckStarted()
        {
            foreach (var process in _startedProcesses)
                if (!process.IsRunning)
                {
                    WriteLineColored($"{{green}}{process.Name}{{}}: {{red}}process did not start, or started and then exited.{{}}");
                    Logmeon.AnyFailures = true;
                }
            foreach (var service in _startedServices)
                if (!service.IsRunning)
                {
                    WriteLineColored($"{{green}}{service.Name}{{}}: {{red}}service did not start, or started and then stopped.{{}}");
                    Logmeon.AnyFailures = true;
                }
        }

        private static void LogAndSuppressException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                WriteLineColored("");
                WriteLineColored($"{{red}}ERROR: {e.Message} ({e.GetType().Name}){{}}");
                Logmeon.AnyFailures = true;
            }
        }

        private static bool WaitFor(Func<bool> check, TimeSpan duration)
        {
            var start = DateTime.UtcNow;
            while (DateTime.UtcNow < start + duration)
            {
                if (check())
                    return true;
                Thread.Sleep(150);
            }
            return false;
        }
    }
}
