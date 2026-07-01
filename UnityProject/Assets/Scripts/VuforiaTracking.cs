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

    }

}
