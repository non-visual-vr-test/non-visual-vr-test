using System.Collections;
using UnityEngine;
using UnityEditor;

public class ControllerManager : MonoBehaviour
{
    // Classes
    private Logs logsManager;
    private TargetManager targetManager;

    // Controller haptic adjustments for trigger and target contact behaviour
    [Header("Controller Haptics Settings")]
    // Controller haptics after trigger
    [SerializeField] private float controllerTriggerFreq = 0f;
    [SerializeField] private float controllerTriggerAmp = 0;
    [SerializeField] private float controllerTriggerDuration = 0f;
    // Controller haptics on target contact
    [SerializeField] private float controllerContactFreq = 1f;
    [SerializeField] private float controllerContactAmp = 1f;
    [SerializeField] private float controllerContactDuration = 0f;

    // Controller settings - setting hand
    [Header("Controller Settings")]
    public OVRInput.Controller controller;                          // Reference to the VR controller - left or right hand
    public Transform controllerAnchorPosition;                      // Reference to the controller anchor's position in the VR rig
    [SerializeField] private GameObject controllerCollider;         // Reference to the controller anchor gameobject used for interactions, i.e., the cube attached to controllerCollider
    [SerializeField] private float controllerAnchorWidth = 0.01f;   // Width of the cube connected to the controller anchor to make selections with default value - updated in targetmanager

    // Flags for tracking/logging
    private bool isFirstTriggerPress = true;    // Flag to ignore the first trigger press - start of test
    private bool isTrackingEnabled = false;     // Track controller position only after testing started until end

    // Flag for debugging if anchor error
    private bool controllerAnchorErrorLogged = false;

    // Input mode - handling allowed button inputs according to stage
    private InputMode currentInputMode = InputMode.None;        // Default current input mode to none

    /// <summary>
    /// Input modes - handling what buttons can be pressed according to stage of test, e.g., grip to start, trigger to seelect target
    /// </summary>
    /// <returns>Input mode enum - which buttons can currently be used - None, GripOnly, or TriggerOnly.</returns>
    public enum InputMode
    {
        None,
        GripOnly,
        TriggerOnly
    }

    #region Unity Lifecycle Methods

    /// <summary>
    /// Assign classes
    /// </summary>
    void Awake()
    {
        if (logsManager == null) logsManager = FindObjectOfType<Logs>();
        if (targetManager == null) targetManager = FindObjectOfType<TargetManager>();
    }

    /// <summary>
    /// Set controller anchor game object to the current controller anchor being used
    /// </summary>
    private void Start()
    {
        // Set controllerCollider to the GameObject sphere attached to the controllerAnchor associated with controllerAnchorPosition
        if (controllerAnchorPosition != null)
        {
            Transform sphereTransform = controllerAnchorPosition.Find("ControllerColliderSphere");
            if (sphereTransform != null)
            {
                controllerCollider = sphereTransform.gameObject;
            }
            else
            {
                Debug.LogError("Child object 'ControllerColliderSphere' not found under controllerAnchorPosition.");
            }
        }
        else
        {
            Debug.LogError("controllerAnchorPosition is not assigned in the Inspector.");
        }
    }

    /// <summary>
    /// Updating controller position and logging data during each frame
    /// </summary>
    private void Update()
    {
        // If tracking is not enabled, exit the update loop. Prevent logging until after first target selection
        if (!isTrackingEnabled) return;

        // Get the controller's position & rotation on each loop
        Vector3 position = GetControllerPosition();
        Quaternion rotation = GetControllerRotation();

        // Check if the trigger button is pressed
        bool isTriggerPressed = OVRInput.Get(OVRInput.Button.PrimaryIndexTrigger, controller);

        // Retrieve Phase, DistanceID, and DirectionID from TargetManager
        int phase = targetManager.IsTrainingPhase() ? 0 : 1;                // Determine if in training phase
        int distanceID = (int)targetManager.GetCurrentTargetDistance();     // Get current target distance ID
        int directionID = (int)targetManager.GetCurrentTargetDirection();   // Get current target direction ID
        float controllerWidth = targetManager.GetControllerWidth();         // Get the width of the controller anchor
        float targetWidth = targetManager.GetCurrentTargetWidth();          // Get the width of the current target

        // Logging every loop for continuous tracking and speed calculation
        LogControllerData(position, rotation, isTriggerPressed, phase, distanceID, directionID, controllerWidth, targetWidth);
    }

