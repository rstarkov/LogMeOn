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
                    WriteLineColored($"{{green}}{Name}{{}}: service named {{aqua}}{ServiceName}{{}} not found.");
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
                WriteLineColored($"done.");
                find().StartService();
                return this;
            }

            public Service Stop()
            {
                WriteLineColored($"{{green}}{Name}{{}}: service stopped.");
                find().StopService();
                return this;
            }
        }
    }
}
