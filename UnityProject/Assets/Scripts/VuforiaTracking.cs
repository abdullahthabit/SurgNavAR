using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuforia;
using System.Linq;

public class VuforiaTracking : MonoBehaviour
{
    Dictionary<int, GameObject> vuforiaMarkers;
    bool imageTargetsAlreadyCreated = false;
    // Start is called before the first frame update
    void Start()
    {
        // callback for when vuforia engine is initialized
        VuforiaApplication.Instance.OnVuforiaInitialized += OnVuforiaInitialized;
        // create image target from file when vuforia engine is started
        VuforiaApplication.Instance.OnVuforiaStarted += OnVuforiaStarted;

        vuforiaMarkers = new Dictionary<int, GameObject>();
    }

    private void Update()
    {
        if(vuforiaMarkers.Count > 0)
        {
            foreach (var vuforiaMarker in vuforiaMarkers)
            {
                UpdateMarkerOutlineWithTrckingStatus(vuforiaMarker);
            }
        }  
    }

    void UpdateMarkerOutlineWithTrckingStatus(KeyValuePair<int, GameObject> vuforiaMarker)
    {
        if (vuforiaMarker.Value.GetComponent<MarkerTrackingStatus>().ExtendedTracked)
        {
            // child(0) -> access the markerOutline gameobject
            var rendererComponents = vuforiaMarker.Value.transform.GetChild(0).GetComponentsInChildren<Renderer>(true);
            foreach (var component in rendererComponents)
                component.material.color = Color.red;
        }
        else // tracking status == Tracked
        {
            var rendererComponents = vuforiaMarker.Value.transform.GetChild(0).GetComponentsInChildren<Renderer>(true);
            foreach (var component in rendererComponents)
                component.material.color = Config.Instance.configFile.unityConfig.vuforia.imageTargets[vuforiaMarker.Key].outlineColor.ToColor();
        }
    }

    public void InitializeVuforiaEngine()
    {
        if(VuforiaApplication.Instance.IsInitialized) { return; }
        VuforiaConfiguration.Instance.Vuforia.MaxSimultaneousImageTargets = Config.Instance.configFile.unityConfig.vuforia.numberOfImageTargets;
        VuforiaApplication.Instance.Initialize();
    }

    public void StartVuforiaEgnie()
    {
        if (Config.Instance.IsVuforiaTrackingActive) { return; }

        VuforiaBehaviour.Instance.enabled = true;
        Config.Instance.IsVuforiaTrackingActive = true;

        for (int i = 0; i < vuforiaMarkers.Count; i++)
        {
            vuforiaMarkers[i].SetActive(true);
        }
    }

    private void OnDestroy()
    {
        //Debug.Log("destroying vuforia image targets ... ");
        //StopVuforiaEngine();
        //if (imageTargetsAlreadyCreated)
        //{
        //    for (int i = 0; i < vuforiaMarkers.Count; i++)
        //    {
        //        GameObject.Destroy(vuforiaMarkers[i]);
        //    }

        //}
        //vuforiaMarkers.Clear();
        ////VuforiaBehaviour.Instance.enabled = false;
        ////VuforiaBehaviour.Instance.ObserverFactory.Equals(null);

        //// Optionally destroy target GameObjects if you’ll rebuild them
        //foreach (var target in FindObjectsOfType<ImageTargetBehaviour>())
        //    Destroy(target.gameObject);

        //StartVuforiaEgnie();
        //StopVuforiaEngine();
        VuforiaApplication.Instance.Deinit();
    }

    public void StopVuforiaEngine()
    {
        if (!Config.Instance.IsVuforiaTrackingActive) { return; }

        Config.Instance.IsVuforiaTrackingActive = false;
        VuforiaBehaviour.Instance.enabled = false;

        for(int i=0; i < vuforiaMarkers.Count; i++)
        {
            vuforiaMarkers[i].SetActive(false);
        } 

    }

    void OnVuforiaInitialized(VuforiaInitError error)
    {
        if (error != VuforiaInitError.NONE)
        {
            Debug.LogError("Vuforia Initialization error: " + error);   
        }
        else
        {
            Debug.Log("Vuforia Initialized successfully");
            Debug.Log("Vuforia Maximum number of tracked markers: " + VuforiaConfiguration.Instance.Vuforia.MaxSimultaneousImageTargets);
        }
    }
    void OnVuforiaStarted()
    {
        Debug.Log("Starting Vuforia engine: creating target markers ... ");
        CreateImageTargetFromLocalImage();    
    }

