# Logmeon.py  --  a library for writing logon/re-logon scripts.
# Copyright (C) 2008 Roman Starkov
#
# $Id: //depot/users/rs/Logmeon/Logmeon.py#9 $
# $DateTime: 2008/09/25 18:52:31 $

# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU General Public License as published by
# the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU General Public License for more details.
#
# You should have received a copy of the GNU General Public License
# along with this program.  If not, see <http://www.gnu.org/licenses/>.

import sys, os, re
import subprocess
from optparse import OptionParser, OptionGroup

#-----------------------------------------------------------------------------
class LogonConfig():

    #-----------------------------------------------------------------------------
    def __init__(self):
        self.__items = []
        self.parser = None
        self.default_wait_first = 2
        self.default_wait_next = 0

    #-----------------------------------------------------------------------------
    def ConstructCmdlineParser(self):
        self.parser = OptionParser()

        self.parser.add_option('-f', '--first-logon',     action="store_true", default=False, help="If set, the script will know it's performing the initial logon")
        self.parser.add_option('-c', '--close-when-done', action="store_true", default=False, help="If set, the script will not wait for user to press Enter upon completion")

        debug_group = OptionGroup(self.parser, "Debugging options")
        debug_group.add_option ('-v', '--verbose',         action="store_true", default=False, help="Enables the printing of extra info")
        debug_group.add_option ('-d', '--debug-level',     type="int",          default=0,     help="Enables debug mode LEVEL. Any value above 0 also implies --verbose.", metavar="LEVEL")

        self.parser.add_option_group(debug_group)

    #-----------------------------------------------------------------------------
    def ParseArgs(self):
        if self.parser == None:
            self.ConstructCmdlineParser()

        self.options, self.parameters = self.parser.parse_args()

        if len(self.parameters) > 0:
            self.parser.error("Unexpected positional arguments supplied. This script doesn't take any.")

        if self.options.debug_level > 0:
            self.options.verbose = True

    #-----------------------------------------------------------------------------
    def Add(self, item, wait_first = None, wait_next = None, is_execute_needed = lambda item: item.NeedsExecute()):
        self.__items.append(item)
        item.options = self.options
        if self.options.first_logon:
            if wait_first == None: item.wait = self.default_wait_first
            else:                  item.wait = wait_first
        else:
            if wait_next  == None: item.wait = self.default_wait_next
            else:                  item.wait = wait_next
        item.is_execute_needed = is_execute_needed

    #-----------------------------------------------------------------------------
    def Execute(self):
        print "Logmeon started."
        print

        hadErrors = False
        for item in self.__items:
            if item.is_execute_needed(item):
                try:
                    item.Execute()
                    Helpers.Sleep(item.wait)
                except Exception, e:
                    hadErrors = True
                    print "....ERROR executing action: %s" % str(e)
                    if self.options.debug_level >= 1:
                        import traceback
                        print re.compile(r'^', re.MULTILINE).sub('....', traceback.format_exc())
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
class Nothing:

    #-----------------------------------------------------------------------------
    def NeedsExecute(self):
        return False

    #-----------------------------------------------------------------------------
    def Execute(self):
        pass



#-----------------------------------------------------------------------------
class Subst:

    #-----------------------------------------------------------------------------
    def __init__(self, drive, path):
        self.path = path
        self.drive = drive

    #-----------------------------------------------------------------------------
    def NeedsExecute(self):
        return not os.path.isdir(self.drive + ":\\")

    #-----------------------------------------------------------------------------
    def Execute(self):
        print("Substing drive %s: for directory %s" % (self.drive, self.path))
        subprocess.check_call("subst %s: %s" % (self.drive, self.path))



#-----------------------------------------------------------------------------
class DeleteFile:

    #-----------------------------------------------------------------------------
    def __init__(self, filename):
        self.filename = filename

    #-----------------------------------------------------------------------------
    def NeedsExecute(self):
        return os.path.isfile(self.filename)

    #-----------------------------------------------------------------------------
    def Execute(self):
        print("Deleting file " + self.filename)
        os.remove(self.filename)



#-----------------------------------------------------------------------------
class Process:

    #-----------------------------------------------------------------------------
    def __init__(self, nicename, exe, args="", work_dir=None, use_shell=False, wait_done=False):
        self.nicename = nicename     # Used for information only
        self.exe = exe
        self.args = args             # Must be None or a single string with all arguments.
        self.work_dir = work_dir
        self.use_shell = use_shell   # if True, command will be executed within the shell
        self.wait_done = wait_done   # if True, will wait for the process to terminate.

    #-----------------------------------------------------------------------------
    def NeedsExecute(self):
        from win32com.client import GetObject

        if self.options.debug_level >= 5: print; print

        # Construct the regex used to match running processes
        rgx = '^["\\s]*' + re.escape(self.exe.replace('\\', '/')) + '["\\s]*'
        if self.args != None and len(self.args) > 0:
            rgx += '\\s+' + re.escape(self.args.replace('\\', '/')).replace('\\ ', '\\s+')
        rgx += '\\s*$'

        if self.options.debug_level >= 5: print "REGEX: " + rgx
        rgx = re.compile(rgx)

        # Search for this process among the running processes
        for proc in GetObject("WinMgMts:").InstancesOf("Win32_Process"):
            cmdline = proc.Properties_("CommandLine").Value
            if cmdline == None:
                continue

            cmdline = cmdline.replace('\\', '/')

            if self.options.debug_level >= 5: print "CMDLINE: " + cmdline
            if rgx.match(cmdline):
                return False

        return True

    #-----------------------------------------------------------------------------
    def Execute(self):
        print "Starting %s..." % self.nicename
        if not os.path.isfile(self.exe):
            raise Exception("File not found: " + self.exe)
        if self.work_dir == None:
            self.work_dir = os.path.dirname(self.exe)

        cmdline = ('"' + self.exe + '" ' + self.args).strip()
        if self.options.verbose:
            print "....Executing " + cmdline
        proc = subprocess.Popen(cmdline, cwd = self.work_dir, shell = self.use_shell)

        if self.wait_done:
            print "....Waiting for the command to complete"
            proc.wait()



#-----------------------------------------------------------------------------
class Helpers:

    #-----------------------------------------------------------------------------
    @staticmethod
    def Sleep(seconds):
        if seconds == 0:
            return
        print "....Sleeping for %s seconds" % seconds
        import time
        time.sleep(seconds)

    #-----------------------------------------------------------------------------
    @staticmethod
    def MutexExists(mutexName):
        from win32event import OpenMutex, ReleaseMutex
        from pywintypes import error as PyWinTypesError
        try:
            mutex = OpenMutex(1048576, False, mutexName)
            ReleaseMutex(mutex)
            return True
        except PyWinTypesError:
            return False
