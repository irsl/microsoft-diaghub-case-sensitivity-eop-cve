// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.PerfMarkerAgent
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  internal class PerfMarkerAgent
  {
    public static readonly Guid CLSID = new Guid("69938483-B247-4DB9-887E-E19795979356");
    public static readonly string AssemblyName = "DiagnosticsHub.StandardCollector.Runtime.dll";
    public static readonly Guid CpuCLSID = new Guid("4EA90761-2248-496C-B854-3C0399A591A4");
    public static readonly string CpuAssemblyName = "DiagnosticsHub.CpuAgent.dll";
    public static readonly Guid BlockedTimeCLSID = new Guid("911852E4-8EB4-4F5F-91BC-0632B6F0AB62");
    public static readonly string BlockedTimeAssemblyName = PerfMarkerAgent.CpuAssemblyName;
  }
}
