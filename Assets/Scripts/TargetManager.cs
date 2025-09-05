using System.Collections;
using System.Collections.Generic;
using System.Timers;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.XR;
using static TargetManager;

public class TargetManager : MonoBehaviour
{
    // Classes
    private ControllerManager controllerManager;                    // Manages controller inputs and haptics
    private HapticsManager hapticsManager;                          // Handles haptic feedback
    private Logs logsManager;                                       // Manages logging of trial data
    private TargetSetup targetSetup;                                // Sets up target objects in the scene
    private TimeManagement timeManagement;                          // Manages timing for trials
    private CameraManagement cameraManagement;                      // Controls camera behaviour
    private FittsLaw fittsLawManager;                               // Implements Fitts' Law calculations
    private SessionManager sessionManager;                          // Manages session states - mostly closing behaviour
    private OvershootUndershootManager overshootUndershootManager;  // Calulates overshooting and undershooting metrics
    private VisualsManager visualsManager;                          // Handles visual feedback and target visuals

    // Phase enum
    public enum PhaseType
    {
        Training = 0,
        Testing = 1
    }

    // DirectionType enum - target directions from midpoint
    public enum DirectionType
    {
        Forward = 0,    // + z position
        Right = 1,      // + x position
        Back = 2,       // - z position
        Left = 3,       // - x position
        Off = 4         // Off for pulse control only & error handling if direction not found
    }

    // TargetWidthType enum - target widths along movement axis
    public enum TargetWidthType
    {
        Training = 0,   // 2.5cm -- 0.025 x scale
        Small = 1,      // 1.5cm -- 0.015 x scale
        Large = 2       // 3.5cm -- 0.035 x scale
    }

    // DistanceType enum - target distances along movement axis
    public enum DistanceType
    {
        Training = 0,       // 24cm -- +/- 0.12 position
        Short = 1,          // 10cm -- +/- 0.05 position
        Medium = 2,         // 20cm -- +/- 0.1 position
        Long = 3            // 30cm -- +/- 0.15 position
    }

    // AxisType enum - movement axis
    public enum AxisType
    {
        Horizontal,     // x axis
        Vertical        // z axis
    }

    // GrowthPattern enum - haptic feedback growth patterns
    public enum GrowthPattern
    {
        Linear,     // 0
        Quadratic,  // 1
        Stair,      // 2
        Pulse       // 3
    }

    // Setting targets in Inspector - pairing targets, width, directions, distance, and haptic growth pattern
    [System.Serializable]
    public class TargetPair
    {
        public DirectionType firstTargetDirection;      // Direction of the first target in the pair
        public TargetWidthType targetWidth;             // Width category of the targets
        public DistanceType distance;                   // Distance category of the targets
        public GrowthPattern growthPattern;             // Haptic feedback growth pattern for the targets

        [HideInInspector]
        public int targetID;                            // Unique identifier for target pair - 0-6 based on TargetWidthType and DistanceType - i.e., Fitts Law IDs

        [HideInInspector]
        public GameObject target1;                      // First target GameObject in the pair
        [HideInInspector]
        public GameObject target2;                      // Second target GameObject in the pair
        [HideInInspector]
        public DirectionType direction1;                // Direction of the first target
        [HideInInspector]
        public DirectionType direction2;                // Direction of the second target
        [HideInInspector]
        public int pairIndex;                           // Index of the pair within the list
    }

    // Block letters
    private string blockLetter = "";            // Block letter based on haptic pattern & direction
    
    // Block letter dictionary
    private static Dictionary<(AxisType, GrowthPattern), string> BlockLetterMap = new Dictionary<(AxisType, GrowthPattern), string>
    {
        {(AxisType.Horizontal, GrowthPattern.Quadratic), "A"},
        {(AxisType.Vertical, GrowthPattern.Quadratic), "B"},
        {(AxisType.Horizontal, GrowthPattern.Pulse), "C"},
        {(AxisType.Vertical, GrowthPattern.Pulse), "D"},
        {(AxisType.Horizontal, GrowthPattern.Linear), "E"},
        {(AxisType.Vertical, GrowthPattern.Linear), "F"},
        {(AxisType.Horizontal, GrowthPattern.Stair), "G"},
        {(AxisType.Vertical, GrowthPattern.Stair), "H"}
    };

    // Target identifier dictionary - maps TargetID to TrialNumber
    private Dictionary<int, int> trialNumberByIDDict = new Dictionary<int, int>();      // Key: TargetID, Value: TrialNumberByID - Based on unique target ID

    // Target pairs lists - for currently active target selection control in a set
    public List<TargetPair> trainingPairs = new List<TargetPair>();             // List of training target pairs
    public List<TargetPair> testingPairs = new List<TargetPair>();              // List of testing target pairs
    private int targetIndex = 0;                                                // Index number of current target

    // Target control
    private List<TargetPair> currentPairList;   // Reference to the current list of target pairs in a set
    private int currentPairIndex = 0;           // Index to the current pair index in above list
    private TargetPair currentPair;             // TargetPair object to keep track of current pair during runtime
    private TargetPair previousPair;            // Keeps track of the previous target pair
    private GameObject currentTarget = null;    // Current target pointer
    private GameObject previousTarget = null;   // Previous target pointer

    // Target positions
    private Vector3 previousTargetPosition;     // Tracking previous target position
    private DistanceType previousDistance;      // Tracking prvious distance between targets for set change
    private DirectionType previousDirection;    // Tracking previous direction for set change
    private DirectionType direction;            // Direction of current target

    // Midpoint haptic
    private GameObject midpointHaptic;          // Midpoint hapitc gameobject
    private bool wasPaused = true;              // Flag for pause state - midpoint haptic lineup. Disables midpoint haptic gameobject after pause finsihed
    private float midpointWidth;                // Midpoint haptic gameobject width

    // Collision tracking - target
    private Collider targetCollider;            // The collider of the current target to track contact
    private Vector3 targetMidpoint;             // Midpoint location of target object
    private bool isContactingTarget = false;    // True if contacting target - flag to ensure controller haptics happens only once per target contact
    private bool wasTargetContacted = false;    // Flag to determine if new contact

    // Collision tracking - controller
    private Collider controllerCollider;        // The controller's collider
    private float controllerWidth = 0f;         // Width of the controller anchor for determining contact with targets & midpoint
    
    // Collision tracking - midpoint
    private bool isMidpointContacted = false;   // Flag to ensure controller haptics happens only once per midpoint contact

    // Dwell time with target
    private float dwellStartTime = 0f;          // Time when the controller first contacts the target
    private float totalDwellTime = 0f;          // Total dwell time for logging

    // Trial counters
    private int trialCount = 0;                     // Total trials in a set (including the first unlogged target selection)
    private int loggedTrialCount = 0;               // Counter for the number of trials data is logged (excluding the first selection)
    private bool isFirstSelectionInTrial = true;    // Flag to indicate first selection in a new trial so data not logged

    // Trial counter logging
    private int trialNumberInSet = 0;       // Resets per target set
    private int trialNumberInBlock = 0;     // Continuously increments in block
    private int setNumber = 1;              // Starts at 1 - counts sets in current block
    private int trainingSetNumber = 1;      // Set number for training only
    private int testSetNumber = 0;          // Set nnumber for testing only

    // Controller movement tracking
    private bool isTrackingMovement = false;                                    // Flag for movement logging
    private List<Vector3> controllerMovementPositions = new List<Vector3>();    // Vector3 positions list for controllre positions every loop during tracking phase
    private Vector3 previousPositionOnTriggerPosition;                          // Vector3 position of controller at previous trigger press, i..e., controller position when previous target selected
    private Vector3 currentPositionOnTriggerPress;                              // Vector3 position of controller at current trigger press, ie., controller position for current target selection
    private Vector3 currentControllerPosition;                                  // Current controller position
    private Quaternion controllerRotation;                                      // Controller rotation at end
    private Vector3 startControllerPosition;                                    // Controller position for start
    private Quaternion startControllerRotation;                                 // Controller rotation at start
    private Vector3 previousControllerPosition = Vector3.zero;                  // Position of controller at last loop

    // Flags for effective calculations for Fitts Law
    private bool isLastSelectionInPair = false;                 // Flag to indicate the last selection in the set to begin effective calculations at set end
    private bool isLastSelectionInBlock = false;                // Flag to indicate last selection in whole block

    // Movement axis calculations
    private int movementAxis = 0;                               // 0 = x-axis movement (Left/Right) or 1 = z-axis movement (Forward/Back)
    private float currentAxisDistance = 0f;                     // Total movement along movement axis
    private float differenceToAmplitude = 0f;                   // Difference between total movement along axis and amplitude
    private float currentAxisNetDistance = 0f;                  // Total straight-line net movement distance across axis - between trigger presses not accounting for overshot
    private float totalPathLength = 0f;                         // Total path across all axes
    private float euclideanDeviation = 0f;                      // To calculate deviations from straight-line - any movements not along current axis
    private float amplitude = 0f;                               // Straight-line distance between targets
    private float aggregateMovementDistanceAlongAxis = 0f;      // Total aggregate axis movement across set
    private float aggregateMovementDistanceAlongAllAxes = 0f;   // Total aggregate all axes movement across set
    private Vector3 currentMovementAxis = Vector3.forward;      // Current movement axis direction - x or z

    // Trial control
    [Header("Trial control")]
    [SerializeField] private bool skipTraining = false;         // Toggle to skip training phase
    [SerializeField] private bool skipTesting = false;          // Allow skipping of testing phase
    [SerializeField] private int trainingTrials = 9;            // Number of logged selections/trials for each training set
    [SerializeField] private int testingTrials = 9;             // Number of logged selections/trials for each testing set
    [SerializeField] private int trialsInBlock = 63;            // Total number of trials in a block to work out fitts law calulations at end (2x traing + 6x testing, with 11 trials each)
    private bool isTrainingPhase = true;                        // Flag to determine training or testing phase

    // Switching targets in trial
    private bool canSwitch = true;              // Flag to control when the user can switch to prevent multiple inputs per trigger button press
    private float inputDelay = 0.1f;            // Input trigger delay (to prevent multiple trigger clicks being registered too fast)

    // Reaction time
    private float reactionTime = 0f;                        // Time between target appearance and movement initiation in the target's direction (e.g., positive x-axis for a right target)
    private float targetAppearanceTime;                     // Timestamp when the target appeared
    private bool reactionTimeRecorded = false;              // Flag to ensure reactionTime is recorded only once per target

