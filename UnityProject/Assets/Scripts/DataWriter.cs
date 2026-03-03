using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class DataWriter : MonoBehaviour
{
    public static void WriteToolCalibrationDataToCSV(string toolName, List<Matrix4x4> toolCalibrationPoses)
    {
        string fileName = toolName + ".csv";
        string filePath = System.IO.Path.Combine(Config.Instance.CalibrationFolderOut, fileName);
        Debug.Log("file path: " + filePath);

        StreamWriter streamWriter = new StreamWriter(filePath);

        foreach(var toolPose in toolCalibrationPoses)
        {
            string pose = toolPose.m00 + "," + toolPose.m01 + "," + toolPose.m02 + "," + toolPose.m03 + "," +
                          toolPose.m10 + "," + toolPose.m11 + "," + toolPose.m12 + "," + toolPose.m13 + "," +
                          toolPose.m20 + "," + toolPose.m21 + "," + toolPose.m22 + "," + toolPose.m23 + "," +
                          toolPose.m30 + "," + toolPose.m31 + "," + toolPose.m32 + "," + toolPose.m33;

            streamWriter.WriteLine(pose);
        }
        streamWriter.Close();
        Debug.Log("closing streamWriter...");
    }
}
