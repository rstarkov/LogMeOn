# Logmeon.py
# Copyright (C) 2008 Roman Starkov
#
# $Id: //depot/users/rs/Logmeon/Logmeon.py#4 $
# $DateTime: 2008/08/16 08:34:01 $

import sys, os, re
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
        return os.path.isfile(self.filename)

    #-----------------------------------------------------------------------------
    def Execute(self):
        print("Deleting file " + self.filename)
        os.remove(self.filename)
        sleep(self.wait)



#-----------------------------------------------------------------------------
class Process:

    #-----------------------------------------------------------------------------
    def __init__(self, nicename, exefile, args="", workdir=None, wait=0.0, useShell=False, waitDone=False):
        self.nicename = nicename   # Used for information only
        self.exefile = exefile
        self.args = args           # Must be None or a single string with all arguments.
        self.workdir = workdir
        self.wait = wait           # seconds to wait after fire-and-forget start
        self.useShell = useShell   # if True, command will be executed within the shell
        self.waitDone = waitDone   # if True, will wait for the process to terminate.
        self.debug_level = 0

    #-----------------------------------------------------------------------------
    def NeedsExecuting(self):
        from win32com.client import GetObject

        if self.debug_level >= 5: print; print

        # Construct the regex used to match running processes
        rgx = '^["\\s]*' + re.escape(self.exefile.replace('\\', '/')) + '["\\s]*'
        if self.args != None and len(self.args) > 0:
            rgx += '\\s+' + re.escape(self.args.replace('\\', '/')).replace('\\ ', '\\s+')
        rgx += '\\s*$'

        if self.debug_level >= 5: print "REGEX: " + regex
        rgx = re.compile(rgx)

        # Search for this process among the running processes
        for proc in GetObject("WinMgMts:").InstancesOf("Win32_Process"):
            cmdline = proc.Properties_("CommandLine").Value
            if cmdline == None:
                continue

            cmdline = cmdline.replace('\\', '/')

            if self.debug_level >= 5: print "CMDLINE: " + cmdline
            if rgx.match(cmdline):
                return False

        return True

    #-----------------------------------------------------------------------------
    def Execute(self):
        print "Starting %s..." % self.nicename
        if not os.path.isfile(self.exefile):
            raise Exception("File not found: " + self.exefile)
        if self.workdir == None:
            self.workdir = os.path.dirname(self.exefile)

        proc = subprocess.Popen('"' + self.exefile + '" ' + self.args, cwd = self.workdir, shell = self.useShell)

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