    // Path curvature
    private float euclideanDistance = 0f;                   // Straight-line path
    private float pathCurvature = 0f;                       // Measure of how much the path deviates from a straight line (straight line = 1)
    private float directionThreshold = 0.5f;                // Threshold to match directions. 0.5 = movements that are within 60 degrees of the target direction are considered as moving towards the target using formalua cos^-1 (0.5) = 60°

    // Aggregate distances
    private float aggregateNetMovementDistanceAlongAxisForSet = 0f;     // Straight-line net movement along axis for set
    private float aggregateMovementDistanceAlongAxisForSet = 0f;        // Movement axis only aggregate for set
    private float aggregateMovementDistanceAlongAllAxesForSet = 0f;     // All axes aggregate for set
    private float aggregateMovementDistanceAlongAxisForBlock = 0f;      // Movement axis only aggregate for block
    private float aggregateNetMovementDistanceAlongAxisForBlock = 0f;   // Straight-line net movement along axis for block
    private float aggregateMovementDistanceAlongAllAxesForBlock = 0f;   // All axes aggregate for block
    private float euclideanDistanceForSet = 0f;                         // Euclidean straight line distance for set
    private float euclideanDistanceForBlock = 0f;                       // Euclidean straight line distance for block
    private float euclideanDeviationForSet = 0f;                        // Deviatations from straight-line for set
    private float euclideanDeviationForBlock = 0f;                      // Deviations from straight-line for block

    // Class-level variables for logging
    private bool previousIsHit;                             // Logging if just hit target
    private float previousDistanceToTargetCollider;         // Distance to closest point on just selected target collider
    private Vector3 previousTargetMidpoint;                 // Previous target midpoint location
    private float previousDistanceToTargetMidpoint;         // Distance to midpoint of previous target
    private float previousTimeTaken;                        // Time taken for last selection
    private PhaseType previousPhase;                        // PhaseType of previous selection
    private DistanceType previousDistanceID;                // DistanceType of previous selection
    private DirectionType previousDirectionID;              // DirectionType of previous selection
    private float previousOvershootDistanceForLogging;      // Total overshoot distance of previous target selection
    private float previousUndershootDistanceForLogging;     // Total undershoot distance of previous target selection
    private float previousBallisticDistanceAlongAxis;       // Ballistic distance along movement axis for previous target
    private float previousBallisticDistanceAlongAllAxes;    // Ballistic distance along all axes for previous target
    private float previousCorrectionDistanceAlongAxis;      // Correction distance along movement axis for preious target
    private float previousCorrectionDistanceAlongAllAxes;   // Correction distance along all axes for previous target

    #region Unity Lifecycle Methods

    /// <summary>
    /// Assigns classes
    /// </summary>
    void Awake()
    {
        // Assign all classes
        if (targetSetup == null) targetSetup = FindObjectOfType<TargetSetup>();
        if (controllerManager == null) controllerManager = FindObjectOfType<ControllerManager>();
        if (hapticsManager == null) hapticsManager = FindObjectOfType<HapticsManager>();
        if (logsManager == null) logsManager = FindObjectOfType<Logs>();
        if (timeManagement == null) timeManagement = FindObjectOfType<TimeManagement>();
        if (cameraManagement == null) cameraManagement = FindObjectOfType<CameraManagement>();
        if (fittsLawManager == null) fittsLawManager = FindObjectOfType<FittsLaw>();
        if (sessionManager == null) sessionManager = FindObjectOfType<SessionManager>();
        if (overshootUndershootManager == null) overshootUndershootManager = FindObjectOfType<OvershootUndershootManager>();
        if (visualsManager == null) visualsManager = FindObjectOfType<VisualsManager>();
    }

    /// <summary>
    /// Initialises all target behaviours, sets up the environment, and prepares the first target
    /// </summary>
    private void Start()
    {
        // Initialise midpoint haptic object and camera
        InitialiseMidpointHaptic();

        // Ensure logged info for first target is reset and not recorded & haptics only start with grip
        ResetCounters();

        // Ensure movement aggregate counts reset
        ResetAggregatesForBlock();

        // Reset target contact bool
        ResetTargetContact();

        // Assign targetIDs based on configurations
        AssignTargetIDs();

        // Initialise dictionary
        InitialiseTrialNumberDict();

        // On initial load, set isPaused to true to wait for grip press and pause haptics on targets until participant presses grip to start
        sessionManager.PauseHaptics();

        // On initial load, turn off all inputs except grip to start target selection
        GripOnly();

        // Setting the controlleranchor and intialise width
        SetControllerAnchor();

        // Initialise targets - setup, visuals, assigning to list, setting training or testing phase
        InitialiseTargets();

        // Start with the first pair of targets in list and first target of pair
        FirstTargetSetup();

        // Initialise distances, directions, and position - for effective fitts law calculations
        InitialiseEffectiveMeasures();

        // Initilaise visuals
        InitialiseVisuals();

        // Adjust the width of the midpointHaptic object
        AdjustMidpointHapticWidth();
    }

    /// <summary>
    /// Handles grip and trigger press behaviour, updates controller position, and manages the paused state
    /// </summary>
    private void Update()
    {
        // Error handling for controller manager
        if (controllerManager == null)
        {
            Debug.LogError("ControllerManager is not assigned.");
            // Exit loop if controllermanager not assigned
            return;
        }

        // Check if testing is over to exit loop
        if (sessionManager.CheckIfTestingIsOver()) return;

        // Get controller position and rotation, and add controller poistion to list if tracking
        TrackControllerMovement();

        // Update the width of the midpointHaptic object if changed during runtime
        AdjustMidpointHapticWidth();

        // Continuously check for movement towards the target to record reaction time if not recorded for target and flagged to record
        if (!reactionTimeRecorded && isTrackingMovement) CalculateReactionTime();

        // Set the collider of the current target
        SetTargetCollider();

        // If paused, i.e., at start or between sets, midpoint alignment phase
        PauseControl();

        // Check if trigger input is allowed, i.e., during target selection phase - swich to target selection loop behaviour
        if (controllerManager.GetCurrentInputMode() == ControllerManager.InputMode.TriggerOnly) TargetSelectionPhaseLoop();
    }

    #endregion

    #region Initialisation Methods

    /// <summary>
    /// Initialise targets - setup, visuals, assigning to list, setting training or testing phase
    /// </summary>
    private void InitialiseTargets()
    {
        // Setup target locations, scale etc.
        targetSetup.InitialiseTargets(trainingPairs, testingPairs);

        // Initialise visuals
        if (visualsManager != null)
        {
            List<TargetPair> pairsToInitialise = skipTraining ? testingPairs : trainingPairs;
            visualsManager.InitialiseVisuals(pairsToInitialise, skipTraining);
        }

        // Assign currentPairList based on phase, create a new list to avoid modifying the original serialised list set by the user in the inspector
        if (skipTraining && skipTesting)
        {
            // Skipping both - end testing
            Debug.LogError("Both skipTraining and skipTesting are true. Nothing to run, testing is over.");
            sessionManager.SetTestingOver();
            return;
        }
        else if (skipTraining)
        {
            currentPairList = new List<TargetPair>(testingPairs);
        }
        else
        {
            currentPairList = new List<TargetPair>(trainingPairs);
        }

        // Setup first phase - training or testing
        InitialiseFirstPhase();

        // Randomise the order of testing pairs if starting with testing phase - random order of IDs
        if (!isTrainingPhase) ShuffleList(currentPairList);

        // Ensure first targets selected from list
        currentPairIndex = 0;
    }

    /// <summary>
    /// Initialise first phase and initialise set numbers according to if training skipped
    /// </summary>
    private void InitialiseFirstPhase()
    {
        // If both skip trainign and skip testing flags true, end testing
        if (skipTraining && skipTesting)
        {
            Debug.LogError("Both skipTraining and skipTesting are true. Nothing to run, testing ending.");
            sessionManager.SetTestingOver();
            return;
        }
        // Set trainingSetNumber to 1 and testSetNumber to 0 if starting with training phase
        else if (!skipTraining)
        {
            isTrainingPhase = true;
            trainingSetNumber = 1;
            testSetNumber = 0;
        }
        // Set testSetNumber to 1 and trainingSetNumber to 0 if starting directly with testing phase
        else
        {
            isTrainingPhase = false;
            trainingSetNumber = 0;
            testSetNumber = 1;
        }
    }

    /// <summary>
    /// Initilaise visuals
    /// </summary>
    private void InitialiseVisuals()
    {
        // Initialise feedback counter
        visualsManager.ResetVisualCounters();

        // Disable visuals at Start if targetsVisualsToggle is false
        if (visualsManager != null && !visualsManager.AreVisualsEnabled()) visualsManager.DisableVisuals(trainingPairs);
    }

    /// <summary>
    /// Initialise distances, directions, and position - for effective fitts law calculations
    /// </summary>
    private void InitialiseEffectiveMeasures()
    {
        // Initialise distance
        previousDistance = currentPair.distance;

        // Initialise direction
        previousDirection = GetCurrentTargetDirection();

        // Initialise previousTargetPosition to the position of the initial target
        previousTargetPosition = currentTarget.transform.position;
    }

    /// <summary>
    /// Initialises the trial number dictionary with target IDs from training and testing pairs
    /// </summary>
    private void InitialiseTrialNumberDict()
    {
        // Initialise for Training Pairs
        foreach (var pair in trainingPairs)
        {
            int targetID = pair.targetID;
            if (targetID == -1)
            {
                Debug.LogWarning($"[Training] Pair {pair.target1.name} & {pair.target2.name} has invalid targetID=-1. Skipping.");
                continue;
            }

            if (!trialNumberByIDDict.ContainsKey(targetID))
            {
                trialNumberByIDDict[targetID] = 0;
                Debug.Log($"[Dictionary Init] Added targetID={targetID} with initial trialNumberByID=0");
            }
        }

        // Initialise for Testing Pairs
        foreach (var pair in testingPairs)
        {
            int targetID = pair.targetID;
            if (targetID == -1)
            {
                Debug.LogWarning($"[Testing] Pair {pair.target1.name} & {pair.target2.name} has invalid targetID=-1. Skipping.");
                continue;
            }

            if (!trialNumberByIDDict.ContainsKey(targetID))
            {
                trialNumberByIDDict[targetID] = 0;
                Debug.Log($"[Dictionary Init] Added targetID={targetID} with initial trialNumberByID=0");
            }
        }
    }

