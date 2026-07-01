using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Dummiesman;
using MixedReality.Toolkit.SpatialManipulation;
using MixedReality.Toolkit.UX;
using TMPro;
using System.IO;
using System;
using MixedReality.Toolkit.Input;

public class Patient : MonoBehaviour
{
    public Material transparentMaterial;
    public Material opaqueMaterial;
    public Material unlitMaterial;
    public GameObject ObjectsDropDown;
    public GameObject toolTipPrefab;

    GameObject patientContainer;
    Vector3 patientContainerCenter = Vector3.zero;
    string patientMarkerName = "";
    string pointerMarkerName = "";
    string trackingModeActive = "";
    Dictionary<int, GameObject> patientOverlayModels;
    List<string> modelsNames;
    TMP_Dropdown dropDownMenu;
    GameObject targetGameObject;
    Renderer targetRenderer;

    bool sliderRedInitialValue = false;
    bool sliderGreenInitialValue = false;
    bool sliderBlueInitialValue = false;
    bool sliderAlphaInitialValue = false;

    public LandmarksBasedRegistration landmarksBasedRegistration;
    List<GameObject> listOfLandmarks;
    public List<GameObject> listOfAcquiredTipPoints;
    GameObject patientMarker;
    GameObject pointerMarker;
    bool patientAttachedToMarker = false;

    bool refreshPressed = false;
    Matrix4x4 registrationMatrix;
    List<Vector3> intraopLandmarks;
    List<Vector3> preopLandmarks;

    public GameObject diskPrefab;
    public List<GameObject> trajectoriesList;
    public int selectedTrajectoryIndex = 0;
    public GameObject scriptsGameObject;

    public bool isPatientaligned = false;

    int registrationCounter = 0;

    public GameObject boundsControlVisualPrefab;

    List<GameObject> list_cubes;
    List<GameObject> list_shperes;
    List<GameObject> list_cylinders;
    float v_value = 500;
    GameObject widgets1DManipuation;
    Matrix4x4 boundingBoxWithHandles;   // workaround to fit the bounding box pose as was determined initially

    List<Vector3> registrationPointsOverMultipleFrames;
    bool useRecordedPointsForRegistration = false;
    int numberOfFramesToRecord = 100;
    int recordedFramesCounter = 0;
    AudioSource beep;

    // Start is called before the first frame update
    void Start()
    {
        patientOverlayModels = new Dictionary<int, GameObject>();
        dropDownMenu = ObjectsDropDown.GetComponent<TMP_Dropdown>();
        modelsNames = new List<string>();
        listOfLandmarks = new List<GameObject>();
        listOfAcquiredTipPoints = new List<GameObject>();
        trajectoriesList = new List<GameObject>();

        list_cubes = new List<GameObject>();
        list_shperes = new List<GameObject>();
        list_cylinders = new List<GameObject>();

        beep = scriptsGameObject.GetComponent<AudioSource>();
        registrationPointsOverMultipleFrames = new List<Vector3>();
    }

    // Update is called once per frame
    void Update()
    {
        if (useRecordedPointsForRegistration)
        {
            if (pointerMarker.GetComponent<MarkerTrackingStatus>().Tracked && patientMarker.GetComponent<MarkerTrackingStatus>().Tracked)
            {
                Matrix4x4 patientMarkerToWorld = Matrix4x4.TRS(patientMarker.transform.position, patientMarker.transform.rotation, patientMarker.transform.localScale);
                Vector3 tipInPatient = patientMarkerToWorld.inverse.MultiplyPoint(pointerMarker.transform.GetChild(1).position);
                registrationPointsOverMultipleFrames.Add(tipInPatient);

                recordedFramesCounter++;
            }

            if (recordedFramesCounter == numberOfFramesToRecord)
            {
                acquireRegistrationPoint();
                registrationPointsOverMultipleFrames.Clear();
                useRecordedPointsForRegistration = false;
                recordedFramesCounter = 0;
            }
        } 
    }

    public void RefreshButtonPressed()
    {
        refreshPressed = true;
        ActivatePatientModel();
    }

    public void deactivate1DManipulationHandles()
    {
        if (list_cubes.Count == 0 || widgets1DManipuation==null)
        {
            Debug.Log("1D manipulation handles has not yet initialized...");
            return;
        }

        widgets1DManipuation.SetActive(false);

        patientContainer.GetComponent<ObjectManipulator>().enabled = true;
        patientContainer.GetComponent<BoundsControl>().enabled = true;
    }

    public void activate1DManipulationHandles()
    {
        patientContainer.GetComponent<ObjectManipulator>().enabled = false;

        if(widgets1DManipuation!=null)
        {
            widgets1DManipuation.SetActive(true);
            return;
        }

        widgets1DManipuation = new GameObject("1DWidgets");
        widgets1DManipuation.transform.parent = patientContainer.transform;
        widgets1DManipuation.transform.localPosition = Vector3.zero;
        widgets1DManipuation.transform.localRotation = Quaternion.identity;
        widgets1DManipuation.transform.localScale = Vector3.one;

        GameObject x_translate_cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject y_translate_cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject z_translate_cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        GameObject x_rotate_sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GameObject y_rotate_sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        GameObject z_rotate_sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        GameObject x_axis_cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        GameObject y_axis_cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        GameObject z_axis_cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);

        x_translate_cube.name = "x_translate";
        y_translate_cube.name = "y_translate";
        z_translate_cube.name = "z_translate";

        x_rotate_sphere.name = "x_rotate";
        y_rotate_sphere.name = "y_rotate";
        z_rotate_sphere.name = "z_rotate";

        x_axis_cylinder.name = "x_axis";
        y_axis_cylinder.name = "y_axis";
        z_axis_cylinder.name = "z_axis";

        list_cubes.Add(x_translate_cube); list_cubes.Add(y_translate_cube); list_cubes.Add (z_translate_cube);
        list_shperes.Add(x_rotate_sphere); list_shperes.Add(y_rotate_sphere); list_shperes.Add (z_rotate_sphere);
        list_cylinders.Add(x_axis_cylinder); list_cylinders.Add(y_axis_cylinder); list_cylinders.Add (z_axis_cylinder);

