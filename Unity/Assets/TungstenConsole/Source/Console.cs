using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using UnityEngine;

namespace Tungsten.Console {
public class Console : MonoBehaviour {

#region Fields
    /// <summary>
    /// A scriptable object that contains the configuration for the console.
    /// This is lazy loaded on first use.
    /// </summary>
    private static ConsoleConfig _config;

    /// <summary>
    /// Tests whether a string is a drive root. e.g. "D:\"
    /// </summary>
    private const string REGEX_DRIVE_PATH = @"^\w:(?:\\|\/)?$";

    /// <summary>
    /// Tests whether a string is a drive root. e.g. "D:\"
    /// </summary>
    private static readonly Regex IsDrivePath = new(REGEX_DRIVE_PATH);

    /// <summary>
    /// Splits a string by spaces, unless surrounded by (unescaped) single or double quotes.
    /// </summary>
    private const string REGEX_STRING_SPLIT = @"(""[^""\\]*(?:\\.[^""\\]*)*""|'[^'\\]*(?:\\.[^'\\]*)*'|[\S]+)+";

    /// <summary>
    /// Tests whether a string starts and ends with either double or single quotes (not a mix).
    /// </summary>
    private const string REGEX_QUOTE_WRAPPED = @"^"".*""$|^'.*'$";

    /// <summary>
    /// Tests whether a string starts and ends with either double or single quotes (not a mix).
    /// </summary>
    private static readonly Regex IsWrappedInQuotes = new(REGEX_QUOTE_WRAPPED);

    private const string _helpTextFormat = "{0} : {1}";

    private static readonly Dictionary<string, ConsoleCommand> _commands = new();

    private static Action<ConsoleLog> _newLogAdded;
    private static readonly List<string> _commandHistory = new();
    private static readonly List<ConsoleLog> _logHistory = new();
#endregion Fields

#region Public
    public static ReadOnlyCollection<string> CommandHistory => _commandHistory.AsReadOnly();

    public static void Log(string logString, LogType logType = LogType.Log, bool doStackTrace = true) {
        CreateLog(logString, logType, doStackTrace, false, Color.white, Color.black);
    }

    public static void Log(string logString, Color textColor, Color bgColor, bool doStackTrace = true) {
        CreateLog(logString, LogType.Log, doStackTrace, true, textColor, bgColor);
    }

    public static void LogWarning(string logString) {
        CreateLog(logString, LogType.Warning, true, false, Color.white, Color.black);
    }

    public static void LogError(string logString) {
        CreateLog(logString, LogType.Error, true, false, Color.white, Color.black);
    }

    public static void LogAssert(string logString) {
        CreateLog(logString, LogType.Assert, true, false, Color.white, Color.black);
    }

    public static void LogException(string logString) {
        CreateLog(logString, LogType.Exception, true, false, Color.white, Color.black);
    }

    // todo: add remove command equivalent?
    public static void AddCommand(string commandName, CommandHandler handler, string helpText) {
        _commands.Add(commandName.ToLowerInvariant(), new ConsoleCommand(commandName, handler, helpText));
    }

    public static void ExecuteCommand(string command) {
        ParseCommand(command);
    }

    public static List<ConsoleCommand> GetOrderedCommands() {
        return _commands.Values.OrderBy(c => c.commandName).ToList();
    }

    public static string GetHistoryString(bool stripRichText = false) {
        var stringBuilder = new StringBuilder();

        foreach (var log in _logHistory) {
            stringBuilder.AppendLine(log.logString.Trim());
            if (log.stackTrace != "") {
                stringBuilder.AppendLine(log.stackTrace.Trim());
            }

            stringBuilder.Append(Environment.NewLine);
        }

        return (stripRichText
            ? Regex.Replace(stringBuilder.ToString(), "<.*?>", string.Empty)
            : stringBuilder.ToString()).Trim();
    }

    /// <summary>
    /// Copy console history to the clipboard (<see cref="GUIUtility.systemCopyBuffer" />).
    /// </summary>
    public static void CopyHistoryToClipboard(bool stripRichText = false) {
        GUIUtility.systemCopyBuffer = GetHistoryString(stripRichText);
    }

    public static void PrintHelpText() {
        foreach (var command in _commands.Values.OrderBy(c => c.commandName)) {
            Log(string.Format(_helpTextFormat, command.commandName, command.helpText), LogType.Log, false);
        }
    }

    /// <summary>
    /// Save console history to a log file and return the file's path.
    /// </summary>
    public static string SaveHistoryToLogFile(string path = "", string prefix = "console", bool stripRichText = false) {
        path = path.Trim();

        if (path == string.Empty) {
            path = Application.persistentDataPath;
        }
        else if (path.EndsWith(":")) {
            path += "\\";
        }
        else if (path.EndsWith("/")) {
            path = path.Replace("/", "\\");
        }

        if (IsDrivePath.IsMatch(path)) {
            if (Directory.GetLogicalDrives()
                         .All(drive => !drive.Equals(path, StringComparison.CurrentCultureIgnoreCase))) {
                LogError($"Drive not found: {path}");

                throw new Exception("Drive not found");
            }

            path = string.Format("{0}:", path[0]);
        }
        else if (Directory.Exists(Path.GetDirectoryName(path)) == false) {
            LogError($"Directory not found: {path}");

            throw new Exception("Directory not found");
        }

        path = $"{path}/{prefix}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
        File.WriteAllText(path, GetHistoryString(stripRichText));

        return path.Replace("\\", "/");
    }

