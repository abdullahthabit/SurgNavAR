using UnityEngine;
using System.Collections.Generic;

// TODO: update the strucutre of the config file... remove unity config and native config, group them by function
[System.Serializable]
public class ConfigFile
{
    public UnityConfig unityConfig;
    public NativeConfig nativeConfig;
    public NDIConfig ndiConfig;
}

[System.Serializable]
public class NDIConfig
{
    public string ipAddress;
    public int portNumber;
    public string markerNamePort1;
    public string markerNamePort2;
    public string markerNamePort3;
    public string calibrationMatrix;
}

[System.Serializable]
public class Registration
{
    public string registrationMatrix;
    public string sourcePoints;
    public string targetPoints;
    public string recordedData;
}

[System.Serializable]
public class UnityConfig
{
    public PatientModel patientModel;
    public VuforiaConfig vuforia;
    public LandmarksRegistration landmarksRegistration;
    public Registration registration;
    public Vector3 calibrationCylinderPoisitionVuforia;
    public Vector3 calibrationCylinderPoisitionAruco;
    public int calibrationTimeInSeconds;
    public int pivotCalibrationTimeInSeconds;
    public bool recordDataActive;
    public bool ignoreInvalidMarkerPosition;
    public bool rotateDistanceMenu;
    public bool saveMatrixButNotAttach;
    public int sizeOf1Dwidgets;
    public bool disk1Active;
    public bool disk2Active;
    public bool disk3Active;
    public float disk1Diameter;
    public float disk2Diameter;
    public float disk3Diameter;
    public bool shouldSaveTooltipCalibration;
}

[System.Serializable]
public class PatientModel
{
    public float scale;
    public List<Models> models;
    public Vector3 distFeedbackMenuPosition;
    public List<PreopPlans> preopPlans;
    public int numberOfTrainingTasks;
}

[System.Serializable]
public class Models
{
    public string fileName;
    public bool showModel;
    public string material;
    public string color;
}

[System.Serializable]
public class PreopPlans
{
    public string trajectoryName;
    public bool showTrajectory;
    public string material;
    public string color;
    public Vector3 entryPoint;
    public Vector3 exitPoint;
}

[System.Serializable]
public class VuforiaConfig
{
    public int numberOfImageTargets;
    public List<VuforiaImageTarget> imageTargets;

}

[System.Serializable]
public class VuforiaImageTarget
{
    public string name;
    public float widthInMeters;
    public int trackingStatusFiliter;
    public string outlineColor;
    public bool isTool;
    public ToolCalibration toolCalibration;
    public bool isPatient;
    public bool isCalibrationTool;
}

[System.Serializable]
public class ToolCalibration
{
    public Vector3 tipOffset;
    public Quaternion tipOrientation;
    public float diameter;
}

[System.Serializable]
public class LandmarksRegistration
{
    public string fileName;
    public string material;
    public string color;
    public float sphereRadious;
    public bool useSubset;
    public List<int> subsetIndeces;
}

[System.Serializable]
public class NativeConfig
{
    public PVCamera pvCamera;
    public Aruco aruco;
}

[System.Serializable]
public class PVCamera
{
    public bool pvActive;
    public int width;
    public int height;
}

[System.Serializable]
public class Aruco
{
    public int numberOfMarkers;
    public float markerSize;
    public List<ArucoMarker> marker;
    public bool trackTheMarker;
    public bool trackTheBoard;
    public bool trackNormal;
}

[System.Serializable]
public class ArucoMarker
{
    public string name;
    public int trackingStatusFiliter;
    public string outlineColor;
    public bool isTool;
    public ToolCalibration toolCalibration;
    public bool isPatient;
    public bool isCalibrationTool;
}
