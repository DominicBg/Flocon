using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;

public class Draw : MonoBehaviour
{
    List<SnowFlakeLine> lines = new();
    SnowFlakeLine currentLine;

    public float delay = 0.2f;
    public float minSnapDist = 1f;
    float currentDelay = 0;
    public float distanceFromCamera = 10;
    public LineRenderer lineRendererPrefab;
    public float lineWidth = 0.3f;
    ObjectPool<LineRenderer> objectPool;
    public Color currentColor;

    float zoomFactor;
    public float zoomSpeed = 0.5f;
    public float minZoom = 0;
    public float maxZoom = 10;

    public float panDuration = 10;
    public AnimationCurve panCurve;

    bool showDebugLine = false;

    List<LineRenderer> toggleLines = new();
    float3 previousRecordedPoint;

    Camera mainCam;
    public ParticleSystem flakesPS;

    List<Mesh> meshList = new List<Mesh>();

    void Start()
    {
        objectPool = CreateObjectPool();
        zoomFactor = (minZoom + maxZoom) / 2;
        mainCam = Camera.main;
    }

    ObjectPool<LineRenderer> CreateObjectPool()
    {
        const int capacity = 500;
        var objectPool = new ObjectPool<LineRenderer>(() => Instantiate(lineRendererPrefab, transform), actionOnRelease: (lr) => lr.positionCount = 0, defaultCapacity: capacity);

        LineRenderer[] prewarmArray = new LineRenderer[capacity];
        for (int i = 0; i < capacity; i++)
        {
            prewarmArray[i] = objectPool.Get();
        }

        for (int i = 0; i < capacity; i++)
        {
            objectPool.Release(prewarmArray[i]);
        }

        return objectPool;
    }

    private void Update()
    {
        zoomFactor = math.clamp(zoomFactor + Input.mouseScrollDelta.y * zoomSpeed, minZoom, maxZoom);
        mainCam.transform.position = new float3(((float3)mainCam.transform.position).xy, zoomFactor);

        if (Input.GetMouseButtonDown(0))
        {
            currentLine = new SnowFlakeLine(objectPool, currentColor, lineWidth);
            AddPointToCurrentLine(ref currentLine, isFirstPoint: true);
            currentDelay = delay;
        }
        else if (Input.GetMouseButton(0))
        {
            //record
            if (currentDelay <= 0)
            {
                AddPointToCurrentLine(ref currentLine);
                currentDelay = delay;
            }
            else
            {
                currentDelay -= Time.deltaTime;
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            lines.Add(currentLine);
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            Undo();
        }
   
        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleDebugLines();
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            GenerateMesh();
        }

        if(Input.GetKeyDown(KeyCode.N))
        {
            StartCoroutine(ZoomCoroutine());
        }
    }

    private void Undo()
    {
        if (lines.Count > 0)
        {
            int lastIndex = lines.Count - 1;
            lines[lastIndex].Dispose();
            lines.RemoveAt(lastIndex);
        }
    }
    
    private void GenerateMesh()
    {
        var mesh = GenerateMesh(in lines);
        //meshFilter.mesh = mesh;
        var renderer = flakesPS.GetComponent<ParticleSystemRenderer>();
        //renderer.mesh = meshFilter.mesh;

        meshList.Add(mesh);

        //particle system limit
        if(meshList.Count > 4)
        {
            meshList.RemoveAt(0);
        }
        renderer.SetMeshes(meshList.ToArray());
        ClearCurrentSnowFlake();
    }

