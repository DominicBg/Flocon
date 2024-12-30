using System.Collections.Generic;
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

    ObjectPool<LineRenderer> objectPool;
    public Color currentColor;

    float zoomFactor;
    public float zoomSpeed = 0.5f;
    public float minZoom = 0;
    public float maxZoom = 10;

    bool showDebugLine = false;

    List<LineRenderer> toggleLines = new();
    float3 previousRecordedPoint;

    void Start()
    {
        objectPool = CreateObjectPool();
        zoomFactor = (minZoom + maxZoom) / 2;
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
        Camera camera = Camera.main;

        zoomFactor = math.clamp(zoomFactor + Input.mouseScrollDelta.y * zoomSpeed, minZoom, maxZoom);
        camera.transform.position = new float3(((float3)camera.transform.position).xy, zoomFactor);

        if (Input.GetMouseButtonDown(0))
        {
            currentLine = new SnowFlakeLine(objectPool, currentColor);
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

        //if(Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.Z))
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (lines.Count > 0)
            {
                int lastIndex = lines.Count - 1;
                lines[lastIndex].Dispose();
                lines.RemoveAt(lastIndex);
            }
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            ToggleDebugLines();
        }
    }

    void AddPointToCurrentLine(ref SnowFlakeLine currentLine, bool isFirstPoint = false)
    {
        float3 mousePos = new float3(Input.mousePosition.x, Input.mousePosition.y, distanceFromCamera);

        //put bool
        if(Input.GetKey(KeyCode.LeftShift) && !isFirstPoint)
        {
            if(math.any(math.abs(mousePos - previousRecordedPoint) > minSnapDist))
            {
                float3 delta = mousePos - previousRecordedPoint;
                float angleRad = math.atan2(delta.y, delta.x);
                float length = math.length(delta.xy);
                angleRad = SnowFlakeUtils.QuantizeRound(angleRad, math.radians(45));
                Debug.Log(math.degrees(angleRad));

                float2 recomposePoint = new float2(math.cos(angleRad), math.sin(angleRad)) * length;
                mousePos = previousRecordedPoint + new float3(recomposePoint.xy, 0);
            }
            else
            {
                mousePos = previousRecordedPoint;
            }
        }

        if(!SnowFlakeUtils.IsApprox(mousePos, previousRecordedPoint))
        {
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
            Camera camera = Camera.main;
            float cameraZ = camera.transform.position.z;

            for (int i = 0; i < debugLineCount; i++)
            {
                var debugLine = objectPool.Get();

                debugLine.positionCount = 2;
                debugLine.SetPosition(0, new float3(0, 0, cameraZ + distanceFromCamera));
                debugLine.SetPosition(1, SnowFlakeUtils.TransformPointHalfHex(new float3(0, 5, cameraZ + distanceFromCamera), i));

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

}
