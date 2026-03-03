using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System.IO;
using System.Linq;
using System;

// The class needs to be renamed to tooltip calibration
public class PivotCalibration : MonoBehaviour
{
    // Dropdown tools definitions
    public GameObject pivotCalibrationDropDown;
    TMP_Dropdown dropDownMenu;
    List<string> toolsNames;
    Dictionary<int, string> tools;

    // assigned tool for calibration
    GameObject toolToCalibrate;
    // shows up when pivot calibration is in progress (maybe also let it show when the other methods of calibration are running as well
    GameObject recordingSpehere;
    // could be either vuforia or aruco
    string trackingModeActive;
    // accumlate poses to save them to desk for the pivot calibration method
    List<Matrix4x4> toolCalibrationPoses;
    List<Matrix4x4> ReferenceCalibrationPoses;
    // used to collect poses for pivot calibration
    bool recordToolCalibrationPoses = false;

    // assigned calibration tool marker, can also be assinged for patient marker (test it?!)
    GameObject referenceMarker;
    GameObject referenceMarkerOrientation;
    // accumulate the tooltip positions estiamted multiple times with different methods (to be saved to desk)
    List<Vector3> tooltipPositions;
    List<Quaternion> tooltipOrientations;
    // accumulate the method used to estiamte the tooltip position (goes together with the tooltippositions) maybe use dictionary instead?!
    List<string> tooltipCalibrationMethods;
    List<string> markerTrackingMethods;

    // host the dropdown menu for the diameter of the calibration tool (rename variables accordingly) also in the menu on the scene
    public GameObject drillCalibrationDropDown;
    TMP_Dropdown drillDropDownMenu;
    List<Vector3> tooltipCalibrationToolOffsets;

    // is activated when reference calibration method is activated
    bool collectReferenceTooltipPosesActive = false;
    bool collectTooltipPositionAndOrientationActive = false;
    // accumulate the tooltip poses across multiple frames for the reference calibration method
    List<Vector3> referenceToolTipPoses;
    List<Vector3> positionOfrecordedReferenceTooltips;
    List<Quaternion> orientationOfrecordedReferenceTooltips;

    List<Vector3> positionOfrecordedToolTooltips;
    List<Quaternion> orientationOfrecordedToolTooltips;
    // how many frames to collect the poses from for the reference calibraiton method, maybe set it through the config file
    int referenceTooltipCounter = 60*5; // 5 seconds
    // linked to the collected tooltips for the reference calibration method, to be destroyed after calculation (find a better way?!)
    List<GameObject> recordedReferenceToolTips;
    // to use predefined calibration offsets (perhaps not useful now, delete?)
    bool isDrillCalibrated = false;

    // to access public variables in other game objects, change method to the one of the private patient (seems safer)
    public GameObject scriptsGameObject;
    private Patient patientScript;

    AudioSource beep;

    GameObject tooltipCalibCylinder;
    GameObject tooltipToolCalibCylinder;

    int recordingFileCounterVuforia = 1;
    int recordingFileCounterAruco = 1;

    public bool skipPoseRecording = false;

    public GameObject diskPrefab;

    // Start is called before the first frame update
    void Start()
    {
        // tools to calibrate
        tools = new Dictionary<int, string>();
        toolsNames = new List<string>();
        // related to pivot calibration
        toolCalibrationPoses = new List<Matrix4x4>();
        dropDownMenu = pivotCalibrationDropDown.GetComponent<TMP_Dropdown>();

        // for saving to disk all tooltips and methods
        tooltipPositions = new List<Vector3>();
        tooltipOrientations = new List<Quaternion>();
        tooltipCalibrationMethods = new List<string>();
        markerTrackingMethods = new List<string>();

        // related to calibration tool
        tooltipCalibrationToolOffsets = initialiseCalibrationTool(tooltipCalibrationToolOffsets);
        drillDropDownMenu = drillCalibrationDropDown.GetComponent<TMP_Dropdown>();

        // related to reference marker tooltip calibraiton
        referenceToolTipPoses = new List<Vector3>();
        recordedReferenceToolTips = new List<GameObject>();

        // to access the trajectories for planning cylinders
        patientScript = this.gameObject.GetComponent<Patient>();

        positionOfrecordedReferenceTooltips = new List<Vector3>();
        orientationOfrecordedReferenceTooltips = new List<Quaternion>();

        positionOfrecordedToolTooltips = new List<Vector3>();
        orientationOfrecordedToolTooltips = new List<Quaternion>();
        ReferenceCalibrationPoses = new List<Matrix4x4>();

        beep = scriptsGameObject.GetComponent<AudioSource>();
    }

    // Update is called once per frame
    void Update()
    {
        // activated with pivot calibration
        if(recordToolCalibrationPoses)
        {
            if (Config.Instance.configFile.unityConfig.pivotCalibrationTimeInSeconds > 0)
                referenceTooltipCounter = 60 * Config.Instance.configFile.unityConfig.pivotCalibrationTimeInSeconds;

            // only collect the pose when the marker is tracked
            if (toolToCalibrate.GetComponent<MarkerTrackingStatus>().Tracked)
            {
                skipPoseRecording = false;
                recordingSpehere.SetActive(true);
                toolCalibrationPoses.Add(toolToCalibrate.transform.localToWorldMatrix);
            }
            else
            {
                skipPoseRecording = true;
                recordingSpehere.SetActive(false);
            }

            if(toolCalibrationPoses.Count == referenceTooltipCounter)
            {
                FindToolTipFromCalibrationPoses();
                //scriptsGameObject.GetComponent<NDIconnection>().stopCollectingDataForCalibration = true;
                beep.Play(0);
                skipPoseRecording = false;
            }
        }

        // activated with reference marker calibration
        if(collectReferenceTooltipPosesActive)
        {
            if (Config.Instance.configFile.unityConfig.calibrationTimeInSeconds > 0)
                referenceTooltipCounter = 60 * Config.Instance.configFile.unityConfig.calibrationTimeInSeconds;

            if (toolToCalibrate.GetComponent<MarkerTrackingStatus>().Tracked && referenceMarker.GetComponent<MarkerTrackingStatus>().Tracked)
            {
                skipPoseRecording = false;
                //locateToolTipWithReferenceMarker();
                locateTooltipOrientationWithToolMarker();
                toolCalibrationPoses.Add(toolToCalibrate.transform.localToWorldMatrix);
                ReferenceCalibrationPoses.Add(referenceMarker.transform.localToWorldMatrix);

                recordingSpehere.SetActive(true);
            }
            else
            {
                skipPoseRecording = true;
                recordingSpehere.SetActive(false);
            }
            

            if (positionOfrecordedToolTooltips.Count == referenceTooltipCounter && orientationOfrecordedToolTooltips.Count == referenceTooltipCounter)
            {
                collectReferenceTooltipPosesActive = false;
                computeToolTipToolCentroid();

                //scriptsGameObject.GetComponent<NDIconnection>().stopCollectingDataForCalibration = true;

                //computeToolTipCentroid();
                //referenceToolTipPoses.Clear();

                recordMarkerPosesOverColibrationPoses("tool");
                positionOfrecordedToolTooltips.Clear();
                orientationOfrecordedToolTooltips.Clear();

                if (recordingSpehere)
                    GameObject.Destroy(recordingSpehere);

                beep.Play(0);
                skipPoseRecording = false;
            }
        }

        if (collectTooltipPositionAndOrientationActive)
        {
            if (Config.Instance.configFile.unityConfig.calibrationTimeInSeconds > 0)
                referenceTooltipCounter = 60 * Config.Instance.configFile.unityConfig.calibrationTimeInSeconds;

            if (toolToCalibrate.GetComponent<MarkerTrackingStatus>().Tracked && referenceMarkerOrientation.GetComponent<MarkerTrackingStatus>().Tracked)
            {
                skipPoseRecording = false;
                locateTooltipOrientationWithReferenceMarker();

                toolCalibrationPoses.Add(toolToCalibrate.transform.localToWorldMatrix);
                ReferenceCalibrationPoses.Add(referenceMarkerOrientation.transform.localToWorldMatrix);

                recordingSpehere.SetActive(true);
            }
            else
            {
                skipPoseRecording = true;
                recordingSpehere.SetActive(false);
            }
           

            if (positionOfrecordedReferenceTooltips.Count == referenceTooltipCounter && orientationOfrecordedReferenceTooltips.Count == referenceTooltipCounter)
            {
                collectTooltipPositionAndOrientationActive = false;
                computeToolTipPositionOrientationCentroid();

                //scriptsGameObject.GetComponent<NDIconnection>().stopCollectingDataForCalibration = true;

                recordMarkerPosesOverColibrationPoses("reference");

                positionOfrecordedReferenceTooltips.Clear();
                orientationOfrecordedReferenceTooltips.Clear();

                if(recordingSpehere)
                    GameObject.Destroy(recordingSpehere);

                print("finished collecting poses!!");
                beep.Play(0);
                skipPoseRecording = false;
            }

        }
    }

