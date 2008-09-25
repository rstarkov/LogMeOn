from Logmeon import LogonConfig, Subst, Process, DeleteFile, Helpers

cfg = LogonConfig()
cfg.ParseArgs()

cfg.Add(item = Subst('P', r'I:\Projects'))
cfg.Add(item = Process("Process Explorer", exe = r"I:\Root\SysInternals\procexp.exe", args = "/t"))
cfg.Add(item = Process("TClockEx", exe = r"I:\Temp\TClockEx\tclockex.exe"), is_execute_needed = lambda x: not Helpers.MutexExists("TClockExDllInstalled"))

cfg.Execute()
