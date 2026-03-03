using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Linq;
using System;

public class LandmarksBasedRegistration : MonoBehaviour
{
    string nameOfLandmarksTXTFile;
    int numberOfLandmarks;
    List<Vector3> preopModelLandmarks;
    List<Vector3> intraopPatientLandmarks;

    enum TransformationType
    {
        RIGIDBODY, AFFINE
    }
    TransformationType Transformation;


    public LandmarksBasedRegistration()
    {
        preopModelLandmarks = new List<Vector3>();
        intraopPatientLandmarks = new List<Vector3>();

        ReadLandmarksTXTFile();
    }

    void ReadLandmarksTXTFile()
    {
        if(!Config.Instance.ConfigFileInitialized) { return; }

        nameOfLandmarksTXTFile = Config.Instance.configFile.unityConfig.landmarksRegistration.fileName;
        string filePath = Path.Combine(Config.Instance.RegistrationFolder, nameOfLandmarksTXTFile);

        var lines = File.ReadLines(filePath);
        foreach (var line in lines.Select((Value, Index) => new { Value, Index }))
        {
            if (Config.Instance.configFile.unityConfig.landmarksRegistration.useSubset)
            {
                if (!Config.Instance.configFile.unityConfig.landmarksRegistration.subsetIndeces.Contains(line.Index))
                    continue;
            }

            string[] components = line.Value.Split(' ', ',');

            if (components.Length != 3)
                continue;
            // TODO: handle when there are white spaces at the end
            float x = float.Parse(components[0]) * Config.Instance.configFile.unityConfig.patientModel.scale;
            float y = float.Parse(components[1]) * Config.Instance.configFile.unityConfig.patientModel.scale;
            float z = float.Parse(components[2]) * Config.Instance.configFile.unityConfig.patientModel.scale;

            preopModelLandmarks.Add(new Vector3(x, y, z));
        }

        numberOfLandmarks = preopModelLandmarks.Count;
        Debug.Log($"Registration Lnamdarks successfully read from file: {numberOfLandmarks} were found");
    }

    public void AddIntraopPatientPoint(Vector3 pointerTipPosition)
    {
        intraopPatientLandmarks.Add(pointerTipPosition);
    }

    public void ClearIntraopLandmarks()
    {
        intraopPatientLandmarks.Clear();
    }

    public void RemoveLastIntraopLandmark()
    {
        intraopPatientLandmarks.RemoveAt(intraopPatientLandmarks.Count - 1);
    }

    public Matrix4x4 PerformPointBasedRegistration()
    {
        return landMarkTransform(preopModelLandmarks, intraopPatientLandmarks);
    }

    public List<Vector3> GetPreopLandmarks()
    {
        if (preopModelLandmarks.Count == 0)
            return new List<Vector3>();
        else
            return preopModelLandmarks;
    }

    public List<Vector3> GetIntraopLandmarks()
    {
        if (intraopPatientLandmarks.Count == 0)
            return new List<Vector3>();
        else
            return intraopPatientLandmarks;
    }

    public int GetNumberOfLandMarks()
    {
        return numberOfLandmarks;
    }

