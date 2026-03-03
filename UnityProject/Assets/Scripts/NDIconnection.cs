using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Threading;
using System.Net.Sockets;
using System.Text;
using System.Collections.Concurrent;
using System.IO;
using MixedReality.Toolkit.UX;
using System.Diagnostics;

public class NDIconnection : MonoBehaviour
{
    const string NDI_POINTER_FLAG = "0";
    const string NDI_PATIENT_FLAG = "1";
    const string NDI_CALIBRATION_BOARD_FLAG = "2";

    bool ndiConnectionActive = false;
    string ndiIpAddress;
    int ndiPortNumber;
    Thread tcpClientThread;
    TcpClient socketConnection;

    MaxLengthConcurrentQueue<Matrix4x4> pointerQueue = new MaxLengthConcurrentQueue<Matrix4x4>(5);
    MaxLengthConcurrentQueue<Matrix4x4> patientQueue = new MaxLengthConcurrentQueue<Matrix4x4>(5);
    MaxLengthConcurrentQueue<Matrix4x4> calibrationBoardQueue = new MaxLengthConcurrentQueue<Matrix4x4>(5);

    GameObject ndiPort1Marker;
    GameObject ndiPort2Marker;
    GameObject ndiPort3Marker;

    GameObject hl2Marker1;
    GameObject hl2Marker2;
    GameObject mainCamera;
    bool alreadyAssigned = false;
    bool souldAssignMarkers = false;
    public GameObject vuforiaTrackingToggleButton;
    public GameObject arucoTrackingToggleButton;
    bool shouldActivateVuforia = false;
    bool shouldActivateAruco = false;
    bool shouldDeactivateVuforiaAruco = false;
    bool shouldActivateCalibrationMenu = false;
    bool shouldActivatePatientMenu = false;
    bool shouldDeactivateAllMenus = false;
    bool patientModelActivated = false;
    bool pointerTipCalibrated = false;
    bool shouldActivateNDIVisualization = false;
    bool shouldDeactivateNDIVisualization = false;
    bool showNDIvisualization = false;
    bool writeCSVheader = false;
    public GameObject scriptsGameObject;
    public GameObject toolCalibrationMenu;
    public GameObject patientMenu;

    // record data
    bool recordDataActive = false;
    int numberOfFramesToRecord = 100;
    int recordedFramesCounter = 0;
    StreamWriter poseSW;
    int recordingFileCounter = 1;
    int recordingFileCounterVuforia = 1;
    int recordingFileCounterAruco = 1;
    bool ValidVuforiaPoseShouldBeep = true;

    bool entryConditionMet = false;
    bool beginRecordingData = false;
    bool doneRecordingData = false;
    bool listening = true;

    bool NDIcameraStartedMoving = false;
    bool NDIcameraStopedMoving = false;

    bool showCalibCylinder = false;
    bool CalibCylinerIsShown = false;

    bool startMRTooltipCalibration = false;

    bool nextTrajectory = false;
    bool previousTrajectory = false;
    bool tool1 = false; // round burr
    bool tool2 = false; // calibrated tool
    bool tool3 = false; // pilot twist
    bool tool4 = false; // pilot twist
    bool closeCSVfile = false;

    AudioSource beep;

    Matrix4x4 ndiCalibrationMatrix;
    GameObject ndiPointerSphere;
    GameObject ndiPointerCylinder;
    GameObject ndiMarkerSphere;
    Matrix4x4 ndiPoseInVuforiaMarker;
    Matrix4x4 pointerInMarkerPose;
    GameObject calibratedHL2Marker;

    List<Vector3> registrationPointsOverMultipleFrames;
    bool useRecordedPointsForRegistration = false;
    bool useRecordedPointsForCalibration = false;
    public bool stopCollectingDataForCalibration = false;
    string recordedFilePrefix = "test";
    int idx_calibrationMarkerToAssign = 1;

    string trackingModeActive;

    Stopwatch stopWatch;

    StreamWriter manaulCSV;
    bool beginRecordingManualRegistrationMetrics = false;
    Stopwatch manualRegistrationStopWatch;
    string manualPhase = "_";
    string manualMode = "_";
    string manualSpeed = "_";
    bool shouldLoadSavedRegistrationMatrix = false;
    bool shouldAttachPatientModelToMarker = false;
    bool shouldRefreshPatientModelPose = false;
    bool shouldUse6DManualManipulation = false;
    bool shouldUse1DManualManipulation = false;
    bool shouldBeepTCPcommandReceived = false;
    bool shouldMovePatientModelFast = false;
    bool shouldMovePatientModelSlow = false;
    bool shouldMovePatientModelNormal = false;

    int incision_counter = -1;
    bool shouldCallRecordPoseDataFromPatientScript = false;

    public bool callingToAttachForManualRegistration = false;

    // Start is called before the first frame update
    void Start()
    {
        beep = scriptsGameObject.GetComponent<AudioSource>();
        registrationPointsOverMultipleFrames = new List<Vector3>();

        StartNDIConnection();

        stopWatch = new Stopwatch();
        manualRegistrationStopWatch = new Stopwatch();
    }

