using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;

public class MLReferenceSpaces : MonoBehaviour
{
    
    private bool inputSubsystemValid;
    private XRInputSubsystem inputSubsystem;

    // Start is called before the first frame update
    IEnumerator Start()
    {
#if UNITY_ANDROID
        var referenceSpaceFeature = OpenXRSettings.Instance.GetFeature<MagicLeap.OpenXR.Features.MagicLeapReferenceSpacesFeature>();
        if (!referenceSpaceFeature.enabled)
        {
            Debug.LogError("Unbounded Tracking Space cannot be set if the OpenXR Magic Leap Reference Spaces Feature is not enabled. Stopping Script.");
            yield break;
        }
#endif
        yield return new WaitUntil(() => XRGeneralSettings.Instance != null &&
                                          XRGeneralSettings.Instance.Manager != null &&
                                          XRGeneralSettings.Instance.Manager.activeLoader != null &&
                                          XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRInputSubsystem>() != null);

        inputSubsystem = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRInputSubsystem>();
        TrackingOriginModeFlags supportedModes = inputSubsystem.GetSupportedTrackingOriginModes();

        string supportedSpaces = string.Join("\n",
             ((TrackingOriginModeFlags[])Enum.GetValues(typeof(TrackingOriginModeFlags))).Where((flag) =>
                 supportedModes.HasFlag(flag) && flag != TrackingOriginModeFlags.Unknown));
        Debug.Log($"Supported Spaces:{supportedSpaces}");

        string currentSpace = inputSubsystem.GetTrackingOriginMode().ToString();
        Debug.Log($"Current Space:{currentSpace}");

        inputSubsystemValid = true;

        //SetSpace(TrackingOriginModeFlags.Unbounded);
    }

    public void SetSpace(TrackingOriginModeFlags flag)
    {
        Debug.Log("Setting Reference Space at start time to unbounded ...");
        string previousSpace = inputSubsystem.GetTrackingOriginMode().ToString();
        Debug.Log($"Current Space:{previousSpace}");
        if (inputSubsystemValid)
        {
            if (inputSubsystem.TrySetTrackingOriginMode(flag))
            {
                string currentSpace = inputSubsystem.GetTrackingOriginMode().ToString();
                Debug.Log($"New Space:{currentSpace}");
                inputSubsystem.TryRecenter();
                return;
            }
        }
        Debug.LogError("SetSpace failed to set Tracking Mode Origin to " + flag.ToString());
    }

    public void SetSpaceDevice()
    {
        Debug.Log("Calling SetSpaceDevice ...");
        string previousSpace = inputSubsystem.GetTrackingOriginMode().ToString();
        Debug.Log($"Current Space:{previousSpace}");
        if (inputSubsystemValid)
        {
            if (inputSubsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device))
            {
                string currentSpace = inputSubsystem.GetTrackingOriginMode().ToString();
                Debug.Log($"New Space:{currentSpace}");
                inputSubsystem.TryRecenter();
                return;
            }
        }
        Debug.LogError("SetSpace failed to set Tracking Mode Origin to " + TrackingOriginModeFlags.Device.ToString());
    }
    public void SetSpaceFloor()
    {
        Debug.Log("Calling SetSpaceFloor ...");
        string previousSpace = inputSubsystem.GetTrackingOriginMode().ToString();
        Debug.Log($"Current Space:{previousSpace}");
        if (inputSubsystemValid)
        {
            if (inputSubsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Floor))
            {
                string currentSpace = inputSubsystem.GetTrackingOriginMode().ToString();
                Debug.Log($"New Space:{currentSpace}");
                inputSubsystem.TryRecenter();
                return;
            }
        }
        Debug.LogError("SetSpace failed to set Tracking Mode Origin to " + TrackingOriginModeFlags.Floor.ToString());
    }
    public void SetSpaceUnbounded()
    {
        Debug.Log("Calling SetSpaceUnbounded ...");
        string previousSpace = inputSubsystem.GetTrackingOriginMode().ToString();
        Debug.Log($"Current Space:{previousSpace}");
        if (inputSubsystemValid)
        {
            if (inputSubsystem.TrySetTrackingOriginMode(TrackingOriginModeFlags.Unbounded))
            {
                string currentSpace = inputSubsystem.GetTrackingOriginMode().ToString();
                Debug.Log($"New Space:{currentSpace}");
                inputSubsystem.TryRecenter();
                return;
            }
        }
        Debug.LogError("SetSpace failed to set Tracking Mode Origin to " + TrackingOriginModeFlags.Unbounded.ToString());
    }

    }
