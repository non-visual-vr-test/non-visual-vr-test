using UnityEngine;

public struct LogData
{
    // Basic Information
    public string Timestamp;                    // Timestamp of the log event
    public int ParticipantNumber;               // Unique identifier for the participant
    public int BlockNumber;                     // Identifier for the block of trials
    public string BlockLetter;                  // Identifier of the specific block according to AxisType and GrowthPattern
    public float ControllerWidth;               // Width of the controlleranchor used during the trial
    public float DistanceToTargetCollider;      // Distance from the controller to the target's collider
    public float DistanceToTargetMidpoint;      // Distance from the controller to the midpoint of the target
    public float TimeTaken;                     // Time taken to complete the trial
    public int IsHit;                           // Whether the target was successfully hit (1 = hit, 0 = miss)
    public float TotalDwellTime;                // Total time spent dwelling on the target

    // Overshoot and Undershoot Data
    public int OvershotCount;                   // Number of times the controller overshot the target
    public int UndershotCount;                  // Number of times the controller undershot the target
    public int ReEntryNumber;                   // Number of times the controller re-entered the target
    public float OvershootDistance;             // Distance of overshoot beyond the target
    public float UndershootDistance;            // Distance of undershoot before reaching the target
    public float TotalOvershootDistance;        // Total distance accumulated from overshooting
    public float TotalUndershootDistance;       // Total distance accumulated from undershooting
    public float CorrectionDistance;            // Total distance moved to correct overshoots and undershoots

    // Phase and IDs
    public int Phase;                           // Current phase of the experiment
    public int DistanceID;                      // Identifier for the distance condition
    public int DirectionID;                     // Identifier for the direction condition

    // Controller state at end
    public Vector3 ControllerPosition;          // Position of the controller at the end of the trial
    public Quaternion ControllerRotation;       // Rotation of the controller at the end of the trial
    public float MovementAngle;                 // The angle in degrees between the movement vector and the target vector

    // Controller state at start
    public Vector3 StartControllerPosition;     // Position of the controller at the start of the trial
    public Quaternion StartControllerRotation;  // Rotation of the controller at the stqrt of the trial

    // Controller position and rotation differences
    public float ControllerPositionXDifference; // Difference in X position from start to end
    public float ControllerPositionYDifference; // Difference in Y position from start to end
    public float ControllerPositionZDifference; // Difference in Z position from start to end
    public float ControllerRotationXDifference; // Difference in X rotation from start to end
    public float ControllerRotationYDifference; // Difference in Y rotation from start to end
    public float ControllerRotationZDifference; // Difference in Z rotation from start to end

    // Target Midpoint
    public Vector3 TargetMidpoint;              // Midpoint of the target used for calculations

    // Trial Numbers    
    public int SetNumber;                       // Set number within the block
    public int TrialNumberInSet;                // Trial number within the current set
    public int TrialNumberInBlock;              // Trial number within the current block
    public int TrialNumberByID;                 // Unique identifier for the trial across all blocks and sets
    public int TrainingSetNumber;               // Training set identifier
    public int TestSetNumber;                   // Test set identifier

    // Haptic Feedback
    public int HapticFeedbackMethod;            // Method of haptic feedback used
    public int HapticFeedbackStrength;          // Strength level of the haptic feedback

    // Fitts' Law Data
    public FittsLawData FittsData;              // Fitts' Law related data

    // Movement Distances
    public float CurrentAxisDistance;                           // Current distance along the movement axis
    public float DifferenceToAmplitude;                         // Difference between axis movement and amplitude
    public float CurrentAxisNetDistance;                        // Current net distance along the movement axis between start and end (not accounting for overshots)
    public float AggregateMovementDistanceAlongAxisForSet;      // Total movement distance along the axis for set
    public float AggregateMovementDistanceAlongAllAxesForSet;   // Total movement distance across all axes for set
    public float AggregateMovementDistanceAlongAxisForBlock;    // Total movement distance along the axis for set
    public float AggregateMovementDistanceAlongAllAxesForBlock; // Total movement distance across all axes for set
    public float AggregateNetMovementDistanceAlongAxisForSet;   // Total straight-line along axis for set
    public float AggregateNetMovementDistanceAlongAxisForBlock; // Total straight-line along axis for block
    public float PathCurvature;                                 // Measure of how much the path deviates from a straight line (straight line between targets, taken from amplitude, = 1. Values greater than 1 indicate deviations)

    // Euclidean Metrics
    public float EuclideanDistance;             // Straight-line distance of controller movment from start to end position
    public float EuclideanDistanceForSet;       // Staright-line for set
    public float EuclideanDistanceForBlock;     // Straight-line for block
    public float EuclideanDeviation;            // Deviation value from the ideal straight-line path
    public float EuclideanDeviationForSet;      // Deviation for set
    public float EuclideanDeviationForBlock;    // Deviation for block

    // Speed Metrics
    public float AverageMovementSpeed;          // Average speed of movement during the trial
    public float MaxSpeed;                      // Maximum speed achieved during the trial
    public float MinSpeed;                      // Minimum speed achieved during the trial

