using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

public class Utils : MonoBehaviour
{
    // source: https://stackoverflow.com/questions/10316997/how-can-i-read-json-content-with-a-comment-with-json-net
    public static string StripComments(string input)
    {
        // JavaScriptSerializer doesn't accept commented-out JSON,
        // so we'll strip them out ourselves;
        // NOTE: for safety and simplicity, we only support comments on their own lines,
        // not sharing lines with real JSON

        input = Regex.Replace(input, @"^\s*//.*$", "", RegexOptions.Multiline);  // removes comments like this
        input = Regex.Replace(input, @"^\s*/\*(\s|\S)*?\*/\s*$", "", RegexOptions.Multiline); /* comments like this */

        return input;
    }

    public static Vector3 computePointsCentroid(List<Vector3> points)
    {
        Vector3 sum = Vector3.zero;
        foreach (var position in points)
        {
            sum.x += position.x;
            sum.y += position.y;
            sum.z += position.z;
        }

        Vector3 centroid = new Vector3(sum.x / points.Count, sum.y / points.Count, sum.z / points.Count);

        return centroid;
    }

    public static Quaternion computeAverageQuaternion(List<Quaternion> quaternions)
    {
        Quaternion averageQuat = Quaternion.identity;
        if (quaternions.Count > 0)
        {
            Vector3 forwardSum = Vector3.zero;
            Vector3 upwardSum = Vector3.zero;

            foreach (var quat in quaternions)
            {
                forwardSum += (quat * Vector3.forward);
                upwardSum += (quat * Vector3.up);
            }

            forwardSum /= (float)quaternions.Count;
            upwardSum /= (float)quaternions.Count;

            averageQuat = Quaternion.LookRotation(forwardSum, upwardSum);
        }

        return averageQuat;
    }
}


// class extending the functionalities of Unity Matrix4x4
public static class MatrixExtensions
{
    // Get a qaternion from a Matrix4x4 rotation matrix
    public static Quaternion GetRotation(this Matrix4x4 m)
    {
        // Adapted from: http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/index.htm
        Quaternion q = new Quaternion();
        q.w = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] + m[1, 1] + m[2, 2])) / 2;
        q.x = Mathf.Sqrt(Mathf.Max(0, 1 + m[0, 0] - m[1, 1] - m[2, 2])) / 2;
        q.y = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] + m[1, 1] - m[2, 2])) / 2;
        q.z = Mathf.Sqrt(Mathf.Max(0, 1 - m[0, 0] - m[1, 1] + m[2, 2])) / 2;
        q.x *= Mathf.Sign(q.x * (m[2, 1] - m[1, 2]));
        q.y *= Mathf.Sign(q.y * (m[0, 2] - m[2, 0]));
        q.z *= Mathf.Sign(q.z * (m[1, 0] - m[0, 1]));
        return q;
    }

    // Source: https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
    public static Quaternion ExtractRotation(this Matrix4x4 matrix)
    {
        Vector3 forward;
        forward.x = matrix.m02;
        forward.y = matrix.m12;
        forward.z = matrix.m22;

        Vector3 upwards;
        upwards.x = matrix.m01;
        upwards.y = matrix.m11;
        upwards.z = matrix.m21;

        return Quaternion.LookRotation(forward, upwards);
    }

    public static Vector3 ExtractPosition(this Matrix4x4 matrix)
    {
        Vector3 position;
        position.x = matrix.m03;
        position.y = matrix.m13;
        position.z = matrix.m23;
        return position;
    }

    public static Vector3 ExtractScale(this Matrix4x4 matrix)
    {
        Vector3 scale;
        scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
        scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
        scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
        return scale;
    }
}

// Source: https://www.loekvandenouweland.com/content/hex-colors-in-unity.html 
public static class StringEx
{
    // Example: "#ff000099".ToColor() red with alpha ~50%
    // Example: "ffffffff".ToColor() white with alpha 100%
    // Example: "00ff00".ToColor() green with alpha 100%
    // Example: "0000ff00".ToColor() blue with alpha 0%
    public static Color ToColor(this string color)
    {
        if (color.StartsWith("#", StringComparison.InvariantCulture))
        {
            color = color.Substring(1); // strip #
        }

        if (color.Length == 6)
        {
            color += "FF"; // add alpha if missing
        }

        var hex = Convert.ToUInt32(color, 16);
        var r = ((hex & 0xff000000) >> 0x18) / 255f;
        var g = ((hex & 0xff0000) >> 0x10) / 255f;
        var b = ((hex & 0xff00) >> 8) / 255f;
        var a = ((hex & 0xff)) / 255f;

        return new Color(r, g, b, a);
    }
}

// Source: https://forum.unity.com/threads/how-to-assign-matrix4x4-to-transform.121966/
public static class TransformExtensions
{
    public static void FromMatrix(this Transform transform, Matrix4x4 matrix)
    {
        transform.localScale = matrix.ExtractScale();
        transform.rotation = matrix.ExtractRotation();
        transform.position = matrix.ExtractPosition();
    }
}

public class MaxLengthConcurrentQueue<T>
{
    private ConcurrentQueue<T> queue = new ConcurrentQueue<T>();
    private int maxLength;
    private readonly object lockObject = new object();

    public MaxLengthConcurrentQueue(int maxLength)
    {
        this.maxLength = maxLength;
    }

    public void Enqueue(T item)
    {
        queue.Enqueue(item);

        lock (lockObject)
        {
            while (queue.Count > maxLength)
            {
                if (queue.TryDequeue(out _))
                {
                    //Debug.Log("Dequeuing an element to maintain max length.");
                }
            }
        }
    }

    public bool TryDequeue(out T result)
    {
        return queue.TryDequeue(out result);
    }

    // Other methods you might want to expose, like Count, Clear, etc.
}