    /// <summary>
    /// Set midpoint haptic width
    /// </summary>
    private void InitialiseMidpointHaptic()
    {
        // Error handling for camera
        if (cameraManagement == null) Debug.LogError("CameraManagement is not assigned in TargetManager.");

        // Set up midpoint haptic game object
        (midpointHaptic, midpointWidth) = visualsManager.GetMidpointHapticAndWidth();
    }

    /// <summary>
    /// Sets controller anchor object and width
    /// </summary>
    private void SetControllerAnchor()
    {
        // Set gameobject based on controller anchor
        GameObject controllerColliderObject = controllerManager.GetControllerAnchor();

        // Set collider if controller anchor exists
        if (controllerColliderObject != null)
        {
            // Find collider with error handling
            controllerCollider = controllerColliderObject.GetComponent<Collider>();
            if (controllerCollider == null)
            {
                Debug.LogError("ControllerCollider GameObject does not have a Collider component.");
            }
        }
        else
        {
            Debug.LogError("ControllerCollider GameObject is not assigned.");
        }

        // Intialise the controllerWidth
        controllerWidth = controllerManager.GetControllerAnchorWidth();
    }

    /// <summary>
    /// Setup the first pair of targets in list and first target of pair
    /// </summary>
    private void FirstTargetSetup()
    {
        // Start with the first pair of targets in list
        currentPair = currentPairList[currentPairIndex];
        // Start with the first target of the pair
        currentTarget = currentPair.target1;

        // Assign targetIDs based on configurations
        AssignTargetIDs();

        // Update and log current target corners
        BoxCollider targetCollider = currentTarget.GetComponent<BoxCollider>();
        if (targetCollider != null)
        {
            targetIndex = GetCurrentTargetIndex();
            logsManager.LogTargetBoxColliderCorners(targetIndex, targetCollider);
        }
        else
        {
            Debug.LogWarning($"Current target {currentTarget.name} does not have a BoxCollider.");
        }

        // Start the trial for the first target by resetting dwell time
        ResetTotalDwellTime();

        // Reset reaction time
        NewTargetTime();

        // Inform VisualsManager about the current pair for visual feedback
        if (visualsManager != null && visualsManager.AreVisualTrialsActive()) visualsManager.SetCurrentPair(currentPair, currentTarget);
    }

    /// <summary>
    /// Assign targetID to each TargetPair based on TargetWidthType and DistanceType.
    /// </summary>
    private void AssignTargetIDs()
    {
        foreach (var pair in trainingPairs)
        {
            pair.targetID = CalculateTargetID(pair.targetWidth, pair.distance);
        }

        foreach (var pair in testingPairs)
        {
            pair.targetID = CalculateTargetID(pair.targetWidth, pair.distance);
        }
    }

    /// <summary>
    /// Calculates the targetID based on TargetWidthType and DistanceType.
    /// </summary>
    /// <param name="widthType">Width category of the target</param>
    /// <param name="distanceType">Distance category of the target</param>
    /// <returns>Integer targetID (0-6)</returns>
    private int CalculateTargetID(TargetWidthType widthType, DistanceType distanceType)
    {
        if (isTrainingPhase)
        {
            if (widthType == TargetWidthType.Training && distanceType == DistanceType.Training)
            {
                return 0;
            }
        }
        // Testing phase
        if (widthType == TargetWidthType.Small)
        {
            switch (distanceType)
            {
                case DistanceType.Short:
                    return 1;
                case DistanceType.Medium:
                    return 2;
                case DistanceType.Long:
                    return 3;
                default:
                    Debug.LogWarning("Unknown DistanceType for Small width. Assigning targetID = -1.");
                    return -1;
            }
        }
        else if (widthType == TargetWidthType.Large)
        {
            switch (distanceType)
            {
                case DistanceType.Short:
                    return 4;
                case DistanceType.Medium:
                    return 5;
                case DistanceType.Long:
                    return 6;
                default:
                    Debug.LogWarning("Unknown DistanceType for Large width. Assigning targetID = -1.");
                    return -1;
            }
        }
        else
        {
            Debug.LogWarning("Unknown TargetWidthType. Assigning targetID = -1.");
            return -1;
        }
    }

    #endregion

    #region Update Loop Methods

    /// <summary>
    /// Controller contact with target, detect over/undershoot, update position, trigger press behaviour during target selection phase
    /// </summary>
    private void TargetSelectionPhaseLoop()
    {
        // bool to check if the controller is in contact with the current target's collider
        isContactingTarget = CheckIfHitTarget(controllerCollider, targetCollider);

        // Handle controller contact with target
        HandleControllerContactWithTarget();

        // Check if the trigger is currently being pressed
        bool isTriggerPressed = OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controllerManager.controller);

        // Detects undershoot and overshoot only after 2nd trigger selection - first target not logged
        if (!isFirstSelectionInTrial)
        {
            // Detect overshoot and undershoot
            overshootUndershootManager.DetectOvershootUndershoot(
                isTriggerPressed,
                direction,
                currentControllerPosition,
                previousControllerPosition,
                currentTarget.transform.position,
                isContactingTarget,
                targetCollider
            );
        }

        // Update previous controller position on each loop
        previousControllerPosition = currentControllerPosition;

