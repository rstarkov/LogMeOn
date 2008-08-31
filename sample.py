from Logmeon import LogonConfig, Subst, Process, DeleteFile, Helpers

cfg = LogonConfig(initialWait = 10.0)
cfg.ParseArgs()

cfg.Add(wait = 1.0, item = Subst('P', r'I:\Projects'))
cfg.Add(wait = 3.0, item = Process("Process Explorer", exefile = r"I:\Root\SysInternals\procexp.exe", args = "/t"))
cfg.Add(wait = 1.0, item = Process("TClockEx", exefile = r"I:\Temp\TClockEx\tclockex.exe"), isExecuteNeeded = lambda x: not Helpers.MutexExists("TClockExDllInstalled"))

cfg.Execute()