    public void ListToolsForCalibration()
    {
        // initially clear the drop down menu
        dropDownMenu.options.Clear();
        toolsNames.Clear();
        tools.Clear();

        if (Config.Instance.IsVuforiaTrackingActive)
        {
            trackingModeActive = "Vuforia";

            foreach (var vuforiaMarkerConfig in Config.Instance.configFile.unityConfig.vuforia.imageTargets.Select((Value, Index) => new { Value, Index }))
            {
                if (vuforiaMarkerConfig.Value.isTool == true)
                {
                    var toolName = vuforiaMarkerConfig.Value.name.Substring(0, vuforiaMarkerConfig.Value.name.IndexOf("."));
                    tools.Add(vuforiaMarkerConfig.Index, toolName);
                    toolsNames.Add(toolName);
                }
                else if (vuforiaMarkerConfig.Value.isCalibrationTool == true)
                {
                    var referenceMarkerName = vuforiaMarkerConfig.Value.name.Substring(0, vuforiaMarkerConfig.Value.name.IndexOf("."));
                    referenceMarker = GameObject.Find(trackingModeActive + referenceMarkerName);
                }
            }
            if (toolsNames.Count > 0)
                dropDownMenu.AddOptions(toolsNames);
            else
                Debug.Log("No tools to calibrate were found! please check the config file");
        }
        else if (Config.Instance.IsArucoTrackingActive)
        {
            trackingModeActive = "Aruco";

            foreach (var arucoMarkerConfig in Config.Instance.configFile.nativeConfig.aruco.marker.Select((Value, Index) => new { Value, Index }))
            {
                if (arucoMarkerConfig.Value.isTool == true)
                {
                    var toolName = arucoMarkerConfig.Value.name;
                    tools.Add(arucoMarkerConfig.Index, toolName);
                    toolsNames.Add(toolName);
                }
                else if (arucoMarkerConfig.Value.isCalibrationTool == true)
                {
                    var referenceMarkerName = arucoMarkerConfig.Value.name.Substring(0, arucoMarkerConfig.Value.name.IndexOf("."));
                    referenceMarker = GameObject.Find(trackingModeActive + referenceMarkerName);
                }
            }
            if (toolsNames.Count > 0)
                dropDownMenu.AddOptions(toolsNames);
            else
                Debug.Log("No tools to calibrate were found! please check the config file");
        }
        else
        {
            Debug.Log("Please activate either Vuforia or Aruco tracking first");
            return;
        }

        // set dropdown value initially to 0 ==> select first element
        dropDownMenu.value = 0;
        // assign first tool for calibration by default
        toolToCalibrate = GameObject.Find(trackingModeActive + toolsNames[dropDownMenu.value]);
        Debug.Log("Tool set for calibration: " + toolToCalibrate.name);
    }

    public void ChooseToolForCalibration()
    {
        toolToCalibrate = GameObject.Find(trackingModeActive + toolsNames[dropDownMenu.value]);
        Debug.Log("Tool set for calibration: " + toolToCalibrate.name);
    }

    public void CollectToolCalibrationPoses()
    {
        if (toolToCalibrate)
        {
            recordToolCalibrationPoses = true;
            Debug.Log("Starting to collect tool poses for calibration: " + toolToCalibrate.name);

            showRecoridingSphere();
            //// recording sphere
            //recordingSpehere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //recordingSpehere.transform.parent = toolToCalibrate.transform;
            //recordingSpehere.transform.localPosition = new Vector3(0f, 0f, 0f);
            //recordingSpehere.transform.localRotation = Quaternion.identity;
            //recordingSpehere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            //recordingSpehere.transform.gameObject.SetActive(true);
            //recordingSpehere.GetComponent<Renderer>().material.color = Color.blue;

            //scriptsGameObject.GetComponent<NDIconnection>().RecordPoseData(true, "pivot");
        }
    }

    void showRecoridingSphere()
    {
        if (toolToCalibrate)
        {
            // recording sphere
            recordingSpehere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            recordingSpehere.transform.parent = toolToCalibrate.transform;
            recordingSpehere.transform.localPosition = new Vector3(0f, 0f, 0f);
            recordingSpehere.transform.localRotation = Quaternion.identity;
            recordingSpehere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
            recordingSpehere.transform.gameObject.SetActive(true);
            recordingSpehere.GetComponent<Renderer>().material.color = Color.blue;
        }
        else { print("No tool was chosen for calibration"); }
        
    }

    void recordMarkerPosesOverColibrationPoses(string calibrationMethod)
    {
        if (Config.Instance.IsVuforiaTrackingActive)
        {
            DataWriter.WriteToolCalibrationDataToCSV($"{toolToCalibrate.name}_{calibrationMethod}_{recordingFileCounterVuforia}", toolCalibrationPoses);
            if(calibrationMethod == "reference")
                DataWriter.WriteToolCalibrationDataToCSV($"{referenceMarkerOrientation.name}_{calibrationMethod}_{recordingFileCounterVuforia}", ReferenceCalibrationPoses);
            if(calibrationMethod == "tool")
                DataWriter.WriteToolCalibrationDataToCSV($"{referenceMarker.name}_{calibrationMethod}_{recordingFileCounterVuforia}", ReferenceCalibrationPoses);
            recordingFileCounterVuforia++;
        }
        else
        {
            DataWriter.WriteToolCalibrationDataToCSV($"{toolToCalibrate.name}_{calibrationMethod}_{recordingFileCounterAruco}", toolCalibrationPoses);
            if (calibrationMethod == "reference")
                DataWriter.WriteToolCalibrationDataToCSV($"{referenceMarkerOrientation.name}_{calibrationMethod}_{recordingFileCounterAruco}", ReferenceCalibrationPoses);
            if (calibrationMethod == "tool")
                DataWriter.WriteToolCalibrationDataToCSV($"{referenceMarker.name}_{calibrationMethod}_{recordingFileCounterAruco}", ReferenceCalibrationPoses);
            recordingFileCounterAruco++;
        }
        
        // clear recorded data
        toolCalibrationPoses.Clear();
        ReferenceCalibrationPoses.Clear();
    }

    public void FindToolTipFromCalibrationPoses()
    {
        if (toolCalibrationPoses.Count > 0)
        {
            recordToolCalibrationPoses = false;
            // distroy recording sphere
            GameObject.Destroy(recordingSpehere);
            // perform pivot calibration
            RuntimePivotCalibration(toolCalibrationPoses);
            // write calibration data to file
            recordMarkerPosesOverColibrationPoses("pivot");

            Debug.Log("Calibration data saved to file: " + toolToCalibrate.name);

        }
        else
        {
            recordToolCalibrationPoses = false;
            Debug.Log("No calibration poses were found");
            // distroy recording sphere
            GameObject.Destroy(recordingSpehere);
            // clear recorded data
            toolCalibrationPoses.Clear();
            toolCalibrationPoses.Clear();

            GameObject tooltip = new GameObject("tooltip");
            tooltip.transform.parent = toolToCalibrate.transform;
            tooltip.transform.localPosition = new Vector3(-0.0016223438215128196f, -0.07903463282799648f, -0.004144789402918101f);
            tooltip.transform.localRotation = Quaternion.identity;
            tooltip.transform.localScale = Vector3.one;
            // show a sphere at the tip
            GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipSphere.transform.parent = tooltip.gameObject.transform;
            tipSphere.transform.localPosition = Vector3.zero;
            tipSphere.transform.localRotation = Quaternion.identity;
            tipSphere.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            tipSphere.SetActive(true);
            //tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //tipSphere.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            tooltipPositions.Add(tooltip.transform.localPosition);
            tooltipOrientations.Add(tooltip.transform.localRotation);
            tooltipCalibrationMethods.Add("invalid");
            markerTrackingMethods.Add(trackingModeActive);


            //if (Config.Instance.isVuforiaTrackingActive)
            //{
            //    int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
            //    Debug.Log(toolToCalibrate.name.Substring(7));
            //    Debug.Log(index);
            //    Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            //} 
            //else
            //{
            //    int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
            //    Debug.Log(toolToCalibrate.name.Substring(5));
            //    Debug.Log(index);
            //    Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            //}
                
        }
    }

