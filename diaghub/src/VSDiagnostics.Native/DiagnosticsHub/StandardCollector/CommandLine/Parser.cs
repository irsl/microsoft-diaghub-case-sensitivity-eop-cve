// Decompiled with JetBrains decompiler
// Type: Microsoft.DiagnosticsHub.StandardCollector.CommandLine.Parser
// Assembly: VSDiagnostics, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2F9D4495-41C9-4FD5-8B64-0B1B63E216DA
// Assembly location: C:\Program Files (x86)\Microsoft Visual Studio\2017\Community\Team Tools\DiagnosticsHub\Collector\VSDiagnostics.exe

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;

namespace Microsoft.DiagnosticsHub.StandardCollector.CommandLine
{
  public class Parser : ParserBase<RuntimeOptions>
  {
    private static readonly Guid BaseSessionId = new Guid("0097E42F-003D-4F91-A845-6404CF289E84");
    private static string flagFormat = "/{0}:{1}";
    private static string helpString = "help";
    private Dictionary<string, FlagSpecifics> knownFlagsDictionary = new Dictionary<string, FlagSpecifics>()
    {
        { "loadAgent", new FlagSpecifics(){ Syntax = "/loadAgent:<agentCLSID>;<agentName>[;<config>]", Description= "Agent to be loaded - this option may be specified multiple times to load multiple agents" } },
        { "scratchLocation", new FlagSpecifics(){ Syntax = "/scratchLocation:<folderName>", Description= "Path to the desired output folder" } },
        { "attach", new FlagSpecifics(){ Syntax = "/attach:<pid>[;<pid>;...]", Description= "Process to attach to" } },
        { "monitor", new FlagSpecifics(){ Syntax = "/monitor", Description= "Start monitor mode (async callbacks)" } },
        { "output", new FlagSpecifics(){ Syntax = "/output:<filename>", Description= "Stop session (and write etl package to to destination file)" } },
        { "killSessionAtExit", new FlagSpecifics(){ Syntax = "/killSessionAtExit", Description= "Start monitor mode (async callbacks)" } },
        { "agent", new FlagSpecifics(){ Syntax = "/agent:<agentCLSID>", Description= "Agent to communicate with" } },
        { "pause", new FlagSpecifics(){ Syntax = "/pause", Description= "Parameter for GetCurrentResult" } },
        { "package", new FlagSpecifics(){ Syntax = "/package", Description= "Controls whether a directory (default) or an archive should be created as a diagsession" } },
    };
    private Dictionary<string, ModeSpecifics<RuntimeOptions>> modeDictionary = new Dictionary<string, ModeSpecifics<RuntimeOptions>>();

    public override Dictionary<string, ModeSpecifics<RuntimeOptions>> ModeDictionary
    {
      get
      {
        return this.modeDictionary;
      }
    }

    public override Dictionary<string, FlagSpecifics> KnownFlagsDictionary
    {
      get
      {
        return this.knownFlagsDictionary;
      }
    }

    public override void PrintHelp()
    {
      this.PrintHelpInstructions();
      Console.WriteLine(Resources.Description_Commands);
      base.PrintHelp();
    }

    public void PrintHelpInstructions()
    {
      Console.WriteLine(Resources.Description_HelpInstructions, (object) Assembly.GetExecutingAssembly().GetName().Name, (object) Parser.helpString);
    }

    private static bool CheckShowHelp(
      IDictionary<string, IList<string>> flags,
      IList<string> args,
      RuntimeOptions options)
    {
      return args.Contains(Parser.helpString) || flags.ContainsKey(Parser.helpString);
    }

    private static void ParseAndSetSessionId(string sessionIdString, ref RuntimeOptions options)
    {
      options.SessionIdArg = sessionIdString;
      byte result1;
      if (byte.TryParse(sessionIdString, out result1))
      {
        options.SessionId = Parser.CreateSessionId(result1);
      }
      else
      {
        Guid result2;
        if (Guid.TryParse(sessionIdString, out result2))
          options.SessionId = result2;
        else
          throw new ParsingException(Resources.Error_InvalidSessionID, new object[1]
          {
            (object) sessionIdString
          });
      }
    }