    Vector3 getCentroid(List<Vector3> pointSet)
    {

        Vector3 centroid = new Vector3(0.0f, 0.0f, 0.0f);
        int numberOfPoints = pointSet.Count;
        for (int i = 0; i < numberOfPoints; i++)
        {
            centroid.x += pointSet[i].x;
            centroid.y += pointSet[i].y;
            centroid.z += pointSet[i].z;
        }
        centroid.x /= numberOfPoints;
        centroid.y /= numberOfPoints;
        centroid.z /= numberOfPoints;

        return centroid;
    }
    float[][] MatrixCreate(int rows, int cols)
    {
        float[][] result = new float[rows][];
        for (int i = 0; i < rows; ++i)
            result[i] = new float[cols];
        return result;
    }
    float[][] MatrixDuplicate(float[][] matrix)
    {
        // allocates/creates a duplicate of a matrix.
        float[][] result = MatrixCreate(matrix.Length, matrix[0].Length);
        for (int i = 0; i < matrix.Length; ++i) // copy the values
            for (int j = 0; j < matrix[i].Length; ++j)
                result[i][j] = matrix[i][j];
        return result;
    }
    float[][] MatrixDecompose(float[][] matrix, out int[] perm, out int toggle)
    {
        // Doolittle LUP decomposition with partial pivoting.
        // rerturns: result is L (with 1s on diagonal) and U;
        // perm holds row permutations; toggle is +1 or -1 (even or odd)
        int rows = matrix.Length;
        int cols = matrix[0].Length; // assume square
        if (rows != cols)
            throw new Exception("Attempt to decompose a non-square m");

        int n = rows; // convenience

        float[][] result = MatrixDuplicate(matrix);

        perm = new int[n]; // set up row permutation result
        for (int i = 0; i < n; ++i) { perm[i] = i; }

        toggle = 1; // toggle tracks row swaps.
                    // +1 -greater-than even, -1 -greater-than odd. used by MatrixDeterminant

        for (int j = 0; j < n - 1; ++j) // each column
        {
            float colMax = Mathf.Abs(result[j][j]); // find largest val in col
            int pRow = j;
            //for (int i = j + 1; i less-than n; ++i)
            //{
            //  if (result[i][j] greater-than colMax)
            //  {
            //    colMax = result[i][j];
            //    pRow = i;
            //  }
            //}

            // reader Matt V needed this:
            for (int i = j + 1; i < n; ++i)
            {
                if (Mathf.Abs(result[i][j]) > colMax)
                {
                    colMax = Mathf.Abs(result[i][j]);
                    pRow = i;
                }
            }
            // Not sure if this approach is needed always, or not.

            if (pRow != j) // if largest value not on pivot, swap rows
            {
                float[] rowPtr = result[pRow];
                result[pRow] = result[j];
                result[j] = rowPtr;

                int tmp = perm[pRow]; // and swap perm info
                perm[pRow] = perm[j];
                perm[j] = tmp;

                toggle = -toggle; // adjust the row-swap toggle
            }

            // --------------------------------------------------
            // This part added later (not in original)
            // and replaces the 'return null' below.
            // if there is a 0 on the diagonal, find a good row
            // from i = j+1 down that doesn't have
            // a 0 in column j, and swap that good row with row j
            // --------------------------------------------------

            if (result[j][j] == 0.0)
            {
                // find a good row to swap
                int goodRow = -1;
                for (int row = j + 1; row < n; ++row)
                {
                    if (result[row][j] != 0.0)
                        goodRow = row;
                }

                if (goodRow == -1)
                    throw new Exception("Cannot use Doolittle's method");

                // swap rows so 0.0 no longer on diagonal
                float[] rowPtr = result[goodRow];
                result[goodRow] = result[j];
                result[j] = rowPtr;

                int tmp = perm[goodRow]; // and swap perm info
                perm[goodRow] = perm[j];
                perm[j] = tmp;

                toggle = -toggle; // adjust the row-swap toggle
            }
            // --------------------------------------------------
            // if diagonal after swap is zero . .
            //if (Mathf.Abs(result[j][j]) less-than 1.0E-20) 
            //  return null; // consider a throw

            for (int i = j + 1; i < n; ++i)
            {
                result[i][j] /= result[j][j];
                for (int k = j + 1; k < n; ++k)
                {
                    result[i][k] -= result[i][j] * result[j][k];
                }
            }


        } // main j column loop

        return result;
    }
    float[] HelperSolve(float[][] luMatrix, float[] b)
    {
        // before calling this helper, permute b using the perm array
        // from MatrixDecompose that generated luMatrix
        int n = luMatrix.Length;
        float[] x = new float[n];
        b.CopyTo(x, 0);

        for (int i = 1; i < n; ++i)
        {
            float sum = x[i];
            for (int j = 0; j < i; ++j)
                sum -= luMatrix[i][j] * x[j];
            x[i] = sum;
        }

        x[n - 1] /= luMatrix[n - 1][n - 1];
        for (int i = n - 2; i >= 0; --i)
        {
            float sum = x[i];
            for (int j = i + 1; j < n; ++j)
                sum -= luMatrix[i][j] * x[j];
            x[i] = sum / luMatrix[i][i];
        }

        return x;
    }
    float[][] MatrixInverse(float[][] matrix)
    {
        int n = matrix.Length;
        float[][] result = MatrixDuplicate(matrix);

        int[] perm;
        int toggle;
        float[][] lum = MatrixDecompose(matrix, out perm,
          out toggle);
        if (lum == null)
            throw new Exception("Unable to compute inverse");

        float[] b = new float[n];
        for (int i = 0; i < n; ++i)
        {
            for (int j = 0; j < n; ++j)
            {
                if (i == perm[j])
                    b[j] = 1.0F;
                else
                    b[j] = 0.0F;
            }

            float[] x = HelperSolve(lum, b);

            for (int j = 0; j < n; ++j)
                result[j][i] = x[j];
        }
        return result;
    }
    float[][] MatrixProduct(float[][] matrixA, float[][] matrixB)
    {
        int aRows = matrixA.Length; int aCols = matrixA[0].Length;
        int bRows = matrixB.Length; int bCols = matrixB[0].Length;
        if (aCols != bRows)
            throw new Exception("Non-conformable matrices in MatrixProduct");

        float[][] result = MatrixCreate(aRows, bCols);

        for (int i = 0; i < aRows; ++i) // each row of A
            for (int j = 0; j < bCols; ++j) // each col of B
                for (int k = 0; k < aCols; ++k) // could use k less-than bRows
                    result[i][j] += matrixA[i][k] * matrixB[k][j];

        return result;
    }
    bool JacobiN(float[][] a, int n, float[] w, float[][] v)
    {
        int i, j, k, iq, ip, numPos;
        float tresh, theta, tau, t, sm, s, h, g, c, tmp;
        float[] bspace = new float[4]; float[] zspace = new float[4];
        float[] b = bspace;
        float[] z = zspace;

        // only allocate memory if the matrix is large
        if (n > 4)
        {
            b = new float[n];
            z = new float[n];
        }

        // initialize
        for (ip = 0; ip < n; ip++)
        {
            for (iq = 0; iq < n; iq++)
            {
                v[ip][iq] = 0.0F;
            }
            v[ip][ip] = 1.0F;
        }
        for (ip = 0; ip < n; ip++)
        {
            b[ip] = w[ip] = a[ip][ip];
            z[ip] = 0.0F;
        }

        // begin rotation sequence
        for (i = 0; i < 20; ++i)
        {
            sm = 0.0F;
            for (ip = 0; ip < n - 1; ip++)
            {
                for (iq = ip + 1; iq < n; iq++)
                {
                    sm += Mathf.Abs(a[ip][iq]);
                }
            }
            if (sm == 0.0F)
            {
                break;
            }

            if (i < 3) // first 3 sweeps
            {
                tresh = 0.2F * sm / (n * n);
            }
            else
            {
                tresh = 0.0F;
            }

            for (ip = 0; ip < n - 1; ip++)
            {
                for (iq = ip + 1; iq < n; iq++)
                {
                    g = 100.0F * Mathf.Abs(a[ip][iq]);

                    // after 4 sweeps
                    if (i > 3 && (Mathf.Abs(w[ip]) + g) == Mathf.Abs(w[ip])
                    && (Mathf.Abs(w[iq]) + g) == Mathf.Abs(w[iq]))
                    {
                        a[ip][iq] = 0.0F;
                    }
                    else if (Mathf.Abs(a[ip][iq]) > tresh)
                    {
                        h = w[iq] - w[ip];
                        if ((Mathf.Abs(h) + g) == Mathf.Abs(h))
                        {
                            t = (a[ip][iq]) / h;
                        }
                        else
                        {
                            theta = 0.5F * h / (a[ip][iq]);
                            t = 1.0F / (Mathf.Abs(theta) + Mathf.Sqrt(1.0F + theta * theta));
                            if (theta < 0.0F)
                            {
                                t = -t;
                            }
                        }
                        c = 1.0F / Mathf.Sqrt(1 + t * t);
                        s = t * c;
                        tau = s / (1.0F + c);
                        h = t * a[ip][iq];
                        z[ip] -= h;
                        z[iq] += h;
                        w[ip] -= h;
                        w[iq] += h;
                        a[ip][iq] = 0.0F;

                        // ip already shifted left by 1 unit
                        for (j = 0; j <= ip - 1; ++j)
                        {
                            g = a[j][ip];
                            h = a[j][iq];
                            a[j][ip] = g - s * (h + g * tau);
                            a[j][iq] = h + s * (g - h * tau);
                        }
                        // ip and iq already shifted left by 1 unit
                        for (j = ip + 1; j <= iq - 1; ++j)
                        {
                            g = a[ip][j];
                            h = a[j][iq];
                            a[ip][j] = g - s * (h + g * tau);
                            a[j][iq] = h + s * (g - h * tau);
                        }
                        // iq already shifted left by 1 unit
                        for (j = iq + 1; j < n; ++j)
                        {
                            g = a[ip][j];
                            h = a[iq][j];
                            a[ip][j] = g - s * (h + g * tau);
                            a[iq][j] = h + s * (g - h * tau);
                        }
                        for (j = 0; j < n; ++j)
                        {
                            g = v[j][ip];
                            h = v[j][iq];
                            v[j][ip] = g - s * (h + g * tau);
                            v[j][iq] = h + s * (g - h * tau);
                        }
                    }
                }
            }

            for (ip = 0; ip < n; ip++)
            {
                b[ip] += z[ip];
                w[ip] = b[ip];
                z[ip] = 0.0F;
            }
        }



        // sort eigenfunctions                 these changes do not affect accuracy
        for (j = 0; j < n - 1; ++j)                  // boundary incorrect
        {
            k = j;
            tmp = w[k];
            for (i = j + 1; i < n; ++i)                // boundary incorrect, shifted already
            {
                if (w[i] >= tmp)                   // why exchange if same?
                {
                    k = i;
                    tmp = w[k];
                }
            }
            if (k != j)
            {
                w[k] = w[j];
                w[j] = tmp;
                for (i = 0; i < n; ++i)
                {
                    tmp = v[i][j];
                    v[i][j] = v[i][k];
                    v[i][k] = tmp;
                }
            }
        }
        // insure eigenvector consistency (i.e., Jacobi can compute vectors that
        // are negative of one another (.707,.707,0) and (-.707,-.707,0). This can
        // reek havoc in hyperstreamline/other stuff. We will select the most
        // positive eigenvector.
        int ceil_half_n = (n >> 1) + (n & 1);
        for (j = 0; j < n; ++j)
        {
            for (numPos = 0, i = 0; i < n; ++i)
            {
                if (v[i][j] >= 0.0F)
                {
                    numPos++;
                }
            }
            if (numPos < ceil_half_n)
            {
                for (i = 0; i < n; ++i)
                {
                    v[i][j] *= -1.0F;
                }
            }
        }

        /*if (n > 4)
        {
            delete[] b;
            delete[] z;
        }*/
        return true;
    }
    void Perpendiculars(float[] v1, float[] v2, float[] v3, float theta)
    {
        float v1sq = v1[0] * v1[0];
        float v2sq = v1[1] * v1[1];
        float v3sq = v1[2] * v1[2];
        float r = Mathf.Sqrt(v1sq + v2sq + v3sq);

        // transpose the vector to avoid divide-by-zero error
        int dv1, dv2, dv3;
        if (v1sq > v2sq && v1sq > v3sq)
        {
            dv1 = 0; dv2 = 1; dv3 = 2;
        }
        else if (v2sq > v3sq)
        {
            dv1 = 1; dv2 = 2; dv3 = 0;
        }
        else
        {
            dv1 = 2; dv2 = 0; dv3 = 1;
        }

        float a = v1[dv1] / r;
        float b = v1[dv2] / r;
        float c = v1[dv3] / r;

        float tmp = Mathf.Sqrt(a * a + c * c);

        if (theta != 0.0F)
        {
            float sintheta = Mathf.Sin(theta);
            float costheta = Mathf.Cos(theta);

            if (v2 != null)
            {
                v2[dv1] = (c * costheta - a * b * sintheta) / tmp;
                v2[dv2] = sintheta * tmp;
                v2[dv3] = (-a * costheta - b * c * sintheta) / tmp;
            }

            if (v3 != null)
            {
                v3[dv1] = (-c * sintheta - a * b * costheta) / tmp;
                v3[dv2] = costheta * tmp;
                v3[dv3] = (a * sintheta - b * c * costheta) / tmp;
            }
        }
        else
        {
            if (v2 != null)
            {
                v2[dv1] = c / tmp;
                v2[dv2] = 0;
                v2[dv3] = -a / tmp;
            }

            if (v3 != null)
            {
                v3[dv1] = -a * b / tmp;
                v3[dv2] = tmp;
                v3[dv3] = -b * c / tmp;
            }
        }
    }
    Matrix4x4 landMarkTransform(List<Vector3> sourcePoints, List<Vector3> targetPoints)
    {
        Matrix4x4 calibration = new Matrix4x4();
        int numberOfPoints = sourcePoints.Count;

        Vector3 sourceCentroid;
        Vector3 targetCentroid;
        sourceCentroid = getCentroid(sourcePoints);
        targetCentroid = getCentroid(targetPoints);
        if (numberOfPoints == 1)
        {
            calibration = Matrix4x4.identity;
            calibration.m03 = targetCentroid[0] - sourceCentroid[0];
            calibration.m13 = targetCentroid[1] - sourceCentroid[1];
            calibration.m23 = targetCentroid[2] - sourceCentroid[2];
            return calibration;
        }


        // -- build the 3x3 matrix M --

        float[][] M = new float[3][];
        M[0] = new float[3];
        M[1] = new float[3];
        M[2] = new float[3];

        float[][] AAT = new float[3][];
        AAT[0] = new float[3];
        AAT[1] = new float[3];
        AAT[2] = new float[3];

        for (int i = 0; i < 3; i++)
        {
            AAT[i][0] = M[i][0] = 0.0F; // fill M with zeros
            AAT[i][1] = M[i][1] = 0.0F;
            AAT[i][2] = M[i][2] = 0.0F;
        }
        float[] pt = new float[3];
        float[] a = new float[3];
        float[] b = new float[3];
        float sa = 0.0F, sb = 0.0F;
        for (int i = 0; i < numberOfPoints; i++)
        {
            // get the origin-centred point (a) in the source set
            a[0] = sourcePoints[i].x; a[1] = sourcePoints[i].y; a[2] = sourcePoints[i].z;
            a[0] -= sourceCentroid[0];
            a[1] -= sourceCentroid[1];
            a[2] -= sourceCentroid[2];
            // get the origin-centred point (b) in the target set
            b[0] = targetPoints[i].x; b[1] = targetPoints[i].y; b[2] = targetPoints[i].z;
            b[0] -= targetCentroid[0];
            b[1] -= targetCentroid[1];
            b[2] -= targetCentroid[2];
            // accumulate the products a*T(b) into the matrix M
            for (int j = 0; j < 3; j++)
            {
                M[j][0] += a[j] * b[0];
                M[j][1] += a[j] * b[1];
                M[j][2] += a[j] * b[2];

                // for the affine transform, compute ((a.a^t)^-1 . a.b^t)^t.
                // a.b^t is already in M.  here we put a.a^t in AAT.
                if (Transformation == TransformationType.AFFINE)
                {
                    AAT[j][0] += a[j] * a[0];
                    AAT[j][1] += a[j] * a[1];
                    AAT[j][2] += a[j] * a[2];
                }
            }
            // accumulate scale factors (if desired)
            sa += a[0] * a[0] + a[1] * a[1] + a[2] * a[2];
            sb += b[0] * b[0] + b[1] * b[1] + b[2] * b[2];
        }

        // if source or destination is degenerate then only report
        // translation
        if (sa == 0.0F || sb == 0.0F)
        {
            calibration = Matrix4x4.identity;
            calibration[0, 3] = targetCentroid[0] - sourceCentroid[0];
            calibration[1, 3] = targetCentroid[1] - sourceCentroid[1];
            calibration[2, 3] = targetCentroid[2] - sourceCentroid[2];
            UnityEngine.Debug.Log("2 :" + calibration);
            return calibration;
        }

        if (Transformation == TransformationType.AFFINE)
        {
            // AAT = (a.a^t)^-1
            AAT = MatrixInverse(AAT);

            // M = (a.a^t)^-1 . a.b^t
            M = MatrixProduct(AAT, M);

            // this->Matrix = M^t
            for (int i = 0; i < 3; ++i)
            {
                for (int j = 0; j < 3; ++j)
                {
                    calibration[i, j] = M[j][i];
                }
            }
        }
        else
        {
            // compute required scaling factor (if desired)
            float scale = Mathf.Sqrt(sb / sa);

            // -- build the 4x4 matrix N --

            float[][] Ndata = new float[4][];
            Ndata[0] = new float[4];
            Ndata[1] = new float[4];
            Ndata[2] = new float[4];
            Ndata[3] = new float[4];

            float[][] N = new float[4][];
            N[0] = new float[4];
            N[1] = new float[4];
            N[2] = new float[4];
            N[3] = new float[4];
            for (int i = 0; i < 4; i++)
            {
                N[i] = Ndata[i];
                N[i][0] = 0.0F; // fill N with zeros
                N[i][1] = 0.0F;
                N[i][2] = 0.0F;
                N[i][3] = 0.0F;
            }
            // on-diagonal elements
            N[0][0] = M[0][0] + M[1][1] + M[2][2];
            N[1][1] = M[0][0] - M[1][1] - M[2][2];
            N[2][2] = -M[0][0] + M[1][1] - M[2][2];
            N[3][3] = -M[0][0] - M[1][1] + M[2][2];
            // off-diagonal elements
            N[0][1] = N[1][0] = M[1][2] - M[2][1];
            N[0][2] = N[2][0] = M[2][0] - M[0][2];
            N[0][3] = N[3][0] = M[0][1] - M[1][0];

            N[1][2] = N[2][1] = M[0][1] + M[1][0];
            N[1][3] = N[3][1] = M[2][0] + M[0][2];
            N[2][3] = N[3][2] = M[1][2] + M[2][1];


            // -- eigen-decompose N (is symmetric) --

            float[][] eigenvectorData = new float[4][];
            eigenvectorData[0] = new float[4];
            eigenvectorData[1] = new float[4];
            eigenvectorData[2] = new float[4];
            eigenvectorData[3] = new float[4];

            float[][] eigenvectors = new float[4][];
            eigenvectors[0] = new float[4];
            eigenvectors[1] = new float[4];
            eigenvectors[2] = new float[4];
            eigenvectors[3] = new float[4];

            float[] eigenvalues = new float[4];

            eigenvectors[0] = eigenvectorData[0];
            eigenvectors[1] = eigenvectorData[1];
            eigenvectors[2] = eigenvectorData[2];
            eigenvectors[3] = eigenvectorData[3];

            JacobiN(N, 4, eigenvalues, eigenvectors);

            // the eigenvector with the largest eigenvalue is the quaternion we want
            // (they are sorted in decreasing order for us by JacobiN)
            float w, x, y, z;
            // first: if points are collinear, choose the quaternion that
            // results in the smallest rotation.
            if (eigenvalues[0] == eigenvalues[1] || numberOfPoints == 2)
            {
                float[] s0 = new float[3];
                float[] t0 = new float[3];
                float[] s1 = new float[3];
                float[] t1 = new float[3];

                s0[0] = sourcePoints[0].x; s0[1] = sourcePoints[0].y; s0[2] = sourcePoints[0].z;
                t0[0] = targetPoints[0].x; t0[1] = targetPoints[0].y; t0[2] = targetPoints[0].z;
                s1[0] = sourcePoints[1].x; s1[1] = sourcePoints[1].y; s1[2] = sourcePoints[1].z;
                t1[0] = targetPoints[1].x; t1[1] = targetPoints[1].y; t1[2] = targetPoints[1].z;



                float[] ds = new float[3];
                float[] dt = new float[3];
                float rs = 0, rt = 0;
                for (int i = 0; i < 3; i++)
                {
                    ds[i] = s1[i] - s0[i];      // vector between points
                    rs += ds[i] * ds[i];
                    dt[i] = t1[i] - t0[i];
                    rt += dt[i] * dt[i];
                }

                // normalize the two vectors
                rs = Mathf.Sqrt(rs);
                ds[0] /= rs; ds[1] /= rs; ds[2] /= rs;
                rt = Mathf.Sqrt(rt);
                dt[0] /= rt; dt[1] /= rt; dt[2] /= rt;

                // take dot & cross product
                w = ds[0] * dt[0] + ds[1] * dt[1] + ds[2] * dt[2];
                x = ds[1] * dt[2] - ds[2] * dt[1];
                y = ds[2] * dt[0] - ds[0] * dt[2];
                z = ds[0] * dt[1] - ds[1] * dt[0];

                float r = Mathf.Sqrt(x * x + y * y + z * z);
                float theta = Mathf.Atan2(r, w);

                // construct quaternion
                w = Mathf.Cos(theta / 2);
                if (r != 0.0F)
                {
                    UnityEngine.Debug.Log("construct quaternion");
                    r = Mathf.Sin(theta / 2) / r;
                    x = x * r;
                    y = y * r;
                    z = z * r;
                }
                else // rotation by 180 degrees: special case
                {
                    UnityEngine.Debug.Log("rotation by 180 degrees: special case");
                    // rotate around a vector perpendicular to ds
                    Perpendiculars(ds, dt, null, 0);
                    r = Mathf.Sin(theta / 2);
                    x = dt[0] * r;
                    y = dt[1] * r;
                    z = dt[2] * r;
                }
            }
            else // points are not collinear
            {
                UnityEngine.Debug.Log("points are not collinear");
                w = eigenvectors[0][0];
                x = eigenvectors[1][0];
                y = eigenvectors[2][0];
                z = eigenvectors[3][0];
            }

            // convert quaternion to a rotation matrix
            UnityEngine.Debug.Log("x,y,z,w = " + x + ", " + y + ", " + z + ", " + w);
            float ww = w * w;
            float wx = w * x;
            float wy = w * y;
            float wz = w * z;

            float xx = x * x;
            float yy = y * y;
            float zz = z * z;

            float xy = x * y;
            float xz = x * z;
            float yz = y * z;



            calibration[0, 0] = ww + xx - yy - zz;
            calibration[1, 0] = 2.0F * (wz + xy);
            calibration[2, 0] = 2.0F * (-wy + xz);

            calibration[0, 1] = 2.0F * (-wz + xy);
            calibration[1, 1] = ww - xx + yy - zz;
            calibration[2, 1] = 2.0F * (wx + yz);

            calibration[0, 2] = 2.0F * (wy + xz);
            calibration[1, 2] = 2.0F * (-wx + yz);
            calibration[2, 2] = ww - xx - yy + zz;

            if (Transformation != TransformationType.RIGIDBODY)
            { // add in the scale factor (if desired)
                for (int i = 0; i < 3; i++)
                {
                    calibration[i, 0] *= scale;
                    calibration[i, 1] *= scale;
                    calibration[i, 2] *= scale;
                }
            }
        }

        // the translation is given by the difference in the transformed source
        // centroid and the target centroid
        float sx, sy, sz;

        sx = calibration[0, 0] * sourceCentroid[0] +
             calibration[0, 1] * sourceCentroid[1] +
             calibration[0, 2] * sourceCentroid[2];

        sy = calibration[1, 0] * sourceCentroid[0] +
             calibration[1, 1] * sourceCentroid[1] +
             calibration[1, 2] * sourceCentroid[2];

        sz = calibration[2, 0] * sourceCentroid[0] +
             calibration[2, 1] * sourceCentroid[1] +
             calibration[2, 2] * sourceCentroid[2];

        calibration[0, 3] = targetCentroid[0] - sx;
        calibration[1, 3] = targetCentroid[1] - sy;
        calibration[2, 3] = targetCentroid[2] - sz;

        // fill the bottom row of the 4x4 matrix
        calibration[3, 0] = 0.0F;
        calibration[3, 1] = 0.0F;
        calibration[3, 2] = 0.0F;
        calibration[3, 3] = 1.0F;

        return calibration;
    }

}
