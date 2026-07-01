using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Diagnostics;
using TMPro;
using System;

// class used for recording data mainly for offline experimentaiton and analysis
public class RecordData : MonoBehaviour
{
	StreamWriter csvWriter;
    Stopwatch stopwatch;
    bool startRecording = false;
	string dataFilePath;

	GameObject drillEndPoint;
	GameObject drillEntryPoint;
	GameObject planningEndPoint;
	GameObject planningEntryPoint;

	int phaseIndex = 0;
	int drillIndex = 0;
	bool insertionStatus = false;

    public GameObject scriptsGameObject;

	float tipToEntryDist;
	public float tipToExitDist;
	public float tipToAxisDist;
	public float angleDeviation;

	public GameObject distanceFeedbackMenu;
    public TextMeshProUGUI textDistExit;
    public TextMeshProUGUI textDistAxis;
	public TextMeshProUGUI textAngle;

	GameObject drill;

	GameObject hl2pointer;
	GameObject hl2patient;
	GameObject ndipointer;
	GameObject ndipatient;

	public bool isNDIcameraMoving = false;

	string trackingModeActive;

	void Start()
	{
		stopwatch = new Stopwatch();

		drill = new GameObject();

		tipToExitDist = 1000.0f;
		tipToAxisDist = 1000.0f;
		angleDeviation = 1000.0f;
	}

	// Update is called once per frame
	void Update()
	{
		if (startRecording)
		{
			tipToEntryDist = CalculateDistanceBetweenTwoPoints(planningEntryPoint.transform.position, drillEndPoint.transform.position) * 1000;
			tipToExitDist = CalculateDistanceBetweenTwoPoints(planningEndPoint.transform.position, drillEndPoint.transform.position) * 1000;
			tipToAxisDist = CalculateDistancePointToLine(drillEndPoint.transform.position, planningEntryPoint.transform.position, planningEndPoint.transform.position) * 1000;
			angleDeviation = Vector3.Angle(drillEndPoint.transform.up, planningEndPoint.transform.up);

			textDistExit.GetComponent<TextMeshProUGUI>().text = tipToExitDist.ToString("F1");
			textDistAxis.GetComponent<TextMeshProUGUI>().text = tipToAxisDist.ToString("F1");
			textAngle.GetComponent<TextMeshProUGUI>().text = angleDeviation.ToString("F1");
			
			// Recording markers poses can be used for experimentation and validation

			//csvWriter.WriteLine(drillEndPoint.transform.position.x + "," + drillEndPoint.transform.position.y + "," + drillEndPoint.transform.position.z + ","
			//+ drillEndPoint.transform.up.x + "," + drillEndPoint.transform.up.y + "," + drillEndPoint.transform.up.z + ","
			//+ planningEndPoint.transform.position.x + "," + planningEndPoint.transform.position.y + "," + planningEndPoint.transform.position.z + ","
			//+ planningEndPoint.transform.up.x + "," + planningEndPoint.transform.up.y + "," + planningEndPoint.transform.up.z + ","
			//+ stopwatch.ElapsedMilliseconds.ToString() + "," + isNDIcameraMoving + "," + angleDeviation + ","
   //         + drillEntryPoint.transform.position.x + "," + drillEntryPoint.transform.position.y + "," + drillEntryPoint.transform.position.z + ","
			//+ planningEntryPoint.transform.position.x + "," + planningEntryPoint.transform.position.y + "," + planningEntryPoint.transform.position.z + ","
			//+ tipToEntryDist + "," + tipToExitDist + "," + tipToAxisDist + "," + phaseIndex + "," + drillIndex + "," + insertionStatus + ","
			//+ hl2pointer.transform.position.x + "," + hl2pointer.transform.position.y + "," + hl2pointer.transform.position.z + ","
			//+ hl2pointer.transform.rotation.x + "," + hl2pointer.transform.rotation.y + "," + hl2pointer.transform.rotation.z + "," + hl2pointer.transform.rotation.w + ","
			//+ hl2patient.transform.position.x + "," + hl2patient.transform.position.y + "," + hl2patient.transform.position.z + ","
			//+ hl2patient.transform.rotation.x + "," + hl2patient.transform.rotation.y + "," + hl2patient.transform.rotation.z + "," + hl2patient.transform.rotation.w + ","
			//+ ndipointer.transform.position.x + "," + ndipointer.transform.position.y + "," + ndipointer.transform.position.z + ","
			//+ ndipointer.transform.rotation.x + "," + ndipointer.transform.rotation.y + "," + ndipointer.transform.rotation.z + "," + ndipointer.transform.rotation.w + ","
			//+ ndipatient.transform.position.x + "," + ndipatient.transform.position.y + "," + ndipatient.transform.position.z + ","
			//+ ndipatient.transform.rotation.x + "," + ndipatient.transform.rotation.y + "," + ndipatient.transform.rotation.z + "," + ndipatient.transform.rotation.w);
		}
	}

