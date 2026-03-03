using System.Collections.Generic;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MagicLeapSupport;

public class MarkerTrackerExample : MonoBehaviour
{
#if UNITY_ANDROID
    [Tooltip("Set the XR Origin so that the marker appears relative to headset's origin. If null, the script will try to find the component automatically.")]
    public XROrigin XROrigin;

    [Tooltip("If Not Null, this is the object that will be created at the position of each detected marker.")]
    public GameObject MarkerPrefab;

    public MagicLeapMarkerUnderstandingFeature.ArucoType ArucoType =
        MagicLeapMarkerUnderstandingFeature.ArucoType.Dictionary_5x5_50;

    //public MagicLeapMarkerUnderstandingFeature.MarkerDetectorProfile DetectorProfile =
    //    MagicLeapMarkerUnderstandingFeature.MarkerDetectorProfile.Default;

    private MagicLeapMarkerUnderstandingFeature.MarkerDetectorSettings _detectorSettings;
    private MagicLeapMarkerUnderstandingFeature _markerFeature;
    private readonly Dictionary<string, GameObject> _markerObjectById = new Dictionary<string, GameObject>();

    private bool isMLMarkerDetectoryActive = false;

    float MARKER_SIZE = 0.08f;

    private void OnValidate()
    {
        // Automatically find the XROrigin component if it's present in the scene
        if (XROrigin == null)
        {
            XROrigin = FindAnyObjectByType<XROrigin>();
        }
    }

    private void Start()
    {
        
    }

    public void StartMLMarkerDetector()
    {
        Debug.Log("starting ML marker detection ....");

        _markerFeature = OpenXRSettings.Instance.GetFeature<MagicLeapMarkerUnderstandingFeature>();

        if (_markerFeature == null || _markerFeature.enabled == false)
        {
            Debug.LogError("The Magic Leap 2 Marker Understanding OpenXR Feature is missing or disabled enabled. Disabling Script.");
            this.enabled = false;
            return;
        }

        if (XROrigin == null)
        {
            Debug.LogError("No XR Origin Found, markers sample will not work. Disabling Script.");
            this.enabled = false;
        }

        // Configure a generic detector with QR and Aruco Detector settings 
        //_detectorSettings.QRSettings.EstimateQRLength = true;
        //_detectorSettings.ArucoSettings.EstimateArucoLength = true;
        _detectorSettings.ArucoSettings.ArucoLength = MARKER_SIZE;
        _detectorSettings.ArucoSettings.ArucoType = ArucoType;

        MagicLeapMarkerUnderstandingFeature.MarkerDetectorProfile DetectorProfile = MagicLeapMarkerUnderstandingFeature.MarkerDetectorProfile.LargeFOV;
        _detectorSettings.MarkerDetectorProfile = DetectorProfile;

        //_detectorSettings.MarkerDetectorProfile = MagicLeapMarkerUnderstandingFeature.MarkerDetectorProfile.Custom;
        ////Create the custom profile
        //MagicLeapMarkerUnderstandingFeature.CustomProfileSettings customProfileSettings = new MagicLeapMarkerUnderstandingFeature.CustomProfileSettings();
        //customProfileSettings.AnalysisInterval = MagicLeapMarkerUnderstandingFeature.MarkerDetectorFullAnalysisInterval.Max;
        //customProfileSettings.CameraHint = MagicLeapMarkerUnderstandingFeature.MarkerDetectorCamera.RGB;
        //customProfileSettings.CornerRefinement = MagicLeapMarkerUnderstandingFeature.MarkerDetectorCornerRefineMethod.None;
        //customProfileSettings.ResolutionHint = MagicLeapMarkerUnderstandingFeature.MarkerDetectorResolution.Low;
        //customProfileSettings.FPSHint = MagicLeapMarkerUnderstandingFeature.MarkerDetectorFPS.Low;
        //customProfileSettings.UseEdgeRefinement = false;
        //_detectorSettings.CustomProfileSettings = customProfileSettings;

        // We use the same settings on all 3 of the 
        // different detectors and target the specific marker by setting the Marker Type before creating the detector 

        // Create Aruco detector
        _detectorSettings.MarkerType = MagicLeapMarkerUnderstandingFeature.MarkerType.Aruco;
        _markerFeature.CreateMarkerDetector(_detectorSettings);

        //// Create QRCode Detector
        //_detectorSettings.MarkerType = MagicLeapMarkerUnderstandingFeature.MarkerType.QR;
        //_markerFeature.CreateMarkerDetector(_detectorSettings);

        //// Create UPCA Detector
        //_detectorSettings.MarkerType = MagicLeapMarkerUnderstandingFeature.MarkerType.UPCA;
        //_markerFeature.CreateMarkerDetector(_detectorSettings);

        isMLMarkerDetectoryActive = true;

        Debug.Log("ML marker detction started successfully ....");
    }

    public void DestroyMLMarkerDetecor()
    {
        Debug.Log("Stopping ML marker detection ....");
        if (_markerFeature != null)
        {
            _markerFeature.DestroyAllMarkerDetectors();

            isMLMarkerDetectoryActive = false;

            Debug.Log("ML marker detction stopped successfully ....");
        }
    }

    private void OnDestroy()
    {
        if (_markerFeature != null)
        {
            _markerFeature.DestroyAllMarkerDetectors();

            isMLMarkerDetectoryActive = false;
        }
    }

