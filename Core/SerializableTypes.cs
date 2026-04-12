using UnityEngine;

namespace BBBNexus
{
    /// <summary>
    /// BBBNexus 内部的可安全序列化向量类型喵~
    /// 与 NekoGraph.SerializableTypes 平行存在，两边互不依赖、互不转换。
    /// </summary>
    [System.Serializable]
    public struct SerializableVector2
    {
        public float x;
        public float y;

        public SerializableVector2(float x, float y)
        {
            this.x = x;
            this.y = y;
        }

        public static SerializableVector2 zero => new SerializableVector2(0f, 0f);
        public static SerializableVector2 one => new SerializableVector2(1f, 1f);

        [Newtonsoft.Json.JsonIgnore]
        public float sqrMagnitude => x * x + y * y;

        [Newtonsoft.Json.JsonIgnore]
        public float magnitude => Mathf.Sqrt(sqrMagnitude);

        [Newtonsoft.Json.JsonIgnore]
        public Vector2 normalized
        {
            get
            {
                float mag = magnitude;
                if (mag > 0f)
                    return new Vector2(x / mag, y / mag);
                return Vector2.zero;
            }
        }

        public static SerializableVector2 operator +(SerializableVector2 a, SerializableVector2 b) => new SerializableVector2(a.x + b.x, a.y + b.y);
        public static SerializableVector2 operator -(SerializableVector2 a, SerializableVector2 b) => new SerializableVector2(a.x - b.x, a.y - b.y);
        public static SerializableVector2 operator *(SerializableVector2 a, float b) => new SerializableVector2(a.x * b, a.y * b);
        public static SerializableVector2 operator *(float a, SerializableVector2 b) => new SerializableVector2(b.x * a, b.y * a);
        public static bool operator ==(SerializableVector2 a, SerializableVector2 b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(SerializableVector2 a, SerializableVector2 b) => a.x != b.x || a.y != b.y;
        public static SerializableVector2 operator +(SerializableVector2 a, Vector2 b) => new SerializableVector2(a.x + b.x, a.y + b.y);
        public static SerializableVector2 operator +(Vector2 a, SerializableVector2 b) => new SerializableVector2(a.x + b.x, a.y + b.y);
        public static SerializableVector2 operator -(SerializableVector2 a, Vector2 b) => new SerializableVector2(a.x - b.x, a.y - b.y);
        public static SerializableVector2 operator -(Vector2 a, SerializableVector2 b) => new SerializableVector2(a.x - b.x, a.y - b.y);

        public override bool Equals(object obj)
        {
            if (obj is SerializableVector2 sv2) return x == sv2.x && y == sv2.y;
            if (obj is Vector2 v2) return x == v2.x && y == v2.y;
            return false;
        }
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);

        public static implicit operator SerializableVector2(Vector2 v) => new SerializableVector2(v.x, v.y);
        public static implicit operator Vector2(SerializableVector2 v) => new Vector2(v.x, v.y);
        public static implicit operator Vector3(SerializableVector2 v) => new Vector3(v.x, v.y, 0f);
    }

    [System.Serializable]
    public struct SerializableVector3
    {
        public float x;
        public float y;
        public float z;

        public SerializableVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator SerializableVector3(Vector3 v) => new SerializableVector3(v.x, v.y, v.z);
        public static implicit operator Vector3(SerializableVector3 v) => new Vector3(v.x, v.y, v.z);
    }

    [System.Serializable]
    public struct SerializableVector2Int
    {
        public int x;
        public int y;

        public SerializableVector2Int(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public static SerializableVector2Int zero => new SerializableVector2Int(0, 0);
        public static SerializableVector2Int one => new SerializableVector2Int(1, 1);

        public static SerializableVector2Int operator +(SerializableVector2Int a, SerializableVector2Int b) => new SerializableVector2Int(a.x + b.x, a.y + b.y);
        public static SerializableVector2Int operator -(SerializableVector2Int a, SerializableVector2Int b) => new SerializableVector2Int(a.x - b.x, a.y - b.y);
        public static bool operator ==(SerializableVector2Int a, SerializableVector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(SerializableVector2Int a, SerializableVector2Int b) => a.x != b.x || a.y != b.y;
        public static SerializableVector2Int operator +(SerializableVector2Int a, Vector2Int b) => new SerializableVector2Int(a.x + b.x, a.y + b.y);
        public static SerializableVector2Int operator +(Vector2Int a, SerializableVector2Int b) => new SerializableVector2Int(a.x + b.x, a.y + b.y);
        public static SerializableVector2Int operator -(SerializableVector2Int a, Vector2Int b) => new SerializableVector2Int(a.x - b.x, a.y - b.y);
        public static SerializableVector2Int operator -(Vector2Int a, SerializableVector2Int b) => new SerializableVector2Int(a.x - b.x, a.y - b.y);

        public override bool Equals(object obj)
        {
            if (obj is SerializableVector2Int sv) return x == sv.x && y == sv.y;
            if (obj is Vector2Int v) return x == v.x && y == v.y;
            return false;
        }
        public override int GetHashCode() => x ^ (y << 2);

        public static implicit operator SerializableVector2Int(Vector2Int v) => new SerializableVector2Int(v.x, v.y);
        public static implicit operator Vector2Int(SerializableVector2Int v) => new Vector2Int(v.x, v.y);
    }

    [System.Serializable]
    public struct SerializableVector3Int
    {
        public int x;
        public int y;
        public int z;

        public SerializableVector3Int(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator SerializableVector3Int(Vector3Int v) => new SerializableVector3Int(v.x, v.y, v.z);
        public static implicit operator Vector3Int(SerializableVector3Int v) => new Vector3Int(v.x, v.y, v.z);
    }

    [System.Serializable]
    public struct SerializableQuaternion
    {
        public float x;
        public float y;
        public float z;
        public float w;

        public SerializableQuaternion(float x, float y, float z, float w)
        {
            this.x = x;
            this.y = y;
            this.z = z;
            this.w = w;
        }

        public static implicit operator SerializableQuaternion(Quaternion q) => new SerializableQuaternion(q.x, q.y, q.z, q.w);
        public static implicit operator Quaternion(SerializableQuaternion q) => new Quaternion(q.x, q.y, q.z, q.w);
    }
}
