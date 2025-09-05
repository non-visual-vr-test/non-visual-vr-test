using UnityEngine;
using static FittsLaw;

public class OvershootUndershootManager : MonoBehaviour
{
    // Over and undershoot tracking along movement axis
    private int overshotCount = 0;                                  // Number of times overshooting target on set axis
    private int undershotCount = 0;                                 // Number of times undershooting target on set axis
    private int reEntryNumber = 0;                                  // Combined time overshooting and undershooting

    // Timers
    [SerializeField] private float cooldownDuration = 0f;           // Cooldown duration in seconds to prevent micromovements from being counted
    private float lastCountTime = -Mathf.Infinity;                  // Timer for cooldown - initialised to negative infinity to allow immediate counting at start

    // Distances to closest point of target collider
    private float overshootDistance = 0f;                           // Current overshoot distance - difference in distance between contoller and nearest target position beyond target
    private float undershootDistance = 0f;                          // Current undershoot distance - difference in distance between contoller and nearest target position before target
    private float axisDifference = 0f;                              // Difference along the movement axis - either under or overshot - positive for undershot, negative for overshot

    // Accumulating the total distances overshot and undershot across trials.
    private float totalOvershootDistance = 0f;                      // Total accumulated overshoot distance
    private float totalUndershootDistance = 0f;                     // Total accumulated undershoot distance

    // State Flags - determining whether certain conditions have been met to count undershoots or overshoots
    private bool hasContactedTargetAtLeastOnce = false;             // Indicates if the target has been contacted at least once
    private bool hasOvershotAtLeastOnce = false;                    // Indicates if overshooting has occurred at least once (after conditions met)
    private bool hasUndershotAtLeastOnce = false;                   // Indicates if undershooting has occurred at least once
    private bool isCurrentlyOvershooting = false;                   // Flag for current overshooting state
    private bool isCurrentlyUndershooting = false;                  // Flag for current undershooting state

    // Re-entry flags
    private bool wasInContact = false;                              // Tracks if the user was previously in contact with the target
    private bool hasReEntryBeenCounted = false;                     // Ensures ReEntryNumber is incremented only once per valid re-entry

    // Peak movement tracking - maximum overshoot and undershoot distances within a single overshoot or undershoot event
    private float currentOvershootPeak = 0f;                        // Peak overshoot distance in the current overshoot event
    private float currentUndershootPeak = 0f;                       // Peak undershoot distance in the current undershoot event

    // State transition
    private float previousAxisDifference = 0f;                      // Previous axis difference for state transition checks

    // Track correction distances during correction phase
    private float correctionOvershootDistance = 0f;                 // Accumulated overshoot distance during correction phase
    private float correctionUndershootDistance = 0f;                // Accumulated undershoot distance during correction phase
    private float correctionTotalDistance = 0f;                     // Accumulated total movement distance during correction phase
    private bool isInCorrectionPhase = false;                       // Indicates if the correction phase is active

    // Controller Position Tracking - overshoot and undershoot crossing detection variables - calculate the differences in movement along the axis of interest
    private Vector3 previousControllerPosition = Vector3.zero;      // Previous controller position
    private float previousAxisPosition = 0f;                        // Previous position along the movement axis - z or z axis - controller position on previous loop
    private float currentAxisPosition = 0f;                         // Current position along the movement axis - z or z axis - controller position on current loop
    private float targetAxisPosition = 0f;                          // Target position along the movement axis

    #region Initialisation and Reset Counters

    /// <summary>
    /// Initialise by resetting all counters and states
    /// </summary>
    public OvershootUndershootManager()
    {
        ResetOvershootUndershoot();
    }

    /// <summary>
    /// Resets all overshoot and undershoot counters and related states.
    /// </summary>
    public void ResetOvershootUndershoot()
    {
        overshotCount = 0;
        undershotCount = 0;
        reEntryNumber = 0;
        overshootDistance = 0f;
        undershootDistance = 0f;
        axisDifference = 0f;
        totalOvershootDistance = 0f;
        totalUndershootDistance = 0f;
        correctionOvershootDistance = 0f;
        correctionUndershootDistance = 0f;
        correctionTotalDistance = 0f;

        isInCorrectionPhase = false;
        hasContactedTargetAtLeastOnce = false;
        hasOvershotAtLeastOnce = false;
        hasUndershotAtLeastOnce = false;

        isCurrentlyOvershooting = false;
        isCurrentlyUndershooting = false;

        currentOvershootPeak = 0f;
        currentUndershootPeak = 0f;

        previousAxisDifference = 0f;

        lastCountTime = -Mathf.Infinity;

        wasInContact = false;            
        hasReEntryBeenCounted = false;
    }

