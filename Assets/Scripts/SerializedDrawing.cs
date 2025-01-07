using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "SerializedDrawing", menuName = "ScriptableObjects/SerializedDrawing", order = 1)]
public class SerializedDrawing : ScriptableObject
{
    public float3[] allPoints;
    public int[] lineCounts;

    public void Serialize(List<SnowFlakeLine> lines)
    {
        List<float3> pointList = new List<float3>();
        lineCounts = new int[lines.Count];

        for (int i = 0; i < lines.Count; i++)
        {
            var mainLine = lines[i].GetMainLine();

            lineCounts[i] = pointList.Count;
            for (int j = 0; j < mainLine.Length; j++)
            {
                pointList.Add(mainLine[j]);
            }
        }
        allPoints = pointList.ToArray();

#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    public List<Vector3>[] Deserialize()
    {
        List<Vector3>[] allLines = new List<Vector3>[lineCounts.Length];
        for (int i = 0; i < lineCounts.Length; i++)
        {
            int start = lineCounts[i];
            int end = i + 1 < lineCounts.Length ? lineCounts[i + 1] : allPoints.Length;

            allLines[i] = new List<Vector3>();
            for (int j = start; j < end; j++)
            {
                allLines[i].Add(allPoints[j]);
            }
        }
        return allLines;
    }
}
