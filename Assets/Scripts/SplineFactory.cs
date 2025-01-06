using UnityEngine;
/// <summary>
/// MADE BY DOMINIONXVII ~
/// </summary>
/// 

namespace MoreMaths
{
    public static class SplineFactory
    {
        /// <summary>
        /// Input generic objects array with a function to extract its positions, then return an array of Vector3 containings the spline.
        /// Ex : Vector3[] spline = GetFullSpline(rigidbodies, 10, rigidbody => rigidbody.position);
        /// The ouput length will be resolutionPerSegment * (inputPositions.Length - 1);
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inputPositions"></param>
        /// <param name="resolutionPerSegment"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Spline GenerateSpline<T>(T[] inputs, int resolutionPerSegment, System.Func<T, Vector3> func)
        {
            //Will get filled overtime
            Vector3[] convertedInputs = new Vector3[inputs.Length];

            Vector3[] positions = GenerateSplinePositions(inputs, resolutionPerSegment, func, ref convertedInputs);
            return InternalGenerateSpline(positions, convertedInputs);
        }

        /// <summary>
        /// Input a vector3 array, then return an array of Vector3 containings the spline.
        /// Ex : Vector3[] spline = GetFullSpline(positions, 10);
        /// The ouput length will be resolutionPerSegment * (inputPositions.Length - 1);
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inputPositions"></param>
        /// <param name="resolutionPerSegment"></param>
        /// <param name="func"></param>
        /// <returns></returns>
        public static Spline GenerateSpline(Vector3[] inputs, int resolutionPerSegment)
        {
            Vector3[] positions = GenerateSplinePositions(inputs, resolutionPerSegment);
            return InternalGenerateSpline(positions, inputs);
        }

        static Spline InternalGenerateSpline(Vector3[] positions, Vector3[] inputs)
        {
            if (inputs.Length < 2)
            {
                Debug.LogError("A spline must have at least 2 positions");
                return null;
            }

            float[] distances = CalculateSplineDistances(positions);
            float[] sums = CalculateSplineSums(positions);
            return new Spline(positions, inputs, distances, sums);
        }

        #region Generic

        static Vector3[] GenerateSplinePositions<T>(T[] input, int resolutionPerSegment, System.Func<T, Vector3> func, ref Vector3[] convertedInputs)
        {
            int length = resolutionPerSegment * (input.Length - 1);
            Vector3[] positions = new Vector3[length];
            for (int i = 0; i < input.Length - 1; i++)
            {
                int index = i * resolutionPerSegment;
                SetSpline(ref positions, i, index, input, resolutionPerSegment, func);

                //Fills the input array with actual vector3 instead 
                convertedInputs[i] = func(input[i]);
            }
            return positions;
        }


        static void SetSpline<T>(ref Vector3[] positions, int index, int placementIndex, T[] points, int resolutionPerSegment, System.Func<T, Vector3> func)
        {
            float increment = 1 / (float)resolutionPerSegment;

            for (int i = 0; i < resolutionPerSegment; i++)
            {
                float t = i * increment;
                Vector3[] pos = Get4PointPositions(index, t, points, func);
                positions[placementIndex + i] = CalculateSplinePosition(index, t, pos);
            }
        }

        static Vector3[] Get4PointPositions<T>(int index, float t, T[] points, System.Func<T, Vector3> func)
        {
            Vector3[] pos = new Vector3[4];
            pos[0] = (index - 1 >= 0) ? func(points[index - 1]) : func(points[0]) - (func(points[1]) - func(points[0])).normalized * 0.1f;
            pos[1] = func(points[index]);
            pos[2] = func(points[index + 1]);
            pos[3] = (index + 2 < points.Length) ? func(points[index + 2]) : func(points[index + 1]) - (func(points[index + 1]) - func(points[index])).normalized * 0.1f;

            return pos;
        }

        #endregion
        #region Vector3
        static Vector3[] GenerateSplinePositions(Vector3[] inputPositions, int resolutionPerSegment)
        {
            int length = resolutionPerSegment * (inputPositions.Length - 1);
            Vector3[] positions = new Vector3[length];
            for (int i = 0; i < inputPositions.Length - 1; i++)
            {
                int index = i * resolutionPerSegment;
                SetSpline(ref positions, i, index, inputPositions, resolutionPerSegment);
            }
            return positions;
        }

        static void SetSpline(ref Vector3[] positions, int index, int placementIndex, Vector3[] points, int resolutionPerSegment)
        {
            float increment = 1 / (float)resolutionPerSegment;

            for (int i = 0; i < resolutionPerSegment; i++)
            {
                float t = i * increment;
                Vector3[] pos = Get4PointPositions(index, t, points);
                positions[placementIndex + i] = CalculateSplinePosition(index, t, pos);
            }
        }

        static Vector3[] Get4PointPositions(int index, float t, Vector3[] points)
        {
            Vector3[] pos = new Vector3[4];
            pos[0] = (index - 1 >= 0) ? points[index - 1] : points[0] - (points[1] - points[0]).normalized * 0.1f;
            pos[1] = points[index];
            pos[2] = points[index + 1];
            pos[3] = (index + 2 < points.Length) ? points[index + 2] : points[index + 1] - (points[index + 1] - points[index]).normalized * 0.1f;
            return pos;
        }
        #endregion

        static Vector3 CalculateSplinePosition(int index, float t, Vector3[] pos)
        {
            float tt = t * t;
            float ttt = t * t * t;

            float c0 = -ttt + 2 * tt - t;
            float c1 = 3 * ttt - 5 * tt + 2;
            float c2 = -3 * ttt + 4 * tt + t;
            float c3 = ttt - tt;

            return 0.5f * (c0 * pos[0] + c1 * pos[1] + c2 * pos[2] + c3 * pos[3]);
        }

        static float[] CalculateSplineDistances(Vector3[] positions)
        {
            float[] distances = new float[positions.Length - 1];
            for (int i = 0; i < distances.Length; i++)
            {
                distances[i] = Vector3.Distance(positions[i], positions[i + 1]);
            }
            return distances;
        }
        static float[] CalculateSplineSums(Vector3[] positions)
        {
            float[] sums = new float[positions.Length];

            float previousSum = 0;

            // if distance = 1,2,3,4,5. total = 15
            // sums = 1 + 3 + 6 + 10 + 15
            for (int i = 1; i < sums.Length; i++)
            {
                sums[i] = previousSum + Vector3.Distance(positions[i], positions[i - 1]);
                previousSum = sums[i];
            }
            return sums;
        }
    }
}