using System.Collections.Generic;
using UnityEngine;

public class VisualsManager : MonoBehaviour
{
    // Reference to CameraManagement for screen blackout
    private CameraManagement cameraManagement;

    // Visual feedback control
    [Header("Visual Feedback Control")]
    [SerializeField] private bool targetsVisualsToggle = false;                                     // Flag to indicate if visuals for targets are on for a set number of trials
    [SerializeField] private int visualTrials = 3;                                                  // Number of trials with visual feedback
    private bool visualsDisabled = false;                                                           // Flag to indicate if visuals are disabled
    private int currentVisualTrialCount = 0;                                                        // Counter for visual trials
    private Dictionary<GameObject, Color> originalColours = new Dictionary<GameObject, Color>();    // Store original colours for target objects
    private List<TargetManager.TargetPair> targetPairs = new List<TargetManager.TargetPair>();      // List to store TargetPairs to disable visuals

    // Screen Blackout Settings
    [Header("Screen Blackout Settings")]
    [SerializeField] private int preBlackoutRepetitions = 0;         // Number of trials before screen turns black
    private int blackoutTrialCount = 0;                              // Counter for trials pre-blackout

    // Midpoint haptic object
    [Header("Midpoint Haptic Object")]
    [SerializeField] private GameObject midpointHaptic;             // GameObject placed at midpoint of all targets
    [SerializeField] private float midpointWidth = 0.05f;           // Default width of midpoint
    [SerializeField] private bool midpointVisualsToggle = false;    // Flag to indicate if renderer is on or off
    private Renderer midpointHapticRenderer;                        // Renderer for midpoint haptic GameObject

    // Current and Previous Target Pairs and target for visual handling
    private TargetManager.TargetPair currentPair = null;
    private TargetManager.TargetPair previousPair = null;
    private GameObject currentTarget = null;

    #region Unity Lifecycle Methods

    /// <summary>
    /// Assign class
    /// </summary>
    void Awake()
    {
        if (cameraManagement == null) cameraManagement = FindObjectOfType<CameraManagement>();
    }

    /// <summary>
    /// Assign class and initialise midpoint haptic renderer
    /// </summary>
    private void Start()
    {
        // Initialise the midpoint haptic renderer
        if (midpointHaptic != null)
        {
            midpointHapticRenderer = midpointHaptic.GetComponent<Renderer>();
            if (midpointHapticRenderer == null)
            {
                Debug.LogError("Midpoint Haptic GameObject does not have a Renderer component.");
            }
        }
        else
        {
            Debug.LogError("Midpoint Haptic GameObject is not assigned.");
        }
    }

    #endregion

    #region Initialisation Methods

    /// <summary>
    /// Initialise visuals at the start of a new set
    /// </summary>
    /// <param name="targetPairs">List of target pairs for the visual trials</param>
    /// <param name="skipTraining">Flag to determine if training is skipped</param>
    public void InitialiseVisuals(List<TargetManager.TargetPair> targetPairs, bool skipTraining)
    {
        // Store the targetPairs list to targetPairs class variable
        this.targetPairs = targetPairs;

        // Cache the original colours of all targets
        CacheOriginalColours();

        // Disable all target and midpoint mesh renderers initially
        DisableAllMeshRenderers();

        // Set the state of the midpoint haptic renderer based on midpointVisualsToggle
        MidpointHapticRenderer(!midpointVisualsToggle);

        // Toggle intial blackout and visual state
        InitialiseBlackOutAndVisuals();
    }

    /// <summary>
    /// Toggle intial blackout and visual state based on counters
    /// </summary>
    private void InitialiseBlackOutAndVisuals()
    {
        // Handle initial blackout state based on preBlackoutRepetitions
        if (preBlackoutRepetitions == 0)
        {
            // Blackout the screen initially if no repetitions are set
            ToggleScreenBlackout(true);
        }
        else
        {
            // Keep the screen visible until blackout repetitions are reached
            ToggleScreenBlackout(false);
        }

        // If visuals toggle is on, prepare for visual trials
        if (targetsVisualsToggle)
        {
            visualsDisabled = false;
            // Reset trial count for visual trials
            currentVisualTrialCount = 0;
        }
        // Disable visuals if toggle is off
        else
        {
            visualsDisabled = true;
        }
    }

    #endregion

    #region Colour Control

