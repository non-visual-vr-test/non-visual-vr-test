using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class FittsLaw : MonoBehaviour
{
    // Classes
    private Logs logsManager;
    private TargetManager targetManager;

    // Movement distances along axes
    [HideInInspector] public int movementAxis;                          // 0 if left/right, 1 if forward/back
    [HideInInspector] public float movementDistanceAlongAxis;           // For movement along axis between previous and active target
    [HideInInspector] public float totalMovementDistanceAlongAllAxes;   // For movement along all axes between previous and active target
    private Vector3 currentMovementAxis = Vector3.forward;              // Default to forwad

    // Lists to store data for aggregate calculations across a trial block (complete number of target pairs)
    private List<float> movementTimesForSet = new List<float>();            // Stores movement times for calculating the mean time between targets
    private List<(Vector3 endPosition, Vector3 targetCentre)> endpointPositionsWithCentresForSet = new List<(Vector3, Vector3)>();      // Endposition and centres combined list
    private List<Vector3> startingPositionsForSet = new List<Vector3>();    // Stores starting positions for effective measurements

    // Block list
    private List<(Vector3 endPosition, Vector3 targetCentre)> endpointPositionsWithCentresForBlock = new List<(Vector3, Vector3)>();    // Endpositions and centres combined list for block
    private List<Vector3> startingPositionsForBlock = new List<Vector3>();    // Stores starting positions for effective measurements

    // For set calculations
    private List<float> amplitudesForSet = new List<float>();             // Stores amplitudes (distance between targets) for each set
    private List<float> widthsForSet = new List<float>();                 // Stores target widths for each set
    private List<float> IDsForSet = new List<float>();                    // Aggregate IDs for the set

    // For block calculations
    private List<float> movementTimesForBlock = new List<float>();                  // Stores movement times for all block
    private List<float> amplitudesForBlock = new List<float>();                     // Stores amplitudes for all block
    private List<float> widthsForBlock = new List<float>();                         // Stores target widths for all block    
    private List<float> IDsForBlock = new List<float>();                            // Aggregate IDs for the block
    private List<float> effectiveIDsForBlock = new List<float>();                   // Aggregate effectoveIDsForBlock for the block
    private List<float> movementTimesForEffectiveIDsForBlock = new List<float>();   // To ensure effective throughput calulated correctly - mean movement times corresponding to the effective IDs for the whole block

    // Aggregate totals
    private float effectiveWidthPerpendicularVariabilityForSet = 0f;
    private float effectiveDistanceOvershootUndershootForSet = 0f;
    private float aggregateMovementDistanceAlongAxisForSet = 0f;
    private float aggregateMovementDistanceAlongAllAxesForSet = 0f;
    private float aggregateMovementDistanceAlongAllAxesForBlock = 0f;
    private float aggregateMovementDistanceAlongAxisForBlock = 0f;
    private float effectiveDistanceOvershootUndershootForBlock = 0f;
    private float effectiveWidthPerpendicularVariabilityForBlock = 0f;
    private float aggregateAmplitudeForSet = 0f;
    private float aggregateAmplitudeForBlock = 0f;

    // Track counts for effective measures of block
    private int numberOfEffectiveMeasuresForBlock = 0;

    #region Unity Lifecycle Method

    /// <summary>
    /// Assign classes
    /// </summary>
    void Awake()
    {
        if (logsManager == null) logsManager = FindObjectOfType<Logs>();
        if (targetManager == null) targetManager = FindObjectOfType<TargetManager>();
    }

    #endregion

    #region Calculation and Logging

    /// <summary>
    /// Calculates Fitts' Law parameters and returns a FittsLawData struct for logging.
    /// Effective measures are calculated only on the last selection of a target pair
    /// </summary>
    /// <param name="movementTime">Movment time betwen targets</param>
    /// <param name="targetWidth">Width of target along movement axis</param>
    /// <param name="distanceBetweenTargets">Amplitude distance betwen targets</param>
    /// <param name="isHit">bool hit target or not</param>
    /// <param name="startPosition">Vector3 controller position at start</param>
    /// <param name="endPosition">Vector3 controller position at end</param>
    /// <param name="currentMovementAxis">Current axis of movement between targets</param>
    /// <param name="aggregateDistanceAlongAxis">Aggregate movement along axis</param>
    /// <param name="aggregateMovementDistanceAlongAllAxes">Aggregate movement along all axes</param>
    /// <param name="movementAxis">Current movement axis int identifier</param>
    /// <param name="isLastSelectionInPair">Flag for set calculations</param>
    /// <param name="isLastSelectionInBlock">Flag for block calcuations</param>
    /// <param name="axisDifference">OVershoot/undershoot distance along axis</param>
    /// <param name="targetCentrePosition">Vector3 centre of target</param>
    /// <param name="controllerWidth">Width of controller anchor</param>
    /// <param name="currentAxisDistance">Movment along axis</param>
    /// <param name="totalPathLength">Total movement along all axes</param>
    /// <param name="euclideanDeviation">Deviation from straight-line movement</param>
    /// <returns>FittsLawData struct</returns>
    public FittsLawData CalculateFittsLawData(
        float movementTime,
        float targetWidth,
        float distanceBetweenTargets,
        bool isHit,
        Vector3 startPosition,
        Vector3 endPosition,
        Vector3 currentMovementAxis,
        float aggregateDistanceAlongAxis,
        float aggregateMovementDistanceAlongAllAxes,
        int movementAxis,
        bool isLastSelectionInPair,
        bool isLastSelectionInBlock,
        float axisDifference,
        Vector3 targetCentrePosition,
        float controllerWidth,
        float currentAxisDistance,
        float totalPathLength,
        float euclideanDeviation)
    {
        // Error handling for logging
        if (logsManager == null)
        {
            Debug.LogError("FittsLaw: logsManager is not assigned in the Inspector.");
            return default;
        }

        // Validate inputs - ensure all are greater than zero to prevent errors in calculations
        if (!ValidateInputs(movementTime, targetWidth, distanceBetweenTargets)) return default;

        // Fitts' Law calculations
        // Calculate the Index of Difficulty (ID)
        float ID = CalculateID(distanceBetweenTargets, targetWidth);

        // Add ID to lists for set and block
        IDsForSet.Add(ID);
        IDsForBlock.Add(ID);

        // Determine the precision level required for the task
        int taskPrecision = GetPrecisionLevel(ID);
        
        // Calculate throughput based on ID and movement time
        float throughput = CalculateThroughput(ID, movementTime);

        // Hit or miss - convert to 1 for hit or 0 for miss
        int hitMissValue = isHit ? 1 : 0;

        // Add data to set lists for aggregate calculations at the end of the set
        movementTimesForSet.Add(movementTime);
        amplitudesForSet.Add(distanceBetweenTargets);
        widthsForSet.Add(targetWidth);
        startingPositionsForSet.Add(startPosition);
        endpointPositionsWithCentresForSet.Add((endPosition, targetCentrePosition));

        // Add data to block lists for end of block calculations
        movementTimesForBlock.Add(movementTime);
        amplitudesForBlock.Add(distanceBetweenTargets);
        widthsForBlock.Add(targetWidth);
        startingPositionsForBlock.Add(startPosition);
        endpointPositionsWithCentresForBlock.Add((endPosition, targetCentrePosition));

        // Set the current movement axis for calculations (normalise to ensure correct direction)
        this.currentMovementAxis = currentMovementAxis.normalized;

        // Initialise effective measures to zero for logging
        float effectiveISOWidth = 0f;
        float effectivePerpendicularDeviation = 0f;
        float effectiveISODistance = 0f;
        float effectiveISOIDe = 0f;
        float effectiveISOThroughput = 0f;

        // Set and Block measurement variable initialised and set to 0 for logging
        float throughputForSet = 0f;
        float idForBlock = 0f;
        float throughputForBlock = 0f;
        float effectiveISOWidthForBlock = 0f;
        float effectivePerpendicularDeviationForBlock = 0f;
        float effectiveISODistanceForBlock = 0f;
        float effectiveISOIDForBlock = 0f;
        float effectiveISOThroughputForBlock = 0f;

        // Per-target deviation perpendicular to movement axis - effective width perpendicular variabliity for each target
        // Measure deviation from the startPosition to get movements from startPosition along perpendicular axes
        // Vector from start to end position
        Vector3 delta = endPosition - startPosition;
        // Project delta onto movement axis
        float projectionLength = Vector3.Dot(delta, this.currentMovementAxis);
        // Projection vector along movement axis
        Vector3 projectionVector = projectionLength * this.currentMovementAxis;
        // Deviation perpendicular to the movement axis - residual vector (perpendicular component)
        Vector3 residualVector = delta - projectionVector;
        // Magnitude of the residual vector - gives the perpendicular deviation
        float deviationPerpendicular = residualVector.magnitude;

        // Calculate per-trial movement distances
        Vector3 deltaMovement = endPosition - startPosition;
        float perTrialMovementDistanceAlongAxis = Mathf.Abs(Vector3.Dot(deltaMovement, currentMovementAxis));
        float perTrialTotalMovementDistance = deltaMovement.magnitude;

        // Accumulate distances for set
        aggregateMovementDistanceAlongAxisForSet += perTrialMovementDistanceAlongAxis;
        aggregateMovementDistanceAlongAllAxesForSet += perTrialTotalMovementDistance;
        effectiveWidthPerpendicularVariabilityForSet += deviationPerpendicular;
        effectiveDistanceOvershootUndershootForSet += axisDifference;
        aggregateAmplitudeForSet += distanceBetweenTargets;

        // Aggregate distances for block
        aggregateMovementDistanceAlongAxisForBlock += perTrialMovementDistanceAlongAxis;
        aggregateMovementDistanceAlongAllAxesForBlock += perTrialTotalMovementDistance;
        effectiveWidthPerpendicularVariabilityForBlock += deviationPerpendicular;
        effectiveDistanceOvershootUndershootForBlock += axisDifference;
        aggregateAmplitudeForBlock += distanceBetweenTargets;

        // Increment block count for effective measures
        numberOfEffectiveMeasuresForBlock += 1;

        // Calculate effective measures and for trial measures only if this is the last selection in the target pair - effective measure and set overall measures
        if (isLastSelectionInPair && endpointPositionsWithCentresForSet.Count >= 2)
        {
            // Calculate effective width along movement axis using collected endpoint positions
            effectiveISOWidth = CalculateEffectiveISOWidth(endpointPositionsWithCentresForSet);

            // Calculate effective deviation along perpendicular axes
            effectivePerpendicularDeviation = CalculateEffectivePerpendicularDeviation(endpointPositionsWithCentresForSet, startingPositionsForSet);

            // Calculate effective distance using collected starting and ending positions
            effectiveISODistance = CalculateEffectiveISODistance(endpointPositionsWithCentresForSet, startingPositionsForSet);

            // Calculate effective ID - ISO standard for calculations
            effectiveISOIDe = CalculateEffectiveISOIDe(effectiveISODistance, effectiveISOWidth);

            // Add to effective IDs list
            effectiveIDsForBlock.Add(effectiveISOIDe);

            // Calculate mean movement time for the set
            float meanMovementTime = movementTimesForSet.Average();

            // Add mean movement time to movement times list for block
            movementTimesForEffectiveIDsForBlock.Add(meanMovementTime);

            // Calculate effective throughput with effective ID and mean movement time measures
            effectiveISOThroughput = CalculateThroughput(effectiveISOIDe, meanMovementTime);

            // Calulate throughput for set
            throughputForSet = CalculateThroughputForSetAndBlock(IDsForSet, movementTimesForSet);
        }

        // Calculate measures for the block if this is the last selection in the block
        if (isLastSelectionInBlock &&  endpointPositionsWithCentresForBlock.Count >= 2)
        {
            // Calculate the ID for the entire block
            idForBlock = CalculateIDForBlock(amplitudesForBlock, widthsForBlock);

            // Calculate throughput for the block
            throughputForBlock = CalculateThroughputForSetAndBlock(IDsForBlock, movementTimesForBlock);

            // Effective width along movement axis with all endpoint positions
            effectiveISOWidthForBlock = CalculateEffectiveISOWidth(endpointPositionsWithCentresForBlock);

            // Calculate effective perpendicular deviation for block
            effectivePerpendicularDeviationForBlock = CalculateEffectivePerpendicularDeviation(endpointPositionsWithCentresForBlock, startingPositionsForBlock);

            // Calculate effective ISO distance for block
            effectiveISODistanceForBlock = CalculateEffectiveISODistance(endpointPositionsWithCentresForBlock, startingPositionsForBlock);

            // Calculate effective ISO IDe for block
            effectiveISOIDForBlock = CalculateEffectiveISOIDe(effectiveISODistanceForBlock, effectiveISOWidthForBlock);

            // Calculate mean movement time for the block
            float meanMovementTimeForBlock = movementTimesForBlock.Average();

            // Calculate effective ISO throughput for block
            effectiveISOThroughputForBlock = CalculateThroughputForSetAndBlock(effectiveIDsForBlock, movementTimesForEffectiveIDsForBlock);
        }

        // Create FittsLawData struct to return for logging
        FittsLawData data = new FittsLawData
        {
            MovementTimeMs = movementTime,
            TargetWidth = targetWidth,
            DistanceBetweenTargets = distanceBetweenTargets,
            ID = ID,
            TaskPrecision = taskPrecision,
            Throughput = throughput,
            HitMissValue = hitMissValue,
            AggregateMovementDistanceAlongAllAxes = aggregateMovementDistanceAlongAllAxes,
            MovementAxis = movementAxis,
            EffectiveISOWidth = effectiveISOWidth,
            EffectivePerpendicularDeviation = effectivePerpendicularDeviation,
            EffectiveDistance = effectiveISODistance,
            EffectiveISOIDe = effectiveISOIDe,
            EffectiveISOThroughput = effectiveISOThroughput,
            EffectiveWidthPerpendicularVariability = deviationPerpendicular,
            EffectiveDistanceOvershootUndershoot = axisDifference,
            TotalPathLength = totalPathLength,
            EuclideanDeviation = euclideanDeviation,
            ControllerWidth = controllerWidth,
            CurrentAxisDistance = currentAxisDistance,
            ThroughputForSet = throughputForSet,
            EffectiveWidthPerpendicularVariabilityForSet = effectiveWidthPerpendicularVariabilityForSet,
            EffectiveDistanceOvershootUndershootForSet = effectiveDistanceOvershootUndershootForSet,
            AggregateMovementDistanceAlongAxisForSet = aggregateMovementDistanceAlongAxisForSet,
            AggregateMovementDistanceAlongAllAxesForSet = aggregateMovementDistanceAlongAllAxesForSet,
            ThroughputForBlock = throughputForBlock,
            IDForBlock = idForBlock,
            EffectiveISOWidthForBlock = effectiveISOWidthForBlock,
            EffectivePerpendicularDeviationForBlock = effectivePerpendicularDeviationForBlock,
            EffectiveISODistanceForBlock = effectiveISODistanceForBlock,
            EffectiveISOIDForBlock = effectiveISOIDForBlock,
            EffectiveISOThroughputForBlock = effectiveISOThroughputForBlock,
            EffectiveWidthPerpendicularVariabilityForBlock = effectiveWidthPerpendicularVariabilityForBlock,
            EffectiveDistanceOvershootUndershootForBlock = effectiveDistanceOvershootUndershootForBlock,
            AggregateMovementDistanceAlongAxisForBlock = aggregateMovementDistanceAlongAxisForBlock,
            AggregateMovementDistanceAlongAllAxesForBlock = aggregateMovementDistanceAlongAllAxesForBlock,
            AmplitudeAggregateForSet = aggregateAmplitudeForSet,
            AmplitudeAggregateForBlock = aggregateAmplitudeForBlock
        };

        return data;
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validates the input parameters to ensure they are greater than zero.
    /// </summary>
    /// <returns>bool true if all inputs are greter than zero.</returns>
    private bool ValidateInputs(float movementTime, float targetWidth, float distanceBetweenTargets)
    {
        if (movementTime <= 0f || targetWidth <= 0f || distanceBetweenTargets <= 0f)
        {
            Debug.LogError("FittsLaw: Invalid input parameters.");
            return false;
        }
        return true;
    }

    #endregion

    #region Per Trial Fitts Law

    /// <summary>
    /// Calculates the Index of Difficulty (ID) using Fitts' Law formula: ID = log2((A / W) + 1)
    /// Shannon formulation of Fitts' Law - Add 1 to log to ensure positive and non-zero result
    /// </summary>
    /// <returns>float index of difficulty (ID) based on distance between targets and target width.</returns>
    private float CalculateID(float amplitude, float width)
    {
        // Mathf.Log with base 2
        return Mathf.Log((amplitude / width) + 1f, 2f);
    }

    /// <summary>
    /// Calculates the Throughput in bits per second of set.
    /// </summary>
    /// /// <param name="ID">ID for selection</param>
    /// <param name="movementTimeMs">Movement time for selection</param>
    /// <returns>float throughput based on ID and movement time.</returns>
    private float CalculateThroughput(float ID, float movementTimeMs)
    {
        // Convert ms to seconds
        return ID / (movementTimeMs / 1000f);
    }

    /// <summary>
    /// Task precision according to ISO standard - measure of the accuracy required for selecting task quantified by ID.
    /// </summary>
    /// <param name="id">ID for current selection</param>
    /// <returns>Int precision level</returns>
    private int GetPrecisionLevel(float id)
    {
        // High precision
        if (id > 6)
        {
            return 1;
        }
        // Medium precision
        else if (id > 4 && id <= 6)
        {
            return 2;
        }
        // Low precision
        else if (id > 3 && id <= 4)
        {
            return 3;
        }
        // Very low precision
        else
        {
            return 4;
        }
    }

    #endregion

    #region Set and Block Fitts Law

    /// <summary>
    /// Calculates the Effective Index of Difficulty (IDe) using amplitude (A) and effective width (We) - ISO 9241-411 standard Fitts' Law formula : IDe = log2((A + We) / We)
    /// Adds target width to movement distance - more reflective of actual performance
    /// </summary>
    /// <param name="amplitude">Distance between targets (A)</param>
    /// <param name="effectiveWidth">Effective width of the target (We)</param>
    /// <returns>float index of difficulty (ID) based on distance between targets and target width.</returns>
    private float CalculateEffectiveISOIDe(float amplitude, float effectiveWidth)
    {
        // Mathf.Log with base 2
        return Mathf.Log((amplitude + effectiveWidth) / effectiveWidth, 2f);
    }

    /// <summary>
    /// Calculates the Index of Difficulty (ID) for full trial using Fitts' Law formula: ID = log2((A / W) + 1)
    /// </summary>
    /// <param name="amplitudes">List of amplitudes for the block</param>
    /// <param name="widths">List of widths for the block</param>
    /// <returns>ID for set</returns>
    private float CalculateIDForBlock(List<float> amplitudes, List<float> widths)
    {
        float totalAmplitude = amplitudes.Sum();
        float totalWidth = widths.Sum();

        return Mathf.Log((totalAmplitude / totalWidth) + 1f, 2f);
    }

    /// <summary>
    /// Caluculates the throughput for group of selections - set and block
    /// Not strictly following ISO standards - not for publication - but could be interesting to see leaerning etc.
    /// </summary>
    /// <param name="ID">ID of group of selection</param>
    /// <param name="movementTimes">List of movement times for multiple selections</param>
    /// <returns>float throughput for combined sets</returns>
    private float CalculateThroughputForSetAndBlock(List<float> IDs, List<float> movementTimes)
    {
        if (movementTimes == null || movementTimes.Count == 0 || IDs == null || IDs.Count != movementTimes.Count) return 0f;

        float totalID = IDs.Sum();
        // Convert ms to seconds
        float totalMovementTimeSeconds = movementTimes.Sum() / 1000f;

        // Division by zero handler
        if (totalMovementTimeSeconds <= 0f) return 0f;

        return totalID / totalMovementTimeSeconds;
    }

    /// <summary>
    /// /// <summary>
    /// Calculates the effective width (We) using the standard deviation of endpoint positions using deviations along the movement axis - according to ISO standard
    /// Accounting for spread of the movement endpoints in relation to centre of target in the direction of movement - variability in endpoint accuracy along the movement axis.
    /// Used for calculating precision - the consistency of hitting the target along the movement axis - reflects spatial accuracy rather than control over movement length (determined instead by CalculateEffectiveISODistance())
    /// <param name="endpointPositionsWithCentres">The end and centre position of the target as combined Vector3 list</param>
    /// <returns>float effective width of block of targets.</returns>
    private float CalculateEffectiveISOWidth(List<(Vector3 endPosition, Vector3 targetCentre)> endpointPositionsWithCentres)
    {
        // Error handling for insufficient data
        if (endpointPositionsWithCentres.Count < 2)
        {
            Debug.LogError("CalculateEffectiveISOWidth: Insufficient data for calculation.");
            return 0f;
        }

        // List to store deviations for all targets
        List<float> allDeviations = new List<float>();

        // Calculate deviations from the target centre along the movement axis for each endpoint position
        foreach (var pair in endpointPositionsWithCentres)
        {
            Vector3 endPosition = pair.endPosition;
            Vector3 targetCentre = pair.targetCentre;

            Vector3 delta = endPosition - targetCentre;
            float deviation = Vector3.Dot(delta, this.currentMovementAxis.normalized);
            allDeviations.Add(deviation);
        }

        // Calculate standard deviation of all deviations
        float SDx = CalculateStandardDeviation(allDeviations);

        // Multiply by constant derived from normal distribution assumptions
        float effectiveISOWidth = 4.133f * SDx;

        Debug.Log($"CalculateEffectiveISOWidth: SDx = {SDx}, EffectiveISOWidth = {effectiveISOWidth}");

        return effectiveISOWidth;
    }

    /// <summary>
    /// Calculates the effective deviation using the standard deviation of endpoint positions using deviations perpendicular to movement axis.
    /// Accounting for perpendicular variability. Deviations are measured perpendicular to the movement axis relative to startPosition.
    /// </summary>
    /// <param name="endpointPositionsWithCentres">List of endpositions and centres</param>
    /// <param name="startpointPositions">List of start psotiions</param>
    /// <returns>float effective width of block of targets.</returns>
    private float CalculateEffectivePerpendicularDeviation(List<(Vector3 endPosition, Vector3 targetCentre)> endpointPositionsWithCentres, List<Vector3> startpointPositions)
    {
        // Error handling for 0 or 1 target only
        if (endpointPositionsWithCentres.Count < 2) return 0f;

        // List to store deviations perpendicular to the movement axis
        List<float> perpendicularDeviations = new List<float>();

        // Iterate through each pair of starting and endpoint positions
        for (int i = 0; i < endpointPositionsWithCentres.Count; i++)
        {
            Vector3 endPosition = endpointPositionsWithCentres[i].endPosition;
            Vector3 startPosition = startpointPositions[i];

            // Vector from start to end position
            Vector3 delta = endPosition - startPosition;

            // Project delta onto movement axis
            float projectionLength = Vector3.Dot(delta, this.currentMovementAxis);
            Vector3 projectionVector = projectionLength * this.currentMovementAxis;

            // Residual vector (perpendicular component)
            Vector3 residualVector = delta - projectionVector;

            // Deviation magnitude
            float deviationPerpendicular = residualVector.magnitude;
            perpendicularDeviations.Add(deviationPerpendicular);
        }

        // Calculate standard deviation
        float SDx = CalculateStandardDeviation(perpendicularDeviations);

        // Return effective width
        return 4.133f * SDx;
    }

    /// <summary>
    /// Calculates the effective distance using the average movement distance along the movement axis.
    /// Actual distance the user moved along the movement axis, accounting for any overshoots or undershoots along movement axis.
    /// </summary>
    /// <param name="endpointPositionsWithCentres">List of endpositions and target centres</param>
    /// <param name="startpointPositions">List of start positions</param>
    /// <returns>float effective distance for block of targets.</returns>
    private float CalculateEffectiveISODistance(List<(Vector3 endPosition, Vector3 targetCentre)> endpointPositionsWithCentres, List<Vector3> startpointPositions)
    {
        // Error handling for 0 or 1 target only
        if (endpointPositionsWithCentres.Count < 2 || startpointPositions.Count < 2) return 0f;

        // Create list to store distances along the movement axis
        List<float> distances = new List<float>();

        // Calculate the movement distances along the movement axis for all target pair selections
        for (int i = 0; i < endpointPositionsWithCentres.Count; i++)
        {
            // Calculate the distance between start and endpoint along the movement axis
            float distance = Vector3.Dot(endpointPositionsWithCentres[i].endPosition - startpointPositions[i], currentMovementAxis);
            distances.Add(Mathf.Abs(distance));
        }

        // Return the mean of these distances
        return distances.Average();
    }

    #endregion

    #region Maths Helper

    /// <summary>
    /// Calculates the standard deviation of a list of float values.
    /// </summary>
    /// <param name="values">List of float values</param>
    /// <returns>float standard deviation of float values list.</returns>
    private float CalculateStandardDeviation(List<float> values)
    {
        if (values.Count < 2) return 0f;

        // Calculate mean of the values
        float mean = values.Average();
        // Sum of squared differences from the mean
        float sumOfSquares = values.Sum(v => (v - mean) * (v - mean));
        // Calculate standard deviation
        return Mathf.Sqrt(sumOfSquares / (values.Count - 1));
    }

    #endregion

    #region List Control

    /// <summary>
    /// Adds a new start position to the startingPositionsForSet list whenever a target changes.
    /// </summary>
    /// <param name="startPosition">The start position of the movement</param>
    public void AddStartPosition(Vector3 startPosition)
    {
        startingPositionsForSet.Add(startPosition);
    }

    /// <summary>
    /// Adds a new end position to the endpointPositionsWithCentresForSet list whenever a target changes.
    /// </summary>
    /// <param name="endPosition">The end position of the movement</param>
    public void AddEndPosition(Vector3 endPosition, Vector3 targetCentre)
    {
        endpointPositionsWithCentresForSet.Add((endPosition, targetCentre));
    }

    /// <summary>
    /// Resets the data collections used for calculating effective measures. Called when starting a new target pair.
    /// </summary>
    public void ResetSetLists()
    {
        // Clear all data collections for the current set
        movementTimesForSet.Clear();
        endpointPositionsWithCentresForSet.Clear();
        startingPositionsForSet.Clear();
        amplitudesForSet.Clear();
        widthsForSet.Clear();
        IDsForSet.Clear();

        effectiveWidthPerpendicularVariabilityForSet = 0f;
        effectiveDistanceOvershootUndershootForSet = 0f;
        aggregateMovementDistanceAlongAxisForSet = 0f;
        aggregateMovementDistanceAlongAllAxesForSet = 0f;
        aggregateAmplitudeForSet = 0f;
    }

    /// <summary>
    /// Resets the block lists used for calculations. Called when starting a new block.
    /// </summary>
    public void ResetBlockLists()
    {
        // Clear all data collections for the current block
        movementTimesForBlock.Clear();
        endpointPositionsWithCentresForBlock.Clear();
        startingPositionsForBlock.Clear();
        amplitudesForBlock.Clear();
        widthsForBlock.Clear();
        IDsForBlock.Clear();
        movementTimesForEffectiveIDsForBlock.Clear();
        effectiveIDsForBlock.Clear();

        aggregateMovementDistanceAlongAllAxesForBlock = 0f;
        aggregateMovementDistanceAlongAxisForBlock = 0f;
        effectiveDistanceOvershootUndershootForBlock = 0f;
        effectiveWidthPerpendicularVariabilityForBlock = 0f;
        aggregateAmplitudeForBlock = 0f;
        numberOfEffectiveMeasuresForBlock = 0;
    }

    #endregion
}
