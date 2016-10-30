using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using csscript;
using CSScriptLibrary;
using RT.Util;
using RT.Util.ExtensionMethods;

namespace LogMeOn
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            WinAPI.ModifyPrivilege(PrivilegeName.SeDebugPrivilege, true);

            var scriptFile = PathUtil.AppPathCombine($"Logmeon-{Environment.MachineName.ToLower()}.cs");
            Logmeon.WriteLineColored($"Logmeon v{Ut.VersionOfExe()}");
            Logmeon.WriteLineColored($"Executing Logmeon script: {{yellow}}{scriptFile}{{}}");
            Logmeon.WriteLineColored("");

            if (!File.Exists(scriptFile))
            {
                Logmeon.WriteLineColored($"{{red}}ERROR:{{}} script file not found: {{yellow}}{scriptFile}{{}}");
                return;
            }

            // Load the script
            var code = File.ReadAllText(scriptFile);
            code = "using System; using System.Collections.Generic; using System.Threading; namespace LogMeOn { public class LogMeOnScript : ILogmeonScript {" + code + "} }";

            // Compile the script
            ILogmeonScript script;
            try
            {
                script = CSScript.Evaluator.LoadCode<ILogmeonScript>(code);
            }
            catch (CompilerException e)
            {
                Logmeon.WriteLineColored($"{{red}}ERROR:{{}} could not compile script {{yellow}}{scriptFile}{{}}");
                foreach (var error in (List<string>) e.Data["Errors"])
                {
                    Logmeon.WriteLineColored("");
                    Logmeon.WriteLineColored(error);
                }
                return;
            }

            // Execute the script
            try
            {
                script.Main(args);
            }
            catch (Exception e)
            {
                Logmeon.WriteLineColored("");
                Logmeon.WriteLineColored($"{{red}}UNHANDLED EXCEPTION:{{}}");
                foreach (var excp in e.SelectChain(ee => ee.InnerException))
                {
                    Logmeon.WriteLineColored("");
                    Logmeon.WriteLineColored($"{{red}}{e.GetType().Name}:{{}} {e.Message}");
                    Console.WriteLine(e.StackTrace);
                }
                Logmeon.WriteLineColored("");
            }
        }
    }

    public interface ILogmeonScript
    {
        void Main(string[] args);
    }
}
