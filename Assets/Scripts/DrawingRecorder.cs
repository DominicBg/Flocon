using System.Collections.Generic;
using UnityEngine;

public class DrawingRecorder : MonoBehaviour
{
    [SerializeField] Draw drawer;
    [SerializeField] bool inPlayBack;
    [SerializeField] SerializedDrawing toSave;
    [SerializeField] SerializedDrawing[] toPlayBack;

    [SerializeField] float pointInterval = 0.5f;

    float currentPointInterval = 0;
    Index index;

    List<Vector3>[] allLines;

    public struct Index
    {
        public int point;
        public int line;
        public int flake;
    }

    private void Start()
    {
        currentPointInterval = pointInterval;
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

        if(index.flake > toPlayBack.Length)
        {
            //finished
            return;
        }

        if(allLines == null)
        {
            //Create a new line
            drawer.SetNewLine(drawer.CreateNewLine());
            allLines = toPlayBack[index.flake].Deserialize();
        }


        currentPointInterval -= Time.deltaTime;
        if (currentPointInterval < 0)
        {
            currentPointInterval += pointInterval;

            drawer.CurrentLine.AddPoint(allLines[index.line][index.point]);
            index.point++;

            //reach end of single line
            if (index.point == allLines[index.line].Count)
            {
                drawer.CurrentLines.Add(drawer.CurrentLine);
                drawer.SetNewLine(drawer.CreateNewLine());

                index.point = 0;
                index.line++;
            }

            //reach end of all lines
            if (index.line == allLines.Length)
            {
                drawer.GenerateMesh();

                index.point = 0;
                index.line = 0;
                index.flake++;
                allLines = null;
            }



            //if (index.point == allLines.Length - 1)
            //{
            //    drawer.CurrentLine.AddPoint(allLines[index.line][index.point]);
            //    index.line++;
            //    index.point = 0;
            //}
            //else if (index.point < allLines.Length)
            //{
            //    drawer.CurrentLine.AddPoint(allLines[index.point]);
            //    index.point++;
            //}
            //else
            //{
            //    //reached the end
            //    index.point = 0;
            //    currentSerializeDrawingIndex++;
            //    drawer.SetNewLine(drawer.CreateNewLine());
            //}
                //drawer.GenerateMesh();
        }
    }
}