    void CreateImageTargetFromLocalImage()
    {
        if (imageTargetsAlreadyCreated) { return; }

        foreach (var imageTargetConfig in Config.Instance.configFile.unityConfig.vuforia.imageTargets.Select((Value, Index) => new {Value, Index}))
        {
            string imageTargetPath = System.IO.Path.Combine(Config.Instance.MarkerTrackingFolder, imageTargetConfig.Value.name);

            if (System.IO.File.Exists(imageTargetPath))
            {
                string imageTargetName = "Vuforia" + imageTargetConfig.Value.name.Substring(0, imageTargetConfig.Value.name.IndexOf("."));
                float imageTargetWidth = imageTargetConfig.Value.widthInMeters;
                int imageTargetStatusFilter = imageTargetConfig.Value.trackingStatusFiliter;
                // TODO: handle failure of creation of image target
                var imageTarget = VuforiaBehaviour.Instance.ObserverFactory.CreateImageTarget(imageTargetPath, imageTargetWidth, imageTargetName);

                vuforiaMarkers.Add(imageTargetConfig.Index, imageTarget.gameObject);

                // workaround to use a custom image target observer --- fix the translatiional shift of the image target in the HL
                // TODO: find a cleaner way of customizing the image target observer behaviour
                imageTarget.gameObject.AddComponent<DefaultObserverEventHandler>();
                imageTarget.gameObject.GetComponent<DefaultObserverEventHandler>().enabled = false;
                imageTarget.gameObject.AddComponent<CustomObserverEventHandler>();
                imageTarget.gameObject.AddComponent<MarkerTrackingStatus>();

                // set tracking status filter
                imageTarget.gameObject.GetComponent<CustomObserverEventHandler>().StatusFilter = (CustomObserverEventHandler.TrackingStatusFilter) imageTargetStatusFilter;
               
                Debug.Log("Vufroia Instant image target created " + imageTarget.TargetName);
                CreateMarkerOutline(imageTarget.gameObject, imageTargetWidth, imageTargetConfig.Value.outlineColor.ToColor());

                //if(imageTargetConfig.Value.isTool && imageTargetConfig.Value.toolCalibration.tipOffset.x != 0f)
                if(false)
                {
                    GameObject tooltip = new GameObject("tooltip");
                    tooltip.transform.parent = imageTarget.gameObject.transform;
                    tooltip.transform.localPosition = imageTargetConfig.Value.toolCalibration.tipOffset;
                    tooltip.transform.localRotation = imageTargetConfig.Value.toolCalibration.tipOrientation;
                    tooltip.transform.localScale = Vector3.one;
                    //// show a sphere at the tip
                    //GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    //tipSphere.transform.parent = tooltip.gameObject.transform;
                    //tipSphere.transform.localPosition = Vector3.zero; 
                    //tipSphere.transform.localRotation = Quaternion.identity; 
                    //tipSphere.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
                    //tipSphere.SetActive(true);
                    //tipSphere.GetComponent<Renderer>().material.color = imageTargetConfig.Value.outlineColor.ToColor();

                    // show a sphere at the tip
                    GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    cylinder.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);
                    cylinder.transform.localPosition = new Vector3(0, cylinder.transform.localScale.y, 0);

                    //GameObject diskExtension = Instantiate(extensionDeskPrefab);

                    cylinder.transform.parent = tipSphere.gameObject.transform;
                    tipSphere.transform.parent = tooltip.gameObject.transform;
                    tipSphere.transform.localPosition = Vector3.zero;
                    tipSphere.transform.localRotation = Quaternion.identity;
                    tipSphere.transform.localScale = imageTargetConfig.Value.toolCalibration.diameter * Vector3.one;
                    tipSphere.SetActive(true);
                }
                //if (imageTargetName == "VuforiaPointerMarker")
                //{
                //    GameObject ndiSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                //    ndiSphere.name = "ndiPoseInVuforiaMarker";
                //    ndiSphere.transform.parent = imageTarget.gameObject.transform;

                //    Matrix4x4 markerCalibrationWithNdi = new Matrix4x4(new Vector4(-0.006294882636393079f, 0.013515500674433467f, -0.9998888466695255f, 0.0f),
                //                                                      (new Vector4(0.006630480694542459f, 0.999887241221531f, 0.013473736235948552f, 0.0f)),
                //                                                      (new Vector4(0.999958204715754f, -0.006544928106251582f, -0.006383787099415334f, 0.0f)),
                //                                                      (new Vector4(113.9630202677547f, 8.278335444866087f, -46.58464597288513f, 1.0f)));


                //    //// working well :)
                //    //Matrix4x4 markerCalibrationWithNdi = new Matrix4x4(new Vector4(0.9991236582748115f, -0.041617527666752904f, -0.004460590371707162f, 0.0f),
                //    //                                                  (new Vector4(0.04180189326635135f, 0.997563702018988f, 0.055850354819998824f, 0.0f)),
                //    //                                                  (new Vector4(0.002125369357470982f, -0.05598787194632646f, 0.9984291867729107f, 0.0f)),
                //    //                                                   (new Vector4(114.74074796130458f, 10.941982252062461f, -46.472335077145175f, 1.0f)));

                //    Quaternion rotation = markerCalibrationWithNdi.inverse.GetRotation();
                //    ndiSphere.transform.localPosition = 0.001f * new Vector3(markerCalibrationWithNdi.inverse.m03, markerCalibrationWithNdi.inverse.m13, markerCalibrationWithNdi.inverse.m23);
                //    ndiSphere.transform.localRotation = rotation;
                //    ndiSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                //}
            }
            else { Debug.LogError("Vuforia Image target file doesn't exist: " + imageTargetPath); }

        }

        imageTargetsAlreadyCreated = true;
    }

    void CreateMarkerOutline(GameObject markerGameObject, float markerSize, Color color)
    {
        // empty placeholder
        GameObject markerOutline = new GameObject("markerOutline");
        markerOutline.transform.parent = markerGameObject.transform;

        // top side cylinder
        GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        top.transform.parent = markerOutline.transform;
        top.transform.localPosition = new Vector3(0f, 0f, markerSize/2f);
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

}
