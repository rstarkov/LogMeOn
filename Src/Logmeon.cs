using System;
using System.Collections.Generic;
using System.Threading;
using RT.Util;
using RT.CommandLine;
using RT.Util.Consoles;
using RT.Util.ExtensionMethods;

namespace LogMeOn
{
    public static partial class Logmeon
    {
        /// <summary>
        ///     Specifies a default value for the amount of time to wait before performing certain actions (allowing the user
        ///     to Ctrl+C the script if desired). Actions using this value are <see cref="Process.Run"/> , <see
        ///     cref="Process.SetRunning"/> , <see cref="Service.SetRunning"/>. Defaults to zero.</summary>
        public static TimeSpan WaitBeforeAction { get; set; } = TimeSpan.Zero;

        /// <summary>
        ///     Specifies how long <see cref="Process.Kill"/> will wait before concluding that process did not shut down in
        ///     time and attempting a more brute-force method (or concluding that shutdown failed). Defaults to 7 seconds.</summary>
        public static TimeSpan WaitForProcessShutdown { get; set; } = TimeSpan.FromSeconds(7);

        /// <summary>
        ///     Specifies how long <see cref="Service.SetRunning"/> will wait before concluding that service did not stop in
        ///     time and concluding that shutdown failed. Defaults to 7 seconds.</summary>
        public static TimeSpan WaitForServiceShutdown { get; set; } = TimeSpan.FromSeconds(7);

        /// <summary>
        ///     This value is initially false. Any time any sort of failure occurs (e.g. process would not shut down;
        ///     executable not found etc), this value is set to true. It is not consumed directly by Logmeon, and is intended
        ///     to be looked at by the user script.</summary>
        public static bool AnyFailures { get; set; } = false;

        /// <summary>
        ///     Outputs text to the console, parsing and colorizing where required. For syntax, see
        ///     https://docs.timwi.de/M:RT.Util.CommandLine.CommandLineParser.Colorize(RT.Util.RhoElement) and
        ///     https://docs.timwi.de/T:RT.Util.RhoML. Example: "Normal text {red}Red text{} normal text."</summary>
        public static void WriteColored(string str)
        {
            ConsoleUtil.Write(CommandLineParser.Colorize(RhoML.Parse(str)));
        }

        /// <summary>
        ///     Outputs text to the console, parsing and colorizing where required. For syntax, see
        ///     https://docs.timwi.de/M:RT.Util.CommandLine.CommandLineParser.Colorize(RT.Util.RhoElement) and
        ///     https://docs.timwi.de/T:RT.Util.RhoML. Example: "Normal text {red}Red text{} normal text."</summary>
        public static void WriteLineColored(string str)
        {
            ConsoleUtil.WriteLine(CommandLineParser.Colorize(RhoML.Parse(str)));
        }

        private static List<Process> _startedProcesses = new List<Process>();
        private static List<Service> _startedServices = new List<Service>();

        /// <summary>
        ///     Verifies that all processes and services that were started during this run of the script have actually started
        ///     and are still running. This is a separate step to make it possible for the calling script to pause before
        ///     checking, to confirm that a process/service did not just start and then exit a few seconds later. Every time
        ///     <see cref="Process"/> or <see cref="Service"/> starts a process or a service, it is automatically enrolled for
        ///     this check, but the check itself is optional.</summary>
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

        /// <summary>
        ///     Waits until the specified date/time, logging a message to this effect. Does not wait nor logs anything if the
        ///     time is null or in the past.</summary>
        public static void WaitUntil(DateTime? date)
        {
            if (date == null)
                return;
            if (date.Value.Kind == DateTimeKind.Unspecified)
                throw new ArgumentException($"{nameof(WaitUntil)} requires a local or a UTC time; time passed in was of an unspecified type.", nameof(date));
            var dt = date.Value.ToUniversalTime();
            if (dt < DateTime.UtcNow)
                return;
            WriteLineColored($"Sleeping for {(dt - DateTime.UtcNow).TotalSeconds:0.0} seconds...");
            while (DateTime.UtcNow < dt)
                Thread.Sleep(TimeSpan.FromSeconds((dt - DateTime.UtcNow).TotalSeconds.Clip(0, 10)));
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
