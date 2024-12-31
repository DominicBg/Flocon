using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

public struct SnowFlakeLine : IDisposable
{
    NativeList<Vector3>[] points;
    ObjectPool<LineRenderer> objectPoolRef;

    LineRenderer[] lineRenderers;

    public SnowFlakeLine(ObjectPool<LineRenderer> objectPoolRef, Color color, float width)
    {
        this.objectPoolRef = objectPoolRef;

        const int length = SnowFlakeUtils.mirrorCount * SnowFlakeUtils.hexCount;

        points = new NativeList<Vector3>[length];

        lineRenderers = new LineRenderer[length];
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            points[i] = new NativeList<Vector3>(Allocator.Persistent);
            lineRenderers[i] = objectPoolRef.Get();
            lineRenderers[i].startColor = color;
            lineRenderers[i].endColor = color;
            lineRenderers[i].startWidth = width;
            lineRenderers[i].endWidth = width;
        }
    }

    public void AddPoint(float3 point)
    {
        for (int i = 0; i < points.Length; i++)
        {
            points[i].Add(SnowFlakeUtils.TransformPointMirror(point, i));
            UpdateLineRenderers();
        }
    }

    public int LineCount => points.Length;
    public int GetPointCount(int lineIndex) => points[lineIndex].Length;

    public float3 GetPoint(int lineIndex, int pointIndex)
    {
        return points[lineIndex][pointIndex];
    }

    public void UpdateLineRenderers()
    {
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            lineRenderers[i].positionCount = points[i].Length;
            lineRenderers[i].SetPositions(points[i].AsArray());
        }
    }

    public void Dispose()
    {
        for (int i = 0; i < lineRenderers.Length; i++)
        {
            objectPoolRef.Release(lineRenderers[i]);
            points[i].Dispose();
        }
    }
}