    public List<Vector3> initialiseCalibrationTool(List<Vector3> tooltipCalibrationToolOffsets)
    {
        tooltipCalibrationToolOffsets = new List<Vector3>();
        //tooltipCalibrationToolOffsets.Add(new Vector3((-0.075f, -0.020f, -0.050f)
        //tooltipCalibrationToolOffsets.Add(new Vector3((-0.075f, -0.020f, -0.040f)
        //tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.035f, -0.030f));  //2.55
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.035f, -0.020f));
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.035f, -0.010f));
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.035f, 0));
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.045f, 0.010f));
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.045f, 0.020f));
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.045f, 0.030f));
        //(-0.075f, -0.045f, 0.040f)
        //(-0.075f, -0.045f, 0.050f)

        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, 0.00535f, -0.01f));
        tooltipCalibrationToolOffsets.Add(new Vector3(-0.075f, -0.040f, -0.01f));

        return tooltipCalibrationToolOffsets;
    }

    public void ShowCylincerForOrientationCalibration()
    {
        if(Config.Instance.IsArucoTrackingActive)
            referenceMarkerOrientation = GameObject.Find("ArucoPatientMarker");
        else if (Config.Instance.IsVuforiaTrackingActive)
            referenceMarkerOrientation = GameObject.Find("VuforiaPatientMarker");
        else { print("reference Patient marker cannot be found!!"); return; }

        bool tooltipAlreadyCreated = false;
        tooltipCalibCylinder = new GameObject("tooltip");
        foreach (Transform child in referenceMarkerOrientation.transform)
        {
            if (child.name == "tooltip")
            {
                tooltipAlreadyCreated = true;
                Destroy(tooltipCalibCylinder);
                tooltipCalibCylinder = child.gameObject;
            }
        }

        if(tooltipAlreadyCreated)
        {
            tooltipCalibCylinder.SetActive(true);
        }
        else
        {
            //GameObject tooltipCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //tooltipCylinder.transform.localScale = new Vector3(1, 17.875f, 1);      // based on the optical pointer shaft length (~143mm / 2 / 0.004)
            //tooltipCylinder.transform.localPosition = new Vector3(0, tooltipCylinder.transform.localScale.y, 0);

            // create hollow cylinder
            GameObject tooltipCylinder2 = GameObject.Instantiate(diskPrefab);
            tooltipCylinder2.transform.localScale = new Vector3(0.5f, 17.875f*5, 0.5f);      // based on the optical pointer shaft length (~143mm / 2 / 0.004)
            tooltipCylinder2.transform.localPosition = new Vector3(0, 17.875f, 0);

            GameObject tooltipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //tooltipCylinder.transform.parent = tooltipSphere.transform;
            tooltipCylinder2.transform.parent = tooltipSphere.transform;
            tooltipSphere.transform.localScale = 0.004f * Vector3.one;

            Vector3 tooltipLocalPosition = Vector3.zero;
            if (Config.Instance.IsArucoTrackingActive)
            {
                tooltipLocalPosition = Config.Instance.configFile.unityConfig.calibrationCylinderPoisitionAruco;
                tooltipSphere.transform.localRotation = Quaternion.Euler(90, 0, 0);
#if UNITY_ANDROID
                tooltipSphere.transform.localRotation = Quaternion.Euler(270, 0, 0);
#endif
            }
            else
            {
                tooltipLocalPosition = Config.Instance.configFile.unityConfig.calibrationCylinderPoisitionVuforia;
                tooltipSphere.transform.localRotation = Quaternion.identity;
            }

            tooltipSphere.transform.parent = tooltipCalibCylinder.transform;
            tooltipCalibCylinder.transform.parent = referenceMarkerOrientation.transform;
            //tooltip.transform.localPosition = Vector3.zero;

            tooltipCalibCylinder.transform.localPosition = tooltipLocalPosition;
            tooltipCalibCylinder.transform.localRotation = Quaternion.identity;
            tooltipCalibCylinder.transform.localScale = Vector3.one;

            //tooltipSphere.GetComponent<Renderer>().material.color = referenceMarkerOrientation.GetComponentInChildren<Renderer>().material.color;
            tooltipSphere.GetComponent<Renderer>().material.color = Color.green;
            tooltipCylinder2.transform.GetChild(0).GetComponent<Renderer>().material.color = referenceMarkerOrientation.GetComponentInChildren<Renderer>().material.color;

            //tooltipSphere.GetComponent<MeshRenderer>().enabled = false;
        }
    }

    public void ShowCylincerForCalibrationTool()
    {
        if (!referenceMarker)
            { print("calibration tool marker cannot be found!!"); return; }

        tooltipToolCalibCylinder = new GameObject("tooltip");
        foreach (Transform child in referenceMarker.transform)
        {
            if (child.name == "tooltip")
            {
                Destroy(child.gameObject);
                //tooltipToolCalibCylinder = child.gameObject;
            }
        }
        float drillDiameter = float.Parse(drillDropDownMenu.options[drillDropDownMenu.value].text) * 0.001f;

        GameObject tooltipCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        print("y: " + tooltipCalibrationToolOffsets[drillDropDownMenu.value].y);
        print("yd: " + tooltipCalibrationToolOffsets[drillDropDownMenu.value].y * drillDiameter);
        tooltipCylinder.transform.localScale = new Vector3(1, tooltipCalibrationToolOffsets[drillDropDownMenu.value].y/drillDiameter/2, 1);      // based on the optical pointer shaft length (~143mm / 2 / 0.004)
        tooltipCylinder.transform.localPosition = new Vector3(0, -tooltipCylinder.transform.localScale.y, 0);
        GameObject tooltipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        tooltipCylinder.transform.parent = tooltipSphere.transform;
        tooltipSphere.transform.localScale = drillDiameter * Vector3.one;

        Vector3 tooltipLocalPosition = Vector3.zero;
        if (Config.Instance.IsArucoTrackingActive)
        {
            tooltipLocalPosition = Config.Instance.configFile.unityConfig.calibrationCylinderPoisitionAruco;
            tooltipSphere.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        else
        {
            tooltipLocalPosition = tooltipCalibrationToolOffsets[drillDropDownMenu.value];
            tooltipSphere.transform.localRotation = Quaternion.identity;
        }

        tooltipSphere.transform.parent = tooltipToolCalibCylinder.transform;
        tooltipToolCalibCylinder.transform.parent = referenceMarker.transform;
        //tooltip.transform.localPosition = Vector3.zero;

        tooltipToolCalibCylinder.transform.localPosition = tooltipLocalPosition;
        tooltipToolCalibCylinder.transform.localRotation = Quaternion.identity;
        tooltipToolCalibCylinder.transform.localScale = Vector3.one;

        tooltipSphere.GetComponent<Renderer>().material.color = referenceMarker.GetComponentInChildren<Renderer>().material.color;
        tooltipCylinder.GetComponent<Renderer>().material.color = referenceMarker.GetComponentInChildren<Renderer>().material.color;
    }

    public void HideCylincerForOrientationCalibration()
    {
        if (tooltipCalibCylinder)
            tooltipCalibCylinder.SetActive(false);
    }

    public void CalibrateToolTipPositionOrientationWithMarker()
    {
        collectTooltipPositionAndOrientationActive = true;
        showRecoridingSphere();
        //scriptsGameObject.GetComponent<NDIconnection>().RecordPoseData(true, "marker_reference");
    }

    public void LoadCalibrationOfTooltip()
    {
        ToolCalibration tooltipPose;
        if (Config.Instance.IsVuforiaTrackingActive)
            tooltipPose = Config.Instance.configFile.unityConfig.vuforia.imageTargets[0].toolCalibration;
        else if (Config.Instance.IsArucoTrackingActive)
            tooltipPose = Config.Instance.configFile.nativeConfig.aruco.marker[0].toolCalibration;
        else
        {
            print("no tracking method is active");
            return;
        }

        if (tooltipPose.tipOffset.x != 0f)
        {
            print("created the tooltip");

            GameObject tooltip = new GameObject("tooltip");
            tooltip.transform.parent = toolToCalibrate.gameObject.transform;
            tooltip.transform.localPosition = tooltipPose.tipOffset;
            tooltip.transform.localRotation = tooltipPose.tipOrientation;
            tooltip.transform.localScale = Vector3.one;

            // show a sphere at the tip
            GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //cylinder.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);
            cylinder.transform.localScale = new Vector3(1.0f, 17.875f, 1.0f);
            cylinder.transform.localPosition = new Vector3(0, cylinder.transform.localScale.y, 0);

            //GameObject diskExtension = Instantiate(extensionDeskPrefab);

            cylinder.transform.parent = tipSphere.gameObject.transform;
            tipSphere.transform.parent = tooltip.gameObject.transform;
            tipSphere.transform.localPosition = Vector3.zero;
            tipSphere.transform.localRotation = Quaternion.identity;
            //tipSphere.transform.localScale = Config.Instance.configFile.unityConfig.vuforia.imageTargets[0].toolCalibration.diameter * Vector3.one;
            tipSphere.transform.localScale = 0.004f * Vector3.one;
            tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            cylinder.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

            tooltipPositions.Add(tooltip.transform.localPosition);
            tooltipOrientations.Add(tooltip.transform.localRotation);
            tooltipCalibrationMethods.Add("ndi_ground_truth");
            markerTrackingMethods.Add(trackingModeActive);

            //if (patientScript.trajectoriesList.Count > 0)
            //{
            //    GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

            //    print("traj:" + traj.name);
            //    Transform trajParent = traj.transform.parent;
            //    GameObject tooltipCylinder = GameObject.Find("VuforiaPointerMarker");
            //    foreach (Transform child in GameObject.Find("VuforiaPointerMarker").transform)
            //    {
            //        if (child.gameObject.name.Equals("tooltip"))
            //            tooltipCylinder = child.gameObject;
            //    }

            //    traj.transform.parent = tooltipCylinder.transform.GetChild(0);
            //    tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

            //    Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            //    Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
            //    Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

            //    // commented to prevent updating the trajectory scale based on the toolsize
            //    //traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);


            //    //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            //    //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            //    tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
            //    tooltipCylinder.transform.localRotation = traj.transform.localRotation;
            //    tooltipCylinder.transform.localScale = traj.transform.localScale;
            //    print("SCALE:" + traj.transform.localScale);
            //    GameObject disk1 = traj.transform.GetChild(0).gameObject;
            //    GameObject disk2 = traj.transform.GetChild(1).gameObject;
            //    GameObject disk3 = traj.transform.GetChild(2).gameObject;

            //    // to avoid dublicates
            //    if (GameObject.Find("toolDisk1"))
            //        Destroy(GameObject.Find("toolDisk1"));
            //    if (GameObject.Find("toolDisk2"))
            //        Destroy(GameObject.Find("toolDisk2"));
            //    if (GameObject.Find("toolDisk3"))
            //        Destroy(GameObject.Find("toolDisk3"));

            //    GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
            //    disk1Drill.name = "toolDisk1";
            //    GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
            //    disk2Drill.name = "toolDisk2";
            //    GameObject disk3Drill = Instantiate(disk3, tooltipCylinder.transform);
            //    disk3Drill.name = "toolDisk3";

            //    disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //    disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //    disk3Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

            //    tooltipCylinder.transform.localRotation = cylRot;
            //    //tooltipCylinder.transform.localScale = cylScale;
            //    tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);

            //    traj.transform.parent = trajParent;

            //    // for evd trajectories (makes it shorter (half) than planning
            //    // workaround to avoid having the disk getting shorter and being displaced
            //    disk1Drill.transform.parent = tooltipCylinder.transform.parent;
            //    disk2Drill.transform.parent = tooltipCylinder.transform.parent;
            //    disk3Drill.transform.parent = tooltipCylinder.transform.parent;
            //    tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
            //    tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);
            //    disk1Drill.transform.parent = tooltipCylinder.transform;
            //    disk2Drill.transform.parent = tooltipCylinder.transform;
            //    disk3Drill.transform.parent = tooltipCylinder.transform;

            //    // only activate after starting the insertion
            //    disk1Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
            //    disk2Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
            //    disk3Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
            //}

            CreateDisksForTooltipBasedOnPlanningTrajectories();

            if (scriptsGameObject.GetComponent<Patient>().isPatientaligned)
                scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();
        }
        else
        {
            print("no tooltip information was found");
        }
    }

    public void reloadTooltipForTargetTrajectory()
    {
        GameObject tooltipCylinder = GameObject.Find("VuforiaPointerMarker");
        if (tooltipCylinder == null)
        {
            Debug.Log("No VuforiaPointerMarker found!");
            return;
        }

        foreach (Transform child in tooltipCylinder.transform)
        {
            if (child.gameObject.name.Equals("tooltip"))
                tooltipCylinder = child.gameObject;
        }
        if (!tooltipCylinder.name.Equals("tooltip"))
        {
            Debug.Log("VuforiaPointerMarker is not calibrated, tooltip is not found!");
            return;
        }
        CreateDisksForTooltipBasedOnPlanningTrajectories();
    }

    void CreateDisksForTooltipBasedOnPlanningTrajectories()
    {
        if (patientScript.trajectoriesList.Count == 0) { return; }

        Debug.Log("Creating desks for tool augmentation ...");

        GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

        print("traj:" + traj.name);
        Transform trajParent = traj.transform.parent;
        GameObject tooltipCylinder = GameObject.Find("VuforiaPointerMarker");
        foreach (Transform child in GameObject.Find("VuforiaPointerMarker").transform)
        {
            if (child.gameObject.name.Equals("tooltip"))
                tooltipCylinder = child.gameObject;
        }

        traj.transform.parent = tooltipCylinder.transform.GetChild(0);
        tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

        Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
        Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

        // commented to prevent updating the trajectory scale based on the toolsize
        //traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);

        //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
        //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
        tooltipCylinder.transform.localRotation = traj.transform.localRotation;
        tooltipCylinder.transform.localScale = traj.transform.localScale;
        print("SCALE:" + traj.transform.localScale);

        // to avoid dublicates
        if (GameObject.Find("toolDisk1"))
            Destroy(GameObject.Find("toolDisk1"));
        if (GameObject.Find("toolDisk2"))
            Destroy(GameObject.Find("toolDisk2"));
        if (GameObject.Find("toolDisk3"))
            Destroy(GameObject.Find("toolDisk3"));


        int idx_1 = 0;
        int idx_2 = 0;
        int idx_3 = 0;

        if (Config.Instance.configFile.unityConfig.disk1Active)
        {
            GameObject disk1 = traj.transform.GetChild(idx_1).gameObject;
            Debug.Log("disk1 index ... " + idx_1);
            Debug.Log("name of trajectory gameobject ... " + traj.transform.GetChild(idx_1).gameObject.name);
            GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
            disk1Drill.name = "toolDisk1";
            disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

            idx_2++;
            idx_3++;
        }
       
        if (Config.Instance.configFile.unityConfig.disk2Active)
        {
            GameObject disk2 = traj.transform.GetChild(idx_2).gameObject;
            GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
            disk2Drill.name = "toolDisk2";
            disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

            idx_3++;
        }

        if (Config.Instance.configFile.unityConfig.disk3Active)
        {
            GameObject disk3 = traj.transform.GetChild(idx_3).gameObject;
            GameObject disk3Drill = Instantiate(disk3, tooltipCylinder.transform);
            disk3Drill.name = "toolDisk3";
            disk3Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        }

        tooltipCylinder.transform.localRotation = cylRot;
        tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);
        traj.transform.parent = trajParent;

        if (Config.Instance.configFile.unityConfig.disk1Active)
        {
            GameObject disk1Drill = GameObject.Find("toolDisk1");
            disk1Drill.transform.parent = tooltipCylinder.transform.parent;
        }
        if (Config.Instance.configFile.unityConfig.disk2Active)
        {
            GameObject disk2Drill = GameObject.Find("toolDisk2");
            disk2Drill.transform.parent = tooltipCylinder.transform.parent;
        }
        if (Config.Instance.configFile.unityConfig.disk3Active)
        {
            GameObject disk3Drill = GameObject.Find("toolDisk3");
            disk3Drill.transform.parent = tooltipCylinder.transform.parent;
        }

        // for evd trajectories (makes it shorter (half) than planning
        // workaround to avoid having the disk getting shorter and being displaced          
        tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
        tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);

        if (Config.Instance.configFile.unityConfig.disk1Active)
        {
            GameObject disk1Drill = GameObject.Find("toolDisk1");
            disk1Drill.transform.parent = tooltipCylinder.transform;
            disk1Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        }

        if (Config.Instance.configFile.unityConfig.disk2Active)
        {
            GameObject disk2Drill = GameObject.Find("toolDisk2");
            disk2Drill.transform.parent = tooltipCylinder.transform;
            disk2Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        }

        if (Config.Instance.configFile.unityConfig.disk3Active)
        {
            GameObject disk3Drill = GameObject.Find("toolDisk3");
            disk3Drill.transform.parent = tooltipCylinder.transform;
            disk3Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        }

    }

    public void RemoveTooltip()
    {
        // remove already created tooltip in order to replace it with the new one
        foreach (Transform child in toolToCalibrate.transform)
        {
            if (child.name == "tooltip")
            {
                GameObject.Destroy(child.gameObject);
            }
        }
    }

    public void CalibrateToolTipWithReferenceMarker()
    {
        if (drillDropDownMenu.value < 6)
        {
            collectReferenceTooltipPosesActive = true;
            showRecoridingSphere();
            //scriptsGameObject.GetComponent<NDIconnection>().RecordPoseData(true, "calibration_tool");
        }     
        else if (isDrillCalibrated)
        {
            GameObject tooltip = new GameObject("tooltipRelative");
            float drillDiameter = float.Parse(drillDropDownMenu.options[drillDropDownMenu.value].text.Substring(0, 4)) * 0.001f;
            // show a sphere at the tip
            GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);

            cylinder.transform.localPosition = new Vector3(0, cylinder.transform.localScale.y, 0);
            cylinder.transform.parent = tipSphere.gameObject.transform;
            tipSphere.transform.parent = tooltip.gameObject.transform;
            tipSphere.transform.localPosition = Vector3.zero;
            tipSphere.transform.localRotation = Quaternion.identity;
            tipSphere.transform.localScale = drillDiameter * Vector3.one;
            tipSphere.SetActive(true);
            tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            cylinder.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //tipSphere.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            //cylinder.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));


            foreach (Transform child in toolToCalibrate.transform)
            {
                if (child.name == "tooltip(Clone)")
                {
                    tooltip.transform.parent = child.gameObject.transform;

                    if (drillDropDownMenu.value == 6)
                        tooltip.transform.localPosition = new Vector3(0, 0.04034992f, 0);
                    else // drillDropDownMenu.value == 7
                        tooltip.transform.localPosition = new Vector3(0, -0.005000047f, 0);

                    tooltip.transform.parent = toolToCalibrate.transform;
                    tooltip.transform.localRotation = child.transform.localRotation;

                }
                if (child.name == "tooltip")
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
            tooltip.name = "tooltip";
            //patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex]
            GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

            print("traj:" + traj.name);
            Transform trajParent = traj.transform.parent;
            GameObject tooltipCylinder = GameObject.Find("VuforiaPointerMarker");
            foreach (Transform child in GameObject.Find("VuforiaPointerMarker").transform)
            {
                if (child.gameObject.name.Equals("tooltip"))
                    tooltipCylinder = child.gameObject;
            }

            traj.transform.parent = tooltipCylinder.transform.GetChild(0);
            tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

            Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
            Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

            traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
            tooltipCylinder.transform.localRotation = traj.transform.localRotation;
            tooltipCylinder.transform.localScale = traj.transform.localScale;
            print("SCALE:" + traj.transform.localScale);
            GameObject disk1 = traj.transform.GetChild(0).gameObject;
            GameObject disk2 = traj.transform.GetChild(1).gameObject;

            GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
            GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
            disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

            tooltipCylinder.transform.localRotation = cylRot;
            //tooltipCylinder.transform.localScale = cylScale;
            tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);



            traj.transform.parent = trajParent;

            if (Config.Instance.IsVuforiaTrackingActive)
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOrientation = tooltip.transform.localRotation;
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.diameter = drillDiameter;
            }
            else
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOrientation = tooltip.transform.localRotation;
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.diameter = drillDiameter;
            }
        }

        if (scriptsGameObject.GetComponent<Patient>().isPatientaligned)
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();      
    }


    public void CalibrateToolTipWithReferenceMarkerFromNDIConnect(int dropDownIndex, bool recalibrate=false )
    {
        drillDropDownMenu.value = dropDownIndex;
        dropDownMenu.value = 1;
        if (drillDropDownMenu.value < 6 && recalibrate)
        {
            collectReferenceTooltipPosesActive = true;
        }   
        else if (isDrillCalibrated)
        {
            GameObject tooltip = new GameObject("tooltipRelative");
            float drillDiameter = float.Parse(drillDropDownMenu.options[drillDropDownMenu.value].text.Substring(0, 4)) * 0.001f;
            // show a sphere at the tip
            GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);

            cylinder.transform.localPosition = new Vector3(0, cylinder.transform.localScale.y, 0);
            cylinder.transform.parent = tipSphere.gameObject.transform;
            tipSphere.transform.parent = tooltip.gameObject.transform;
            tipSphere.transform.localPosition = Vector3.zero;
            tipSphere.transform.localRotation = Quaternion.identity;
            tipSphere.transform.localScale = drillDiameter * Vector3.one;
            tipSphere.SetActive(true);
            tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            cylinder.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //tipSphere.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            //cylinder.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));


            foreach (Transform child in toolToCalibrate.transform)
            {
                if (child.name == "tooltip(Clone)")
                {
                    tooltip.transform.parent = child.gameObject.transform;

                    if (drillDropDownMenu.value == 6)
                        tooltip.transform.localPosition = new Vector3(0, 0.04034992f, 0);
                    else if (drillDropDownMenu.value == 7)
                        tooltip.transform.localPosition = new Vector3(0, -0.005000047f, 0);
                    else if (drillDropDownMenu.value == 1)
                        tooltip.transform.localPosition = Vector3.zero;

                    tooltip.transform.parent = toolToCalibrate.transform;
                    tooltip.transform.localRotation = child.transform.localRotation;

                }
                if (child.name == "tooltip")
                {
                    GameObject.Destroy(child.gameObject);
                }
            }
            tooltip.name = "tooltip";
            //patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex]
            GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

            print("traj:" + traj.name);
            Transform trajParent = traj.transform.parent;
            GameObject tooltipCylinder = GameObject.Find("VuforiaPointerMarker");
            foreach (Transform child in GameObject.Find("VuforiaPointerMarker").transform)
            {
                if (child.gameObject.name.Equals("tooltip"))
                    tooltipCylinder = child.gameObject;
            }

            traj.transform.parent = tooltipCylinder.transform.GetChild(0);
            tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

            Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
            Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

            traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
            tooltipCylinder.transform.localRotation = traj.transform.localRotation;
            tooltipCylinder.transform.localScale = traj.transform.localScale;
            print("SCALE:" + traj.transform.localScale);
            GameObject disk1 = traj.transform.GetChild(0).gameObject;
            GameObject disk2 = traj.transform.GetChild(1).gameObject;

            GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
            GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
            disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

            tooltipCylinder.transform.localRotation = cylRot;
            //tooltipCylinder.transform.localScale = cylScale;
            tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);



            traj.transform.parent = trajParent;

            if (Config.Instance.IsVuforiaTrackingActive)
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOrientation = tooltip.transform.localRotation;
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.diameter = drillDiameter;
            }
            else
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOrientation = tooltip.transform.localRotation;
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.diameter = drillDiameter;
            }
        }

        if(scriptsGameObject.GetComponent<Patient>().isPatientaligned)
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();
    }
    void computeToolTipCentroid()
    {
        //// remove already created tooltip in order to replace it with the new one
        //foreach (Transform child in toolToCalibrate.transform)
        //{
        //    if (child.name == "tooltip")
        //    {
        //        GameObject.Destroy(child.gameObject);
        //    }
        //}

        Vector3 sum = Vector3.zero;
        foreach(var position in referenceToolTipPoses)
        {
            sum.x += position.x;
            sum.y += position.y;
            sum.z += position.z;
        }
        GameObject tooltip = new GameObject("tooltip");

        float drillDiameter;
        if (drillDropDownMenu.value < 6)
        {
            drillDiameter = float.Parse(drillDropDownMenu.options[drillDropDownMenu.value].text) * 0.001f;
        }
        else
        {
            Debug.Log(drillDropDownMenu.options[drillDropDownMenu.value].text);
            Debug.Log(drillDropDownMenu.options[drillDropDownMenu.value].text.Substring(0, 4));
            drillDiameter = float.Parse(drillDropDownMenu.options[drillDropDownMenu.value].text.Substring(0, 4)) * 0.001f;
        }
            
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
        tipSphere.transform.localScale = drillDiameter * Vector3.one;
        tipSphere.SetActive(true);

        tooltip.transform.parent = referenceMarker.transform;
        tooltip.transform.localRotation = Quaternion.identity;
        tooltip.transform.localScale = Vector3.one;

        tooltip.transform.parent = toolToCalibrate.transform;
        tooltip.transform.localPosition = new Vector3(sum.x / referenceToolTipPoses.Count, sum.y / referenceToolTipPoses.Count, sum.z / referenceToolTipPoses.Count);


        foreach (var go in recordedReferenceToolTips)
        {
            GameObject.Destroy(go);
        }

        

        if (drillDropDownMenu.value == 1)
        {
            isDrillCalibrated = true;
            Debug.Log("dublicating");
            GameObject refToolTip = Instantiate(tooltip,toolToCalibrate.transform);
            Debug.Log(refToolTip.name);
            refToolTip.SetActive(false);
        }
        else
        {
            isDrillCalibrated = false;
            foreach (Transform child in toolToCalibrate.transform)
            {
                if (child.name == "tooltip(Clone)")
                {
                    GameObject.Destroy(child.gameObject);

                }
            }
        }

        Color newColor = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        tipSphere.GetComponent<Renderer>().material.color = newColor;
        cylinder.GetComponent<Renderer>().material.color = newColor;

        //tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //cylinder.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        tooltipPositions.Add(tooltip.transform.localPosition);
        tooltipOrientations.Add(tooltip.transform.localRotation);
        tooltipCalibrationMethods.Add("calibration_tool");
        markerTrackingMethods.Add(trackingModeActive);

        if (Config.Instance.IsVuforiaTrackingActive)
        {
            int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
            Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOrientation = tooltip.transform.localRotation;
            Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.diameter = drillDiameter;
        }
        else
        {
            int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
            Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOrientation = tooltip.transform.localRotation;
            Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.diameter = drillDiameter;
        }

        //if(patientScript.trajectoriesList.Count > 0)
        //{
        //    GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

        //    print("traj:" + traj.name);
        //    Transform trajParent = traj.transform.parent;
        //    GameObject tooltipCylinder = GameObject.Find("VuforiaPointerMarker");
        //    foreach (Transform child in GameObject.Find("VuforiaPointerMarker").transform)
        //    {
        //        if (child.gameObject.name.Equals("tooltip"))
        //            tooltipCylinder = child.gameObject;
        //    }

        //    traj.transform.parent = tooltipCylinder.transform.GetChild(0);
        //    tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

        //    Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        //    Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
        //    Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

        //    // commented to prevent updating the trajectory scale based on the toolsize
        //    //traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);


        //    //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
        //    //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        //    tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
        //    tooltipCylinder.transform.localRotation = traj.transform.localRotation;
        //    tooltipCylinder.transform.localScale = traj.transform.localScale;
        //    print("SCALE:" + traj.transform.localScale);
        //    GameObject disk1 = traj.transform.GetChild(0).gameObject;
        //    GameObject disk2 = traj.transform.GetChild(1).gameObject;
        //    GameObject disk3 = traj.transform.GetChild(2).gameObject;

        //    // to avoid dublicates
        //    if (GameObject.Find("toolDisk1"))
        //        Destroy(GameObject.Find("toolDisk1"));
        //    if (GameObject.Find("toolDisk2"))
        //        Destroy(GameObject.Find("toolDisk2"));
        //    if (GameObject.Find("toolDisk3"))
        //        Destroy(GameObject.Find("toolDisk3"));

        //    GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
        //    disk1Drill.name = "toolDisk1";
        //    GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
        //    disk2Drill.name = "toolDisk2";
        //    GameObject disk3Drill = Instantiate(disk3, tooltipCylinder.transform);
        //    disk3Drill.name = "toolDisk3";

        //    disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //    disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //    disk3Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        //    tooltipCylinder.transform.localRotation = cylRot;
        //    //tooltipCylinder.transform.localScale = cylScale;
        //    tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);

        //    traj.transform.parent = trajParent;

        //    // for evd trajectories (makes it shorter (half) than planning
        //    // workaround to avoid having the disk getting shorter and being displaced
        //    disk1Drill.transform.parent = tooltipCylinder.transform.parent;
        //    disk2Drill.transform.parent = tooltipCylinder.transform.parent;
        //    disk3Drill.transform.parent = tooltipCylinder.transform.parent;
        //    tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
        //    tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);
        //    disk1Drill.transform.parent = tooltipCylinder.transform;
        //    disk2Drill.transform.parent = tooltipCylinder.transform;
        //    disk3Drill.transform.parent = tooltipCylinder.transform;

        //    // only activate after starting the insertion
        //    disk1Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //    disk2Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //    disk3Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //}
        //

        CreateDisksForTooltipBasedOnPlanningTrajectories();

        if (scriptsGameObject.GetComponent<Patient>().isPatientaligned)
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();
    }

    void locateTooltipOrientationWithReferenceMarker()
    {
        GameObject ttp;
        foreach (Transform child in referenceMarkerOrientation.transform)
        {
            if (child.name == "tooltip")
            {
                ttp = child.gameObject;

                ttp.transform.parent = toolToCalibrate.transform;
                positionOfrecordedReferenceTooltips.Add(ttp.transform.localPosition);
                orientationOfrecordedReferenceTooltips.Add(ttp.transform.localRotation);
                ttp.transform.parent = referenceMarkerOrientation.transform;
            }
        }
    }

    void locateTooltipOrientationWithToolMarker()
    {
        GameObject ttp;
        foreach (Transform child in referenceMarker.transform)
        {
            if (child.name == "tooltip")
            {
                ttp = child.gameObject;

                ttp.transform.parent = toolToCalibrate.transform;
                positionOfrecordedToolTooltips.Add(ttp.transform.localPosition);
                orientationOfrecordedToolTooltips.Add(ttp.transform.localRotation);
                ttp.transform.parent = referenceMarker.transform;
            }
        }
    }

    void computeToolTipPositionOrientationCentroid()
    {
        //// remove already created tooltip in order to replace it with the new one
        //foreach (Transform child in toolToCalibrate.transform)
        //{
        //    if (child.name == "tooltip")
        //    {
        //        GameObject.Destroy(child.gameObject);
        //    }
        //}

        GameObject Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Cylinder.transform.localScale = new Vector3(1, 10, 1);
        Cylinder.transform.localPosition = new Vector3(0, Cylinder.transform.localScale.y, 0);
        GameObject Sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Cylinder.transform.parent = Sphere.transform;
        Sphere.transform.localScale = 0.001f * 3.05f * Vector3.one;

        if (Config.Instance.IsArucoTrackingActive)
        {
            Sphere.transform.localRotation = Quaternion.Euler(90, 0, 0);

#if UNITY_ANDROID
            Sphere.transform.localRotation = Quaternion.Euler(270, 0, 0);
#endif
        }
        else
        {
            Sphere.transform.localRotation = Quaternion.identity;
        }

        GameObject tooltip = new GameObject("tooltip");
        Sphere.transform.parent = tooltip.transform;
        tooltip.transform.parent = toolToCalibrate.transform;
        tooltip.transform.localPosition = Utils.computePointsCentroid(positionOfrecordedReferenceTooltips);
        tooltip.transform.localRotation = Utils.computeAverageQuaternion(orientationOfrecordedReferenceTooltips);
        //tooltip.transform.localPosition = positionOfrecordedReferenceTooltips[0];
        //tooltip.transform.localRotation = orientationOfrecordedReferenceTooltips[0];
        tooltip.transform.localScale = Vector3.one;

        Color newColor = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        Sphere.GetComponent<Renderer>().material.color = newColor;
        Cylinder.GetComponent<Renderer>().material.color = newColor;

        //Sphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //Cylinder.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        print($"first Position = {positionOfrecordedReferenceTooltips[0]} ---------- mean Position = {tooltip.transform.localPosition}");
        print($"first Rotation = {orientationOfrecordedReferenceTooltips[0]} ---------- mean Rotation = {tooltip.transform.localRotation}");

        tooltipPositions.Add(tooltip.transform.localPosition);
        tooltipOrientations.Add(tooltip.transform.localRotation);
        tooltipCalibrationMethods.Add("marker_reference");
        markerTrackingMethods.Add(trackingModeActive);


        //if (patientScript.trajectoriesList.Count > 0)
        //{
        //    GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

        //    print("traj:" + traj.name);
        //    Transform trajParent = traj.transform.parent;
        //    GameObject tooltipCylinder = toolToCalibrate;
        //    foreach (Transform child in toolToCalibrate.transform)
        //    {
        //        if (child.gameObject.name.Equals("tooltip"))
        //            tooltipCylinder = child.gameObject;
        //    }

        //    traj.transform.parent = tooltipCylinder.transform.GetChild(0);
        //    tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

        //    Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        //    Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
        //    Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

        //    // commented to prevent updating the trajectory scale based on the toolsize
        //    //traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);


        //    //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
        //    //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        //    tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
        //    tooltipCylinder.transform.localRotation = traj.transform.localRotation;
        //    tooltipCylinder.transform.localScale = traj.transform.localScale;
        //    print("SCALE:" + traj.transform.localScale);
        //    GameObject disk1 = traj.transform.GetChild(0).gameObject;
        //    GameObject disk2 = traj.transform.GetChild(1).gameObject;
        //    GameObject disk3 = traj.transform.GetChild(2).gameObject;

        //    // to avoid dublicates
        //    if (GameObject.Find("toolDisk1"))
        //        Destroy(GameObject.Find("toolDisk1"));
        //    if (GameObject.Find("toolDisk2"))
        //        Destroy(GameObject.Find("toolDisk2"));
        //    if (GameObject.Find("toolDisk3"))
        //        Destroy(GameObject.Find("toolDisk3"));

        //    GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
        //    disk1Drill.name = "toolDisk1";
        //    GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
        //    disk2Drill.name = "toolDisk2";
        //    GameObject disk3Drill = Instantiate(disk3, tooltipCylinder.transform);
        //    disk3Drill.name = "toolDisk3";

        //    disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //    disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //    disk3Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        //    tooltipCylinder.transform.localRotation = cylRot;
        //    //tooltipCylinder.transform.localScale = cylScale;
        //    tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);

        //    traj.transform.parent = trajParent;

        //    // for evd trajectories (makes it shorter (half) than planning
        //    // workaround to avoid having the disk getting shorter and being displaced
        //    disk1Drill.transform.parent = tooltipCylinder.transform.parent;
        //    disk2Drill.transform.parent = tooltipCylinder.transform.parent;
        //    disk3Drill.transform.parent = tooltipCylinder.transform.parent;
        //    tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
        //    tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);
        //    disk1Drill.transform.parent = tooltipCylinder.transform;
        //    disk2Drill.transform.parent = tooltipCylinder.transform;
        //    disk3Drill.transform.parent = tooltipCylinder.transform;

        //    // only activate after starting the insertion
        //    disk1Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //    disk2Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //    disk3Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //}

        CreateDisksForTooltipBasedOnPlanningTrajectories();

        if (scriptsGameObject.GetComponent<Patient>().isPatientaligned)
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();

    }

    void computeToolTipToolCentroid()
    {

        GameObject Cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Cylinder.transform.localScale = new Vector3(1, 10, 1);
        Cylinder.transform.localPosition = new Vector3(0, Cylinder.transform.localScale.y, 0);
        GameObject Sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Cylinder.transform.parent = Sphere.transform;
        Sphere.transform.localScale = 0.001f * 3.05f * Vector3.one;

        if (Config.Instance.IsArucoTrackingActive)
        {
            Sphere.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        else
        {
            Sphere.transform.localRotation = Quaternion.identity;
        }

        GameObject tooltip = new GameObject("tooltip");
        Sphere.transform.parent = tooltip.transform;
        tooltip.transform.parent = toolToCalibrate.transform;
        tooltip.transform.localPosition = Utils.computePointsCentroid(positionOfrecordedToolTooltips);
        tooltip.transform.localRotation = Utils.computeAverageQuaternion(orientationOfrecordedToolTooltips);
        tooltip.transform.localScale = Vector3.one;

        Color newColor = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        Sphere.GetComponent<Renderer>().material.color = newColor;
        Cylinder.GetComponent<Renderer>().material.color = newColor;

        //Sphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //Cylinder.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        tooltipPositions.Add(tooltip.transform.localPosition);
        tooltipOrientations.Add(tooltip.transform.localRotation);
        tooltipCalibrationMethods.Add("calibration_tool");
        markerTrackingMethods.Add(trackingModeActive);

        //if (patientScript.trajectoriesList.Count > 0)
        //{
        //    GameObject traj = patientScript.trajectoriesList[patientScript.selectedTrajectoryIndex];

        //    print("traj:" + traj.name);
        //    Transform trajParent = traj.transform.parent;
        //    GameObject tooltipCylinder = toolToCalibrate;
        //    foreach (Transform child in toolToCalibrate.transform)
        //    {
        //        if (child.gameObject.name.Equals("tooltip"))
        //            tooltipCylinder = child.gameObject;
        //    }

        //    traj.transform.parent = tooltipCylinder.transform.GetChild(0);
        //    tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

        //    Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        //    Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
        //    Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

        //    // commented to prevent updating the trajectory scale based on the toolsize
        //    //traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);


        //    //tooltipCylinder.transform.localScale = traj.transform.localScale;//new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
        //    //tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, traj.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
        //    tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
        //    tooltipCylinder.transform.localRotation = traj.transform.localRotation;
        //    tooltipCylinder.transform.localScale = traj.transform.localScale;
        //    print("SCALE:" + traj.transform.localScale);
        //    GameObject disk1 = traj.transform.GetChild(0).gameObject;
        //    GameObject disk2 = traj.transform.GetChild(1).gameObject;
        //    GameObject disk3 = traj.transform.GetChild(2).gameObject;

        //    // to avoid dublicates
        //    if (GameObject.Find("toolDisk1"))
        //        Destroy(GameObject.Find("toolDisk1"));
        //    if (GameObject.Find("toolDisk2"))
        //        Destroy(GameObject.Find("toolDisk2"));
        //    if (GameObject.Find("toolDisk3"))
        //        Destroy(GameObject.Find("toolDisk3"));

        //    GameObject disk1Drill = Instantiate(disk1, tooltipCylinder.transform);
        //    disk1Drill.name = "toolDisk1";
        //    GameObject disk2Drill = Instantiate(disk2, tooltipCylinder.transform);
        //    disk2Drill.name = "toolDisk2";
        //    GameObject disk3Drill = Instantiate(disk3, tooltipCylinder.transform);
        //    disk3Drill.name = "toolDisk3";

        //    disk1Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //    disk2Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
        //    disk3Drill.transform.GetChild(0).gameObject.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        //    tooltipCylinder.transform.localRotation = cylRot;
        //    //tooltipCylinder.transform.localScale = cylScale;
        //    tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);

        //    traj.transform.parent = trajParent;

        //    // for evd trajectories (makes it shorter (half) than planning
        //    // workaround to avoid having the disk getting shorter and being displaced
        //    disk1Drill.transform.parent = tooltipCylinder.transform.parent;
        //    disk2Drill.transform.parent = tooltipCylinder.transform.parent;
        //    disk3Drill.transform.parent = tooltipCylinder.transform.parent;
        //    tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
        //    tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);
        //    disk1Drill.transform.parent = tooltipCylinder.transform;
        //    disk2Drill.transform.parent = tooltipCylinder.transform;
        //    disk3Drill.transform.parent = tooltipCylinder.transform;

        //    // only activate after starting the insertion
        //    disk1Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //    disk2Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //    disk3Drill.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        //}

        CreateDisksForTooltipBasedOnPlanningTrajectories();

        if (scriptsGameObject.GetComponent<Patient>().isPatientaligned)
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();

    }

    void locateToolTipWithReferenceMarker()
    { 
        if (referenceMarker == null)
        {
            Debug.Log("There is no assigned reference marker for calibration");
            return;
        }
        //if (dropDownMenu.value == 0) // pointer
        if (false) // pointer
        {
            GameObject tooltip = new GameObject("tooltip");
            tooltip.transform.parent = referenceMarker.transform;
            tooltip.transform.localPosition = Vector3.zero;
            tooltip.transform.parent = toolToCalibrate.transform;
            tooltip.transform.localRotation = Quaternion.identity;
            tooltip.transform.localScale = Vector3.one;

            // show a sphere at the tip
            GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipSphere.transform.parent = tooltip.gameObject.transform;
            tipSphere.transform.localPosition = Vector3.zero;
            tipSphere.transform.localRotation = Quaternion.identity;
            tipSphere.transform.localScale = new Vector3(0.001f, 0.001f, 0.001f);
            tipSphere.SetActive(true);
            tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //tipSphere.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            tooltipPositions.Add(tooltip.transform.localPosition);
            tooltipCalibrationMethods.Add("marker");

            if (Config.Instance.IsVuforiaTrackingActive)
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            }
            else
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            }
        }
        else
        {
            GameObject tooltip = new GameObject("ttp");
            tooltip.transform.parent = referenceMarker.transform;
            tooltip.transform.localPosition = tooltipCalibrationToolOffsets[drillDropDownMenu.value];
            tooltip.transform.localRotation = Quaternion.identity;
            tooltip.transform.localScale = Vector3.one;

            //float drillDiameter = float.Parse(drillDropDownMenu.options[drillDropDownMenu.value].text) * 0.001f;
            //// show a sphere at the tip
            //GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            //cylinder.transform.localScale = new Vector3(1.0f, 10.0f, 1.0f);
            //cylinder.transform.localPosition = new Vector3(0, cylinder.transform.localScale.y, 0);
            //cylinder.transform.parent = tipSphere.gameObject.transform;
            //tipSphere.transform.parent = tooltip.gameObject.transform;
            //tipSphere.transform.localPosition = Vector3.zero;
            //tipSphere.transform.localRotation = Quaternion.identity;
            ////tipSphere.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            //tipSphere.transform.localScale = drillDiameter * Vector3.one;
            //tipSphere.SetActive(true);

            tooltip.transform.parent = toolToCalibrate.transform;

            referenceToolTipPoses.Add(tooltip.transform.localPosition);
            recordedReferenceToolTips.Add(tooltip);

            ////tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;
            //tipSphere.GetComponent<Renderer>().material.color = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
            //tooltipPositions.Add(tooltip.transform.localPosition);
            //tooltipCalibrationMethods.Add("marker");

            //if (Config.Instance.isVuforiaTrackingActive)
            //{
            //    int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
            //    Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            //}
            //else
            //{
            //    int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
            //    Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
            //}
        }
    }

    void RuntimePivotCalibration(List<Matrix4x4> markerCalibrationPoses)
    {
        int numberOfRows = 3 * markerCalibrationPoses.Count;
        int numberOfColumns = 6;
        double[,] A = new double[numberOfRows, numberOfColumns];
        double[] x = new double[numberOfColumns];
        double[] b = new double[numberOfRows];
        double[,] b2d = new double[numberOfRows, 1];

        double[,] u;
        double[] w;
        double[,] vt;

        for(int i=0; i<markerCalibrationPoses.Count;i++)
        {
            A[3 * i + 0, 0] = markerCalibrationPoses[i].m00;
            A[3 * i + 0, 1] = markerCalibrationPoses[i].m01;
            A[3 * i + 0, 2] = markerCalibrationPoses[i].m02;
            A[3 * i + 1, 0] = markerCalibrationPoses[i].m10;
            A[3 * i + 1, 1] = markerCalibrationPoses[i].m11;
            A[3 * i + 1, 2] = markerCalibrationPoses[i].m12;
            A[3 * i + 2, 0] = markerCalibrationPoses[i].m20;
            A[3 * i + 2, 1] = markerCalibrationPoses[i].m21;
            A[3 * i + 2, 2] = markerCalibrationPoses[i].m22;

            A[3 * i + 0, 3] = -1;
            A[3 * i + 0, 4] = 0;
            A[3 * i + 0, 5] = 0;
            A[3 * i + 1, 3] = 0;
            A[3 * i + 1, 4] = -1;
            A[3 * i + 1, 5] = 0;
            A[3 * i + 2, 3] = 0;
            A[3 * i + 2, 4] = 0;
            A[3 * i + 2, 5] = -1;

            b[3 * i + 0] = -markerCalibrationPoses[i].m03;
            b[3 * i + 1] = -markerCalibrationPoses[i].m13;
            b[3 * i + 2] = -markerCalibrationPoses[i].m23;

            b2d[3 * i + 0, 0] = -markerCalibrationPoses[i].m03;
            b2d[3 * i + 1, 0] = -markerCalibrationPoses[i].m13;
            b2d[3 * i + 2, 0] = -markerCalibrationPoses[i].m23;

        }
        alglib.rmatrixsvd(A, numberOfRows, numberOfColumns, 0, 0, 2, out w, out u, out vt);
        int rank = 0;
        for (int i = 0; i < w.Length; i++)
        {
            if(w[i] !=0)
            {
                rank++;
            }
        }
        Debug.Log("rank = " + rank);

        alglib.densesolverlsreport rep;
        alglib.rmatrixsolvels(A, b, 0.0, out x, out rep);
        Debug.Log(x[0]);
        Debug.Log(x[1]);
        Debug.Log(x[2]);

        double[,] residualMatrix = new double[numberOfRows, 1];
        double residualError = 0;
        alglib.rmatrixgemm(numberOfRows, 1, numberOfColumns, 1, A, 0, 0, 0, b2d, 0, 0, 0, 0, residualMatrix, 0, 0);
        for (int i=0;i<markerCalibrationPoses.Count;i++)
        {
            residualError += residualMatrix[i, 0] * residualMatrix[i, 0];
        }
        residualError /= (double)markerCalibrationPoses.Count;
        residualError = Math.Sqrt(residualError);
        Debug.Log("residual error = " + residualError);

        GameObject tooltip = new GameObject("tooltip");
        tooltip.transform.parent = toolToCalibrate.transform;
        tooltip.transform.localPosition = new Vector3((float)x[0], (float)x[1], (float)x[2]);
        tooltip.transform.localRotation = Quaternion.identity;
        tooltip.transform.localScale = Vector3.one;
        // show a sphere at the tip
        GameObject tipSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        tipSphere.transform.parent = tooltip.gameObject.transform;
        tipSphere.transform.localPosition = Vector3.zero;
        tipSphere.transform.localRotation = Quaternion.identity;
        tipSphere.transform.localScale = new Vector3(0.004f, 0.004f, 0.004f);
        tipSphere.SetActive(true);
        //tipSphere.GetComponent<Renderer>().material.color = toolToCalibrate.GetComponentInChildren<Renderer>().material.color;

        Color newColor = new Color(UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f), UnityEngine.Random.Range(0f, 1f));
        tipSphere.GetComponent<Renderer>().material.color = newColor;

        tooltipPositions.Add(tooltip.transform.localPosition);
        tooltipOrientations.Add(tooltip.transform.localRotation);
        tooltipCalibrationMethods.Add("pivot_calibration");
        markerTrackingMethods.Add(trackingModeActive);

        //if (Config.Instance.isVuforiaTrackingActive)
        //{
        //    int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
        //    Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
        //}
        //else
        //{
        //    int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
        //    Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltip.transform.localPosition;
        //}
    }

    List<Matrix4x4> ReadToolCalibrationDataFromCSV(string toolName)
    {
        string fileName = "calibrationPoses_" + toolName + ".csv";
        string filePath = System.IO.Path.Combine(Config.Instance.CalibrationFolder, fileName);
        Debug.Log("file path: " + filePath);

        StreamReader csvReader = new StreamReader(filePath);

        List<Matrix4x4> toolTransforms = new List<Matrix4x4>();

        Matrix4x4 registrationMatrix = Matrix4x4.identity;
        string row;

        while ((row = csvReader.ReadLine()) != null)
        {
            var values = row.Split(',');
            registrationMatrix.SetRow(0, new Vector4(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3])));
            registrationMatrix.SetRow(1, new Vector4(float.Parse(values[4]), float.Parse(values[5]), float.Parse(values[6]), float.Parse(values[7])));
            registrationMatrix.SetRow(2, new Vector4(float.Parse(values[8]), float.Parse(values[9]), float.Parse(values[10]), float.Parse(values[11])));
            registrationMatrix.SetRow(3, new Vector4(float.Parse(values[12]), float.Parse(values[13]), float.Parse(values[14]), float.Parse(values[15])));

            toolTransforms.Add(registrationMatrix);
        }
        csvReader.Close();

        Debug.Log("closing streamWriter...");

        return toolTransforms;
    }

    public void SaveToolTipPoses()
    {
        string fileName = "tooltipPoses.csv";
        string filePath = System.IO.Path.Combine(Config.Instance.CalibrationFolderOut, fileName);
        Debug.Log("file path: " + filePath);

        StreamWriter streamWriter = new StreamWriter(filePath);

        string header = "tooltip_p_x" + "," + "tooltip_p_y" + "," + "tooltip_p_z" + ","
            + "tooltip_o_x" + "," + "tooltip_o_y" + "," + "tooltip_o_z" + "," + "tooltip_o_w" + ","
            + "calibration_method" + "," + "tracking_method";

        streamWriter.WriteLine(header);

        foreach (var tipPosition in tooltipPositions.Select((Value, Index) => new { Value, Index }))
        {
            string tipData = tipPosition.Value.x + "," + tipPosition.Value.y + "," + tipPosition.Value.z + "," + tooltipOrientations[tipPosition.Index].x + "," + 
                             tooltipOrientations[tipPosition.Index].y + "," + tooltipOrientations[tipPosition.Index].z + "," + tooltipOrientations[tipPosition.Index].w + "," + 
                             tooltipCalibrationMethods[tipPosition.Index] + "," + markerTrackingMethods[tipPosition.Index];

            streamWriter.WriteLine(tipData);
        }
        streamWriter.Close();
        Debug.Log("closing streamWriter...");

        if (Config.Instance.configFile.unityConfig.shouldSaveTooltipCalibration && tooltipPositions.Count !=0 && tooltipPositions.Count != 0)
        {
            Debug.Log("Saviging tooltip to config file ...");

            if (Config.Instance.IsVuforiaTrackingActive)
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(7));
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOffset = tooltipPositions[tooltipPositions.Count-1];
                Config.Instance.configFile.unityConfig.vuforia.imageTargets[index].toolCalibration.tipOrientation = tooltipOrientations[tooltipOrientations.Count - 1];
            }
            else
            {
                int index = tools.Values.ToList().IndexOf(toolToCalibrate.name.Substring(5));
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOffset = tooltipPositions[tooltipPositions.Count - 1];
                Config.Instance.configFile.nativeConfig.aruco.marker[index].toolCalibration.tipOrientation = tooltipOrientations[tooltipOrientations.Count - 1];
            }

            Config.Instance.SaveConfigFile();
        }

    }
}
