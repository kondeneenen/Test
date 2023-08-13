using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Util
{
    public class Math
    {
        public const float cAlmostZero = 0.000001f;
        public const float cAlmostOne = 0.999999f;

        public static bool EqualEpsilon(float a, float b, float e = cAlmostZero)
        {
            return Mathf.Abs(a - b) < e;
        }

        public static bool IsZero(float v, float e = cAlmostZero)
        {
            return EqualEpsilon(v, 0f, e);
        }

    }
}
