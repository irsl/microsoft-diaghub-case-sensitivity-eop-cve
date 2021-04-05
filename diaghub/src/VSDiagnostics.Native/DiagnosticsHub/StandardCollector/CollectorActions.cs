// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.CollectorActions
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using DiagnosticsHub.Packaging.Interop;
using DiagnosticsHub.StandardCollector.Host.Interop;
using Microsoft.DiagnosticsHub.Packaging.InteropEx;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  public class CollectorActions
  {
    private const int VSHUBESESSIONAUTHORIZATIONREQUIRED = -518979497;

    public static void Start(RuntimeOptions options)
    {
      var collectorService = CollectorActions.GetCollectorService();
      Console.WriteLine("Got the collector service reference");
      var thisProc = System.Diagnostics.Process.GetCurrentProcess();
      var scratch_path = !string.IsNullOrEmpty(options.ScratchLocation) ? options.ScratchLocation : Path.Combine(Path.GetDirectoryName(thisProc.MainModule.FileName), "etw");
      Console.WriteLine($"Scratch path: {scratch_path}");
      // the specified dir must exist
      Directory.CreateDirectory(scratch_path);

      uint mainpid = (uint)thisProc.Id;
      SessionConfiguration sessionConfiguration = new SessionConfiguration()
      {
          Type = CollectionType.CollectionType_Etw,
          LifetimeMonitorProcessId = options.KillSessionAtExit ? mainpid : 0, // mainpid, if we specify no pid, then the session wont be lost between executions
          CollectorScratch = scratch_path,
          SessionId = options.SessionId,

          /*
          ClientLocale = (ushort) CultureInfo.CurrentUICulture.LCID,
          Location = CollectionLocation.CollectionLocation_Headless,
          Flags = SessionConfigurationFlags.SessionConfigurationFlags_None,
          */
      };
      ClientDelegateNative clientDelegate1 = null;
      if (options.Monitor)
      {
          clientDelegate1 = new ClientDelegateNative();
      }

      Console.WriteLine($"Creating a new session with guid: {options.SessionId}");
      var session = collectorService.CreateSession(ref sessionConfiguration, clientDelegate1);
      Console.WriteLine($"Session created");
      CollectorActions.SetPackageOptions(session, options);
      
      /*
       * // dont try to be more clever than the user
      if (!options.AgentsByCLSID.ContainsKey(DefaultAgent.CLSID))
      {
                Console.WriteLine($"Default agent not specified, but loading it anyway");
                options.AgentsByCLSID.Add(DefaultAgent.CLSID, new RuntimeOptions.AgentToLoad()
                {
                    DllName = DefaultAgent.AssemblyName
                });
      }
      */
      foreach (KeyValuePair<Guid, RuntimeOptions.AgentToLoad> keyValuePair in (IEnumerable<KeyValuePair<Guid, RuntimeOptions.AgentToLoad>>) options.AgentsByCLSID)
      {
        try
        {
            Console.WriteLine($"Loading agent: {keyValuePair.Key} -> {keyValuePair.Value.DllName}");
            CollectorActions.LoadAgent(session, keyValuePair.Key, keyValuePair.Value);
        }
        catch (COMException ex)
        {
          if (ex.HResult == -518979497)
          {
            int startIndex = ex.Message.IndexOf(':') + 1;
            if (startIndex == 0)
            {
              throw;
            }
            else
            {
              using (Process process = Process.Start(new ProcessStartInfo(ex.Message.Substring(startIndex))
              {
                Arguments = string.Format("authorize {0}", (object) options.SessionId),
                Verb = "runas",
                UseShellExecute = true
              }))
                process.WaitForExit();
              CollectorActions.LoadAgent(session, keyValuePair.Key, keyValuePair.Value);
            }
          }
          else
            throw;
        }
      }
      CollectorActions.SetProxyBlanket(session);
      using (TargetLauncher targetLauncher = new TargetLauncher())
      {
        int pid = -1;
        if (!string.IsNullOrEmpty(options.LaunchExecutable))
        {
          object eventArg1 = (object) null;
          object launchExecutable = (object) options.LaunchExecutable;
          object eventOut;
          session.TriggerEvent(SessionEvent.SessionEvent_BeforeProcessLaunch, ref eventArg1, ref launchExecutable, out eventOut);
          string environmentBlockVariables = string.Format("DIAGHUB_SESSION_ID={0}\0{1}", (object) options.SessionId.ToString("B"), (object) (eventOut as string));
          pid = targetLauncher.LaunchSuspended(options.LaunchExecutable, options.LaunchExecutableArgs, environmentBlockVariables);
        }
        session.Start();
        if (pid != -1)
        {
          object obj1 = (object) null;
          object obj2 = (object)null;
          object obj3;
          session.TriggerEvent(SessionEvent.SessionEvent_AfterProcessLaunch, ref obj1, ref obj2, out obj3);
          session.PostStringToListener(DefaultAgent.CLSID, DefaultAgent.AddTargetProcess((uint) pid));
        }
        foreach (uint attachProcessId in (IEnumerable<uint>) options.AttachProcessIds)
          session.PostStringToListener(DefaultAgent.CLSID, DefaultAgent.AddTargetProcess(attachProcessId));
        if (pid != -1)
          targetLauncher.Resume(pid);
      }
      CollectorActions.PrintDetailedSessionStatus(options, session);
      if (!options.Monitor)
        return;
      EventWaitHandle eventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, session.GetStatusChangeEventName());
      while (eventWaitHandle.WaitOne())
      {
        switch (session.QueryState())
        {
          case SessionState.SessionState_Stopped:
            return;
          case SessionState.SessionState_Errored:
            return;
          default:
            continue;
        }
      }
    }

    public static void Update(RuntimeOptions options)
    {
      var collectionSession = CollectorActions.GetCollectionSession(options);
      if (options.LifetimeProcess > 0)
      {
        var collectorService = CollectorActions.GetCollectorService();
        Guid sessionId = options.SessionId;
        ref Guid local = ref sessionId;
        int lifetimeProcess = options.LifetimeProcess;
        collectorService.AddLifetimeMonitorProcessIdForSession(ref local, (uint) lifetimeProcess);
      }
      foreach (KeyValuePair<Guid, RuntimeOptions.AgentToLoad> keyValuePair in (IEnumerable<KeyValuePair<Guid, RuntimeOptions.AgentToLoad>>) options.AgentsByCLSID)
        CollectorActions.LoadAgent(collectionSession, keyValuePair.Key, keyValuePair.Value);
      CollectorActions.SetProxyBlanket(collectionSession);
      foreach (uint attachProcessId in (IEnumerable<uint>) options.AttachProcessIds)
        collectionSession.PostStringToListener(DefaultAgent.CLSID, DefaultAgent.AddTargetProcess(attachProcessId));
      foreach (uint detachProcessId in (IEnumerable<uint>) options.DetachProcessIds)
        collectionSession.PostStringToListener(DefaultAgent.CLSID, DefaultAgent.RemoveTargetProcess(detachProcessId));
      CollectorActions.PrintDetailedSessionStatus(options, collectionSession);
    }

    public static void Stop(RuntimeOptions options)
    {
      var collectionSession = CollectorActions.GetCollectionSession(options);
      CollectorActions.SetProxyBlanket(collectionSession);
      string str1;
      try
      {
        str1 = collectionSession.Stop() as string;
      }
      catch (ArgumentException ex)
      {
        throw new CollectorActionException(ex.Message, Array.Empty<object>());
      }
      Console.WriteLine($"Stop returned with: {str1}");
      if(options.DiagSessionPath != "-")
      {
                bool flag = File.Exists(str1);
                if (flag || Directory.Exists(str1))
                {
                    string str2 = options.DiagSessionPath;
                    try
                    {
                        if (flag)
                        {
                            if (!Path.HasExtension(str2))
                                str2 = Path.ChangeExtension(str2, Path.GetExtension(str1));
                            File.Delete(str2);
                            File.Move(str1, str2);
                        }
                        else
                        {
                            if (Directory.Exists(str2))
                                Directory.Delete(str2, true);
                            Directory.Move(str1, str2);
                        }
                        Console.WriteLine(string.Format((IFormatProvider)CultureInfo.CurrentCulture, Resources.Message_CollectionResult, (object)str2));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(string.Format((IFormatProvider)CultureInfo.CurrentCulture, Resources.Error_CollectionResultMoveError, (object)str1));
                        throw;
                    }
                }
      }
      CollectorActions.PrintDetailedSessionStatus(options, collectionSession);
      var collectorService = CollectorActions.GetCollectorService();
      Guid sessionId = options.SessionId;
      ref Guid local = ref sessionId;
      collectorService.DestroySession(ref local);
    }

    public static void Pause(RuntimeOptions options)
    {
      var collectionSession = CollectorActions.GetCollectionSession(options);
      try
      {
        collectionSession.Pause();
      }
      catch (ArgumentException ex)
      {
        throw new CollectorActionException(ex.Message, Array.Empty<object>());
      }
      CollectorActions.PrintDetailedSessionStatus(options, collectionSession);
    }

    public static void Resume(RuntimeOptions options)
    {
      var collectionSession = CollectorActions.GetCollectionSession(options);
      try
      {
        collectionSession.Resume();
      }
      catch (ArgumentException ex)
      {
        throw new CollectorActionException(ex.Message, Array.Empty<object>());
      }
      CollectorActions.PrintDetailedSessionStatus(options, collectionSession);
    }

    public static void Status(RuntimeOptions options)
    {
      var collectionSession = CollectorActions.GetCollectionSession(options);
      CollectorActions.PrintDetailedSessionStatus(options, collectionSession);
    }

    public static void GetCurrentResult(RuntimeOptions options)
    {
        var collectionSession = CollectorActions.GetCollectionSession(options);
        CollectorActions.GetCurrentResult(options, collectionSession);
    }

    public static void PostString(RuntimeOptions options)
    {
      var collectionSession = CollectorActions.GetCollectionSession(options);
      CollectorActions.SetProxyBlanket(collectionSession);
      collectionSession.PostStringToListener(options.AgentsByCLSID.First<KeyValuePair<Guid, RuntimeOptions.AgentToLoad>>().Key, options.Message);
    }

    public static void ExpandDiagSession(RuntimeOptions options)
    {
      string fullPath = Path.GetFullPath(options.DiagSessionPath);
      using (DhPackage dhPackage = DhPackage.Open(fullPath))
      {
        string packageFullPath = Path.Combine(Path.GetDirectoryName(fullPath), Path.GetFileNameWithoutExtension(fullPath));
        dhPackage.CommitToPath(packageFullPath, CommitOption.CommitOption_Directory);
      }
    }

    public static void PerfMarkers(RuntimeOptions options)
    {
      options.SessionId = Guid.NewGuid();
      options.SessionIdArg = options.SessionId.ToString();
      options.AgentsByCLSID.Add(DefaultAgent.CLSID, new RuntimeOptions.AgentToLoad()
      {
        DllName = DefaultAgent.AssemblyName,
        Configuration = "{\"enableMarks\":true}"
      });
      options.AgentsByCLSID.Add(PerfMarkerAgent.CLSID, new RuntimeOptions.AgentToLoad()
      {
        DllName = PerfMarkerAgent.AssemblyName
      });
      options.AgentsByCLSID.Add(PerfMarkerAgent.CpuCLSID, new RuntimeOptions.AgentToLoad()
      {
        DllName = PerfMarkerAgent.CpuAssemblyName
      });
      options.AgentsByCLSID.Add(PerfMarkerAgent.BlockedTimeCLSID, new RuntimeOptions.AgentToLoad()
      {
        DllName = PerfMarkerAgent.BlockedTimeAssemblyName
      });
      CollectorActions.Start(options);
      Console.WriteLine("Visual Studio Perf Marker collection started.\r\nPress any key to stop collection and generate diagsession");
      Console.ReadKey();
      Console.WriteLine("Stopping...");
      string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
      string str = DateTime.Now.ToString("yyyy-MM-d--HH-mm-ss");
      options.DiagSessionPath = Path.Combine(folderPath, "Report-" + str + ".diagsession");
      CollectorActions.Stop(options);
    }

    public static void ConvertToVspx(RuntimeOptions options)
    {
      using (DhPackage dhPackage = DhPackage.Open(Path.GetFullPath(options.DiagSessionPath)))
      {
        using (VspxConverter vspxConverter = new VspxConverter((IDhPackage) dhPackage))
        {
          string str = vspxConverter.Convert(options.VspxPath);
          Console.WriteLine(string.Format((IFormatProvider) CultureInfo.CurrentCulture, Resources.Message_VspxConversion, (object) options.DiagSessionPath, (object) str));
        }
      }
    }
        



        private static IStandardCollectorServiceNative GetCollectorService()
        {
            CollectorActions.LoadProxyStubsForCollectorService();

            Guid guid = typeof(StandardCollectorNativeClass).GUID;
            Console.WriteLine($"Creating instance of StandardCollector {guid} (Windows Native)");
            object returnedComObject;
            try
            {
                CollectorActions.NativeMethods.CoCreateInstance(guid, (object)null, 4U, typeof(IStandardCollectorServiceNative).GUID, out returnedComObject);
            }
            catch (COMException ex)
            {
                if (ex.HResult == -2147221164)
                    throw new CollectorActionException("#1 " + Resources.Error_CollectorServiceNotFound, Array.Empty<object>());
                throw new CollectorActionException(ex.Message, Array.Empty<object>());
            }
            if (!(returnedComObject is IStandardCollectorServiceNative re))
                throw new CollectorActionException(Resources.Error_CollectorHostNotFound, Array.Empty<object>());

            // we need to impersonate here as the remote side validates write access to the directory we specify.
            // it is called with the token of this RPC session, and CreateFileW throws BAD_IMPERSONATION if
            // we dont allow impersonation
            SetProxyBlanket(re);
            return re;

        }

    private static void LoadProxyStubsForCollectorService()
    {
      IntPtr hModule = !Environment.Is64BitProcess ? CollectorActions.NativeMethods.LoadLibraryEx("x86\\DiagnosticsHub.StandardCollector.Proxy.dll", IntPtr.Zero, 0) : CollectorActions.NativeMethods.LoadLibraryEx("amd64\\DiagnosticsHub.StandardCollector.Proxy.dll", IntPtr.Zero, 0);
      if (hModule == IntPtr.Zero)
        throw new CollectorActionException(string.Format(Resources.ErrMsg_FailedToLoadProxyStubsBinary), Array.Empty<object>());
      IntPtr procAddress = CollectorActions.NativeMethods.GetProcAddress(hModule, "ManualRegisterInterfaces");
      if (procAddress == IntPtr.Zero)
        throw new CollectorActionException(string.Format(Resources.ErrMsg_RegistrationEntryPointNotFound), Array.Empty<object>());
      Marshal.ThrowExceptionForHR(((CollectorActions.ManualRegisterInterfacesDelegate) Marshal.GetDelegateForFunctionPointer(procAddress, typeof (CollectorActions.ManualRegisterInterfacesDelegate)))());
    }

        private static void SetProxyBlanket<T>(T service)
        {
            Guid guid = typeof(T).GUID;
            IntPtr ppv;
            object serviceAsObject = (object)service;
            IntPtr iunkPtr = Marshal.GetIUnknownForObject(serviceAsObject);
            Marshal.ThrowExceptionForHR(Marshal.QueryInterface(iunkPtr, ref guid, out ppv));
            try
            {
                // 3U = RPC_C_IMP_LEVEL_IMPERSONATE
                Marshal.ThrowExceptionForHR(CollectorActions.NativeMethods.CoSetProxyBlanket(ppv, uint.MaxValue, uint.MaxValue, IntPtr.Zero, 0U, 3U, IntPtr.Zero, 2048U));
            }
            finally
            {
                if (ppv != IntPtr.Zero)
                    Marshal.Release(ppv);
            }
        }


    private static ICollectionSessionNative GetCollectionSession(RuntimeOptions options)
        {
      var collectorService1 = CollectorActions.GetCollectorService();
      try
      {
        Guid sessionId = options.SessionId;
        ref Guid local = ref sessionId;
        return collectorService1.GetSession(ref local);
      }
      catch (ArgumentException ex)
      {
        throw new CollectorActionException(Resources.Error_SessionDoesNotExist, new object[1]
        {
          (object) options.SessionIdArg
        });
      }
    }

    private static void SetPackageOptions(ICollectionSessionNative session, RuntimeOptions options)
    {
      if (options.PackageArchiveOptimized)
        ((IDhPackageConfiguration) session).SetArchiveType(2U);
      if (!options.PackageDirectoryFormat)
        return;
      ((IDhPackageConfiguration) session).SetCommitOptions(257U);
    }

    private static void LoadAgent(
      ICollectionSessionNative session,
      Guid classId,
      RuntimeOptions.AgentToLoad agentToLoad)
    {
      if (string.IsNullOrEmpty(agentToLoad.Configuration))
      {
        string dllName = agentToLoad.DllName;
        Guid guid = classId;
        ref Guid local = ref guid;
        session.AddAgent(dllName, ref local);
      }
      else
      {
        string dllName = agentToLoad.DllName;
        Guid guid = classId;
        ref Guid local = ref guid;
        string configuration = agentToLoad.Configuration;
        session.AddAgentWithConfiguration(dllName, ref local, configuration);
      }
    }

        private static void GetCurrentResult(
  RuntimeOptions options,
  ICollectionSessionNative session)
    {
            object re = session.GetCurrentResult(options.Pause);
            Console.WriteLine($"GetCurrentResult returned: {re}");
    }

    private static void PrintDetailedSessionStatus(
      RuntimeOptions options,
      ICollectionSessionNative session)
    {
      string str;
      switch (session.QueryState())
      {
        case SessionState.SessionState_Created:
          str = Resources.SessionState_Created;
          break;
        case SessionState.SessionState_Running:
          str = Resources.SessionState_Running;
          break;
        case SessionState.SessionState_Paused:
          str = Resources.SessionState_Paused;
          break;
        case SessionState.SessionState_Stopped:
          str = Resources.SessionState_Stopped;
          break;
        case SessionState.SessionState_Errored:
          str = Resources.SessionState_Errored;
          break;
        default:
          str = Resources.SessionState_Unknown;
          break;
      }
      Console.WriteLine(string.Format((IFormatProvider) CultureInfo.CurrentCulture, Resources.Message_SessionState, (object) options.SessionIdArg, (object) options.SessionId.ToString("B"), (object) str));
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int ManualRegisterInterfacesDelegate();

    private static class NativeMethods
    {
      [DllImport("ole32.dll")]
      public static extern int CoSetProxyBlanket(
        IntPtr pProxy,
        uint dwAuthnSvc,
        uint dwAuthzSvc,
        IntPtr pServerPrincName,
        uint dwAuthnLevel,
        uint dwImpLevel,
        IntPtr pAuthInfo,
        uint dwCapabilities);

      [DllImport("ole32.dll", PreserveSig = false)]
      [return: MarshalAs(UnmanagedType.Interface)]
      public static extern void CoCreateInstance(
        [MarshalAs(UnmanagedType.LPStruct), In] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object aggregateObject,
        uint classContext,
        [MarshalAs(UnmanagedType.LPStruct), In] Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object returnedComObject);

      [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
      public static extern IntPtr LoadLibraryEx(
        string lpFileName,
        IntPtr hReservedNull,
        int dwFlags);

      [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ThrowOnUnmappableChar = true, BestFitMapping = false)]
      public static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procname);
    }
  }
}
