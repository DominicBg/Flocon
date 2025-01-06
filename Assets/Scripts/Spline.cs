using UnityEngine;

[System.Serializable]
public class Spline
{
    public Vector3[] SegmentPositions { get; private set; }
    public int SegmentLength { get { return SegmentPositions.Length; } }
    public Vector3[] HandlePositions { get; private set; }
    public int HandleLength { get { return HandlePositions != null ? HandlePositions.Length : 0; } }

    public float[] Distances { get; private set; }
    public float[] DistancesSums { get; private set; }
    public float TotalDistance { get; private set; }

    public Spline(Vector3[] segmentPositions, Vector3[] handlePositions, float[] distances, float[] distancesSums)
    {
        SegmentPositions = segmentPositions;
        HandlePositions = handlePositions;
        Distances = distances;
        DistancesSums = distancesSums;
        TotalDistance = distancesSums[distancesSums.Length - 1];
    }

    /// <summary>
    /// Input a value between 0 and 1, and receive the position on the spline
    /// </summary>
    /// <param name="spline"></param>
    /// <param name="t"></param>
    /// <returns></returns>
    public Vector3 Lerp(float t)
    {
        float floatIndex = SegmentPositions.Length * t;
        int index = (int)floatIndex;
        float tt = floatIndex - index;

        if (index + 1 > SegmentPositions.Length - 1)
            return SegmentPositions[SegmentPositions.Length - 1];

        return Vector3.Lerp(SegmentPositions[index], SegmentPositions[index + 1], tt);
    }

    public Vector3 SlowAccurateLerp(float t)
    {
        float positionNormalizedBydistance = TotalDistance * t;
        for (int i = 0; i < DistancesSums.Length - 1; i++)
        {
            if (positionNormalizedBydistance >= DistancesSums[i] && positionNormalizedBydistance < DistancesSums[i + 1])
            {
                float tt = Mathf.InverseLerp(DistancesSums[i], DistancesSums[i + 1], positionNormalizedBydistance);
                return Vector3.Lerp(SegmentPositions[i], SegmentPositions[i + 1], tt);
            }
        }

        return SegmentPositions[SegmentPositions.Length - 1];
    }


    public void DebugShow(Color color)
    {
        Transform cameraTransform = Camera.main.transform;

        if (SegmentPositions == null || SegmentPositions.Length == 1)
            return;

        for (int i = 1; i < SegmentPositions.Length; i++)
        {
            Debug.DrawLine(SegmentPositions[i - 1], SegmentPositions[i], color);
        }
    }

    public void NormalizeSpline(bool normalizeFromMinusOnetoOne = false)
    {
        float minX = SegmentPositions[0].x;
        float maxX = SegmentPositions[SegmentPositions.Length - 1].x;

        float minY = int.MaxValue;
        float maxY = int.MinValue;

        for (int i = 0; i < SegmentPositions.Length; i++)
        {
            minY = Mathf.Min(minY, SegmentPositions[i].y);
            maxY = Mathf.Max(maxY, SegmentPositions[i].y);
        }

        for (int i = 0; i < SegmentPositions.Length; i++)
        {
            SegmentPositions[i].x = (SegmentPositions[i].x - minX) / (maxX - minX);
            SegmentPositions[i].y = (SegmentPositions[i].y - minY) / (maxY - minY);

            if (normalizeFromMinusOnetoOne)
                SegmentPositions[i].y = SegmentPositions[i].y * 2 - 1;
        }
    }
}