    /// <summary>
    /// Reset total distances after logging
    /// </summary>
    public void ResetTotals()
    {
        // Reset all accumulated distances to zero
        totalOvershootDistance = 0f;
        totalUndershootDistance = 0f;
        correctionOvershootDistance = 0f;
        correctionUndershootDistance = 0f;
    }

    #endregion

    #region Detection

    /// <summary>
    /// Detects overshoot and undershoot based on the current and previous controller positions.
    /// Overshoot or undershoot based on the current and previous controller positions relative to the target.
    /// Updates counts and accumulates distances.
    /// </summary>
    /// <param name="isTriggerPressed">Indicates if the trigger button is currently pressed.</param>
    /// <param name="direction">Direction of the current target.</param>
    /// <param name="controllerPosition">Current position of the controller.</param>
    /// <param name="previousControllerPosition">Previous position of the controller.</param>
    /// <param name="targetPosition">Position of the current target.</param>
    /// <param name="isContactingTarget">Indicates if the controller is currently contacting the target.</param>
    /// <param name="targetCollider">Collider of the current target.</param>
    public void DetectOvershootUndershoot(
        bool isTriggerPressed,
        TargetManager.DirectionType direction,
        Vector3 controllerPosition,
        Vector3 previousControllerPosition,
        Vector3 targetPosition,
        bool isContactingTarget,
        Collider targetCollider)
    {
        // Determine the movement axis based on the direction
        Vector3 movementDirection = Vector3.zero;

        // Negative direciton flag for negative axis movements, i.e., left and back of midpoint
        bool isNegativeDirection = false;

        // Set movement axis and determine if direction is negative based on the target direction type
        switch (direction)
        {
            case TargetManager.DirectionType.Right:
            case TargetManager.DirectionType.Left:
                movementDirection = Vector3.right;
                // NegativeDirection for left
                isNegativeDirection = (direction == TargetManager.DirectionType.Left);
                // 1D movement positions along x axis
                previousAxisPosition = previousControllerPosition.x;
                currentAxisPosition = controllerPosition.x;
                targetAxisPosition = targetPosition.x;
                break;

            case TargetManager.DirectionType.Forward:
            case TargetManager.DirectionType.Back:
                movementDirection = Vector3.forward;
                // Negative direction for back
                isNegativeDirection = (direction == TargetManager.DirectionType.Back);
                // 1D movemnt positions along z axis
                previousAxisPosition = previousControllerPosition.z;
                currentAxisPosition = controllerPosition.z;
                targetAxisPosition = targetPosition.z;
                break;

            default:
                Debug.LogWarning("Unsupported DirectionType encountered.");
                return;
        }

        // Calculate the axis difference to the target boundary
        axisDifference = GetAxisDistanceToTargetBoundary(direction, controllerPosition, targetPosition, targetCollider);

        // Initialise time since last overshoot/undershoot count
        float timeSinceLastCount = Time.time - lastCountTime;

        // Store previous overshooting and undershooting states
        bool wasOvershooting = isCurrentlyOvershooting;
        bool wasUndershooting = isCurrentlyUndershooting;

        // Only increment flag if above cooldown duration - control if micromovements logged or not
        bool canIncrementCount = timeSinceLastCount >= cooldownDuration;

        // Handle starting correction pahse and contact transitions for ReEntryNumber
        if (isContactingTarget)
        {
            // First time contacting target
            if (!wasInContact)
            {
                // User has just made contact with the target so chnage flag
                wasInContact = true;

                // Set contact flag to true
                TargetContact();

                // Check if the user has overshot or undershot before
                if ((hasOvershotAtLeastOnce || hasUndershotAtLeastOnce) && !hasReEntryBeenCounted)
                {
                    // Increment re-entry count if overshot or undershot at least once before
                    reEntryNumber++;
                    // Prevent multiple increments without new overshoot/undershoot
                    hasReEntryBeenCounted = true; 
                }
            }
        }
        else
        {
            // User has just left contact with the target
            if (wasInContact)
            {
                wasInContact = false;

                // Start correction phase when the user leaves the target after first contacting it
                if (hasContactedTargetAtLeastOnce && !isInCorrectionPhase)
                {
                    StartCorrectionPhase();
                }
            }
        }

        // Detect overshooting
        if (axisDifference < 0)
        {
            isCurrentlyOvershooting = true;
            isCurrentlyUndershooting = false;

            // Flag for logging undershots after intitial overshot
            hasOvershotAtLeastOnce = true;
            hasUndershotAtLeastOnce = true;

            // Calculate overshoot distance
            overshootDistance = Mathf.Abs(axisDifference);

            // Moving to overshooting
            if (!wasOvershooting && canIncrementCount)
            {
                // Increment overshoot count
                overshotCount++;
                // Update last count time
                lastCountTime = Time.time;
                // Set current peak for totals
                currentOvershootPeak = overshootDistance;
                // Reset re-entry count flag since a new overshoot has occurred
                hasReEntryBeenCounted = false;
            }
            // Whilst overshooting, change peak if it needs changing
            else if (overshootDistance > currentOvershootPeak)
            {
                currentOvershootPeak = overshootDistance;
            }
        }

        // Detect undershooting
        else if (axisDifference > 0)
        {
            isCurrentlyOvershooting = false;
            isCurrentlyUndershooting = true;
            // Calculate undershoot distance
            undershootDistance = axisDifference;

            // Scenario - after overshooting
            if (hasOvershotAtLeastOnce && canIncrementCount)
            {
                if (!wasUndershooting)
                {
                    // Increment undershoot count
                    undershotCount++;
                    // Update last count time
                    lastCountTime = Time.time;
                    // Set current peak undershoot
                    currentUndershootPeak = undershootDistance;

                    // Reset re-entry count flag since a new undershoot has occurred
                    hasReEntryBeenCounted = false;
                }
                else if (undershootDistance > currentUndershootPeak)
                {
                    // Update peak undershoot distance
                    currentUndershootPeak = undershootDistance;
                }
            }

            // Scenario - trigger pressed before contact and before overshooting
            if (isTriggerPressed && !isContactingTarget && !hasOvershotAtLeastOnce)
            {
                if (canIncrementCount)
                {
                    // Increment undershoot count
                    undershotCount++;
                    // Update last count time
                    lastCountTime = Time.time;
                    // Accumulate total undershoot distance
                    totalUndershootDistance += undershootDistance;
                }
            }

            // If a new undershoot has occurred, reset the re-entry count flag
            if (hasOvershotAtLeastOnce)
            {
                hasReEntryBeenCounted = false;
            }
        }

        // On boundary of both, i.e., target
        else
        {
            isCurrentlyOvershooting = false;
            isCurrentlyUndershooting = false;
            overshootDistance = 0f;
            undershootDistance = 0f;
        }

        // Transitioning out of overshooting
        if (wasOvershooting && !isCurrentlyOvershooting)
        {
            // Add furthest overshoot distance of just finsihed overshoot to total
            totalOvershootDistance += currentOvershootPeak;

            // Accumulate distances during correction phase
            if (isInCorrectionPhase)
            {
                correctionOvershootDistance += currentOvershootPeak;
            }

            // Reset the current overshoot peak
            currentOvershootPeak = 0f;
        }

        // Transitioning out of undershooting
        if (wasUndershooting && !isCurrentlyUndershooting)
        {
            // Don't add total if undershooting condition not met
            if (hasOvershotAtLeastOnce)
            {
                // Add furthest undershoot distance of just finsihed undershot to total
                totalUndershootDistance += currentUndershootPeak;

                // Accumulate distances during correction phase
                if (isInCorrectionPhase)
                {
                    correctionUndershootDistance += currentUndershootPeak;
                }

                // Reset the current undershoot peak
                currentUndershootPeak = 0f;
            }
        }

        // Accumulation during correction phase
        if (isInCorrectionPhase)
        {
            // Accumulate total movement distance during correction phase
            Vector3 delta = controllerPosition - previousControllerPosition;
            float distanceAlongAxis = Mathf.Abs(Vector3.Dot(delta, movementDirection.normalized));
            correctionTotalDistance += distanceAlongAxis;

            if (isCurrentlyOvershooting)
            {
                // Accumulate overshoot during correction phase
                correctionOvershootDistance += overshootDistance;
            }
            else if (isCurrentlyUndershooting && hasOvershotAtLeastOnce)
            {
                // Accumulate undershoot during correction phase
                correctionUndershootDistance += undershootDistance;
            }
        }

        // If in correction phase, end correction phase when the trigger is pressed
        if (isTriggerPressed && isInCorrectionPhase)
        {
            EndCorrectionPhase();
        }

        // Update previous axis difference
        previousAxisDifference = axisDifference;
    }

