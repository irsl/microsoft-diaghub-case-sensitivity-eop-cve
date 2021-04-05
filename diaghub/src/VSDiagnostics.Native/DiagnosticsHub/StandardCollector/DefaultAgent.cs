// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.DefaultAgent
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;
using System.Globalization;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  internal class DefaultAgent
  {
    public static readonly Guid CLSID = new Guid("E485D7A9-C21B-4903-892E-303C10906F6E");
    public static readonly string AssemblyName = "DiagnosticsHub.StandardCollector.Runtime.dll";

    public static string AddTargetProcess(uint processId)
    {
      return string.Format((IFormatProvider) CultureInfo.InvariantCulture, "{{ \"command\":\"addTargetProcess\", \"processId\":{0}, \"startReason\":0, \"requestRundown\":true }}", (object) processId);
    }

    public static string RemoveTargetProcess(uint processId)
    {
      return string.Format((IFormatProvider) CultureInfo.InvariantCulture, "{{ \"command\":\"removeTargetProcess\", \"processId\":{0} }}", (object) processId);
    }
  }
}
