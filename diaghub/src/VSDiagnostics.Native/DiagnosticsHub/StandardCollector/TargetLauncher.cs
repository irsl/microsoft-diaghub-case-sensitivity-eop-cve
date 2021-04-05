// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.TargetLauncher
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  internal class TargetLauncher : IDisposable
  {
    private Dictionary<int, TargetLauncher.LaunchedProcess> launchedProcesses = new Dictionary<int, TargetLauncher.LaunchedProcess>();
    private bool isDisposed;

    public int LaunchSuspended(string exePath, string exeArgs, string environmentBlockVariables)
    {
      StringBuilder lpCommandLine = TargetLauncher.BuildCommandLine(exePath, exeArgs);
      int dwCreationFlags = TargetLauncher.NativeMethods.CREATE_NEW_CONSOLE | TargetLauncher.NativeMethods.CREATE_SUSPENDED | TargetLauncher.NativeMethods.CREATE_UNICODE_ENVIRONMENT;
      GCHandle gcHandle = GCHandle.Alloc((object) TargetLauncher.CreateEnvironmentVariableBlock(environmentBlockVariables), GCHandleType.Pinned);
      TargetLauncher.NativeMethods.STARTUPINFO lpStartupInfo = new TargetLauncher.NativeMethods.STARTUPINFO();
      TargetLauncher.NativeMethods.PROCESS_INFORMATION lpProcessInformation = new TargetLauncher.NativeMethods.PROCESS_INFORMATION();
      if (!TargetLauncher.NativeMethods.CreateProcess((string) null, lpCommandLine, IntPtr.Zero, IntPtr.Zero, false, dwCreationFlags, gcHandle.AddrOfPinnedObject(), (string) null, lpStartupInfo, lpProcessInformation))
      {
        string message = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        throw new CollectorActionException(Resources.Error_FailedToLaunchProcess, new object[2]
        {
          (object) exePath,
          (object) message
        });
      }
      TargetLauncher.LaunchedProcess launchedProcess = new TargetLauncher.LaunchedProcess()
      {
        ProcessId = TargetLauncher.NativeMethods.GetProcessId(lpProcessInformation.Process),
        ProcessHandle = new SafeProcessHandle(lpProcessInformation.Process, true),
        ThreadHandle = new SafeProcessHandle(lpProcessInformation.Thread, true)
      };
      this.launchedProcesses.Add(launchedProcess.ProcessId, launchedProcess);
      return launchedProcess.ProcessId;
    }

    public void Resume(int pid)
    {
      TargetLauncher.LaunchedProcess launchedProcess;
      if (this.launchedProcesses.TryGetValue(pid, out launchedProcess) && TargetLauncher.NativeMethods.ResumeThread(launchedProcess.ThreadHandle.DangerousGetHandle()) == -1)
      {
        string message = new Win32Exception(Marshal.GetLastWin32Error()).Message;
        throw new CollectorActionException(Resources.Error_FailedToResumeProcess, new object[2]
        {
          (object) pid,
          (object) message
        });
      }
    }

    public void Dispose()
    {
      if (this.isDisposed)
        return;
      foreach (KeyValuePair<int, TargetLauncher.LaunchedProcess> launchedProcess in this.launchedProcesses)
        launchedProcess.Value.Dispose();
      this.launchedProcesses.Clear();
      this.isDisposed = true;
    }

    private static StringBuilder BuildCommandLine(
      string executableFileName,
      string arguments)
    {
      StringBuilder stringBuilder = new StringBuilder();
      string str = executableFileName.Trim();
      int num = !str.StartsWith("\"", StringComparison.Ordinal) ? 0 : (str.EndsWith("\"", StringComparison.Ordinal) ? 1 : 0);
      if (num == 0)
        stringBuilder.Append("\"");
      stringBuilder.Append(str);
      if (num == 0)
        stringBuilder.Append("\"");
      if (!string.IsNullOrEmpty(arguments))
      {
        stringBuilder.Append(" ");
        stringBuilder.Append(arguments);
      }
      return stringBuilder;
    }

    private static byte[] CreateEnvironmentVariableBlock(string envBlockToMerge)
    {
      StringBuilder stringBuilder = new StringBuilder();
      IDictionary environmentVariables = Environment.GetEnvironmentVariables();
      foreach (object key in (IEnumerable) environmentVariables.Keys)
      {
        string str = (string) environmentVariables[key];
        stringBuilder.Append((string) key);
        stringBuilder.Append('=');
        stringBuilder.Append(str);
        stringBuilder.Append(char.MinValue);
      }
      if (string.IsNullOrEmpty(envBlockToMerge))
        stringBuilder.Append(char.MinValue);
      else
        stringBuilder.Append(envBlockToMerge);
      return Encoding.Unicode.GetBytes(stringBuilder.ToString());
    }

    private static class NativeMethods
    {
      public static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
      public static readonly int CREATE_SUSPENDED = 4;
      public static readonly int CREATE_NEW_CONSOLE = 16;
      public static readonly int CREATE_UNICODE_ENVIRONMENT = 1024;

      [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true, BestFitMapping = false)]
      public static extern bool CreateProcess(
        [MarshalAs(UnmanagedType.LPTStr)] string lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        int dwCreationFlags,
        IntPtr lpEnvironment,
        [MarshalAs(UnmanagedType.LPTStr)] string lpCurrentDirectory,
        TargetLauncher.NativeMethods.STARTUPINFO lpStartupInfo,
        TargetLauncher.NativeMethods.PROCESS_INFORMATION lpProcessInformation);

      [DllImport("kernel32.dll")]
      public static extern int GetProcessId(IntPtr handle);

      [DllImport("kernel32.dll", SetLastError = true)]
      public static extern int ResumeThread(IntPtr handle);

      [StructLayout(LayoutKind.Sequential)]
      public class STARTUPINFO
      {
        public IntPtr Reserved1 = IntPtr.Zero;
        public IntPtr Desktop = IntPtr.Zero;
        public IntPtr Title = IntPtr.Zero;
        public IntPtr Reserved3 = IntPtr.Zero;
        public IntPtr StdInput = IntPtr.Zero;
        public IntPtr StdOutput = IntPtr.Zero;
        public IntPtr StdError = IntPtr.Zero;
        public int structSize;
        public int XPoint;
        public int YPoint;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;

        public STARTUPINFO()
        {
          this.structSize = Marshal.SizeOf<TargetLauncher.NativeMethods.STARTUPINFO>(this);
        }
      }

      [StructLayout(LayoutKind.Sequential)]
      public class PROCESS_INFORMATION
      {
        public IntPtr Process = IntPtr.Zero;
        public IntPtr Thread = IntPtr.Zero;
        public int ProcessId;
        public int ThreadId;
      }
    }

    private class LaunchedProcess
    {
      public int ProcessId { get; set; }

      public SafeProcessHandle ProcessHandle { get; set; }

      public SafeProcessHandle ThreadHandle { get; set; }

      public void Dispose()
      {
        if (this.ProcessHandle != null)
          this.ProcessHandle.Dispose();
        if (this.ThreadHandle == null)
          return;
        this.ThreadHandle.Dispose();
      }
    }
  }
}
