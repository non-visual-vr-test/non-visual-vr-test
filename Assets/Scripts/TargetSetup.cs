using System.Collections.Generic;
using UnityEngine;

public class TargetSetup : MonoBehaviour
{
    // Classes
    private VisualsManager visualsManager;
    private Logs logs;

    [Header("Base Targets")]
    // Predefined GameObjects in the scene for each target position
    public GameObject forwardTarget;
    public GameObject backTarget;
    public GameObject leftTarget;
    public GameObject rightTarget;

    // Scale
    private float defaultScale = 50f;       // Scale of target not along movement axis

    [Header("Target Distance")]
    // Distances between targets
    public float trainingDistance = 0.12f;
    public float shortDistance = 0.05f;
    public float mediumDistance = 0.1f;
    public float longDistance = 0.15f;

    [Header("Target Width")]
    // Widths of targets along movement axis
    public float trainingWidth = 0.025f;
    public float smallWidth = 0.015f;
    public float largeWidth = 0.035f;

    #region Unity Lifecycle Methods

    /// <summary>
    /// Assign classes
    /// </summary>
    private void Awake()
    {
        if (visualsManager == null) visualsManager = FindObjectOfType<VisualsManager>();
        if (logs == null) logs = FindObjectOfType<Logs>();
    }

    /// <summary>
    /// Validate targets assigned
    /// </summary>
    private void Start()
    {
        // Validate that all base targets are assigned
        if (forwardTarget == null) Debug.LogError("TargetSetup: forwardTarget is not assigned in the Inspector.");

        if (backTarget == null) Debug.LogError("TargetSetup: backTarget is not assigned in the Inspector.");

        if (leftTarget == null) Debug.LogError("TargetSetup: leftTarget is not assigned in the Inspector.");

        if (rightTarget == null) Debug.LogError("TargetSetup: rightTarget is not assigned in the Inspector.");
    }

    #endregion

    #region Initialisation Methods

    /// <summary>
    /// Intialise all targets after placing in allPairs list
    /// </summary>
    /// <param name="trainingPairs">List of training target pairs</param>
    /// <param name="testingPairs">List of testing target pairs</param>
    public void InitialiseTargets(List<TargetManager.TargetPair> trainingPairs, List<TargetManager.TargetPair> testingPairs)
    {
        // Error handling null check
        if (trainingPairs == null || testingPairs == null)
        {
            Debug.LogError("TrainingPairs or TestingPairs is null.");
            return;
        }

        // Combine training and testing pairs into a single list
        List<TargetManager.TargetPair> allPairs = new List<TargetManager.TargetPair>();
        allPairs.AddRange(trainingPairs);
        allPairs.AddRange(testingPairs);

        // Iterate over each pair and set up the targets
        for (int i = 0; i < allPairs.Count; i++)
        {
            // Assign pairIndex for target pair
            var pair = allPairs[i];
            pair.pairIndex = i;

            // Set up target1 and target2
            SetupTargetPair(pair);
        }
    }

    #endregion

    #region Target Pair Setup

    /// <summary>
    /// Set up new pair of targets
    /// </summary>
    /// <param name="pair">Target pair to be set up</param>
    private void SetupTargetPair(TargetManager.TargetPair pair)
    {
        // Skip pairs with direction set to Off
        if (pair.firstTargetDirection == TargetManager.DirectionType.Off)
        {
            Debug.LogWarning($"TargetPair with Off direction is being skipped.");
            return;
        }

        // Determine the first target GameObject based on firstTargetDirection
        GameObject baseTarget1 = GetTargetGameObject(pair.firstTargetDirection);

        // Null check for first target
        if (baseTarget1 == null)
        {
            Debug.LogError($"Base target for direction {pair.firstTargetDirection} is not found. Pair skipped.");
            return;
        }

        // Instantiate a copy of the base target
        GameObject target1 = Instantiate(baseTarget1);
        
        // Give unique identifier
        target1.name = baseTarget1.name + "_Pair" + pair.GetHashCode() + "_Target1";

        // Set second target in the opposite direction
        TargetManager.DirectionType secondTargetDirection = GetOppositeDirection(pair.firstTargetDirection);

        // Determine the second target GameObject based on the opposite direction
        GameObject baseTarget2 = GetTargetGameObject(secondTargetDirection);

        // Null check for second target
        if (baseTarget2 == null)
        {
            Debug.LogError($"Base target for direction {secondTargetDirection} is not found. Pair skipped.");
            return;
        }

        // Instantiate a copy of the second base target
        GameObject target2 = Instantiate(baseTarget2);

        // Give unique identifier
        target2.name = baseTarget2.name + "_Pair" + pair.GetHashCode() + "_Target2";

        // Debug logs to verify instantistion
        if (target1 == null || target2 == null) Debug.LogError("Failed to instantiate targets.");

        // Set up the positions and scales of the targets based on the distance and width parameters assinged in Inspector
        SetupTarget(target1, pair.firstTargetDirection, pair.distance, pair.targetWidth);
        SetupTarget(target2, secondTargetDirection, pair.distance, pair.targetWidth);

        // Assign the instantiated target GameObjects and directions to the pair
        pair.target1 = target1;
        pair.direction1 = pair.firstTargetDirection;
        pair.target2 = target2;
        pair.direction2 = secondTargetDirection;

        // Debug logs to verify pair instantistion
        Debug.Log($"Pair initialized: Target1 = {pair.target1}, Target2 = {pair.target2}");

        // Initialise visuals for the targets by caching their original colours
        if (visualsManager != null)
        {
            visualsManager.CacheOriginalColour(target1);
            visualsManager.CacheOriginalColour(target2);
        }

        // After setting up the target's position and scale - pass boxcolliders to logs
        BoxCollider collider1 = target1.GetComponent<BoxCollider>();
        BoxCollider collider2 = target2.GetComponent<BoxCollider>();

        // Only log when target changes - cache corners to improve perfomance
        if (collider1 != null)
        {
            // Multiply pairIndex by 2 to get a unique index for target1
            logs.LogTargetBoxColliderCorners(pair.pairIndex * 2, collider1);
        }
        else
        {
            Debug.LogWarning($"Target {target1.name} does not have a BoxCollider.");
        }

        if (collider2 != null)
        {
            // Multiply the pairIndex by 2 + 1 to get a unique index for target2
            logs.LogTargetBoxColliderCorners(pair.pairIndex * 2 + 1, collider2);
        }
        else
        {
            Debug.LogWarning($"Target {target2.name} does not have a BoxCollider.");
        }
    }