    private static Guid CreateSessionId(byte sessionId)
    {
      byte[] byteArray = Parser.BaseSessionId.ToByteArray();
      byteArray[3] = sessionId;
      return new Guid(byteArray);
    }

    private static string ValidateAndGetFlagSingleOccurence(string flag, IList<string> argumentList)
    {
      if (argumentList.Count != 1)
        throw new ParsingException(Resources.Error_ValueMayOnlyBeSpecifiedOnce, new object[1]
        {
          (object) flag
        });
      string str = argumentList[0];
      if (string.IsNullOrWhiteSpace(str))
        throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
        {
          (object) flag
        });
      return str;
    }

    private static void ValidatePackageConfiguration(
      IList<string> argumentList,
      RuntimeOptions options)
    {
      string flagSingleOccurence = Parser.ValidateAndGetFlagSingleOccurence("package", argumentList);
      if (flagSingleOccurence.Equals("opt", StringComparison.OrdinalIgnoreCase))
        options.PackageArchiveOptimized = true;
      else if (flagSingleOccurence.Equals("dir", StringComparison.OrdinalIgnoreCase))
        options.PackageDirectoryFormat = true;
      else
        throw new ParsingException(Resources.Error_InvalidValue, new object[2]
        {
          (object) "package",
          (object) flagSingleOccurence
        });
    }

    private static void ParseAttachArgument(string pidListString, RuntimeOptions options)
    {
      string str = pidListString;
      char[] separator = new char[1]{ ';' };
      foreach (string s in str.Split(separator, StringSplitOptions.RemoveEmptyEntries))
      {
        uint result;
        if (!uint.TryParse(s, out result))
          throw new ParsingException(Resources.Error_InvalidValue, new object[2]
          {
            (object) "attach",
            (object) s
          });
        options.AttachProcessIds.Add(result);
      }
    }

    private static void ParseDetachArgument(string pidListString, RuntimeOptions options)
    {
      string str = pidListString;
      char[] separator = new char[1]{ ';' };
      foreach (string s in str.Split(separator, StringSplitOptions.RemoveEmptyEntries))
      {
        uint result;
        if (!uint.TryParse(s, out result))
          throw new ParsingException(Resources.Error_InvalidValue, new object[2]
          {
            (object) "detach",
            (object) s
          });
        options.DetachProcessIds.Add(result);
      }
    }

    private static void ParseAgentArgument(IList<string> agentList, RuntimeOptions options)
    {
      if (agentList.Count == 0)
        throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
        {
          (object) "loadAgent"
        });
      foreach (string agent in (IEnumerable<string>) agentList)
      {
        if (string.IsNullOrWhiteSpace(agent))
          throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
          {
            (object) "loadAgent"
          });
        string[] strArray = agent.Split(new char[1]{ ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (strArray.Length != 2 && strArray.Length != 3 || (string.IsNullOrWhiteSpace(strArray[0]) || string.IsNullOrWhiteSpace(strArray[1])))
          throw new ParsingException(Resources.Error_InvalidValue, new object[2]
          {
            (object) "loadAgent",
            (object) agent
          });
        string input = strArray[0];
        Guid result;
        if (!Guid.TryParse(input, out result))
          throw new ParsingException(Resources.Error_InvalidValue, new object[2]
          {
            (object) "loadAgent",
            (object) input
          });
        if (options.AgentsByCLSID.ContainsKey(result))
          throw new ParsingException(Resources.Error_DuplicateAgent, new object[1]
          {
            (object) input
          });
        RuntimeOptions.AgentToLoad agentToLoad = new RuntimeOptions.AgentToLoad()
        {
          DllName = strArray[1],
          Configuration = strArray.Length > 2 ? strArray[2] : string.Empty
        };
        options.AgentsByCLSID.Add(result, agentToLoad);
      }
    }

