using System.Collections;
using System.Collections.Generic;
using System.Timers;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.UI;

public class CameraManagement : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera eyeAnchor;                  // Eye anchor for main camera for setting midpoint to eyelevel and recquired distance in front of user
    [SerializeField] private GameObject midpoint;               // Track midpoint of all targets
    [SerializeField] private float desiredDistance = 0.4f;      // Desired midpoint distance in front of eyeAnchor

    // Screen Blackout
    [Header("Screen Blackout Settings")]
    [SerializeField] private GameObject blackScreenCube;       // Black full-screen cube that covers the user's view

    #region Camera Movement

    /// <summary>
    /// Move camera to face midpoint on z axis at desired distance.
    /// </summary>
    public void MoveCameraRigToMidpoint()
    {
        // Error handling for Inspector assignments
        if (midpoint == null)
        {
            Debug.LogError("Midpoint GameObject is not assigned.");
            return;
        }
        if (eyeAnchor == null)
        {
            Debug.LogError("Eye anchor is not assigned.");
            return;
        }

        // Calculate the new camera position relative to the midpoint, minus desired z movement
        Vector3 newCameraPosition = new Vector3(midpoint.transform.position.x, midpoint.transform.position.y, midpoint.transform.position.z - desiredDistance);

        // Get the camera rig from the eyeanchor
        Transform cameraRig = eyeAnchor.transform.parent.parent;

        if (cameraRig != null)
        {
            // Get the current headset device
            InputDevice headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);

            // Get the current rotation of the VR headset
            if (headDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion headsetRotation))
            {
                // Extract the yaw (rotation around Y-axis) from the headset rotation
                Vector3 eulerHeadsetRotation = headsetRotation.eulerAngles;
                float headsetYaw = eulerHeadsetRotation.y;

                // Create a rotation that cancels out the headset's yaw to keep the midpoint in front of the participant
                Quaternion inverseYawRotation = Quaternion.Euler(0, -headsetYaw, 0);

                // Apply this rotation to the camera rig to keep midpoint in front of participant
                cameraRig.rotation = inverseYawRotation;
            }
            else
            {
                Debug.LogWarning("Could not get headset rotation from device.");
            }

            // Move the camera rig to the new position
            cameraRig.position = newCameraPosition;
        }
        else
        {
            Debug.LogError("Camera rig not found.");
        }
    }

    #endregion

    #region Blackout

    /// <summary>
    /// Set screen to black by enabling or disabling the black screen cube.
    /// </summary>
    /// <param name="isBlack">If true, the screen will be set to black.</param>
    public void SetScreenBlack(bool isBlack)
    {
        // Error handling for cube element assignment
        if (blackScreenCube != null)
        {
            // Activate or deactivate the black screen cube based on the isBlack parameter
            blackScreenCube.SetActive(isBlack);
        }
        else
        {
            Debug.LogError("CameraManagement: BlackScreenPanel is not assigned.");
        }
    }

    #endregion
}
