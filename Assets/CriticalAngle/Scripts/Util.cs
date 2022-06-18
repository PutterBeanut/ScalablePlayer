using UnityEngine;

namespace CriticalAngle.ExpandablePlayer
{
    public static class Util
    {
        public static Vector3 V3X(this float a)
        {
            return new Vector3(a, 0.0f, 0.0f);
        }
        
        public static Vector3 V3Y(this float a)
        {
            return new Vector3(0.0f, a, 0.0f);
        }
        
        public static Vector3 V3Z(this float a)
        {
            return new Vector3(0.0f, 0.0f, a);
        }

        public static Vector2 V2X(this float a)
        {
            return new Vector2(a, 0.0f);
        }
        
        public static Vector2 V2Y(this float a)
        {
            return new Vector2(0.0f, a);
        }
    }
}