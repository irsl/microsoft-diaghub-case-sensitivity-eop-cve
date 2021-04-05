using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiagHubProof
{
    class Program
    {
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

        static void Main(string[] args)
        {
            RunCommand(@"cmd.exe", "/C whoami > C:\\diaghub-proof.txt");
            RunCommand(@"cmd.exe", "/C whoami /priv >> C:\\diaghub-proof.txt");
        }
    }
}
