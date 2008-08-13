import sys
from Logmeon import LogonConfig, Subst, Process

cfg = LogonConfig()

cfg.Add(Subst('P', r'I:\Projects', wait=1.0))
cfg.Add(Process("Process Explorer", r'I:\Root\SysInternals\procexp.exe /t', wait=3.0))

if len(sys.argv) > 1:
    cfg.Execute(initialWait = 0.0)
else:
    cfg.Execute(initialWait = 3.0)
