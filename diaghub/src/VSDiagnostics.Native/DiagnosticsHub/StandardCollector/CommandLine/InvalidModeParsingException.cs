// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.CommandLine.InvalidModeParsingException
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;

namespace Microsoft.DiagnosticsHub.StandardCollector.CommandLine
{
  [Serializable]
  public class InvalidModeParsingException : ParsingException
  {
    public InvalidModeParsingException(string mode)
      : base(Resources.Error_InvalidCommand, (object) mode)
    {
    }
  }
}
