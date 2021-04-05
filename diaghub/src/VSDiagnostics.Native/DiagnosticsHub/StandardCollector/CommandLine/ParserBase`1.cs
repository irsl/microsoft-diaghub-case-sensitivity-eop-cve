// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.CommandLine.ParserBase`1
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Microsoft.DiagnosticsHub.StandardCollector.CommandLine
{
  public abstract class ParserBase<TOptions> where TOptions : new()
  {
    public abstract Dictionary<string, ModeSpecifics<TOptions>> ModeDictionary { get; }

    public abstract Dictionary<string, FlagSpecifics> KnownFlagsDictionary { get; }

    public TOptions ParseArgs(params string[] arguments)
    {
      TOptions options = new TOptions();
      List<string> list = ((IEnumerable<string>) arguments).ToList<string>();
      if (list.Count > 0)
      {
        string str1 = list[0];
        ModeSpecifics<TOptions> modeSpecifics;
        if (!this.ModeDictionary.TryGetValue(str1, out modeSpecifics))
          throw new InvalidModeParsingException(str1);
        IDictionary<string, IList<string>> flagDictionary = this.CreateFlagDictionary();
        List<string> stringList = new List<string>();
        for (int index1 = 1; index1 < list.Count; ++index1)
        {
          string str2 = list[index1];
          if (str2.StartsWith("-", StringComparison.Ordinal) || str2.StartsWith("/", StringComparison.Ordinal))
          {
            string[] strArray = str2.Substring(1).Split(new char[1]
            {
              ':'
            }, 2);
            string index2 = strArray[0];
            string empty = string.Empty;
            if (strArray.Length == 2)
              empty = strArray[1];
            if (!this.KnownFlagsDictionary.ContainsKey(index2))
              throw new InvalidOptionParsingException(str1, index2);
            if (!flagDictionary.ContainsKey(index2))
              flagDictionary[index2] = (IList<string>) new List<string>();
            flagDictionary[index2].Add(empty);
          }
          else
            stringList.Add(str2);
        }
        options = modeSpecifics.Consumer(flagDictionary, (IList<string>) stringList, options);
      }
      return options;
    }

    public virtual void PrintHelp()
    {
      if (this.ModeDictionary == null)
        return;
      foreach (KeyValuePair<string, ModeSpecifics<TOptions>> mode in this.ModeDictionary)
      {
        if (!string.IsNullOrEmpty(mode.Value.Syntax))
          Console.WriteLine("  {0}{1}      {2}{1}", (object) mode.Value.Syntax, (object) Environment.NewLine, (object) mode.Value.Description);
      }
    }

    public virtual void PrintHelp(string mode)
    {
      ModeSpecifics<TOptions> modeSpecifics;
      if (!this.ModeDictionary.TryGetValue(mode, out modeSpecifics))
        throw new ArgumentException(string.Format((IFormatProvider) CultureInfo.CurrentCulture, Resources.Error_InvalidCommand, (object) mode));
      Console.WriteLine("{0}{1}{1}{2}{1}", (object) modeSpecifics.Description, (object) Environment.NewLine, (object) modeSpecifics.Syntax);
      if (modeSpecifics.ExpectedArguments != null)
      {
        foreach (KeyValuePair<string, string> expectedArgument in modeSpecifics.ExpectedArguments)
          Console.WriteLine("  {0}{1}      {2}{1}", (object) expectedArgument.Key, (object) Environment.NewLine, (object) expectedArgument.Value);
      }
      if (modeSpecifics.SupportedFlags == null)
        return;
      foreach (string supportedFlag in (IEnumerable<string>) modeSpecifics.SupportedFlags)
      {
        FlagSpecifics flagSpecifics;
        if (this.KnownFlagsDictionary.TryGetValue(supportedFlag, out flagSpecifics))
          Console.WriteLine("  {0}{1}      {2}{1}", (object) flagSpecifics.Syntax, (object) Environment.NewLine, (object) flagSpecifics.Description);
      }
    }

    protected virtual IDictionary<string, IList<string>> CreateFlagDictionary()
    {
      return (IDictionary<string, IList<string>>) new Dictionary<string, IList<string>>((IEqualityComparer<string>) StringComparer.CurrentCultureIgnoreCase);
    }
  }
}