    /// <summary>
    /// Set hasContactedTargetAtLeastOnce to true to track undershooting prior to trigger press
    /// </summary>
    public void TargetContact()
    {
        // Set the flag to indicate that the target has been contacted at least once
        hasContactedTargetAtLeastOnce = true;
    }

    #endregion

    #region Correction Flags

    /// <summary>
    /// Start correction phase when target is first contacted
    /// </summary>
    public void StartCorrectionPhase()
    {
        isInCorrectionPhase = true;
    }

    /// <summary>
    /// End correction phase when trigger is pressed after correction phase started
    /// </summary>
    public void EndCorrectionPhase()
    {
        isInCorrectionPhase = false;
    }

    #endregion

    #region Accumulation

    /// <summary>
    /// Accumulates any pending peak distances for overshooting and undershooting.
    /// Called before resetting or at the end of a trial to ensure all peaks are accounted for.
    /// </summary>
    public void AccumulatePendingPeakDistances()
    {
        // Accumulate any remaining peak overshoot distance
        if (isCurrentlyOvershooting)
        {
            totalOvershootDistance += currentOvershootPeak;
            // Accumulate correction overshoot
            if (isInCorrectionPhase)
            {
                correctionOvershootDistance += currentOvershootPeak;
            }
            // Reset overshootpeak value
            currentOvershootPeak = 0f;
        }

        // Accumulate any remaining peak undershoot distance
        if (isCurrentlyUndershooting)
        {
            // Only accumulate if has overshot
            if (hasOvershotAtLeastOnce)
            {
                totalUndershootDistance += currentUndershootPeak;
                // Accumulate correction undershoot
                if (isInCorrectionPhase)
                {
                    correctionUndershootDistance += currentUndershootPeak;
                }
                // Reset undershootpeak value
                currentUndershootPeak = 0f;
            }
        }
    }