    private static void ParseLifetimeProcessArgument(
      IList<string> lifetimeProcess,
      RuntimeOptions options)
    {
      if (lifetimeProcess.Count == 0)
        throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
        {
          (object) nameof (lifetimeProcess)
        });
      foreach (string s in (IEnumerable<string>) lifetimeProcess)
      {
        int result;
        if (!int.TryParse(s, out result))
          throw new ParsingException(Resources.Error_InvalidValue, new object[2]
          {
            (object) nameof (lifetimeProcess),
            (object) s
          });
        options.LifetimeProcess = result;
      }
    }

    public Parser()
    {
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary1 = new Dictionary<string, ModeSpecifics<RuntimeOptions>>((IEqualityComparer<string>) StringComparer.CurrentCultureIgnoreCase);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary2 = dictionary1;
      ModeSpecifics<RuntimeOptions> modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.PerfMarkers;
        return options;
      });
      ModeSpecifics<RuntimeOptions> modeSpecifics2 = modeSpecifics1;
      dictionary2.Add("VSPerfMarkers", modeSpecifics2);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary3 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.ConvertToVspx;
        if (Parser.CheckShowHelp(flags, args, options))
          return options;
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_DiagSessionMustBeSupplied, Array.Empty<object>());
        IList<string> argumentList;
        if (!flags.TryGetValue("output", out argumentList))
          throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
          {
            (object) "output"
          });
        options.DiagSessionPath = args[0];
        options.VspxPath = Parser.ValidateAndGetFlagSingleOccurence("output", argumentList);
        return options;
      });
      ModeSpecifics<RuntimeOptions> modeSpecifics3 = modeSpecifics1;
      dictionary3.Add("convertToVspx", modeSpecifics3);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary4 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Start;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
        Parser.ParseAndSetSessionId(args[0], ref options);
        IList<string> argumentList1;
        if (flags.TryGetValue("attach", out argumentList1))
          Parser.ParseAttachArgument(Parser.ValidateAndGetFlagSingleOccurence("attach", argumentList1), options);
        IList<string> argumentList2;
        if (flags.TryGetValue("launch", out argumentList2))
        {
          string flagSingleOccurence = Parser.ValidateAndGetFlagSingleOccurence("launch", argumentList2);
          options.LaunchExecutable = flagSingleOccurence;
        }
        IList<string> argumentList3;
        if (flags.TryGetValue("launchArgs", out argumentList3))
        {
          if (string.IsNullOrEmpty(options.LaunchExecutable))
            throw new ParsingException(Resources.Error_BothValuesMustBeSpecified, new object[2]
            {
              (object) "launch",
              (object) "launchArgs"
            });
          string flagSingleOccurence = Parser.ValidateAndGetFlagSingleOccurence("launchArgs", argumentList3);
          options.LaunchExecutableArgs = flagSingleOccurence;
        }
        IList<string> agentList;
        if (flags.TryGetValue("loadAgent", out agentList))
          Parser.ParseAgentArgument(agentList, options);
        if (flags.ContainsKey("monitor"))
          options.Monitor = true;
        if (flags.ContainsKey("killSessionAtExit"))
            options.KillSessionAtExit = true;
          IList<string> argumentList4;
        if (flags.TryGetValue("scratchLocation", out argumentList4))
        {
          string flagSingleOccurence = Parser.ValidateAndGetFlagSingleOccurence("scratchLocation", argumentList4);
          options.ScratchLocation = flagSingleOccurence;
        }
        IList<string> argumentList5;
        if (flags.TryGetValue("package", out argumentList5))
          Parser.ValidatePackageConfiguration(argumentList5, options);
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "start {0} [/{1}:{2}] [/{3}:{4} /{5}:{6}] [/{7}:{8}] [/{9}] [/{10}:{11}] [/{12}:{13}]", (object) Resources.Arg_SessionId, (object) "attach", (object) Resources.Syntax_AttachFlag, (object) "launch", (object) Resources.Syntax_LaunchFlag, (object) "launchArgs", (object) Resources.Syntax_LaunchArgsFlag, (object) "loadAgent", (object) Resources.Syntax_LoadAgentFlag, (object) "monitor", (object) "scratchLocation", (object) Resources.Syntax_ScratchLocationFlag, (object) "package", (object) Resources.Syntax_PackageFlagArgs);
      modeSpecifics1.Description = Resources.Description_Start;
      modeSpecifics1.SupportedFlags = (IList<string>) new List<string>()
      {
        "attach",
        "launch",
        "launchArgs",
        "loadAgent",
        "monitor",
        "killSessionAtExit",
        "scratchLocation",
        "package",
      };
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics4 = modeSpecifics1;
      dictionary4.Add("start", modeSpecifics4);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary5 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Update;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
        Parser.ParseAndSetSessionId(args[0], ref options);
        IList<string> argumentList1;
        if (flags.TryGetValue("attach", out argumentList1))
          Parser.ParseAttachArgument(Parser.ValidateAndGetFlagSingleOccurence("attach", argumentList1), options);
        IList<string> argumentList2;
        if (flags.TryGetValue("detach", out argumentList2))
          Parser.ParseDetachArgument(Parser.ValidateAndGetFlagSingleOccurence("detach", argumentList2), options);
        IList<string> agentList;
        if (flags.TryGetValue("loadAgent", out agentList))
          Parser.ParseAgentArgument(agentList, options);
        IList<string> lifetimeProcess;
        if (flags.TryGetValue("lifetimeProcess", out lifetimeProcess))
          Parser.ParseLifetimeProcessArgument(lifetimeProcess, options);
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "update {0} [/{1}:{2}] [/{3}:{4}] [/{5}:{6} ...] [/{7}:{8}]", (object) Resources.Arg_SessionId, (object) "attach", (object) Resources.Syntax_AttachFlag, (object) "detach", (object) Resources.Syntax_DetachFlag, (object) "loadAgent", (object) Resources.Syntax_LoadAgentFlag, (object) "lifetimeProcess", (object) Resources.Syntax_LifetimeProcessFlag);
      modeSpecifics1.Description = Resources.Description_Update;
      modeSpecifics1.SupportedFlags = (IList<string>) new List<string>()
      {
        "attach",
        "detach",
        "loadAgent",
      };
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics5 = modeSpecifics1;
      dictionary5.Add("update", modeSpecifics5);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary6 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Stop;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
        Parser.ParseAndSetSessionId(args[0], ref options);
        IList<string> argumentList;
        if (!flags.TryGetValue("output", out argumentList))
          throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
          {
            (object) "output"
          });
        string flagSingleOccurence = Parser.ValidateAndGetFlagSingleOccurence("output", argumentList);
        options.DiagSessionPath = flagSingleOccurence;
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "stop {0} /{1}:{2}", (object) Resources.Arg_SessionId, (object) "output", (object) Resources.Syntax_OutputFlag);
      modeSpecifics1.Description = Resources.Description_Stop;
      modeSpecifics1.SupportedFlags = (IList<string>) new List<string>()
      {
        "output"
      };
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics6 = modeSpecifics1;
      dictionary6.Add("stop", modeSpecifics6);

            modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
            modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>)((flags, args, options) =>
            {
                options.Mode = Mode.GetCurrentResult;
                if (Parser.CheckShowHelp(flags, args, options))
                {
                    options.ShowModeSpecificHelp = true;
                    return options;
                }
                if (args.Count != 1)
                    throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
                Parser.ParseAndSetSessionId(args[0], ref options);
                if (flags.ContainsKey("pause"))
                    options.Pause = true;
                return options;
            });

            modeSpecifics1.Syntax = string.Format((IFormatProvider)CultureInfo.InvariantCulture, "getcurrentresult {0}", (object)Resources.Arg_SessionId);
            modeSpecifics1.Description = "getcurrentresult";
            modeSpecifics1.SupportedFlags = (IList<string>)new List<string>()
      {
      };
            modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
            dictionary6.Add("getcurrentresult", modeSpecifics1);


            Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary7 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Pause;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
        Parser.ParseAndSetSessionId(args[0], ref options);

          return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "pause {0}", (object) Resources.Arg_SessionId);
      modeSpecifics1.Description = Resources.Description_Pause;
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics7 = modeSpecifics1;
      dictionary7.Add("pause", modeSpecifics7);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary8 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Resume;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
        Parser.ParseAndSetSessionId(args[0], ref options);
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "resume {0}", (object) Resources.Arg_SessionId);
      modeSpecifics1.Description = Resources.Description_Resume;
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics8 = modeSpecifics1;
      dictionary8.Add("resume", modeSpecifics8);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary9 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Status;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_SessionIdMustBeSpecified, Array.Empty<object>());
        Parser.ParseAndSetSessionId(args[0], ref options);
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "status {0}", (object) Resources.Arg_SessionId);
      modeSpecifics1.Description = Resources.Description_Status;
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics9 = modeSpecifics1;
      dictionary9.Add("status", modeSpecifics9);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary10 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.PostString;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 2)
          throw new ParsingException("{0}{1}{2}", new object[3]
          {
            (object) Resources.Error_SessionIdMustBeSpecified,
            (object) Environment.NewLine,
            (object) Resources.Error_MessageMustBeSpecified
          });
        Parser.ParseAndSetSessionId(args[0], ref options);
        if (string.IsNullOrEmpty(args[1]))
          throw new ParsingException(Resources.Error_MessageMustBeSpecified, Array.Empty<object>());
        options.Message = args[1];
          Console.WriteLine($"Message: {options.Message}");
        IList<string> argumentList;
        if (!flags.TryGetValue("agent", out argumentList))
          throw new ParsingException(Resources.Error_ValueMustBeSpecified, new object[1]
          {
            (object) "agent"
          });
        Guid result;
        if (!Guid.TryParse(Parser.ValidateAndGetFlagSingleOccurence("agent", argumentList), out result))
          throw new ParsingException(Resources.Error_InvalidValue, new object[2]
          {
            (object) "agent",
            (object) argumentList
          });
        options.AgentsByCLSID.Add(result, new RuntimeOptions.AgentToLoad());
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "postString {0} \"{1}\" /{2}:{3}", (object) Resources.Arg_SessionId, (object) Resources.Arg_Message, (object) "agent", (object) Resources.Syntax_PostToAgentFlag);
      modeSpecifics1.Description = Resources.Description_PostString;
      modeSpecifics1.SupportedFlags = (IList<string>) new List<string>()
      {
        "agent"
      };
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_SessionId,
          Resources.Description_SessionIdArg
        },
        {
          Resources.Arg_Message,
          Resources.Description_MessageArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics10 = modeSpecifics1;
      dictionary10.Add("postString", modeSpecifics10);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary11 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.ExpandDiagSession;
        if (Parser.CheckShowHelp(flags, args, options))
        {
          options.ShowModeSpecificHelp = true;
          return options;
        }
        if (args.Count != 1)
          throw new ParsingException(Resources.Error_DiagSessionMustBeSupplied, Array.Empty<object>());
        options.DiagSessionPath = args[0];
        return options;
      });
      modeSpecifics1.Syntax = string.Format((IFormatProvider) CultureInfo.InvariantCulture, "expandDiagSession {0}", (object) Resources.Arg_DiagSession);
      modeSpecifics1.Description = Resources.Description_ExpandDiagSession;
      modeSpecifics1.ExpectedArguments = new Dictionary<string, string>()
      {
        {
          Resources.Arg_DiagSession,
          Resources.Description_DiagSessionArg
        }
      };
      ModeSpecifics<RuntimeOptions> modeSpecifics11 = modeSpecifics1;
      dictionary11.Add("expandDiagSession", modeSpecifics11);
      Dictionary<string, ModeSpecifics<RuntimeOptions>> dictionary12 = dictionary1;
      modeSpecifics1 = new ModeSpecifics<RuntimeOptions>();
      modeSpecifics1.Consumer = (Func<IDictionary<string, IList<string>>, IList<string>, RuntimeOptions, RuntimeOptions>) ((flags, args, options) =>
      {
        options.Mode = Mode.Help;
        return options;
      });
      modeSpecifics1.Syntax = "help";
      modeSpecifics1.Description = Resources.Description_Help;
      ModeSpecifics<RuntimeOptions> modeSpecifics12 = modeSpecifics1;
      dictionary12.Add("help", modeSpecifics12);
      this.modeDictionary = dictionary1;

    }

    private static class Flags
    {
      internal const string Output = "output";
      internal const string Attach = "attach";
      internal const string Detach = "detach";
      internal const string Launch = "launch";
      internal const string LaunchArgs = "launchArgs";
      internal const string LoadAgent = "loadAgent";
      internal const string LifetimeProcess = "lifetimeProcess";
      internal const string Monitor = "monitor";
      internal const string PostToAgent = "agent";
      internal const string ScratchLocation = "scratchLocation";
      internal const string Package = "package";
    }
  }
}