    private void ClearCurrentSnowFlake()
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            lines[i].Dispose();
        }
        lines.Clear();
    }

    void AddPointToCurrentLine(ref SnowFlakeLine currentLine, bool isFirstPoint = false)
    {
        float3 mousePos = new float3(Input.mousePosition.x, Input.mousePosition.y, math.abs(mainCam.transform.position.z));

        //put bool for mode?
        if (Input.GetKey(KeyCode.LeftShift) && !isFirstPoint)
        {
            if (math.any(math.abs(mousePos - previousRecordedPoint) > minSnapDist))
            {
                float3 delta = mousePos - previousRecordedPoint;
                float angleRad = math.atan2(delta.y, delta.x);
                float length = math.length(delta.xy);
                angleRad = SnowFlakeUtils.QuantizeRound(angleRad, math.radians(45));

                float2 recomposePoint = new float2(math.cos(angleRad), math.sin(angleRad)) * length;
                mousePos = previousRecordedPoint + new float3(recomposePoint.xy, 0);
            }
            else
            {
                mousePos = previousRecordedPoint;
            }
        }

        if (!SnowFlakeUtils.IsApprox(mousePos, previousRecordedPoint, 1))
        {
            float3 point = Camera.main.ScreenToWorldPoint(mousePos);
            point.z = 0; //force it to be a clean 0
            currentLine.AddPoint(Camera.main.ScreenToWorldPoint(mousePos));
        }
        previousRecordedPoint = mousePos;
    }

    void ToggleDebugLines()
    {
        showDebugLine = !showDebugLine;
        const int debugLineCount = 12;
        if (showDebugLine)
        {
            for (int i = 0; i < debugLineCount; i++)
            {
                var debugLine = objectPool.Get();

                debugLine.startWidth = 0.1f;
                debugLine.endWidth = 0.1f;
                debugLine.positionCount = 2;
                debugLine.SetPosition(0, new float3(0, 0, 0));
                debugLine.SetPosition(1, SnowFlakeUtils.TransformPointHalfHex(new float3(0, 100, 0), i));

                toggleLines.Add(debugLine);
            }
        }
        else
        {
            for (int i = 0; i < toggleLines.Count; i++)
            {
                objectPool.Release(toggleLines[i]);
            }
            toggleLines.Clear();
        }
    }


    Mesh GenerateMesh(in List<SnowFlakeLine> lines)
    {
        Mesh mesh = new Mesh();

        using NativeList<float3> vertices = new NativeList<float3>(Allocator.TempJob);
        using NativeList<int> triangles = new NativeList<int>(Allocator.TempJob);
        using NativeList<float2> uvs = new NativeList<float2>(Allocator.TempJob);

        foreach (SnowFlakeLine line in lines)
        {
            for (int lineId = 0; lineId < line.LineCount; lineId++)
            {
                int count = line.GetPointCount(lineId);
                float3 prevPoint = line.GetPoint(lineId, 0);

                for (int i = 1; i < count; i++)
                {
                    float3 point = line.GetPoint(lineId, i);

                    float3 localUp = point - prevPoint;
                    float3 localRight = math.cross(math.normalize(localUp), math.forward()) * (lineWidth * 0.5f);

                    float3 bottomLeft = prevPoint + localRight;
                    float3 bottomRight = prevPoint - localRight;
                    float3 topLeft = point + localRight;
                    float3 topRight = point - localRight;

                    int triIndex = vertices.Length;

                    vertices.Add(bottomLeft);
                    vertices.Add(bottomRight);
                    vertices.Add(topLeft);
                    vertices.Add(topRight);

                    uvs.Add(new float2(0, 0));
                    uvs.Add(new float2(1, 0));
                    uvs.Add(new float2(0, 1));
                    uvs.Add(new float2(1, 1));

                    //todo debug lol
                    triangles.Add(triIndex + 0);
                    triangles.Add(triIndex + 2);
                    triangles.Add(triIndex + 1);

                    triangles.Add(triIndex + 2);
                    triangles.Add(triIndex + 3);
                    triangles.Add(triIndex + 1);

                    prevPoint = point;
                }
            }
        }

        mesh.SetVertices<float3>(vertices);
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetUVs<float2>(0, uvs);
        mesh.RecalculateNormals();

        return mesh;
    }

    IEnumerator ZoomCoroutine()
    {
        float t = 0;
        float panSpeed = 1f / panDuration;
        while (t < 1)
        {
            t += Time.deltaTime * panSpeed;
            zoomFactor = math.lerp(maxZoom, minZoom, panCurve.Evaluate(t));
            yield return null;
        }
    }
}