    #endregion

    #region Distance Measuring

    /// <summary>
    /// Calculates the distance along the movement axis from the controller to the target boundary.
    /// Positive values indicate undershooting, negative values indicate overshooting.
    /// </summary>
    /// <param name="direction">Direction of movement.</param>
    /// <param name="controllerPosition">Current controller position.</param>
    /// <param name="targetPosition">Position of the target.</param>
    /// <param name="targetCollider">Collider of the target.</param>
    /// <returns>Axis difference value.</returns>
    public float GetAxisDistanceToTargetBoundary(
        TargetManager.DirectionType direction,
        Vector3 controllerPosition,
        Vector3 targetPosition,
        Collider targetCollider)
    {
        // Determine movement vector
        Vector3 movementDirection = DetermineMovementVector(direction);

        // Get the closest point on the target collider to the controller position
        Vector3 closestPoint = targetCollider.ClosestPoint(controllerPosition);

        // Compute the vector from the controller to the closest point on the collider
        Vector3 vectorToBoundary = closestPoint - controllerPosition;

        // Project this vector onto the movement axis to get the axis difference
        float axisDifference = Vector3.Dot(vectorToBoundary, movementDirection);

        return axisDifference;
    }

    /// <summary>
    /// Calculates distance along movement axis from controller to target midpoint.
    /// Positive values indicate undershooting, negative overshotting.
    /// </summary>
    /// <param name="direction">Direction of movement.</param>
    /// <param name="controllerPosition">Current controller position.</param>
    /// <param name="targetMidPoint">Position of the target midpoint.</param>
    /// <returns></returns>
    public float GetAxisDistanceToTargetMidPoint(
        TargetManager.DirectionType direction,
        Vector3 controllerPosition,
        Vector3 targetMidPoint)
    {
        // Determine movement vector
        Vector3 movementDirection = DetermineMovementVector(direction);

        // Compute the vector from the controller to the midpoint of the target
        Vector3 vectorToMidPoint = targetMidPoint - controllerPosition;

        // Project this vector onto the movement axis to get the axis difference
        float axisDifference = Vector3.Dot(vectorToMidPoint, movementDirection);

        return axisDifference;
    }

