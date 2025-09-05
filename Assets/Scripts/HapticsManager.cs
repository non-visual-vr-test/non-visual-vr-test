using Bhaptics.SDK2;
using Oculus.Haptics;
using System.Collections.Generic;
using UnityEngine;

public class HapticsManager : MonoBehaviour
{
    // Singleton instance - to control Meta Haptic SDK playback
    public static HapticsManager Instance;

    // Classes
    private TargetManager targetManager;
    private ControllerManager controllerManager;
    private Logs logsManager;
    private OvershootUndershootManager overshootUndershootManager;
    private SessionManager sessionManager;

    // enum for motor array - for each motor on vest
    public enum MotorIndex
    {
        Motor1 = 0,
        Motor2 = 1,
        Motor3 = 2,
        Motor4 = 3,
        Motor5 = 4,
        Motor6 = 5,
        Motor7 = 6,
        Motor8 = 7,
        Motor9 = 8,
        Motor10 = 9,
        Motor11 = 10,
        Motor12 = 11,
        Motor13 = 12,
        Motor14 = 13,
        Motor15 = 14,
        Motor16 = 15,
        Motor17 = 16,
        Motor18 = 17,
        Motor19 = 18,
        Motor20 = 19,
        Motor21 = 20,
        Motor22 = 21,
        Motor23 = 22,
        Motor24 = 23,
        Motor25 = 24,
        Motor26 = 25,
        Motor27 = 26,
        Motor28 = 27,
        Motor29 = 28,
        Motor30 = 29,
        Motor31 = 30,
        Motor32 = 31,
        Motor33 = 32,
        Motor34 = 33,
        Motor35 = 34,
        Motor36 = 35,
        Motor37 = 36,
        Motor38 = 37,
        Motor39 = 38,
        Motor40 = 39
    }

    // Vest haptic settings
    [Header("Vest Haptics Settings")]
    [SerializeField] private bool chestHaptics = false;     // Flag for haptic location - back only or back and front for depth-based haptics
    [SerializeField] private int minPercentage = 10;        // Smallest amount of vest feedback at largest distance
    [SerializeField] private int maxPercentage = 100;       // Largest amount of feedback when contacting target
    [SerializeField] private int vestHapticDuration = 20;   // bHaptics vest duration motor on (milliseconds)
    private bool areVestHapticsPaused = false;              // Flag for controlling haptic feedback pausing behaviour, e.g., between trials whilst participant is returning to midpoint
    private float axisSpecificDistance = 1;                 // Axis-specific distance between current and previous target
    private float targetDistance = 1f;                      // Distance between current and previous target for vest control with to avoid division by zero

    // Pulse growth pattern variables
    [Header("Pulse Growth Pattern Settings")]
    [SerializeField] private float maxPulseInterval = 0.5f; // Maximum pulse interval (500ms)
    [SerializeField] private float minPulseInterval = 0.1f; // Minimum pulse interval (100ms)
    private float pulseTimer = 0f;                          // Timer for pulse intervals
    private bool pulseState = false;                        // Flag to toggle between On and Off patterns

    // Stair pattern control
    private int totalSteps = 6;         // Steps for stairs
    private float stairSize = 18f;      // Size of stairs

    // Vest haptic patterns
    private TargetManager.GrowthPattern currentGrowthPattern;   // Current growth pattern from TargetManager

    // Vest haptic strength
    private const float MinimumDistance = 0.01f;    // To avoid division by zero when calculating distance between controller and target
    private float distanceRatio = 0f;               // The distance between the controller and the current target
    private float percentage = 0f;                  // Haptic strength based on distance as a percentage
    private int percentageInt = 0;                  // Above percentage as an int - based on bHaptics documentation of 0-100 int only

    // Haptic SDK for controller haptics designed in Meta Haptics Studio
    [Header("Meta Haptic SDK Settings")]
    [SerializeField] private HapticClip triggerHaptic;  // Haptic clip from Meta Haptics Studio
    private HapticClipPlayer player;                    // Haptic SDK player

