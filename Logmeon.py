import subprocess

#-----------------------------------------------------------------------------
class LogonConfig():

    #-----------------------------------------------------------------------------
    def __init__(self):
        self.__items = []

    #-----------------------------------------------------------------------------
    def Add(self, item):
        self.__items.append(item)

    #-----------------------------------------------------------------------------
    def Execute(self, initialWait = 0):
        print "Logmeon started."
        sleep(initialWait)
        print

        for item in self.__items:
            if item.NeedsExecuting():
                item.Execute()
                print

        print "Logmeon finished."
        raw_input("Press Enter to exit")



#-----------------------------------------------------------------------------
class Subst:

    #-----------------------------------------------------------------------------
    def __init__(self, drive, path, wait):
        self.path = path
        self.drive = drive
        self.wait = wait

    #-----------------------------------------------------------------------------
    def NeedsExecuting(self):
        import os
        return not os.path.isdir(self.drive + ":\\")

    #-----------------------------------------------------------------------------
    def Execute(self):
        print("Substing drive %s: for directory %s" % (self.drive, self.path))
        subprocess.check_call("subst %s: %s" % (self.drive, self.path))
        sleep(self.wait)



#-----------------------------------------------------------------------------
class Process:

    #-----------------------------------------------------------------------------
    def __init__(self, nicename, cmd, wait, useShell = False, exactMatch = False, waitDone = False):
        self.nicename = nicename # Used for information only
        self.cmd = cmd  # Must be a string with the command and all arguments to it.
                        # This same string is used for searching for running processes.
        self.useShell = useShell      # if True, command will be executed within the shell
        self.exactMatch = exactMatch  # unless True, fuzzy matching of the command against
                                      # running processes will be used.
        self.wait = wait          # seconds to wait after fire-and-forget start
        self.waitDone = waitDone  # if True, will wait for the process to terminate.

    #-----------------------------------------------------------------------------
    def NeedsExecuting(self):
        from win32com.client import GetObject
        import re

        if self.exactMatch:
            rgx = re.compile(re.escape(self.cmd))
        else:
            cmd = self.cmd.strip().replace('\\', '/')
            if cmd[0] == '"':
                exepos = cmd.find('"', 1)
                exe = cmd[1:exepos]
            else:
                exepos = cmd.find(' ')
                exe = cmd[cmd[0:exepos].rfind('/')+1 : exepos]

            args = re.sub(r'\s+', ' ', cmd[exepos+1:].replace('"', ''))
            #print exepos, exe, args
            rgx = '^.*?' + re.escape(exe) + '\s+' + re.escape(args).replace('\\ ', '\\s+') + '$'
            #print "REGEX: " + rgx
            rgx = re.compile(rgx, re.IGNORECASE)

        for proc in GetObject("WinMgMts:").InstancesOf("Win32_Process"):
            cmdline = proc.Properties_("CommandLine").Value
            if cmdline == None:
                continue
            if not self.exactMatch:
                cmdline = cmdline.replace('"', '')

            #print "CMDLINE: " + cmdline
            if rgx.match(cmdline):
                return False

        return True

    #-----------------------------------------------------------------------------
    def Execute(self):
        print "Starting %s..." % self.nicename
        proc = subprocess.Popen(self.cmd, shell = self.useShell)
        if self.waitDone:
            print "....Waiting for the command to complete"
            proc.wait()
        else:
            sleep(self.wait)



#-----------------------------------------------------------------------------
def sleep(seconds):
    if seconds == 0:
        return
    print "....Sleeping for %s seconds" % seconds
    import time
    time.sleep(seconds)