        // Handle trigger press event
        HandleTriggerPress();
    }

    #endregion

    #region Controller Contact Handling

    /// <summary>
    /// Handles dwell time and haptic feedback when the controller contacts the active target.
    /// Provides continuous haptics during contact.
    /// </summary>
    private void HandleControllerContactWithTarget()
    {
        // If the controller is currently in contact with the target
        if (isContactingTarget)
        {
            // First frame of contact: start haptics and record start time for dwell.
            if (!wasTargetContacted)
            {
                // Set the flag to indicate contact has been made
                wasTargetContacted = true;

                // Start continuous haptic feedback on the controller
                controllerManager.StartContactHaptics();

                // Only start recording dwell time after the first trigger press of a set
                if (!isFirstSelectionInTrial)
                {
                    // Record the time when contact starts
                    dwellStartTime = Time.time;
                }

                // Set hasContactedTargetAtLeastOnce flag to true to track undershooting prior to trigger press
                overshootUndershootManager.TargetContact();
            }

            // Continuously accumulate dwell time whilst in contact with target if not the first selection
            if (!isFirstSelectionInTrial)
            {
                totalDwellTime += Time.deltaTime;
            }
        }
        // If the controller is NOT in contact with the target
        else
        {
            // First frame after contact has been broken: stop haptics.
            if (wasTargetContacted)
            {
                // Reset the contact flag
                wasTargetContacted = false;

                // Stop the haptic feedback on the controller
                controllerManager.StopHapticFeedback();

                // Reset the dwell time start time
                dwellStartTime = 0f;
            }
        }
    }

    /// <summary>
    /// Check if controller anchor colliding with target collider.
    /// </summary>
    /// <param name="controllerCollider">Collider of the controller</param>
    /// <param name="targetCollider">Collider of the target</param>
    /// <returns>True if controller anchor contacting target collider, otherwise false</returns>
    private bool CheckIfHitTarget(Collider controllerCollider, Collider targetCollider)
    {
        if (currentTarget == null)
        {
            // Only log an error if testing is not over
            if (!sessionManager.GetOverState)
            {
                Debug.LogError("Current target object is not assinged.");
            }
            return false;
        }

        // To detect overlapping between sphere and box colliders
        Vector3 direction;
        float distance;

        // Physics.ComputePenetration to check for any collision/overlapping between controller anchor sphere collider and target box collider
        // Direction: The direction in which to move controllerCollider to resolve the overlap. Distance: The distance controllerCollider needs to move to resolve the overlap. Both used to determine any overlap, i.e., collision
        bool isOverlapping = Physics.ComputePenetration(
            controllerCollider,
            controllerCollider.transform.position,
            controllerCollider.transform.rotation,
            targetCollider,
            targetCollider.transform.position,
            targetCollider.transform.rotation,
            out direction,
            out distance);

        // Returns true if the colliders are overlapping
        return isOverlapping;
    }

    /// <summary>
    /// Check if controller anchor is colliding with the hitting midpoint collider
    /// </summary>
    /// <param name="controllerCollider"></param>
    /// <returns>True if controller anchor contacting midpoint haptic object.</returns>
    private bool CheckIfHitMidpoint(Collider controllerCollider)
    {
        if (midpointHaptic == null)
        {
            Debug.LogError("Midpoint Haptic object is not assigned.");
        }

        Collider midPointCollider = midpointHaptic.GetComponent<Collider>();

        // To detect overlapping between sphere and box colliders
        Vector3 direction;
        float distance;

        // Physics.ComputePenetration to check for collision between controller anchor and midpoint collider
        bool isOverlapping = Physics.ComputePenetration(controllerCollider,
            controllerCollider.transform.position,
            controllerCollider.transform.rotation,
            midPointCollider,
            midPointCollider.transform.position,
            midPointCollider.transform.rotation,
            out direction,
            out distance);

        // Return true if colliders are overlapping
        return isOverlapping;
    }

    /// <summary>
    /// Set targetcollider with error check to exit loop
    /// </summary>
    private void SetTargetCollider()
    {
        // Set collider of current target
        targetCollider = currentTarget.GetComponent<Collider>();

        // Exit loop if current target has no collider
        if (targetCollider == null)
        {
            Debug.LogError($"Collider component missing on target '{currentTarget.name}'.");
            return;
        }
    }

    #endregion

    #region Controller Movement Tracking

    /// <summary>
    /// Get controller position and rotation, and add controller positon to list if tracking enabled
    /// </summary>
    private void TrackControllerMovement()
    {
        // Get controller position each loop
        currentControllerPosition = UpdateControllerPosition();

        // Determine the direction ID each loop
        direction = GetCurrentTargetDirection();

        // If tracking enabled, i.e., after first trigger press
        if (isTrackingMovement)
        {
            // Collect movement positions when tracking is active and add to position list for logging
            controllerMovementPositions.Add(currentControllerPosition);
        }
    }

    /// <summary>
    /// Start tracking of controller movement for total distance moved.
    /// </summary>
    private void StartMovementTracking()
    {
        // Clear controller movement positions
        controllerMovementPositions.Clear();

        // Start tracking controller
        controllerManager.StartTracking();

        // Set controller tracking flag to true
        isTrackingMovement = true;

        // Controller position at start of tracking
        Vector3 startPosition = controllerManager.GetControllerPosition();

        // Add start position to movement positions list
        controllerMovementPositions.Add(startPosition);

        // Record start state of controller (position and rotation)
        RecordStartState();
    }

    /// <summary>
    /// Stop tracking controller movement to determine distance moved since tracking started, i.e., total distance moved between target selections.
    /// </summary>
    private void StopMovementTracking()
    {
        isTrackingMovement = false;

        // Controller position at end of tracking
        Vector3 endPosition = controllerManager.GetControllerPosition();

        // Add end point position to calulate in relation to startPosition
        controllerMovementPositions.Add(endPosition);

        // Get movementDirection
        movementAxis = GetMovementAxis();

        // Calculate total distance along the movement axis
        currentAxisDistance = CalculateAxisAlignedDistance();

        // Calcuate movement distance along all axes
        totalPathLength = CalculateTotalPathLength();

        controllerManager.StopTracking();
    }

    /// <summary>
    /// Controller Position Update from controller manager
    /// </summary>
    /// <returns>Vector3 controller position</returns>
    private Vector3 UpdateControllerPosition() => controllerManager.GetControllerPosition();

    /// <summary>
    /// Set controller rotation with controllermanager
    /// </summary>
    /// <returns>Quaternion rotation of controller</returns>
    private Quaternion UpdateControllerRotation() => controllerManager.GetControllerRotation();

    /// <summary>
    /// Returns controller width
    /// </summary>
    /// <returns>Float for width of controller</returns>
    public float GetControllerWidth() => controllerWidth;

    #endregion

    #region Target Switching

    /// <summary>
    /// Handle switching to the next targets.
    /// </summary>
    private void SwitchTargets()
    {
        // Determine the trial count based on the phase
        trialCount = isTrainingPhase ? trainingTrials : testingTrials;

        // Check if the set is over
        if (loggedTrialCount >= trialCount)
        {
            // Reset colour for the current target and move to the next set if visuals are enabled
            if (visualsManager.AreVisualsEnabled()) visualsManager.RestoreColour(currentTarget);

            // Reset counters and flag
            ResetCounters();

            // Stop movement tracking
            StopMovementTracking();

            // Reset last click time
            timeManagement.ResetLastClickTime();

            // Pause vest haptics between sets
            sessionManager.PauseHaptics();

            // On new set, turn off all inputs except grip to allow user to start when ready
            GripOnly();

            // Increment setNumber when moving to a new set
            setNumber++;

            // Increment individual phase set numebrs
            if (isTrainingPhase)
            {
                trainingSetNumber++;
            }
            else
            {
                testSetNumber++;
            }

            // Move to the next set
            currentPairIndex++;

            // Switching behaviour when last target hit
            if (currentPairIndex >= currentPairList.Count)
            {
                // End of training phase, switch to testing, or end of all testing once all sets have been ran the required number of times
                if (isTrainingPhase)
                {
                    SwitchToTestingPhase();
                }
                else
                {
                    if (sessionManager != null)
                    {
                        // After logging block fittslaw data, reset the block lists
                        fittsLawManager.ResetBlockLists();

                        // End testing behaviour
                        sessionManager.EndTesting();
                    }
                    else
                    {
                        Debug.LogError("SessionManager is not assigned.");
                    }
                }
            }
            else
            {
                // Switch to next set
                NewSetSetup();
            }
        }
        // Target switching within phase
        else
        {
            // Switch to the other target in the pair
            if (currentTarget == currentPair.target1)
            {
                SwitchToNewTargetInPair(currentPair.target2);
            }
            else
            {
                SwitchToNewTargetInPair(currentPair.target1);
            }
        }

        // Handle screen blackout trial behaoiur
        visualsManager.HandleBlackoutTrials();

        // Re-enable switching, i.e., trigger input, after delay
        Invoke(nameof(EnableSwitching), inputDelay);
    }

    /// <summary>
    /// Switch to testing phase.
    /// </summary>
    private void SwitchToTestingPhase()
    {
        // End testing rather than switching to testing phase if skipTesting phase flag true
        if (skipTesting)
        {
            // End testing immediately
            if (sessionManager != null)
            {
                // After logging block fittsLaw data, reset the block lists
                fittsLawManager.ResetBlockLists();

                // End testing behaviour
                sessionManager.EndTesting();
            }
            else
            {
                Debug.LogError("SessionManager is not assigned.");
            }
            return;
        }

        isTrainingPhase = false;
        currentPairIndex = 0;

        // Create a new list from testingPairs (set in inspector by user) to avoid modifying the serialised list set by the user in the instpector
        currentPairList = new List<TargetPair>(testingPairs);

        // Shuffle the testingPairs list when switching from training to testing - random order of IDs
        ShuffleList(currentPairList);

        // Reset counters and flag
        ResetCounters();

        // Reset total dwell time
        ResetTotalDwellTime();

        // Reset set numbers, no more training so 0, first testing so 1
        trainingSetNumber = 0;   
        testSetNumber = 1;       

        // Set midpoint haptics renderer off
        visualsManager.MidpointHapticRenderer(false);

        // Clear colours list and cache new colours
        visualsManager.RestoreOriginalColours(testingPairs);

        // Disable visuals if targetsVisualsToggle off and visuals not already disabled 
        if (!visualsManager.AreVisualsEnabled() && !visualsManager.AreVisualsDisabled()) visualsManager.DisableVisuals(testingPairs);

        // New set setup
        if (currentPairList.Count > 0)
        {
            NewSetSetup();
        }
        else
        {
            // Testing over handling
            Debug.LogError("No testing pairs assigned.");
            sessionManager.SetTestingOver();
        }
    }

    /// <summary>
    /// Setup for when starting a new set after the trial count is met.
    /// </summary>
    private void NewSetSetup()
    {
        // Reset trialNumberInSet to 0
        trialNumberInSet = 0;

        // After all effective calculations, reset the lists for the next block
        fittsLawManager.ResetSetLists();

        // Reset reaction time
        NewTargetTime();

        // Start with the first pair of targets in list and first target of pair
        FirstTargetSetup();

        // Calculate and set the movement axis for the current pair
        currentMovementAxis = CalculateMovementAxis();

        // Change target colour to green if visuals on
        if (visualsManager.AreVisualsEnabled()) visualsManager.ChangeTargetColour(currentTarget, Color.green);

        // Inform VisualsManager about the current pair for visual feedback
        if (visualsManager != null && visualsManager.AreVisualTrialsActive()) visualsManager.SetCurrentPair(currentPair, currentTarget);

        // Notify HapticsManager of set change
        hapticsManager.UpdateTargetInfo(currentPair.growthPattern);

        // Reset click time
        timeManagement.ResetLastClickTime();

        // On new set, turn off all inputs except grip
        GripOnly();

        // Initialise previousControllerPosition to current controller position
        previousControllerPosition = UpdateControllerPosition();

        // Reset isLastSelectionInPair
        isLastSelectionInPair = false;

        // Reset aggregate distances for new set
        ResetAggregatesForSet();

        // Reset the first trigger press flag in controller manager
        controllerManager.ResetFirstTriggerPress();

        // Reset totalDwellTime at the start of a new set
        ResetTotalDwellTime();

        // Check for block change
        CheckForSetChange();
    }

    /// <summary>
    /// Switch to a new target in the pair.
    /// </summary>
    /// <param name="newTarget">GameObject for new target</param>
    private void SwitchToNewTargetInPair(GameObject newTarget)
    {
        // Store the current target as the previous target before switching
        previousTarget = currentTarget;

        // Update previousTargetPosition only if previousTarget is not null
        if (previousTarget != null) previousTargetPosition = previousTarget.transform.position;

        // Update current target to new target
        currentTarget = newTarget;

        // Call LogTargetBoxColliderCorners to update the corners for the new target
        BoxCollider targetCollider = currentTarget.GetComponent<BoxCollider>();
        if (targetCollider != null)
        {
            targetIndex = GetCurrentTargetIndex();
            logsManager.LogTargetBoxColliderCorners(targetIndex, targetCollider);
        }
        else
        {
            Debug.LogWarning($"Current target {currentTarget.name} does not have a BoxCollider.");
        }

        if (visualsManager.AreVisualsEnabled())
        {
            // Reset color of previous target if visuals are on
            visualsManager.RestoreColour(previousTarget);
            // Change color of current target to green
            visualsManager.ChangeTargetColour(currentTarget, Color.green);
        }

        // Calculate new movement axis
        currentMovementAxis = CalculateMovementAxis();

        // Update movementDirection for logging
        movementAxis = GetMovementAxis();

        // Notify HapticsManager of target change
        hapticsManager.UpdateTargetInfo(currentPair.growthPattern);
        
        // Initialise previousControllerPosition to current controller position
        previousControllerPosition = UpdateControllerPosition();

        // Inform VisualsManager about the current pair for visual feedback
        if (visualsManager != null && visualsManager.AreVisualTrialsActive()) visualsManager.SetCurrentPair(currentPair, currentTarget);

        // Start a new trial for the new target by reseting dwell time
        ResetTotalDwellTime();

        // Reset reaction time
        NewTargetTime();
    }

    #endregion

    #region Trigger Press Handling

    /// <summary>
    /// Handle trigger press behaviour.
    /// </summary>
    private void HandleTriggerPress()
    {
        // Trigger pressed on selected controller & able to switch
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, controllerManager.controller) && canSwitch)
        {
            // Get the controller's rotation for logging
            controllerRotation = UpdateControllerRotation();

            // Handle the first trigger press behaviour
            controllerManager.HandleFirstTriggerPress();

            // Capture current controller position at trigger press
            currentPositionOnTriggerPress = currentControllerPosition;

            // Increment trial count
            trialCount++;

            // First trigger press
            if (isFirstSelectionInTrial)
            {
                // Mvement tracking starts only after first trigger press - first trigger press behaviour
                HandleFirstTriggerSelection();
            }
            // Subsequent trigger presses
            else
            {
                HandleSubsequentTriggerSelection();
            }

            // Inform VisualsManager about the current pair for visual feedback if on
            if (visualsManager != null && visualsManager.AreVisualTrialsActive()) visualsManager.SetCurrentPair(currentPair, currentTarget);

            // Handle visuals (if needed)
            visualsManager.HandleVisualTrials();

            // Haptic feedback on controller trigger press (if set in inspector)
            controllerManager.TriggerHapticFeedback(
                controllerManager.ControllerFreq,
                controllerManager.ControllerAmp,
                controllerManager.ControllerDuration);

            // Prevent multiple input selections during the same trigger press
            canSwitch = false;

            // Re-enable switching after inputDelay
            Invoke("EnableSwitching", inputDelay);

            // Switch to the next target (if not null)
            if (currentTarget) SwitchTargets();
        }
    }

    /// <summary>
    /// First trigger press behaviour - resetting, logging, and starting tracking for next target
    /// </summary>
    private void HandleFirstTriggerSelection()
    {
        // Reset dwell time
        ResetTotalDwellTime();

        // Disable first selection flag after first selection is made but don't calculate distances yet
        isFirstSelectionInTrial = false;

        // Initialise dwellStartTime and wasTargetContacted for the second target
        if (isContactingTarget)
        {
            dwellStartTime = Time.time;
            wasTargetContacted = true;
        }

        // Start timing only after first selection
        timeManagement.UpdateLastClickTime(Time.time);

        // Store current trigger position as previous for next calculation
        previousPositionOnTriggerPosition = currentPositionOnTriggerPress;

        // Start movement tracking for next movement
        StartMovementTracking();

        // Add start position for Fitts Law after first trigger press
        fittsLawManager.AddStartPosition(currentPositionOnTriggerPress);

        // Add end position to the combined list for Fitts Law
        // Ensures that the first trial's end position is recorded, even though it will not be included in effective width calculations
        Vector3 targetCentrePosition = GetTargetCentrePosition(currentTarget, (int)direction);
        fittsLawManager.AddEndPosition(currentPositionOnTriggerPress, targetCentrePosition);
    }

    /// <summary>
    /// Behaviour on trigger presses for all targets following first - calulations, logging, tracking behaviours etc.
    /// </summary>
    private void HandleSubsequentTriggerSelection()
    {
        // Assign previous target and pair before data collection
        previousTarget = currentTarget;
        previousPair = currentPair;

        // Stop movement tracking and calculate movement distances for current trial
        StopMovementTracking();

        // Collect all data for the trial for logging
        CollectTrialData();

        // Get midpoint
        targetMidpoint = GetTargetCentrePosition(currentTarget, (int)direction);

        // Add end position for Fitts Law
        fittsLawManager.AddEndPosition(currentPositionOnTriggerPress, targetMidpoint);

        // Update last click time
        timeManagement.UpdateLastClickTime(Time.time);

        // Reset dwell time
        ResetTotalDwellTime();

        // Reset total distances after logging
        overshootUndershootManager.ResetTotals();

        // Reset overshot and undershot counts and distances
        overshootUndershootManager.ResetOvershootUndershoot();

        // Store current trigger position as previous for next calculation
        previousPositionOnTriggerPosition = currentPositionOnTriggerPress;

        // Start movement tracking for the next trial
        StartMovementTracking();

        // Add start position for next movement
        fittsLawManager.AddStartPosition(currentPositionOnTriggerPress);
    }

    #endregion

    #region Data Collection and Logging

    /// <summary>
    /// Collect all data needed for logging and store them in class-level variables prefixed with 'previous'.
    /// </summary>
    private void CollectTrialData()
    {
        // Set the variables in logsManager used by movement files
        if (logsManager != null)
        {
            logsManager.MovementAxis = GetMovementAxis();
            logsManager.BlockLetter = blockLetter;
            logsManager.SetNumber = setNumber;
            logsManager.TrainingSetNumber = trainingSetNumber;
            logsManager.TestSetNumber = testSetNumber;
            logsManager.TrialNumberInSet = trialNumberInSet;
            logsManager.TrialNumberInBlock = trialNumberInBlock;
            logsManager.TrialNumberByID = trialNumberByIDDict[previousPair.targetID];
            logsManager.HapticFeedbackMethod = (int)GetGrowthPattern(previousPair);
        }

        // Null check and don't log if previous target null
        if (previousTarget == null || previousPair == null)
        {
            Debug.LogWarning("Previous target or pair is null. Skipping data collection for this trial.");
            return;
        }

        // Collect data about phase, distance, and direction and store in class-level variables with 'previous' prefix - just selected target data to be logged
        previousPhase = isTrainingPhase ? PhaseType.Training : PhaseType.Testing;
        previousDistanceID = previousPair.distance;
        previousDirectionID = GetTargetDirection(previousTarget, previousPair);

        // Calculate time taken between previous trials, i.e., from target before until just selected target
        previousTimeTaken = timeManagement.GetTimeTaken();

        // Get whether the controller is within the previous target's collider
        Collider previousTargetCollider = previousTarget.GetComponent<Collider>();
        // To log if collision, i.e., target hit on previous trigger press
        previousIsHit = CheckIfHitTarget(controllerCollider, previousTargetCollider);

        // Obtain the closest points on the previous target - midpoint and collider
        previousTargetMidpoint = GetTargetCentrePosition(previousTarget, movementAxis);
        previousDistanceToTargetCollider = overshootUndershootManager.GetDistanceToTargetCollider(
            previousDirectionID,
            currentControllerPosition,
            previousTargetMidpoint,
            previousTargetCollider);

        // Obtain the distance to the target midpoint
        previousDistanceToTargetMidpoint = overshootUndershootManager.GetDistanceToTargetMidpoint(
            previousDirectionID,
            currentControllerPosition,
            previousTargetMidpoint);

        // Capture previous overshoot and undershoot final distances for logging
        previousOvershootDistanceForLogging = overshootUndershootManager.IsCurrentlyOvershooting ? overshootUndershootManager.CurrentOvershootDistance : 0f;
        previousUndershootDistanceForLogging = overshootUndershootManager.IsCurrentlyUndershooting ? overshootUndershootManager.CurrentUndershootDistance : 0f;

        // Previous ballistic and correction distances based on logged controller data between previous two target selections
        previousBallisticDistanceAlongAxis = logsManager.GetBallisticDistanceAlongAxis(currentMovementAxis);
        previousBallisticDistanceAlongAllAxes = logsManager.GetBallisticDistanceAlongAllAxes();
        previousCorrectionDistanceAlongAxis = logsManager.GetCorrectionDistanceAlongAxis(currentMovementAxis);
        previousCorrectionDistanceAlongAllAxes = logsManager.GetCorrectionDistanceAlongAllAxes();

        // Increment logged trial count
        loggedTrialCount++;

        // Get straight-line distance between previous targets
        amplitude = GetDistanceBetweenTargets();

        // Calculate difference between axis movement and amplitude
        differenceToAmplitude = currentAxisDistance - amplitude;

        // Calculate EuclideanDeviation - movements not along current axis
        euclideanDeviation = CalculateEuclideanDeviation(amplitude, totalPathLength);

        // Calculate difference in position of controller from last +1 to most recent trigger press
        Vector3 controllerMovementVector = currentPositionOnTriggerPress - previousPositionOnTriggerPosition;

        // Calculate absolute distance from previous position to current across movement axis - straight-line path taken between start and end (not accounting for overshots)
        currentAxisNetDistance = Mathf.Abs(Vector3.Dot(controllerMovementVector, currentMovementAxis.normalized));

        // Calculate euclidean straight-line movement distance between trigger presses across all dimensions
        euclideanDistance = CalculateEuclideanDistance(previousPositionOnTriggerPosition, currentPositionOnTriggerPress);

        // Accumulate distances into aggregates
        aggregateMovementDistanceAlongAxisForSet += currentAxisDistance;
        aggregateNetMovementDistanceAlongAxisForSet += currentAxisNetDistance;
        aggregateMovementDistanceAlongAllAxesForSet += totalPathLength;
        aggregateMovementDistanceAlongAxisForBlock += currentAxisDistance;
        aggregateNetMovementDistanceAlongAxisForBlock += currentAxisNetDistance;
        aggregateMovementDistanceAlongAllAxesForBlock += totalPathLength;
        euclideanDistanceForSet += euclideanDistance;
        euclideanDistanceForBlock += euclideanDistance;
        euclideanDeviationForSet += euclideanDeviation;
        euclideanDeviationForBlock += euclideanDeviation;

        // Get trial limit to determine control sequence for next target
        int trialLimit = isTrainingPhase ? trainingTrials : testingTrials;

        // Check if this trial is the last in the set
        isLastSelectionInPair = (loggedTrialCount == trialLimit);

        // Check if this trial is the last in the block
        isLastSelectionInBlock = (trialNumberInBlock == trialsInBlock - 1);

        // Before resetting overshoot/undershoot, accumulate pending peaks
        overshootUndershootManager.AccumulatePendingPeakDistances();

        // Increment trial numbers before logging
        IncrementTrialNumbers();

        // Log data using the collected variables
        LogCompleteData();

        // Reset movement data after logging
        logsManager.ResetMovementData();
    }

    /// <summary>
    /// Logs the collected data to CSV files and resets variables for the next trial
    /// </summary>
    private void LogCompleteData()
    {
        // FittsLaw input variable setting
        // Convert seconds to milliseconds
        float movementTimeMs = previousTimeTaken * 1000f;
        // Get width of previous target along movement axis
        float targetWidth = GetTargetWidth(previousTarget, previousDirectionID);
        // Get difference in axis movement
        float axisDifference = overshootUndershootManager.GetAxisDifference;
        // Get movement axis
        int currentMoveAxis = GetMovementAxis();
        // Get centre position of target along movement axis
        Vector3 targetCentrePosition = GetTargetCentrePosition(previousTarget, movementAxis);

        // Calculate all fitts law data with movement and time etc. inputs from just completed trial
        FittsLawData fittsData = fittsLawManager.CalculateFittsLawData(
            movementTimeMs,
            targetWidth,
            amplitude,
            previousIsHit,
            previousPositionOnTriggerPosition,
            currentPositionOnTriggerPress,
            currentMovementAxis,
            aggregateMovementDistanceAlongAxisForSet,
            aggregateMovementDistanceAlongAllAxesForSet,
            currentMoveAxis,
            isLastSelectionInPair,
            isLastSelectionInBlock,
            axisDifference,
            targetCentrePosition,
            controllerWidth,
            currentAxisDistance,
            totalPathLength,
            euclideanDeviation
        );

        // Convert previousIsHit to int - 1 for hit, 0 for miss
        int hitMissValue = previousIsHit ? 1 : 0;

        // Obtain all overshoot and undershoot data for logging
        OverUnderShootData overshootUndershootData = overshootUndershootManager.GetOvershootUndershootData();

        // Calculate movement angle between trigger positions
        float movementAngle = CalculateMovementAngle(previousPositionOnTriggerPosition, currentPositionOnTriggerPress, targetCentrePosition);

        // Calculate total correction distance following first contact with target, i.e., combined overshoots and undershoots following first target contact until trigger press
        float correctionDistance = overshootUndershootData.CorrectionTotalDistance;

        // Calculate current block letter for logging
        blockLetter = CalculateBlockLetter();

        // Calculate acerage, maxSpeed, and minSpeed based on all controller positions
        float averageSpeed = logsManager.CalculateAverageMovementSpeed();
        float maxSpeed = logsManager.GetMaxSpeed();
        float minSpeed = logsManager.GetMinSpeed();

        // Calculate ballistic and correction times based on controller positions
        var (ballisticTime, correctionTime) = logsManager.CalculateBallisticAndCorrectionTimes();

        // Set growth pattern int for logging
        int previousGrowthPatternInt = (int)GetGrowthPattern(previousPair);
        
        // Determine haptic strength at trigger press
        int hapticStrength = hapticsManager.GetHapticStrength();

        // Set trial numbers
        int targetID = previousPair.targetID;
        int trialNumberByID = trialNumberByIDDict[targetID];

        // Set direction between targets as int for logging
        int directionInt = (int)previousDirectionID;

        // Calculate controller position and rotation differences
        float ControllerPositionXDifference = currentControllerPosition.x - startControllerPosition.x;
        float ControllerPositionYDifference = currentControllerPosition.y - startControllerPosition.y;
        float ControllerPositionZDifference = currentControllerPosition.z - startControllerPosition.z;
        Vector3 startEuler = startControllerRotation.eulerAngles;
        Vector3 endEuler = controllerRotation.eulerAngles;
        float ControllerRotationXDifference = Mathf.DeltaAngle(startEuler.x, endEuler.x);
        float ControllerRotationYDifference = Mathf.DeltaAngle(startEuler.y, endEuler.y);
        float ControllerRotationZDifference = Mathf.DeltaAngle(startEuler.z, endEuler.z);

        // Calculate PathCurvature based on amplitude and actual total path taken
        pathCurvature = amplitude > 0 ? totalPathLength / amplitude : 0f;

        // Log all data for previous target selection
        LogData data = new LogData
        {
            Timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss"),
            ParticipantNumber = logsManager.ParticipantNumber,
            BlockNumber = logsManager.BlockNumber,
            BlockLetter = blockLetter,
            SetNumber = setNumber,
            // Set to 0 if not training
            TrainingSetNumber = isTrainingPhase ? trainingSetNumber : 0,
            // Set to 0 if testing
            TestSetNumber = isTrainingPhase ? 0 : testSetNumber,
            TrialNumberInSet = trialNumberInSet,
            TrialNumberInBlock = trialNumberInBlock,
            TrialNumberByID = trialNumberByID,
            HapticFeedbackMethod = previousGrowthPatternInt,
            HapticFeedbackStrength = hapticStrength,
            ControllerWidth = controllerWidth,
            DistanceToTargetCollider = previousDistanceToTargetCollider,
            TargetMidpoint = previousTargetMidpoint,
            DistanceToTargetMidpoint = previousDistanceToTargetMidpoint,
            TimeTaken = previousTimeTaken,
            IsHit = hitMissValue,
            TotalDwellTime = totalDwellTime,
            UndershotCount = overshootUndershootData.UndershotCount,
            OvershotCount = overshootUndershootData.OvershotCount,
            ReEntryNumber = overshootUndershootData.ReEntryNumber,
            UndershootDistance = previousUndershootDistanceForLogging,
            OvershootDistance = previousOvershootDistanceForLogging,
            TotalUndershootDistance = overshootUndershootData.TotalUndershootDistance,
            TotalOvershootDistance = overshootUndershootData.TotalOvershootDistance,
            Phase = (int)previousPhase,
            DistanceID = (int)previousDistanceID,
            DirectionID = directionInt,
            StartControllerPosition = startControllerPosition,
            StartControllerRotation = startControllerRotation,
            ControllerPosition = currentControllerPosition,
            ControllerRotation = controllerRotation,
            FittsData = fittsData,
            CurrentAxisNetDistance = currentAxisNetDistance,
            AggregateMovementDistanceAlongAxisForSet = aggregateMovementDistanceAlongAxisForSet,
            AggregateMovementDistanceAlongAllAxesForSet = aggregateMovementDistanceAlongAllAxesForSet,
            AggregateNetMovementDistanceAlongAxisForSet = aggregateNetMovementDistanceAlongAxisForSet,
            AggregateMovementDistanceAlongAxisForBlock = aggregateMovementDistanceAlongAxisForBlock,
            AggregateMovementDistanceAlongAllAxesForBlock = aggregateMovementDistanceAlongAllAxesForBlock,
            AggregateNetMovementDistanceAlongAxisForBlock = aggregateNetMovementDistanceAlongAxisForBlock,
            EuclideanDistance = euclideanDistance,
            EuclideanDeviation = euclideanDeviation,
            AverageMovementSpeed = averageSpeed,
            CorrectionDistance = correctionDistance,
            MaxSpeed = maxSpeed,
            MinSpeed = minSpeed,
            BallisticTime = ballisticTime,
            CorrectionTime = correctionTime,
            BallisticDistanceAlongAxis = previousBallisticDistanceAlongAxis,
            BallisticDistanceAlongAllAxes = previousBallisticDistanceAlongAllAxes,
            CorrectionDistanceAlongAxis = previousCorrectionDistanceAlongAxis,
            CorrectionDistanceAlongAllAxes = previousCorrectionDistanceAlongAllAxes,
            ControllerPositionXDifference = ControllerPositionXDifference,
            ControllerPositionYDifference = ControllerPositionYDifference,
            ControllerPositionZDifference = ControllerPositionZDifference,
            ControllerRotationXDifference = ControllerRotationXDifference,
            ControllerRotationYDifference = ControllerRotationYDifference,
            ControllerRotationZDifference = ControllerRotationZDifference,
            ReactionTime = reactionTime,
            PathCurvature = pathCurvature,
            MovementAngle = movementAngle,
            EuclideanDistanceForSet = euclideanDistanceForSet,
            EuclideanDistanceForBlock = euclideanDistanceForBlock,
            EuclideanDeviationForSet = euclideanDeviationForSet,
            EuclideanDeviationForBlock = euclideanDeviationForBlock,
            CurrentAxisDistance = currentAxisDistance,
            DifferenceToAmplitude = differenceToAmplitude
        };

        // Write data
        logsManager.CSVDataWriter(data);

        // Reset selection flags
        if (isLastSelectionInPair)
        {
            isLastSelectionInPair = false;
        }

        if (isLastSelectionInBlock)
        {
            isLastSelectionInBlock = false;
        }
    }

    #endregion

    #region Target Information

    /// <summary>
    /// Get the target width based on the DirectionType - 1D width
    /// Returns the x scale for left/right movements and z scale for forward/backward movements.
    /// </summary>
    /// <param name="target">Current target gameobject</param>
    /// <param name="direction">Current direction of target</param>
    /// <returns>Float width of target along movement direction</returns>
    public float GetTargetWidth(GameObject target, DirectionType direction)
    {
        if (target == null)
        {
            Debug.LogError("GetTargetWidth: target is null.");
            return 0f;
        }

        float targetWidth = 0f;

        // Determine the width based on the current target's direction - only the localscale for direction relevant for 1D Fitts Law
        switch (direction)
        {
            case DirectionType.Left:
            case DirectionType.Right:
                targetWidth = target.transform.localScale.x;
                break;
            case DirectionType.Forward:
            case DirectionType.Back:
                targetWidth = target.transform.localScale.z;
                break;
            // Defaults to x if DirectionType undefined
            default:
                Debug.LogWarning("GetTargetWidth: Undefined direction. Using x scale as default.");
                targetWidth = target.transform.localScale.x;
                break;
        }

        // Debug log to verify correct targetWidth assignment
        Debug.Log($"GetTargetWidth: Direction={direction}, TargetWidth={targetWidth}");

        return targetWidth;
    }

    /// <summary>
    /// Determine the direction based on the previous target and pair for logging
    /// </summary>
    /// <param name="target">Previous target</param>
    /// <param name="pair">Previous pair</param>
    /// <returns>Direction of previous selection</returns>
    private DirectionType GetTargetDirection(GameObject target, TargetPair pair)
    {
        // Null checks
        if (pair == null || target == null)
        {
            Debug.LogError("GetTargetDirection: pair or target is null.");
            return DirectionType.Off;
        }
        if (pair.target1 == null || pair.target2 == null)
        {
            Debug.LogError("GetTargetDirection: target1 or target2 in pair is null.");
            return DirectionType.Off;
        }

        // Return direction of previous target in pair
        return target == pair.target1 ? pair.direction1 : pair.direction2;
    }

    /// <summary>
    /// Return the full centre position of the target.
    /// Note: not currently relevant. For future or different target tasks - use this method if full 3D target centre needed.
    /// </summary>
    /// <param name="target">target Gameobject</param>
    /// <param name="movementAxis">movment axis</param>
    /// <returns>Vector3 centre position of target</returns>
    private Vector3 GetFullTargetCentrePosition(GameObject target, int movementAxis)
    {
        if (target == null)
        {
            Debug.LogError("GetTargetCentrePosition: target is null.");
            return Vector3.zero;
        }

        Collider collider = target.GetComponent<Collider>();

        if (collider != null)
        {
            // Return the full centre position
            return collider.bounds.center;
        }
        else
        {
            Debug.LogError("GetTargetCentrePosition: Collider component missing on target.");
            return target.transform.position;
        }
    }

    /// <summary>
    /// Get the centre of the target along the movement axis
    /// Use this for centre for 1D calculations
    /// </summary>
    /// <param name="target">target Gameobject</param>
    /// <param name="movementAxis">movment axis int</param>
    /// <returns>Vector3 position centre of target's collider along movement axis</returns>
    private Vector3 GetTargetCentrePosition(GameObject target, int movementAxis)
    {
        if (target == null)
        {
            Debug.LogError("GetTargetCentrePosition: target is null.");
            return Vector3.zero;
        }

        Collider collider = target.GetComponent<Collider>();

        if (collider != null)
        {
            // 3D centre for all axis
            Vector3 fullCentre = collider.bounds.center;
            // 1D centre intialisation
            Vector3 axisCentre = Vector3.zero;

            // Horizontal targets - only x axis taken from 3D centre
            if (movementAxis == 0)
            {
                axisCentre.x = fullCentre.x;
                axisCentre.y = 0f;
                axisCentre.z = 0f;
            }
            // Vertical - only z axis relevant
            else if (movementAxis == 1)
            {
                axisCentre.x = 0f;
                axisCentre.y = 0f;
                axisCentre.z = fullCentre.z;
            }
            // If movement axis not defined - full 3D centre returned
            else
            {
                Debug.LogWarning("GetTargetCentrePosition: Undefined movementAxis. Returning full centre.");
                return fullCentre;
            }

            return axisCentre;
        }
        // If target collider not found, return position of target gameobject instead
        else
        {
            Debug.LogError("GetTargetCentrePosition: Collider component missing on target.");
            return target.transform.position;
        }
    }

    /// <summary>
    /// Returns direction of current target, either target 1 or 2 in current pair
    /// </summary>
    /// <returns>DirectionType of current target - left, right, forward, or back.</returns>
    public DirectionType GetCurrentTargetDirection()
    {
        // Error handling with Off DirectionType
        if (currentPair == null)
        {
            Debug.LogError("GetCurrentTargetDirection: currentPair is null.");
            return DirectionType.Off;
        }

        if (currentTarget == null)
        {
            Debug.LogError("GetCurrentTargetDirection: currentTarget is null.");
            return DirectionType.Off;
        }

        if (currentPair.target1 == null || currentPair.target2 == null)
        {
            Debug.LogError("GetCurrentTargetDirection: target1 or target2 in currentPair is null.");
            return DirectionType.Off;
        }

        // Returns direction of current target in current pair
        return currentTarget == currentPair.target1 ? currentPair.direction1 : currentPair.direction2;
    }

    /// <summary>
    /// Returns the current target index
    /// </summary>
    /// <returns>Int - current target index in the current set.</returns>
    public int GetCurrentTargetIndex()
    {
        // Calculate target index based on pair index and current target within the pair
        int indexInPair = currentTarget == currentPair.target2 ? 1 : 0;
        return currentPairIndex * 2 + indexInPair;
    }

    /// <summary>
    /// Target width of current target
    /// </summary>
    /// <returns>Float width and direction of the current target</returns>
    public float GetCurrentTargetWidth()
    {
        return GetTargetWidth(currentTarget, direction);
    }

    /// <summary>
    /// Get centre position of current target
    /// </summary>
    /// <returns>Vector3 centre position and movement axis of current target</returns>
    public Vector3 GetCurrentTargetCentrePosition()
    {
        return GetTargetCentrePosition(currentTarget, movementAxis);
    }

    /// <summary>
    /// Returns distance of current targets
    /// </summary>
    /// <returns>DistanceType distance between current targets.</returns>
    public DistanceType GetCurrentTargetDistance() => currentPair.distance;

    /// <summary>
    /// Returns current target
    /// </summary>
    /// <returns>GameObject of the current target.</returns>
    public GameObject GetCurrentTarget() => currentTarget;

    /// <summary>
    /// Returns current target's collider
    /// </summary>
    /// <returns>Collider of the current target.</returns>
    public Collider GetCurrentTargetCollider() => targetCollider;

    /// <summary>
    /// Returns the position of the current target, forward, right, back, or left
    /// </summary>
    /// <returns>Vector3 position of the current target.</returns>
    public Vector3 GetCurrentTargetPosition() => currentTarget.transform.position;

    #endregion

    #region Movement and Distance Calculations

    /// <summary>
    /// Get movement axis.
    /// </summary>
    /// <returns>Int movement axis</returns>
    private int GetMovementAxis()
    {
        // Use currentMovementAxis to determine the axis
        if (Mathf.Abs(currentMovementAxis.x) > Mathf.Abs(currentMovementAxis.z))
        {
            // x-axis movement (Left/Right)
            return 0;
        }
        else
        {
            // z-axis movement (Forward/Back)
            return 1;
        }
    }

    /// <summary>
    /// Return movement axis from current target positions.
    /// </summary>
    /// <returns>Vector3 axis</returns>
    private Vector3 CalculateMovementAxis()
    {
        if (currentPair != null)
        {
            // Determine nomralised movement axis based on target positions
            Vector3 movementVector = currentPair.target2.transform.position - currentPair.target1.transform.position;
            return movementVector.normalized;
        }
        else
        {
            // Default to forward direction if currentPair is null
            return Vector3.forward;
        }
    }

    /// <summary>
    /// Calculates the total cumulative distance along the movement axis during the trial
    /// Includes overshoot and understhoot distances.
    /// </summary>
    /// <returns>Float controller movement distance along axis between targets.</returns>
    private float CalculateAxisAlignedDistance()
    {
        // Don't calculate for first position
        if (controllerMovementPositions.Count < 2) return 0f;

        // Initialise totalDistance to 0
        float totalDistance = 0f;

        // Determine normalised movement axis
        Vector3 movementAxis = currentMovementAxis.normalized;

        for (int i = 1; i < controllerMovementPositions.Count; i++)
        {
            // Calculate the difference (delta) between the current and previous positions
            Vector3 delta = controllerMovementPositions[i] - controllerMovementPositions[i - 1];
            // Project delta onto the movementAxis using the dot product
            float distanceAlongAxis = Vector3.Dot(delta, movementAxis);
            // Absolute value of the projected distance to account for movements in both directions, e.g., for summing up betwen target movement along with any overshooting and undershooting along axis
            totalDistance += Mathf.Abs(distanceAlongAxis);
        }
        return totalDistance;
    }

    /// <summary>
    /// Total distance the controller moved along all axes during the trial
    /// </summary>
    /// <returns>Float controller movement distance along moement axss between targets.</returns>
    private float CalculateTotalPathLength()
    {
        // Don't calculate for first position
        if (controllerMovementPositions.Count < 2) return 0f;

        float totalDistance = 0f;
        for (int i = 1; i < controllerMovementPositions.Count; i++)
        {
            // Distance between start and end points along all axes - euclidean distance between each controller position added
            totalDistance += CalculateEuclideanDistance(controllerMovementPositions[i], controllerMovementPositions[i - 1]);
        }
        return totalDistance;
    }

    /// <summary>
    /// Get the distance between targets
    /// AKA amplitude for Fitts Law calulations
    /// </summary>
    /// <returns>Float distance betwen targets.</returns>
    private float GetDistanceBetweenTargets()
    {
        if (previousPair == null)
        {
            Debug.LogError("GetDistanceBetweenTargets: previousPair is null.");
            return 0f;
        }
        if (previousPair.target1 == null || previousPair.target2 == null)
        {
            Debug.LogError("GetDistanceBetweenTargets: One or both targets in previousPair are null.");
            return 0f;
        }

        // Reutrn Euclidean straight-line distance
        return CalculateEuclideanDistance(previousPair.target1.transform.position, previousPair.target2.transform.position);
    }

    /// <summary>
    /// Calculates the straiight line distance between the start and end positions.
    /// </summary>
    /// <returns>float straight-line distance.</returns>
    private float CalculateEuclideanDistance(Vector3 startPosition, Vector3 endPosition) => Vector3.Distance(startPosition, endPosition);

    /// <summary>
    /// Calculates the Euclidean deviation as the difference between the total path length traversed and the straight-line distance between targets.
    /// Ensures that the deviation is always zero or positive - total over the straight-line distance,, i.e., any movements not along current axis
    /// </summary>
    /// <param name="distanceBetweenTargets">Straight-line distance between two targets.</param>
    /// <param name="totalPathLength">Total movement path length traversed by the user.</param>
    /// <returns>Positive float representing Euclidean Deviation.</returns>
    private float CalculateEuclideanDeviation(float distanceBetweenTargets, float totalPathLength)
    {
        return Mathf.Max(totalPathLength - distanceBetweenTargets, 0f);
    }

    /// <summary>
    /// Calculates the angle in degrees between the movement vector and the target vector
    /// targetVector represents the ideal movement direction from the startPosition to the target centre along the movement axis, with non-movement axes matching the controller initial position
    /// This helps determine the efficiency of the movement towards the target
    /// </summary>
    /// <param name="startPosition">The start position of the movement</param>
    /// <param name="endPosition">The end position of the movement</param>
    /// <param name="targetCentrePosition">The centre position of the target</param>
    /// <returns>float angle in degrees between the movement vector and target vector</returns>
    private float CalculateMovementAngle(Vector3 startPosition, Vector3 endPosition, Vector3 targetCentrePosition)
    {
        // targetCentrePosition is only the centre along movement axis, other 2 axes are 0, so:
        // Adjust targetCentrePosition to have the same non-movement axis components as startPosition - same axes components as controller at start
        if (movementAxis == 0)
        {
            // x-axis movement already set
            targetCentrePosition.y = startPosition.y;
            targetCentrePosition.z = startPosition.z;
        }
        else if (movementAxis == 1)
        {
            // z-axis movement already set
            targetCentrePosition.x = startPosition.x;
            targetCentrePosition.y = startPosition.y;
        }
        else
        {
            Debug.LogWarning("CalculateMovementAngle: Undefined movementAxis. Using default adjustments.");
            targetCentrePosition.y = startPosition.y;
            targetCentrePosition.z = startPosition.z;
        }

        // User's actual movement from the start to the end position
        Vector3 movementVector = endPosition - startPosition;
        // Ideal movement vector from the start position to the centre of the target
        Vector3 targetVector = targetCentrePosition - startPosition;

        // Normalise vectors to ensure accurate angle calculation
        movementVector.Normalize();
        targetVector.Normalize();

        // The angle in degrees between two vectors
        float angle = Vector3.Angle(movementVector, targetVector);
        return angle;
    }

    /// <summary>
    /// Determines the movement direction vector based on the current target's direction.
    /// </summary>
    /// <returns>Normalised Vector3 representing the target's direction - right, left, forward, or back - matching DirectionTypes.</returns>
    private Vector3 GetMovementDirectionVector()
    {
        // Switch based on current target DirectionType set in Update() loop
        switch (direction)
        {
            case DirectionType.Right:
                return Vector3.right;
            case DirectionType.Left:
                return Vector3.left;
            case DirectionType.Forward:
                return Vector3.forward;
            case DirectionType.Back:
                return Vector3.back;
            default:
                return Vector3.zero;
        }
    }

    /// <summary>
    /// Checks if the movement vector of the controller is in the direction of the target - for determining reaction time
    /// directionThreshold determines the degree to which movement counts as the same direction - currently at 60 degrees
    /// </summary>
    /// <param name="movement">Normalised movement vector.</param>
    /// <param name="targetDirection">Normalised target direction vector.</param>
    /// <returns>True if movement is in the target's direction, otherwise false.</returns>
    private bool IsMovementInTargetDirection(Vector3 movement, Vector3 targetDirection)
    {
        if (targetDirection == Vector3.zero) return false;

        // Determine if current movement is significantly in the target's direction using the Vector3 dot product
        float dotProduct = Vector3.Dot(movement, targetDirection);

        // Threshold can be adjusted based on sensitivity by altering directionThreshold value
        // Currently = 0.5 - movements that are within 60 degrees of the target direction considered as moving towards the target
        return dotProduct > directionThreshold;
    }

    #endregion

    #region Trial Management

    /// <summary>
    /// Calculate block letter based on predetermined block letter listing using 2 independent variables - direction (vertical or horizontal) and growth pattern (quadratic, pulse, linear, stair)
    /// </summary>
    /// <returns>string representing block letter</returns>
    private string CalculateBlockLetter()
    {
        // Determine the AxisType based on movementAxis - horizontal or vertical
        AxisType axisType = movementAxis == 0 ? AxisType.Horizontal : AxisType.Vertical;

        // Get the current GrowthPattern
        GrowthPattern growthPattern = GetGrowthPattern(currentPair);

        // Return letter from dictionary based on axis and haptic pattern
        if (BlockLetterMap.TryGetValue((axisType, growthPattern), out string blockLetter)) return blockLetter;

        // Error handling if axistype and growthpattern combo not in dictionary
        return string.Empty;
    }

    /// <summary>
    /// Increments trial numbers after each trial.
    /// </summary>
    private void IncrementTrialNumbers()
    {
        trialNumberInSet++;
        trialNumberInBlock++;

        // Retrieve the ID of the current target pair being interacted with - distance and width combo
        int targetID = currentPair.targetID;

        // If targetID exists in dictionary increment count for that ID
        if (trialNumberByIDDict.ContainsKey(targetID))
        {
            trialNumberByIDDict[targetID]++;
        }
        else
        {
            // New ID in block, set trial number for ID to 1
            trialNumberByIDDict[targetID] = 1;
        }
    }

    /// <summary>
    /// Returns the current trial count
    /// </summary>
    /// <returns>Int count of the current trial of the active targets.</returns>
    public int GetTrialCount() => trialCount;

    /// <summary>
    /// Returns the current trial number during both training and testing
    /// </summary>
    /// <returns>Int - current trial number of the active targets.</returns>
    public int GetTrialNumber() => isTrainingPhase ? trainingTrials : testingTrials;

    /// <summary>
    /// Checks if participant in training phase
    /// </summary>
    /// <returns>Bool true if in training phase.</returns>
    public bool IsTrainingPhase() => isTrainingPhase;

    #endregion

    #region Time Management

    /// <summary>
    /// Record the current time when a new target appears and set flag to false to record new reaction time
    /// </summary>
    private void NewTargetTime()
    {
        targetAppearanceTime = Time.time;
        reactionTimeRecorded = false;
    }

    /// <summary>
    /// Calculates time between target appearance, i.e., on trigger press, and movement in target axis direction
    /// How quickly do participants start moving within 60 degrees of the target direction - set in IsMovementInTargetDirection via directionThreshold var
    /// </summary>
    private void CalculateReactionTime()
    {
        // Determine the direction vector based on the current target's direction
        Vector3 targetDirection = GetMovementDirectionVector();

        // Get the current movement vector of the controller from controller positions list in logsmanager
        Vector3 movementVector = logsManager.CalculateCurrentMovementVector();

        // Check if movement is in the direction of the target (e.g., positive x for right)
        if (IsMovementInTargetDirection(movementVector, targetDirection))
        {
            // Record reaction time since target first appeared
            reactionTime = Time.time - targetAppearanceTime;
            // Set flag to true to record only once per target
            reactionTimeRecorded = true;
        }
    }

    #endregion

    #region State Management

    /// <summary>
    /// To reset the endpointProjections list when starting a new set - fitts law helper.
    /// </summary>
    private void CheckForSetChange()
    {
        // Compare current and previous distance and direction
        if (previousDistance != currentPair.distance || previousDirection != GetCurrentTargetDirection())
        {
            // Update previous distance and direction
            previousDistance = currentPair.distance;
            previousDirection = GetCurrentTargetDirection();
        }
    }

    /// <summary>
    /// Records the start position and rotation of the controller at the beginning of a trial.
    /// </summary>
    private void RecordStartState()
    {
        startControllerPosition = UpdateControllerPosition();
        startControllerRotation = UpdateControllerRotation();
    }

    /// <summary>
    /// Set target contact flags to false
    /// </summary>
    private void ResetTargetContact()
    {
        isContactingTarget = false;
        wasTargetContacted = false;
    }

    /// <summary>
    /// Reset counters for trial
    /// </summary>
    private void ResetCounters()
    {
        trialCount = 0;
        loggedTrialCount = 0;
        isFirstSelectionInTrial = true;
        ResetTotalDwellTime();
    }

    /// <summary>
    /// Resets total dwell time to 0
    /// </summary>
    private void ResetTotalDwellTime()
    {
        totalDwellTime = 0f;
    }

    /// <summary>
    /// Reset all set distance aggregates
    /// </summary>
    private void ResetAggregatesForSet()
    {
        aggregateMovementDistanceAlongAxisForSet = 0f;
        aggregateMovementDistanceAlongAllAxesForSet = 0f;
        aggregateNetMovementDistanceAlongAxisForSet = 0f;
        euclideanDistanceForSet = 0f;
        euclideanDeviationForSet = 0f;
    }

    /// <summary>
    /// Reset all movement aggregate counts
    /// </summary>
    private void ResetAggregatesForBlock()
    {
        ResetAggregatesForSet();
        aggregateMovementDistanceAlongAxisForBlock = 0f;
        aggregateMovementDistanceAlongAllAxesForBlock = 0f;
        euclideanDistanceForBlock = 0f;
        euclideanDeviationForBlock = 0f;
        aggregateNetMovementDistanceAlongAxisForBlock = 0f;
    }

    /// <summary>
    /// Set grip only for controller input - i.e., pre-target selection state
    /// </summary>
    private void GripOnly()
    {
        controllerManager.SetInputMode(ControllerManager.InputMode.GripOnly);
    }

    #endregion

    #region State Control

    /// <summary>
    /// Handle pause behaviour e.g., midpoint lineup
    /// Additionally, control behaviour from pause to unpause state
    /// </summary>
    private void PauseControl()
    {
        // If currently paused
        if (sessionManager.GetPauseState)
        {
            // Pause until grip press
            sessionManager.HandlePausedState();

            // Line up midpoint during paused state
            LineUpMidpoint();

            // Set flag to true
            wasPaused = true;

            // Exit Update as testing hasn't started/resumed yet
            return;
        }
        else
        {
            // Moving from pause to unpaused
            if (wasPaused)
            {
                // Just exited the paused state, disable the midpoint haptic visuals
                visualsManager.MidpointHapticRenderer(false);

                // Set flag to false
                wasPaused = false;
            }
        }
    }

    /// <summary>
    /// Enable switching after delay.
    /// </summary>
    private void EnableSwitching()
    {
        canSwitch = true;
    }

    #endregion

    #region Midpoint Haptic Object

    /// <summary>
    /// Enable midpoint haptic object and handle controller contact.
    /// </summary>
    public void LineUpMidpoint()
    {
        // Enable midpoint haptic
        midpointHaptic.SetActive(true);

        // Ensure the renderer is enabled
        visualsManager.MidpointHapticRenderer(true);

        // Handle controller contact with midpoint
        bool isMidpointContact = CheckIfHitMidpoint(controllerCollider);

        // New contact: start continuous haptics
        if (isMidpointContact && !isMidpointContacted)
        {
            // Set contacted flag to true
            isMidpointContacted = true;
            // Start haptic feedback on contact
            controllerManager.StartContactHaptics();
        }
        // Leaving contact: stop continuous haptics
        else if (!isMidpointContact && isMidpointContacted)
        {
            // Reset contact flag when controller leaves the collider
            isMidpointContacted = false;
            // Stop haptic feedback
            controllerManager.StopHapticFeedback();
        }
    }

    /// <summary>
    /// Adjust the width of the midpointHaptic object.
    /// </summary>
    private void AdjustMidpointHapticWidth()
    {
        if (midpointHaptic != null)
        {
            Vector3 scale = midpointHaptic.transform.localScale;
            // Set the x-scale to the adjustable width
            scale.x = midpointWidth;
            midpointHaptic.transform.localScale = scale;
        }
        else
        {
            Debug.LogError("Midpoint Haptic object is not assigned.");
        }
    }

    /// <summary>
    /// Disable midpoint haptic
    /// </summary>
    public void DisableMidpointHaptic()
    {
        midpointHaptic.SetActive(false);
    }

    #endregion

    #region Pattern Management

    /// <summary>
    /// Returns the vest growth pattern of the targets, linear, quadratic, stair, or pulse
    /// Based on target input
    /// </summary>
    /// <param name="pair">Target pair to get pattern for</param>
    /// <returns>GrowthPattern - the haptic vest growth pattern of the param targets pair</returns>
    public GrowthPattern GetGrowthPattern(TargetPair pair) => pair.growthPattern;

    /// <summary>
    /// Gets growth pattern of current target - no target pair input
    /// </summary>
    /// <returns>GrowthPattern - the haptic vest growth pattern of the current targets</returns>
    public GrowthPattern GetCurrentGrowthPattern() => currentPair.growthPattern;

    #endregion

    #region Trial Ordering

    /// <summary>
    /// Shuffle the list of TargetPair elements using the Fisher-Yates algorithm.
    /// Ensures that each target pair (i.e., each ID) is randomly presented in a block.
    /// </summary>
    /// <param name="list">The list of TargetPair elements (i.e., testing pairs) that will be randomised/shuffled</param>
    private void ShuffleList(List<TargetPair> list)
    {
        // Get the count of the list (number of targerpairs)
        int n = list.Count;

        // Randomise with Fisher-Yates shuffle algorithm with System.Random for random number generator
        System.Random rng = new System.Random();

        while (n > 1)
        {
            n--;

            // Pick a random index between 0 and n (inclusive) using rng
            int randomIndex = rng.Next(0, n + 1);

            // Store the element at the index
            var value = list[randomIndex];

            // Swap the element at the index with the element at index 'n'
            list[randomIndex] = list[n];

            // Place the element originally at 'n' into the new index - i.e., swap elements.
            list[n] = value;
        }
    }

    #endregion
}