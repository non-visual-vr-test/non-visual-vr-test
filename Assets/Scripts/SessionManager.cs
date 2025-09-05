using UnityEngine;
using UnityEditor;
using System.IO;

public class SessionManager : MonoBehaviour
{
    // Classes
    private HapticsManager hapticsManager;
    private ControllerManager controllerManager;
    private TargetManager targetManager;
    private CameraManagement cameraManagement;
    private Logs logsManager;
    private TimeManagement timeManagement;

    // Control quitting after last target
    [Header("Quiting Settings")]
    public float quitAfterSeconds = 0f;                 // Number of seconds after last selection that Unity quits running in editor
    public bool quitAfterLastTarget = true;             // Flag to quit automatically

    private bool isPaused = false;              // Flag to control haptic feedback pause behaviour between sets
    private bool isTestingOver = false;         // Flag to indicate when testing is over

    #region Unity Lifecycle Method

    /// <summary>
    /// Assign classes
    /// </summary>
    void Awake()
    {
        if (hapticsManager == null) hapticsManager = FindObjectOfType<HapticsManager>();
        if (controllerManager == null) controllerManager = FindObjectOfType<ControllerManager>();
        if (targetManager == null) targetManager = FindObjectOfType<TargetManager>();
        if (cameraManagement == null) cameraManagement = FindObjectOfType<CameraManagement>();
        if (logsManager == null) logsManager = FindObjectOfType<Logs>();
        if (timeManagement == null) timeManagement = FindObjectOfType<TimeManagement>();
    }

    #endregion

    #region Pause State

    /// <summary>
    /// Handle midpoint alignment, camera setup, and grip press when paused
    /// </summary>
    public void HandlePausedState()
    {
        // Align the midpoint haptic object
        targetManager.LineUpMidpoint();

        // Move the camera rig so that the midpoint is directly in front of the participant at a set distance
        cameraManagement.MoveCameraRigToMidpoint();

        // Check if the participant presses the grip button to resume the target selection phase
        if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, controllerManager.controller))
        {
            // Start/resume testing
            isPaused = false;

            // Start/resume haptic vest feedback
            hapticsManager.ResumeHaptics();

            // Reset time for new pair
            timeManagement.ResetLastClickTime();

            // Set the controller input mode to trigger-only after the grip press
            controllerManager.SetInputMode(ControllerManager.InputMode.TriggerOnly);

            // Disable the midpoint after grip is pressed
            targetManager.DisableMidpointHaptic();
        }
    }

    /// <summary>
    /// On initial load, set isPaused to true to wait for grip press and pause haptics on targets until participant presses grip to start
    /// </summary>
    public void PauseHaptics()
    {
        // Pause flag
        isPaused = true;
        // Pause haptics
        hapticsManager.PauseHaptics();
    }

    /// <summary>
    /// Returns pause status
    /// </summary>
    /// <returns>True if paused, false otherwise</returns>
    public bool GetPauseState
    {
        get { return isPaused; }
    }

    #endregion

    #region End State

    /// <summary>
    /// Handle the end of testing session.
    /// </summary>
    public void EndTesting()
    {
        Debug.Log("SessionManager: Testing is complete.");

        // Stop haptics
        if (hapticsManager != null)
        {
            hapticsManager.StopAllHaptics();
        }

        // Stop tracking the controller
        if (controllerManager != null)
        {
            controllerManager.StopTracking();
        }

        // Open the logs folder
        OpenLogsFolder();

        // Initiate quitting the application after a set delay
        if (quitAfterLastTarget)
        {
            Invoke(nameof(QuitAfterDelay), quitAfterSeconds);
        }
    }

    /// <summary>
    /// Quit the application after a delay when running in Unity Editor.
    /// </summary>
    private void QuitAfterDelay()
    {
        // Stop playing the application in the Unity Editor
        EditorApplication.isPlaying = false;
    }

    /// <summary>
    /// Open the folder where the logs are stored.
    /// </summary>
    private void OpenLogsFolder()
    {
        if (logsManager != null)
        {
            // Get the path to the logs folder
            string logsFolderPath = logsManager.GetLogsFolderPath();

            // Check if the path is valid and the directory exists
            if (!string.IsNullOrEmpty(logsFolderPath) && Directory.Exists(logsFolderPath))
            {
                // Open the folder containing the logs
                Application.OpenURL("file://" + logsFolderPath);
                Debug.Log("SessionManager: Logs folder opened successfully.");
            }
            else
            {
                Debug.LogWarning("SessionManager: Logs folder not found or path is invalid.");
            }
        }
        else
        {
            Debug.LogWarning("SessionManager: LogsManager is not assigned.");
        }
    }

    /// <summary>
    /// Testing State Check - ending handling during loop
    /// </summary>
    /// <returns>True if testing is over, false otherwise</returns>
    public bool CheckIfTestingIsOver()
    {
        if (isTestingOver)
        {
            // Handle the end of testing
            EndTesting();

            // Prevent further processing by reseting flag
            isTestingOver = false;

            // Exit Update to prevent further processing
            return true;
        }
        return false;
    }

    /// <summary>
    /// Set isTestingOver to true
    /// </summary>
    public void SetTestingOver()
    {
        isTestingOver = true;
    }

    /// <summary>
    /// Returns if testing over
    /// </summary>
    /// <returns>True if testing is over, false otherwise</returns>
    public bool GetOverState
    {
        get { return isTestingOver; }
    }

    #endregion
}
