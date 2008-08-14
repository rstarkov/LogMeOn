# Logmeon.py
# Copyright (C) 2008 Roman Starkov
#
# $Id: //depot/users/rs/Logmeon/Logmeon.py#3 $
# $DateTime: 2008/08/14 20:22:04 $

import sys
import subprocess
from optparse import OptionParser

#-----------------------------------------------------------------------------
class LogonConfig():

    #-----------------------------------------------------------------------------
    def __init__(self, initialWait = 0.0):
        self.__items = []
        self.parser = None
        self.initialWait = initialWait

    #-----------------------------------------------------------------------------
    def ConstructCmdlineParser(self):
        self.parser = OptionParser()

        self.parser.add_option('-f', '--first-logon',     action="store_true", default=False, help="If set, the script will know it's performing the initial logon")
        self.parser.add_option('-d', '--debug-level',     type="int",          default=0,     help="Enables debug mode LEVEL, printing extra info", metavar="LEVEL")
        self.parser.add_option('-c', '--close-when-done', action="store_true", default=False, help="If set, the script will not wait for user to press Enter upon completion")

    #-----------------------------------------------------------------------------
    def ParseArgs(self):
        if self.parser == None:
            self.ConstructCmdlineParser()

        self.options, self.parameters = self.parser.parse_args()

    #-----------------------------------------------------------------------------
    def Add(self, item):
        self.__items.append(item)
        item.debug_level = self.options.debug_level

    #-----------------------------------------------------------------------------
    def Execute(self):
        print "Logmeon started."
        if self.options.first_logon:
            sleep(self.initialWait)
        print

        hadErrors = False
        for item in self.__items:
            if item.NeedsExecuting():
                try:
                    item.Execute()
                except Exception, e:
                    hadErrors = True
                    print "....ERROR executing action: %s" % str(e)
                    if self.options.debug_level >= 1:
                        import traceback
                        traceback.print_exc(file=sys.stdout)
                print

        print
        if hadErrors:
            print "Logmeon finished WITH ERRORS :("
        else:
            print "Logmeon finished SUCCESSFULLY :)"

        if hadErrors or not self.options.close_when_done:
            print
            raw_input("Press Enter to exit")



#-----------------------------------------------------------------------------
class Subst:

    #-----------------------------------------------------------------------------
    def __init__(self, drive, path, wait=0.0):
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
class DeleteFile:

    #-----------------------------------------------------------------------------
    def __init__(self, filename, wait=0.0):
        self.filename = filename
        self.wait = wait

    #-----------------------------------------------------------------------------
    def NeedsExecuting(self):
        import os
        return os.path.isfile(self.filename)

    #-----------------------------------------------------------------------------
    def Execute(self):
        print("Deleting file " + self.filename)
        import os
        os.remove(self.filename)
        sleep(self.wait)



#-----------------------------------------------------------------------------
class Process:

    #-----------------------------------------------------------------------------
    def __init__(self, nicename, cmd, wait=0.0, useShell=False, exactMatch=False, waitDone=False):
        self.nicename = nicename # Used for information only
        self.cmd = cmd  # Must be a string with the command and all arguments to it.
                        # This same string is used for searching for running processes.
        self.useShell = useShell      # if True, command will be executed within the shell
        self.exactMatch = exactMatch  # unless True, fuzzy matching of the command against
                                      # running processes will be used.
        self.wait = wait          # seconds to wait after fire-and-forget start
        self.waitDone = waitDone  # if True, will wait for the process to terminate.
        self.debug_level = 0

    #-----------------------------------------------------------------------------
    def NeedsExecuting(self):
        from win32com.client import GetObject
        import re

        if self.debug_level >= 5: print; print

        # Construct the regex used to match running processes
        if self.exactMatch:
            rgx = '^' + re.escape(self.cmd) + '$'
            if self.debug_level >= 5: print "REGEX: " + regex
            rgx = re.compile(rgx)
        else:
            cmd = self.cmd.strip().replace('\\', '/')
            if cmd[0] == '"':
                exepos = cmd.find('"', 1)
                exe = cmd[1:exepos]
            else:
                exepos = cmd.find(' ')
                exe = cmd[cmd[0:exepos].rfind('/')+1 : exepos]

            args = re.sub(r'\s+', ' ', cmd[exepos+1:].replace('"', '')).strip()
            if len(args) > 0:
                args = '\s+' + re.escape(args).replace('\\ ', '\\s+')

            rgx = '^.*?' + re.escape(exe.strip()) + args + '\s*$'
            if self.debug_level >= 5: print "REGEX: " + rgx
            rgx = re.compile(rgx, re.IGNORECASE)

        # Search for this process among the running processes
        for proc in GetObject("WinMgMts:").InstancesOf("Win32_Process"):
            cmdline = proc.Properties_("CommandLine").Value
            if cmdline == None:
                continue
            if not self.exactMatch:
                cmdline = cmdline.replace('"', '').replace('\\', '/')

            if self.debug_level >= 5: print "CMDLINE: " + cmdline
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

