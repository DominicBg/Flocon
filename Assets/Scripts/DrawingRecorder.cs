using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class DrawingRecorder : MonoBehaviour
{
    [SerializeField] Draw drawer;
    [SerializeField] bool inPlayBack;
    [SerializeField] SerializedDrawing toSave;
    [SerializeField] SerializedDrawing[] toPlayBack;

    [SerializeField] float pointInterval = 0.5f;
    [SerializeField] float drawSpeed = 2; // meter/sec
    [SerializeField] AnimationCurve drawCruve;

    float currentPointInterval = 0;
    Index index;

    List<Vector3>[] allLines;

    //line count
    float3[] lerpPosEnd = new float3[SnowFlakeUtils.mirrorCount * SnowFlakeUtils.hexCount];
    float currentLineLength;

    public struct Index
    {
        public int pointIndex;
        public int lineIndex;
        public int flakeIndex;
    }


    private void Update()
    {
        if (inPlayBack && toPlayBack.Length == Draw.flakesToDraw)
        {
            UpdatePlayback();
        }
        else
        {
            UpdateRecording();
        }
    }

    void UpdateRecording()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            toSave.Serialize(drawer.CurrentLines);
        }

    }
    void UpdatePlayback()
    {
        if(drawer.CurrentMode != Draw.Mode.Drawing)
        {
            return;
        }

        if(index.flakeIndex == toPlayBack.Length)
        {
            //finished
            return;
        }

        if(allLines == null)
        {
            //Create a new line
            drawer.SetNewLine(drawer.CreateNewLine());
            allLines = toPlayBack[index.flakeIndex].Deserialize();
        }

        currentPointInterval += Time.deltaTime;
        if (currentPointInterval >= pointInterval)
        {
            currentPointInterval -= pointInterval;

            drawer.CurrentLine.AddPoint(allLines[index.lineIndex][index.pointIndex]);

            if(index.pointIndex >= 1)
            {
                for (int i = 0; i < lerpPosEnd.Length; i++)
                {
                    //Cache it because we modify it in the lerping
                    lerpPosEnd[i] = drawer.CurrentLine.GetPoint(i, index.pointIndex);
                }
                currentLineLength = math.distance(drawer.CurrentLine.GetPoint(0, index.pointIndex), drawer.CurrentLine.GetPoint(0, index.pointIndex - 1));
                pointInterval = (1f / drawSpeed) * currentLineLength;
            }
            else
            {
                for (int i = 0; i < lerpPosEnd.Length; i++)
                {
                    //Cache it because we modify it in the lerping
                    lerpPosEnd[i] = 0;
                }
            }

            index.pointIndex++;

            //reach end of single line
            if (index.pointIndex == allLines[index.lineIndex].Count)
            {
                drawer.CurrentLines.Add(drawer.CurrentLine);
                drawer.SetNewLine(drawer.CreateNewLine());

                index.pointIndex = 0;
                index.lineIndex++;
            }

            //reach end of all lines
            if (index.lineIndex == allLines.Length)
            {
                drawer.GenerateMesh();

                index.pointIndex = 0;
                index.lineIndex = 0;
                index.flakeIndex++;
                allLines = null;
            }
        }
        
        //Smooth the drawing
        if(drawer.CurrentLine.GetPointCount(0) >= 2)
        {
            float t = math.saturate(currentPointInterval / pointInterval);
            var lineRenderers = drawer.CurrentLine.LineRenderers;
            for (int lineId = 0; lineId < lineRenderers.Length; lineId++)
            {
                int currentIndex = lineRenderers[lineId].positionCount - 1;
                int prevIndex = lineRenderers[lineId].positionCount - 2;

                float3 prevPos = lineRenderers[lineId].GetPosition(prevIndex);
                float3 currentPos = lerpPosEnd[lineId];

                float3 lerpPos = math.lerp(prevPos, currentPos, drawCruve.Evaluate(t));
                lineRenderers[lineId].SetPosition(currentIndex, lerpPos);
            }
        }
    }
}
