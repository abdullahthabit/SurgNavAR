using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Microsoft.MixedReality.OpenXR;


#if WINDOWS_UWP
using HL2NavigationNative;
#endif

public class NativePlugin : MonoBehaviour
{
    // Get a singelton instance at runtime
    public static NativePlugin Instance { get; private set; }
    [System.NonSerialized] public float[] markersPoses;

#if WINDOWS_UWP
    HL2NativeAPI nativeAPI;
    Windows.Perception.Spatial.SpatialCoordinateSystem unityWorldOrigin;
#endif


    // Start is called before the first frame update
    void Start()
    {

    }

    private void Awake()
    {
        // If there is an instance, and it's not this instance, delete this instance.
        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }

#if WINDOWS_UWP // requires the OpenXR plugin (unity 2020.3 or newer)
        unityWorldOrigin = Microsoft.MixedReality.OpenXR.PerceptionInterop.GetSceneCoordinateSystem(Pose.identity) as Windows.Perception.Spatial.SpatialCoordinateSystem;
#endif
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InitializeNativePlugin()
    {
        // TODO: check if operations are successful and return 1 otherwise 0
#if WINDOWS_UWP
        nativeAPI = new HL2NativeAPI(unityWorldOrigin);
        nativeAPI.SetConfigParams(Config.Instance.ConfigData);
#endif
        Debug.Log("Native plugin: initialized successfully");
    }

    public void StartPVCamera()
    {
#if WINDOWS_UWP
        nativeAPI.StartPVCamera();
#endif
    }

    public void StopPVCamera()
    {
#if WINDOWS_UWP
        nativeAPI.StopPVCamera();
#endif
    }

    public float[] GetMarkerPoses()
    {
#if WINDOWS_UWP
        markersPoses = nativeAPI.GetPVCameraMarkerPose();
#else
        markersPoses = null;
#endif
        return markersPoses;
    }
}
