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

    public SnowFlakeLine(ObjectPool<LineRenderer> objectPoolRef, Color color)
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