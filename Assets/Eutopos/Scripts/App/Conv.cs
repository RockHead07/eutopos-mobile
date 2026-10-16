namespace Eutopos.App
{
    static class Conv
    {
        public static UnityEngine.Vector3 ToUnity(System.Numerics.Vector3 v) => new UnityEngine.Vector3(v.X, v.Y, v.Z);
        public static System.Numerics.Vector3 ToNum(UnityEngine.Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        public static System.Numerics.Vector2 FloorG(System.Numerics.Vector3 posB) => new System.Numerics.Vector2(posB.X, posB.Z);
    }
}
