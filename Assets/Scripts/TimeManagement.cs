using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TimeManagement : MonoBehaviour
{
    // Const var
    private const float UNSET_TIME = -1f;           // Constant for unset time values, used to indicate that a time value has not been set

    // Time count variables
    private float lastClickTime = UNSET_TIME;       // Exact time of last click
    private float lastLogTime = UNSET_TIME;         // Last time when position was logged
    private float startTime = UNSET_TIME;           // Time when the first trigger click occurred

    #region Click Time

    /// <summary>
    /// Update last trigger click time and start time with the current time
    /// </summary>
    /// <param name="currentTime">The current time of the trigger click.</param>
    public void UpdateLastClickTime(float currentTime)
    {
        // Update the time of the last click
        lastClickTime = currentTime;

        // Set the start time if it has not been set yet (i.e., the first click)
        if (startTime < 0)
        {
            startTime = currentTime;
        }
    }

    #endregion

    #region Timer Control

    /// <summary>
    /// Start timer
    /// </summary>
    public void StartTimer()
    {
        // Set the start time if it has not been set yet
        if (startTime < 0)
        {
            startTime = Time.time;
        }
    }

    /// <summary>
    /// Reset the last click time.
    /// </summary>
    public void ResetLastClickTime()
    {
        lastClickTime = UNSET_TIME;
    }

    #endregion

    #region Logging

    /// <summary>
    /// Set the last log time to the specified value.
    /// </summary>
    /// <param name="logTime">The time to set as the last log time.</param>
    public void SetLastLogTime(float logTime)
    {
        lastLogTime = logTime;
    }

    /// <summary>
    /// Determine the time taken between the last click and the current time, i.e., current target hit, and returns time taken
    /// </summary>
    /// <returns>Float time taken from last trigger slection.</returns>
    public float GetTimeTaken()
    {
        float currentTime = Time.time;
        // If lastClickTime is unset and has no value, i.e., first hit, return 0, else return currenttime - lastclicktime
        return lastClickTime >= 0 ? currentTime - lastClickTime : 0f;
    }

    /// <summary>
    /// Returns time since start of trigger selections
    /// </summary>
    /// <returns>Float time since logging started in testing.</returns>
    public float GetTimeSinceStart()
    {
        // If startTime is set, return the time elapsed since the start; otherwise, return 0
        return startTime >= 0 ? Time.time - startTime : 0f;
    }

    /// <summary>
    /// Returns last time position was logged
    /// </summary>
    /// <returns>Float last logged time.</returns>
    public float GetLastLogTime() => lastLogTime;

    #endregion
}