    // Target control
    private GameObject currentTarget;               // Store teh current target
    private GameObject lastTarget;                  // Store the last target - to detect when it changes
    private TargetManager.DirectionType direction;  // The direction of the current target - DirectionType from targetmanager
    private Collider currentTargetCollider;         // Current target collider for distance measuring
    private Vector3 controllerPosition;             // Controller positon for distance measuring
    private Vector3 targetPosition;                 // Current target position for distance measuring

    // Dictionaries to map each target to specific vest motor indices
    private Dictionary<GameObject, TargetManager.DirectionType> targetToPatternMapTraining = new Dictionary<GameObject, TargetManager.DirectionType>();
    private Dictionary<GameObject, TargetManager.DirectionType> targetToPatternMapTesting = new Dictionary<GameObject, TargetManager.DirectionType>();

    #region Unity Lifecycle Methods

    /// <summary>
    /// Set up haptic player - meta haptic sdk.
    /// Assign classses
    /// </summary>
    private void Awake()
    {
        // Singleton pattern setup
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            // Destroy duplicate instance
            Destroy(gameObject);
        }

        // Persist across scenes for haptics SDK management
        DontDestroyOnLoad(gameObject);

        // Initialise the haptic player
        player = new HapticClipPlayer(triggerHaptic);

        if (targetManager == null) targetManager = FindObjectOfType<TargetManager>();
        if (controllerManager == null) controllerManager = FindObjectOfType<ControllerManager>();
        if (logsManager == null) logsManager = FindObjectOfType<Logs>();
        if (overshootUndershootManager == null) overshootUndershootManager = FindObjectOfType<OvershootUndershootManager>();
        if (sessionManager == null) sessionManager = FindObjectOfType<SessionManager>();
    }

    /// <summary>
    /// Intialise mappings for vest haptics based on target locations.
    /// </summary>
    private void Start()
    {
        InitialiseHapticMappings();
    }

    /// <summary>
    /// Calculating vest haptic pattern and strength every loop.
    /// </summary>
    void Update()
    {
        // Check if testing is over or paused to exit loop
        if (HandleEndOrPauseState()) return;

        // Get the current target from the TargetManager
        currentTarget = targetManager.GetCurrentTarget();
        // No current target to process - exit loop
        if (currentTarget == null)
        {
            Debug.LogError($"HapticsManager: No currentTarget set in TargetManager.");
            return;
        }

        // Get current target's collider from currentTarget
        currentTargetCollider = targetManager.GetCurrentTargetCollider();
        // No collider on current target - exit loop
        if (currentTargetCollider == null)
        {
            Debug.LogError($"HapticsManager: Collider component missing on currentTarget '{currentTarget.name}' in TargetManager.");
            return;
        }

        // Update the direction of the current target as DirectionType from targetmanager
        direction = GetCurrentDirection(currentTarget);

        // Check if the target has changed & update if necessary
        UpdateCurrentTarget(currentTarget);

        // Get the current position of the controller
        controllerPosition = controllerManager.GetControllerPosition();

        // Get the current target position
        targetPosition = targetManager.GetCurrentTargetCentrePosition();

        // Calculate the distance between the controller and the closest point on the target
        distanceRatio = CalculateDistanceRatio();

        // Convert to int between min and max strengths - for array mapping on vest, i.e., between 0-100 value in accordance with bHaptics documentation
        percentageInt = DetermineHapticPatternAndStrength(distanceRatio);

        // Set haptic pattern based on growth type
        HandleGrowthPattern(currentTarget, percentageInt);

        // Log the current haptic strength
        LogHapticStrength();
    }

    #endregion

    #region Initialisation Method

    /// <summary>
    /// Setting up dictionaires for tragets.
    /// </summary>
    private void InitialiseHapticMappings()
    {
        // Map training targets to directions
        foreach (var pair in targetManager.trainingPairs)
        {
            targetToPatternMapTraining[pair.target1] = pair.direction1;
            targetToPatternMapTraining[pair.target2] = pair.direction2;
        }

        // Map testing targets to directions
        foreach (var pair in targetManager.testingPairs)
        {
            if (pair.target1 != null && pair.target2 != null)
            {
                targetToPatternMapTesting[pair.target1] = pair.direction1;
                targetToPatternMapTesting[pair.target2] = pair.direction2;
            }
            else
            {
                // End of testing - no more mappings needed
                return;
            }
        }
    }

    #endregion

    #region Target Information

    /// <summary>
    /// Update current target only if not the last target.
    /// </summary>
    /// <param name="currentTarget">GameObecjt for current target</param>
    private void UpdateCurrentTarget(GameObject currentTarget)
    {
        // Only update if target has changed
        if (currentTarget != lastTarget)
        {
            // Update haptic growth pattern
            currentGrowthPattern = targetManager.GetCurrentGrowthPattern();

            // Collider error handling
            if (currentTargetCollider == null)
            {
                Debug.LogError("UpdateCurrentTarget: currentTargetCollider is null.");
                return;
            }

            // Update the distance between the two targets using axis-specific calculations
            UpdateTargetDistance();

            // Update last target
            lastTarget = currentTarget;
        }
    }

    /// <summary>
    /// Update the distance between the two targets to get percentage values for haptic strength.
    /// </summary>
    private void UpdateTargetDistance()
    {
        // Update the direction
        direction = GetCurrentDirection(currentTarget);

        // During training/testing after initial target selected
        if (lastTarget != null && lastTarget != currentTarget)
        {
            // Calculate axis-specific distance between targets
            axisSpecificDistance = GetAxisDistanceBetweenTargets(lastTarget.transform.position, currentTarget.transform.position, direction);
        }
        // Initial case: set targetDistance to the distance from the controller to the first target rather than between targets
        else
        {
            // Initial case: set targetDistance to the distance from the controller to the current target
            Vector3 initialControllerPosition = controllerManager.GetControllerPosition();
            // Calculate distance
            axisSpecificDistance = GetDistanceToTarget();
        }

        // Avoid division by zero
        targetDistance = Mathf.Max(axisSpecificDistance, MinimumDistance);
    }

    /// <summary>
    /// Update growth pattern.
    /// </summary>
    /// <param name="growthPattern">Gwroth pattern vest haptic</param>
    public void UpdateTargetInfo(TargetManager.GrowthPattern growthPattern)
    {
        currentGrowthPattern = growthPattern;
    }

    /// <summary>
    /// Returns current direction for a target, using TargetManager DirectionType.
    /// </summary>
    /// <param name="target">GameObject of current target</param>
    /// <returns><returns>DirectionType current direction for a target from TargetManager - left, right, forward, or back.</returns></returns>
    public TargetManager.DirectionType GetCurrentDirection(GameObject target)
    {
        // Get direction from training phase mappings
        if (targetManager.IsTrainingPhase())
        {
            if (targetToPatternMapTraining.TryGetValue(target, out TargetManager.DirectionType direction))
            {
                return direction;
            }
        }
        // Get direction from testing phase mappings
        else
        {
            if (targetToPatternMapTesting.TryGetValue(target, out TargetManager.DirectionType direction))
            {
                return direction;
            }
        }
        // Default to Off if not found
        return TargetManager.DirectionType.Off;
    }

    #endregion

    #region Haptic Strength and Pattern

    /// <summary>
    /// Log the haptic strength.
    /// </summary>
    private void LogHapticStrength()
    {
        if (logsManager != null)
        {
            // Log the current haptic strength
            logsManager.SetCurrentHapticStrength(percentageInt);
        }
        else
        {
            Debug.LogError("HapticsManager: LogsManager is not assigned.");
        }
    }    

    /// <summary>
    /// Determine if pulse or any other pattern - handle pulse growth pattern seperately due to need for pulse interval control.
    /// </summary>
    /// <param name="currentTarget">GameObject of current target</param>
    /// <param name="percentageInt">Int percentage value distance between controller and target</param>
    private void HandleGrowthPattern(GameObject currentTarget, int percentageInt)
    {
        if (currentGrowthPattern == TargetManager.GrowthPattern.Pulse)
        {
            HandlePulsePattern(currentTarget, percentageInt);
        }
        else
        {
            SetArrayForTarget(currentTarget, percentageInt);
        }
    }

    /// <summary>
    /// Setting vest direction and strength.
    /// </summary>
    /// <param name="target">GameObject active target</param>
    /// <param name="currentPercentage">Int current percentage baesd on distance</param>
    private void SetArrayForTarget(GameObject target, int currentPercentage)
    {
        // Set array based on bHaptics documentation - which motors to turn on and how much based on direction and current percentage
        int[] motorValueArray = GetMotorArrayForPattern(GetCurrentDirection(target), currentPercentage);
        // Play haptic feedback using the generated array
        PlayHapticFeedback(motorValueArray);
    }

    /// <summary>
    /// Interval control for pulse - based on min and max intervals and current percentage target distance
    /// </summary>
    /// <param name="currentTarget">GameObject current target</param>
    /// <param name="percentageInt">Int percentage distance between controller and target</param>
    private void HandlePulsePattern(GameObject currentTarget, int percentageInt)
    {
        // Calculate the interval for pulses based on distance (interpolates between max and min interval)
        // Timer can be changed in Unity Inspector
        float pulseInterval = Mathf.Lerp(maxPulseInterval, minPulseInterval, percentageInt / 100f);
        pulseTimer += Time.deltaTime;

        // Toggle pulse state when timer exceeds interval
        if (pulseTimer >= pulseInterval)
        {
            pulseTimer = 0f;
            // Alternate between On and Off states
            pulseState = !pulseState;
        }

        // Determine pattern based on pulseState
        int[] motorValueArray;

        if (pulseState)
        {
            // Use current pattern, i.e., direction on vest
            motorValueArray = GetMotorArrayForPattern(GetCurrentDirection(currentTarget), maxPercentage);
        }
        else
        {
            // Use Off pattern
            motorValueArray = GetMotorArrayForPattern(TargetManager.DirectionType.Off, 0);
        }

        // Play haptic feedback
        PlayHapticFeedback(motorValueArray);
    }

    /// <summary>
    /// Set pattern based on growth pattern and return percentageInt for strength control.
    /// </summary>
    /// <param name="distanceRatio">float distance between target and controller</param>
    /// <returns>Int between 0-100 for bHaptics motor control</returns>
    private int DetermineHapticPatternAndStrength(float distanceRatio)
    {
        // Intialise haptic strength
        percentage = 0f;

        // Haptic growth percentage control based on selected growth pattern
        switch (currentGrowthPattern)
        {
            case TargetManager.GrowthPattern.Linear:
                percentage = (1 - distanceRatio) * 100f;
                break;
            case TargetManager.GrowthPattern.Quadratic:
                percentage = (1 - Mathf.Pow(distanceRatio, 2)) * 100f;
                break;
            case TargetManager.GrowthPattern.Stair:
                percentage = CalculateStairPatternPercentage(distanceRatio);
                break;
            case TargetManager.GrowthPattern.Pulse:
                percentage = (1 - distanceRatio) * 100f;
                break;
            default:
                percentage = (1 - distanceRatio) * 100f;
                break;
        }

        // Clamp to int for bHapctics motor control between min and max strengths - percentage min/max between 0-100
        int currentPercentageInt = Mathf.Clamp((int)percentage, minPercentage, maxPercentage);
        return currentPercentageInt;
    }

    /// <summary>
    /// Get motor directions based on bHaptics documentation
    /// </summary>
    /// <param name="patternType">DiectionType direction between targets</param>
    /// <param name="percentageInt">int percentage value based on distance</param>
    /// <returns>Int array for 40 motors on bHaptics vest</returns>
    private int[] GetMotorArrayForPattern(TargetManager.DirectionType patternType, int percentageInt)
    {
        // Create new array and initialise all to 0 for off state
        int[] motorValueArray = new int[40];

        // Modify array based on pattern type and whether pattern on chest and back or back only for forward and back patterns
        switch (patternType)
        {
            case TargetManager.DirectionType.Left:
                motorValueArray[(int)MotorIndex.Motor21] = percentageInt;
                motorValueArray[(int)MotorIndex.Motor25] = percentageInt;
                motorValueArray[(int)MotorIndex.Motor29] = percentageInt;
                motorValueArray[(int)MotorIndex.Motor33] = percentageInt;
                break;

            case TargetManager.DirectionType.Right:
                motorValueArray[(int)MotorIndex.Motor24] = percentageInt;
                motorValueArray[(int)MotorIndex.Motor28] = percentageInt;
                motorValueArray[(int)MotorIndex.Motor32] = percentageInt;
                motorValueArray[(int)MotorIndex.Motor40] = percentageInt;
                break;

            case TargetManager.DirectionType.Forward:
                // Chest & back pattern
                if (chestHaptics)
                {
                    motorValueArray[(int)MotorIndex.Motor6] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor7] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor10] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor11] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor14] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor15] = percentageInt;
                }
                // Back only
                else
                {
                    motorValueArray[(int)MotorIndex.Motor21] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor22] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor23] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor24] = percentageInt;
                }
                break;

            case TargetManager.DirectionType.Back:
                // Chest & back pattern
                if (chestHaptics)
                {
                    motorValueArray[(int)MotorIndex.Motor26] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor27] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor30] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor31] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor34] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor35] = percentageInt;
                }
                // Back only
                else
                {
                    motorValueArray[(int)MotorIndex.Motor37] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor38] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor39] = percentageInt;
                    motorValueArray[(int)MotorIndex.Motor40] = percentageInt;
                }
                break;


            // Default to off state for all motors
            default:
                motorValueArray = new int[40];
                break;
        }

        // Set the motor values to the current percentage - between 0-100 in accordance to bHaptics documentation
        for (int i = 0; i < motorValueArray.Length; i++) motorValueArray[i] = (motorValueArray[i] > 0) ? percentageInt : 0;

        return motorValueArray;
    }

    /// <summary>
    /// Returns haptic strength percentage
    /// </summary>
    /// <returns>Int haptic strength</returns>
    public int GetHapticStrength() => percentageInt;

    #endregion

    #region Play and Stop Haptics

    /// <summary>
    /// bHaptics manual motor control from documentation.
    /// </summary>
    /// <param name="motorValueArray">Array of 40 for each motor on vest</param>
    public void PlayHapticFeedback(int[] motorValueArray)
    {
        // Bhaptics equipment type, array of each motor, duration on
        BhapticsLibrary.PlayMotors(
            (int)PositionType.Vest,
            motorValueArray,
            vestHapticDuration
        );
    }    

    /// <summary>
    /// Pause vest haptics.
    /// </summary>
    public void PauseHaptics()
    {
        areVestHapticsPaused = true;
        StopVest();
    }

    /// <summary>
    /// Resume vest haptics.
    /// </summary>
    public void ResumeHaptics()
    {
        areVestHapticsPaused = false;
        // Ensure that lastTarget is updated
        lastTarget = null;
    }

    /// <summary>
    /// From bHaptics documentation to stop all motors.
    /// </summary>
    public void StopVest()
    {
        BhapticsLibrary.StopAll();
    }

    /// <summary>
    /// Play haptics SDK on controllers.
    /// </summary>
    public void PlayHapticClip()
    {
        // Play haptic clip on both controllers
        player.Play(Controller.Both);
    }

    /// <summary>
    /// Stop all haptics.
    /// </summary>
    public void StopAllHaptics()
    {
        // Stop haptic feedback on vest
        StopVest();
        // Stop manual controller haptics
        controllerManager.StopHapticFeedback();
        // Haptics SDK for controller
        player.Stop();
    }

    /// <summary>
    /// Meta Haptic SDK cleaner.
    /// </summary>
    private void OnDestroy()
    {
        // Dispose of haptic player resources
        player.Dispose();
    }

    /// <summary>
    /// Meta Haptics SDK disposing on quit.
    /// </summary>
    private void OnApplicationQuit()
    {
        // Dispose of haptic resources on application quit
        Haptics.Instance.Dispose();
    }

    /// <summary>
    /// Check if testing is over or paused.
    /// </summary>
    /// <returns>Bool - true if endstate or pause state.</returns>
    private bool HandleEndOrPauseState()
    {
        if (HandleEndState() || HandlePauseState())
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handle haptics when ending, i.e., turn all off.
    /// </summary>
    /// <returns>Bool true if testing over.</returns>
    private bool HandleEndState()
    {
        // Check if testing is over
        if (sessionManager.GetOverState)
        {
            StopAllHaptics();
            return true;
        }
        return false;
    }

    /// <summary>
    /// Handle haptics on vest when paused.
    /// </summary>
    /// <returns>Bool true if testing paused, i.e., between blocks.</returns>
    private bool HandlePauseState()
    {
        // Check if haptics are paused
        if (areVestHapticsPaused)
        {
            return true;
        }
        return false;
    }

    #endregion

    #region Distance

    /// <summary>
    /// Calculate axis-specific distance between targets.
    /// </summary>
    /// <param name="position1">Vector3 position of first target</param>
    /// <param name="position2">Vector3 position of second target</param>
    /// <param name="direction">DirectionType betweeen targets</param>
    /// <returns>Float axis-specific distance between two current targets.</returns>
    private float GetAxisDistanceBetweenTargets(Vector3 position1, Vector3 position2, TargetManager.DirectionType direction)
    {
        float distanceBetweenTargets = 0f;

        // Determine axis-specifc distance based on direction
        switch (direction)
        {
            case TargetManager.DirectionType.Right:
            case TargetManager.DirectionType.Left:
                // Absolute distance value on x-axis
                distanceBetweenTargets = Mathf.Abs(position1.x - position2.x);
                break;
            case TargetManager.DirectionType.Forward:
            case TargetManager.DirectionType.Back:
                // Absolute distance value  on z-axis
                distanceBetweenTargets = Mathf.Abs(position1.z - position2.z);
                break;
            default:
                // Non axis-specific distance
                distanceBetweenTargets = Vector3.Distance(position1, position2);
                break;
        }

        return distanceBetweenTargets;
    }

    /// <summary>
    /// Calulate the current distance ratio between controller position and target for haptic strength adjustment.
    /// </summary>
    /// <returns>Float distance between controller anchor and current target.</returns>
    private float CalculateDistanceRatio()
    {
        // Set the current distance to target
        float currentDistanceToTarget = GetDistanceToTarget();

        // Adjust targetDistance variable if initial case
        if (targetDistance <= 0f) targetDistance = currentDistanceToTarget;

        // Prevent division by zero - if smaller than the smallest value a float can have above zero, i.e., zero
        if (targetDistance < Mathf.Epsilon)
        {
            Debug.LogWarning("CalculateDistanceRatio: targetDistance is too small, clamping to MinimumDistance.");
            // Set to minimum distance to prevent division by zero
            targetDistance = MinimumDistance;
        }

        // Clamp distanceRatio to [0,1] for strength calculations in each growth pattern
        float currentDistanceRatio = Mathf.Clamp01(currentDistanceToTarget / targetDistance);
        return currentDistanceRatio;
    }

    /// <summary>
    /// Specific stair pattern calculation.
    /// </summary>
    /// <param name="distanceRatio">float distance between controller and target</param>
    /// <returns>Float for current stair pattern strength.</returns>
    private float CalculateStairPatternPercentage(float distanceRatio)
    {
        // Clamp to int based on number of steps
        int step = Mathf.Clamp(Mathf.FloorToInt((1 - distanceRatio) * totalSteps), 0, totalSteps - 1);
        return minPercentage + step * stairSize;
    }

    /// <summary>
    /// Get the distance to the target's collider from controller anchor based on direction.
    /// </summary>
    /// <returns>Float distance from controller to target collider</returns>
    private float GetDistanceToTarget()
    {
        // Use the overshoot/undershoot manager to calculate the distance to target collider
        return overshootUndershootManager.GetDistanceToTargetCollider(direction, controllerPosition, targetPosition, currentTargetCollider);
    }

    #endregion    
}