	public void StartRecordingData()
	{

        DateTime currentTime = DateTime.Now;
        string time = currentTime.ToString("_HH_mm_ss");
        UnityEngine.Debug.Log(time);

        stopwatch.Start();
		// TODO: fix the file name to include the .csv extension in the name from the config file
        string fileName = Config.Instance.configFile.unityConfig.registration.recordedData + time + ".csv";
        dataFilePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, fileName);

        // Recording markers poses can be used for experimentation and validation
        //csvWriter = new StreamWriter(dataFilePath);

        //csvWriter.WriteLine("e_drill_p_x" + "," + "e_drill_p_y" + "," + "e_drill_p_z" + ","
        //	+ "e_drill_o_x" + "," + "e_drill_o_y" + "," + "e_drill_o_z" + ","
        //	+ "e_plan_p_x" + "," + "e_plan_p_y" + "," + "e_plan_p_z" + ","
        //	+ "e_plan_o_x" + "," + "e_plan_o_y" + "," + "e_plan_o_z" + ","
        //	+ "timestamp" + "," + "isNDIcameraMoving" + "," + "angle_drill_plan_e" + ","
        //	+ "s_drill_p_x" + "," + "s_drill_p_y" + "," + "s_drill_p_z" + ","
        //	+ "s_plan_p_x" + "," + "s_plan_p_y" + "," + "s_plan_p_z" + ","
        //	+ "tipToEntryDist" + "," + "tipToExitDist" + "," + "tipToAxisDist" + ","
        //	+ "traj_counter" + "," + "drill_index" + "," + "startedInsertion" + ","
        //	+ "hl2_pointer_p_x" + "," + "hl2_pointer_p_y" + "," + "hl2_pointer_p_z" + ","
        //	+ "hl2_pointer_o_x" + "," + "hl2_pointer_o_y" + "," + "hl2_pointer_o_z" + "," + "hl2_pointer_o_w" + ","
        //	+ "hl2_patient_p_x" + "," + "hl2_patient_p_y" + "," + "hl2_patient_p_z" + ","
        //	+ "hl2_patient_o_x" + "," + "hl2_patient_o_y" + "," + "hl2_patient_o_z" + "," + "hl2_patient_o_w" + ","
        //	+ "ndi_pointer_p_x" + "," + "ndi_pointer_p_y" + "," + "ndi_pointer_p_z" + ","
        //	+ "ndi_pointer_o_x" + "," + "ndi_pointer_o_y" + "," + "ndi_pointer_o_z" + "," + "ndi_pointer_o_w" + ","
        //	+ "ndi_patient_p_x" + "," + "ndi_patient_p_y" + "," + "ndi_patient_p_z" + ","
        //	+ "ndi_patient_o_x" + "," + "ndi_patient_o_y" + "," + "ndi_patient_o_z" + "," + "ndi_patient_o_w");

        if (Config.Instance.IsVuforiaTrackingActive)
		{
			trackingModeActive = "Vuforia";
		}
		else if (Config.Instance.IsArucoTrackingActive)
		{
			trackingModeActive = "Aruco";
		}

		AssignRecordedGameObjects();
		AssignMarkersGameObjects();

        startRecording = true;
        distanceFeedbackMenu.SetActive(true);
        distanceFeedbackMenu.transform.parent = hl2pointer.transform;
		distanceFeedbackMenu.transform.localPosition = Config.Instance.configFile.unityConfig.patientModel.distFeedbackMenuPosition;

		if (Config.Instance.configFile.unityConfig.rotateDistanceMenu)
			distanceFeedbackMenu.transform.localRotation = Quaternion.Euler(90, 0, 90);
		else
			distanceFeedbackMenu.transform.localRotation = Quaternion.Euler(90, 0, 0);