    #endregion

    #region Width

    /// <summary>
    /// Adjust width based on inspector value
    /// </summary>
    private void AdjustControllerWidth()
    {
        if (controllerCollider != null)
        {
            SphereCollider sphereCollider = controllerCollider.GetComponent<SphereCollider>();
            if (sphereCollider != null)
            {
                // Set the radius of the sphere collider based on controllerAnchorWidth
                sphereCollider.radius = controllerAnchorWidth;

                // Adjust the visual size of the controller collider based on controllerAnchorWidth
                controllerCollider.transform.localScale = Vector3.one * controllerAnchorWidth;
            }
            else
            {
                Debug.LogError("controllerCollider does not have a SphereCollider.");
            }
        }
        else
        {
            Debug.LogError("controllerCollider is not assigned.");
        }
    }

    /// <summary>
    /// Set controller anchor width to the width of the controlleranchor used in unity
    /// </summary>
    /// <param name="width">The new width for the controller anchor</param>
    public void SetControllerAnchorWidth(float width)
    {
        controllerAnchorWidth = width;
        AdjustControllerWidth();
    }

    /// <summary>
    /// Get controller anchor object
    /// </summary>
    /// <returns>GameObject representing the controllerAnchor currently besing used</returns>
    public GameObject GetControllerAnchor() => controllerCollider;

    /// <summary>
    /// Get controller anchor object's width
    /// </summary>
    /// <returns>float width of controllerAnchor currently besing used</returns>
    public float GetControllerAnchorWidth() => controllerAnchorWidth;

    #endregion

    #region Controller Haptics

    /// <summary>
    /// Starts continuous haptic feedback on the controller for target contact.
    /// Call this on contact start.
    /// </summary>
    public void StartContactHaptics()
    {
        // Vibrate the controller using the predefined contact frequency and amplitude
        OVRInput.SetControllerVibration(controllerContactFreq, controllerContactAmp, controller);
    }

    /// <summary>
    /// Method to trigger haptic feedback on the controller for set amount of time
    /// Not currently used - but might be useful in future
    /// </summary>
    /// <param name="freq">Frequency of the haptic feedback</param>
    /// <param name="amp">Amplitude of the haptic feedback</param>
    /// <param name="duration">Duration of the haptic feedback</param>
    public void TriggerHapticFeedback(float freq, float amp, float duration)
    {
        // Set the controller vibration with specified frequency and amplitude
        OVRInput.SetControllerVibration(freq, amp, controller);

        // Schedule the stopping of haptic feedback after a certain duration
        Invoke(nameof(StopHapticFeedback), duration);
    }

