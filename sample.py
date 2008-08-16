from Logmeon import LogonConfig, Subst, Process, DeleteFile

cfg = LogonConfig(initialWait = 20.0)
cfg.ParseArgs()

cfg.Add(Subst('P', r'I:\Projects', wait=1.0))
cfg.Add(Process("Process Explorer", exefile = r"I:\Root\SysInternals\procexp.exe", args = "/t", wait=6.0))

cfg.Execute()
