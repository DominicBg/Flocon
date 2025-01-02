using Unity.Mathematics;

public static class SnowFlakeUtils
{
    public const int mirrorCount = 2;
    public const int hexCount = 6;
    public static readonly float hexAngle = math.radians(360f / hexCount);

    /// <summary>
    /// Index is base 12, 
    /// Each multiple of 2 represents the angle offset
    /// If odd number, mirror along axis
    /// </summary>
    public static float3 TransformPointMirror(float3 point, int index)
    {
        bool isMirror = index % 2 == 1;
        int rotationIndex = index / 2;

        //mirroring around 0
        point.x *= isMirror ? -1 : 1;

        //rotating around 0
        float2x2 rotMatrix = float2x2.Rotate(rotationIndex * hexAngle);
        point.xy = math.mul(rotMatrix, point.xy);

        return point;
    }

    /// <summary>
    /// Index is base 6, represents the angle
    /// </summary>
    public static float3 TransformPointHex(float3 point, int index)
    {
        //rotating around 0
        float2x2 rotMatrix = float2x2.Rotate(index * hexAngle);
        point.xy = math.mul(rotMatrix, point.xy);

        return point;
    }

    /// <summary>
    /// Index is base 12, represents the angle
    /// </summary>
    public static float3 TransformPointHalfHex(float3 point, int index)
    {
        //rotating around 0
        float2x2 rotMatrix = float2x2.Rotate(index * hexAngle * 0.5f);
        point.xy = math.mul(rotMatrix, point.xy);

        return point;
    }

    public static float Quantize(float x, float ammount)
    {
        return math.floor(x / ammount) * ammount;
    }
    public static float QuantizeRound(float x, float ammount)
    {
        return math.round(x / ammount) * ammount;
    }

    public static bool IsApprox(float3 a, float3 b, float approxDist = 0.01f)
    {
        return math.all(math.abs(a - b) < approxDist);
    }
    public static bool IsApprox(float2 a, float2 b, float approxDist = 0.01f)
    {
        return math.all(math.abs(a - b) < approxDist);
    }

    public static float3 Bezier(float3 p0, float3 p1, float3 p2, float t)
    {
        return math.lerp(math.lerp(p0, p1, t), math.lerp(p1, p2, t), t);
    }

    public static float3 Bezier(float3x3 bezier, float t)
    {
        float3 p0 = new float3(bezier.c0.x, bezier.c1.x, bezier.c2.x);
        float3 p1 = new float3(bezier.c0.y, bezier.c1.y, bezier.c2.y);
        float3 p2 = new float3(bezier.c0.z, bezier.c1.z, bezier.c2.z);
        return Bezier(p0, p1, p2, t);
    }
}
