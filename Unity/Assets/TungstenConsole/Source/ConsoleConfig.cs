using UnityEngine;

namespace Tungsten.Console {
[CreateAssetMenu(menuName = "Tungsten/Console/Console Config Asset", fileName = "ConsoleConfig")]
public class ConsoleConfig : ScriptableObject {

#region Public
    public bool SurviveSceneChanges => _surviveSceneChanges;
    public int LogHistoryCapacity => _logHistoryCapacity;
    public int CommandHistoryCapacity => _commandHistoryCapacity;
    public bool EnableCoreCommands => _enableCoreCommands;
    public bool LogUnityErrors => _logUnityErrors;
    public bool LogUnityAsserts => _logUnityAsserts;
    public bool LogUnityWarnings => _logUnityWarnings;
    public bool LogUnityLogs => _logUnityLogs;
    public bool LogUnityExceptions => _logUnityExceptions;
#endregion Public

#region Private
    [Header("Instantiation")]
    [SerializeField] private bool _surviveSceneChanges = true;

    [Tooltip("Set to -1 to not limit the number of logs stored")]
    [SerializeField] private int _logHistoryCapacity = -1;

    [Tooltip("Set to -1 to not limit the number of commands stored")]
    [SerializeField] private int _commandHistoryCapacity = -1;

    [Header("Core Commands")]
    [SerializeField] private bool _enableCoreCommands = true;

    [Header("Unity Log Settings")]
    [SerializeField] bool _logUnityErrors = true;

    [SerializeField] private bool _logUnityAsserts = true;
    [SerializeField] private bool _logUnityWarnings = true;
    [SerializeField] private bool _logUnityLogs = true;
    [SerializeField] private bool _logUnityExceptions = true;
#endregion Private

}
}