    public static List<ConsoleLog> GetLogsSince(DateTime sinceTime) {
        if (_logHistory.Count > 0 && _logHistory[^1].timeStamp > sinceTime) {
            for (int i = _logHistory.Count - 1; i >= 0; i--) {
                if (_logHistory[i].timeStamp <= sinceTime) {
                    // we found a log that is older than sinceTime
                    // so we want to include ALL logs AFTER this one
                    // return the list from i + 1 to the end of the list
                    return _logHistory.GetRange(i + 1, _logHistory.Count - i - 1);
                }
            }

            // we didn't find a log that is older than sinceTime
            // so we want to include ALL logs
            return _logHistory;
        }

        // return _logHistory.Where(log => log.dateTime > sinceTime).ToList();
        return new List<ConsoleLog>();
    }

    public static void AddLogAddedListener(Action<ConsoleLog> callback) {
        _newLogAdded += callback;
    }

    public static void RemoveLogAddedListener(Action<ConsoleLog> callback) {
        _newLogAdded -= callback;
    }
#endregion Public

#region Private
    private static ConsoleConfig ConsoleConfig {
        get {
            if (_config == null) {
                _config = Resources.Load<ConsoleConfig>("ConsoleConfig");
            }

            return _config;
        }
    }

    private void Awake() {
        // Set dont destroy on load to this object.
        if (ConsoleConfig.SurviveSceneChanges) {
            DontDestroyOnLoad(gameObject);
        }

        // Add core commands.
        if (ConsoleConfig.EnableCoreCommands) {
            ConsoleCoreCommands.AddCoreCommands();
        }
    }

    private void Start() {
        Application.logMessageReceived += HandleUnityLog;
    }

    private void OnDestroy() {
        Application.logMessageReceived -= HandleUnityLog;
    }

    private static void CreateLog(
    string logString,
    LogType logType,
    bool doStackTrace,
    bool customColor,
    Color textColor,
    Color bgColor) {
        string stackTrace = doStackTrace ? new StackTrace().ToString() : string.Empty;
        ConsoleLog newLog = new ConsoleLog(logString, stackTrace, logType, DateTime.Now, customColor, textColor, bgColor);
        _logHistory.Add(newLog);
        _newLogAdded?.Invoke(newLog);
    }

    private static void CreateLog(string logString, string stackTrace, LogType logType) {
        ConsoleLog newLog = new(
            logString,
            stackTrace,
            logType,
            DateTime.Now,
            false,
            Color.white,
            Color.black
        );
        _logHistory.Add(newLog);
        _newLogAdded?.Invoke(newLog);
    }

    private static void ParseCommand(string commandString) {
        _commandHistory.Add(commandString);

        commandString = commandString.Trim();

        List<string> cmdSplit = ParseArguments(commandString);

        string cmdName = cmdSplit[0].ToLower();
        cmdSplit.RemoveAt(0);

        ConsoleLog newLog = new(
            $"> {commandString}",
            "",
            LogType.Log,
            DateTime.Now,
            false,
            Color.white,
            Color.black
        );
        _logHistory.Add(newLog);
        _newLogAdded?.Invoke(newLog);

        try {
            _commands[cmdName].handler(cmdSplit.ToArray());
        }
        catch (KeyNotFoundException) {
            LogError($"Command \"{cmdName}\" not found.");
        }
        catch (Exception) { }
    }

    private static List<string> ParseArguments(string commandString) {
        List<string> args = new();

        foreach (Match match in Regex.Matches(commandString, REGEX_STRING_SPLIT)) {
            var value = match.Value.Trim();

            if (IsWrappedInQuotes.IsMatch(value)) {
                value = value.Substring(1, value.Length - 2);
            }

            args.Add(value);
        }

        return args;
    }

    private static void HandleUnityLog(string logString, string stackTrace, LogType logType) {
        switch (logType) {
            case LogType.Error:
                if (_config.LogUnityErrors == false) {
                    return;
                }

                break;
            case LogType.Assert:
                if (_config.LogUnityAsserts == false) {
                    return;
                }

                break;
            case LogType.Warning:
                if (_config.LogUnityWarnings == false) {
                    return;
                }

                break;
            default:
            case LogType.Log:
                if (_config.LogUnityLogs == false) {
                    return;
                }

                break;
            case LogType.Exception:
                if (_config.LogUnityExceptions == false) {
                    return;
                }

                break;
        }

        CreateLog(logString, stackTrace, logType);
    }
#endregion Private

}
}