    /// <summary>
    /// Cache the original colour of a target for later restoration
    /// </summary>
    /// <param name="target">Target GameObject</param>
    public void CacheOriginalColour(GameObject target)
    {
        if (target == null)
        {
            Debug.LogError("CacheOriginalColour: target is null.");
            return;
        }

        // Store the original color of the target if it hasn't been cached yet
        if (!originalColours.ContainsKey(target))
        {
            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                // Cache the original colour
                originalColours[target] = targetRenderer.material.color;
            }
            else
            {
                // Default colour
                originalColours[target] = Color.white;
            }
        }
    }

    /// <summary>
    /// Cache the original colours of all targets in a pair
    /// </summary>
    private void CacheOriginalColours()
    {
        // Cache the original colours of all targets
        foreach (var pair in targetPairs)
        {
            if (pair.target1 != null)
                CacheOriginalColour(pair.target1);
            else
                Debug.LogError($"InitialiseVisuals: Pair {pair.GetHashCode()} has target1 as null.");

            if (pair.target2 != null)
                CacheOriginalColour(pair.target2);
            else
                Debug.LogError($"InitialiseVisuals: Pair {pair.GetHashCode()} has target2 as null.");
        }
    }

    /// <summary>
    /// Change the colour of a target for visual feedback
    /// </summary>
    /// <param name="target">Target GameObject</param>
    /// <param name="colour">New colour</param>
    public void ChangeTargetColour(GameObject target, Color colour)
    {
        if (target == null)
        {
            Debug.LogError("ChangeTargetColour: target is null.");
            return;
        }

        // Get the Renderer component and change the target's colour
        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            targetRenderer.material.color = colour;
        }
        else
        {
            Debug.LogError($"ChangeTargetColour: Renderer component missing on target '{target.name}'.");
        }
    }

    /// <summary>
    /// Restore the original colour of a specific target
    /// </summary>
    /// <param name="target">Target GameObject</param>
    public void RestoreColour(GameObject target)
    {
        if (target == null)
        {
            Debug.LogError("RestoreColour: target is null.");
            return;
        }

        // Check if the original colour for the target is stored
        if (originalColours.ContainsKey(target))
        {
            // Get the Renderer component of the target
            Renderer targetRenderer = target.GetComponent<Renderer>();
            if (targetRenderer != null)
            {
                // Restore the original colour of the target
                targetRenderer.material.color = originalColours[target];
            }
            else
            {
                Debug.LogError($"RestoreColour: Renderer component missing on target '{target.name}'.");
            }
        }
        else
        {
            Debug.LogWarning($"RestoreColour: Original colour for target '{target.name}' not found.");
        }
    }

    /// <summary>
    /// Restore original colours of all targets
    /// </summary>
    /// <param name="targetPairs">List of TargetPairs</param>
    public void RestoreOriginalColours(List<TargetManager.TargetPair> targetPairs)
    {
        // Iterate over all target pairs and restore their original colours
        foreach (var pair in targetPairs)
        {
            RestoreColour(pair.target1);
            RestoreColour(pair.target2);
        }
    }

    #endregion

    #region Renderer Control

    /// <summary>
    /// Disable all mesh renderers for all targets and disable the midpoint visual if midpointVisualsToggle is true
    /// </summary>
    private void DisableAllMeshRenderers()
    {
        foreach (var pair in targetPairs)
        {
            SetTargetVisualState(pair.target1, false);
            SetTargetVisualState(pair.target2, false);
        }

        // Disable the midpoint visual feedback if midpointVisualsToggle is true
        if (midpointVisualsToggle) DisableMidpointVisual();
    }

    /// <summary>
    /// Enable or disable visuals for a specific target based on state
    /// </summary>
    /// <param name="target">Target GameObject</param>
    /// <param name="state">true or flase for visuals</param>
    private void SetTargetVisualState(GameObject target, bool state)
    {
        if (target == null)
        {
            Debug.LogError("SetTargetVisualState: target is null.");
            return;
        }

        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            // Enable or disable based on bool state
            targetRenderer.enabled = state;
        }
        else
        {
            Debug.LogError($"SetTargetVisualState: Renderer component missing on target '{target.name}'.");
        }
    }

    /// <summary>
    /// Enable Mesh Renderers for the current pair and set colors appropriately
    /// </summary>
    private void EnableCurrentPairVisuals()
    {
        if (currentPair == null)
        {
            Debug.LogError("EnableCurrentPairVisuals: currentPair is null.");
            return;
        }

        if (currentTarget == null)
        {
            Debug.LogError("EnableCurrentPairVisuals: currentTarget is null.");
            return;
        }

        // Enable Mesh Renderers for both targets in the current pair
        SetTargetVisualState(currentPair.target1, true);
        SetTargetVisualState(currentPair.target2, true);

        // Change the color of the current target to green
        ChangeTargetColour(currentTarget, Color.green);

        // For the other target, restore its original color
        GameObject otherTarget = (currentTarget == currentPair.target1) ? currentPair.target2 : currentPair.target1;
        RestoreColour(otherTarget);
    }

    /// <summary>
    /// Restore original colours and disable Mesh Renderers for all targets
    /// </summary>
    public void RestoreAllColoursAndDisableRenderers()
    {
        foreach (var pair in targetPairs)
        {
            RestoreColour(pair.target1);
            RestoreColour(pair.target2);
            SetTargetVisualState(pair.target1, false);
            SetTargetVisualState(pair.target2, false);
        }

        // Disable the midpoint visual feedback if midpointVisualsToggle is true
        if (midpointVisualsToggle) DisableMidpointVisual();
    }

    /// <summary>
    /// Disable visuals of all targets and midpoint
    /// </summary>
    /// <param name="targetPairs">List of target pairs to disable visuals for</param>
    public void DisableVisuals(List<TargetManager.TargetPair> targetPairs)
    {
        visualsDisabled = true;

        // Iterate over all target pairs and disable their visuals
        foreach (var pair in targetPairs)
        {
            SetTargetVisualState(pair.target1, false);
            SetTargetVisualState(pair.target2, false);
        }

        // Disable the midpoint visual feedback if midpointVisualsToggle is true
        if (midpointVisualsToggle) DisableMidpointVisual();
    }

    /// <summary>
    /// Disable visuals of all targets
    /// </summary>
    private void DisableVisuals()
    {
        visualsDisabled = true;
    }

    #endregion

    #region Screen Blackout

    /// <summary>
    /// Handle screen blackout after a set number of trials
    /// </summary>
    public void HandleBlackoutTrials()
    {
        blackoutTrialCount++;

        // Check if the blackout trial count exceeds the pre-blackout repetitions threshold
        if (blackoutTrialCount >= preBlackoutRepetitions && preBlackoutRepetitions > 0)
        {
            // Make the screen black using camera manager
            if (cameraManagement != null)
            {
                cameraManagement.SetScreenBlack(true);
            }
            else
            {
                Debug.LogError("CameraManagement reference is missing in VisualsManager.");
            }
        }
    }

    /// <summary>
    /// Toggle screen blackout on or off
    /// </summary>
    /// <param name="isBlackout">True to enable blackout, false to disable</param>
    public void ToggleScreenBlackout(bool isBlackout)
    {
        // Set the screen to black or revert using the camera manager
        if (cameraManagement != null)
        {
            cameraManagement.SetScreenBlack(isBlackout);
        }
        else
        {
            Debug.LogError("ToggleScreenBlackout: CameraManagement reference is missing.");
        }
    }

    #endregion

    #region Midpoint Visual

    /// <summary>
    /// Disable midpoint's visual renderer
    /// </summary>
    public void DisableMidpointVisual()
    {
        if (midpointHapticRenderer != null)
        {
            // Disable the midpoint visual by setting renderer to false
            midpointHapticRenderer.enabled = false;
        }
    }

    /// <summary>
    /// Set midpoint haptics renderer on or off
    /// </summary>
    /// <param name="rendererOn">True to enable, false to disable</param>
    public void MidpointHapticRenderer(bool rendererOn)
    {
        if (midpointHapticRenderer != null)
        {
            // Enable or disable the midpoint visual based on parameter
            midpointHapticRenderer.enabled = rendererOn;
        }
    }

    /// <summary>
    /// Returns midpoint haptic gameobject and width
    /// </summary>
    /// <returns>A tuple containing the midpoint haptic GameObject and its width as a float</returns>
    public (GameObject, float) GetMidpointHapticAndWidth() => (midpointHaptic, midpointWidth);

    #endregion

    #region Trial Counting

    /// <summary>
    /// Handle visual trials and disable visuals after specified trials
    /// </summary>
    public void HandleVisualTrials()
    {
        // Check if visuals are enabled and not yet disabled
        if (targetsVisualsToggle && !visualsDisabled)
        {
            currentVisualTrialCount++;
            if (currentVisualTrialCount <= visualTrials)
            {
                // Enable visuals for current two targets
                EnableCurrentPairVisuals();
            }
            else
            {
                // Disable visuals after visual trials are completed
                DisableAllMeshRenderers();
                visualsDisabled = true;
            }
        }
    }

    /// <summary>
    /// Reset visual counters (e.g., after a new set)
    /// </summary>
    public void ResetVisualCounters()
    {
        currentVisualTrialCount = 0;
        blackoutTrialCount = 0;
    }

    /// <summary>
    /// Resets the visual trial counter.
    /// </summary>
    public void ResetVisualTrialCount()
    {
        currentVisualTrialCount = 0;
    }

    #endregion

    #region Current Pair Control

    /// <summary>
    /// Set the current pair and update visuals accordingly.
    /// </summary>
    /// <param name="newPair">The new pair of targets</param>
    /// <param name="newCurrentTarget">The new current target within the pair</param>
    public void SetCurrentPair(TargetManager.TargetPair newPair, GameObject newCurrentTarget)
    {
        if (newPair == null)
        {
            Debug.LogError("SetCurrentPair: newPair is null.");
            return;
        }

        // Clear visuals of the previous pair if it exists
        if (previousPair != null)
        {
            ClearCurrentPairVisuals(previousPair);
        }

        // Assign newPair to currentPair
        currentPair = newPair;

        // Update currentTarget
        currentTarget = newCurrentTarget;

        // Enable Mesh Renderers for both targets in the current pair
        SetTargetVisualState(currentPair.target1, true);
        SetTargetVisualState(currentPair.target2, true);

        // Change the color of the current target to green
        ChangeTargetColour(currentTarget, Color.green);

        // For the other target, restore its original color
        GameObject otherTarget = (currentTarget == currentPair.target1) ? currentPair.target2 : currentPair.target1;
        RestoreColour(otherTarget);

        // Update previousPair to the current pair
        previousPair = currentPair;
    }

    /// <summary>
    /// Clear the current pair visuals
    /// </summary>
    /// <param name="pair">The pair of targets to clear visuals for</param>
    public void ClearCurrentPairVisuals(TargetManager.TargetPair pair)
    {
        if (pair == null)
        {
            Debug.LogError("ClearCurrentPairVisuals: pair is null.");
            return;
        }

        // Restore original color of the previous targets
        RestoreColour(pair.target1);
        RestoreColour(pair.target2);

        // Disable Mesh Renderers for both targets
        SetTargetVisualState(pair.target1, false);
        SetTargetVisualState(pair.target2, false);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Check if visuals are toggled on and not yet disabled
    /// </summary>
    /// <returns>True if visuals are enabled, false otherwise</returns>
    public bool AreVisualsEnabled() => targetsVisualsToggle && !visualsDisabled;

    /// <summary>
    /// Get the current blackout trial count
    /// </summary>
    /// <returns>Number of trials before blackout</returns>
    public int GetBlackoutTrialCount() => blackoutTrialCount;

    /// <summary>
    /// Check if visuals are disabled
    /// </summary>
    /// <returns>True if visuals are disabled, false otherwise</returns>
    public bool AreVisualsDisabled() => visualsDisabled;

    /// <summary>
    /// Get the number of trials before blackout
    /// </summary>
    /// <returns>preBlackoutRepetitions value</returns>
    public int GetPreBlackoutRepetitions() => preBlackoutRepetitions;

    /// <summary>
    /// Determines if visual trials are currently active.
    /// Check if visuals are toggled on and the current trial count is below the limit
    /// </summary>
    /// <returns>True if visuals are enabled and the current trial count is below the visual trial limit.</returns>
    public bool AreVisualTrialsActive() => targetsVisualsToggle && currentVisualTrialCount < visualTrials;

    /// <summary>
    /// Gets the number of visual trials remaining.
    /// Calculate and return the number of visual trials remaining
    /// </summary>
    /// <returns>Number of visual trials remaining.</returns>
    public int GetVisualTrialsRemaining() => visualTrials - currentVisualTrialCount;

    #endregion
}