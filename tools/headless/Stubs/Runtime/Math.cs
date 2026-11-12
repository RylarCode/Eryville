// Headless stand-ins for Unity's math types: enough behaviour for the game's code to run outside Unity.
using System;
using System.Globalization;

namespace UnityEngine
{
    public struct Vector2 : IEquatable<Vector2>
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 zero => new Vector2(0, 0);
        public static Vector2 one => new Vector2(1, 1);
        public static Vector2 up => new Vector2(0, 1);
        public static Vector2 down => new Vector2(0, -1);
        public static Vector2 left => new Vector2(-1, 0);
        public static Vector2 right => new Vector2(1, 0);
        public float this[int i] { get => i == 0 ? x : y; set { if (i == 0) x = value; else y = value; } }
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public float sqrMagnitude => x * x + y * y;
        public Vector2 normalized { get { float m = magnitude; return m > 1e-5f ? new Vector2(x / m, y / m) : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny) { x = nx; y = ny; }
        public static float Distance(Vector2 a, Vector2 b) => (a - b).magnitude;
        public static float Dot(Vector2 a, Vector2 b) => a.x * b.x + a.y * b.y;
        public static Vector2 Lerp(Vector2 a, Vector2 b, float t) { t = Mathf.Clamp01(t); return new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t); }
        public static Vector2 LerpUnclamped(Vector2 a, Vector2 b, float t) => new Vector2(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t);
        public static Vector2 MoveTowards(Vector2 c, Vector2 t, float d) { Vector2 v = t - c; float m = v.magnitude; return m <= d || m == 0f ? t : c + v / m * d; }
        public static Vector2 Scale(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
        public static Vector2 Min(Vector2 a, Vector2 b) => new Vector2(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2 Max(Vector2 a, Vector2 b) => new Vector2(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static float Angle(Vector2 a, Vector2 b) { float d = (float)Math.Sqrt(a.sqrMagnitude * b.sqrMagnitude); if (d < 1e-15f) return 0f; return (float)Math.Acos(Mathf.Clamp(Dot(a, b) / d, -1f, 1f)) * Mathf.Rad2Deg; }
        public static Vector2 ClampMagnitude(Vector2 v, float max) => v.sqrMagnitude > max * max ? v.normalized * max : v;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
        public static Vector2 operator -(Vector2 a) => new Vector2(-a.x, -a.y);
        public static Vector2 operator *(Vector2 a, float d) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(float d, Vector2 a) => new Vector2(a.x * d, a.y * d);
        public static Vector2 operator *(Vector2 a, Vector2 b) => new Vector2(a.x * b.x, a.y * b.y);
        public static Vector2 operator /(Vector2 a, float d) => new Vector2(a.x / d, a.y / d);
        public static Vector2 operator /(Vector2 a, Vector2 b) => new Vector2(a.x / b.x, a.y / b.y);
        public static bool operator ==(Vector2 a, Vector2 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector2 a, Vector2 b) => !(a == b);
        public static implicit operator Vector3(Vector2 v) => new Vector3(v.x, v.y, 0);
        public static implicit operator Vector2(Vector3 v) => new Vector2(v.x, v.y);
        public bool Equals(Vector2 o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2})", x, y);
        public string ToString(string f) => string.Format(CultureInfo.InvariantCulture, "({0}, {1})", x.ToString(f, CultureInfo.InvariantCulture), y.ToString(f, CultureInfo.InvariantCulture));
    }

    public struct Vector3 : IEquatable<Vector3>
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public Vector3(float x, float y) { this.x = x; this.y = y; z = 0; }
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 left => new Vector3(-1, 0, 0);
        public static Vector3 right => new Vector3(1, 0, 0);
        public static Vector3 forward => new Vector3(0, 0, 1);
        public static Vector3 back => new Vector3(0, 0, -1);
        public float this[int i] { get => i == 0 ? x : i == 1 ? y : z; set { if (i == 0) x = value; else if (i == 1) y = value; else z = value; } }
        public float magnitude => (float)Math.Sqrt(x * x + y * y + z * z);
        public float sqrMagnitude => x * x + y * y + z * z;
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public void Normalize() { this = normalized; }
        public void Set(float nx, float ny, float nz) { x = nx; y = ny; z = nz; }
        public static float Distance(Vector3 a, Vector3 b) => (a - b).magnitude;
        public static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) { t = Mathf.Clamp01(t); return a + (b - a) * t; }
        public static Vector3 LerpUnclamped(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
        public static Vector3 MoveTowards(Vector3 c, Vector3 t, float d) { Vector3 v = t - c; float m = v.magnitude; return m <= d || m == 0f ? t : c + v / m * d; }
        public static Vector3 Scale(Vector3 a, Vector3 b) => new Vector3(a.x * b.x, a.y * b.y, a.z * b.z);
        public static Vector3 Min(Vector3 a, Vector3 b) => new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
        public static Vector3 Max(Vector3 a, Vector3 b) => new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
        public static Vector3 ClampMagnitude(Vector3 v, float max) => v.sqrMagnitude > max * max ? v.normalized * max : v;
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float time, float maxSpeed, float dt) { float tt = Math.Max(0.0001f, time); float k = Mathf.Clamp01(dt / tt); Vector3 n = Lerp(c, t, k); vel = dt > 0 ? (n - c) / dt : zero; return n; }
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float time) => SmoothDamp(c, t, ref vel, time, float.PositiveInfinity, Time.deltaTime);
        public static Vector3 SmoothDamp(Vector3 c, Vector3 t, ref Vector3 vel, float time, float maxSpeed) => SmoothDamp(c, t, ref vel, time, maxSpeed, Time.deltaTime);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator -(Vector3 a) => new Vector3(-a.x, -a.y, -a.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator *(float d, Vector3 a) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 operator /(Vector3 a, float d) => new Vector3(a.x / d, a.y / d, a.z / d);
        public static bool operator ==(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < 9.99999944E-11f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public bool Equals(Vector3 o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3 v && Equals(v);
        public override int GetHashCode() => x.GetHashCode() ^ (y.GetHashCode() << 2) ^ (z.GetHashCode() >> 2);
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:F2}, {1:F2}, {2:F2})", x, y, z);
        public string ToString(string f) => string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", x.ToString(f, CultureInfo.InvariantCulture), y.ToString(f, CultureInfo.InvariantCulture), z.ToString(f, CultureInfo.InvariantCulture));
    }

    public struct Vector4
    {
        public float x, y, z, w;
        public Vector4(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Vector4 zero => new Vector4(0, 0, 0, 0);
        public static Vector4 one => new Vector4(1, 1, 1, 1);
    }

    public struct Vector2Int : IEquatable<Vector2Int>
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static Vector2Int zero => new Vector2Int(0, 0);
        public static Vector2Int one => new Vector2Int(1, 1);
        public static Vector2Int up => new Vector2Int(0, 1);
        public static Vector2Int down => new Vector2Int(0, -1);
        public static Vector2Int left => new Vector2Int(-1, 0);
        public static Vector2Int right => new Vector2Int(1, 0);
        public float magnitude => (float)Math.Sqrt(x * x + y * y);
        public int sqrMagnitude => x * x + y * y;
        public int this[int i] { get => i == 0 ? x : y; set { if (i == 0) x = value; else y = value; } }
        public void Set(int nx, int ny) { x = nx; y = ny; }
        public static float Distance(Vector2Int a, Vector2Int b) => (a - b).magnitude;
        public static Vector2Int Min(Vector2Int a, Vector2Int b) => new Vector2Int(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
        public static Vector2Int Max(Vector2Int a, Vector2Int b) => new Vector2Int(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
        public static Vector2Int FloorToInt(Vector2 v) => new Vector2Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y));
        public static Vector2Int RoundToInt(Vector2 v) => new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
        public static Vector2Int CeilToInt(Vector2 v) => new Vector2Int(Mathf.CeilToInt(v.x), Mathf.CeilToInt(v.y));
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new Vector2Int(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new Vector2Int(a.x - b.x, a.y - b.y);
        public static Vector2Int operator -(Vector2Int a) => new Vector2Int(-a.x, -a.y);
        public static Vector2Int operator *(Vector2Int a, int d) => new Vector2Int(a.x * d, a.y * d);
        public static Vector2Int operator *(int d, Vector2Int a) => new Vector2Int(a.x * d, a.y * d);
        public static Vector2Int operator /(Vector2Int a, int d) => new Vector2Int(a.x / d, a.y / d);
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public static implicit operator Vector2(Vector2Int v) => new Vector2(v.x, v.y);
        public static explicit operator Vector3Int(Vector2Int v) => new Vector3Int(v.x, v.y, 0);
        public bool Equals(Vector2Int o) => x == o.x && y == o.y;
        public override bool Equals(object o) => o is Vector2Int v && Equals(v);
        public override int GetHashCode() => unchecked(x * 73856093 ^ y * 19349663);
        public override string ToString() => $"({x}, {y})";
    }

    public struct Vector3Int : IEquatable<Vector3Int>
    {
        public int x, y, z;
        public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public Vector3Int(int x, int y) { this.x = x; this.y = y; z = 0; }
        public static Vector3Int zero => new Vector3Int(0, 0, 0);
        public static Vector3Int one => new Vector3Int(1, 1, 1);
        public static Vector3Int FloorToInt(Vector3 v) => new Vector3Int(Mathf.FloorToInt(v.x), Mathf.FloorToInt(v.y), Mathf.FloorToInt(v.z));
        public static Vector3Int RoundToInt(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y), Mathf.RoundToInt(v.z));
        public static Vector3Int operator +(Vector3Int a, Vector3Int b) => new Vector3Int(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3Int operator -(Vector3Int a, Vector3Int b) => new Vector3Int(a.x - b.x, a.y - b.y, a.z - b.z);
        public static bool operator ==(Vector3Int a, Vector3Int b) => a.x == b.x && a.y == b.y && a.z == b.z;
        public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);
        public static implicit operator Vector3(Vector3Int v) => new Vector3(v.x, v.y, v.z);
        public static explicit operator Vector2Int(Vector3Int v) => new Vector2Int(v.x, v.y);
        public bool Equals(Vector3Int o) => x == o.x && y == o.y && z == o.z;
        public override bool Equals(object o) => o is Vector3Int v && Equals(v);
        public override int GetHashCode() => unchecked(x * 73856093 ^ y * 19349663 ^ z * 83492791);
        public override string ToString() => $"({x}, {y}, {z})";
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; euler = Vector3.zero; }
        public static Quaternion identity => new Quaternion(0, 0, 0, 1);
        private Vector3 euler;
        public Vector3 eulerAngles { get => euler; set { this = Euler(value); } }
        public static Quaternion Euler(Vector3 e) { Quaternion q = identity; q.euler = e; return q; }
        public static Quaternion Euler(float x, float y, float z) => Euler(new Vector3(x, y, z));
        public static Quaternion AngleAxis(float angle, Vector3 axis) => Euler(axis * angle);
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            float a = q.euler.z * Mathf.Deg2Rad; float c = (float)Math.Cos(a), s = (float)Math.Sin(a);
            return new Vector3(v.x * c - v.y * s, v.x * s + v.y * c, v.z);
        }
        public static Quaternion operator *(Quaternion a, Quaternion b) => Euler(a.euler + b.euler);
        public static bool operator ==(Quaternion a, Quaternion b) => a.euler == b.euler;
        public static bool operator !=(Quaternion a, Quaternion b) => !(a == b);
        public override bool Equals(object o) => o is Quaternion q && q == this;
        public override int GetHashCode() => euler.GetHashCode();
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public const float Deg2Rad = PI / 180f;
        public const float Rad2Deg = 180f / PI;
        public const float Infinity = float.PositiveInfinity;
        public const float NegativeInfinity = float.NegativeInfinity;
        public static readonly float Epsilon = float.Epsilon;
        public static float Abs(float v) => Math.Abs(v);
        public static int Abs(int v) => Math.Abs(v);
        public static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
        public static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
        public static float LerpUnclamped(float a, float b, float t) => a + (b - a) * t;
        public static float InverseLerp(float a, float b, float v) => a != b ? Clamp01((v - a) / (b - a)) : 0f;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Max(params float[] v) { float m = v.Length > 0 ? v[0] : 0f; foreach (float x in v) if (x > m) m = x; return m; }
        public static int Max(int a, int b) => a > b ? a : b;
        public static int Max(params int[] v) { int m = v.Length > 0 ? v[0] : 0; foreach (int x in v) if (x > m) m = x; return m; }
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Min(params float[] v) { float m = v.Length > 0 ? v[0] : 0f; foreach (float x in v) if (x < m) m = x; return m; }
        public static int Min(int a, int b) => a < b ? a : b;
        public static int Min(params int[] v) { int m = v.Length > 0 ? v[0] : 0; foreach (int x in v) if (x < m) m = x; return m; }
        public static float Floor(float v) => (float)Math.Floor(v);
        public static float Ceil(float v) => (float)Math.Ceiling(v);
        public static float Round(float v) => (float)Math.Round(v, MidpointRounding.ToEven);
        public static int FloorToInt(float v) => (int)Math.Floor(v);
        public static int CeilToInt(float v) => (int)Math.Ceiling(v);
        public static int RoundToInt(float v) => (int)Math.Round(v, MidpointRounding.ToEven);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Log(float v) => (float)Math.Log(v);
        public static float Log(float v, float b) => (float)Math.Log(v, b);
        public static float Log10(float v) => (float)Math.Log10(v);
        public static float Sin(float v) => (float)Math.Sin(v);
        public static float Cos(float v) => (float)Math.Cos(v);
        public static float Tan(float v) => (float)Math.Tan(v);
        public static float Atan2(float y, float x) => (float)Math.Atan2(y, x);
        public static float Sign(float v) => v >= 0f ? 1f : -1f;
        public static bool Approximately(float a, float b) => Math.Abs(b - a) < Math.Max(1E-06f * Math.Max(Math.Abs(a), Math.Abs(b)), Epsilon * 8f);
        public static float MoveTowards(float c, float t, float d) => Math.Abs(t - c) <= d ? t : c + Math.Sign(t - c) * d;
        public static float Repeat(float t, float length) => Clamp(t - Floor(t / length) * length, 0f, length);
        public static float PingPong(float t, float length) { t = Repeat(t, length * 2f); return length - Math.Abs(t - length); }
        public static float SmoothStep(float from, float to, float t) { t = Clamp01(t); t = -2f * t * t * t + 3f * t * t; return to * t + from * (1f - t); }
        public static float DeltaAngle(float c, float t) { float d = Repeat(t - c, 360f); if (d > 180f) d -= 360f; return d; }
        public static float SmoothDamp(float c, float t, ref float vel, float time, float maxSpeed, float dt) { float k = Clamp01(dt / Math.Max(0.0001f, time)); float n = Lerp(c, t, k); vel = dt > 0 ? (n - c) / dt : 0f; return n; }
        public static float SmoothDamp(float c, float t, ref float vel, float time) => SmoothDamp(c, t, ref vel, time, Infinity, Time.deltaTime);
    }

    public struct Color : IEquatable<Color>
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1f; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color black => new Color(0, 0, 0, 1);
        public static Color clear => new Color(0, 0, 0, 0);
        public static Color gray => new Color(0.5f, 0.5f, 0.5f, 1);
        public static Color grey => gray;
        public static Color red => new Color(1, 0, 0, 1);
        public static Color green => new Color(0, 1, 0, 1);
        public static Color blue => new Color(0, 0, 1, 1);
        public static Color yellow => new Color(1, 0.92156863f, 0.015686275f, 1);
        public static Color cyan => new Color(0, 1, 1, 1);
        public static Color magenta => new Color(1, 0, 1, 1);
        public float grayscale => 0.299f * r + 0.587f * g + 0.114f * b;
        public static Color Lerp(Color a, Color b, float t) { t = Mathf.Clamp01(t); return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t); }
        public static Color LerpUnclamped(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        public static Color HSVToRGB(float h, float s, float v)
        {
            if (s == 0f) return new Color(v, v, v, 1);
            h = (h % 1f + 1f) % 1f * 6f; int i = (int)Math.Floor(h); float f = h - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
            switch (i % 6) { case 0: return new Color(v, t, p); case 1: return new Color(q, v, p); case 2: return new Color(p, v, t); case 3: return new Color(p, q, v); case 4: return new Color(t, p, v); default: return new Color(v, p, q); }
        }
        public static Color operator *(Color a, Color b) => new Color(a.r * b.r, a.g * b.g, a.b * b.b, a.a * b.a);
        public static Color operator *(Color a, float d) => new Color(a.r * d, a.g * d, a.b * d, a.a * d);
        public static Color operator *(float d, Color a) => a * d;
        public static Color operator +(Color a, Color b) => new Color(a.r + b.r, a.g + b.g, a.b + b.b, a.a + b.a);
        public static Color operator -(Color a, Color b) => new Color(a.r - b.r, a.g - b.g, a.b - b.b, a.a - b.a);
        public static Color operator /(Color a, float d) => new Color(a.r / d, a.g / d, a.b / d, a.a / d);
        public static bool operator ==(Color a, Color b) => Math.Abs(a.r - b.r) < 1e-5f && Math.Abs(a.g - b.g) < 1e-5f && Math.Abs(a.b - b.b) < 1e-5f && Math.Abs(a.a - b.a) < 1e-5f;
        public static bool operator !=(Color a, Color b) => !(a == b);
        public static implicit operator Color(Color32 c) => new Color(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);
        public static implicit operator Vector4(Color c) => new Vector4(c.r, c.g, c.b, c.a);
        public bool Equals(Color o) => this == o;
        public override bool Equals(object o) => o is Color c && c == this;
        public override int GetHashCode() => r.GetHashCode() ^ g.GetHashCode() << 2 ^ b.GetHashCode() >> 2 ^ a.GetHashCode() >> 1;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "RGBA({0:F3}, {1:F3}, {2:F3}, {3:F3})", r, g, b, a);
    }

    public struct Color32
    {
        public byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static implicit operator Color32(Color c) => new Color32((byte)Math.Round(Mathf.Clamp01(c.r) * 255), (byte)Math.Round(Mathf.Clamp01(c.g) * 255), (byte)Math.Round(Mathf.Clamp01(c.b) * 255), (byte)Math.Round(Mathf.Clamp01(c.a) * 255));
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public Rect(Vector2 position, Vector2 size) { x = position.x; y = position.y; width = size.x; height = size.y; }
        public static Rect zero => new Rect(0, 0, 0, 0);
        public float xMin { get => x; set { float m = xMax; x = value; width = m - x; } }
        public float yMin { get => y; set { float m = yMax; y = value; height = m - y; } }
        public float xMax { get => x + width; set => width = value - x; }
        public float yMax { get => y + height; set => height = value - y; }
        public Vector2 position { get => new Vector2(x, y); set { x = value.x; y = value.y; } }
        public Vector2 size { get => new Vector2(width, height); set { width = value.x; height = value.y; } }
        public Vector2 center { get => new Vector2(x + width / 2f, y + height / 2f); set { x = value.x - width / 2f; y = value.y - height / 2f; } }
        public Vector2 min => new Vector2(xMin, yMin);
        public Vector2 max => new Vector2(xMax, yMax);
        public bool Contains(Vector2 p) => p.x >= xMin && p.x < xMax && p.y >= yMin && p.y < yMax;
        public bool Contains(Vector3 p) => Contains((Vector2)p);
        public bool Overlaps(Rect o) => o.xMax > xMin && o.xMin < xMax && o.yMax > yMin && o.yMin < yMax;
        public static bool operator ==(Rect a, Rect b) => a.x == b.x && a.y == b.y && a.width == b.width && a.height == b.height;
        public static bool operator !=(Rect a, Rect b) => !(a == b);
        public override bool Equals(object o) => o is Rect r && r == this;
        public override int GetHashCode() => x.GetHashCode() ^ y.GetHashCode() ^ width.GetHashCode() ^ height.GetHashCode();
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "(x:{0:F2}, y:{1:F2}, width:{2:F2}, height:{3:F2})", x, y, width, height);
    }

    public struct RectInt
    {
        public int x, y, width, height;
        public RectInt(int x, int y, int width, int height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public int xMin => x; public int yMin => y; public int xMax => x + width; public int yMax => y + height;
    }

    public struct Bounds
    {
        public Vector3 center, extents;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; extents = size * 0.5f; }
        public Vector3 size { get => extents * 2f; set => extents = value * 0.5f; }
        public Vector3 min { get => center - extents; set => SetMinMax(value, max); }
        public Vector3 max { get => center + extents; set => SetMinMax(min, value); }
        public void SetMinMax(Vector3 mn, Vector3 mx) { extents = (mx - mn) * 0.5f; center = mn + extents; }
        public bool Contains(Vector3 p) => p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y && p.z >= min.z && p.z <= max.z;
        public bool Intersects(Bounds o) => min.x <= o.max.x && max.x >= o.min.x && min.y <= o.max.y && max.y >= o.min.y && min.z <= o.max.z && max.z >= o.min.z;
        public void Encapsulate(Vector3 p) => SetMinMax(Vector3.Min(min, p), Vector3.Max(max, p));
        public void Encapsulate(Bounds b) { Encapsulate(b.min); Encapsulate(b.max); }
        public void Expand(float amount) { extents += new Vector3(amount, amount, amount) * 0.5f; }
    }

    public struct BoundsInt
    {
        public Vector3Int position, size;
        public BoundsInt(Vector3Int position, Vector3Int size) { this.position = position; this.size = size; }
        public BoundsInt(int x, int y, int z, int sx, int sy, int sz) { position = new Vector3Int(x, y, z); size = new Vector3Int(sx, sy, sz); }
        public int xMin => position.x; public int yMin => position.y; public int zMin => position.z;
        public int xMax => position.x + size.x; public int yMax => position.y + size.y; public int zMax => position.z + size.z;
        public Vector3Int min => position; public Vector3Int max => position + size;
        public System.Collections.Generic.IEnumerable<Vector3Int> allPositionsWithin { get { for (int z = zMin; z < zMax; z++) for (int y = yMin; y < yMax; y++) for (int x = xMin; x < xMax; x++) yield return new Vector3Int(x, y, z); } }
    }

    public struct Matrix4x4
    {
        public Vector3 scale;
        public static Matrix4x4 identity => new Matrix4x4 { scale = Vector3.one };
        public static Matrix4x4 Scale(Vector3 s) => new Matrix4x4 { scale = s };
        public static Matrix4x4 TRS(Vector3 p, Quaternion r, Vector3 s) => new Matrix4x4 { scale = s };
    }

    public static class Random
    {
        private static System.Random rng = new System.Random(12345);
        public static void InitState(int seed) { rng = new System.Random(seed); }
        public static float value => (float)rng.NextDouble();
        public static float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);
        public static int Range(int min, int max) => max <= min ? min : rng.Next(min, max);
        public static Vector2 insideUnitCircle { get { double a = rng.NextDouble() * Math.PI * 2, r = Math.Sqrt(rng.NextDouble()); return new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r)); } }
        public static Vector3 insideUnitSphere { get { Vector2 c = insideUnitCircle; return new Vector3(c.x, c.y, (float)(rng.NextDouble() * 2 - 1)); } }
        public static Quaternion rotation => Quaternion.identity;
    }
}