    /// <summary>
    /// Get distance to target boundary as a positive float for logging.
    /// </summary>
    /// <param name="direction">Direction of movement.</param>
    /// <param name="controllerPosition">Current position of the controller.</param>
    /// <param name="targetPosition">Position of the target.</param>
    /// <param name="targetCollider">The target Collider.</param>
    /// <returns>Float absolute distance from the controller to the closest point on the collider.</returns>
    public float GetDistanceToTargetCollider(
        TargetManager.DirectionType direction,
        Vector3 controllerPosition,
        Vector3 targetPosition,
        Collider targetCollider)
    {
        // Calculate the axis difference and return its absolute value
        float axisDifference = GetAxisDistanceToTargetBoundary(direction, controllerPosition, targetPosition, targetCollider);
        return Mathf.Abs(axisDifference);
    }

    /// <summary>
    /// Get distance to target midpoint as a positive float for logging.
    /// </summary>
    /// <param name="direction">Direction of movement.</param>
    /// <param name="controllerPosition">Current position of the controller.</param>
    /// <param name="targetPosition">Position of the target midpoint.</param>
    /// <returns>Float absolute distance from the controller to the midpoint of the target.</returns>
    public float GetDistanceToTargetMidpoint(
        TargetManager.DirectionType direction,
        Vector3 controllerPosition,
        Vector3 targetPosition)
    {
        // Calculate the axis difference and return its absolute value
        float axisDifference = GetAxisDistanceToTargetMidPoint(direction, controllerPosition, targetPosition);
        return Mathf.Abs(axisDifference);
    }

    #endregion

    #region Direction

    /// <summary>
    /// Determine movement vector based on direction.
    /// </summary>
    /// <param name="direction">Direction of movement.</param>
    /// <returns>Direciton of target.</returns>
    public Vector3 DetermineMovementVector(TargetManager.DirectionType direction)
    {
        // Determine the movement direction vector
        Vector3 movementDirection = Vector3.zero;

        switch (direction)
        {
            case TargetManager.DirectionType.Right:
                movementDirection = Vector3.right;
                break;
            case TargetManager.DirectionType.Left:
                movementDirection = Vector3.left;
                break;
            case TargetManager.DirectionType.Forward:
                movementDirection = Vector3.forward;
                break;
            case TargetManager.DirectionType.Back:
                movementDirection = Vector3.back;
                break;
            default:
                Debug.LogWarning("Unsupported DirectionType encountered in GetAxisDistanceToTargetBoundary.");
                break;
        }

        return movementDirection;
    }

    #endregion

    #region Logging

    /// <summary>
    /// Returns undershotCount, overshotCount, undershootDistance, overshootDistance, totalOvershootDistance, totalUndershootDistance for logging
    /// Ends correction phase
    /// </summary>
    /// <returns>OverUnderShootData struct</returns>
    public OverUnderShootData GetOvershootUndershootData()
    {
        // Create and populate OverUnderShootData struct to return for logging
        OverUnderShootData data = new OverUnderShootData
        {
            OvershotCount = overshotCount,
            UndershotCount = undershotCount,
            ReEntryNumber = reEntryNumber,
            OvershootDistance = overshootDistance,
            UndershootDistance = undershootDistance,
            TotalOvershootDistance = totalOvershootDistance,
            TotalUndershootDistance = totalUndershootDistance,
            CorrectionOvershootDistance = correctionOvershootDistance,
            CorrectionUndershootDistance = correctionUndershootDistance,
            CorrectionTotalDistance = correctionTotalDistance
        };

        // Return data struct
        return data;
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Determine if currently overshooting
    /// </summary>
    /// <returns>Bool true if currently overshooting</returns>
    public bool IsCurrentlyOvershooting
    {
        get { return isCurrentlyOvershooting; }
    }

    /// <summary>
    /// Determine if currently undershooting
    /// </summary>
    /// <returns>True if currently overshooting, false otherwise</returns>
    public bool IsCurrentlyUndershooting
    {
        get { return isCurrentlyUndershooting; }
    }

    /// <summary>
    /// Returns current overshoot distance
    /// </summary>
    /// <returns>True if currently undershooting, false otherwise</returns>
    public float CurrentOvershootDistance
    {
        get { return overshootDistance; }
    }

    /// <summary>
    /// Returns current undershoot distance
    /// </summary>
    /// <returns>Float current undershoot distance</returns>
    public float CurrentUndershootDistance
    {
        get { return undershootDistance; }
    }

    /// <summary>
    /// Returns current axis differecnce
    /// </summary>
    /// <returns>Float axis difference along the movement axis between controller and target</returns>
    public float GetAxisDifference
    {
        get { return axisDifference; }
    }

    #endregion
}
