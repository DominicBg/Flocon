using SCPE;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.UI;

//I swear, ill add a state machine one day
public class Draw : MonoBehaviour
{
    public const int flakesToDraw = 3;

    [Header("Draw")]
    public float delay = 0.2f;
    public float minSnapDist = 1f;
    public float lineWidth = 0.3f;
    public Color currentColor;

    public float3 starPos = new float3(0, 0, -25);

    [Header("Spin")]
    public float spinDuration = 2;
    public AnimationCurve spinCurve;
    public float spinAngles = 360 * 5;
    public float3 acceleration;
    public float selectDuration = 0.3f;
    public float selectDelayDurationBefore = 0.1f;
    public float selectDelayDurationAfter = 0.1f;
    public float selectRadialBlurIntensity = 0.3f;
    public float selectAngle = 10f;

    [Header("Pan")]
    public float panDuration = 10;
    public AnimationCurve panCurve;
    public float fadeToBlackDuration = 1;
    public float3x3 panBezier;
    public float3 panStartPos;
    public float3 panEndPos;
    public FlakeNoise[] flakeNoises;
    public int splineResolutionPerSegment = 25;

    [Header("Mesh")]
    public float meshThicc = 1;
    public bool doubleFace = false;
    public bool optimizeTriangles = true;

    [Header("Refs")]
    public ParticleSystem flakesPS;
    public MeshFilter meshFilter;
    public LineRenderer lineRendererPrefab;
    public MeshFilter[] flakesInPath;

    public TextMeshProUGUI instructions;
    public Image blackFade;
    public PostProcessVolume postProcessVolume;

    [Header("Other")]
    bool showDebugLine = false;
    public GameObject debugLines;

    ObjectPool<LineRenderer> objectPool;
    List<SnowFlakeLine> lines = new();
    SnowFlakeLine currentLine;
    float currentDelay = 0;

    float3 previousRecordedPoint;

    Camera mainCam;

    List<Mesh> meshList = new List<Mesh>();
    RadialBlur radialBlur;
    //Who has time to code a FSM anyway
    public enum Mode { Drawing, ConfirmingFlake, CameraPanning }
    Mode currentMode = Mode.Drawing;

    public SnowFlakeLine CurrentLine => currentLine;
    public Mode CurrentMode => currentMode;
    public List<SnowFlakeLine> CurrentLines => lines;

    [System.Serializable]
    public struct FlakeNoise
    {
        public float3 initialPosition;
        public float2 noiseSpeed;
        public float2 noiseAmp;
        public float2 noiseOffset;
        public float3 windStrength;

        //flakes quickly reach a terminal velocity, so applying gravity gives a weird effect
        public float fallSpeed;

        public float3 CalculateFlakePosition(float t)
        {
            float x = noiseAmp.x * noise.cnoise(new float2(t * noiseSpeed.x, noiseOffset.x));
            float y = fallSpeed * t;
            float z = noiseAmp.y * noise.cnoise(new float2(t * noiseSpeed.y, noiseOffset.y));
            return initialPosition + new float3(x, -y, z) + windStrength * t;
        }
    }

    void Start()
    {
        objectPool = CreateObjectPool();
        mainCam = Camera.main;
        ResetCamera();

        flakesPS.Stop();
        SetMode(Mode.Drawing);

        postProcessVolume.profile.TryGetSettings(out radialBlur);
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
        DebugFlakePath();
        switch (currentMode)
        {
            case Mode.Drawing:
                UpdateDrawingMode();
                break;
            case Mode.ConfirmingFlake:
                //updated in coroutine
                break;
            case Mode.CameraPanning:
                //updated in coroutine
                break;
        }
    }

