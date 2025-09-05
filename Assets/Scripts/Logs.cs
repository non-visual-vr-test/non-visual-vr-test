using System.IO;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using static TargetManager;

public class Logs : MonoBehaviour
{
    // Class for time management
    private TimeManagement timeManagement;

    // Participant number, block number, and if block a repetition for each participant - set in Inspector
    [Header("Participant and Block")]
    [SerializeField] private int participantNumber = 1;
    [SerializeField] private int blockNumber = 1;
    [SerializeField] private bool blockRepetition = false;

    // Flag to log to master files or not - all participant data and all movement data for every loop
    [Header("Logging Options")]
    [SerializeField] private bool logToMasterFile = false;

    // Timestamp for log files
    string timestamp;

    // File path
    private string logsFolderPath;                          // Folder for all files

    // CSV files
    private string individualParticipantPath;               // Path for individual participant data
    private string allDataPath;                             // Path for master data CSV file of all participants
    private string individualMovementFilePath;              // Path for movement data for each loop
    private string allMovementFilePath;                     // Path for master movement data - all participants together

    // Logging flag to control logging behavior
    private bool isTrackingEnabled = false;                 // Controls whether logging should occur - only after first trigger press until last target trigger press - no logging when paused or moving towards first unlogged target

    // Lists for storing controller data over time
    private List<Vector3> controllerPositions = new List<Vector3>();                // List to store controller position data
    private List<float> controllerTimestamps = new List<float>();                   // List to store timestamps of controller positions
    private List<Quaternion> controllerRotations = new List<Quaternion>();          // List to store controller rotation data
    private List<bool> triggerStates = new List<bool>();                            // List to store trigger state

    // Controller differences in position and rotation
    [HideInInspector] public float ControllerPositionXDifference;
    [HideInInspector] public float ControllerPositionYDifference;
    [HideInInspector] public float ControllerPositionZDifference;
    [HideInInspector] public float ControllerRotationXDifference;
    [HideInInspector] public float ControllerRotationYDifference;
    [HideInInspector] public float ControllerRotationZDifference;

    // Speed calculation
    private Vector3 lastPosition = Vector3.zero;            // Last recorded position of the controller
    private float lastLogTime = 0f;                         // Last recorded time for speed calculation
    private float speedThreshold = 0.01f;                   // Minimal speed threshold for logging - catch first frame being 0
    private int maxSpeedIndex = 0;                          // Index of max speed value
    private float maxSpeed = 0f;                            // Max speed value

    // Convert hit/miss & trigger to ints for logging
    private int hitMissValue = 0;
    private int triggerPressed = 0;

    // Lists for average movement speed calculation
    private List<Vector3> positionsSinceLastSelection = new List<Vector3>();            // Positions since last selection
    private List<float> timesSinceLastSelection = new List<float>();                    // Times since last selection
    private List<float> speedsSinceLastSelection = new List<float>();                   // Speeds since last selection
    private float lastSelectionTime = 0f;                                               // Last selection time

    // Low-pass filter for speed
    private float filteredSpeed = 0f;                   // Filtered speed value
    private const float speedAlpha = 0.2f;              // Low-pass filter alpha value for filtered speed control

    // Store current haptic strength
    private int currentHapticStrength = 0;

    // Logging target corner coordinates
    private Vector3[] currentTargetCorners = new Vector3[8];

    #region Unity Lifecycle Method

    /// <summary>
    /// Assign class. Set up paths and lists
    /// </summary>
    void Awake()
    {
        if (timeManagement == null) timeManagement = FindObjectOfType<TimeManagement>();

        // Initialise timestamp and file paths
        InitialiseTimestamp();
        InitialiseFilePaths();

        // Write headers for CSV files
        CSVHeadersFormatting();

        // Ensure tracking is disabled at the start
        isTrackingEnabled = false;
    }

    #endregion

    #region Initialisation Methods