    #endregion

    #region Target GameObject

    /// <summary>
    /// Get gameObject to use as target based on DirectionType
    /// </summary>
    /// <param name="direction">The direction of the target</param>
    /// <returns>GameObject target.</returns>
    private GameObject GetTargetGameObject(TargetManager.DirectionType direction)
    {
        // Return the corresponding target GameObject based on the direction
        switch (direction)
        {
            case TargetManager.DirectionType.Forward:
                return forwardTarget;
            case TargetManager.DirectionType.Back:
                return backTarget;
            case TargetManager.DirectionType.Left:
                return leftTarget;
            case TargetManager.DirectionType.Right:
                return rightTarget;
            default:
                return null;
        }
    }

    /// <summary>
    /// Set up the positions and scales of the target based on the direction, distance, and width
    /// </summary>
    /// <param name="target">The target GameObject to be set up</param>
    /// <param name="direction">The direction of the target</param>
    /// <param name="distanceType">The distance type between targets</param>
    /// <param name="widthType">The width type of the target</param>
    private void SetupTarget(GameObject target,
        TargetManager.DirectionType direction,
        TargetManager.DistanceType distanceType,
        TargetManager.TargetWidthType widthType)
    {
        // Dfault position for all targets
        Vector3 position = Vector3.zero;

        // Default scale for all targets
        Vector3 scale = new Vector3(defaultScale, defaultScale, defaultScale);

        // Get the distance and width values based on the specified types
        float distance = GetDistanceValue(distanceType);
        float width = GetWidthValue(widthType, direction);

        // Set position and scale based on the direction
        if (direction == TargetManager.DirectionType.Forward || direction == TargetManager.DirectionType.Back)
        {
            // Move along z axis
            float z = direction == TargetManager.DirectionType.Forward ? distance : -distance;
            position.z = z;
            // Set z scale to width
            scale.z = width;
        }
        else if (direction == TargetManager.DirectionType.Left || direction == TargetManager.DirectionType.Right)
        {
            // Move along x axis
            float x = direction == TargetManager.DirectionType.Right ? distance : -distance;
            position.x = x;
            // Set x scale to width
            scale.x = width;
        }

        // Apply the position and scale to the target's transform
        target.transform.position = position;
        target.transform.localScale = scale;
    }

    #endregion

    #region Direction, Distance, and Width

    /// <summary>
    /// Get the opposite direction of the specified DirectionType
    /// </summary>
    /// <param name="direction">The direction for which to find the opposite</param>
    /// <returns>The opposite DirectionType</returns>
    private TargetManager.DirectionType GetOppositeDirection(TargetManager.DirectionType direction)
    {
        // Return the opposite direction based on the input direction
        switch (direction)
        {
            case TargetManager.DirectionType.Forward:
                return TargetManager.DirectionType.Back;
            case TargetManager.DirectionType.Back:
                return TargetManager.DirectionType.Forward;
            case TargetManager.DirectionType.Left:
                return TargetManager.DirectionType.Right;
            case TargetManager.DirectionType.Right:
                return TargetManager.DirectionType.Left;
            default:
                return TargetManager.DirectionType.Off;
        }
    }

    /// <summary>
    /// Get the distance value based on the specified DistanceType
    /// </summary>
    /// <param name="distanceType">The type of distance between targets</param>
    /// <returns>The distance value as a float</returns>
    private float GetDistanceValue(TargetManager.DistanceType distanceType)
    {
        // Return the corresponding distance value based on the distance type
        switch (distanceType)
        {
            case TargetManager.DistanceType.Training:
                return trainingDistance;
            case TargetManager.DistanceType.Short:
                return shortDistance;
            case TargetManager.DistanceType.Medium:
                return mediumDistance;
            case TargetManager.DistanceType.Long:
                return longDistance;
            default:
                return 0f;
        }
    }

    /// <summary>
    /// Get the width of the target based on the specified TargetWidthType and direction
    /// </summary>
    /// <param name="widthType">The type of width for the target</param>
    /// <param name="direction">The direction of the target</param>
    /// <returns>The width value as a float</returns>
    private float GetWidthValue(TargetManager.TargetWidthType widthType, TargetManager.DirectionType direction)
    {
        float width = 0f;

        // Return the corresponding width value based on the width type
        switch (widthType)
        {
            case TargetManager.TargetWidthType.Training:
                width = trainingWidth;
                break;
            case TargetManager.TargetWidthType.Small:
                width = smallWidth;
                break;
            case TargetManager.TargetWidthType.Large:
                width = largeWidth;
                break;
            default:
                break;
        }

        return width;
    }

    #endregion
}