    // Movement phases  
    public float ReactionTime;                  // Time between target appearance and movement initiation in the target's direction (e.g., positive x-axis for a right target)
    public float BallisticTime;                 // Time spent in the ballistic (initial rapid) phase of movement
    public float CorrectionTime;                // Time spent in the correction phase of movement

    // Moveemnt phases distances
    public float BallisticDistanceAlongAxis;        // Distance covered during ballistic phase along the axis
    public float BallisticDistanceAlongAllAxes;     // Distance covered during ballistic phase along all axe  s
    public float CorrectionDistanceAlongAxis;       // Distance covered during correction phase along the axis
    public float CorrectionDistanceAlongAllAxes;    // Distance covered during correction phase along all axes
}

public struct OverUnderShootData
{
    public int OvershotCount;                   // Number of times the target was overshot
    public int UndershotCount;                  // Number of times the target was undershot
    public int ReEntryNumber;                   // Number of re-entries into the target area
    public float OvershootDistance;             // Distance accumulated from single overshooting of the target
    public float UndershootDistance;            // Distance accumulated from single undershooting of the target
    public float TotalOvershootDistance;        // Total overshoot distance during the trial
    public float TotalUndershootDistance;       // Total undershot distance during the trial
    public float CorrectionOvershootDistance;   // Distance covered to correct overshoots
    public float CorrectionUndershootDistance;  // Distance covered to correct undershoots
    public float CorrectionTotalDistance;       // Accumulated correction distance - all distance after making contact with target for first time
}

public struct FittsLawData
{
    // Per trial standard Fitts Law measures
    public float MovementTimeMs;                            // Time taken to complete the movement (in milliseconds)
    public float TargetWidth;                               // Width of the target
    public float DistanceBetweenTargets;                    // Distance between targets for the trial
    public float ID;                                        // Index of Difficulty (ID) for Fitts' Law
    public int TaskPrecision;                               // Precision level required for the task
    public float Throughput;                                // Throughput calculated for Fitts' Law
    public int HitMissValue;                                // Indicator of hit or miss for the target (1 = hit, 0 = miss)
    public float ControllerWidth;                           // Width of the controller used in the trial

    // TODO: CHECK THESE ALL MATCH DISTANCES IN ABOVE LOGGING FROM TARGETMANAGER.cs

    // Per trial distances
    public float AggregateMovementDistanceAlongAllAxes;     // Total movement distance across all axes for set
    public int MovementAxis;                                // Axis along which movement occurred

    // Effective measures - for set
    public float EffectiveISOWidth;                         // Effective width following ISO calculation for set
    public float EffectivePerpendicularDeviation;           // Effective perpendicular deviation from the target line for set
    public float EffectiveDistance;                         // Effective movement distance
    public float EffectiveISOIDe;                           // Effective Index of Difficulty (IDe) following ISO calculation
    public float EffectiveISOThroughput;                    // Effective throughput based on IDe
    public float EffectiveWidthPerpendicularVariability;    // Variability in effective width perpendicular to the target
    public float EffectiveDistanceOvershootUndershoot;      // Effective distance considering overshoot and undershoot
    
    // Set distances
    public float TotalPathLength;                               // Total length of the movement path
    public float EuclideanDeviation;                            // Deviation from the ideal Euclidean path
    public float CurrentAxisDistance;                           // Total movement along movement axis
    public float EffectiveDistanceOvershootUndershootForSet;    // Effective distance overshooting and undershooting in set
    public float EffectiveWidthPerpendicularVariabilityForSet;  // Variability in effective width perpendicular to the target for set
    public float AggregateMovementDistanceAlongAxisForSet;      // Total movement along the axis for set
    public float AggregateMovementDistanceAlongAllAxesForSet;   // Total movement along all axes for set
    public float AmplitudeAggregateForSet;                      // Aggregate amplitude - straight-line distance between targets for set
    public float AmplitudeAggregateForBlock;                    // Aggregate amplitude - straight-line distance between targets for block

    // Standard Fitts measure for set
    public float ThroughputForSet;                          // Throughput calculated for the entire set
    
    // Standard Fitts measures for block
    public float ThroughputForBlock;                        // Throughput calculated for the entire block
    public float IDForBlock;                                // Index of Difficulty for the entire block

    // Effective measures - for block
    public float EffectiveISOWidthForBlock;                        // Effective width following ISO calculation for block
    public float EffectivePerpendicularDeviationForBlock;          // Effective perpendicular deviation from the target line for block
    public float EffectiveISODistanceForBlock;                     // Effective movement distance for block
    public float EffectiveISOIDForBlock;                           // Effective Index of Difficulty (IDe) following ISO calculation for block
    public float EffectiveISOThroughputForBlock;                   // Effective throughput based on IDe for block
    public float EffectiveWidthPerpendicularVariabilityForBlock;   // Variability in effective width perpendicular to the target for block
    public float EffectiveDistanceOvershootUndershootForBlock;     // Effective distance considering overshoot and undershoot
    public float AggregateMovementDistanceAlongAxisForBlock;       // Total movement distance along the axis for block
    public float AggregateMovementDistanceAlongAllAxesForBlock;    // Total movement distance across all axes for block
}