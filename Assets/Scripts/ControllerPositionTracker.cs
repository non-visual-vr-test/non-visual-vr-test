using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ControllerPositionTracker : MonoBehaviour
{
    public OVRInput.Controller controllerType = OVRInput.Controller.RTouch;
    public bool showDebugInfo = true;

    private Vector3 controllerPosition;
    private Quaternion controllerRotation;

    private void Update()
    {
        // Get the current position and rotation of the controller
        controllerPosition = OVRInput.GetLocalControllerPosition(controllerType);
        controllerRotation = OVRInput.GetLocalControllerRotation(controllerType);

        // Transform the local position to world space
        Vector3 worldPosition = transform.TransformPoint(controllerPosition);

        if (showDebugInfo)
        {
            Debug.Log($"Controller Position: {worldPosition}");
            Debug.Log($"Controller Rotation: {controllerRotation.eulerAngles}");
        }

        // You can use the position and rotation data here for your specific needs
        // For example, you might want to update the position of an object:
        // transform.position = worldPosition;
        // transform.rotation = controllerRotation;
    }

    // Method to get the current world position of the controller
    public Vector3 GetControllerWorldPosition()
    {
        return transform.TransformPoint(controllerPosition);
    }

    // Method to get the current rotation of the controller
    public Quaternion GetControllerRotation()
    {
        return controllerRotation;
    }
}
