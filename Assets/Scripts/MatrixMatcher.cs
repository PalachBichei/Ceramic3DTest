using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class MatrixMatcher : MonoBehaviour
{
    public string modelFileName = "model.json";
    public string spaceFileName = "space.json";
    public string outputFileName = "output.json";

    private List<Matrix4x4> modelMatrices;
    private List<Matrix4x4> spaceMatrices;
    private List<Vector3> matchingOffsets;
    private List<Vector3> nonMatchingPoints;

    void Start()
    {
        StartCoroutine(LoadMatrices());
    }

    IEnumerator LoadMatrices()
    {
        yield return StartCoroutine(LoadJsonFromFile(modelFileName, result => modelMatrices = ParseMatrices(result)));
        yield return StartCoroutine(LoadJsonFromFile(spaceFileName, result => spaceMatrices = ParseMatrices(result)));

        if (modelMatrices == null || spaceMatrices == null)
        {
            Debug.LogError("Failed to load matrices, stopping execution.");
            yield break;
        }

        matchingOffsets = new List<Vector3>();
        nonMatchingPoints = new List<Vector3>();
        FindMatchingOffsets();
        VisualizeMatches();
        ExportOffsetsToJson(outputFileName);
    }

    IEnumerator LoadJsonFromFile(string fileName, System.Action<string> callback)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        
        #if UNITY_IOS && !UNITY_EDITOR
            path = "file://" + path;
        #endif
        
        Debug.Log("Loading file from: " + path);

        #if UNITY_EDITOR
            if (File.Exists(path))
            {
                callback(File.ReadAllText(path));
                yield break;
            }
            else
            {
                Debug.LogError($"File not found: {path}");
                yield break;
            }
        #endif

        UnityWebRequest request = UnityWebRequest.Get(path);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            callback(request.downloadHandler.text);
        }
        else
        {
            Debug.LogError($"File not found: {path} | Error: {request.error}");
        }
    }

    void FindMatchingOffsets()
    {
        HashSet<Vector3> matchedOffsets = new HashSet<Vector3>();
        
        foreach (var modelMatrix in modelMatrices)
        {
            Vector3 modelPosition = modelMatrix.GetColumn(3);
            bool hasMatch = false;

            foreach (var spaceMatrix in spaceMatrices)
            {
                Vector3 spacePosition = spaceMatrix.GetColumn(3);
                Vector3 offset = spacePosition - modelPosition;
                
                Matrix4x4 transformedModel = Matrix4x4.TRS(offset, Quaternion.identity, Vector3.one) * modelMatrix;
                
                if (MatrixMatchesSpace(transformedModel, spaceMatrix))
                {
                    if (!matchedOffsets.Contains(offset))
                    {
                        matchingOffsets.Add(offset);
                        matchedOffsets.Add(offset);
                        Debug.Log($"Match found! Offset: {offset}");
                    }
                    hasMatch = true;
                    break;
                }
            }

            if (!hasMatch)
            {
                nonMatchingPoints.Add(modelPosition);
            }
        }
    }

    bool MatrixMatchesSpace(Matrix4x4 transformedModel, Matrix4x4 spaceMatrix)
    {
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                if (Mathf.Abs(transformedModel[i, j] - spaceMatrix[i, j]) > 0.01f)
                {
                    return false;
                }
            }
        }
        return true;
    }

    void VisualizeMatches()
    {
        foreach (var offset in matchingOffsets)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = offset;
            cube.transform.localScale = Vector3.one * 0.2f;
            cube.GetComponent<Renderer>().material.color = Color.blue;
        }

        foreach (var point in nonMatchingPoints)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = point;
            cube.transform.localScale = Vector3.one * 0.2f;
            cube.GetComponent<Renderer>().material.color = Color.white;
        }
    }

    List<Matrix4x4> ParseMatrices(string json)
    {
        Debug.Log("Raw JSON data: " + json);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError("JSON is empty or null!");
            return new List<Matrix4x4>();
        }

        MatrixData[] matricesArray = JsonUtility.FromJson<MatrixDataArray>("{\"matrices\":" + json + "}").matrices;

        if (matricesArray == null || matricesArray.Length == 0)
        {
            Debug.LogError("Failed to parse JSON into MatrixData[]!");
            return new List<Matrix4x4>();
        }

        List<Matrix4x4> matrices = new List<Matrix4x4>();

        foreach (var data in matricesArray)
        {
            Matrix4x4 matrix = new Matrix4x4();
            matrix.m00 = data.m00; matrix.m01 = data.m01; matrix.m02 = data.m02; matrix.m03 = data.m03;
            matrix.m10 = data.m10; matrix.m11 = data.m11; matrix.m12 = data.m12; matrix.m13 = data.m13;
            matrix.m20 = data.m20; matrix.m21 = data.m21; matrix.m22 = data.m22; matrix.m23 = data.m23;
            matrix.m30 = data.m30; matrix.m31 = data.m31; matrix.m32 = data.m32; matrix.m33 = data.m33;

            matrices.Add(matrix);
        }

        return matrices;
    }

    void ExportOffsetsToJson(string fileName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, fileName);
        File.WriteAllText(path, JsonUtility.ToJson(new OffsetList { offsets = matchingOffsets }));
        Debug.Log($"Offsets exported to {path}");
    }
}

[System.Serializable]
public class MatrixDataArray
{
    public MatrixData[] matrices;
}

[System.Serializable]
public class MatrixData
{
    public float m00, m01, m02, m03;
    public float m10, m11, m12, m13;
    public float m20, m21, m22, m23;
    public float m30, m31, m32, m33;
}

[System.Serializable]
public class OffsetList
{
    public List<Vector3> offsets;
}