    private void UpdateDrawingMode()
    {
        if (Input.GetMouseButtonDown(0))
        {
            currentLine = CreateNewLine();
            AddPointToLine(MousePos, ref currentLine, isFirstPoint: true);
            currentDelay = delay;
        }
        else if (Input.GetMouseButton(0))
        {
            //record
            if (currentDelay <= 0)
            {
                AddPointToLine(MousePos, ref currentLine);
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

        if (Input.GetKeyDown(KeyCode.N))
        {
            SetMode(Mode.CameraPanning);
        }
    }

    public SnowFlakeLine CreateNewLine()
    {
        return new SnowFlakeLine(objectPool, currentColor, lineWidth);
    }

    public void SetNewLine(in SnowFlakeLine line)
    {
        currentLine = line;
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

    public void GenerateMesh()
    {
        var mesh = GenerateMesh(in lines);
        var renderer = flakesPS.GetComponent<ParticleSystemRenderer>();
        meshFilter.mesh = mesh;
        meshList.Add(mesh);

        //particle system limit
        if (meshList.Count > 4)
        {
            meshList.RemoveAt(0);
        }
        renderer.SetMeshes(meshList.ToArray());
        ClearCurrentSnowFlake();

        SetMode(Mode.ConfirmingFlake);
    }

    private void ClearCurrentSnowFlake()
    {
        for (int i = lines.Count - 1; i >= 0; i--)
        {
            lines[i].Dispose();
        }
        lines.Clear();
    }

    private float3 MousePos => new float3(Input.mousePosition.x, Input.mousePosition.y, math.abs(mainCam.transform.position.z));

    public void AddPointToLine(float3 mousePos, ref SnowFlakeLine currentLine, bool isFirstPoint = false)
    {
        //put bool for mode?
        if (!Input.GetKey(KeyCode.LeftShift) && !isFirstPoint)
        {
            //Snap point in angles
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

    void ResetCamera()
    {
        mainCam.transform.position = starPos;
        mainCam.transform.rotation = quaternion.identity;
    }

    void ToggleDebugLines()
    {
        ShowDebugLine(!showDebugLine);
    }

    void ShowDebugLine(bool show)
    {
        showDebugLine = show;
        if (show)
        {
            ResetCamera();
            debugLines.SetActive(true);
        }
        else
        {
            debugLines.SetActive(false);
        }
    }

    //TODO burst?
    Mesh GenerateMesh(in List<SnowFlakeLine> lines)
    {
        Mesh mesh = new Mesh();

        NativeList<float3> vertices = new NativeList<float3>(Allocator.TempJob);
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

                    if (meshThicc <= 0)
                    {
                        //Index are ordered for vertices
                        // 2    3
                        //  
                        //
                        // 0    1
                        // 4 vertex per lines
                        const int bottomLeftIndex = 0;
                        const int bottomRightIndex = 1;
                        const int topLeftIndex = 2;
                        const int topRightIndex = 3;
                        const int vertexPerLine = 4;
                        int triIndex = vertices.Length;

                        //Second iteration can blend corners
                        if (i > 1)
                        {
                            int prevTriIndex = triIndex - vertexPerLine;
                            float3 prevTopLeft = vertices[prevTriIndex + topLeftIndex];
                            float3 prevTopRight = vertices[prevTriIndex + topRightIndex];

                            //Blend edge with previous position so they touch if the line width is not mini 
                            bottomLeft = (bottomLeft + prevTopLeft) / 2f; //todo optim, reuse same vertex
                            bottomRight = (bottomRight + prevTopRight) / 2f; //todo optim, reuse same vertex

                            vertices[prevTriIndex + topLeftIndex] = bottomLeft;
                            vertices[prevTriIndex + topRightIndex] = bottomRight;
                        }

                        vertices.Add(bottomLeft);
                        vertices.Add(bottomRight);
                        vertices.Add(topLeft);
                        vertices.Add(topRight);

                        uvs.Add(new float2(0, 0));
                        uvs.Add(new float2(1, 0));
                        uvs.Add(new float2(0, 1));
                        uvs.Add(new float2(1, 1));

                        triangles.Add(triIndex + bottomRightIndex);
                        triangles.Add(triIndex + topLeftIndex);
                        triangles.Add(triIndex + bottomLeftIndex);

                        triangles.Add(triIndex + bottomRightIndex);
                        triangles.Add(triIndex + topRightIndex);
                        triangles.Add(triIndex + topLeftIndex);
                    }
                    else
                    {

                        //Index are ordered for vertices
                        // 2    3,6    7
                        //  
                        //
                        // 0    1,4    5
                        // 8 vertex per lines
                        float3 bottomMid = prevPoint - math.forward() * meshThicc;
                        float3 topMid = point - math.forward() * meshThicc;


                        //Side 1
                        int triIndex1 = vertices.Length;
                        int prevTriIndex1 = triIndex1 - 8;
                        //Second iteration can blend corners
                        if (i > 1)
                        {
                            float3 prevTopLeft = vertices[prevTriIndex1 + 2];
                            float3 prevTopMid = vertices[prevTriIndex1 + 3];

                            //Blend edge with previous position so they touch if the line width is not mini 
                            bottomLeft = (bottomLeft + prevTopLeft) / 2f; //todo optim, reuse same vertex
                            bottomMid = (bottomMid + prevTopMid) / 2f; //todo optim, reuse same vertex

                            vertices[prevTriIndex1 + 2] = bottomLeft;
                            vertices[prevTriIndex1 + 3] = bottomMid;
                        }

                        vertices.Add(bottomLeft);
                        vertices.Add(bottomMid);
                        vertices.Add(topLeft);
                        vertices.Add(topMid);

                        uvs.Add(new float2(0, 0));
                        uvs.Add(new float2(1, 0));
                        uvs.Add(new float2(0, 1));
                        uvs.Add(new float2(1, 1));

                        triangles.Add(triIndex1 + 1);
                        triangles.Add(triIndex1 + 2);
                        triangles.Add(triIndex1 + 0);

                        triangles.Add(triIndex1 + 1);
                        triangles.Add(triIndex1 + 3);
                        triangles.Add(triIndex1 + 2);

                        //Side 2
                        int triIndex2 = vertices.Length;

                        //Second iteration can blend corners
                        if (i > 1)
                        {
                            float3 prevTopMid = vertices[prevTriIndex1 + 6];
                            float3 prevTopRight = vertices[prevTriIndex1 + 7];

                            //Blend edge with previous position so they touch if the line width is not mini 
                            bottomMid = (bottomMid + prevTopMid) / 2f; //todo optim, reuse same vertex
                            bottomRight = (bottomRight + prevTopRight) / 2f; //todo optim, reuse same vertex

                            vertices[prevTriIndex1 + 6] = bottomMid;
                            vertices[prevTriIndex1 + 7] = bottomRight;
                        }

                        vertices.Add(bottomMid); //todo optim, reuse previous
                        vertices.Add(bottomRight);
                        vertices.Add(topMid); //todo optim, reuse previous
                        vertices.Add(topRight);

                        uvs.Add(new float2(0, 0));
                        uvs.Add(new float2(1, 0));
                        uvs.Add(new float2(0, 1));
                        uvs.Add(new float2(1, 1));

                        triangles.Add(triIndex2 + 1);
                        triangles.Add(triIndex2 + 2);
                        triangles.Add(triIndex2 + 0);

                        triangles.Add(triIndex2 + 1);
                        triangles.Add(triIndex2 + 3);
                        triangles.Add(triIndex2 + 2);
                    }

                    prevPoint = point;
                }
            }
        }

        if (doubleFace)
        {
            int vertexCount = vertices.Length;
            for (int i = 0; i < vertexCount; i += 4)
            {
                float3 v0 = vertices[i + 0];
                float3 v1 = vertices[i + 1];
                float3 v2 = vertices[i + 2];
                float3 v3 = vertices[i + 3];

                v0.z = -v0.z;
                v1.z = -v1.z;
                v2.z = -v2.z;
                v3.z = -v3.z;

                int triIndex = vertices.Length;
                vertices.Add(v0);
                vertices.Add(v1);
                vertices.Add(v2);
                vertices.Add(v3);

                uvs.Add(new float2(0, 0));
                uvs.Add(new float2(1, 0));
                uvs.Add(new float2(0, 1));
                uvs.Add(new float2(1, 1));

                //inverse the order so that the normal is outward 
                triangles.Add(triIndex + 0);
                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 1);

                triangles.Add(triIndex + 2);
                triangles.Add(triIndex + 3);
                triangles.Add(triIndex + 1);
            }
        }

        mesh.SetVertices<float3>(vertices);
        mesh.SetTriangles(triangles.ToArray(), 0);
        mesh.SetUVs<float2>(0, uvs);

        if (optimizeTriangles)
        {
            mesh.Optimize();
        }

        mesh.RecalculateNormals();

        vertices.Dispose();
        return mesh;
    }

    IEnumerator SpinFlakeCoroutine()
    {
        meshFilter.transform.position = Vector3.zero;
        meshFilter.transform.rotation = quaternion.identity;
        meshFilter.transform.localScale = Vector3.one;

        yield return new WaitForSeconds(selectDelayDurationBefore);
        yield return FlakeConfirmAnim();
        yield return new WaitForSeconds(selectDelayDurationAfter);

        bool willGoToCameraPanning = meshList.Count == flakesToDraw;
        if (willGoToCameraPanning)
        {
            StartCoroutine(FadeToBlack(true, spinDuration));
        }

        float3 velocity = 0;
        float t = 0;
        float spinSpeed = 1f / spinDuration;
        while (t < 1)
        {
            t += Time.deltaTime * spinSpeed;
            t = math.saturate(t);

            float yEuler = spinCurve.Evaluate(t) * spinAngles;
            quaternion rotation = quaternion.Euler(0, math.radians(yEuler), 0);
            meshFilter.transform.rotation = rotation;
            velocity += acceleration * Time.deltaTime;
            meshFilter.transform.position += (Vector3)velocity * Time.deltaTime;

            meshFilter.transform.localScale = Vector3.one * (1 - t);

            yield return null;
        }

        if (willGoToCameraPanning)
        {
            SetMode(Mode.CameraPanning);
        }
        else
        {
            SetMode(Mode.Drawing);
        }
    }

    IEnumerator CameraPanCoroutine()
    {
        flakesPS.Play();

        int flakeCount = math.min(flakesInPath.Length, meshList.Count);
        for (int i = 0; i < flakeCount; i++)
        {
            flakesInPath[i].mesh = meshList[i];

            //create a division between points
            //lines are index, x are desired pos
            //| x | x | x | x |
            float ratio = ((float)i + 1) / (flakeCount + 1);
            float3 bezierPos = SnowFlakeUtils.Bezier(panBezier, ratio);
            flakesInPath[i].transform.position = bezierPos;

            const float dt = 0.01f;
            float3 bezierDx = (bezierPos - SnowFlakeUtils.Bezier(panBezier, ratio + dt)) / dt;
            quaternion alignedRotation = quaternion.LookRotation(bezierDx, math.up());
            flakesInPath[i].transform.rotation = alignedRotation;
        }

        StartCoroutine(FadeToBlack(false, fadeToBlackDuration));

        float3[] cameraPositions = CalculateCameraPanTrack();

        Spline spline = MoreMaths.SplineFactory.GenerateSpline(cameraPositions, splineResolutionPerSegment, x => x);

        float t = 0;
        float panSpeed = 1f / panDuration;

        while (t < 1)
        {
            t += Time.deltaTime * panSpeed;

            float3 position = spline.Lerp(t);
            float3 positionDt = spline.Lerp(t + 0.01f); //record prevPos for max perf?

            quaternion splineRotation = quaternion.LookRotation(math.normalize(positionDt - position), math.up());

            mainCam.transform.position = position;
            mainCam.transform.rotation = splineRotation;

            float duration = t * panDuration;
            for (int i = 0; i < flakeCount; i++)
            {
                //todo redo this part
                float3 localEuler = flakesInPath[i].transform.localEulerAngles;
                localEuler.z = t * 360;
                flakesInPath[i].transform.localEulerAngles = localEuler;

                flakesInPath[i].transform.position = flakeNoises[i].CalculateFlakePosition(duration);
            }

            yield return null;
        }
        SetMode(Mode.Drawing);
    }

    float3[] CalculateCameraPanTrack()
    {
        //+2 for initial and end position
        float3[] cameraPosition = new float3[flakesInPath.Length + 2];

        //5 waypoint, so we have 4 separation
        float durationPerPathSegment = panDuration / (cameraPosition.Length - 1);
        for (int i = 0; i < flakesInPath.Length; i++)
        {
            float timeToReachFlake = (i + 1) * durationPerPathSegment;
            float3 flakePredictedPosition = flakeNoises[i].CalculateFlakePosition(timeToReachFlake);
            cameraPosition[i + 1] = flakePredictedPosition;
        }
        cameraPosition[0] = panStartPos;
        cameraPosition[cameraPosition.Length - 1] = panEndPos;

        return cameraPosition;
    }

    void DebugFlakePath()
    {
        float3[] cameraPositions = CalculateCameraPanTrack();

        //Camera Path
        for (int i = 1; i < cameraPositions.Length; i++)
        {
            Debug.DrawLine(cameraPositions[i - 1], cameraPositions[i], Color.cyan);
        }

        Spline spline = MoreMaths.SplineFactory.GenerateSpline(cameraPositions, splineResolutionPerSegment, x => x);

        //Camera Path
        for (int i = 1; i < spline.SegmentLength; i++)
        {
            Debug.DrawLine(spline.SegmentPositions[i - 1], spline.SegmentPositions[i], Color.green);
        }

        //Flake falling
        const int debugAccuracy = 100;
        float fallingDebugStep = (1f / debugAccuracy) * panDuration;
        for (int i = 0; i < flakesInPath.Length; i++)
        {
            float3 prevPos = flakeNoises[i].CalculateFlakePosition(0);
            for (int j = 1; j < debugAccuracy; j++)
            {
                float t = j * fallingDebugStep;
                float3 currentPos = flakeNoises[i].CalculateFlakePosition(t);
                Debug.DrawLine(prevPos, currentPos);
                prevPos = currentPos;
            }
        }
    }

    IEnumerator FlakeConfirmAnim()
    {
        float t = 0;
        float popSpeed = 1f / selectDuration;
        while (t < 1)
        {
            t += Time.deltaTime * popSpeed;
            t = math.saturate(t);
            // first part of the sin that does [0 > 1 > 0] without the negative part
            float sinScale = math.sin(t * math.PI);
            radialBlur.amount.value = sinScale * selectRadialBlurIntensity;
            meshFilter.transform.localEulerAngles = new Vector3(0, 0, sinScale * selectAngle);
            yield return null;
        }
    }

    void SetMode(Mode mode)
    {
        currentMode = mode;
        switch (mode)
        {
            case Mode.Drawing:
                StartDrawing();
                break;
            case Mode.ConfirmingFlake:
                StartConfirmFlake();
                break;
            case Mode.CameraPanning:
                StartCameraPanning();
                break;
        }
    }

    private void StartDrawing()
    {
        ShowDebugLine(true);
        instructions.gameObject.SetActive(true);
        instructions.SetText($"Draw Snowflakes ({ meshList.Count}/{flakesToDraw})");
    }

    private void StartConfirmFlake()
    {
        StartCoroutine(SpinFlakeCoroutine());
        ShowDebugLine(false);
        instructions.gameObject.SetActive(false);
    }

    private void StartCameraPanning()
    {
        StartCoroutine(CameraPanCoroutine());
        ShowDebugLine(false);
        instructions.gameObject.SetActive(false);
    }

    IEnumerator FadeToBlack(bool fadetoBlack, float duration)
    {
        float fade = 0;
        float fadeSpeed = 1f / duration;
        while (fade < 1)
        {
            fade += Time.deltaTime * fadeSpeed;
            fade = math.saturate(fade);

            float t = fade;
            if (!fadetoBlack)
            {
                t = (1 - fade);
            }

            blackFade.color = Color.black * t;
            yield return null;
        }
    }
}
