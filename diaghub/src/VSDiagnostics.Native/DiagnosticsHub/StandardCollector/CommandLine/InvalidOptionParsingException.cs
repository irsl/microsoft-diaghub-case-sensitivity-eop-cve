// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.CommandLine.InvalidOptionParsingException
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;
using System.Runtime.Serialization;

namespace Microsoft.DiagnosticsHub.StandardCollector.CommandLine
{
  [Serializable]
  public class InvalidOptionParsingException : ParsingException
  {
    public InvalidOptionParsingException(string mode, string option)
      : base(Resources.Error_InvalidOption, (object) option)
    {
      this.Mode = mode;
    }

    public string Mode { get; private set; }

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);
      info.AddValue("Mode", (object) this.Mode);
    }
  }
}