    // Update is called once per frame
    void Update()
    {

        if (ndiConnectionActive)
        {
            if (shouldActivateVuforia)
            {
                ActivateVuforiaTracking();
            }
            else if (shouldActivateAruco)
            {
                ActivateArucoTracking();
            }
            else if (shouldDeactivateVuforiaAruco)
            {
                DeactivateArucoAndVuforiaTracking();
            }
            else if (shouldActivateCalibrationMenu)
            {
                ActivateCalibrationMenu();
            }
            else if (shouldActivatePatientMenu)
            {
                ActivatePatientMenu();
            }
            else if (shouldDeactivateAllMenus)
            {
                DeactivateAllMenus();
            }
            else if (shouldActivateNDIVisualization)
            {
                ActivateNDIVisualization();
            }
            else if (shouldDeactivateNDIVisualization)
            {
                DeactivateNDIVisualization();
            }

            if (pointerQueue.TryDequeue(out Matrix4x4 pointerTransform))
            {
                TransformExtensions.FromMatrix(ndiPort1Marker.transform, pointerTransform);
                //Debug.Log(pointerTransform);
            }
            else
            {
                pointerTransform = Matrix4x4.identity;
                //Debug.Log("in else");
            }
            if (patientQueue.TryDequeue(out Matrix4x4 patientTransform))
            {
                TransformExtensions.FromMatrix(ndiPort2Marker.transform, patientTransform);
            }
            if (calibrationBoardQueue.TryDequeue(out Matrix4x4 calibrationBoardTransform))
            {
                TransformExtensions.FromMatrix(ndiPort3Marker.transform, calibrationBoardTransform);
            }
        }

        if (souldAssignMarkers)
        {
            assingCameraMarker();
            RecordPoseData();
        }

        if (recordDataActive)
        {
            if (writeCSVheader)
            {
                poseSW.WriteLine($"{ndiPort1Marker.name},Tx.0,Ty.0,Tz.0," +
                             $"Qx.0,Qy.0,Qz.0,Qw.0," +
                             $"{ndiPort2Marker.name},Tx.1,Ty.1,Tz.1," +
                             $"Qx.1,Qy.1,Qz.1,Qw.1," +
                             $"{ndiPort3Marker.name},Tx.2,Ty.2,Tz.2," +
                             $"Qx.2,Qy.2,Qz.2,Qw.2," +
                             $"{hl2Marker1.name},Tx.3,Ty.3,Tz.3," +
                             $"Qx.3,Qy.3,Qz.3,Qw.3," +
                             $"{hl2Marker2.name},Tx.4,Ty.4,Tz.4," +
                             $"Qx.4,Qy.4,Qz.4,Qw.4," +
                             $"{mainCamera.name},Tx.5,Ty.5,Tz.5," +
                             $"Qx.5,Qy.5,Qz.5,Qw.5, timeStamp");

                writeCSVheader = false;
                stopWatch.Start();
            }

            //if (!scriptsGameObject.GetComponent<PivotCalibration>().skipPoseRecording)
            if (!scriptsGameObject.GetComponent<PivotCalibration>().skipPoseRecording && hl2Marker1.GetComponent<MarkerTrackingStatus>().Tracked && hl2Marker2.GetComponent<MarkerTrackingStatus>().Tracked)
            {
                poseSW.WriteLine($"{ndiPort1Marker.name}, {ndiPort1Marker.transform.position.x}, {ndiPort1Marker.transform.position.y}, {ndiPort1Marker.transform.position.z}," +
                                 $"{ndiPort1Marker.transform.rotation.x}, {ndiPort1Marker.transform.rotation.y}, {ndiPort1Marker.transform.rotation.z}, {ndiPort1Marker.transform.rotation.w}," +
                                 $"{ndiPort2Marker.name}, {ndiPort2Marker.transform.position.x}, {ndiPort2Marker.transform.position.y}, {ndiPort2Marker.transform.position.z}," +
                                 $"{ndiPort2Marker.transform.rotation.x}, {ndiPort2Marker.transform.rotation.y}, {ndiPort2Marker.transform.rotation.z}, {ndiPort2Marker.transform.rotation.w}," +
                                 $"{ndiPort3Marker.name}, {ndiPort3Marker.transform.position.x}, {ndiPort3Marker.transform.position.y}, {ndiPort3Marker.transform.position.z}," +
                                 $"{ndiPort3Marker.transform.rotation.x}, {ndiPort3Marker.transform.rotation.y}, {ndiPort3Marker.transform.rotation.z}, {ndiPort3Marker.transform.rotation.w}," +
                                 $"{hl2Marker1.name}, {hl2Marker1.transform.position.x}, {hl2Marker1.transform.position.y}, {hl2Marker1.transform.position.z}," +
                                 $"{hl2Marker1.transform.rotation.x}, {hl2Marker1.transform.rotation.y}, {hl2Marker1.transform.rotation.z}, {hl2Marker1.transform.rotation.w}," +
                                 $"{hl2Marker2.name}, {hl2Marker2.transform.position.x}, {hl2Marker2.transform.position.y}, {hl2Marker2.transform.position.z}," +
                                 $"{hl2Marker2.transform.rotation.x}, {hl2Marker2.transform.rotation.y}, {hl2Marker2.transform.rotation.z}, {hl2Marker2.transform.rotation.w}," +
                                 $"{mainCamera.name}, {mainCamera.transform.position.x}, {mainCamera.transform.position.y}, {mainCamera.transform.position.z}," +
                                 $"{mainCamera.transform.rotation.x}, {mainCamera.transform.rotation.y}, {mainCamera.transform.rotation.z}, {mainCamera.transform.rotation.w}, {stopWatch.ElapsedMilliseconds.ToString()}");

                recordedFramesCounter++;

            }

            if (useRecordedPointsForRegistration)
            {
                // hl2marker2 == patientMarker && hl2marker1 == pointerMarker
                Matrix4x4 patientMarkerToWorld = Matrix4x4.TRS(hl2Marker2.transform.position, hl2Marker2.transform.rotation, hl2Marker2.transform.localScale);
                Vector3 tipInPatient = patientMarkerToWorld.inverse.MultiplyPoint(hl2Marker1.transform.GetChild(1).position);
                registrationPointsOverMultipleFrames.Add(tipInPatient);
            }

            if (useRecordedPointsForCalibration)
            {
                if (stopCollectingDataForCalibration)
                {
                    poseSW.Close();
                    recordDataActive = false;
                    alreadyAssigned = false;
                    stopCollectingDataForCalibration = false;
                    useRecordedPointsForCalibration = false;
                    recordedFramesCounter = 0;
                    stopWatch.Stop();
                    stopWatch.Reset();
                }
            }
            else if (recordedFramesCounter == numberOfFramesToRecord)
            {
                if (useRecordedPointsForRegistration)
                {
                    acquireRegistrationPoint();
                    registrationPointsOverMultipleFrames.Clear();
                    useRecordedPointsForRegistration = false;
                }
                poseSW.Close();
                recordedFramesCounter = 0;
                recordDataActive = false;
                alreadyAssigned = false;
                stopWatch.Stop();
                stopWatch.Reset();

                if (ValidVuforiaPoseShouldBeep)
                    beep.Play(0);
            }
        }

        if (scriptsGameObject.GetComponent<Patient>().selectedTrajectoryIndex < Config.Instance.configFile.unityConfig.patientModel.numberOfTrainingTasks)
        {
            if (!entryConditionMet)
            {
                if (scriptsGameObject.GetComponent<RecordData>().tipToAxisDist < 1 && scriptsGameObject.GetComponent<RecordData>().angleDeviation < 2)
                {
                    beginRecordingData = true;
                    entryConditionMet = true;
                    print("entryConditionMet === TRUE");
                }

            }
            else
            {
                if (scriptsGameObject.GetComponent<RecordData>().tipToExitDist < 1)
                {
                    doneRecordingData = true;
                    entryConditionMet = false;
                    nextTrajectory = true;

                    //if (scriptsGameObject.GetComponent<Patient>().selectedTrajectoryIndex == 1)
                    //    previousTrajectory = true;
                    //else
                    //    nextTrajectory = true;

                }
            }

        }

        if (beginRecordingData)
        {
            scriptsGameObject.GetComponent<RecordData>().InsertionBegin();
            beginRecordingData = false;
            beep.Play(0);
            print("BeginRecordingData");
            // activate desks for visual alignment
            {
                if (Config.Instance.configFile.unityConfig.disk1Active)
                {
                    GameObject preopDesk1 = GameObject.Find("disk1");
                    preopDesk1.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
                    GameObject toolDesk1 = GameObject.Find("toolDisk1");
                    toolDesk1.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
                }
                if (Config.Instance.configFile.unityConfig.disk2Active)
                {
                    GameObject preopDesk2 = GameObject.Find("disk2");
                    preopDesk2.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
                    GameObject toolDesk2 = GameObject.Find("toolDisk2");
                    toolDesk2.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
                }

                if (Config.Instance.configFile.unityConfig.disk3Active)
                {
                    UnityEngine.Debug.Log("enabling desks for preop and needle...");
                    GameObject preopDesk3 = GameObject.Find("disk3");
                    preopDesk3.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
                    GameObject toolDesk3 = GameObject.Find("toolDisk3");
                    toolDesk3.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = true;
                } 
            }
        }
        if (doneRecordingData)
        {
            scriptsGameObject.GetComponent<RecordData>().InsertionFinish();
            doneRecordingData = false;
            beep.Play(0);
            print("DoneRecordingData");
            // deactivate desks after alignment
            {
                if (Config.Instance.configFile.unityConfig.disk1Active)
                {
                    GameObject preopDesk1 = GameObject.Find("disk1");
                    preopDesk1.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
                    GameObject toolDesk1 = GameObject.Find("toolDisk1");
                    toolDesk1.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
                }
                if (Config.Instance.configFile.unityConfig.disk2Active)
                {
                    GameObject preopDesk2 = GameObject.Find("disk2");
                    preopDesk2.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
                    GameObject toolDesk2 = GameObject.Find("toolDisk2");
                    toolDesk2.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
                }

                if (Config.Instance.configFile.unityConfig.disk3Active)
                {
                    GameObject preopDesk3 = GameObject.Find("disk3");
                    preopDesk3.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
                    GameObject toolDesk3 = GameObject.Find("toolDisk3");
                    toolDesk3.transform.GetChild(0).GetComponent<MeshRenderer>().enabled = false;
                }
            }
        }

        if (previousTrajectory)
        {
            scriptsGameObject.GetComponent<Patient>().previousTrajectoryButton();
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();
            previousTrajectory = false;
            beep.Play(0);
        }
        if (nextTrajectory)
        {
            scriptsGameObject.GetComponent<Patient>().nextTrajectoryButton();
            scriptsGameObject.GetComponent<RecordData>().AssignRecordedGameObjects();
            nextTrajectory = false;
            beep.Play(0);
        }
        if (tool1)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().CalibrateToolTipWithReferenceMarkerFromNDIConnect(6);
            tool1 = false;
            beep.Play(0);
        }
        if (tool2)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().CalibrateToolTipWithReferenceMarkerFromNDIConnect(1, true);
            tool2 = false;
            beep.Play(0);
        }
        if (tool3)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().CalibrateToolTipWithReferenceMarkerFromNDIConnect(7);
            tool3 = false;
            beep.Play(0);
        }
        if (tool4)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().CalibrateToolTipWithReferenceMarkerFromNDIConnect(1);
            tool4 = false;
            beep.Play(0);
        }
        if (closeCSVfile)
        {
            scriptsGameObject.GetComponent<RecordData>().CloseDataWriter();
            closeCSVfile = false;
            beep.Play(0);
        }

        if (showNDIvisualization)
        {
            ndiPoseInVuforiaMarker = Matrix4x4.TRS(-ndiCalibrationMatrix.ExtractPosition(), ndiCalibrationMatrix.ExtractRotation(), Vector3.one);
            //print(ndiCalibrationMatrix);
            pointerInMarkerPose = ndiPoseInVuforiaMarker * ndiPort3Marker.transform.localToWorldMatrix.inverse * ndiPort1Marker.transform.localToWorldMatrix;
            ndiPointerSphere.transform.localPosition = 0.001f * new Vector3(-pointerInMarkerPose.m03, -pointerInMarkerPose.m13, -pointerInMarkerPose.m23);

            ndiPointerSphere.transform.localRotation = pointerInMarkerPose.GetRotation() * Quaternion.Euler(90, 0, 0);
        }

        if (NDIcameraStartedMoving)
        {
            scriptsGameObject.GetComponent<RecordData>().isNDIcameraMoving = true;
            NDIcameraStartedMoving = false;
        }
        if (NDIcameraStopedMoving)
        {
            scriptsGameObject.GetComponent<RecordData>().isNDIcameraMoving = false;
            NDIcameraStopedMoving = false;
        }
        if (showCalibCylinder)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().ShowCylincerForOrientationCalibration();
            CalibCylinerIsShown = true;
        }
        if (CalibCylinerIsShown && !showCalibCylinder)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().HideCylincerForOrientationCalibration();
            CalibCylinerIsShown = false;
        }
        if (startMRTooltipCalibration)
        {
            scriptsGameObject.GetComponent<PivotCalibration>().CalibrateToolTipPositionOrientationWithMarker();
            startMRTooltipCalibration = false;
        }

        if(beginRecordingManualRegistrationMetrics)
        {
            manaulCSV.WriteLine($"{manualRegistrationStopWatch.ElapsedMilliseconds.ToString()},{manualPhase},{manualMode},{manualSpeed}");
        }
        if (shouldLoadSavedRegistrationMatrix)
        {
            UnityEngine.Debug.Log("in the update NDI connection ...");
            shouldLoadSavedRegistrationMatrix = false;
            LoadSavedRegistrationMatrix();  
        }
        if (shouldAttachPatientModelToMarker)
        {
            shouldAttachPatientModelToMarker = false;
            AttachAndDetachPatientModelToMarker();
        }
        if (shouldRefreshPatientModelPose)
        {
            shouldRefreshPatientModelPose = false;
            RefreshPatientModelPosition();
        }
        if(shouldUse6DManualManipulation)
        {
            shouldUse6DManualManipulation = false;
            Use6DManualManipulation();
        }
        if (shouldUse1DManualManipulation)
        {
            shouldUse1DManualManipulation = false;
            Use1DManualManipulation();
        }
        if (shouldMovePatientModelFast)
        {
            shouldMovePatientModelFast = false;
            MovePatientModelFast();
        }
        if (shouldMovePatientModelSlow)
        {
            shouldMovePatientModelSlow = false;
            MovePatientModelSlow();
        }
        if (shouldMovePatientModelNormal)
        {
            shouldMovePatientModelNormal = false;
            MovePatientModelNormal();
        }
        if (shouldBeepTCPcommandReceived)
        {
            shouldBeepTCPcommandReceived = false;
            beep.Play(0);
        }
        if (shouldCallRecordPoseDataFromPatientScript)
        {
            shouldCallRecordPoseDataFromPatientScript = false;
            scriptsGameObject.GetComponent<Patient>().AcquirePointerTipPosition();
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
            tipAcquiredPoint.transform.parent = hl2Marker2.transform;
            tipAcquiredPoint.transform.localPosition = tipInPatient;
            tipAcquiredPoint.transform.localScale = Vector3.one * Config.Instance.configFile.unityConfig.landmarksRegistration.sphereRadious;
            tipAcquiredPoint.GetComponent<Renderer>().material.color = Config.Instance.configFile.unityConfig.landmarksRegistration.color.ToColor();

            scriptsGameObject.GetComponent<Patient>().listOfAcquiredTipPoints.Add(tipAcquiredPoint);
        }
    }

    void ActivateVuforiaTracking()
    {
        vuforiaTrackingToggleButton.GetComponent<ToggleCollection>().Toggles[0].ForceSetToggled(true);
        arucoTrackingToggleButton.GetComponent<ToggleCollection>().Toggles[1].ForceSetToggled(false);

#if WINDOWS_UWP
        scriptsGameObject.GetComponent<PVArucoTracking>().StopPVArucoTracking();
#endif
        scriptsGameObject.GetComponent<VuforiaTracking>().StartVuforiaEgnie();

        shouldActivateVuforia = false;
    }

    void ActivateArucoTracking()
    {
        //vuforiaTrackingToggleButton.GetComponent<Interactable>().IsToggled = false;
        //arucoTrackingToggleButton.GetComponent<Interactable>().IsToggled = true;
        vuforiaTrackingToggleButton.GetComponent<ToggleCollection>().Toggles[0].ForceSetToggled(false);
        arucoTrackingToggleButton.GetComponent<ToggleCollection>().Toggles[1].ForceSetToggled(true);

        scriptsGameObject.GetComponent<VuforiaTracking>().StopVuforiaEngine();
#if WINDOWS_UWP
        scriptsGameObject.GetComponent<PVArucoTracking>().StartPVArucoTracking();
#endif

        shouldActivateAruco = false;
    }

    void DeactivateArucoAndVuforiaTracking()
    {
        vuforiaTrackingToggleButton.GetComponent<ToggleCollection>().Toggles[0].ForceSetToggled(false);
        arucoTrackingToggleButton.GetComponent<ToggleCollection>().Toggles[1].ForceSetToggled(false);

        scriptsGameObject.GetComponent<VuforiaTracking>().StopVuforiaEngine();
#if WINDOWS_UWP
        scriptsGameObject.GetComponent<PVArucoTracking>().StopPVArucoTracking();
#endif

        shouldDeactivateVuforiaAruco = false;
    }

    public void ActivateCalibrationMenu()
    {
        toolCalibrationMenu.SetActive(true);

        shouldActivateCalibrationMenu = false;
    }
    public void ActivatePatientMenu()
    {
        patientMenu.SetActive(true);

        shouldActivatePatientMenu = false;
    }
    void DeactivateAllMenus()
    {
        toolCalibrationMenu.SetActive(false);
        patientMenu.SetActive(false);

        shouldDeactivateAllMenus = false;
    }

    void ActivateNDIVisualization()
    {
        if (!ndiMarkerSphere)
        {
            ndiMarkerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ndiMarkerSphere.name = "ndiPoseInVuforiaMarker";
            if (Config.Instance.IsVuforiaTrackingActive)
            {
                trackingModeActive = "Vuforia";
                calibratedHL2Marker = GameObject.Find("VuforiaPatientMarker");
            }
            else if (Config.Instance.IsArucoTrackingActive)
            {
                trackingModeActive = "Aruco";
                calibratedHL2Marker = GameObject.Find("ArucoPatientMarker");
            }
            else
            {
                UnityEngine.Debug.Log("Please activate either Vuforia or Aruco tracking first");
                return;
            }

            ndiMarkerSphere.transform.parent = calibratedHL2Marker.transform;
            print(ndiCalibrationMatrix);
            loadCalibrationMatrix();
            print(ndiCalibrationMatrix);

            ndiMarkerSphere.transform.localPosition = 0.001f * new Vector3(ndiCalibrationMatrix.m03, ndiCalibrationMatrix.m13, ndiCalibrationMatrix.m23);
            ndiMarkerSphere.transform.localRotation = ndiCalibrationMatrix.GetRotation();
            ndiMarkerSphere.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        }


        if (!ndiPointerSphere)
        {
            ndiPointerSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ndiPointerSphere.name = "NDIpointerSphere";
            ndiPointerSphere.transform.parent = calibratedHL2Marker.transform;
            ndiPointerSphere.transform.localScale = new Vector3(0.002f, 0.002f, 0.002f);
        }

        if (!ndiPointerCylinder)
        {
            ndiPointerCylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ndiPointerCylinder.name = "NDIpointerCylinder";
            ndiPointerCylinder.transform.parent = ndiPointerSphere.transform;
            ndiPointerCylinder.transform.localPosition = new Vector3(0, -10, 0);
            ndiPointerCylinder.transform.localScale = new Vector3(1, 10, 1);
        }

        ndiMarkerSphere.SetActive(true);
        ndiPointerSphere.SetActive(true);
        ndiPointerCylinder.SetActive(true);

        shouldActivateNDIVisualization = false;
        showNDIvisualization = true;
    }

    void DeactivateNDIVisualization()
    {
        ndiMarkerSphere.SetActive(false);
        ndiPointerSphere.SetActive(false);
        ndiPointerCylinder.SetActive(false);
        showNDIvisualization = false;
        shouldDeactivateNDIVisualization = false;
    }

    void StartRecordingMetricsForManualAlignment()
    {
        UnityEngine.Debug.Log("starting recording metrics for manual alignment...");
        string filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, "manualRegistrationMetrics.csv");
        manaulCSV = new StreamWriter(filePath);
        manaulCSV.WriteLine("timeStamp,phase,mode,speed");
        manualRegistrationStopWatch.Start();
        beginRecordingManualRegistrationMetrics = true;
    }

    void StopRecordingMetricsForManualAlignment()
    {
        if(beginRecordingManualRegistrationMetrics)
        {
            beginRecordingManualRegistrationMetrics = false;
            manaulCSV.Close();
            manualRegistrationStopWatch.Stop();
            manualRegistrationStopWatch.Reset();
        }
            
    }

    void AttachAndDetachPatientModelToMarker()
    {
        callingToAttachForManualRegistration = true;
        UnityEngine.Debug.Log("attaching patient model to marker ...");
        scriptsGameObject.GetComponent<Patient>().AttachPatientModelToMarker();
        UnityEngine.Debug.Log("detaching patient model to marker ...");
        scriptsGameObject.GetComponent<Patient>().DetachPatientModelFromMarker();
        callingToAttachForManualRegistration = false;
    }

    void LoadSavedRegistrationMatrix()
    {
        UnityEngine.Debug.Log("calling align from patientScript ...");
        scriptsGameObject.GetComponent<Patient>().AlignPatientModel();
    }
    void RefreshPatientModelPosition()
    {
        scriptsGameObject.GetComponent<Patient>().RefreshButtonPressed();
    }

    void Use6DManualManipulation()
    {
        scriptsGameObject.GetComponent<Patient>().deactivate1DManipulationHandles();
    }
    void Use1DManualManipulation()
    {
        scriptsGameObject.GetComponent<Patient>().activate1DManipulationHandles();
    }
    void MovePatientModelFast()
    {
        scriptsGameObject.GetComponent<Patient>().makePatientModelMoveFast();
    }
    void MovePatientModelSlow()
    {
        scriptsGameObject.GetComponent<Patient>().makePatientModelMoveSlow();
    }
    void MovePatientModelNormal()
    {
        scriptsGameObject.GetComponent<Patient>().makePatientModelMoveNormal();
    }


    void loadCalibrationMatrix()
    {
        string filePath = System.IO.Path.Combine(Config.Instance.CalibrationFolder, trackingModeActive + Config.Instance.configFile.ndiConfig.calibrationMatrix);

        StreamReader csvReader = new StreamReader(filePath);
        //var headerLine = csvReader.ReadLine();
        for (int i = 0; i < 4; i++)
        {
            var row = csvReader.ReadLine();
            var values = row.Split(',');
            ndiCalibrationMatrix.SetRow(i, new Vector4(float.Parse(values[0]), float.Parse(values[1]), float.Parse(values[2]), float.Parse(values[3])));
        }
        csvReader.Close();

        // to locate ndi referene in vuforia marker coordinate system
        ndiCalibrationMatrix = ndiCalibrationMatrix.inverse;
    }

    void assingCameraMarker()
    {

        if (Config.Instance.IsVuforiaTrackingActive)
        {
            var imageTarget = Config.Instance.configFile.unityConfig.vuforia.imageTargets[0];
            string markerName = $"Vuforia{imageTarget.name.Substring(0, imageTarget.name.IndexOf("."))}";
            UnityEngine.Debug.Log(markerName);
            hl2Marker1 = GameObject.Find(markerName);
            UnityEngine.Debug.Log(hl2Marker1.name);

            imageTarget = Config.Instance.configFile.unityConfig.vuforia.imageTargets[idx_calibrationMarkerToAssign];
            markerName = $"Vuforia{imageTarget.name.Substring(0, imageTarget.name.IndexOf("."))}";
            UnityEngine.Debug.Log(markerName);
            hl2Marker2 = GameObject.Find(markerName);
            UnityEngine.Debug.Log(hl2Marker2.name);

            mainCamera = GameObject.Find("Main Camera");
        }
        else if (Config.Instance.IsArucoTrackingActive)
        {
            string markerName = $"Aruco{Config.Instance.configFile.nativeConfig.aruco.marker[0].name}";
            hl2Marker1 = GameObject.Find(markerName);
            markerName = $"Aruco{Config.Instance.configFile.nativeConfig.aruco.marker[1].name}";
            hl2Marker2 = GameObject.Find(markerName);
            string cameraName = $"Aruco{Config.Instance.configFile.nativeConfig.aruco.marker[1].name}";
            mainCamera = GameObject.Find(cameraName);
        }
        else
        {
            hl2Marker1 = new GameObject("unAssignedMarker");
            hl2Marker2 = new GameObject("unAssignedMarker");
        }

        if (mainCamera == null)
        {
            mainCamera = new GameObject("unAssignedCamera");
        }

        alreadyAssigned = true;

    }

    // This function should be called after loading the config file.
    public void StartNDIConnection()
    {
        ndiIpAddress = Config.Instance.configFile.ndiConfig.ipAddress;
        ndiPortNumber = Config.Instance.configFile.ndiConfig.portNumber;

        UnityEngine.Debug.Log($"Set NDI connection ip address: {ndiIpAddress}, port number: {ndiPortNumber}");

        if (ndiIpAddress.Length == 0 && ndiPortNumber == 0) { return; }

        try
        {
            tcpClientThread = new Thread(new ThreadStart(ListenForData));
            tcpClientThread.IsBackground = true;
            tcpClientThread.Start();

            CreateNDIGameObjects();

            ndiConnectionActive = true;
            listening = true;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log("On client connect exception " + e);
        }
    }

    void ListenForData()
    {
        // to confirm data trasform of initial pose -- TODO: remove it after checking
        bool printFlag = false;
        try
        {
            socketConnection = new TcpClient(ndiIpAddress, ndiPortNumber);
            byte[] bytes = new byte[2048];
            Matrix4x4 transformation = Matrix4x4.identity;

            while (listening)
            {
                // Get a stream object for reading 
                using (NetworkStream stream = socketConnection.GetStream())
                {
                    int length;
                    float[] m = new float[12];

                    while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        byte[] incommingData = new byte[length];
                        Array.Copy(bytes, 0, incommingData, 0, length);
                        string serverMessage = Encoding.ASCII.GetString(incommingData);

                        if (serverMessage.Contains("RecordMarkerPose"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            RecordPoseData();
                        }
                        else if (serverMessage.Contains("Vuforia"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldActivateVuforia = true;
                        }
                        else if (serverMessage.Contains("Aruco"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldActivateAruco = true;
                        }
                        else if (serverMessage.Contains("NoTracking"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldDeactivateVuforiaAruco = true;
                        }
                        else if (serverMessage.Contains("CalibrationMenu"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldActivateCalibrationMenu = true;

                            pointerTipCalibrated = true;
                        }
                        else if (serverMessage.Contains("PatientMenu"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldActivatePatientMenu = true;

                            patientModelActivated = true;

                        }
                        else if (serverMessage.Contains("NoMenus"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldDeactivateAllMenus = true;
                        }
                        else if (serverMessage.Contains("ShowNDI"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldActivateNDIVisualization = true;
                        }
                        else if (serverMessage.Contains("HideNDI"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldDeactivateNDIVisualization = true;
                        }
                        else if (serverMessage.Contains("Begin"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            beginRecordingData = true;
                            //scriptsGameObject.GetComponent<RecordData>().InsertionBegin();
                        }
                        else if (serverMessage.Contains("Done"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            doneRecordingData = true;
                            //scriptsGameObject.GetComponent<RecordData>().InsertionFinish();
                        }
                        else if (serverMessage.Contains("Previous"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            previousTrajectory = true;
                        }
                        else if (serverMessage.Contains("Next"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            nextTrajectory = true;
                        }
                        else if (serverMessage.Contains("UpIncisionNum"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            incision_counter++;
                            recordedFilePrefix = $"incision_{incision_counter}";
                            UnityEngine.Debug.Log($"++{recordedFilePrefix}");
                            recordingFileCounter = 1;
                        }
                        else if (serverMessage.Contains("DownIncisionNum"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            incision_counter--;
                            recordedFilePrefix = $"incision_{incision_counter}";
                            UnityEngine.Debug.Log($"--{recordedFilePrefix}");
                            recordingFileCounter = 1;
                        }
                        else if (serverMessage.Contains("ShowCalibCylinder"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            showCalibCylinder = true;
                        }
                        else if (serverMessage.Contains("HideCalibCylinder"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            showCalibCylinder = false;
                        }
                        else if (serverMessage.Contains("StartMRTooltipCalibration"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            startMRTooltipCalibration = true;
                        }
                        else if (serverMessage.Contains("StartManualDataRecording"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            StartRecordingMetricsForManualAlignment();
                            shouldBeepTCPcommandReceived = true;
                            manualSpeed = "Fast";
                            manualMode = "6D";
                        }
                        else if (serverMessage.Contains("StopManualDataRecording"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            StopRecordingMetricsForManualAlignment();
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("ManualPhaseTrain"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualPhase = "Train";
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("ManualPhaseAlignTask"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualPhase = "AlignTask";
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("ManualPhaseDelineate"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualPhase = "Delineate";
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("PointBasedRegistration"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualPhase = "Point-based";
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("AttachDetachToMarker"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldAttachPatientModelToMarker = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("LoadRegistrationMatrix"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldLoadSavedRegistrationMatrix = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("RefreshPatientModel"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            shouldRefreshPatientModelPose = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("FastManipulation"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualSpeed = "Fast";
                            shouldMovePatientModelFast = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("NormalSpeedManipulation"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualSpeed = "Average";
                            shouldMovePatientModelNormal = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("SlowManipulation"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualSpeed = "Slow";
                            shouldMovePatientModelSlow = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("FreeManipulation"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualMode = "6D";
                            shouldUse6DManualManipulation = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("1DWidgets"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            manualMode = "1D";
                            shouldUse1DManualManipulation = true;
                            shouldBeepTCPcommandReceived = true;
                        }
                        else if (serverMessage.Contains("AcquirePoint"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            if (recordedFilePrefix.Contains("incision"))
                                RecordPoseData();
                            else
                                shouldCallRecordPoseDataFromPatientScript = true;
                        }
                        //else if (serverMessage.Contains("Tool1"))
                        //{
                        //    Debug.Log($"Received message from server: {serverMessage}");
                        //    tool1 = true;;
                        //}
                        //else if (serverMessage.Contains("Tool2")) // This one should be performed from the HoloLens (Calibrated tool)
                        //{
                        //    Debug.Log($"Received message from server: {serverMessage}");
                        //    tool2 = true;
                        //}
                        //else if (serverMessage.Contains("Tool3"))
                        //{
                        //    Debug.Log($"Received message from server: {serverMessage}");
                        //    tool3 = true;
                        //}
                        //else if (serverMessage.Contains("Tool4"))
                        //{
                        //    Debug.Log($"Received message from server: {serverMessage}");
                        //    tool4 = true;
                        //}
                        else if (serverMessage.Contains("CloseCSV"))
                        {
                            UnityEngine.Debug.Log($"Received message from server: {serverMessage}");
                            closeCSVfile = true;
                        }
                        else
                        {
                            bool transformParsingFailed = false;

                            string msg = serverMessage.Substring(5);

                            string[] tools = msg.Split('*');
                            for (int i = 0; i < tools.Length; i++)
                            {
                                string[] numbers = tools[i].Split('/');
                                int j = 2;
                                if (numbers.Length == 14)
                                {
                                    while (j < 14)
                                    {
                                        try
                                        {
                                            m[j - 2] = float.Parse(numbers[j]);
                                            j++;
                                        }
                                        catch
                                        {
                                            transformParsingFailed = true;
                                            print("parsing number failed: " + numbers[j]);
                                            print(numbers);
                                            print("parsing number index: " + j);
                                            print(msg);
                                        }

                                    }

                                    if (!transformParsingFailed)
                                    {
                                        transformation.SetRow(0, new Vector4(m[0], m[1], m[2], m[3]));
                                        transformation.SetRow(1, new Vector4(m[4], m[5], m[6], m[7]));
                                        transformation.SetRow(2, new Vector4(m[8], m[9], m[10], m[11]));
                                        transformation.SetRow(3, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));

                                        if (numbers[1].Equals(NDI_POINTER_FLAG))
                                        {
                                            //Debug.Log("pointer = " + transformation);
                                            pointerQueue.Enqueue(transformation);
                                        }
                                        else if (numbers[1].Equals(NDI_PATIENT_FLAG))
                                        {
                                            //Debug.Log("patient = " + transformation);
                                            patientQueue.Enqueue(transformation);
                                        }
                                        else if (numbers[1].Equals(NDI_CALIBRATION_BOARD_FLAG))
                                        {
                                            //Debug.Log("board = " + transformation);
                                            calibrationBoardQueue.Enqueue(transformation);
                                        }
                                    }

                                }
                            }
                        }

                    }
                }
            }

        }
        catch (SocketException socketException)
        {
            UnityEngine.Debug.Log("On client data listening socket exception " + socketException);
        }
    }

    void CreateNDIGameObjects()
    {
        if (ndiPort1Marker == null && ndiPort2Marker == null && ndiPort3Marker == null)
        {
            ndiPort1Marker = new GameObject(Config.Instance.configFile.ndiConfig.markerNamePort1);
            ndiPort2Marker = new GameObject(Config.Instance.configFile.ndiConfig.markerNamePort2);
            ndiPort3Marker = new GameObject(Config.Instance.configFile.ndiConfig.markerNamePort3);
        }
    }

    public void RecordPoseData()
    {
        if (!alreadyAssigned)
        {
            souldAssignMarkers = true;
            return;
        }
        // create recording folder if it doesn't exist
        string markerTrackingFolder = System.IO.Path.Combine(Config.Instance.ApplicationFolder, "recordings");
        if (!Directory.Exists(markerTrackingFolder))
            Directory.CreateDirectory(markerTrackingFolder);

        string fileName = System.IO.Path.Combine(markerTrackingFolder, $"{recordedFilePrefix}_{recordingFileCounter.ToString()}.csv");

        if (Config.Instance.IsVuforiaTrackingActive)
        {
            if (hl2Marker1.GetComponent<MarkerTrackingStatus>().Tracked || Config.Instance.configFile.unityConfig.ignoreInvalidMarkerPosition)
            {
                fileName = System.IO.Path.Combine(markerTrackingFolder, $"v_{recordedFilePrefix}_{recordingFileCounterVuforia.ToString()}.csv");
                recordingFileCounterVuforia++;
                ValidVuforiaPoseShouldBeep = true;
            }
            else
            {
                fileName = System.IO.Path.Combine(markerTrackingFolder, $"v_{recordedFilePrefix}_{recordingFileCounterVuforia.ToString()}_INVALID.csv");
                ValidVuforiaPoseShouldBeep = false;
            }
        }
        else if (Config.Instance.IsArucoTrackingActive)
        {
            fileName = System.IO.Path.Combine(markerTrackingFolder, $"a_{recordedFilePrefix}_{recordingFileCounterAruco.ToString()}.csv");
            recordingFileCounterAruco++;
            ValidVuforiaPoseShouldBeep = true;
        }

        poseSW = new StreamWriter(fileName);

        writeCSVheader = true;

        // flag to start collecting data in update()
        souldAssignMarkers = false;
        recordDataActive = true;
        recordingFileCounter++;

        if (!recordedFilePrefix.Contains("incision"))
            recordedFilePrefix = "test";
    }

    public void RecordPoseData(bool forRegistration)
    {
        if (forRegistration)
        {
            useRecordedPointsForRegistration = true;
            print("called from registration");
            // make sure hl2_marker is set to patient marker
            idx_calibrationMarkerToAssign = 1;
        }
        RecordPoseData();
    }

    public void RecordPoseData(bool forCalibration, string calibrationMethod)
    {
        if (forCalibration)
        {
            useRecordedPointsForCalibration = true;
            recordedFilePrefix = calibrationMethod;
            print("called from calibration");

            if (calibrationMethod == "calibration_tool")
                idx_calibrationMarkerToAssign = 2;
            else
                idx_calibrationMarkerToAssign = 1;
        }
        RecordPoseData();
    }

    public void StopNDIConnection()
    {
        listening = false;
        // close the socket connection
        try
        {
            socketConnection.Close();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message);
        }

        // Close the TCP client listener
        try
        {
            tcpClientThread.Abort();
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.Message);
        }
    }
}