    /// <summary>
    /// Date and time to maintain persistence and order of files
    /// </summary>
    private void InitialiseTimestamp()
    {
        timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    /// <summary>
    /// Directory and file path intialisation
    /// </summary>
    private void InitialiseFilePaths()
    {
        // Persistent path for data directory
        logsFolderPath = Application.persistentDataPath;

        // Check if the logsFolderPath directory exists and create it if it doesn't
        if (!Directory.Exists(logsFolderPath)) Directory.CreateDirectory(logsFolderPath);

        // CSV file paths for storing data
        individualParticipantPath = Path.Combine(logsFolderPath, $"individual_data_Participant_{participantNumber}_date_{timestamp}.csv");
        allDataPath = Path.Combine(logsFolderPath, "allData.csv");
        individualMovementFilePath = Path.Combine(logsFolderPath, $"individual_Movement_{participantNumber}_date_{timestamp}.csv");
        allMovementFilePath = Path.Combine(logsFolderPath, "allMovementData.csv");
    }

    #endregion

    #region Public Properties For Logging

    // Public properties for logging to movement files - set by targetmanager
    public int MovementAxis { get; set; }
    public string BlockLetter { get; set; }
    public int SetNumber { get; set; }
    public int TrainingSetNumber { get; set; }
    public int TestSetNumber { get; set; }
    public int TrialNumberInSet { get; set; }
    public int TrialNumberInBlock { get; set; }
    public int TrialNumberByID { get; set; }
    public int HapticFeedbackMethod { get; set; }

    #endregion

    #region Header Formatting

    /// <summary>
    /// Write headers to CSV files
    /// </summary>
    private void CSVHeadersFormatting()
    {
        try
        {
            // Individual Participant header stringbuilder
            StringBuilder individualHeader = new StringBuilder();

            // General info - date, participant no., phase, axis, distance etc.
            individualHeader.Append("Date_and_time," +
                "Participant_number," +
                "Phase(0=training_1=testing)," +
                "Movement_axis(0_horizontal_1_vertical)," +
                "DistanceID(0=training_1=short_2=medium_3=long)," +
                "DirectionID(0=forward_1=right_2=back_3=left)," +
                "Target_width(m)," +
                "Controller_width(m)," +
                "Time(s)_from_previous_target_to_this," +
                "Hit_miss(0=miss_1=hit),");
            
            // Block and set numbers
            individualHeader.Append("Block_number_for_user," +
                "Block_letter," +
                "Block_repetition(0_first_1_repeition)," +
                "Set_number_in_block," +
                "Training_set_number," +
                "Testing_set_number," +
                "Trial_number_in_set," +
                "Trial_number_in_block," +
                "Trial_number_in_block_grouped_by_ID,");
            
            // Haptic feedback
            individualHeader.Append("Haptic_feedback_method(0_Linear_1_Quadratic_2_Stair_3_Pulse)," +
                "Haptic_feedback_strength_on_first_loop_trigger_press,");
            
            // Closest distances to target
            individualHeader.Append("Distance_to_closest_point_on_target(controller_anchor_to_closest_target_collider_point)," +
                "Distance_to_target_midpoint(controller_anchor_to_target_midpoint_along_movement_axis),");
            
            // Target midpoint along axes
            individualHeader.Append("Target_midpoint_along_x_axis_1D," +
                "Target_midpoint_along_y_axis_1D," +
                "Target_midpoint_along_z_axis_1D,");
            
            // Dwell and under/overshooting
            individualHeader.Append("Dwell_time(s)(total_time_in_contact_with_target)," +
                "Total_undershot_to_nearest_target_point_count," +
                "Total_overshot_to_nearest_target_point_count," +
                "Target_re_entry_number," +
                "On_trigger_undershoot_distance_to_nearest_target_point(m)," +
                "On_trigger_overshoot_distance_to_nearest_target_point(m)," +
                "Total_undershoot_distance_to_nearest_target_point(m)," +
                "Total_overshoot_distance_to_nearest_target_point(m)," +
                "Post_contact_correction_distance(m)(distance_moved_along_axis_post_target_contact_after_first_leaving_contact_until_trigger_press),");
            
            // Controller position and rotation states
            individualHeader.Append("Start_controller_position_x_axis," +
                "Start_controller_position_y_axis," +
                "Start_controller_position_z_axis," +
                "Start_controller_rotation_x_axis," +
                "Start_controller_rotation_y_axis," +
                "Start_controller_rotation_z_axis," +
                "End_controller_position_x_axis," +
                "End_controller_position_y_axis," +
                "End_controller_position_z_axis," +
                "End_controller_rotation_x_axis," +
                "End_controller_rotation_y_axis," +
                "End_controller_rotation_z_axis," +
                "Controller_position_x_axis_difference," +
                "Controller_position_y_axis_difference," +
                "Controller_position_z_axis_difference," +
                "Controller_rotation_x_axis_difference," +
                "Controller_rotation_y_axis_difference," +
                "Controller_rotation_z_axis_difference," +
                "Movement_angle(3D_degrees_between_movement_vector_and_target_vector),");
            
            // Fitts Law
            individualHeader.Append("fitts_law_movement_time(ms)," +
                "fitts_law_throughput(bits/s)," +
                "fitts_law_throughput_for_set(bits/s)," +
                "fitts_law_throughput_for_block(bits/s)," +
                "fitts_law__index_of_difficulty_ID(bits)," +
                "fitts_law__ID_for_block(bits)," +
                "fitts_law__precision_level(high_1_medium_2_low_3_verylow_4)," +
                "fitts_law_effective_width_ISO_calculation(m)," +
                "fitts_law_effective_width_for_block_ISO_calculation(m)," +
                "fitts_law_effective_perpendicular_deviation(m)," +
                "fitts_law_effective_perpendicular_deviation_for_block(m)," +
                "fitts_law_effective_distance_ISO_calculation(m)," +
                "fitts_law_effective_distance_for_block_ISO_calculation(m)," +
                "fitts_law_effective_ID_ISO_calculation(bits)," +
                "fitts_law_effective_ID_for_block_ISO_calculation(bits)," +
                "fitts_law_effective_throughput_ISO_calculation(bits/s)," +
                "fitts_law_effective_throughput_for_block_ISO_calculation(bits/s)," +
                "fitts_law_effective_width_perpendicular_variability(m)," +
                "fitts_law_effective_width_perpendicular_variability_aggregate_for_set(m)," +
                "fitts_law_effective_width_perpendicular_variability_aggregate_for_block(m)," +
                "fitts_law_effective_distance_overshoot_undershoot(negative=overshoot_positive=undershoot)," +
                "fitts_law_effective_distance_overshoot_undershoot_aggregate_for_set(negative=overshoot_positive=undershoot)," +
                "fitts_law_effective_distance_overshoot_undershoot_aggregate_for_block(negative=overshoot_positive=undershoot),");
            
            // Movement axes
            individualHeader.Append("fitts_law_amplitude(m)," +
                "Euclidean_distance_along_axis_net_displacement_movement_distance_along_movement_axis(m)(straight_line_distance_along_the_axis_doesnt_account_for_overshot_correcting)," +
                "Movement_along_movement_axis(m)," +
                "Movement_difference_along_axis_over_or_under_amplitude(negative_less_than_amplitude_positive_over_amplitude)," +
                "AmplitudeAggregateForSet(m)," +
                "Euclidean_distance_along_axis_net_displacement_aggregate_movement_distance_along_movement_axis_for_set(m)," +
                "Aggregate_movement_distance_along_movement_axis_for_set(m)," +
                "AmplitudeAggregateForBlock(m)," +
                "Euclidean_distance_along_axis_net_displacement_aggregate_movement_distance_along_movement_axis_for_block(m)," +
                "Aggregate_movement_distance_along_movement_axis_for_block(m)," +
                "Movement_distance_along_all_axes(Path_length)(m)," +
                "Aggregate_movement_distance_along_all_axes_for_set(m)," +
                "Aggregate_movement_distance_along_all_axes_for_block(m)," +
                "Euclidean_distance(straight_line_distance_between_start_and_end_positions(m))," +
                "Euclidean_distance_for_set(straight_line_distance_between_start_and_end_positions(m))," +
                "Euclidean_distance_for_block(straight_line_distance_between_start_and_end_positions(m))," +
                "Euclidean_deviation(deviation_from_ideal_straight_line_of_actual_movement(m))," +
                "Euclidean_deviation_for_set(deviation_from_ideal_straight_line_of_actual_movement(m))," +
                "Euclidean_deviation_for_block(deviation_from_ideal_straight_line_of_actual_movement(m))," +
                "Path_curvature(deviation_from_amplitude_straight_line_of_1),");
            
            // Speeds and times
            individualHeader.Append("Average_movement_speed(m/s)," +
                "Max_speed(m/s)," +
                "Min_speed(m/s)," +
                "Reaction_time(s)," +
                "Ballistic_time(s)," +
                "Correction_time(s),");
            
            // Balistic and correction distances
            individualHeader.Append("Ballistic_distance_along_movement_axis," +
                "Ballistic_distance_along_all_axes," +
                "Correction_distance_along_movement_axis," +
                "Correction_distance_along_all_axes,");
            
            // Target coreners
            individualHeader.Append("Target_corner1_X," + "Target_corner1_Y," + "Target_corner1_Z," +
                "Target_corner2_X," + "Target_corner2_Y," + "Target_corner2_Z," +
                "Target_corner3_X," + "Target_corner3_Y," + "Target_corner3_Z," +
                "Target_corner4_X," + "Target_corner4_Y," + "Target_corner4_Z," +
                "Target_corner5_X," + "Target_corner5_Y," + "Target_corner5_Z," +
                "Target_corner6_X," + "Target_corner6_Y," + "Target_corner6_Z," +
                "Target_corner7_X," + "Target_corner7_Y," + "Target_corner7_Z," +
                "Target_corner8_X," + "Target_corner8_Y," + "Target_corner8_Z");

            // Convert to string for writing
            string headerLine = individualHeader.ToString();

            // Write CSV headers for individual participant data
            WriteCSVHeader(individualParticipantPath, headerLine);

            // Only write headers for the allData file if it doesn't exist
            if (!File.Exists(allDataPath))
            {
                WriteCSVHeader(allDataPath, headerLine);
            }

            // Individual participant movement header stringbuilder
            StringBuilder movementHeader = new StringBuilder();
            
            // General info
            movementHeader.Append("Date_and_time," +
                "Participant_number," +
                "Block," +
                "ControllerWidth," +
                "TargetWidth," +
                "Phase(0=training_1=testing)," +
                "DistanceID(0=training_1=short_2=med_3=long)," +
                "DirectionID(0=forward_1=right_2=back_3=left),");

            // General info - cached variables
            movementHeader.Append("Movement_axis(0_horizontal_1_vertical)," +
                "Block_letter," +
                "Block_repetition(0_first_1_repeition)," +
                "Set_number_in_block," +
                "Training_set_number," +
                "Testing_set_number," +
                "Trial_number_in_set," +
                "Trial_number_in_block," +
                "Trial_number_in_block_grouped_by_ID," +
                "Haptic_feedback_method(0_Linear_1_Quadratic_2_Stair_3_Pulse),");

            // Controller positions
            movementHeader.Append("PositionX," +
                "PositionY," +
                "PositionZ," +
                "RotationX," +
                "RotationY," +
                "RotationZ," +
                "RotationW,");
            
            // Trigger press and haptic strength
            movementHeader.Append("TriggerPressed(1=yes_0=no)," +
                "HapticStrength,");
            
            // Time and speed
            movementHeader.Append("TimeSinceStart," +
                "rawSpeed," +
                "FilteredSpeed,");
            
            // Target coreners
            movementHeader.Append("Target_corner1_X," + "Target_corner1_Y," + "Target_corner1_Z," +
                "Target_corner2_X," + "Target_corner2_Y," + "Target_corner2_Z," +
                "Target_corner3_X," + "Target_corner3_Y," + "Target_corner3_Z," +
                "Target_corner4_X," + "Target_corner4_Y," + "Target_corner4_Z," +
                "Target_corner5_X," + "Target_corner5_Y," + "Target_corner5_Z," +
                "Target_corner6_X," + "Target_corner6_Y," + "Target_corner6_Z," +
                "Target_corner7_X," + "Target_corner7_Y," + "Target_corner7_Z," +
                "Target_corner8_X," + "Target_corner8_Y," + "Target_corner8_Z");

            // Convert to string for writing
            string movementHeaderLine = movementHeader.ToString();

            // Write CSV headers for individual participant movements every loop
            WriteCSVHeader(individualMovementFilePath, movementHeaderLine);

            // Write headers for allMovementFilePath if it doesn't exist
            if (!File.Exists(allMovementFilePath))
            {
                WriteCSVHeader(allMovementFilePath, movementHeaderLine);
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"Logs: Failed to write CSV headers. Exception: {ex.Message}");
        }
    }

    #endregion

    #region Writers

    /// <summary>
    /// Write the header line to CSV files
    /// </summary>
    /// <param name="filePath">Path of the file to write the header to</param>
    /// <param name="header">Header string to write</param>
    private void WriteCSVHeader(string filePath, string header)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                writer.WriteLine(header);
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"Logs: Failed to write CSV header to {filePath}. Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Write a line of data to a CSV file
    /// </summary>
    /// <param name="filePath">Path of the file to write the data to</param>
    /// <param name="dataLine">Data line string to write</param>
    private void WriteDataLine(string filePath, string dataLine)
    {
        try
        {
            using (StreamWriter writer = new StreamWriter(filePath, append: true))
            {
                writer.WriteLine(dataLine);
            }
        }
        catch (IOException ex)
        {
            Debug.LogError($"Logs: Failed to write data line to {filePath}. Exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Logs data for each participant, including participant data, fitts law data, and controller locations at target selcetion points
    /// </summary>
    /// <param name="data">Log data to write to the CSV</param>
    public void CSVDataWriter(LogData data)
    {
        // Format data into a CSV string
        StringBuilder dataLineBuilder = new StringBuilder();

        // General info - date, participant no., phase, axis, distance etc.
        dataLineBuilder.Append($"{data.Timestamp}," +
            $"{data.ParticipantNumber}," +
            $"{data.Phase}," +
            $"{data.FittsData.MovementAxis}," +
            $"{data.DistanceID}," +
            $"{data.DirectionID}," +
            $"{data.FittsData.TargetWidth}," +
            $"{data.ControllerWidth}," +
            $"{data.TimeTaken}," +
            $"{data.IsHit},");

        // Block and set numbers
        dataLineBuilder.Append($"{data.BlockNumber}," +
           $"{data.BlockLetter}," +
           $"{(blockRepetition ? "1" : "0")}," +
           $"{data.SetNumber}," +
           $"{data.TrainingSetNumber}," +
           $"{data.TestSetNumber}," +
           $"{data.TrialNumberInSet}," +
           $"{data.TrialNumberInBlock}," +
           $"{data.TrialNumberByID},");
        
        // Haptic feedback
        dataLineBuilder.Append($"{data.HapticFeedbackMethod}," +
            $"{data.HapticFeedbackStrength},");
        
        // Closest distances to target
        dataLineBuilder.Append($"{data.DistanceToTargetCollider}," +
            $"{data.DistanceToTargetMidpoint},");
        
        // Target midpoint along axes
        dataLineBuilder.Append($"{data.TargetMidpoint.x}," +
            $"{data.TargetMidpoint.y}," +
            $"{data.TargetMidpoint.z},");

        // Dwell and under/overshooting
        dataLineBuilder.Append($"{data.TotalDwellTime}," +
            $"{data.UndershotCount}," +
            $"{data.OvershotCount}," +
            $"{data.ReEntryNumber}," +
            $"{data.UndershootDistance}," +
            $"{data.OvershootDistance}," +
            $"{data.TotalUndershootDistance}," +
            $"{data.TotalOvershootDistance}," +
            $"{data.CorrectionDistance},");
        
        // Controller position and rotation states
        dataLineBuilder.Append($"{data.StartControllerPosition.x}," +
            $"{data.StartControllerPosition.y}," +
            $"{data.StartControllerPosition.z}," +
            $"{data.StartControllerRotation.x}," +
            $"{data.StartControllerRotation.y}," +
            $"{data.StartControllerRotation.z}," +
            $"{data.ControllerPosition.x}," +
            $"{data.ControllerPosition.y}," +
            $"{data.ControllerPosition.z}," +
            $"{data.ControllerRotation.x}," +
            $"{data.ControllerRotation.y}," +
            $"{data.ControllerRotation.z}," +
            $"{data.ControllerPositionXDifference}," +
            $"{data.ControllerPositionYDifference}," +
            $"{data.ControllerPositionZDifference}," +
            $"{data.ControllerRotationXDifference}," +
            $"{data.ControllerRotationYDifference}," +
            $"{data.ControllerRotationZDifference}," +
            $"{data.MovementAngle},");
        
        // Fitts Law
        dataLineBuilder.Append($"{data.FittsData.MovementTimeMs}," +
            $"{data.FittsData.Throughput}," +
            $"{data.FittsData.ThroughputForSet}," +
            $"{data.FittsData.ThroughputForBlock}," +
            $"{data.FittsData.ID}," +
            $"{data.FittsData.IDForBlock}," +
            $"{data.FittsData.TaskPrecision}," +
            $"{data.FittsData.EffectiveISOWidth}," +
            $"{data.FittsData.EffectiveISOWidthForBlock}," +
            $"{data.FittsData.EffectivePerpendicularDeviation}," +
            $"{data.FittsData.EffectivePerpendicularDeviationForBlock}," +
            $"{data.FittsData.EffectiveDistance}," +
            $"{data.FittsData.EffectiveISODistanceForBlock}," +
            $"{data.FittsData.EffectiveISOIDe}," +
            $"{data.FittsData.EffectiveISOIDForBlock}," +
            $"{data.FittsData.EffectiveISOThroughput}," +
            $"{data.FittsData.EffectiveISOThroughputForBlock}," +
            $"{data.FittsData.EffectiveWidthPerpendicularVariability}," +
            $"{data.FittsData.EffectiveWidthPerpendicularVariabilityForSet}," +
            $"{data.FittsData.EffectiveWidthPerpendicularVariabilityForBlock}," +
            $"{data.FittsData.EffectiveDistanceOvershootUndershoot}," +
            $"{data.FittsData.EffectiveDistanceOvershootUndershootForSet}," +
            $"{data.FittsData.EffectiveDistanceOvershootUndershootForBlock},");

        // Movement axes
        dataLineBuilder.Append($"{data.FittsData.DistanceBetweenTargets}," +
            $"{data.CurrentAxisNetDistance}," +
            $"{data.CurrentAxisDistance}," +
            $"{data.DifferenceToAmplitude}," +
            $"{data.FittsData.AmplitudeAggregateForSet}," +
            $"{data.AggregateNetMovementDistanceAlongAxisForSet}," +
            $"{data.AggregateMovementDistanceAlongAxisForSet}," +
            $"{data.FittsData.AmplitudeAggregateForBlock}," +
            $"{data.AggregateNetMovementDistanceAlongAxisForBlock}," +
            $"{data.AggregateMovementDistanceAlongAxisForBlock}," +
            $"{data.FittsData.TotalPathLength}," +
            $"{data.AggregateMovementDistanceAlongAllAxesForSet}," +
            $"{data.AggregateMovementDistanceAlongAllAxesForBlock}," +
            $"{data.EuclideanDistance}," +
            $"{data.EuclideanDistanceForSet}," +
            $"{data.EuclideanDistanceForBlock}," +
            $"{data.FittsData.EuclideanDeviation}," +
            $"{data.EuclideanDeviationForSet}," +
            $"{data.EuclideanDeviationForBlock}," +
            $"{data.PathCurvature},");
        
        // Speeds and times
        dataLineBuilder.Append($"{data.AverageMovementSpeed}," +
            $"{data.MaxSpeed}," +
            $"{data.MinSpeed}," +
            $"{data.ReactionTime}," +
            $"{data.BallisticTime}," +
            $"{data.CorrectionTime},");

        // Balistic and correction distances
        dataLineBuilder.Append($"{data.BallisticDistanceAlongAxis}," +
            $"{data.BallisticDistanceAlongAllAxes}," +
            $"{data.CorrectionDistanceAlongAxis}," +
            $"{data.CorrectionDistanceAlongAllAxes}");

        // Log corners of target
        for (int i = 0; i < currentTargetCorners.Length; i++)
        {
            dataLineBuilder.Append($",{currentTargetCorners[i].x},{currentTargetCorners[i].y},{currentTargetCorners[i].z}");
        }

        // Convert to string
        string dataLine = dataLineBuilder.ToString();

        // Write to individual and master data files
        WriteDataLine(individualParticipantPath, dataLine);

        // Only write to the master data file if logToMasterFile is true
        if (logToMasterFile)
        {
            WriteDataLine(allDataPath, dataLine);
        }
    }

    /// <summary>
    /// Write all movement data for each loop.
    /// </summary>
    /// <param name="position">Current position of the controller</param>
    /// <param name="rotation">Current rotation of the controller</param>
    /// <param name="isTriggerPressed">Whether the trigger is pressed</param>
    /// <param name="hapticStrength">Current haptic feedback strength</param>
    /// <param name="timeSinceStart">Time elapsed since the start of logging</param>
    /// <param name="rawSpeed">Raw speed of the controller</param>
    /// <param name="filteredSpeed">Filtered speed of the controller</param>
    /// <param name="phase">Current phase (training or testing)</param>
    /// <param name="distanceID">Identifier for the target distance</param>
    /// <param name="directionID">Identifier for the movement direction</param>
    /// <param name="controllerWidth">Width of the controlleranchor</param>
    /// <param name="targetWidth">Width of the target</param>
    private void CSVMovementWriter(
        Vector3 position,
        Quaternion rotation,
        bool isTriggerPressed,
        int hapticStrength,
        float timeSinceStart,
        float rawSpeed,
        float filteredSpeed,
        int phase,
        int distanceID,
        int directionID,
        float controllerWidth,
        float targetWidth)
    {
        // Trigger pressed = 1, not = 0
        triggerPressed = isTriggerPressed ? 1 : 0;

        // Format data into a CSV string
        StringBuilder movementDataLineBuilder = new StringBuilder();

        // General info - date, participant no., phase, axis, distance etc.
        movementDataLineBuilder.Append($"{timestamp}," +
            $"{participantNumber}," +
            $"{blockNumber}," +
            $"{controllerWidth}," +
            $"{targetWidth}," +
            $"{phase}," +
            $"{distanceID}," +
            $"{directionID}," +
            $"{MovementAxis}," +
            $"{BlockLetter}," +
            $"{(blockRepetition ? "1" : "0")}," +
            $"{SetNumber}," +
            $"{TrainingSetNumber}," +
            $"{TestSetNumber}," +
            $"{TrialNumberInSet}," +
            $"{TrialNumberInBlock}," +
            $"{TrialNumberByID}," +
            $"{HapticFeedbackMethod}," +
            $"{position.x}," +
            $"{position.y}," +
            $"{position.z}," +
            $"{rotation.x}," +
            $"{rotation.y}," +
            $"{rotation.z}," +
            $"{rotation.w}," +
            $"{triggerPressed}," +
            $"{hapticStrength}," +
            $"{timeSinceStart}," +
            $"{rawSpeed}," +
            $"{filteredSpeed}");

        // Log corners of target
        for (int i = 0; i < currentTargetCorners.Length; i++)
        {
            movementDataLineBuilder.Append($",{currentTargetCorners[i].x},{currentTargetCorners[i].y},{currentTargetCorners[i].z}");
        }

        // Convert to string
        string movmementDataLine = movementDataLineBuilder.ToString();

        // Write to individual and master data files
        WriteDataLine(individualMovementFilePath, movmementDataLine);

        // Only write to the master data file if logToMasterFile is true
        if (logToMasterFile)
        {
            WriteDataLine(allMovementFilePath, movmementDataLine);
        }
    }

    #endregion

    #region Speed and Distance Calculations

    /// <summary>
    /// Logs controller position, rotation, and speed over time for single runs.
    /// </summary>
    /// <param name="position">Current position of the controller</param>
    /// <param name="rotation">Current rotation of the controller</param>
    /// <param name="isTriggerPressed">Whether the trigger is pressed</param>
    /// <param name="phase">Current phase (training/testing)</param>
    /// <param name="distanceID">Distance ID (short/medium/long)</param>
    /// <param name="directionID">Direction ID (forward/right/back/left)</param>
    /// <param name="controllerWidth">Width of the controller</param>
    /// <param name="targetWidth">Width of the target</param>
    /// <param name="rawSpeed">Raw speed of the controller</param>
    public void LogControllerPositionRotationAndSpeed(
        Vector3 position, 
        Quaternion rotation, 
        bool isTriggerPressed, 
        int phase, 
        int distanceID, 
        int directionID, 
        float controllerWidth, 
        float targetWidth, 
        float rawSpeed)
    {
        // Only log while tracking is enabled
        if (!isTrackingEnabled) return;

        // Record the current timestamp
        float currentTime = Time.time;

        // Add current data to lists
        controllerPositions.Add(position);
        controllerRotations.Add(rotation);
        triggerStates.Add(isTriggerPressed);
        controllerTimestamps.Add(currentTime);
        speedsSinceLastSelection.Add(rawSpeed);
        positionsSinceLastSelection.Add(position);
        timesSinceLastSelection.Add(currentTime);

        // Update filtered speed
        filteredSpeed = speedAlpha * rawSpeed + (1 - speedAlpha) * filteredSpeed;

        // Calculate time since start
        float timeSinceStart = timeManagement.GetTimeSinceStart();

        // Set max speed index
        GetMaxSpeedIndex();

        // Write data to movement master file
        CSVMovementWriter(position, rotation, isTriggerPressed, currentHapticStrength, timeSinceStart, rawSpeed, filteredSpeed, phase, distanceID, directionID, controllerWidth, targetWidth);
    }

    /// <summary>
    /// Calculate maximum speed during movement between trigger presses and set index to this speed
    /// </summary>
    public void GetMaxSpeedIndex()
    {
        // Initialise index for speed list
        maxSpeedIndex = -1;
        // Initialise maxspeed value
        maxSpeed = 0f;

        // Get max speed value from all values
        for (int i = 0; i < speedsSinceLastSelection.Count; i++)
        {
            // Change maxSpeed value and index if new maxSpeed
            if (speedsSinceLastSelection[i] > maxSpeed)
            {
                maxSpeed = speedsSinceLastSelection[i];
                maxSpeedIndex = i;
            }
        }

        // Error checking by validating index
        if (maxSpeedIndex < 0 || maxSpeedIndex >= timesSinceLastSelection.Count)
        {
            Debug.LogWarning("Logs: Invalid maxSpeedIndex detected.");
            maxSpeedIndex = - 1;
        }
    }

    /// <summary>
    /// Calculate minimum speed during movement between trigger presses
    /// </summary>
    /// <returns>float min speed recorded</returns>
    public float GetMinSpeed()
    {
        if (speedsSinceLastSelection.Count == 0) return 0f;

        // Filter out speeds below the threshold
        var validSpeeds = speedsSinceLastSelection.Where(speed => speed > speedThreshold).ToList();

        if (validSpeeds.Count == 0) return 0f;

        return validSpeeds.Min();
    }

    /// <summary>
    /// Calculate ballistic and correction times using Nieuwenhuizen's method.
    /// Ballistic time: Duration from movement start to peak speed.
    /// Correction time: Duration from peak speed to movement end.
    /// </summary>
    /// <returns>Tuple containing ballistic time and correction time.</returns>
    public (float BallisticTime, float CorrectionTime) CalculateBallisticAndCorrectionTimes()
    {
        if (speedsSinceLastSelection.Count < 2 || maxSpeedIndex == -1)
        {
            Debug.LogWarning("Logs: Insufficient data to calculate ballistic and correction times.");
            return (0f, 0f);
        }

        // Ballistic time is the duration from movement start to peak speed
        float ballisticTime = timesSinceLastSelection[maxSpeedIndex] - timesSinceLastSelection[0];

        // Correction time is the duration from peak speed to movement end
        float correctionTime = timesSinceLastSelection.Last() - timesSinceLastSelection[maxSpeedIndex];

        return (ballisticTime, correctionTime);
    }

    /// <summary>
    /// Calculate movement speed based on the current position and time.
    /// </summary>
    /// <param name="currentPosition">Current position of the controller</param>
    /// <returns>Float representing the movement speed of the controller</returns>
    public float CalculateSpeed(Vector3 currentPosition)
    {
        // Initialise currentTime and speed
        float currentTime = Time.time;
        float speed = 0;

        // If this is the first log, initialise values and return 0 speed
        if (lastLogTime == 0f)
        {
            lastLogTime = currentTime;
            lastPosition = currentPosition;
            return 0f;
        }

        // Calculate the time difference since the last log
        float timeDifference = currentTime - lastLogTime;

        // Divsion by zero checker
        if (timeDifference > 0f)
        {
            // Calculate the distance traveled since the last log
            float distance = Vector3.Distance(currentPosition, lastPosition);
            // Calculate and set speed
            speed = distance / timeDifference;
            // Update the last log time and position for future calculations
            lastLogTime = currentTime;
            lastPosition = currentPosition;
        }

        // Ensure speed is positive
        return Mathf.Abs(speed);
    }

    /// <summary>
    /// Calculate average movement speed
    /// </summary>
    /// <returns>Float average movement speed of trials.</returns>
    public float CalculateAverageMovementSpeed()
    {
        if (positionsSinceLastSelection.Count < 2) return 0f;

        // Calculate the total distance traveled
        float totalDistance = 0f;
        for (int i = 1; i < positionsSinceLastSelection.Count; i++)
        {
            totalDistance += Vector3.Distance(positionsSinceLastSelection[i - 1], positionsSinceLastSelection[i]);
        }

        // Calculate the total time elapsed
        float totalTime = timesSinceLastSelection.Last() - timesSinceLastSelection.First();

        return totalTime > 0f ? totalDistance / totalTime : 0f;
    }

    /// <summary>
    /// Get distance of ballistic phase along current movement axis
    /// </summary>
    /// <param name="movementAxis">Current movement axis</param>
    /// <returns>Float distance moved</returns>
    public float GetBallisticDistanceAlongAxis(Vector3 movementAxis)
    {
        if (positionsSinceLastSelection.Count < 2) return 0f;

        // Initialise distance
        float distance = 0f;

        // Total up distance in positionsSinceLastSelection list until max speed
        for (int i = 1; i <= maxSpeedIndex; i++)
        {
            Vector3 delta = positionsSinceLastSelection[i] - positionsSinceLastSelection[i - 1];
            // Vector3.Dot for one axis
            float distanceAlongAxis = Vector3.Dot(delta, movementAxis.normalized);
            // Ensure distance is positve
            distance += Mathf.Abs(distanceAlongAxis);
        }

        return distance;
    }

    /// <summary>
    /// Get distacnce of ballistice phase along all axes
    /// </summary>
    /// <returns>Float distance along all axes</returns>
    public float GetBallisticDistanceAlongAllAxes()
    {
        if (positionsSinceLastSelection.Count < 2) return 0f;

        // Initialise distance
        float distance = 0f;

        // Total up distance in positionsSinceLastSelection list until max speed
        for (int i = 1; i <= maxSpeedIndex; i++)
        {
            // Vector3.Distance for all axes
            float deltaDistance = Vector3.Distance(positionsSinceLastSelection[i], positionsSinceLastSelection[i - 1]);
            distance += deltaDistance;
        }

        return distance;
    }

    /// <summary>
    /// Get distance of correction phase along movement axis
    /// </summary>
    /// <param name="movementAxis">Current movmenet axis</param>
    /// <returns>Float distance along movement axis</returns>
    public float GetCorrectionDistanceAlongAxis(Vector3 movementAxis)
    {
        if (positionsSinceLastSelection.Count < 2) return 0f;

        // Intialise distance
        float distance = 0f;

        // Total up distance in speedSinceLastSelection list from max speed to end
        for (int i = maxSpeedIndex + 1; i < positionsSinceLastSelection.Count; i++)
        {
            Vector3 delta = positionsSinceLastSelection[i] - positionsSinceLastSelection[i - 1];
            // Vector3.Dot for one axis
            float distanceAlongAxis = Vector3.Dot(delta, movementAxis.normalized);
            distance += Mathf.Abs(distanceAlongAxis);
        }

        return distance;
    }

    /// <summary>
    /// Get distance of correction phase along all exes
    /// </summary>
    /// <returns>Float distance along all axes</returns>
    public float GetCorrectionDistanceAlongAllAxes()
    {
        if (positionsSinceLastSelection.Count < 2) return 0f;

        // Intialise distance
        float distance = 0f;

        // Total up distance in speedSinceLastSelection list from max speed to end
        for (int i = maxSpeedIndex + 1; i < positionsSinceLastSelection.Count; i++)
        {
            // Vector3.Distance for all axes
            float deltaDistance = Vector3.Distance(positionsSinceLastSelection[i], positionsSinceLastSelection[i - 1]);
            distance += deltaDistance;
        }

        return distance;
    }

    /// <summary>
    /// Returns maxSpeed value
    /// </summary>
    /// <returns>float max speed</returns>
    public float GetMaxSpeed() => maxSpeed;

    #endregion

    #region Helper Mehods

    /// <summary>
    /// Reset movement data after logging
    /// </summary>
    public void ResetMovementData()
    {
        positionsSinceLastSelection.Clear();
        timesSinceLastSelection.Clear();
        speedsSinceLastSelection.Clear();

        // Reset lastLogTime and lastPosition
        lastLogTime = 0f;
        lastPosition = Vector3.zero;
    }

    /// <summary>
    /// Set current haptic strength
    /// </summary>
    /// <param name="hapticStrength">Haptic feedback strength value to set</param>
    public void SetCurrentHapticStrength(int hapticStrength)
    {
        currentHapticStrength = hapticStrength;
    }

    /// <summary>
    /// Enables logging by setting the tracking flag to true.
    /// </summary>
    public void EnableLogging()
    {
        isTrackingEnabled = true;
    }

    /// <summary>
    /// Disables logging by setting the tracking flag to false.
    /// </summary>
    public void DisableLogging()
    {
        isTrackingEnabled = false;
    }

    /// <summary>
    /// Resets all movment tracking timers, lists, positions
    /// </summary>
    public void ResetMovementTracking()
    {
        // Reset time and position tracking variables
        lastLogTime = 0f;
        lastPosition = Vector3.zero;

        // Clear all lists that store tracking data
        positionsSinceLastSelection.Clear();
        timesSinceLastSelection.Clear();
        speedsSinceLastSelection.Clear();
        controllerPositions.Clear();
        controllerRotations.Clear();
        triggerStates.Clear();
        controllerTimestamps.Clear();

        // Reset filtered speed and last selection time
        filteredSpeed = 0f;
        lastSelectionTime = 0f;
    }

    /// <summary>
    /// Calculates the current movement vector of the controller.
    /// </summary>
    /// <returns>Vector3 representing the controller's movement vector.</returns>
    public Vector3 CalculateCurrentMovementVector()
    {
        if (positionsSinceLastSelection.Count < 2) return Vector3.zero;

        // Calulate normlised Vector3 direction between last and current position to get direction - from controllerPosition list
        Vector3 lastPosition = positionsSinceLastSelection[positionsSinceLastSelection.Count - 2];
        Vector3 currentPosition = positionsSinceLastSelection[positionsSinceLastSelection.Count - 1];
        return (currentPosition - lastPosition).normalized;
    }

    /// <summary>
    /// Returns folder path where logs stored for end of testing behaviour
    /// </summary>
    /// <returns>String folder path for log files.</returns>
    public string GetLogsFolderPath() => logsFolderPath;

    // Participant number public int.
    public int ParticipantNumber => participantNumber;

    // Block number public int.
    public int BlockNumber => blockNumber;

    #endregion

    #region Target Position Logging

    /// <summary>
    /// Target Position Logging
    /// </summary>
    /// <param name="targetIndex">Index of the target</param>
    /// <param name="targetCollider">BoxCollider component of the target</param>
    public void LogTargetBoxColliderCorners(int targetIndex, BoxCollider targetCollider)
    {
        // Get all corners of the box collider and store the corners in the list for the current target
        currentTargetCorners = GetBoxColliderCorners(targetCollider);
    }

    /// <summary>
    /// Returns all 8 positions of corners of sphere target
    /// </summary>
    /// <param name="boxCollider">BoxCollider component to get corners from</param>
    /// <returns>Vector3 list of target corner positions.</returns>
    private Vector3[] GetBoxColliderCorners(BoxCollider boxCollider)
    {
        // TransformPoint - convert local center of the boxCollider to world space
        Vector3 centre = boxCollider.transform.TransformPoint(boxCollider.center);

        // Accounts for the targets's non-uniform scaling, i.e., width different along movement axis
        Vector3 size = Vector3.Scale(boxCollider.size, boxCollider.transform.lossyScale) * 0.5f;
        Vector3[] corners = new Vector3[8];

        // Calculate all 8 corners of the BoxCollider in world space
        corners[0] = centre + new Vector3(-size.x, -size.y, -size.z);
        corners[1] = centre + new Vector3(-size.x, -size.y, size.z);
        corners[2] = centre + new Vector3(-size.x, size.y, -size.z);
        corners[3] = centre + new Vector3(-size.x, size.y, size.z);
        corners[4] = centre + new Vector3(size.x, -size.y, -size.z);
        corners[5] = centre + new Vector3(size.x, -size.y, size.z);
        corners[6] = centre + new Vector3(size.x, size.y, -size.z);
        corners[7] = centre + new Vector3(size.x, size.y, size.z);

        return corners;
    }

    #endregion    
}