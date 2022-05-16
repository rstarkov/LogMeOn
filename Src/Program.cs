using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RT.Util.ExtensionMethods;

namespace LogMeOn;

class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        var scriptFile = Path.Combine(AppContext.BaseDirectory, $"Logmeon-{Environment.MachineName.ToLower()}.cs");
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        Logmeon.WriteLineColored($"Logmeon v{version.Major}.{version.Minor:000}");
        Logmeon.WriteLineColored($"Executing Logmeon script: {{yellow}}{scriptFile}{{}}");
        Logmeon.WriteLineColored("");

        if (!File.Exists(scriptFile))
        {
            Logmeon.WriteLineColored($"{{red}}ERROR:{{}} script file not found: {{yellow}}{scriptFile}{{}}");
            return;
        }

        // Load the script
        var code = File.ReadAllText(scriptFile);
        code = new[]
        {
            "using System;",
            "using System.Collections.Generic;",
            "using System.Threading;",
            "using System.Text.RegularExpressions;",
            "using RT.Util.ExtensionMethods;",
            "namespace LogMeOn;",
            "public class LogMeOnScript : ILogmeonScript {",
            "#line 2",
            code,
            "}",
        }.JoinString("\r\n");

        // Compile the script
        var tree = CSharpSyntaxTree.ParseText(code, new CSharpParseOptions().WithKind(SourceCodeKind.Regular));
        var opts = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary);
        var comp = CSharpCompilation.Create("script", options: opts).AddSyntaxTrees(tree);

        var added = new HashSet<Assembly>();
        void addAssembly(Assembly assy)
        {
            if (!added.Add(assy))
                return;
            comp = comp.AddAssemblyReference(assy);
            foreach (var aref in assy.GetReferencedAssemblies())
                addAssembly(Assembly.Load(aref));
        }
        addAssembly(Assembly.GetEntryAssembly());

        ILogmeonScript script = null;
        var errors = comp.GetDiagnostics().Where(e => e.Severity == DiagnosticSeverity.Error).ToList();
        if (errors.Count == 0)
        {
            var ms = new MemoryStream();
            var result = comp.Emit(ms);
            errors = result.Diagnostics.Where(e => e.Severity == DiagnosticSeverity.Error).ToList();
            if (result.Success)
            {
                var type = Assembly.Load(ms.ToArray()).GetTypes().Single(t => t.IsAssignableTo(typeof(ILogmeonScript)));
                script = (ILogmeonScript)Activator.CreateInstance(type);
            }
        }

        if (script == null)
        {
            Logmeon.WriteLineColored($"{{red}}ERROR:{{}} could not compile script {{yellow}}{scriptFile}{{}}");
            foreach (var error in errors)
            {
                Logmeon.WriteLineColored("");
                Logmeon.WriteLineColored($"[{error.Location.GetMappedLineSpan().StartLinePosition}]: {error.GetMessage()}");
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
                Logmeon.WriteLineColored($"{{red}}{excp.GetType().Name}:{{}} {excp.Message}");
                Console.WriteLine(excp.StackTrace);
            }
            Logmeon.WriteLineColored("");
        }
    }
}

public interface ILogmeonScript
{
    void Main(string[] args);
}
