//using Unity.Mathematics;
//using UnityEngine;
///// <summary>
///// MADE BY DOMINIONXVII with lov ~
///// </summary>
///// 
////todo add native array support

//namespace MathXVII
//{
//    public static class SplineFactory2
//    {
//        /// <summary>
//        /// Input generic objects array with a function to extract its positions, then return an array of Vector3 containings the spline.
//        /// Ex : float3[] spline = GetFullSpline(rigidbodies, 10, rigidbody => rigidbody.position);
//        /// The ouput length will be resolutionPerSegment * (inputPositions.Length - 1);
//        /// </summary>
//        /// <typeparam name="T"></typeparam>
//        /// <param name="inputPositions"></param>
//        /// <param name="resolutionPerSegment"></param>
//        /// <param name="func"></param>
//        /// <returns></returns>
//        public static Spline GenerateSpline<T>(T[] inputs, int resolutionPerSegment, System.Func<T, float3> func)
//        {
//            //Will get filled overtime
//            float3[] convertedInputs = new float3[inputs.Length];

//            float3[] positions = GenerateSplinePositions(inputs, resolutionPerSegment, func, ref convertedInputs);
//            return InternalGenerateSpline(positions, convertedInputs);
//        }

//        /// <summary>
//        /// Input a vector3 array, then return an array of Vector3 containings the spline.
//        /// Ex : float3[] spline = GetFullSpline(positions, 10);
//        /// The ouput length will be resolutionPerSegment * (inputPositions.Length - 1);
//        /// </summary>
//        /// <typeparam name="T"></typeparam>
//        /// <param name="inputPositions"></param>
//        /// <param name="resolutionPerSegment"></param>
//        /// <param name="func"></param>
//        /// <returns></returns>
//        public static Spline GenerateSpline(float3[] inputs, int resolutionPerSegment)
//        {
//            float3[] positions = GenerateSplinePositions(inputs, resolutionPerSegment);
//            return InternalGenerateSpline(positions, inputs);
//        }

//        static Spline InternalGenerateSpline(float3[] positions, float3[] inputs)
//        {
//            if (inputs.Length < 2)
//            {
//                Debug.LogError("A spline must have at least 2 positions");
//                return null;
//            }

//            float[] distances = CalculateSplineDistances(positions);
//            float[] sums = CalculateSplineSums(positions);
//            return new Spline(positions, inputs, distances, sums);
//        }

//        #region Generic

//        static float3[] GenerateSplinePositions<T>(T[] input, int resolutionPerSegment, System.Func<T, float3> func, ref float3[] convertedInputs)
//        {
//            int length = resolutionPerSegment * (input.Length - 1);
//            float3[] positions = new float3[length];
//            for (int i = 0; i < input.Length - 1; i++)
//            {
//                int index = i * resolutionPerSegment;
//                SetSpline(ref positions, i, index, input, resolutionPerSegment, func);

//                //Fills the input array with actual vector3 instead 
//                convertedInputs[i] = func(input[i]);
//            }
//            return positions;
//        }


//        static void SetSpline<T>(ref float3[] positions, int index, int placementIndex, T[] points, int resolutionPerSegment, System.Func<T, float3> func)
//        {
//            float increment = 1 / (float)resolutionPerSegment;

//            for (int i = 0; i < resolutionPerSegment; i++)
//            {
//                float t = i * increment;
//                float3[] pos = Get4PointPositions(index, points, func);
//                positions[placementIndex + i] = CalculateSplinePosition(t, pos);
//            }
//        }

//        static float3[] Get4PointPositions<T>(int index, T[] points, System.Func<T, float3> func)
//        {
//            float3[] pos = new float3[4];
//            pos[0] = (index - 1 >= 0) ? func(points[index - 1]) : func(points[0]) - math.normalize(func(points[1]) - func(points[0])) * 0.1f;
//            pos[1] = func(points[index]);
//            pos[2] = func(points[index + 1]);
//            pos[3] = (index + 2 < points.Length) ? func(points[index + 2]) : func(points[index + 1]) - math.normalize(func(points[index + 1]) - func(points[index])) * 0.1f;

//            return pos;
//        }

//        #endregion
//        #region Float3
//        static float3[] GenerateSplinePositions(float3[] inputPositions, int resolutionPerSegment)
//        {
//            int length = resolutionPerSegment * (inputPositions.Length - 1);
//            float3[] positions = new float3[length];
//            for (int i = 0; i < inputPositions.Length - 1; i++)
//            {
//                int index = i * resolutionPerSegment;
//                SetSpline(ref positions, i, index, inputPositions, resolutionPerSegment);
//            }
//            return positions;
//        }

//        static void SetSpline(ref float3[] positions, int index, int placementIndex, float3[] points, int resolutionPerSegment)
//        {
//            float increment = 1 / (float)resolutionPerSegment;

//            for (int i = 0; i < resolutionPerSegment; i++)
//            {
//                float t = i * increment;
//                float3[] pos = Get4PointPositions(index, points);
//                positions[placementIndex + i] = CalculateSplinePosition(index, pos);
//            }
//        }

//        static float3[] Get4PointPositions(int index, float3[] points)
//        {
//            float3[] pos = new float3[4];
//            pos[0] = (index - 1 >= 0) ? points[index - 1] : points[0] - math.normalize(points[1] - points[0]) * 0.1f;
//            pos[1] = points[index];
//            pos[2] = points[index + 1];
//            pos[3] = (index + 2 < points.Length) ? points[index + 2] : points[index + 1] - math.normalize(points[index + 1] - points[index]) * 0.1f;
//            return pos;
//        }
//        #endregion

//        static float3 CalculateSplinePosition(float t, float3[] pos)
//        {
//            float tt = t * t;
//            float ttt = t * t * t;

//            float c0 = -ttt + 2 * tt - t;
//            float c1 = 3 * ttt - 5 * tt + 2;
//            float c2 = -3 * ttt + 4 * tt + t;
//            float c3 = ttt - tt;

//            return 0.5f * (c0 * pos[0] + c1 * pos[1] + c2 * pos[2] + c3 * pos[3]);
//        }

//        static float[] CalculateSplineDistances(float3[] positions)
//        {
//            float[] distances = new float[positions.Length - 1];
//            for (int i = 0; i < distances.Length; i++)
//            {
//                distances[i] = math.distance(positions[i], positions[i + 1]);
//            }
//            return distances;
//        }
//        static float[] CalculateSplineSums(float3[] positions)
//        {
//            float[] sums = new float[positions.Length];

//            float previousSum = 0;

//            // if distance = 1,2,3,4,5. total = 15
//            // sums = 1 + 3 + 6 + 10 + 15
//            for (int i = 1; i < sums.Length; i++)
//            {
//                sums[i] = previousSum + math.distance(positions[i], positions[i - 1]);
//                previousSum = sums[i];
//            }
//            return sums;
//        }
//    }
//}