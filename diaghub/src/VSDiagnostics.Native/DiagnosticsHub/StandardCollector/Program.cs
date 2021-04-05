// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.Program
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using Microsoft.DiagnosticsHub.StandardCollector.CommandLine;
using System;

namespace Microsoft.DiagnosticsHub.StandardCollector
{
  public class Program
  {
    public static void Main(string[] args)
    {
      Console.WriteLine("{0}{1} (R) VS Standard Collector{0}", (object) Environment.NewLine, (object) "Microsoft");
      Parser parser = new Parser();
      RuntimeOptions args1;
      try
      {
        args1 = parser.ParseArgs(args);
      }
      catch (ParsingException ex)
      {
        Program.PrintError(ex.Message);
        switch (ex)
        {
          case InvalidModeParsingException _:
            parser.PrintHelp();
            return;
          case InvalidOptionParsingException _:
            parser.PrintHelp(((InvalidOptionParsingException) ex).Mode);
            return;
          default:
            parser.PrintHelpInstructions();
            return;
        }
      }
      if (args1.ShowModeSpecificHelp)
      {
        parser.PrintHelp(args1.Mode.ToString());
      }
      else
      {
        try
        {
          switch (args1.Mode)
          {
            case Mode.Start:
              CollectorActions.Start(args1);
              break;
            case Mode.Update:
              CollectorActions.Update(args1);
              break;
            case Mode.Stop:
              CollectorActions.Stop(args1);
              break;
            case Mode.Pause:
              CollectorActions.Pause(args1);
              break;
            case Mode.Resume:
              CollectorActions.Resume(args1);
              break;
            case Mode.Status:
              CollectorActions.Status(args1);
              break;
            case Mode.GetCurrentResult:
                CollectorActions.GetCurrentResult(args1);
                break;
            case Mode.PostString:
              CollectorActions.PostString(args1);
              break;
            case Mode.ExpandDiagSession:
              CollectorActions.ExpandDiagSession(args1);
              break;
            case Mode.PerfMarkers:
              CollectorActions.PerfMarkers(args1);
              break;
            case Mode.ConvertToVspx:
              CollectorActions.ConvertToVspx(args1);
              break;
            default:
              parser.PrintHelp();
              break;
          }
        }
        catch (CollectorActionException ex)
        {
          Program.PrintError(ex.Message);
        }
        catch (Exception ex)
        {
          Program.PrintError(ex.Message + Environment.NewLine + ex.StackTrace);
        }
      }
    }

    private static void PrintError(string message)
    {
      int foregroundColor = (int) Console.ForegroundColor;
      if (Console.BackgroundColor != ConsoleColor.Red)
        Console.ForegroundColor = ConsoleColor.Red;
      Console.Error.WriteLine(message);
      Console.ForegroundColor = (ConsoleColor) foregroundColor;
      Console.WriteLine();
    }
  }
}