        for (int i = 0; i < list_cubes.Count; i++)
        {
            list_cubes[i].transform.parent = widgets1DManipuation.transform;
            list_shperes[i].transform.parent = widgets1DManipuation.transform;
            list_cylinders[i].transform.parent = widgets1DManipuation.transform;

            list_cubes[i].transform.localScale = (v_value / 5) * Vector3.one;
            list_shperes[i].transform.localScale = (v_value / 5) * Vector3.one;
            list_cylinders[i].transform.localScale = new Vector3(10, v_value, 10);
            list_cylinders[i].transform.localPosition = new Vector3(0, 0, 0);

            list_cubes[i].gameObject.AddComponent<MeshRenderer>();
            list_shperes[i].gameObject.AddComponent<MeshRenderer>();
            list_cylinders[i].gameObject.AddComponent<MeshRenderer>();

            list_cubes[i].gameObject.AddComponent<ObjectManipulator>();
            list_shperes[i].gameObject.AddComponent<ObjectManipulator>();
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().HostTransform = patientContainer.transform;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().HostTransform = patientContainer.transform;

            list_cubes[i].gameObject.AddComponent<MoveAxisConstraint>();
            list_shperes[i].gameObject.AddComponent<MoveAxisConstraint>();
            list_cubes[i].gameObject.AddComponent<RotationAxisConstraint>();
            list_shperes[i].gameObject.AddComponent<RotationAxisConstraint>();
            list_cubes[i].gameObject.GetComponent<MoveAxisConstraint>().UseLocalSpaceForConstraint = true;
            list_cubes[i].gameObject.GetComponent<RotationAxisConstraint>().UseLocalSpaceForConstraint = true;
            list_shperes[i].gameObject.GetComponent<MoveAxisConstraint>().UseLocalSpaceForConstraint = true;
            list_shperes[i].gameObject.GetComponent<RotationAxisConstraint>().UseLocalSpaceForConstraint = true;

            list_cubes[i].gameObject.GetComponent<RotationAxisConstraint>().ConstraintOnRotation =
            MixedReality.Toolkit.AxisFlags.XAxis | MixedReality.Toolkit.AxisFlags.YAxis | MixedReality.Toolkit.AxisFlags.ZAxis;
            list_shperes[i].gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement =
            MixedReality.Toolkit.AxisFlags.XAxis | MixedReality.Toolkit.AxisFlags.YAxis | MixedReality.Toolkit.AxisFlags.ZAxis;


            if (i == 0)    // x-axis
            {
                list_cubes[i].transform.localPosition = new Vector3(v_value, 0, 0);
                list_shperes[i].transform.localPosition = new Vector3(-v_value, 0, 0);
                list_cylinders[i].transform.localRotation = Quaternion.Euler(0, 0, 90);

                list_cubes[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.red;
                list_shperes[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.red;
                list_cylinders[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.red;

                list_cubes[i].gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement = 
                    MixedReality.Toolkit.AxisFlags.YAxis | MixedReality.Toolkit.AxisFlags.ZAxis;

                list_shperes[i].gameObject.GetComponent<RotationAxisConstraint>().ConstraintOnRotation = 
                    MixedReality.Toolkit.AxisFlags.YAxis | MixedReality.Toolkit.AxisFlags.ZAxis; ;
            }
            else if (i == 1)    // y-axis
            {
                list_cubes[i].transform.localPosition = new Vector3(0, v_value, 0);
                list_shperes[i].transform.localPosition = new Vector3(0, -v_value, 0);
                list_cylinders[i].transform.localRotation = Quaternion.Euler(0, 0, 0);  // no rotation

                list_cubes[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.green;
                list_shperes[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.green;
                list_cylinders[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.green;

                list_cubes[i].gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement =
                    MixedReality.Toolkit.AxisFlags.XAxis | MixedReality.Toolkit.AxisFlags.ZAxis;

                list_shperes[i].gameObject.GetComponent<RotationAxisConstraint>().ConstraintOnRotation =
                    MixedReality.Toolkit.AxisFlags.XAxis | MixedReality.Toolkit.AxisFlags.ZAxis; ;
            }
            else   // i==2 z-axis
            {
                list_cubes[i].transform.localPosition = new Vector3(0, 0, v_value);
                list_shperes[i].transform.localPosition = new Vector3(0, 0, -v_value);
                list_cylinders[i].transform.localRotation = Quaternion.Euler(90, 0, 0);

                list_cubes[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.blue;
                list_shperes[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.blue;
                list_cylinders[i].gameObject.GetComponent<MeshRenderer>().material.color = Color.blue;

                list_cubes[i].gameObject.GetComponent<MoveAxisConstraint>().ConstraintOnMovement =
                                    MixedReality.Toolkit.AxisFlags.XAxis | MixedReality.Toolkit.AxisFlags.YAxis;

                list_shperes[i].gameObject.GetComponent<RotationAxisConstraint>().ConstraintOnRotation =
                    MixedReality.Toolkit.AxisFlags.XAxis | MixedReality.Toolkit.AxisFlags.YAxis; ;
            }

        }
    }

    public void ActivatePatientModel()
    {
        // if model is already activated, refresh its transfrom and return
        if (patientContainer != null)
        {
            if (refreshPressed)
            {
                DetachPatientModelFromMarker();
                patientContainer.transform.localScale = Vector3.one * Config.Instance.configFile.unityConfig.patientModel.scale;
                patientContainer.transform.position = Camera.main.transform.position + (Camera.main.transform.forward * 0.5f);
                foreach (Transform child in patientContainer.transform)
                {
                    if (child.name == "ribs") // special case to handle putting the ribcage inside the torso model
                        child.transform.localPosition = new Vector3(1, -190, -925);
                    else if (child.name == "1DWidgets")
                        child.transform.localPosition = Vector3.zero;
                    else if (child.name == "BoundingBoxWithHandles(Clone)")
                    {
                        child.transform.localPosition = boundingBoxWithHandles.GetPosition();
                        child.transform.localRotation = boundingBoxWithHandles.GetRotation();
                        child.transform.localScale = boundingBoxWithHandles.ExtractScale();
                    }

                    else
                        child.localPosition = patientContainerCenter; 
                }
                // reactivate bounds control and object manipulator
                // workaround: needed to deactivate and reactivate the bounding box to reposition it correctly (TODO: find a fix for this)
                patientContainer.GetComponent<ObjectManipulator>().enabled = false;
                patientContainer.GetComponent<BoundsControl>().enabled = false;
                patientContainer.GetComponent<ObjectManipulator>().enabled = true;
                patientContainer.GetComponent<BoundsControl>().enabled = true;
                patientContainer.SetActive(true);
                refreshPressed = false;

                // automatically prevent scalling after loading
                ScaleConstraintOn();
            }
            return;
        }

        // create a new container patient model
        patientContainer = new GameObject("PatientModel");

        // initialize container size to zero 
        float containerSize = 0;
        Vector3 containerSizeVector3 = Vector3.one;

        int numberOfObjModels = 0;
        foreach (var patientModelConfig in Config.Instance.configFile.unityConfig.patientModel.models.Select((Value, Index) => new { Value, Index }))
        {
            // load model from obj file
            string objFilePath = System.IO.Path.Combine(Config.Instance.PatientModelsFolder, patientModelConfig.Value.fileName);
            GameObject patientModel = new OBJLoader().Load(objFilePath);
            // set the model material based on config file
            patientModel.GetComponentInChildren<Renderer>().material = GetMaterialBasedOnConfig(patientModelConfig.Value.material);
            patientModel.GetComponentInChildren<Renderer>().material.color = patientModelConfig.Value.color.ToColor();
            // add the model as a child to container patient model
            patientModel.transform.parent = patientContainer.transform;

            // special case for ribs inside the torso phantom:
            if (patientModel.name == "ribs")
            {
                patientModel.transform.localPosition = new Vector3(1, -190, -925);
                patientModel.transform.localRotation = Quaternion.Euler(4.7f, 2.15f, 4.12f);
                patientModel.transform.localScale = new Vector3(-0.8f, 0.6f, 0.9f);
            }

            // Get the mesh bounds to locate the container center for the largest mesh
            float modelSize = patientModel.GetComponentInChildren<MeshFilter>().mesh.bounds.size.sqrMagnitude;
            Vector3 modelSizeVec3 = patientModel.GetComponentInChildren<MeshFilter>().mesh.bounds.size;
            if (modelSize > containerSize)
            {
                containerSize = modelSize;
                containerSizeVector3 = modelSizeVec3;
                patientContainerCenter = patientModel.GetComponentInChildren<MeshFilter>().mesh.bounds.center;
                // flip y and z axes (is this the same for other models?!) TODO: do it in a better way
                patientContainerCenter = new Vector3(patientContainerCenter.x, -patientContainerCenter.y, -patientContainerCenter.z);
            }
            // add model to list of overlay models
            patientOverlayModels.Add(patientModelConfig.Index, patientModel);
            modelsNames.Add(patientModel.name);
            numberOfObjModels++;
        }
        // add preopPlans
        foreach (var patientPreopPlanConfig in Config.Instance.configFile.unityConfig.patientModel.preopPlans.Select((Value, Index) => new { Value, Index }))
        {
            Material mat = GetMaterialBasedOnConfig(patientPreopPlanConfig.Value.material);
            mat.color = patientPreopPlanConfig.Value.color.ToColor();
            GameObject preopPlan = drawCylinderEvd(patientPreopPlanConfig.Value.entryPoint, patientPreopPlanConfig.Value.exitPoint, patientPreopPlanConfig.Value.trajectoryName, mat, false, patientPreopPlanConfig.Index);
            trajectoriesList.Add(preopPlan);

            preopPlan.GetComponentInChildren<Renderer>().material = mat;

            patientOverlayModels.Add(numberOfObjModels + patientPreopPlanConfig.Index, preopPlan);
        }

        HideAllTrajectories();
        if (trajectoriesList.Count != 0)
        {
            selectedTrajectoryIndex = 0;
            trajectoriesList[selectedTrajectoryIndex].SetActive(true);
        }

        // place model in front of the user
        PositionModelInFrontOfTheUser(patientContainerCenter);



        // add necessary scripts for manual manipulation
        patientContainer.AddComponent<BoxCollider>();
        patientContainer.AddComponent<ObjectManipulator>();
        patientContainer.AddComponent<BoundsControl>();
        //patientContainer.AddComponent<NearInteractionGrabbable>();
        patientContainer.AddComponent<InteractionModeManager>();
        patientContainer.AddComponent<MinMaxScaleConstraint>().enabled = false;
        patientContainer.AddComponent<RotationAxisConstraint>().enabled = false;

        patientContainer.GetComponent<BoxCollider>().size = containerSizeVector3;

        // TODO: workaround to control the visuals of the bounding box (is there a better way?!)
        // currently needs to attach a configured bounds control to the dropdown menu (since it is already accessed from the script)
        //BoundsControl customBoundsControl = ObjectsDropDown.GetComponent<BoundsControl>();
        patientContainer.GetComponent<BoundsControl>().BoundsVisualsPrefab = boundsControlVisualPrefab;
        //patientContainer.GetComponent<BoundsControl>().BoundsControlActivation = customBoundsControl.BoundsControlActivation;
        //patientContainer.GetComponent<BoundsControl>().BoxDisplayConfig = customBoundsControl.BoxDisplayConfig;
        //patientContainer.GetComponent<BoundsControl>().ScaleHandlesConfig = customBoundsControl.ScaleHandlesConfig;
        //patientContainer.GetComponent<BoundsControl>().RotationHandlesConfig = customBoundsControl.RotationHandlesConfig;
        //patientContainer.GetComponent<BoundsControl>().LinksConfig.WireframeMaterial = customBoundsControl.LinksConfig.WireframeMaterial;

        //workaround
        foreach (Transform child in patientContainer.transform)
        {
            if (child.name == "BoundingBoxWithHandles(Clone)")
            {
                boundingBoxWithHandles = Matrix4x4.TRS(child.transform.localPosition, child.transform.localRotation, child.transform.localScale);
            }
        }

        if (modelsNames.Count > 0)
            dropDownMenu.AddOptions(modelsNames);
        else
            Debug.Log("No Models were found! please check the config file");

        // set dropdown value initially to 0 ==> select first element
        dropDownMenu.value = 0;
        // assign first model as target model
        targetGameObject = patientOverlayModels[dropDownMenu.value];
        // workaround to avoid resetting target object color based on intial values of sliders (TODO: find a better way to avoid first slider values activation)
        sliderRedInitialValue = true;
        sliderGreenInitialValue = true;
        sliderBlueInitialValue = true;
        sliderAlphaInitialValue = true;
        // initialize registration landmarks and read them from file
        landmarksBasedRegistration = new LandmarksBasedRegistration();
        Debug.Log("initializing landmarks registration");

        // automatically prevent scalling after loading
        ScaleConstraintOn();

        v_value = Config.Instance.configFile.unityConfig.sizeOf1Dwidgets;


        //reset tooltip to match that of the preoperative planning trajectories
        this.GetComponent<PivotCalibration>().reloadTooltipForTargetTrajectory();
    }

    // Draw the planning trajectory guiding cylinders (tuned for the EVD application)
    public GameObject drawCylinderEvd(Vector3 StartPoint, Vector3 EndPoint, string name, Material preopMaterial, bool showAll, int index)
    {


        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = "Cylinder_" + name;
        print("NAME:::::" + patientOverlayModels[0].name);
        cylinder.transform.parent = patientOverlayModels[0].transform;
        Vector3 cylinderStart = StartPoint;
        cylinder.transform.localPosition = cylinderStart;
        cylinder.transform.localScale = new Vector3(3f, Vector3.Distance(StartPoint, EndPoint), 3f);
        Debug.Log("start:" + StartPoint + "\t end:" + EndPoint + "\t midpoint:" + (EndPoint + StartPoint) * 0.5f);


        GameObject sposition = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sposition.name = "entry_" + name;
        sposition.transform.parent = patientOverlayModels[0].transform;
        sposition.transform.localPosition = StartPoint;
        sposition.transform.localScale = new Vector3(2f / 1000f, 2f / 1000f, 2f / 1000f);
        GameObject eposition = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eposition.name = "exit_" + name;
        eposition.transform.parent = patientOverlayModels[0].transform;
        eposition.transform.localPosition = EndPoint;
        eposition.transform.localScale = new Vector3(2f / 1000f, 2f / 1000f, 2f / 1000f);
        cylinder.transform.LookAt(eposition.transform);
        cylinder.transform.RotateAround(cylinder.transform.position, cylinder.transform.right, -90);


        sposition.transform.parent = cylinder.transform;
        eposition.transform.parent = cylinder.transform;
        eposition.transform.localScale = new Vector3(eposition.transform.localScale.z, eposition.transform.localScale.y, eposition.transform.localScale.z);
        sposition.transform.localScale = new Vector3(sposition.transform.localScale.z, sposition.transform.localScale.y, sposition.transform.localScale.z);
        eposition.transform.up = cylinder.transform.up;

        // Place disk
        if (Config.Instance.configFile.unityConfig.disk1Active)
        {
            float diamter = Config.Instance.configFile.unityConfig.disk1Diameter;

            GameObject disk1 = Instantiate(diskPrefab, cylinder.transform);
            disk1.name = "disk1";
            disk1.transform.localPosition = new Vector3(0f, -0.7f, 0f);
            disk1.transform.localScale = new Vector3(diamter, 0.1f, diamter);
            foreach (Transform t in disk1.transform)
            {
                t.GetComponentInChildren<Renderer>().material = preopMaterial;
            }
            // only activate after insertion
            disk1.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        }
        
        if (Config.Instance.configFile.unityConfig.disk2Active)
        {
            float diamter = Config.Instance.configFile.unityConfig.disk2Diameter;

            GameObject disk2 = Instantiate(diskPrefab, cylinder.transform);
            disk2.name = "disk2";
            disk2.transform.localPosition = new Vector3(0f, -0.4f, 0f);
            disk2.transform.localScale = new Vector3(diamter, 0.1f, diamter);
            foreach (Transform t in disk2.transform)
            {
                t.GetComponentInChildren<Renderer>().material = preopMaterial;
            }
            // only activate after starting the insertion
            disk2.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        }
        
        if (Config.Instance.configFile.unityConfig.disk3Active)
        {
            float diamter = Config.Instance.configFile.unityConfig.disk3Diameter;

            GameObject disk3 = Instantiate(diskPrefab, cylinder.transform);
            disk3.name = "disk3";
            disk3.transform.localPosition = new Vector3(0f, -0.1f, 0f);

            disk3.transform.localScale = new Vector3(diamter, 0.1f, diamter);
            foreach (Transform t in disk3.transform)
            {
                t.GetComponentInChildren<Renderer>().material = preopMaterial;
            }
            // only activate after starting the insertion
            disk3.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
        }
        
        eposition.transform.parent = patientOverlayModels[0].transform;
        sposition.transform.parent = patientOverlayModels[0].transform;
        cylinder.transform.parent = eposition.transform;

        return cylinder;
    }

    // Trajectories for now only used with Vuforia markers; TODO: make it agnostic to the marker tracking mode
    public void nextTrajectoryButton()
    {
        if (selectedTrajectoryIndex + 1 >= trajectoriesList.Count)
        {
            return;
        }
        else
        {
            foreach (GameObject trajectory in trajectoriesList)
            {
                trajectory.SetActive(false);
            }

            selectedTrajectoryIndex++;
            trajectoriesList[selectedTrajectoryIndex].SetActive(true);

            GameObject traj = trajectoriesList[selectedTrajectoryIndex];


            print("traj:" + traj.name);
            Transform trajParent = traj.transform.parent;
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

            traj.transform.parent = tooltipCylinder.transform.GetChild(0);
            tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

            Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
            Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

            traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
            tooltipCylinder.transform.localRotation = traj.transform.localRotation;
            tooltipCylinder.transform.localScale = traj.transform.localScale;
            print("SCALE:" + traj.transform.localScale);
            GameObject disk1 = traj.transform.GetChild(0).gameObject;
            GameObject disk2 = traj.transform.GetChild(1).gameObject;

            tooltipCylinder.transform.localRotation = cylRot;
            tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);

            traj.transform.parent = trajParent;

            // for evd trajectories (makes it shorter (half) than planning
            tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
            tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);
        }
    }

    // Trajectories for now only used with Vuforia markers; TODO: make it agnostic to the marker tracking mode
    public void previousTrajectoryButton()
    {
        if (selectedTrajectoryIndex - 1 < 0)
        {
            return;
        }
        else
        {
            foreach (GameObject trajectory in trajectoriesList)
            {
                trajectory.SetActive(false);
            }
            selectedTrajectoryIndex--;
            trajectoriesList[selectedTrajectoryIndex].SetActive(true);

            GameObject traj = trajectoriesList[selectedTrajectoryIndex];


            print("traj:" + traj.name);
            Transform trajParent = traj.transform.parent;
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

            traj.transform.parent = tooltipCylinder.transform.GetChild(0);
            tooltipCylinder = tooltipCylinder.transform.GetChild(0).GetChild(0).gameObject;

            Vector3 cylPos = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y, tooltipCylinder.transform.localPosition.z);
            Quaternion cylRot = Quaternion.Euler(tooltipCylinder.transform.localRotation.x, tooltipCylinder.transform.localRotation.y, tooltipCylinder.transform.localRotation.z);
            Vector3 cylScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localScale.z);

            traj.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, traj.transform.localScale.y, tooltipCylinder.transform.localScale.z);
            tooltipCylinder.transform.localPosition = new Vector3(traj.transform.localPosition.x, traj.transform.localScale.y, traj.transform.localPosition.z);
            tooltipCylinder.transform.localRotation = traj.transform.localRotation;
            tooltipCylinder.transform.localScale = traj.transform.localScale;
            print("SCALE:" + traj.transform.localScale);
            GameObject disk1 = traj.transform.GetChild(0).gameObject;
            GameObject disk2 = traj.transform.GetChild(1).gameObject;

            tooltipCylinder.transform.localRotation = cylRot;
            tooltipCylinder.transform.localPosition = new Vector3(cylPos.x, traj.transform.localScale.y, cylPos.z);

            traj.transform.parent = trajParent;

            // for evd trajectories (makes it shorter (half) than planning
            tooltipCylinder.transform.localScale = new Vector3(tooltipCylinder.transform.localScale.x, tooltipCylinder.transform.localScale.y / 2, tooltipCylinder.transform.localScale.z);
            tooltipCylinder.transform.localPosition = new Vector3(tooltipCylinder.transform.localPosition.x, tooltipCylinder.transform.localPosition.y - tooltipCylinder.transform.localScale.y, tooltipCylinder.transform.localPosition.z);
        }
    }

    public void HideAllTrajectories()
    {
        foreach (var traj in trajectoriesList)
        {
            traj.SetActive(false);
        }
        selectedTrajectoryIndex = 0;
    }

    public void ShowAllTrajectories()
    {
        foreach (var traj in trajectoriesList)
        {
            traj.SetActive(true);
        }
        selectedTrajectoryIndex = 0;
    }

    public void ChooseTargetObject()
    {
        targetGameObject = patientOverlayModels[dropDownMenu.value];
        Debug.Log("Object set as target: " + targetGameObject.name);
    }

    public void DeactivatePattientModel()
    {
        if (patientContainer)
            patientContainer.SetActive(false);
    }

    void PositionModelInFrontOfTheUser(Vector3 containerCenter)
    {
        // reposition each model to the center of the container model (needed for correct manual rotation around the model center instead of CT origin)
        foreach (Transform childTransform in patientContainer.transform)
        {
            childTransform.position = containerCenter;

            if (childTransform.name == "ribs")
            {
                childTransform.transform.localPosition = childTransform.transform.localPosition + new Vector3(1, -190, -925);
            }

        }
        // scale the model down based on the config scale
        float scale = Config.Instance.configFile.unityConfig.patientModel.scale;
        patientContainer.transform.localScale = Vector3.one * scale;
        // center the model at world origin
        Bounds patientBounds = patientContainer.GetComponentInChildren<MeshFilter>().mesh.bounds;
        Vector3 patientCenter = patientBounds.center;
        Vector3 patientSize = patientBounds.size;
        patientContainer.transform.position = new Vector3(patientCenter.x, -patientCenter.y, -patientCenter.z) * scale;
        // place the model at 50cm away from the camera/head position
        patientContainer.transform.position = Camera.main.transform.position + (Camera.main.transform.forward * 0.5f);
    }

    Material GetMaterialBasedOnConfig(string configMaterial)
    {
        if (configMaterial == "transparent")
            return transparentMaterial;
        else if (configMaterial == "unlit")
            return unlitMaterial;
        else
            return opaqueMaterial;
    }

    public void AttachPatientModelToMarker()
    {
        if (Config.Instance.IsVuforiaTrackingActive)
        {
            trackingModeActive = "Vuforia";

            foreach (var vuforiaMarkerConfig in Config.Instance.configFile.unityConfig.vuforia.imageTargets)
            {
                if (vuforiaMarkerConfig.isPatient == true)
                {
                    patientMarkerName = vuforiaMarkerConfig.name.Substring(0, vuforiaMarkerConfig.name.IndexOf("."));
                }
                else if (vuforiaMarkerConfig.isTool)
                {
                    pointerMarkerName = vuforiaMarkerConfig.name.Substring(0, vuforiaMarkerConfig.name.IndexOf("."));
                }
            }
            if (patientMarkerName.Length == 0)
                Debug.Log("No patient marker was found! please check the config file");
            if (pointerMarkerName.Length == 0)
                Debug.Log("No pointer marker was found! please check the config file");
        }
        else if (Config.Instance.IsArucoTrackingActive)
        {
            trackingModeActive = "Aruco";

            foreach (var arucoMarkerConfig in Config.Instance.configFile.nativeConfig.aruco.marker)
            {
                if (arucoMarkerConfig.isPatient == true)
                {
                    patientMarkerName = arucoMarkerConfig.name;
                }
                else if (arucoMarkerConfig.isTool)
                {
                    pointerMarkerName = arucoMarkerConfig.name;
                }
            }
            if (patientMarkerName.Length == 0)
                Debug.Log("No patient marker was found! please check the config file");
            if (pointerMarkerName.Length == 0)
                Debug.Log("No pointer marker was found! please check the config file");
        }
        else
        {
            Debug.Log("Please activate either Vuforia or Aruco tracking first");
            return;
        }

        patientMarker = GameObject.Find(trackingModeActive + patientMarkerName);
        pointerMarker = GameObject.Find(trackingModeActive + pointerMarkerName);
        Debug.Log(patientMarker.name + "   " + pointerMarker.name);

        patientContainer.transform.parent = patientMarker.transform;
        patientAttachedToMarker = true;

        deactivate1DManipulationHandles();
        BoundingBoxOff();
    }

    public void DetachPatientModelFromMarker()
    {
        patientContainer.transform.parent = null;
        patientAttachedToMarker = false;
    }

    public void TargetObjectOn()
    {
        targetGameObject.SetActive(true);
    }
    public void TargetObjectOff()
    {
        targetGameObject.SetActive(false);
    }

    public void BoundingBoxOn()
    {
        patientContainer.GetComponent<ObjectManipulator>().enabled = true;
        patientContainer.GetComponent<BoundsControl>().enabled = true;
    }

    public void BoundingBoxOff()
    {
        patientContainer.GetComponent<ObjectManipulator>().enabled = false;
        patientContainer.GetComponent<BoundsControl>().enabled = false;
    }

    public void MoveConstraintOn()
    {
        patientContainer.GetComponent<ObjectManipulator>().enabled = false;
    }

    public void MoveConstraintOff()
    {
        patientContainer.GetComponent<ObjectManipulator>().enabled = true;
    }

    public void ScaleConstraintOn()
    {
        patientContainer.GetComponent<MinMaxScaleConstraint>().enabled = true;
        patientContainer.GetComponent<MinMaxScaleConstraint>().MinimumScale = Vector3.one;
        patientContainer.GetComponent<MinMaxScaleConstraint>().MaximumScale = Vector3.one;
    }

    public void ScaleConstraintOff()
    {
        patientContainer.GetComponent<MinMaxScaleConstraint>().enabled = false;
    }

    public void makePatientModelMoveFast()
    {
        patientContainer.GetComponent<ObjectManipulator>().MoveLerpTime = 0.01f;
        patientContainer.GetComponent<ObjectManipulator>().RotateLerpTime = 0.01f;
        patientContainer.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.01f;

        for (int i = 0; i < list_cubes.Count; i++)
        {
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = 0.001f;
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = 0.001f;
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.001f;

            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = 0.001f;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = 0.001f;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.001f;
        }
    }
    public void makePatientModelMoveNormal()
    {
        patientContainer.GetComponent<ObjectManipulator>().MoveLerpTime = 0.5f;
        patientContainer.GetComponent<ObjectManipulator>().RotateLerpTime = 0.5f;
        patientContainer.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.5f;

        for (int i = 0; i < list_cubes.Count; i++)
        {
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = 0.5f;
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = 0.5f;
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.5f;

            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = 0.5f;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = 0.5f;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.5f;
        }
    }
    public void makePatientModelMoveSlow()
    {
        patientContainer.GetComponent<ObjectManipulator>().MoveLerpTime = 0.9f;
        patientContainer.GetComponent<ObjectManipulator>().RotateLerpTime = 0.9f;
        patientContainer.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.9f;

        for (int i = 0; i < list_cubes.Count; i++)
        {
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = 0.9f;
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = 0.9f;
            list_cubes[i].gameObject.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.9f;

            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().MoveLerpTime = 0.9f;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().RotateLerpTime = 0.9f;
            list_shperes[i].gameObject.GetComponent<ObjectManipulator>().ScaleLerpTime = 0.9f;
        }
    }

    public void OnSliderUpdatedManipulationSpeed(SliderEventData eventData)
    {
        if (!patientContainer) { return; }

        patientContainer.GetComponent<ObjectManipulator>().MoveLerpTime = 1 - eventData.NewValue;
        patientContainer.GetComponent<ObjectManipulator>().RotateLerpTime = 1 - eventData.NewValue;
        patientContainer.GetComponent<ObjectManipulator>().ScaleLerpTime = 1 - eventData.NewValue;

    }

    public void OnSliderUpdated1DHandlesSize(SliderEventData eventData)
    {
        if (!patientContainer) { return; }
        if(list_cubes.Count==0) { return; }

        v_value = eventData.NewValue * 100;

        for (int i = 0; i < list_cubes.Count; i++)
        {
            list_cubes[i].transform.localScale = v_value * Vector3.one;
            list_shperes[i].transform.localScale = v_value * Vector3.one;
            list_cylinders[i].transform.localScale = new Vector3(10, v_value*5, 10);

            if (i == 0)    // x-axis
            {
                list_cubes[i].transform.localPosition = new Vector3(v_value * 5, 0, 0);
                list_shperes[i].transform.localPosition = new Vector3(-v_value * 5, 0, 0);
            }
            else if (i == 1)    // y-axis
            {
                list_cubes[i].transform.localPosition = new Vector3(0, v_value * 5, 0);
                list_shperes[i].transform.localPosition = new Vector3(0, -v_value * 5, 0);
            }
            else   // i==2 z-axis
            {
                list_cubes[i].transform.localPosition = new Vector3(0, 0, v_value * 5);
                list_shperes[i].transform.localPosition = new Vector3(0, 0, -v_value * 5);
            }
        }
    }

    public void OnSliderUpdatedRed(SliderEventData eventData)
    {
        if (!targetGameObject) { return; }
        if (sliderRedInitialValue) { sliderRedInitialValue = false; return; }

        targetRenderer = targetGameObject.GetComponentInChildren<Renderer>();
        if ((targetRenderer != null) && (targetRenderer.material != null))
        {
            targetRenderer.material.color = new Color(eventData.NewValue, targetRenderer.sharedMaterial.color.g, targetRenderer.sharedMaterial.color.b, targetRenderer.sharedMaterial.color.a);
        }
    }

    public void OnSliderUpdatedGreen(SliderEventData eventData)
    {
        if (!targetGameObject) { return; }
        if (sliderGreenInitialValue) { sliderGreenInitialValue = false; return; }

        targetRenderer = targetGameObject.GetComponentInChildren<Renderer>();
        if ((targetRenderer != null) && (targetRenderer.material != null))
        {
            targetRenderer.material.color = new Color(targetRenderer.sharedMaterial.color.r, eventData.NewValue, targetRenderer.sharedMaterial.color.b, targetRenderer.sharedMaterial.color.a);
        }
    }

    public void OnSliderUpdatedBlue(SliderEventData eventData)
    {
        if (!targetGameObject) { return; }
        if (sliderBlueInitialValue) { sliderBlueInitialValue = false; return; }

        targetRenderer = targetGameObject.GetComponentInChildren<Renderer>();
        if ((targetRenderer != null) && (targetRenderer.material != null))
        {
            targetRenderer.material.color = new Color(targetRenderer.sharedMaterial.color.r, targetRenderer.sharedMaterial.color.g, eventData.NewValue, targetRenderer.sharedMaterial.color.a);
        }
    }
    public void OnSliderUpdatedAlpha(SliderEventData eventData)
    {
        if (!targetGameObject) { return; }
        if (sliderAlphaInitialValue) { sliderAlphaInitialValue = false; return; }

        targetRenderer = targetGameObject.GetComponentInChildren<Renderer>();
        if ((targetRenderer != null) && (targetRenderer.material != null))
        {
            targetRenderer.material.color = new Color(targetRenderer.sharedMaterial.color.r, targetRenderer.sharedMaterial.color.g, targetRenderer.sharedMaterial.color.b, 1 - eventData.NewValue);
        }
    }

    public void SaveModelsConfiguartion()
    {
        foreach (var patientModelConfig in Config.Instance.configFile.unityConfig.patientModel.models.Select((Value, Index) => new { Value, Index }))
        {
            var targetGameObject = patientOverlayModels[patientModelConfig.Index];

            var r = (int)(targetGameObject.GetComponentInChildren<Renderer>().material.color.r * 255);
            var g = (int)(targetGameObject.GetComponentInChildren<Renderer>().material.color.g * 255);
            var b = (int)(targetGameObject.GetComponentInChildren<Renderer>().material.color.b * 255);
            var a = (int)(targetGameObject.GetComponentInChildren<Renderer>().material.color.a * 255);

            var rhex = r == 0 ? r.ToString("X2") : r.ToString("X");
            var ghex = g == 0 ? g.ToString("X2") : g.ToString("X");
            var bhex = b == 0 ? b.ToString("X2") : b.ToString("X");
            var ahex = a == 0 ? a.ToString("X2") : a.ToString("X");
            string hexColor = rhex + ghex + bhex + ahex;

            patientModelConfig.Value.color = hexColor;
            patientModelConfig.Value.showModel = targetGameObject.activeInHierarchy;

        }

        Config.Instance.SaveConfigFile();
    }

    public void AcquirePointerTipPosition()
    {
        if (!patientAttachedToMarker)
            AttachPatientModelToMarker();

        if (pointerMarker != null && patientMarker != null)
        {
            useRecordedPointsForRegistration = true;
        }
    }

    void acquireRegistrationPoint()
    {
        var intraopLandmarks = scriptsGameObject.GetComponent<Patient>().landmarksBasedRegistration.GetIntraopLandmarks();
        var numberOfLandmarks = scriptsGameObject.GetComponent<Patient>().landmarksBasedRegistration.GetNumberOfLandMarks();

        if (intraopLandmarks.Count < numberOfLandmarks)
        {
            Vector3 tipInPatient = Utils.computePointsCentroid(registrationPointsOverMultipleFrames);
            scriptsGameObject.GetComponent<Patient>().landmarksBasedRegistration.AddIntraopPatientPoint(tipInPatient);

            print($"acquiring a point: {tipInPatient}");

            // create a small sphere indicating the acquired point
            GameObject tipAcquiredPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tipAcquiredPoint.transform.parent = patientMarker.transform;
            tipAcquiredPoint.transform.localPosition = tipInPatient;
            tipAcquiredPoint.transform.localScale = Vector3.one * Config.Instance.configFile.unityConfig.landmarksRegistration.sphereRadious;
            tipAcquiredPoint.GetComponent<Renderer>().material.color = Config.Instance.configFile.unityConfig.landmarksRegistration.color.ToColor();

            listOfAcquiredTipPoints.Add(tipAcquiredPoint);
            beep.Play(0);
        }
    }

    public void RemoveLastAcquiredTipPoint()
    {
        Destroy(listOfAcquiredTipPoints[listOfAcquiredTipPoints.Count - 1]);
        listOfAcquiredTipPoints.RemoveAt(listOfAcquiredTipPoints.Count - 1);
        landmarksBasedRegistration.RemoveLastIntraopLandmark();
        Debug.Log(landmarksBasedRegistration.GetIntraopLandmarks().Count);
    }

    public void AlignPatientModel()
    {
        intraopLandmarks = landmarksBasedRegistration.GetIntraopLandmarks();
        preopLandmarks = landmarksBasedRegistration.GetPreopLandmarks();
        var numberOfLandmarks = landmarksBasedRegistration.GetNumberOfLandMarks();

        if (intraopLandmarks.Count == numberOfLandmarks)
        {
            foreach (Transform child in patientContainer.transform)
            {
                child.localPosition = Vector3.zero;

                if (child.name == "ribs")
                {
                    child.transform.localPosition = new Vector3(1, -190, -925);
                }
            }
            patientContainer.transform.localScale = Vector3.one * Config.Instance.configFile.unityConfig.patientModel.scale;
            patientContainer.GetComponent<BoundsControl>().enabled = false;
            patientContainer.GetComponent<ObjectManipulator>().enabled = false;

            registrationMatrix = landmarksBasedRegistration.PerformPointBasedRegistration();
            Debug.Log($"registration matrix \n{registrationMatrix}");
            SaveRegistrationMatrix();
            savePoints();
            patientContainer.transform.localPosition = new Vector3(registrationMatrix.m03, registrationMatrix.m13, registrationMatrix.m23);
            patientContainer.transform.localRotation = registrationMatrix.GetRotation();
            Debug.Log("model aligned!!");

            float rmse = getRMSE(registrationMatrix, preopLandmarks, intraopLandmarks);
            SaveRMSE(rmse);

            landmarksBasedRegistration.ClearIntraopLandmarks();
            foreach (var acquiredPoint in listOfAcquiredTipPoints) { Destroy(acquiredPoint); }


        }
        else
        {
            Debug.Log("Not enough points were acquired, attempting to load a registration matrix from desk ...");

            if (!patientAttachedToMarker)
                AttachPatientModelToMarker();

            bool loadedSuccessfully = loadRegistrationMatrix();

            if (!loadedSuccessfully) { Debug.Log("RegistrationMatrix could not load correctly, make sure there is a file in desk"); return; }

            patientContainer.transform.localPosition = new Vector3(registrationMatrix.m03, registrationMatrix.m13, registrationMatrix.m23);
            patientContainer.transform.localRotation = registrationMatrix.GetRotation();

            foreach (Transform child in patientContainer.transform)
            {
                child.localPosition = Vector3.zero;

                if (child.name == "ribs")
                {
                    child.transform.localPosition = new Vector3(1, -190, -925);
                }
            }

            if (intraopLandmarks.Count > 0)
            {
                landmarksBasedRegistration.ClearIntraopLandmarks();
                foreach (var acquiredPoint in listOfAcquiredTipPoints) { Destroy(acquiredPoint); }
            }
        }

        isPatientaligned = true;

        BoundingBoxOff();

        if (Config.Instance.configFile.unityConfig.recordDataActive)
            scriptsGameObject.GetComponent<RecordData>().StartRecordingData();

    }

    public void ClearPatientModel()
    {
        Destroy(patientContainer);
        patientOverlayModels.Clear();
        modelsNames.Clear();
        listOfAcquiredTipPoints.Clear();
        listOfLandmarks.Clear();
        landmarksBasedRegistration.ClearIntraopLandmarks();
    }

    void SaveRegistrationMatrix()
    {
        DateTime currentTime = DateTime.Now;
        string time = currentTime.ToString("_HH_mm_ss");

        registrationCounter++;

        string trackingMode = trackingModeActive;
        var numberOfLandmarks = landmarksBasedRegistration.GetNumberOfLandMarks();
        string prefix = $"_{numberOfLandmarks}_{trackingMode}_{registrationCounter}";

        string filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, Config.Instance.configFile.unityConfig.registration.registrationMatrix + prefix + ".txt");

        StreamWriter csvWriter = new StreamWriter(filePath);
        csvWriter.WriteLine("column1, column2, colum3, column4");
        csvWriter.WriteLine(registrationMatrix.m00 + "," + registrationMatrix.m01 + "," + registrationMatrix.m02 + "," + registrationMatrix.m03);
        csvWriter.WriteLine(registrationMatrix.m10 + "," + registrationMatrix.m11 + "," + registrationMatrix.m12 + "," + registrationMatrix.m13);
        csvWriter.WriteLine(registrationMatrix.m20 + "," + registrationMatrix.m21 + "," + registrationMatrix.m22 + "," + registrationMatrix.m23);
        csvWriter.WriteLine(registrationMatrix.m30 + "," + registrationMatrix.m31 + "," + registrationMatrix.m32 + "," + registrationMatrix.m33);
        csvWriter.Close();
        Debug.Log($"saved registration matrix \n{registrationMatrix}");

        filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, Config.Instance.configFile.unityConfig.registration.registrationMatrix);

        csvWriter = new StreamWriter(filePath);
        csvWriter.WriteLine("column1, column2, colum3, column4");
        csvWriter.WriteLine(registrationMatrix.m00 + "," + registrationMatrix.m01 + "," + registrationMatrix.m02 + "," + registrationMatrix.m03);
        csvWriter.WriteLine(registrationMatrix.m10 + "," + registrationMatrix.m11 + "," + registrationMatrix.m12 + "," + registrationMatrix.m13);
        csvWriter.WriteLine(registrationMatrix.m20 + "," + registrationMatrix.m21 + "," + registrationMatrix.m22 + "," + registrationMatrix.m23);
        csvWriter.WriteLine(registrationMatrix.m30 + "," + registrationMatrix.m31 + "," + registrationMatrix.m32 + "," + registrationMatrix.m33);
        csvWriter.Close();
    }

