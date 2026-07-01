using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.Timeline;
using UnityEngine.XR.OpenXR;
using MagicLeap.OpenXR.Features.MarkerUnderstanding;

public class MLArucoTracking : MonoBehaviour
{
#if UNITY_ANDROID
    const int MARKER_DETECTED = 1;
    const int STATUS_EXTENDED_TRACKED = 1;

    public XROrigin XROrigin;
    private MarkerDetectorSettings _detectorSettings;
    private MagicLeapMarkerUnderstandingFeature _markerFeature;

    Dictionary<int, GameObject> mArucoMarkers;
    List<int> mPreviousTrackingStatus;
    int mNumberOfMarkers;
    bool isArucoInitialized = false;
    Aruco mArucoConfig;

    private void OnValidate()
    {
        // Automatically find the XROrigin component if it's present in the scene
        if (XROrigin == null)
        {
            XROrigin = FindAnyObjectByType<XROrigin>();
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        mArucoMarkers = new Dictionary<int, GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Config.Instance.IsArucoTrackingActive)
        {
            updateMarkerPoses();
        }
    }

    private void Awake()
    {
        InitializeMLArucoTracking();
    }

    public void InitializeMLArucoTracking()
    {
        Debug.Log("Initializing ML marker detection ....");

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
            return;
        }

