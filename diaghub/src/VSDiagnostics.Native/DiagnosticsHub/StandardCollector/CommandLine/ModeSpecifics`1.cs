// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.CommandLine.ModeSpecifics`1
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;
using System.Collections.Generic;

namespace Microsoft.DiagnosticsHub.StandardCollector.CommandLine
{
  public struct ModeSpecifics<TOptions>
  {
    public Func<IDictionary<string, IList<string>>, IList<string>, TOptions, TOptions> Consumer;
    public string Syntax;
    public string Description;
    public IList<string> SupportedFlags;
    public Dictionary<string, string> ExpectedArguments;
  }
}