    bool loadRegistrationMatrix()
    {
        string filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, Config.Instance.configFile.unityConfig.registration.registrationMatrix);

        if(!File.Exists(filePath)) {Debug.Log("No file exist: " + filePath); return false; }

        StreamReader csvReader = new StreamReader(filePath);
        var headerLine = csvReader.ReadLine();
        for (int i = 0; i < 4; i++)
        {
            var row = csvReader.ReadLine();
            var values = row.Split(',');
            registrationMatrix.SetRow(i, new Vector4(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3])));
        }
        csvReader.Close();

        return true;
    }

    void savePoints()
    {
        DateTime currentTime = DateTime.Now;
        string time = currentTime.ToString("_HH_mm_ss");

        string trackingMode = trackingModeActive;
        var numberOfLandmarks = landmarksBasedRegistration.GetNumberOfLandMarks();
        string prefix = $"_{numberOfLandmarks}_{trackingMode}_{registrationCounter}";

        string sourceFilePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, Config.Instance.configFile.unityConfig.registration.sourcePoints + prefix + ".txt");
        StreamWriter csvWriter = new StreamWriter(sourceFilePath);
        for (int i = 0; i < preopLandmarks.Count(); i++)
        {
            csvWriter.WriteLine(preopLandmarks[i].x + " , " + preopLandmarks[i].y + " , " + preopLandmarks[i].z);
        }
        csvWriter.Close();

        string targetFilePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, Config.Instance.configFile.unityConfig.registration.targetPoints + prefix + ".txt");
        csvWriter = new StreamWriter(targetFilePath);
        for (int i = 0; i < intraopLandmarks.Count(); i++)
        {
            csvWriter.WriteLine(intraopLandmarks[i].x + " , " + intraopLandmarks[i].y + " , " + intraopLandmarks[i].z);
        }
        csvWriter.Close();
    }

    void SaveRMSE(float rmse)
    {
        DateTime currentTime = DateTime.Now;
        string time = currentTime.ToString("_HH_mm_ss");

        string trackingMode = trackingModeActive;
        var numberOfLandmarks = landmarksBasedRegistration.GetNumberOfLandMarks();
        string prefix = $"_{numberOfLandmarks}_{trackingMode}_{registrationCounter}";

        string sourceFilePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, Config.Instance.configFile.unityConfig.registration.registrationMatrix + "_rmse" + prefix + ".txt");
        StreamWriter csvWriter = new StreamWriter(sourceFilePath);
        csvWriter.WriteLine(rmse);
        csvWriter.Close();
    }

    float getRMSE(Matrix4x4 calibrationMatrix, List<Vector3> sourcePoints, List<Vector3> targetPoints)
    {
        float registrationError = 0.0F;
        //apply transformation
        List<Vector3> calibratedSourcePoints = new List<Vector3>();
        for (int i = 0; i < sourcePoints.Count; i++)
        {
            Vector3 transformedSourcePoint = calibrationMatrix.MultiplyPoint(sourcePoints[i]);
            calibratedSourcePoints.Add(new Vector3(transformedSourcePoint.x, transformedSourcePoint.y, transformedSourcePoint.z));
        }
        for (int i = 0; i < calibratedSourcePoints.Count; i++)
        {
            registrationError += Vector3.Distance(calibratedSourcePoints[i], targetPoints[i]);
        }
        return registrationError / targetPoints.Count;
    }

}