        isArucoInitialized = true;
        Debug.Log("Aruco ML tracking: initialized successfully");
    }

    public void StartMLArucoTracking()
    {
        if (Config.Instance.IsArucoTrackingActive) { return; }

        mArucoConfig = Config.Instance.configFile.nativeConfig.aruco;
        mNumberOfMarkers = mArucoConfig.numberOfMarkers;

        if (mArucoMarkers.Count > 0)
        {
            for (int i = 0; i < mArucoMarkers.Count; i++)
            {
                mArucoMarkers[i].SetActive(true);
            }
        }
        else
        {
            CreateMarkersGameObjects();
        }

        startArucoMLDetector();

        Config.Instance.IsArucoTrackingActive = true;
        Debug.Log("Aruco ML tracking: started successfully");
    }

    public void StopMLArucoTracking()
    {
        if (!Config.Instance.IsArucoTrackingActive) { return; }

        Config.Instance.IsArucoTrackingActive = false;

        DestroyMLMarkerDetecor();

        for (int i = 0; i < mArucoMarkers.Count; i++)
        {
            mArucoMarkers[i].SetActive(false);
        }

        Debug.Log("Aruco ML tracking: stopped");
    }

    void DestroyMLMarkerDetecor()
    {
        Debug.Log("Stopping ML marker detection ....");
        if (_markerFeature != null)
        {
            _markerFeature.DestroyAllMarkerDetectors();

            Debug.Log("ML marker detction stopped successfully ....");
        }
    }

    void updateMarkerPoses()
    {

        // Update the marker detector
        _markerFeature.UpdateMarkerDetectors();

        if(_markerFeature.MarkerDetectors.Count == 0) { return; }

        if (_markerFeature.MarkerDetectors[0].Status == MarkerDetectorStatus.Ready)
        {
            MarkerDetector markerDetector = _markerFeature.MarkerDetectors[0];

            List<int> idsOfDetectedMarkers = new List<int>();

            for (int i = 0; i < markerDetector.Data.Count; i++)
            {
                var data = markerDetector.Data[i];
                int id = (int)data.MarkerNumber;
                var markerPose = data.MarkerPose;

                if (!markerPose.HasValue) { Debug.Log($"marker id:{id} --- no valid pose");  return; }
                if(id >= mNumberOfMarkers) { Debug.Log($"id:{id} --- is larger than the assigned ids of aruco markers in config file"); continue; }

                Transform originTransform = XROrigin.CameraFloorOffsetObject.transform;
                mArucoMarkers[id].transform.localPosition = originTransform.TransformPoint(markerPose.Value.position);
                mArucoMarkers[id].transform.localRotation = originTransform.rotation * markerPose.Value.rotation;


                // for detected markers update status to found
                UpdateMarkerTrackingStatus(id, MARKER_DETECTED);
                idsOfDetectedMarkers.Add(id);
            }
            if(idsOfDetectedMarkers.Count < mArucoMarkers.Count)
            {
                for (int i = 0;i < mArucoMarkers.Count;i++)
                {
                    if(!idsOfDetectedMarkers.Contains(i))
                    {
                        UpdateMarkerTrackingStatus(i, 0);
                    }
                }
            }
        }
    }

    void UpdateMarkerTrackingStatus(int markerId, int markerTrackingStatus)
    {
        int currentTrackingStatus;

        if (markerTrackingStatus == MARKER_DETECTED)
        {
            currentTrackingStatus = 0;
        }
        else // marker not in frame
        {
            if (mArucoConfig.marker[markerId].trackingStatusFiliter == STATUS_EXTENDED_TRACKED)
            {
                currentTrackingStatus = 1;
            }
            else // marker not in frame and trackingStatusFilter == Tracked
            {
                currentTrackingStatus = 2;
            }
        }
        // detect if there is a change in tracking status
        if (currentTrackingStatus != mPreviousTrackingStatus[markerId])
        {
            if (currentTrackingStatus == 0)
            {
                mArucoMarkers[markerId].GetComponent<MarkerTrackingStatus>().Tracked = true;
                mArucoMarkers[markerId].GetComponent<MarkerTrackingStatus>().ExtendedTracked = false;

                if (mPreviousTrackingStatus[markerId] == 2)
                    EnableChildrenRendererComponenets(mArucoMarkers[markerId], true);
                UpdateMarkerOutlineWithTrckingStatus(mArucoMarkers[markerId], markerId, true);
            }
            else if (currentTrackingStatus == 1)
            {
                mArucoMarkers[markerId].GetComponent<MarkerTrackingStatus>().Tracked = false;
                mArucoMarkers[markerId].GetComponent<MarkerTrackingStatus>().ExtendedTracked = true;

                if (mPreviousTrackingStatus[markerId] == 2)
                    EnableChildrenRendererComponenets(mArucoMarkers[markerId], true);
                UpdateMarkerOutlineWithTrckingStatus(mArucoMarkers[markerId], markerId, false);
            }
            else // currentTrackingStatus == 2
            {
                mArucoMarkers[markerId].GetComponent<MarkerTrackingStatus>().Tracked = false;
                mArucoMarkers[markerId].GetComponent<MarkerTrackingStatus>().ExtendedTracked = false;

                EnableChildrenRendererComponenets(mArucoMarkers[markerId], false);
            }


            mPreviousTrackingStatus[markerId] = currentTrackingStatus;
        }
    }

    void UpdateMarkerOutlineWithTrckingStatus(GameObject markerGameObject, int markerKey, bool markerFound)
    {
        if (markerFound)
        {
            // child(0) -> access the markerOutline gameobject
            var rendererComponents = markerGameObject.transform.GetChild(0).GetComponentsInChildren<Renderer>(true);
            foreach (var component in rendererComponents)
                component.material.color = Config.Instance.configFile.nativeConfig.aruco.marker[markerKey].outlineColor.ToColor();
        }
        else // marker not in frame
        {
            var rendererComponents = markerGameObject.transform.GetChild(0).GetComponentsInChildren<Renderer>(true);
            foreach (var component in rendererComponents)
                component.material.color = Color.red;
        }
    }

    void startArucoMLDetector()
    {
        _detectorSettings.AprilTagSettings.AprilTagLength = mArucoConfig.markerSize;
        _detectorSettings.AprilTagSettings.AprilTagType = AprilTagType.Dictionary_16H5;

        MarkerDetectorProfile DetectorProfile = MarkerDetectorProfile.Default;
        _detectorSettings.MarkerDetectorProfile = DetectorProfile;

        // Create Aruco detector
        _detectorSettings.MarkerType = MarkerType.AprilTag;
        _markerFeature.CreateMarkerDetector(_detectorSettings);
    }



    void CreateMarkersGameObjects()
    {
        mPreviousTrackingStatus = new List<int>();

        foreach (var arucoMarker in mArucoConfig.marker.Select((Value, Index) => new { Value, Index }))
        {
            GameObject markerGameObject = new GameObject("Aruco" + arucoMarker.Value.name);
            mArucoMarkers.Add(arucoMarker.Index, markerGameObject);
            markerGameObject.AddComponent<MarkerTrackingStatus>();

            CreateMarkerOutline(markerGameObject, mArucoConfig.markerSize, arucoMarker.Value.outlineColor.ToColor());
            EnableChildrenRendererComponenets(markerGameObject, false);
            mPreviousTrackingStatus.Add(2);
        }
    }

    void EnableChildrenRendererComponenets(GameObject parentGameObject, bool enable)
    {
        var rendererComponents = parentGameObject.GetComponentsInChildren<Renderer>(true);
        foreach (var component in rendererComponents)
            component.enabled = enable;
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
    }

#endif
}