		distanceFeedbackMenu.transform.localScale = new Vector3(0.2f, 0.3f, 1);
    }

	void AssignMarkersGameObjects()
    {
		hl2pointer = GameObject.Find($"{trackingModeActive}PointerMarker");
		hl2patient = GameObject.Find($"{trackingModeActive}PatientMarker");
		ndipointer = GameObject.Find(Config.Instance.configFile.ndiConfig.markerNamePort1);
		ndipatient = GameObject.Find(Config.Instance.configFile.ndiConfig.markerNamePort2);

		if (hl2pointer == null)
			hl2pointer = new GameObject("emptyhl2pointer");
		if (hl2patient == null)
			hl2patient = new GameObject("emptyhl2patient");
		if (ndipointer == null)
			ndipointer = new GameObject("emptyndipointer");
		if (ndipatient == null)
			ndipatient = new GameObject("emptyndipatient");
	}

	// to be called from the scene everytime the next or back buttons is pressed (to assing the preplanning phase and gameobjects as well as the drill gameobject)
	public void AssignRecordedGameObjects()
	{
		AssignPlanning();

		AssignDrill();
	}

	// figure out how to assing the planning gameobjects
	void AssignPlanning()
	{
		phaseIndex = scriptsGameObject.GetComponent<Patient>().selectedTrajectoryIndex;

		GameObject plan = scriptsGameObject.GetComponent<Patient>().trajectoriesList[scriptsGameObject.GetComponent<Patient>().selectedTrajectoryIndex];

		planningEndPoint = plan.transform.parent.gameObject;
		UnityEngine.Debug.Log("exit: " + planningEndPoint.name);
		string entryGoName = "entry_" + planningEndPoint.name.Substring(5);
		UnityEngine.Debug.Log("entry:" + entryGoName);
		planningEntryPoint = GameObject.Find(entryGoName);
	}

	void AssignDrill()
	{
		string filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, "tooltipTransform.txt");
		StreamWriter csvWriter = new StreamWriter(filePath);

		// what if it fails?
		foreach (Transform child in GameObject.Find($"{trackingModeActive}PointerMarker").transform)
		{
			if (child.gameObject.name.Equals("tooltip"))
            {
				drill = child.gameObject;

				Matrix4x4 drillMatrix = Matrix4x4.TRS(drill.transform.localPosition, drill.transform.localRotation, drill.transform.localScale);
				UnityEngine.Debug.Log($"tooltip \n{drillMatrix}");
				csvWriter.WriteLine("column1, column2, colum3, column4");
				csvWriter.WriteLine(drillMatrix.m00 + "," + drillMatrix.m01 + "," + drillMatrix.m02 + "," + drillMatrix.m03);
				csvWriter.WriteLine(drillMatrix.m10 + "," + drillMatrix.m11 + "," + drillMatrix.m12 + "," + drillMatrix.m13);
				csvWriter.WriteLine(drillMatrix.m20 + "," + drillMatrix.m21 + "," + drillMatrix.m22 + "," + drillMatrix.m23);
				csvWriter.WriteLine(drillMatrix.m30 + "," + drillMatrix.m31 + "," + drillMatrix.m32 + "," + drillMatrix.m33);
				csvWriter.Close();
			}	
		}

		drillEndPoint = drill.transform.GetChild(0).gameObject;
		UnityEngine.Debug.Log("drillEndPoint:" + drillEndPoint.name);
		drillEntryPoint = drill.transform.GetChild(0).GetChild(0).GetChild(1).gameObject;
		UnityEngine.Debug.Log("drillEntryPoint:" + drillEntryPoint.name);

		drillIndex = scriptsGameObject.GetComponent<PivotCalibration>().drillCalibrationDropDown.GetComponent<TMP_Dropdown>().value;

		Matrix4x4 drillEntryMiddleCylinderMatrix = Matrix4x4.TRS(drill.transform.GetChild(0).GetChild(0).transform.localPosition, drill.transform.GetChild(0).GetChild(0).transform.localRotation, drill.transform.GetChild(0).GetChild(0).transform.localScale);
		UnityEngine.Debug.Log($"tooltip - {drill.transform.GetChild(0).GetChild(0).name} \n{drillEntryMiddleCylinderMatrix}");
		filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, $"tooltipEndPoint_{drill.transform.GetChild(0).GetChild(0).name}.txt");
		csvWriter = new StreamWriter(filePath);
		csvWriter.WriteLine("column1, column2, colum3, column4");
		csvWriter.WriteLine(drillEntryMiddleCylinderMatrix.m00 + "," + drillEntryMiddleCylinderMatrix.m01 + "," + drillEntryMiddleCylinderMatrix.m02 + "," + drillEntryMiddleCylinderMatrix.m03);
		csvWriter.WriteLine(drillEntryMiddleCylinderMatrix.m10 + "," + drillEntryMiddleCylinderMatrix.m11 + "," + drillEntryMiddleCylinderMatrix.m12 + "," + drillEntryMiddleCylinderMatrix.m13);
		csvWriter.WriteLine(drillEntryMiddleCylinderMatrix.m20 + "," + drillEntryMiddleCylinderMatrix.m21 + "," + drillEntryMiddleCylinderMatrix.m22 + "," + drillEntryMiddleCylinderMatrix.m23);
		csvWriter.WriteLine(drillEntryMiddleCylinderMatrix.m30 + "," + drillEntryMiddleCylinderMatrix.m31 + "," + drillEntryMiddleCylinderMatrix.m32 + "," + drillEntryMiddleCylinderMatrix.m33);
		csvWriter.Close();

		Matrix4x4 drillEndPointMatrix = Matrix4x4.TRS(drillEndPoint.transform.localPosition, drillEndPoint.transform.localRotation, drillEndPoint.transform.localScale);
		UnityEngine.Debug.Log($"tooltip - {drillEndPoint.name} \n{drillEndPointMatrix}");
		filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, $"tooltipEndPoint_{drillEndPoint.name}.txt");
		csvWriter = new StreamWriter(filePath);
		csvWriter.WriteLine("column1, column2, colum3, column4");
		csvWriter.WriteLine(drillEndPointMatrix.m00 + "," + drillEndPointMatrix.m01 + "," + drillEndPointMatrix.m02 + "," + drillEndPointMatrix.m03);
		csvWriter.WriteLine(drillEndPointMatrix.m10 + "," + drillEndPointMatrix.m11 + "," + drillEndPointMatrix.m12 + "," + drillEndPointMatrix.m13);
		csvWriter.WriteLine(drillEndPointMatrix.m20 + "," + drillEndPointMatrix.m21 + "," + drillEndPointMatrix.m22 + "," + drillEndPointMatrix.m23);
		csvWriter.WriteLine(drillEndPointMatrix.m30 + "," + drillEndPointMatrix.m31 + "," + drillEndPointMatrix.m32 + "," + drillEndPointMatrix.m33);
		csvWriter.Close();

		Matrix4x4 drillEntryPointMatrix = Matrix4x4.TRS(drillEntryPoint.transform.localPosition, drillEntryPoint.transform.localRotation, drillEntryPoint.transform.localScale);
		UnityEngine.Debug.Log($"tooltip - {drillEntryPoint.name} \n{drillEntryPointMatrix}");
		filePath = System.IO.Path.Combine(Config.Instance.RegistrationFolderOut, $"tooltipEntryPoint_{drillEntryPoint.name}.txt");
		csvWriter = new StreamWriter(filePath);
		csvWriter.WriteLine("column1, column2, colum3, column4");
		csvWriter.WriteLine(drillEntryPointMatrix.m00 + "," + drillEntryPointMatrix.m01 + "," + drillEntryPointMatrix.m02 + "," + drillEntryPointMatrix.m03);
		csvWriter.WriteLine(drillEntryPointMatrix.m10 + "," + drillEntryPointMatrix.m11 + "," + drillEntryPointMatrix.m12 + "," + drillEntryPointMatrix.m13);
		csvWriter.WriteLine(drillEntryPointMatrix.m20 + "," + drillEntryPointMatrix.m21 + "," + drillEntryPointMatrix.m22 + "," + drillEntryPointMatrix.m23);
		csvWriter.WriteLine(drillEntryPointMatrix.m30 + "," + drillEntryPointMatrix.m31 + "," + drillEntryPointMatrix.m32 + "," + drillEntryPointMatrix.m33);
		csvWriter.Close();
	}

	public void CloseDataWriter()
    {
		startRecording = false;
		if(csvWriter !=null)
			csvWriter.Close();
    }

	float CalculateDistanceBetweenTwoPoints(Vector3 refPoint, Vector3 targetPoint)
    {
		double valx = (refPoint.x - targetPoint.x) * (refPoint.x - targetPoint.x);
		double valy = (refPoint.y - targetPoint.y) * (refPoint.y - targetPoint.y);
		double valz = (refPoint.z - targetPoint.z) * (refPoint.z - targetPoint.z);

		float rmse = (float) Math.Sqrt(valx + valy + valz);

		return rmse;
    }

	float CalculateDistancePointToLine(Vector3 drillEndPoint, Vector3 planEntryPoint, Vector3 planEndPoint)
	{
		Vector3 vectorAP = drillEndPoint - planEntryPoint;
		Vector3 vectorAB = planEndPoint - planEntryPoint;

		Vector3 crossProduct = Vector3.Cross(vectorAP, vectorAB);
		float magnitudeAB = vectorAB.magnitude;
		float distance = crossProduct.magnitude / magnitudeAB;

		return distance;
	}
}
