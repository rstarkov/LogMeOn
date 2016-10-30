using System;
using System.Linq;
using System.Threading;
using RT.Util.ExtensionMethods;

namespace LogMeOn
{
    partial class Logmeon
    {
        public class Service
        {
            public string Name { get; private set; }
            public string ServiceName { get; private set; }

            private TimeSpan _waitBeforeAction;

            public Service(string name, string serviceName = null)
            {
                Name = name;
                ServiceName = serviceName ?? name;
                _waitBeforeAction = Logmeon.WaitBeforeAction;
            }

            private ServiceInfo find()
            {
                var result = ServiceInfo.GetServices().FirstOrDefault(si => si.Name == ServiceName);
                if (result == null)
                {
                    WriteLineColored($"{{green}}{Name}{{}}: service named {{aqua}}{ServiceName}{{}} not found.");
                    Logmeon.AnyFailures = true;
                }
                return result;
            }

            public Service WaitBeforeAction(TimeSpan time)
            {
                _waitBeforeAction = time;
                return this;
            }

            public Service Running(bool shouldBeRunning)
            {
                if (IsRunning != shouldBeRunning)
                {
                    if (shouldBeRunning)
                        Start();
                    else
                        Stop();
                }
                return this;
            }

            public bool IsRunning
            {
                get
                {
                    return find().Started;
                }
            }

            public Service Start()
            {
                WriteColored($"{{green}}{Name}{{}}: starting service in {_waitBeforeAction.TotalSeconds:0} seconds... ");
                Thread.Sleep(_waitBeforeAction);
                WriteColored($"starting... ");
                find().StartService();
                WriteLineColored($"done.");
                _startedServices.Add(this);
                return this;
            }

            public Service Stop()
            {
                WriteColored($"{{green}}{Name}{{}}: stopping service... ");
                find().StopService();
                if (!WaitFor(() => !IsRunning, Logmeon.WaitForServiceShutdown))
                {
                    WriteLineColored($"{{red}}service failed to stop.{{}}");
                    Logmeon.AnyFailures = true;
                }
                else
                    WriteLineColored($"{{green}}done.{{}}");
                return this;
            }

            public Service Priority(Priority priority)
            {
                var service = find();
                if (!service.Started)
                    return this;
                var process = ProcessInfo.GetProcess(service.ProcessId);
                if (process == null)
                {
                    WriteLineColored($"{{green}}{Name}{{}}: {{red}}process {{cyan}}ID {service.ProcessId}{{}} not found.{{}}");
                    Logmeon.AnyFailures = true;
                }
                else if (process.Priority != priority)
                {
                    WriteLineColored($"{{green}}{Name}{{}}: priority of service process {{cyan}}ID {process.ProcessId}{{}} changed from {{yellow}}{process.Priority}{{}} to {{yellow}}{priority}{{}}.");
                    LogAndSuppressException(() => { System.Diagnostics.Process.GetProcessById((int) service.ProcessId).PriorityClass = WinAPI.PriorityToClass(priority); });
                    process = ProcessInfo.GetProcess(service.ProcessId);
                    if (process.Priority != priority)
                    {
                        WriteLineColored($"{{red}}Priority change failed.{{}}");
                        Logmeon.AnyFailures = true;
                    }
                }
                return this;
            }
        }
    }
}