    /// <summary>
    /// Method to stop the haptic feedback on controller
    /// </summary>
    public void StopHapticFeedback()
    {
        // Reset vibration on all controllers to ensure haptics stop
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.All);
    }

    /// <summary>
    /// Lambda expression returns controller trigger haptic frequency
    /// </summary>
    public float ControllerFreq => controllerTriggerFreq;

    /// <summary>
    /// Returns controller trigger amp
    /// </summary>
    public float ControllerAmp => controllerTriggerAmp;

    /// <summary>
    /// Returns controller trigger duration
    /// </summary>
    public float ControllerDuration => controllerTriggerDuration;

    /// <summary>
    /// Returns controller contact freq
    /// </summary>
    public float ControllerContactFreq => controllerContactFreq;

    /// <summary>
    /// Returns controller contact amp
    /// </summary>
    public float ControllerContactAmp => controllerContactAmp;

    /// <summary>
    /// Returns controller contact duration
    /// </summary>
    public float ControllerContactDuration => controllerContactDuration;

    #endregion

    #region Tracking and Button States

    /// <summary>
    /// Start tracking after first trigger press, i.e., only after first target selected - used by TargetManager
    /// </summary>
    public void StartTracking()
    {
        isTrackingEnabled = true;
        // Enable logging in Logs
        logsManager.EnableLogging();
    }

    /// <summary>
    /// Stop tracking after last target clicked - used by TargetManager
    /// </summary>
    public void StopTracking()
    {
        isTrackingEnabled = false;
        // Disable logging in Logs
        logsManager.DisableLogging();
    }

    /// <summary>
    /// Reset flag to start tracking after first trigger press
    /// </summary>
    public void ResetFirstTriggerPress()
    {
        isFirstTriggerPress = true;
    }

    /// <summary>
    /// Handle the first trigger press and prevent logging initially
    /// </summary>
    public void HandleFirstTriggerPress()
    {
        if (isFirstTriggerPress)
        {
            StartTracking();                // Start tracking after first press
            isFirstTriggerPress = false;    // Disable flag after first press
        }
    }

    /// <summary>
    /// Setting input mode - what input buttons are allowed according to current stage
    /// </summary>
    /// <param name="mode">The input mode to be set</param>
    public void SetInputMode(InputMode mode)
    {
        currentInputMode = mode;
    }

    /// <summary>
    /// Get the current input mode - allowed button inputs from controller
    /// </summary>
    /// <returns>InputMode - current input mode setting.</returns>
    public InputMode GetCurrentInputMode() => currentInputMode;

    /// <summary>
    /// Check if tracking is enabled - mostly for debugging
    /// </summary>
    /// <returns>Bool true if trakcing enabled, false if tracking not currently enableed.</returns>
    public bool IsTrackingEnabled() => isTrackingEnabled;

    #endregion

    #region Position and Rotation

    /// <summary>
    /// Get the position of the controller anchor
    /// </summary>
    /// <returns>Vector3 position of controller anchor.</returns>
    public Vector3 GetControllerPosition()
    {
        if (controllerAnchorPosition != null)
        {
            // Return the current position of the controller anchor
            return controllerAnchorPosition.position;
        }
        else
        {
            // Log an error if the controller anchor is not assigned
            LogControllerAnchorError();
            // Return default zero position if there is an error
            return Vector3.zero;
        }
    }

    /// <summary>
    /// Get the controller anchor's rotation
    /// </summary>
    /// <returns>Quaternion rotation of controller anchor.</returns>
    public Quaternion GetControllerRotation()
    {
        if (controllerAnchorPosition != null)
        {
            // Return the current rotation of the controller anchor
            return controllerAnchorPosition.rotation;
        }
        else
        {
            // Log an error if the controller anchor is not assigned
            LogControllerAnchorError();
            // Return  default rotation if there is an error
            return Quaternion.identity;
        }
    }

    #endregion

    #region Logging

    /// <summary>
    /// Log position, rotation, and trigger state every loop
    /// </summary>
    /// <param name="position">The current position of the controller</param>
    /// <param name="rotation">The current rotation of the controller</param>
    /// <param name="isTriggerPressed">Whether the trigger button is pressed</param>
    /// <param name="phase">Current phase (training or testing)</param>
    /// <param name="distanceID">Identifier for the target distance</param>
    /// <param name="directionID">Identifier for the movement direction</param>
    /// <param name="controllerWidth">Width of the controlleranchor</param>
    /// <param name="targetWidth">Width of the target</param>
    private void LogControllerData(
        Vector3 position, 
        Quaternion rotation, 
        bool isTriggerPressed, 
        int phase, 
        int distanceID, 
        int directionID, 
        float controllerWidth, 
        float targetWidth)
    {
        if (logsManager != null)
        {
            // Calculate raw speed based on position change and time
            float rawSpeed = logsManager.CalculateSpeed(position);

            // Log the controller's position, rotation, trigger state, and other related data
            logsManager.LogControllerPositionRotationAndSpeed(position, rotation, isTriggerPressed, phase, distanceID, directionID, controllerWidth, targetWidth, rawSpeed);
        }
        else
        {
            Debug.LogError("ControllerManager: LogsManager is not assigned.");
        }
    }

    #endregion

    #region Helper Mehod

    /// <summary>
    /// Error handling for when controller anchor not set in Inspector
    /// </summary>
    private void LogControllerAnchorError()
    {
        if (!controllerAnchorErrorLogged)
        {
            Debug.LogError("ControllerManager: Controller anchor is not set.");
            // Prevent duplicate error logs
            controllerAnchorErrorLogged = true;
        }
    }

    #endregion
}