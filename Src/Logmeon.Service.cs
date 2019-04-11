using System;
using System.Linq;
using System.Threading;
using RT.Util.ExtensionMethods;

namespace LogMeOn
{
    partial class Logmeon
    {
        /// <summary>Controls a service.</summary>
        public class Service
        {
            /// <summary>Gets the friendly name of the service as used for logging purposes only.</summary>
            public string Name { get; private set; }
            /// <summary>Gets the Windows name of the service as used for locating the service.</summary>
            public string ServiceName { get; private set; }

            /// <summary>
            ///     Gets the time at which <see cref="SetRunning"/> started the service. Null if it has never been started. Do
            ///     not confuse with the start time of any underlying OS process or service; this property relates only to the
            ///     Logmeon concept.</summary>
            public DateTime? StartedAt { get; private set; } = null;

            /// <summary>
            ///     Gets the time at which <see cref="SetRunning"/> successfully stopped the service. Null if it has never
            ///     been stopped or didn't succeed. Do not confuse with the end time of any underlying OS process or service;
            ///     this property relates only to the Logmeon concept.</summary>
            public DateTime? StoppedAt { get; private set; } = null;

            private TimeSpan _waitBeforeAction;

            /// <summary>
            ///     Creates a service controller for the specified service. Does not perform any actions whatsoever; does not
            ///     even check whether the named service can be found.</summary>
            /// <param name="name">
            ///     Friendly name for this service to be used for logging purposes.</param>
            /// <param name="serviceName">
            ///     Name of the service, as known to Windows. If null, <paramref name="name"/> is used.</param>
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
                    WriteLineColored($"{{green}}{Name}{{}}: {{red}}service not found:{{}} {{yellow}}{ServiceName}{{}} not found.");
                    Logmeon.AnyFailures = true;
                }
                return result;
            }

            /// <summary>Overrides <see cref="Logmeon.WaitBeforeAction"/> for this specific service only. Chainable.</summary>
            public Service WaitBeforeAction(TimeSpan time)
            {
                _waitBeforeAction = time;
                return this;
            }

            /// <summary>
            ///     Ensures the service is in the desired state - running or stopped. Does nothing if the service is already
            ///     in the desired state. If the service needs to be stopped, waits for the service to actually stop and logs
            ///     an error if it doesn't (see <see cref="Logmeon.WaitForServiceShutdown"/>). If the service needs to be
            ///     started, starts the service and returns immediately; use <see cref="Logmeon.CheckStarted"/> to confirm
            ///     that the service didn't exit immediately after starting. Chainable.</summary>
            public Service SetRunning(bool shouldBeRunning)
            {
                if (find() == null)
                    return this;
                if (IsRunning != shouldBeRunning)
                {
                    if (shouldBeRunning)
                        start();
                    else
                        stop();
                }
                return this;
            }

            /// <summary>
            ///     Checks whether the service is running. This value is not cached; each invocation will locate the service
            ///     by name and determine its status.</summary>
            public bool IsRunning
            {
                get
                {
                    return find()?.Started ?? false;
                }
            }

            private void start()
            {
                WriteColored($"{{green}}{Name}{{}}: starting service in {_waitBeforeAction.TotalSeconds:0} seconds... ");
                Thread.Sleep(_waitBeforeAction);
                WriteColored($"starting... ");
                find().StartService();
                StartedAt = DateTime.UtcNow;
                WriteLineColored($"done.");
                _startedServices.Add(this);
            }

            private void stop()
            {
                WriteColored($"{{green}}{Name}{{}}: stopping service... ");
                find().StopService();
                if (!WaitFor(() => !IsRunning, Logmeon.WaitForServiceShutdown))
                {
                    WriteLineColored($"{{red}}service failed to stop.{{}}");
                    Logmeon.AnyFailures = true;
                }
                else
                {
                    StoppedAt = DateTime.UtcNow;
                    WriteLineColored($"{{green}}done.{{}}");
                }
            }

            /// <summary>
            ///     Sets the priority of the process hosting this service. Does nothing if the process is already at that
            ///     priority, or if the service is stopped. Verifies that the change took place, and logs an error otherwise.
            ///     Logs an error if the service or its associated process cannot be found. Chainable.</summary>
            public Service Priority(Priority priority)
            {
                var service = find();
                if (service == null || !service.Started)
                    return this;
                var process = ProcessInfo.GetProcess(service.ProcessId);
                if (process == null)
                {
                    WriteLineColored($"{{green}}{Name}{{}}: {{red}}process {{cyan}}ID {service.ProcessId}{{}} not found.{{}}");
                    Logmeon.AnyFailures = true;
                }
                else if (process.Priority != priority)
                {
                    WriteColored($"{{green}}{Name}{{}}: changing priority of service process {{cyan}}ID {process.ProcessId}{{}} from {{yellow}}{process.Priority}{{}} to {{yellow}}{priority}{{}}... ");
                    LogAndSuppressException(() => { System.Diagnostics.Process.GetProcessById((int) service.ProcessId).PriorityClass = WinAPI.PriorityToClass(priority); });
                    process = ProcessInfo.GetProcess(service.ProcessId);
                    if (process.Priority != priority)
                    {
                        WriteLineColored($"{{red}}failed.{{}}");
                        Logmeon.AnyFailures = true;
                    }
                    else
                        WriteLineColored($"{{green}}done.{{}}");
                }
                return this;
            }
        }
    }
}
