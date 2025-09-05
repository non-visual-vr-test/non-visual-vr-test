using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Bhaptics.SDK2;

public class HapticExample : MonoBehaviour
{
    public void OnHover()
    {
        BhapticsLibrary.Play(BhapticsEvent.TEST);
    }
}
