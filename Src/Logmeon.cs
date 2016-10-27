using System;
using RT.Util;
using RT.Util.CommandLine;
using RT.Util.Consoles;

namespace LogMeOn
{
    public static partial class Logmeon
    {
        public static TimeSpan WaitBeforeAction { get; set; } = TimeSpan.Zero;

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

        private static void LogAndSuppressException(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                WriteLineColored($"{{red}}ERROR: {e.Message} ({e.GetType().Name}){{}}");
            }
        }
    }
}