    void Update()
    {
        if (isMLMarkerDetectoryActive)
        {
            // Update the marker detector
            _markerFeature.UpdateMarkerDetectors();

            // Iterate through all of the marker detectors
            for (int i = 0; i < _markerFeature.MarkerDetectors.Count; i++)
            {
                // Verify that the marker detector is running
                if (_markerFeature.MarkerDetectors[i].Status == MagicLeapMarkerUnderstandingFeature.MarkerDetectorStatus.Ready)
                {
                    // Cycle through the detector's data and log it to the debug log
                    MagicLeapMarkerUnderstandingFeature.MarkerDetector currentDetector = _markerFeature.MarkerDetectors[i];
                    OnUpdateDetector(currentDetector);
                }
            }
        }
    }

    private void OnUpdateDetector(MagicLeapMarkerUnderstandingFeature.MarkerDetector detector)
    {

        for (int i = 0; i < detector.Data.Count; i++)
        {
            string id = "";
            float markerSize = .01f;
            var data = detector.Data[i];
            switch (detector.Settings.MarkerType)
            {
                case MagicLeapMarkerUnderstandingFeature.MarkerType.Aruco:
                    id = data.MarkerNumber.ToString();
                    markerSize = data.MarkerLength;
                    break;
                case MagicLeapMarkerUnderstandingFeature.MarkerType.QR:
                    id = data.MarkerString;
                    markerSize = data.MarkerLength;
                    break;
                case MagicLeapMarkerUnderstandingFeature.MarkerType.UPCA:
                    Debug.Log("No pose is given for marker type UPCA, value is " + id);
                    break;
            }

            if (!data.MarkerPose.HasValue)
            {
                //Do not create a marker until the pose is valid
                return;
            }

            if (!string.IsNullOrEmpty(id) && markerSize > 0)
            {
                // If the marker ID has not been tracked create a new marker object
                if (!_markerObjectById.ContainsKey(id))
                {
                    GameObject newMarker = new GameObject("Aruco");
                    _markerObjectById.Add(id, newMarker);
                    CreateMarkerOutline(newMarker, MARKER_SIZE, Color.green);

                    //// Create a primitive cube
                    //if (MarkerPrefab)
                    //{
                    //    GameObject newMarker = Instantiate(MarkerPrefab);
                    //    _markerObjectById.Add(id, newMarker);
                    //}
                    //else
                    //{
                    //    GameObject newDefaultMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    //    _markerObjectById.Add(id, newDefaultMarker);
                    //}

                }

                GameObject marker = _markerObjectById[id];
                SetTransformToMarkerPose(marker.transform, data.MarkerPose.Value, markerSize);
            }
        }
    }

    private void SetTransformToMarkerPose(Transform marker, Pose markerPose, float markerSize)
    {
        Transform originTransform = XROrigin.CameraFloorOffsetObject.transform;

        // Set the position of the marker. Since the pose is given relative to the XR Origin,
        // we need to transform it to world coordinates.
        marker.position = originTransform.TransformPoint(markerPose.position);
        marker.rotation = originTransform.rotation * markerPose.rotation;

        //// When marker size estimation is enabled, markers may take a few frames to scale to their appropriate size.
        //if (marker.transform.localScale.x != markerSize)
        //{
        //    marker.localScale = new Vector3(markerSize, markerSize, markerSize);
        //}
    }

    void CreateMarkerOutline(GameObject markerGameObject, float markerSize, Color color)
    {
        // empty placeholder
        GameObject markerOutline = new GameObject("markerOutline");
        markerOutline.transform.parent = markerGameObject.transform;
        markerOutline.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // top side cylinder
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.transform.parent = markerOutline.transform;
        top.transform.localPosition = new Vector3(0f, 0f, markerSize / 2f);
        top.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
        top.transform.localScale = new Vector3(0.001f, 0.001f, markerSize + 0.002f);
        top.transform.gameObject.SetActive(true);
        top.GetComponent<Renderer>().material.color = color;

        // buttom side cylinder
        GameObject buttom = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        buttom.transform.parent = markerOutline.transform;
        buttom.transform.localPosition = new Vector3(0f, 0f, -markerSize / 2f);
        buttom.transform.localRotation = Quaternion.Euler(0f, 90f, 90f);
        buttom.transform.localScale = new Vector3(0.001f, 0.001f, markerSize + 0.002f);
        buttom.transform.gameObject.SetActive(true);
        buttom.GetComponent<Renderer>().material.color = color;

        // left side cylinder
        GameObject left = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        left.transform.parent = markerOutline.transform;
        left.transform.localPosition = new Vector3(-markerSize / 2f, 0f, 0f);
        left.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        left.transform.localScale = new Vector3(0.001f, 0.001f, markerSize + 0.002f);
        left.transform.gameObject.SetActive(true);
        left.GetComponent<Renderer>().material.color = color;

        // right side cylinder
        GameObject right = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        right.transform.parent = markerOutline.transform;
        right.transform.localPosition = new Vector3(markerSize / 2f, 0f, 0f);
        right.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        right.transform.localScale = new Vector3(0.001f, 0.001f, markerSize + 0.002f);
        right.transform.gameObject.SetActive(true);
        right.GetComponent<Renderer>().material.color = color;

        //// testing local coordinates of aruco markers
        //createTestingSpheres(markerGameObject, markerSize);
    }
#endif
}