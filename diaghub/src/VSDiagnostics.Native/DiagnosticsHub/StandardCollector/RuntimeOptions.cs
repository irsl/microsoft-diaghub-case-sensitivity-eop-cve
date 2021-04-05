// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.RuntimeOptions
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;
using System.Collections.Generic;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  public class RuntimeOptions
  {
    public RuntimeOptions()
    {
      this.Mode = Mode.Help;
      this.KillSessionAtExit = false;
      this.AttachProcessIds = (IList<uint>) new List<uint>();
      this.DetachProcessIds = (IList<uint>) new List<uint>();
      this.AgentsByCLSID = (IDictionary<Guid, RuntimeOptions.AgentToLoad>) new Dictionary<Guid, RuntimeOptions.AgentToLoad>();
      this.SessionId = Guid.Empty;
      this.SessionIdArg = string.Empty;
      this.LifetimeProcess = -1;
    }

    public bool KillSessionAtExit { get; set; }

        public Mode Mode { get; set; }

    public Guid SessionId { get; set; }

    public string SessionIdArg { get; set; }

        public bool Pause { get; set; }
        public string DiagSessionPath { get; set; }

    public string VspxPath { get; set; }

    public string ScratchLocation { get; set; }

    [CLSCompliant(false)]
    public IList<uint> AttachProcessIds { get; private set; }

    [CLSCompliant(false)]
    public IList<uint> DetachProcessIds { get; private set; }

    public string LaunchExecutable { get; set; }

    public string LaunchExecutableArgs { get; set; }

    public IDictionary<Guid, RuntimeOptions.AgentToLoad> AgentsByCLSID { get; private set; }

    public bool Monitor { get; set; }

    public int LifetimeProcess { get; set; }

    public string Message { get; set; }

    public bool ShowModeSpecificHelp { get; set; }

    public bool PackageArchiveOptimized { get; set; }

    public bool PackageDirectoryFormat { get; set; }

    public struct AgentToLoad
    {
      public string DllName { get; set; }

      public string Configuration { get; set; }
    }
  }
}
