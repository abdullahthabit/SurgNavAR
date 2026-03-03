using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class PVArucoTracking : MonoBehaviour
{
#if WINDOWS_UWP
    const int ARRAY_SIZE = 18;                  //number of elements passed to unity for each detected marker: 16 TRS(4x4) + markerid + DETECTION FLAG
    const int INDEX_OF_MARKER_ID = 16;          // either 1 for pointer or 2 for patient
    const int INDEX_OF_DETECTION_STATUS = 17;   // either 1 for detected marker or 0 for undetected marker
    const int MARKER_DETECTED = 1;
    const int STATUS_EXTENDED_TRACKED = 1;

    Dictionary<int, GameObject> mArucoMarkers;
    List<int> mPreviousTrackingStatus;
    Aruco mArucoConfig;
    float[] mMarkersPoses = null;
    int mNumberOfMarkers;

    bool isArucoInitialized = false;

    // Start is called before the first frame update
    void Start()
    {
        mArucoMarkers = new Dictionary<int, GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Config.Instance.IsArucoTrackingActive)
        {
            updateMarkerPoses();
        }
    }

    private void Awake()
    {
        InitializePVArucoTracking();
    }

    public void InitializePVArucoTracking()
    {
        if(isArucoInitialized) { return; }
        NativePlugin.Instance.InitializeNativePlugin();
        isArucoInitialized = true;
        Debug.Log("Aruco PV tracking: initialized successfully");
    }

    public void StartPVArucoTracking()
    {
        // previously was run at InitializePVArucoTracking(), what's the effect of moving it here?!
        mArucoConfig = Config.Instance.configFile.nativeConfig.aruco;
        if (Config.Instance.IsArucoTrackingActive) { return; }

        NativePlugin.Instance.StartPVCamera();

        if(mArucoMarkers.Count > 0)
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
        
        Config.Instance.IsArucoTrackingActive = true;
        Debug.Log("Aruco PV tracking: started  successfully");
   
    }

    public void StopPVArucoTracking()
    {
        if(!Config.Instance.IsArucoTrackingActive) { return; }

        Config.Instance.IsArucoTrackingActive = false;
        NativePlugin.Instance.StopPVCamera();

        for(int i=0;i<mArucoMarkers.Count;i++)
        {
            mArucoMarkers[i].SetActive(false);
        }

        Debug.Log("Aruco PV tracking: stopped");
    }

    void CreateMarkersGameObjects()
    {
        mPreviousTrackingStatus = new List<int>();

        foreach ( var arucoMarker in mArucoConfig.marker.Select((Value, Index) => new {Value, Index}))
        {
            GameObject markerGameObject = new GameObject("Aruco" + arucoMarker.Value.name);
            mArucoMarkers.Add(arucoMarker.Index, markerGameObject);
            markerGameObject.AddComponent<MarkerTrackingStatus>();

            CreateMarkerOutline(markerGameObject, mArucoConfig.markerSize, arucoMarker.Value.outlineColor.ToColor());
            EnableChildrenRendererComponenets(markerGameObject, false);
            mPreviousTrackingStatus.Add(2);

            //// check if tool and if tip is calibrated --> create a tooltip gameobject
            //if (arucoMarker.Value.isTool && arucoMarker.Value.toolCalibration.tipOffset.x != 0f)
            //{
            //    GameObject tooltip = new GameObject("tooltip");
            //    tooltip.transform.parent = markerGameObject.gameObject.transform;
            //    tooltip.transform.localPosition = arucoMarker.Value.toolCalibration.tipOffset;
            //    tooltip.transform.localRotation = Quaternion.identity;
            //    tooltip.transform.localScale = Vector3.one;
            //    // show a sphere at the tip
            //    GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //    tipSphere.transform.parent = tooltip.gameObject.transform;
            //    tipSphere.transform.localPosition = Vector3.zero;
            //    tipSphere.transform.localRotation = Quaternion.identity;
            //    tipSphere.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            //    tipSphere.SetActive(true);
            //    tipSphere.GetComponent<Renderer>().material.color = arucoMarker.Value.outlineColor.ToColor();
            //}
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

    float[] GetArucoMarkerPoses()
    {
        return NativePlugin.Instance.GetMarkerPoses();
    }

    void updateMarkerPoses()
    {
        mMarkersPoses = GetArucoMarkerPoses();
        if (mMarkersPoses == null) { return; } 

        mNumberOfMarkers = mMarkersPoses.Length / ARRAY_SIZE;
        if(mNumberOfMarkers != mArucoConfig.numberOfMarkers) { return; }

        for (int i=0; i<mNumberOfMarkers; i++)
        {
            int markerIndex = i * ARRAY_SIZE;
            int markerId = (int) mMarkersPoses[markerIndex + INDEX_OF_MARKER_ID];
            int markerTrackingStatus = (int) mMarkersPoses[markerIndex + INDEX_OF_DETECTION_STATUS];

            // TODO: change initialization of marker id to -1 instead of 0
            if (markerId > 0) // a marker was detected
            {
                Matrix4x4 markerTRS = GetTransformFromMarkersArray(mMarkersPoses, markerIndex);
                mArucoMarkers[markerId-1].transform.localPosition = new Vector3(markerTRS.m03, markerTRS.m13, markerTRS.m23);
                mArucoMarkers[markerId-1].transform.localRotation = markerTRS.GetRotation();

                UpdateMarkerTrackingStatus(markerId-1, markerTrackingStatus); 
            }
        }
    }

    Matrix4x4 GetTransformFromMarkersArray(float[] markersArray, int markerIndex)
    {
        Matrix4x4 transform = new Matrix4x4(new Vector4(markersArray[markerIndex + 0], markersArray[markerIndex + 1], markersArray[markerIndex + 2], markersArray[markerIndex + 3]),
                                            new Vector4(markersArray[markerIndex + 4], markersArray[markerIndex + 5], markersArray[markerIndex + 6], markersArray[markerIndex + 7]),
                                            new Vector4(markersArray[markerIndex + 8], markersArray[markerIndex + 9], markersArray[markerIndex + 10], markersArray[markerIndex + 11]),
                                            new Vector4(markersArray[markerIndex + 12], markersArray[markerIndex + 13], markersArray[markerIndex + 14], markersArray[markerIndex + 15]));

        return transform;
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
        if(currentTrackingStatus != mPreviousTrackingStatus[markerId])
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

        // testing local coordinates of aruco markers
        // createTestingSpheres(markerGameObject, markerSize);
    }

    void createTestingSpheres(GameObject markerParentGameObject, float markerSize)
    {
        GameObject xSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        xSphere.name = "xAxis";
        xSphere.transform.parent = markerParentGameObject.transform;
        xSphere.transform.localPosition = new Vector3(markerSize / 2f, 0, 0);
        xSphere.transform.localScale = 0.01f * Vector3.one;
        xSphere.GetComponent<Renderer>().material.color = Color.red;

        GameObject ySphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ySphere.name = "yAxis";
        ySphere.transform.parent = markerParentGameObject.transform;
        ySphere.transform.localPosition = new Vector3(0, markerSize / 2f, 0);
        ySphere.transform.localScale = 0.01f * Vector3.one;
        ySphere.GetComponent<Renderer>().material.color = Color.green;

        GameObject zSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        zSphere.name = "yAxis";
        zSphere.transform.parent = markerParentGameObject.transform;
        zSphere.transform.localPosition = new Vector3(0, 0, markerSize / 2f);
        zSphere.transform.localScale = 0.01f * Vector3.one;
        zSphere.GetComponent<Renderer>().material.color = Color.blue;
    }

#endif
}
