using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SuperFXEngine
{
    // ==========================================
    // POLONIUM SCRIPT (.pscript) RUNTIME ENGINE
    // ==========================================
    public class PoloniumScriptEngine
    {
        public string ScriptName { get; set; } = "PlayerController.pscript";
        public string SourceCode { get; set; } =
            "// Polonium Scripting Language (.pscript)\n" +
            "fn OnInit(Mesh self) {\n" +
            "    AttachController(self, \"PlayerMovement\");\n" +
            "}\n\n" +
            "fn OnUpdate(Mesh self) {\n" +
            "    auto moveSpeed = 7.0;\n" +
            "    if (OnButtonPress(\"W\")) {\n" +
            "        self.Position.Z = self.Position.Z + 0.1;\n" +
            "    }\n" +
            "    if (OnMousePress(\"Left\")) {\n" +
            "        self.Rotation.Y = self.Rotation.Y + 0.05;\n" +
            "    }\n" +
            "}";

        private Dictionary<string, float> variables = new Dictionary<string, float>();
        private bool isControllerAttached = false;

        public void Execute(Mesh targetMesh, HashSet<Keys> activeKeys, bool mouseClicked)
        {
            if (string.IsNullOrWhiteSpace(SourceCode)) return;

            try
            {
                string[] lines = SourceCode.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var rawLine in lines)
                {
                    string line = rawLine.Trim();
                    if (line.StartsWith("//") || string.IsNullOrEmpty(line)) continue;

                    if (line.Contains("AttachController"))
                    {
                        isControllerAttached = true;
                    }

                    if (line.StartsWith("auto "))
                    {
                        string decl = line.Substring(5).Replace(";", "").Trim();
                        string[] parts = decl.Split('=');
                        if (parts.Length == 2)
                        {
                            variables[parts[0].Trim()] = float.TryParse(parts[1].Trim(), out float val) ? val : 0.0f;
                        }
                    }

                    if (line.Contains("OnButtonPress"))
                    {
                        if (line.Contains("\"W\"") && activeKeys.Contains(Keys.W)) targetMesh.Position.Z += 0.1f;
                        if (line.Contains("\"S\"") && activeKeys.Contains(Keys.S)) targetMesh.Position.Z -= 0.1f;
                        if (line.Contains("\"A\"") && activeKeys.Contains(Keys.A)) targetMesh.Position.X -= 0.1f;
                        if (line.Contains("\"D\"") && activeKeys.Contains(Keys.D)) targetMesh.Position.X += 0.1f;
                        if (line.Contains("\"Space\"") && activeKeys.Contains(Keys.Space)) targetMesh.Position.Y += 0.15f;
                    }

                    if (line.Contains("OnMousePress") && mouseClicked)
                    {
                        if (line.Contains("\"Left\""))
                        {
                            targetMesh.Rotation.Y += 0.05f;
                        }
                    }
                }

                if (isControllerAttached)
                {
                    if (activeKeys.Contains(Keys.W)) targetMesh.Position.Z += 0.05f;
                    if (activeKeys.Contains(Keys.S)) targetMesh.Position.Z -= 0.05f;
                    if (activeKeys.Contains(Keys.A)) targetMesh.Position.X -= 0.05f;
                    if (activeKeys.Contains(Keys.D)) targetMesh.Position.X += 0.05f;
                }
            }
            catch
            {
                // Suppress evaluation exceptions during runtime
            }
        }
    }

    public struct Vector3
    {
        public float X, Y, Z;
        public Vector3(float x, float y, float z) { X = x; Y = y; Z = z; }

        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vector3 operator *(Vector3 a, float k) => new Vector3(a.X * k, a.Y * k, a.Z * k);
        public static Vector3 operator /(Vector3 a, float k) => new Vector3(a.X / k, a.Y / k, a.Z / k);
        public static float Dot(Vector3 a, Vector3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

        public static Vector3 Cross(Vector3 a, Vector3 b) => new Vector3(
            a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z,
            a.X * b.Y - a.Y * b.X
        );

        public float Length() => (float)Math.Sqrt(X * X + Y * Y + Z * Z);
        public Vector3 Normalized()
        {
            float len = Length();
            return len > 0.0001f ? new Vector3(X / len, Y / len, Z / len) : new Vector3(0, 0, 0);
        }
    }

    public struct Vector2
    {
        public float X, Y;
        public Vector2(float x, float y) { X = x; Y = y; }
    }

    public struct Matrix4x4
    {
        private float[,] m_array;
        public float[,] M
        {
            get
            {
                if (m_array == null) m_array = new float[4, 4];
                return m_array;
            }
            set { m_array = value; }
        }

        public Matrix4x4() { m_array = new float[4, 4]; }

        public static Matrix4x4 Identity()
        {
            Matrix4x4 m = new Matrix4x4();
            m.M[0, 0] = 1; m.M[1, 1] = 1; m.M[2, 2] = 1; m.M[3, 3] = 1;
            return m;
        }

        public static Matrix4x4 Translation(Vector3 v)
        {
            Matrix4x4 m = Identity();
            m.M[3, 0] = v.X; m.M[3, 1] = v.Y; m.M[3, 2] = v.Z;
            return m;
        }

        public static Matrix4x4 Scale(Vector3 s)
        {
            Matrix4x4 m = Identity();
            m.M[0, 0] = s.X; m.M[1, 1] = s.Y; m.M[2, 2] = s.Z;
            return m;
        }

        public static Matrix4x4 RotationX(float angleRad)
        {
            Matrix4x4 m = Identity();
            float c = (float)Math.Cos(angleRad);
            float s = (float)Math.Sin(angleRad);
            m.M[1, 1] = c; m.M[1, 2] = s;
            m.M[2, 1] = -s; m.M[2, 2] = c;
            return m;
        }

        public static Matrix4x4 RotationY(float angleRad)
        {
            Matrix4x4 m = Identity();
            float c = (float)Math.Cos(angleRad);
            float s = (float)Math.Sin(angleRad);
            m.M[0, 0] = c; m.M[0, 2] = -s;
            m.M[2, 0] = s; m.M[2, 2] = c;
            return m;
        }

        public static Matrix4x4 RotationZ(float angleRad)
        {
            Matrix4x4 m = Identity();
            float c = (float)Math.Cos(angleRad);
            float s = (float)Math.Sin(angleRad);
            m.M[0, 0] = c; m.M[0, 1] = s;
            m.M[1, 0] = -s; m.M[1, 1] = c;
            return m;
        }

        public static Matrix4x4 Multiply(Matrix4x4 a, Matrix4x4 b)
        {
            Matrix4x4 result = new Matrix4x4();
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    result.M[r, c] = a.M[r, 0] * b.M[0, c] +
                                     a.M[r, 1] * b.M[1, c] +
                                     a.M[r, 2] * b.M[2, c] +
                                     a.M[r, 3] * b.M[3, c];
                }
            }
            return result;
        }

        public static Vector3 MultiplyVector(Matrix4x4 m, Vector3 i)
        {
            Vector3 v = new Vector3();
            v.X = i.X * m.M[0, 0] + i.Y * m.M[1, 0] + i.Z * m.M[2, 0] + m.M[3, 0];
            v.Y = i.X * m.M[0, 1] + i.Y * m.M[1, 1] + i.Z * m.M[2, 1] + m.M[3, 1];
            v.Z = i.X * m.M[0, 2] + i.Y * m.M[1, 2] + i.Z * m.M[2, 2] + m.M[3, 2];
            float w = i.X * m.M[0, 3] + i.Y * m.M[1, 3] + i.Z * m.M[2, 3] + m.M[3, 3];
            if (w != 0.0f) { v.X /= w; v.Y /= w; v.Z /= w; }
            return v;
        }

        public static Matrix4x4 Perspective(float fovRad, float aspect, float near, float far)
        {
            Matrix4x4 m = new Matrix4x4();
            float f = 1.0f / (float)Math.Tan(fovRad / 2.0f);
            m.M[0, 0] = f / aspect;
            m.M[1, 1] = f;
            m.M[2, 2] = (far + near) / (near - far);
            m.M[2, 3] = -1.0f;
            m.M[3, 2] = (2.0f * far * near) / (near - far);
            return m;
        }

        public static Matrix4x4 LookAt(Vector3 eye, Vector3 target, Vector3 up)
        {
            Vector3 zAxis = (eye - target).Normalized();
            Vector3 xAxis = Vector3.Cross(up, zAxis).Normalized();
            Vector3 yAxis = Vector3.Cross(zAxis, xAxis);

            Matrix4x4 m = Identity();
            m.M[0, 0] = xAxis.X; m.M[1, 0] = xAxis.Y; m.M[2, 0] = xAxis.Z; m.M[3, 0] = -Vector3.Dot(xAxis, eye);
            m.M[0, 1] = yAxis.X; m.M[1, 1] = yAxis.Y; m.M[2, 1] = yAxis.Z; m.M[3, 1] = -Vector3.Dot(yAxis, eye);
            m.M[0, 2] = zAxis.X; m.M[1, 2] = zAxis.Y; m.M[2, 2] = zAxis.Z; m.M[3, 2] = -Vector3.Dot(zAxis, eye);
            return m;
        }
    }

    public class Camera
    {
        public Vector3 Position = new Vector3(0, 2, -10);
        public float Pitch = 0.0f;
        public float Yaw = 0.0f;

        public Vector3 Forward => new Vector3(
            (float)(Math.Sin(Yaw) * Math.Cos(Pitch)),
            (float)Math.Sin(Pitch),
            (float)(Math.Cos(Yaw) * Math.Cos(Pitch))
        ).Normalized();

        public Vector3 Right => Vector3.Cross(Forward, new Vector3(0, 1, 0)).Normalized();
        public Vector3 Up => Vector3.Cross(Right, Forward).Normalized();

        public Matrix4x4 GetViewMatrix() => Matrix4x4.LookAt(Position, Position + Forward, new Vector3(0, 1, 0));
    }

    public enum CameraMode
    {
        Free,
        Static,
        StaticFollow,
        Railed,
        RailFollow,
        FirstPerson,
        ThirdPerson
    }

    public class CameraObject : Mesh
    {
        public CameraMode Mode = CameraMode.Free;
        public Mesh FollowTarget = null;
        public List<Vector3> RailPoints = new List<Vector3>();
        public float RailProgress = 0.0f;
        public Vector3 StaticTarget = new Vector3(0, 0, 0);

        public CameraObject()
        {
            Name = "Camera_Obj";
            RailPoints.Add(new Vector3(-5, 3, -10));
            RailPoints.Add(new Vector3(0, 5, 0));
            RailPoints.Add(new Vector3(5, 3, 10));
        }

        public Camera ComputeViewCamera(Mesh player)
        {
            Camera cam = new Camera();
            switch (Mode)
            {
                case CameraMode.Free:
                    cam.Position = Position;
                    cam.Yaw = Rotation.Y;
                    cam.Pitch = Rotation.X;
                    break;

                case CameraMode.Static:
                    cam.Position = Position;
                    cam.Yaw = Rotation.Y;
                    cam.Pitch = Rotation.X;
                    break;

                case CameraMode.StaticFollow:
                    cam.Position = Position;
                    if (player != null)
                    {
                        Vector3 dir = (player.Position - Position).Normalized();
                        cam.Yaw = (float)Math.Atan2(dir.X, dir.Z);
                        cam.Pitch = (float)Math.Asin(dir.Y);
                    }
                    break;

                case CameraMode.Railed:
                    if (RailPoints.Count >= 2)
                    {
                        RailProgress = (RailProgress + 0.005f) % 1.0f;
                        cam.Position = EvaluateRail(RailProgress);
                        cam.Yaw = Rotation.Y;
                    }
                    break;

                case CameraMode.RailFollow:
                    if (RailPoints.Count >= 2)
                    {
                        RailProgress = (RailProgress + 0.005f) % 1.0f;
                        cam.Position = EvaluateRail(RailProgress);
                        if (player != null)
                        {
                            Vector3 dir = (player.Position - cam.Position).Normalized();
                            cam.Yaw = (float)Math.Atan2(dir.X, dir.Z);
                            cam.Pitch = (float)Math.Asin(dir.Y);
                        }
                    }
                    break;

                case CameraMode.FirstPerson:
                    if (player != null)
                    {
                        cam.Position = player.Position + new Vector3(0, 0.5f, 0);
                        cam.Yaw = player.Rotation.Y;
                        cam.Pitch = player.Rotation.X;
                    }
                    break;

                case CameraMode.ThirdPerson:
                    if (player != null)
                    {
                        Matrix4x4 matRotY = Matrix4x4.RotationY(player.Rotation.Y);
                        Vector3 offset = Matrix4x4.MultiplyVector(matRotY, new Vector3(0, 2.0f, -6.0f));
                        cam.Position = player.Position + offset;
                        Vector3 dir = (player.Position - cam.Position).Normalized();
                        cam.Yaw = (float)Math.Atan2(dir.X, dir.Z);
                        cam.Pitch = 0.2f;
                    }
                    break;
            }
            return cam;
        }

        private Vector3 EvaluateRail(float t)
        {
            if (RailPoints == null || RailPoints.Count == 0) return Position;
            if (RailPoints.Count == 1) return RailPoints[0];

            float scaled = t * (RailPoints.Count - 1);
            int idx = (int)scaled;
            if (idx >= RailPoints.Count - 1) return RailPoints[RailPoints.Count - 1];
            float frac = scaled - idx;

            Vector3 p1 = RailPoints[idx];
            Vector3 p2 = RailPoints[idx + 1];
            return new Vector3(
                p1.X + (p2.X - p1.X) * frac,
                p1.Y + (p2.Y - p1.Y) * frac,
                p1.Z + (p2.Z - p1.Z) * frac
            );
        }

        public new static CameraObject CreateCameraGizmoMesh()
        {
            CameraObject co = new CameraObject { Name = "Camera_Entity" };
            co.CollisionMin = new Vector3(-0.5f, -0.5f, -0.5f);
            co.CollisionMax = new Vector3(0.5f, 0.5f, 0.5f);

            Color col = Color.Yellow;
            Vector3[] v = {
                new Vector3(-0.4f, -0.4f, -0.6f), new Vector3(-0.4f,  0.4f, -0.6f), new Vector3( 0.4f,  0.4f, -0.6f), new Vector3( 0.4f, -0.4f, -0.6f),
                new Vector3(0, 0, 0.6f)
            };
            Vector2 uv = new Vector2(0, 0);

            co.Triangles.Add(new Triangle(v[0], v[1], v[4], col, uv, uv, uv));
            co.Triangles.Add(new Triangle(v[1], v[2], v[4], col, uv, uv, uv));
            co.Triangles.Add(new Triangle(v[2], v[3], v[4], col, uv, uv, uv));
            co.Triangles.Add(new Triangle(v[3], v[0], v[4], col, uv, uv, uv));
            co.Triangles.Add(new Triangle(v[0], v[3], v[2], col, uv, uv, uv));
            co.Triangles.Add(new Triangle(v[0], v[2], v[1], col, uv, uv, uv));
            return co;
        }
    }

    public class Keyframe
    {
        public int FrameIndex;
        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 Scale;

        public Keyframe(int frameIndex, Vector3 pos, Vector3 rot, Vector3 scale)
        {
            FrameIndex = frameIndex; Position = pos; Rotation = rot; Scale = scale;
        }
    }

    public class AnimationClip
    {
        public string Name = "DefaultAnim";
        public int TotalFrames = 30;
        public List<Keyframe> Keyframes = new List<Keyframe>();

        public void SaveToFile(string filePath)
        {
            using (BinaryWriter writer = new BinaryWriter(File.Open(filePath, FileMode.Create)))
            {
                writer.Write("SFXANIM");
                writer.Write(Name);
                writer.Write(TotalFrames);
                writer.Write(Keyframes.Count);
                foreach (var kf in Keyframes)
                {
                    writer.Write(kf.FrameIndex);
                    writer.Write(kf.Position.X); writer.Write(kf.Position.Y); writer.Write(kf.Position.Z);
                    writer.Write(kf.Rotation.X); writer.Write(kf.Rotation.Y); writer.Write(kf.Rotation.Z);
                    writer.Write(kf.Scale.X); writer.Write(kf.Scale.Y); writer.Write(kf.Scale.Z);
                }
            }
        }

        public static AnimationClip LoadFromFile(string filePath)
        {
            AnimationClip clip = new AnimationClip();
            using (BinaryReader reader = new BinaryReader(File.Open(filePath, FileMode.Open)))
            {
                string header = reader.ReadString();
                if (header != "SFXANIM") throw new Exception("Invalid file format.");
                clip.Name = reader.ReadString();
                clip.TotalFrames = reader.ReadInt32();
                int count = reader.ReadInt32();
                clip.Keyframes.Clear();
                for (int i = 0; i < count; i++)
                {
                    int frameIndex = reader.ReadInt32();
                    Vector3 pos = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Vector3 rot = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    Vector3 scl = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    clip.Keyframes.Add(new Keyframe(frameIndex, pos, rot, scl));
                }
            }
            return clip;
        }

        public void Evaluate(int currentFrame, out Vector3 pos, out Vector3 rot, out Vector3 scale)
        {
            if (Keyframes == null || Keyframes.Count == 0)
            {
                pos = new Vector3(0, 0, 0); rot = new Vector3(0, 0, 0); scale = new Vector3(1, 1, 1);
                return;
            }
            if (Keyframes.Count == 1 || currentFrame <= Keyframes[0].FrameIndex)
            {
                pos = Keyframes[0].Position; rot = Keyframes[0].Rotation; scale = Keyframes[0].Scale;
                return;
            }
            if (currentFrame >= Keyframes[Keyframes.Count - 1].FrameIndex)
            {
                var last = Keyframes[Keyframes.Count - 1];
                pos = last.Position; rot = last.Rotation; scale = last.Scale;
                return;
            }

            Keyframe k1 = Keyframes[0], k2 = Keyframes[1];
            for (int i = 0; i < Keyframes.Count - 1; i++)
            {
                if (currentFrame >= Keyframes[i].FrameIndex && currentFrame <= Keyframes[i + 1].FrameIndex)
                {
                    k1 = Keyframes[i]; k2 = Keyframes[i + 1]; break;
                }
            }

            float span = k2.FrameIndex - k1.FrameIndex;
            float t = span > 0 ? (currentFrame - k1.FrameIndex) / span : 0.0f;
            t = Math.Max(0.0f, Math.Min(1.0f, t));

            pos = new Vector3(k1.Position.X + (k2.Position.X - k1.Position.X) * t,
                              k1.Position.Y + (k2.Position.Y - k1.Position.Y) * t,
                              k1.Position.Z + (k2.Position.Z - k1.Position.Z) * t);
            rot = new Vector3(k1.Rotation.X + (k2.Rotation.X - k1.Rotation.X) * t,
                              k1.Rotation.Y + (k2.Rotation.Y - k1.Rotation.Y) * t,
                              k1.Rotation.Z + (k2.Rotation.Z - k1.Rotation.Z) * t);
            scale = new Vector3(k1.Scale.X + (k2.Scale.X - k1.Scale.X) * t,
                                k1.Scale.Y + (k2.Scale.Y - k1.Scale.Y) * t,
                                k1.Scale.Z + (k2.Scale.Z - k1.Scale.Z) * t);
        }
    }

    public class Triangle
    {
        public Vector3[] P = new Vector3[3];
        public Vector2[] UV = new Vector2[3];
        public Color BaseColor = Color.White;
        public Bitmap TextureImage = null;
        public float Shade = 1.0f;
        public bool IsSelected = false;

        public Triangle(Vector3 p1, Vector3 p2, Vector3 p3, Color color, Vector2 uv1, Vector2 uv2, Vector2 uv3)
        {
            P[0] = p1; P[1] = p2; P[2] = p3;
            UV[0] = uv1; UV[1] = uv2; UV[2] = uv3;
            BaseColor = color;
        }

        public Triangle Clone()
        {
            Triangle t = new Triangle(P[0], P[1], P[2], BaseColor, UV[0], UV[1], UV[2]);
            t.TextureImage = TextureImage; t.Shade = Shade;
            return t;
        }
    }

    public class Mesh
    {
        public string Name = "MeshObject";
        public Mesh Parent = null;
        public List<Triangle> Triangles = new List<Triangle>();
        public Vector3 Position = new Vector3(0, 0, 0);
        public Vector3 Rotation = new Vector3(0, 0, 0);
        public Vector3 Scale = new Vector3(1, 1, 1);
        public bool IsSelected = false;

        public Vector3 CollisionMin = new Vector3(-0.5f, -0.5f, -0.5f);
        public Vector3 CollisionMax = new Vector3(0.5f, 0.5f, 0.5f);
        public bool ShowCollisionBox = false;

        public PoloniumScriptEngine AttachedScript = null;
        public bool ScriptEnabled = false;

        public Matrix4x4 GetLocalMatrix()
        {
            Matrix4x4 matScale = Matrix4x4.Scale(Scale);
            Matrix4x4 matRotX = Matrix4x4.RotationX(Rotation.X);
            Matrix4x4 matRotY = Matrix4x4.RotationY(Rotation.Y);
            Matrix4x4 matRotZ = Matrix4x4.RotationZ(Rotation.Z);
            Matrix4x4 matRot = Matrix4x4.Multiply(Matrix4x4.Multiply(matRotX, matRotY), matRotZ);
            Matrix4x4 matTrans = Matrix4x4.Translation(Position);

            return Matrix4x4.Multiply(Matrix4x4.Multiply(matScale, matRot), matTrans);
        }

        public Matrix4x4 GetWorldMatrix()
        {
            Matrix4x4 localMatrix = GetLocalMatrix();
            if (Parent != null && Parent != this)
            {
                return Matrix4x4.Multiply(localMatrix, Parent.GetWorldMatrix());
            }
            return localMatrix;
        }

        public static Mesh CreateCube(float size, Color color)
        {
            Mesh m = new Mesh { Name = "Cube_Mesh" };
            float h = size / 2.0f;
            m.CollisionMin = new Vector3(-h, -h, -h);
            m.CollisionMax = new Vector3(h, h, h);

            Vector3[] v = {
                new Vector3(-h, -h, -h), new Vector3(-h,  h, -h), new Vector3( h,  h, -h), new Vector3( h, -h, -h),
                new Vector3(-h, -h,  h), new Vector3(-h,  h,  h), new Vector3( h,  h,  h), new Vector3( h, -h,  h)
            };

            Vector2 uv0 = new Vector2(0, 1), uv1 = new Vector2(0, 0), uv2 = new Vector2(1, 0), uv3 = new Vector2(1, 1);

            void AddQuad(int i0, int i1, int i2, int i3)
            {
                m.Triangles.Add(new Triangle(v[i0], v[i1], v[i2], color, uv0, uv1, uv2));
                m.Triangles.Add(new Triangle(v[i0], v[i2], v[i3], color, uv0, uv2, uv3));
            }

            AddQuad(0, 1, 2, 3);
            AddQuad(7, 6, 5, 4);
            AddQuad(4, 5, 1, 0);
            AddQuad(3, 2, 6, 7);
            AddQuad(1, 5, 6, 2);
            AddQuad(4, 0, 3, 7);
            return m;
        }

        public static Mesh CreatePyramid(float size, Color color)
        {
            Mesh m = new Mesh { Name = "Pyramid_Mesh" };
            float h = size / 2.0f;
            m.CollisionMin = new Vector3(-h, -h, -h);
            m.CollisionMax = new Vector3(h, h, h);

            Vector3 top = new Vector3(0, h, 0);
            Vector3[] b = {
                new Vector3(-h, -h, -h), new Vector3( h, -h, -h),
                new Vector3( h, -h,  h), new Vector3(-h, -h,  h)
            };

            Vector2 uvTop = new Vector2(0.5f, 0);
            Vector2 uvBl = new Vector2(0, 1), uvBr = new Vector2(1, 1);

            m.Triangles.Add(new Triangle(b[0], top, b[1], color, uvBl, uvTop, uvBr));
            m.Triangles.Add(new Triangle(b[1], top, b[2], color, uvBl, uvTop, uvBr));
            m.Triangles.Add(new Triangle(b[2], top, b[3], color, uvBl, uvTop, uvBr));
            m.Triangles.Add(new Triangle(b[3], top, b[0], color, uvBl, uvTop, uvBr));
            m.Triangles.Add(new Triangle(b[0], b[2], b[1], color, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0)));
            m.Triangles.Add(new Triangle(b[0], b[3], b[2], color, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1)));
            return m;
        }

        public static Mesh CreateGroundGrid(float size, int gridCount)
        {
            Mesh m = new Mesh { Name = "Terrain_Grid" };
            float step = size / gridCount;
            float start = -size / 2.0f;

            for (int r = 0; r < gridCount; r++)
            {
                for (int c = 0; c < gridCount; c++)
                {
                    float x1 = start + c * step;
                    float z1 = start + r * step;
                    float x2 = x1 + step;
                    float z2 = z1 + step;

                    Color tileColor = ((r + c) % 2 == 0) ? Color.FromArgb(255, 30, 80, 40) : Color.FromArgb(255, 20, 50, 30);
                    Vector3 p1 = new Vector3(x1, 0, z1);
                    Vector3 p2 = new Vector3(x1, 0, z2);
                    Vector3 p3 = new Vector3(x2, 0, z2);
                    Vector3 p4 = new Vector3(x2, 0, z1);

                    m.Triangles.Add(new Triangle(p1, p2, p3, tileColor, new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1)));
                    m.Triangles.Add(new Triangle(p1, p3, p4, tileColor, new Vector2(0, 0), new Vector2(1, 1), new Vector2(1, 0)));
                }
            }
            return m;
        }

        public static Mesh CreateStarFoxFighter()
        {
            Mesh m = new Mesh { Name = "Arwing_Fighter" };
            m.CollisionMin = new Vector3(-2.2f, -0.5f, -1.2f);
            m.CollisionMax = new Vector3(2.2f, 1.2f, 2.0f);

            Vector3 nose = new Vector3(0, 0, 2.0f);
            Vector3 cockTop = new Vector3(0, 0.5f, 0.2f);
            Vector3 leftWing = new Vector3(-2.2f, 0, -1.0f);
            Vector3 rightWing = new Vector3(2.2f, 0, -1.0f);
            Vector3 tailTop = new Vector3(0, 1.2f, -1.2f);
            Vector3 tailBase = new Vector3(0, 0, -1.2f);

            Vector2 u0 = new Vector2(0, 0), u1 = new Vector2(0.5f, 1), u2 = new Vector2(1, 0);

            m.Triangles.Add(new Triangle(nose, cockTop, leftWing, Color.DodgerBlue, u0, u1, u2));
            m.Triangles.Add(new Triangle(nose, rightWing, cockTop, Color.DodgerBlue, u0, u2, u1));
            m.Triangles.Add(new Triangle(cockTop, tailTop, leftWing, Color.Yellow, u0, u1, u2));
            m.Triangles.Add(new Triangle(cockTop, rightWing, tailTop, Color.Yellow, u0, u2, u1));
            m.Triangles.Add(new Triangle(tailBase, leftWing, tailTop, Color.Red, u0, u1, u2));
            m.Triangles.Add(new Triangle(tailBase, tailTop, rightWing, Color.Red, u0, u2, u1));
            m.Triangles.Add(new Triangle(nose, leftWing, tailBase, Color.LightGray, u0, u1, u2));
            m.Triangles.Add(new Triangle(nose, tailBase, rightWing, Color.LightGray, u0, u2, u1));
            return m;
        }
    }

    public enum GizmoAxis { None, X, Y, Z }
    public enum GizmoSpace { Global, Local }
    public enum ViewTab { Layout, Animation, CodeEditor }

    public class SoftwareRenderer
    {
        public readonly int Width;
        public readonly int Height;
        private int[] pixelBuffer;
        private float[] depthBuffer;
        public Matrix4x4 ProjectionMatrix;
        private Vector3 lightDirection = new Vector3(0.5f, 1.0f, -0.7f).Normalized();
        private Bitmap renderBitmap;
        public bool WireframeMode = false;

        public SoftwareRenderer(int w, int h)
        {
            Width = w; Height = h;
            pixelBuffer = new int[w * h];
            depthBuffer = new float[w * h];
            renderBitmap = new Bitmap(w, h, PixelFormat.Format32bppRgb);
            float fovRad = (float)(60.0f * Math.PI / 180.0f);
            ProjectionMatrix = Matrix4x4.Perspective(fovRad, (float)w / h, 0.1f, 100.0f);
        }

        public void Clear(Color clearColor)
        {
            int argb = clearColor.ToArgb();
            Array.Fill(pixelBuffer, argb);
            Array.Fill(depthBuffer, float.MaxValue);
        }

        public void RenderMesh(Mesh mesh, Camera camera, Color? overrideColor = null, float shadeMultiplier = 1.0f)
        {
            if (mesh == null || camera == null || mesh.Triangles == null) return;

            Matrix4x4 matWorld = mesh.GetWorldMatrix();
            Matrix4x4 matView = camera.GetViewMatrix();

            foreach (var tri in mesh.Triangles)
            {
                if (tri == null || tri.P == null || tri.P.Length < 3) continue;

                Vector3[] worldP = new Vector3[3];
                Vector3[] viewP = new Vector3[3];

                for (int i = 0; i < 3; i++)
                {
                    worldP[i] = Matrix4x4.MultiplyVector(matWorld, tri.P[i]);
                    viewP[i] = Matrix4x4.MultiplyVector(matView, worldP[i]);
                }

                Vector3 normal = Vector3.Cross(worldP[1] - worldP[0], worldP[2] - worldP[0]).Normalized();
                Vector3 viewDir = (worldP[0] - camera.Position).Normalized();

                if (Vector3.Dot(normal, viewDir) < 0.0f)
                {
                    float lightIntensity = Math.Max(0.25f, Vector3.Dot(normal, lightDirection));
                    Color baseCol = overrideColor ?? tri.BaseColor;

                    Triangle triProjected = new Triangle(new Vector3(), new Vector3(), new Vector3(), baseCol, tri.UV[0], tri.UV[1], tri.UV[2]);
                    triProjected.TextureImage = overrideColor == null ? tri.TextureImage : null;
                    triProjected.Shade = lightIntensity * shadeMultiplier;
                    triProjected.IsSelected = (overrideColor == null) && (tri.IsSelected || mesh.IsSelected);

                    if (viewP[0].Z > -0.1f || viewP[1].Z > -0.1f || viewP[2].Z > -0.1f) continue;

                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 proj = Matrix4x4.MultiplyVector(ProjectionMatrix, viewP[i]);
                        proj.X = (proj.X + 1.0f) * 0.5f * Width;
                        proj.Y = (1.0f - proj.Y) * 0.5f * Height;
                        proj.Z = -viewP[i].Z;
                        triProjected.P[i] = proj;
                    }

                    if (WireframeMode || overrideColor != null) DrawWireTriangle(triProjected, triProjected.IsSelected ? Color.Yellow.ToArgb() : baseCol.ToArgb());
                    else
                    {
                        FillPerspectiveCorrectTexturedTriangle(triProjected);
                        if (triProjected.IsSelected) DrawWireTriangle(triProjected, Color.Cyan.ToArgb());
                    }
                }
            }

            if (overrideColor == null && mesh.ShowCollisionBox) RenderCollisionBox(mesh, camera);
        }

        public void RenderGizmo(Mesh selectedMesh, Camera camera, EditorTool currentTool, GizmoAxis activeAxis, GizmoAxis hoveredGizmoAxis, GizmoSpace space)
        {
            if (selectedMesh == null || camera == null) return;
            Matrix4x4 matView = camera.GetViewMatrix();
            Matrix4x4 matWorld = selectedMesh.GetWorldMatrix();

            Vector3 origin = new Vector3(matWorld.M[3, 0], matWorld.M[3, 1], matWorld.M[3, 2]);
            float handleLength = 1.8f;

            Vector3 xDir = new Vector3(1, 0, 0);
            Vector3 yDir = new Vector3(0, 1, 0);
            Vector3 zDir = new Vector3(0, 0, 1);

            Matrix4x4 matRotX = Matrix4x4.RotationX(selectedMesh.Rotation.X);
            Matrix4x4 matRotY = Matrix4x4.RotationY(selectedMesh.Rotation.Y);
            Matrix4x4 matRotZ = Matrix4x4.RotationZ(selectedMesh.Rotation.Z);
            Matrix4x4 localRot = Matrix4x4.Multiply(Matrix4x4.Multiply(matRotX, matRotY), matRotZ);

            Matrix4x4 orientationMatrix = localRot;
            if (selectedMesh.Parent != null)
            {
                Matrix4x4 parentWorldRot = selectedMesh.Parent.GetWorldMatrix();
                parentWorldRot.M[3, 0] = 0; parentWorldRot.M[3, 1] = 0; parentWorldRot.M[3, 2] = 0;
                orientationMatrix = Matrix4x4.Multiply(localRot, parentWorldRot);
            }

            if (space == GizmoSpace.Local || selectedMesh.Parent != null)
            {
                xDir = Matrix4x4.MultiplyVector(orientationMatrix, xDir).Normalized();
                yDir = Matrix4x4.MultiplyVector(orientationMatrix, yDir).Normalized();
                zDir = Matrix4x4.MultiplyVector(orientationMatrix, zDir).Normalized();
            }

            Vector3 ProjectPoint(Vector3 p)
            {
                Vector3 v = Matrix4x4.MultiplyVector(matView, p);
                if (v.Z > -0.1f) return new Vector3(-1, -1, 1);
                Vector3 proj = Matrix4x4.MultiplyVector(ProjectionMatrix, v);
                proj.X = (proj.X + 1.0f) * 0.5f * Width;
                proj.Y = (1.0f - proj.Y) * 0.5f * Height;
                return proj;
            }

            Vector3 pOrig = ProjectPoint(origin);
            if (pOrig.X < 0) return;

            int colX = (activeAxis == GizmoAxis.X) ? Color.White.ToArgb() : (hoveredGizmoAxis == GizmoAxis.X ? Color.Yellow.ToArgb() : Color.Red.ToArgb());
            int colY = (activeAxis == GizmoAxis.Y) ? Color.White.ToArgb() : (hoveredGizmoAxis == GizmoAxis.Y ? Color.Yellow.ToArgb() : Color.Lime.ToArgb());
            int colZ = (activeAxis == GizmoAxis.Z) ? Color.White.ToArgb() : (hoveredGizmoAxis == GizmoAxis.Z ? Color.Yellow.ToArgb() : Color.Blue.ToArgb());

            if (currentTool == EditorTool.Move || currentTool == EditorTool.Scale)
            {
                Vector3 xEnd = origin + xDir * handleLength;
                Vector3 yEnd = origin + yDir * handleLength;
                Vector3 zEnd = origin + zDir * handleLength;

                Vector3 pX = ProjectPoint(xEnd);
                Vector3 pY = ProjectPoint(yEnd);
                Vector3 pZ = ProjectPoint(zEnd);

                DrawLine((int)pOrig.X, (int)pOrig.Y, (int)pX.X, (int)pX.Y, colX);
                DrawLine((int)pOrig.X, (int)pOrig.Y, (int)pY.X, (int)pY.Y, colY);
                DrawLine((int)pOrig.X, (int)pOrig.Y, (int)pZ.X, (int)pZ.Y, colZ);

                DrawFilledCircle((int)pX.X, (int)pX.Y, 5, colX);
                DrawFilledCircle((int)pY.X, (int)pY.Y, 5, colY);
                DrawFilledCircle((int)pZ.X, (int)pZ.Y, 5, colZ);
            }
            else if (currentTool == EditorTool.Rotate)
            {
                int segments = 24;
                float radius = 1.3f;

                for (int i = 0; i < segments; i++)
                {
                    float a1 = (float)(i * 2 * Math.PI / segments);
                    float a2 = (float)((i + 1) * 2 * Math.PI / segments);

                    Vector3 rx1 = origin + yDir * (float)Math.Cos(a1) * radius + zDir * (float)Math.Sin(a1) * radius;
                    Vector3 rx2 = origin + yDir * (float)Math.Cos(a2) * radius + zDir * (float)Math.Sin(a2) * radius;
                    Vector3 prx1 = ProjectPoint(rx1), prx2 = ProjectPoint(rx2);
                    if (prx1.X >= 0 && prx2.X >= 0) DrawLine((int)prx1.X, (int)prx1.Y, (int)prx2.X, (int)prx2.Y, colX);

                    Vector3 ry1 = origin + xDir * (float)Math.Cos(a1) * radius + zDir * (float)Math.Sin(a1) * radius;
                    Vector3 ry2 = origin + xDir * (float)Math.Cos(a2) * radius + zDir * (float)Math.Sin(a2) * radius;
                    Vector3 pry1 = ProjectPoint(ry1), pry2 = ProjectPoint(ry2);
                    if (pry1.X >= 0 && pry2.X >= 0) DrawLine((int)pry1.X, (int)pry1.Y, (int)pry2.X, (int)pry2.Y, colY);

                    Vector3 rz1 = origin + xDir * (float)Math.Cos(a1) * radius + yDir * (float)Math.Sin(a1) * radius;
                    Vector3 rz2 = origin + xDir * (float)Math.Cos(a2) * radius + yDir * (float)Math.Sin(a2) * radius;
                    Vector3 prz1 = ProjectPoint(rz1), prz2 = ProjectPoint(rz2);
                    if (prz1.X >= 0 && prz2.X >= 0) DrawLine((int)prz1.X, (int)prz1.Y, (int)prz2.X, (int)prz2.Y, colZ);
                }

                Vector3 pTipX = ProjectPoint(origin + yDir * radius);
                Vector3 pTipY = ProjectPoint(origin + zDir * radius);
                Vector3 pTipZ = ProjectPoint(origin + xDir * radius);

                if (pTipX.X >= 0) DrawFilledCircle((int)pTipX.X, (int)pTipX.Y, 5, colX);
                if (pTipY.X >= 0) DrawFilledCircle((int)pTipY.X, (int)pTipY.Y, 5, colY);
                if (pTipZ.X >= 0) DrawFilledCircle((int)pTipZ.X, (int)pTipZ.Y, 5, colZ);
            }
        }

        private void DrawFilledCircle(int cx, int cy, int radius, int argb)
        {
            for (int y = -radius; y <= radius; y++)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        DrawPixel(cx + x, cy + y, 0.0f, argb);
                    }
                }
            }
        }

        private void RenderCollisionBox(Mesh mesh, Camera camera)
        {
            if (mesh == null || camera == null) return;
            Matrix4x4 matWorld = mesh.GetWorldMatrix();
            Matrix4x4 matView = camera.GetViewMatrix();
            Vector3 min = mesh.CollisionMin, max = mesh.CollisionMax;

            Vector3[] corners = {
                new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, min.Y, max.Z)
            };

            Vector3[] projCorners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 w = Matrix4x4.MultiplyVector(matWorld, corners[i]);
                Vector3 v = Matrix4x4.MultiplyVector(matView, w);
                if (v.Z > -0.1f) return;
                Vector3 p = Matrix4x4.MultiplyVector(ProjectionMatrix, v);
                p.X = (p.X + 1.0f) * 0.5f * Width;
                p.Y = (1.0f - p.Y) * 0.5f * Height;
                projCorners[i] = p;
            }

            int col = Color.Magenta.ToArgb();
            int[][] edges = {
                new[] {0,1}, new[] {1,2}, new[] {2,3}, new[] {3,0},
                new[] {4,5}, new[] {5,6}, new[] {6,7}, new[] {7,4},
                new[] {0,4}, new[] {1,5}, new[] {2,6}, new[] {3,7}
            };
            foreach (var e in edges) DrawLine((int)projCorners[e[0]].X, (int)projCorners[e[0]].Y, (int)projCorners[e[1]].X, (int)projCorners[e[1]].Y, col);
        }

        private void FillPerspectiveCorrectTexturedTriangle(Triangle tri)
        {
            if (tri == null || tri.P == null || tri.P.Length < 3) return;
            Vector3 p0 = tri.P[0], p1 = tri.P[1], p2 = tri.P[2];
            int minX = (int)Math.Max(0, Math.Min(p0.X, Math.Min(p1.X, p2.X)));
            int maxX = (int)Math.Min(Width - 1, Math.Max(p0.X, Math.Max(p1.X, p2.X)));
            int minY = (int)Math.Max(0, Math.Min(p0.Y, Math.Min(p1.Y, p2.Y)));
            int maxY = (int)Math.Min(Height - 1, Math.Max(p0.Y, Math.Max(p1.Y, p2.Y)));

            byte baseR = (byte)(tri.BaseColor.R * tri.Shade);
            byte baseG = (byte)(tri.BaseColor.G * tri.Shade);
            byte baseB = (byte)(tri.BaseColor.B * tri.Shade);
            int flatColor = Color.FromArgb(255, baseR, baseG, baseB).ToArgb();
            Bitmap tex = tri.TextureImage;

            float iz0 = p0.Z != 0 ? 1.0f / p0.Z : 1.0f;
            float iz1 = p1.Z != 0 ? 1.0f / p1.Z : 1.0f;
            float iz2 = p2.Z != 0 ? 1.0f / p2.Z : 1.0f;

            float u0z = tri.UV[0].X * iz0, v0z = tri.UV[0].Y * iz0;
            float u1z = tri.UV[1].X * iz1, v1z = tri.UV[1].Y * iz1;
            float u2z = tri.UV[2].X * iz2, v2z = tri.UV[2].Y * iz2;

            float area = EdgeFunction(p0, p1, p2.X, p2.Y);
            if (area == 0) return;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    float w0 = EdgeFunction(p1, p2, x, y);
                    float w1 = EdgeFunction(p2, p0, x, y);
                    float w2 = EdgeFunction(p0, p1, x, y);

                    if (w0 >= 0 && w1 >= 0 && w2 >= 0)
                    {
                        w0 /= area; w1 /= area; w2 /= area;
                        float z = w0 * p0.Z + w1 * p1.Z + w2 * p2.Z;

                        int pixelColor = flatColor;
                        if (tex != null)
                        {
                            float interp_iz = w0 * iz0 + w1 * iz1 + w2 * iz2;
                            float interp_uz = w0 * u0z + w1 * u1z + w2 * u2z;
                            float interp_vz = w0 * v0z + w1 * v1z + w2 * v2z;

                            float u = interp_uz / interp_iz;
                            float v = interp_vz / interp_iz;

                            u = u - (float)Math.Floor(u);
                            v = v - (float)Math.Floor(v);

                            int texX = Math.Clamp((int)(u * (tex.Width - 1)), 0, tex.Width - 1);
                            int texY = Math.Clamp((int)(v * (tex.Height - 1)), 0, tex.Height - 1);

                            Color c = tex.GetPixel(texX, texY);
                            pixelColor = Color.FromArgb(255, (byte)(c.R * tri.Shade), (byte)(c.G * tri.Shade), (byte)(c.B * tri.Shade)).ToArgb();
                        }

                        DrawPixel(x, y, z, pixelColor);
                    }
                }
            }
        }

        public (Mesh mesh, Triangle face) PickFace3D(int screenX, int screenY, List<Mesh> meshes, Camera camera)
        {
            if (meshes == null || camera == null) return (null, null);
            float ndcX = (2.0f * screenX) / Width - 1.0f;
            float ndcY = 1.0f - (2.0f * screenY) / Height;
            float fovRad = (float)(60.0f * Math.PI / 180.0f);
            float aspect = (float)Width / Height;
            float tanFov = (float)Math.Tan(fovRad / 2.0f);

            Vector3 rayView = new Vector3(ndcX * aspect * tanFov, ndcY * tanFov, -1.0f).Normalized();
            Vector3 rayOrigin = camera.Position;
            Vector3 rayDir = (camera.Right * rayView.X + camera.Up * rayView.Y + camera.Forward * (-rayView.Z)).Normalized();

            Mesh hitMesh = null;
            Triangle hitFace = null;
            float closestT = float.MaxValue;

            foreach (var mesh in meshes)
            {
                if (mesh == null || mesh.Triangles == null) continue;
                Matrix4x4 matWorld = mesh.GetWorldMatrix();
                foreach (var tri in mesh.Triangles)
                {
                    if (tri == null || tri.P == null || tri.P.Length < 3) continue;
                    Vector3 v0 = Matrix4x4.MultiplyVector(matWorld, tri.P[0]);
                    Vector3 v1 = Matrix4x4.MultiplyVector(matWorld, tri.P[1]);
                    Vector3 v2 = Matrix4x4.MultiplyVector(matWorld, tri.P[2]);

                    if (RayTriangleIntersect(rayOrigin, rayDir, v0, v1, v2, out float t) && t > 0 && t < closestT)
                    {
                        closestT = t; hitMesh = mesh; hitFace = tri;
                    }
                }
            }
            return (hitMesh, hitFace);
        }

        private bool RayTriangleIntersect(Vector3 orig, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2, out float t)
        {
            t = 0;
            Vector3 edge1 = v1 - v0, edge2 = v2 - v0;
            Vector3 h = Vector3.Cross(dir, edge2);
            float a = Vector3.Dot(edge1, h);
            if (a > -0.00001f && a < 0.00001f) return false;
            float f = 1.0f / a;
            Vector3 s = orig - v0;
            float u = f * Vector3.Dot(s, h);
            if (u < 0.0f || u > 1.0f) return false;
            Vector3 q = Vector3.Cross(s, edge1);
            float v = f * Vector3.Dot(dir, q);
            if (v < 0.0f || u + v > 1.0f) return false;
            t = f * Vector3.Dot(edge2, q);
            return t > 0.00001f;
        }

        private void DrawPixel(int x, int y, float z, int argb)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height)
            {
                int index = y * Width + x;
                if (z < depthBuffer[index]) { depthBuffer[index] = z; pixelBuffer[index] = argb; }
            }
        }

        private float EdgeFunction(Vector3 a, Vector3 b, float cx, float cy) => (cx - a.X) * (b.Y - a.Y) - (cy - a.Y) * (b.X - a.X);

        private void DrawLine(int x0, int y0, int x1, int y1, int argb)
        {
            int dx = Math.Abs(x1 - x0), sx = x0 < x1 ? 1 : -1;
            int dy = -Math.Abs(y1 - y0), sy = y0 < y1 ? 1 : -1;
            int err = dx + dy, e2;
            while (true)
            {
                DrawPixel(x0, y0, 0.0f, argb);
                if (x0 == x1 && y0 == y1) break;
                e2 = 2 * err;
                if (e2 >= dy) { err += dy; x0 += sx; }
                if (e2 <= dx) { err += dx; y0 += sy; }
            }
        }

        private void DrawWireTriangle(Triangle tri, int argb)
        {
            if (tri == null || tri.P == null || tri.P.Length < 3) return;
            DrawLine((int)tri.P[0].X, (int)tri.P[0].Y, (int)tri.P[1].X, (int)tri.P[1].Y, argb);
            DrawLine((int)tri.P[1].X, (int)tri.P[1].Y, (int)tri.P[2].X, (int)tri.P[2].Y, argb);
            DrawLine((int)tri.P[2].X, (int)tri.P[2].Y, (int)tri.P[0].X, (int)tri.P[0].Y, argb);
        }

        public Bitmap GetFrameBitmap()
        {
            BitmapData data = renderBitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppRgb);
            Marshal.Copy(pixelBuffer, 0, data.Scan0, pixelBuffer.Length);
            renderBitmap.UnlockBits(data);
            return renderBitmap;
        }
    }

    public enum EditorTool { Move, Rotate, Scale, FacePaint, Collision, Delete }
    public enum GameMode { Editor, FlightGame, PlatformerGame }

    // ==========================================
    // DEDICATED PLAYTEST WINDOW WITH CAMERA MODES
    // ==========================================
    public class PlaytestWindow : Form
    {
        private SoftwareRenderer renderer;
        private List<Mesh> playObjects;
        private CameraObject activeCameraObj;
        private Camera playCamera;
        private Mesh playerMesh;
        private HashSet<Keys> keys = new HashSet<Keys>();
        private Stopwatch stopwatch = Stopwatch.StartNew();
        private long lastTick = 0;
        private GameMode mode;
        private bool isFullscreen = false;
        private FormWindowState previousWindowState;
        private FormBorderStyle previousBorderStyle;

        public PlaytestWindow(List<Mesh> sourceObjects, Camera sourceCamera, GameMode gameMode)
        {
            Text = $"Super FX Studio - Playtest Window [{gameMode}] (Press F11 for Fullscreen | C to Cycle Camera Mode)";
            ClientSize = new Size(800, 600);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;

            renderer = new SoftwareRenderer(320, 240);
            mode = gameMode;

            playObjects = sourceObjects?.Select(o => {
                Mesh clone = o is CameraObject co ? new CameraObject
                {
                    Mode = co.Mode,
                    RailPoints = co.RailPoints != null ? new List<Vector3>(co.RailPoints) : new List<Vector3>()
                } : new Mesh();

                clone.Name = o.Name;
                clone.Position = o.Position;
                clone.Rotation = o.Rotation;
                clone.Scale = o.Scale;
                clone.CollisionMin = o.CollisionMin;
                clone.CollisionMax = o.CollisionMax;
                clone.ScriptEnabled = o.ScriptEnabled;
                clone.AttachedScript = o.AttachedScript;
                clone.Triangles = o.Triangles != null ? o.Triangles.Select(t => t.Clone()).ToList() : new List<Triangle>();
                return clone;
            }).ToList() ?? new List<Mesh>();

            playerMesh = playObjects.FirstOrDefault(o => o.Name == "Arwing_Fighter") ?? playObjects.LastOrDefault();
            activeCameraObj = playObjects.OfType<CameraObject>().FirstOrDefault() ?? new CameraObject();

            KeyDown += (s, e) =>
            {
                keys.Add(e.KeyCode);
                if (e.KeyCode == Keys.F11) ToggleFullscreen();
                if (e.KeyCode == Keys.C)
                {
                    activeCameraObj.Mode = (CameraMode)(((int)activeCameraObj.Mode + 1) % 7);
                }
            };
            KeyUp += (s, e) => keys.Remove(e.KeyCode);

            Application.Idle += (s, e) =>
            {
                while (!NativeMessageWaiting())
                {
                    UpdatePlaytest();
                    Invalidate();
                }
            };
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                previousWindowState = WindowState;
                previousBorderStyle = FormBorderStyle;
                FormBorderStyle = FormBorderStyle.None;
                WindowState = FormWindowState.Maximized;
                isFullscreen = true;
            }
            else
            {
                FormBorderStyle = previousBorderStyle;
                WindowState = previousWindowState;
                isFullscreen = false;
            }
        }

        private void UpdatePlaytest()
        {
            long now = stopwatch.ElapsedMilliseconds;
            float dt = (now - lastTick) / 1000.0f;
            if (dt > 0.1f) dt = 0.1f;
            lastTick = now;

            foreach (var obj in playObjects)
            {
                if (obj != null && obj.ScriptEnabled && obj.AttachedScript != null)
                {
                    obj.AttachedScript.Execute(obj, keys, false);
                }
            }

            if (playerMesh != null)
            {
                float speed = 8.0f * dt;
                if (keys.Contains(Keys.W)) playerMesh.Position.Z += speed;
                if (keys.Contains(Keys.S)) playerMesh.Position.Z -= speed;
                if (keys.Contains(Keys.A)) playerMesh.Position.X -= speed;
                if (keys.Contains(Keys.D)) playerMesh.Position.X += speed;
                if (keys.Contains(Keys.Q)) playerMesh.Position.Y += speed;
                if (keys.Contains(Keys.E)) playerMesh.Position.Y -= speed;
            }

            playCamera = activeCameraObj?.ComputeViewCamera(playerMesh) ?? new Camera();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            renderer.Clear(Color.FromArgb(255, 15, 15, 40));
            foreach (var obj in playObjects)
            {
                if (obj != null && obj != activeCameraObj) renderer.RenderMesh(obj, playCamera);
            }

            Bitmap frame = renderer.GetFrameBitmap();
            e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
            e.Graphics.DrawImage(frame, 0, 0, ClientSize.Width, ClientSize.Height);

            using (Font f = new Font("Consolas", 9f, FontStyle.Bold))
            using (Brush b = new SolidBrush(Color.Lime))
            {
                string camModeStr = activeCameraObj != null ? activeCameraObj.Mode.ToString() : "Free";
                e.Graphics.DrawString($"PLAYTEST | CamMode: {camModeStr} | [C] Cycle Cam | [F11] Fullscreen | WASD to Move", f, b, 10, 10);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage { public IntPtr hWnd; public uint msg; public IntPtr wParam; public IntPtr lParam; public uint time; public Point pt; }
        private bool NativeMessageWaiting() => PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
    }

    public class SuperFXForm : Form
    {
        private const int INTERNAL_WIDTH = 320;
        private const int INTERNAL_HEIGHT = 240;

        private SoftwareRenderer renderer;
        private List<Mesh> worldObjects = new List<Mesh>();
        private Camera editorCamera = new Camera();

        private HashSet<Keys> activeKeys = new HashSet<Keys>();
        private Stopwatch timer = Stopwatch.StartNew();
        private long lastTime = 0;

        public EditorTool CurrentTool = EditorTool.Move;
        public GizmoSpace CurrentGizmoSpace = GizmoSpace.Global;
        public GameMode CurrentGameMode = GameMode.Editor;
        public ViewTab CurrentViewTab = ViewTab.Layout;
        public Color CurrentPaintColor = Color.Red;
        public Bitmap LoadedImageTexture = null;

        private Mesh selectedMesh = null;
        private Mesh markedParentMesh = null;
        private Triangle selectedFace = null;
        private AnimationClip currentClip = new AnimationClip();
        private int currentFrame = 0;
        private bool isPlayingAnimation = false;
        private bool onionSkinEnabled = false;

        private Point lastMousePos;
        private bool isRightMouseDown = false;
        private bool isGizmoDragging = false;
        private bool isLeftMouseClickedThisFrame = false;
        private GizmoAxis activeGizmoAxis = GizmoAxis.None;
        private GizmoAxis hoveredGizmoAxis = GizmoAxis.None;

        private Mesh playerShip = null;
        private float gameScore = 0;

        private readonly Color[] PaletteColors = new Color[] {
            Color.Red, Color.Orange, Color.Yellow, Color.Lime,
            Color.Cyan, Color.DodgerBlue, Color.Magenta, Color.White, Color.DarkGray
        };

        private Rectangle btnTablayout = new Rectangle(330, 2, 80, 21);
        private Rectangle btnTabAnim = new Rectangle(413, 2, 80, 21);
        private Rectangle btnTabCode = new Rectangle(496, 2, 95, 21);
        private Rectangle btnPlaytestPopup = new Rectangle(594, 2, 115, 21);

        private Rectangle btnMove = new Rectangle(20, 42, 85, 22);
        private Rectangle btnRotate = new Rectangle(110, 42, 85, 22);
        private Rectangle btnScale = new Rectangle(200, 42, 95, 22);
        private Rectangle btnPaint = new Rectangle(20, 68, 85, 22);
        private Rectangle btnColl = new Rectangle(110, 68, 85, 22);
        private Rectangle btnDel = new Rectangle(200, 68, 95, 22);
        private Rectangle btnToggleSpace = new Rectangle(20, 94, 275, 22);

        private Rectangle hierarchyBox = new Rectangle(20, 142, 275, 78);
        private Rectangle btnMarkParent = new Rectangle(20, 224, 85, 20);
        private Rectangle btnAttachChild = new Rectangle(110, 224, 95, 20);
        private Rectangle btnUnparent = new Rectangle(210, 224, 85, 20);

        private Rectangle inspectorBox = new Rectangle(20, 266, 275, 66);
        private Rectangle btnInspectorSubX = new Rectangle(130, 284, 16, 16);
        private Rectangle btnInspectorAddX = new Rectangle(148, 284, 16, 16);
        private Rectangle btnInspectorSubY = new Rectangle(195, 284, 16, 16);
        private Rectangle btnInspectorAddY = new Rectangle(213, 284, 16, 16);
        private Rectangle btnInspectorSubZ = new Rectangle(260, 284, 16, 16);
        private Rectangle btnInspectorAddZ = new Rectangle(278, 284, 16, 16);

        private Rectangle btnLoadTex = new Rectangle(20, 356, 132, 22);
        private Rectangle btnClearTex = new Rectangle(163, 356, 132, 22);

        private Rectangle scriptAttachBox = new Rectangle(20, 386, 275, 68);
        private Rectangle btnToggleScript = new Rectangle(28, 402, 125, 22);
        private Rectangle btnOpenCodeView = new Rectangle(161, 402, 125, 22);

        private Rectangle btnSpawnCube = new Rectangle(20, 464, 85, 22);
        private Rectangle btnSpawnPyr = new Rectangle(110, 464, 85, 22);
        private Rectangle btnSpawnFighter = new Rectangle(200, 464, 95, 22);
        private Rectangle btnSpawnCamera = new Rectangle(20, 490, 275, 22);

        private Rectangle btnPlayEditor = new Rectangle(20, 528, 85, 22);
        private Rectangle btnPlayFlight = new Rectangle(110, 528, 85, 22);
        private Rectangle btnPlayPlatform = new Rectangle(200, 528, 95, 22);

        private TextBox codeEditorTextBox = new TextBox();
        private Rectangle animWindowRect = new Rectangle(330, 725, 740, 120);
        private Rectangle btnAnimPlay = new Rectangle(342, 750, 65, 24);
        private Rectangle btnAnimPrev = new Rectangle(412, 750, 32, 24);
        private Rectangle btnAnimNext = new Rectangle(447, 750, 32, 24);
        private Rectangle btnSubFrameCount = new Rectangle(484, 750, 20, 24);
        private Rectangle btnFrameCountLabel = new Rectangle(506, 750, 95, 24);
        private Rectangle btnAddFrameCount = new Rectangle(603, 750, 20, 24);
        private Rectangle btnAddKey = new Rectangle(628, 750, 80, 24);
        private Rectangle btnToggleOnion = new Rectangle(713, 750, 90, 24);
        private Rectangle btnSaveAnim = new Rectangle(808, 750, 75, 24);
        private Rectangle btnLoadAnim = new Rectangle(888, 750, 75, 24);
        private Rectangle timelineTrackRect = new Rectangle(342, 786, 716, 44);

        public SuperFXForm()
        {
            Text = "Super FX Studio 3D [v1.57.1 - Null-Safe Renderer Fix]";
            ClientSize = new Size(1100, 855);
            DoubleBuffered = true;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;

            renderer = new SoftwareRenderer(INTERNAL_WIDTH, INTERNAL_HEIGHT);
            SetupDefaultScene();

            codeEditorTextBox.Location = new Point(340, 35);
            codeEditorTextBox.Size = new Size(720, 670);
            codeEditorTextBox.Multiline = true;
            codeEditorTextBox.ScrollBars = ScrollBars.Vertical;
            codeEditorTextBox.Font = new Font("Consolas", 10.0f);
            codeEditorTextBox.BackColor = Color.FromArgb(30, 30, 40);
            codeEditorTextBox.ForeColor = Color.FromArgb(220, 220, 240);
            codeEditorTextBox.Visible = false;
            codeEditorTextBox.TextChanged += (s, e) =>
            {
                if (selectedMesh != null && selectedMesh.AttachedScript != null)
                {
                    selectedMesh.AttachedScript.SourceCode = codeEditorTextBox.Text;
                }
            };
            Controls.Add(codeEditorTextBox);

            KeyDown += (s, e) =>
            {
                activeKeys.Add(e.KeyCode);
                if (e.KeyCode == Keys.D1) CurrentTool = EditorTool.Move;
                if (e.KeyCode == Keys.D2) CurrentTool = EditorTool.Rotate;
                if (e.KeyCode == Keys.D3) CurrentTool = EditorTool.Scale;
                if (e.KeyCode == Keys.D4) CurrentTool = EditorTool.FacePaint;
                if (e.KeyCode == Keys.D5) CurrentTool = EditorTool.Collision;

                if (e.KeyCode == Keys.F1) SwitchGameMode(GameMode.Editor);
                if (e.KeyCode == Keys.F2) SwitchGameMode(GameMode.FlightGame);
                if (e.KeyCode == Keys.F3) SwitchGameMode(GameMode.PlatformerGame);

                if (CurrentGameMode == GameMode.Editor)
                {
                    if (e.KeyCode == Keys.C) SpawnObject(Mesh.CreateCube(1.0f, CurrentPaintColor));
                    if (e.KeyCode == Keys.P) SpawnObject(Mesh.CreatePyramid(1.0f, CurrentPaintColor));
                    if (e.KeyCode == Keys.F) SpawnObject(Mesh.CreateStarFoxFighter());
                    if (e.KeyCode == Keys.T) renderer.WireframeMode = !renderer.WireframeMode;
                    if (e.KeyCode == Keys.Delete || e.KeyCode == Keys.X) DeleteSelectedMesh();
                }
            };

            KeyUp += (s, e) => activeKeys.Remove(e.KeyCode);

            MouseDown += (s, e) =>
            {
                if (btnTablayout.Contains(e.Location)) { CurrentViewTab = ViewTab.Layout; codeEditorTextBox.Visible = false; }
                if (btnTabAnim.Contains(e.Location)) { CurrentViewTab = ViewTab.Animation; codeEditorTextBox.Visible = false; }
                if (btnTabCode.Contains(e.Location))
                {
                    CurrentViewTab = ViewTab.CodeEditor;
                    if (selectedMesh != null)
                    {
                        if (selectedMesh.AttachedScript == null) selectedMesh.AttachedScript = new PoloniumScriptEngine();
                        codeEditorTextBox.Text = selectedMesh.AttachedScript.SourceCode;
                    }
                    else codeEditorTextBox.Text = "// Select a mesh in hierarchy to edit its Polonium Script (.pscript)";
                    codeEditorTextBox.Visible = true;
                    codeEditorTextBox.BringToFront();
                }
                if (btnPlaytestPopup.Contains(e.Location))
                {
                    PlaytestWindow playWindow = new PlaytestWindow(worldObjects, editorCamera, CurrentGameMode);
                    playWindow.Show();
                }

                if (CurrentViewTab == ViewTab.Animation && animWindowRect.Contains(e.Location))
                {
                    if (btnAnimPlay.Contains(e.Location)) isPlayingAnimation = !isPlayingAnimation;
                    if (btnAnimPrev.Contains(e.Location)) currentFrame = Math.Max(0, currentFrame - 1);
                    if (btnAnimNext.Contains(e.Location)) currentFrame = Math.Min(currentClip.TotalFrames, currentFrame + 1);

                    if (btnSubFrameCount.Contains(e.Location))
                    {
                        currentClip.TotalFrames = Math.Max(5, currentClip.TotalFrames - 50);
                        if (currentFrame > currentClip.TotalFrames) currentFrame = currentClip.TotalFrames;
                    }
                    if (btnAddFrameCount.Contains(e.Location))
                    {
                        currentClip.TotalFrames = Math.Min(10000, currentClip.TotalFrames + 50);
                    }
                    if (btnFrameCountLabel.Contains(e.Location))
                    {
                        string input = Prompt.ShowDialog("Set Total Animation Frames (5 - 10000):", "Cutscene Animation Length", currentClip.TotalFrames.ToString());
                        if (int.TryParse(input, out int parsedFrames))
                        {
                            currentClip.TotalFrames = Math.Max(5, Math.Min(10000, parsedFrames));
                            if (currentFrame > currentClip.TotalFrames) currentFrame = currentClip.TotalFrames;
                        }
                    }

                    if (btnToggleOnion.Contains(e.Location)) onionSkinEnabled = !onionSkinEnabled;
                    if (btnAddKey.Contains(e.Location)) RecordKeyframeAtCurrentFrame();
                    if (btnSaveAnim.Contains(e.Location)) SaveAnimationToFile();
                    if (btnLoadAnim.Contains(e.Location)) LoadAnimationFromFile();

                    if (timelineTrackRect.Contains(e.Location))
                    {
                        float relX = e.X - timelineTrackRect.Left;
                        float pct = Math.Max(0.0f, Math.Min(1.0f, relX / timelineTrackRect.Width));
                        currentFrame = (int)(pct * currentClip.TotalFrames);
                    }
                    return;
                }

                if (e.X >= 10 && e.X <= 315 && e.Y >= 25 && e.Y <= 560)
                {
                    if (btnMove.Contains(e.Location)) CurrentTool = EditorTool.Move;
                    if (btnRotate.Contains(e.Location)) CurrentTool = EditorTool.Rotate;
                    if (btnScale.Contains(e.Location)) CurrentTool = EditorTool.Scale;
                    if (btnPaint.Contains(e.Location)) CurrentTool = EditorTool.FacePaint;
                    if (btnColl.Contains(e.Location)) { CurrentTool = EditorTool.Collision; if (selectedMesh != null) selectedMesh.ShowCollisionBox = !selectedMesh.ShowCollisionBox; }
                    if (btnDel.Contains(e.Location)) DeleteSelectedMesh();

                    if (btnToggleSpace.Contains(e.Location))
                    {
                        CurrentGizmoSpace = (CurrentGizmoSpace == GizmoSpace.Global) ? GizmoSpace.Local : GizmoSpace.Global;
                    }

                    if (hierarchyBox.Contains(e.Location))
                    {
                        var treeItems = GetHierarchyDisplayList();
                        int relY = e.Y - (hierarchyBox.Top + 4);
                        int clickedIndex = relY / 16;
                        if (clickedIndex >= 0 && clickedIndex < treeItems.Count)
                        {
                            if (selectedMesh != null) selectedMesh.IsSelected = false;
                            selectedMesh = treeItems[clickedIndex].mesh;
                            if (selectedMesh != null) selectedMesh.IsSelected = true;
                            selectedFace = null;
                            if (CurrentViewTab == ViewTab.CodeEditor && selectedMesh != null && selectedMesh.AttachedScript != null)
                            {
                                codeEditorTextBox.Text = selectedMesh.AttachedScript.SourceCode;
                            }
                        }
                    }

                    if (btnMarkParent.Contains(e.Location))
                    {
                        if (selectedMesh != null) { markedParentMesh = selectedMesh; MessageBox.Show($"Marked '{markedParentMesh.Name}' as Parent."); }
                        else MessageBox.Show("Select an object first.");
                    }
                    if (btnAttachChild.Contains(e.Location))
                    {
                        if (selectedMesh != null && markedParentMesh != null)
                        {
                            if (selectedMesh == markedParentMesh) MessageBox.Show("Cannot parent to itself.");
                            else selectedMesh.Parent = markedParentMesh;
                        }
                        else MessageBox.Show("Select child and mark parent first.");
                    }
                    if (btnUnparent.Contains(e.Location))
                    {
                        if (selectedMesh != null) selectedMesh.Parent = null;
                    }

                    if (selectedMesh != null)
                    {
                        float step = 0.5f;
                        bool changed = false;
                        if (btnInspectorSubX.Contains(e.Location)) { selectedMesh.Position.X -= step; changed = true; }
                        if (btnInspectorAddX.Contains(e.Location)) { selectedMesh.Position.X += step; changed = true; }
                        if (btnInspectorSubY.Contains(e.Location)) { selectedMesh.Position.Y -= step; changed = true; }
                        if (btnInspectorAddY.Contains(e.Location)) { selectedMesh.Position.Y += step; changed = true; }
                        if (btnInspectorSubZ.Contains(e.Location)) { selectedMesh.Position.Z -= step; changed = true; }
                        if (btnInspectorAddZ.Contains(e.Location)) { selectedMesh.Position.Z += step; changed = true; }
                        if (changed) RecordKeyframeAtCurrentFrame();
                    }

                    if (btnLoadTex.Contains(e.Location)) LoadImageTextureFromFile();
                    if (btnClearTex.Contains(e.Location)) LoadedImageTexture = null;

                    if (btnToggleScript.Contains(e.Location))
                    {
                        if (selectedMesh != null)
                        {
                            if (selectedMesh.AttachedScript == null) selectedMesh.AttachedScript = new PoloniumScriptEngine();
                            selectedMesh.ScriptEnabled = !selectedMesh.ScriptEnabled;
                        }
                        else MessageBox.Show("Select an object to attach/toggle Polonium Script.");
                    }
                    if (btnOpenCodeView.Contains(e.Location))
                    {
                        CurrentViewTab = ViewTab.CodeEditor;
                        if (selectedMesh != null)
                        {
                            if (selectedMesh.AttachedScript == null) selectedMesh.AttachedScript = new PoloniumScriptEngine();
                            codeEditorTextBox.Text = selectedMesh.AttachedScript.SourceCode;
                        }
                        codeEditorTextBox.Visible = true;
                        codeEditorTextBox.BringToFront();
                    }

                    if (btnSpawnCube.Contains(e.Location)) SpawnObject(Mesh.CreateCube(1.0f, CurrentPaintColor));
                    if (btnSpawnPyr.Contains(e.Location)) SpawnObject(Mesh.CreatePyramid(1.0f, CurrentPaintColor));
                    if (btnSpawnFighter.Contains(e.Location)) SpawnObject(Mesh.CreateStarFoxFighter());
                    if (btnSpawnCamera.Contains(e.Location)) SpawnObject(CameraObject.CreateCameraGizmoMesh());

                    if (btnPlayEditor.Contains(e.Location)) SwitchGameMode(GameMode.Editor);
                    if (btnPlayFlight.Contains(e.Location)) SwitchGameMode(GameMode.FlightGame);
                    if (btnPlayPlatform.Contains(e.Location)) SwitchGameMode(GameMode.PlatformerGame);
                    return;
                }

                if (e.Y >= ClientSize.Height - 130 && e.Y <= ClientSize.Height - 80 && e.X >= 330 && e.X <= 680)
                {
                    int idx = (e.X - 330) / 38;
                    if (idx >= 0 && idx < PaletteColors.Length)
                    {
                        CurrentPaintColor = PaletteColors[idx];
                        if (selectedFace != null) selectedFace.BaseColor = CurrentPaintColor;
                    }
                    return;
                }

                if (CurrentViewTab == ViewTab.CodeEditor || CurrentGameMode != GameMode.Editor) return;
                int vpX = 330, vpY = 25, vpW = 740, vpH = 690;
                Rectangle vpRect = new Rectangle(vpX, vpY, vpW, vpH);

                if (e.X >= vpX && e.X <= vpX + vpW && e.Y >= vpY && e.Y <= vpY + vpH)
                {
                    if (e.Button == MouseButtons.Right)
                    {
                        isRightMouseDown = true;
                        lastMousePos = e.Location;
                    }
                    else if (e.Button == MouseButtons.Left)
                    {
                        isLeftMouseClickedThisFrame = true;
                        if (selectedMesh != null && worldObjects.Count > 0 && selectedMesh != worldObjects[0] &&
                           (CurrentTool == EditorTool.Move || CurrentTool == EditorTool.Rotate || CurrentTool == EditorTool.Scale))
                        {
                            activeGizmoAxis = GetClickedGizmoAxis(e.Location, selectedMesh, vpRect);
                            if (activeGizmoAxis != GizmoAxis.None)
                            {
                                isGizmoDragging = true;
                                lastMousePos = e.Location;
                                return;
                            }
                        }

                        int rx = (int)((float)(e.X - vpX) / vpW * INTERNAL_WIDTH);
                        int ry = (int)((float)(e.Y - vpY) / vpH * INTERNAL_HEIGHT);

                        var (hitMesh, hitFace) = renderer.PickFace3D(rx, ry, worldObjects, editorCamera);

                        if (selectedFace != null) selectedFace.IsSelected = false;
                        if (selectedMesh != null) selectedMesh.IsSelected = false;

                        if (hitMesh != null && hitFace != null)
                        {
                            selectedMesh = hitMesh; selectedFace = hitFace;
                            if (CurrentViewTab == ViewTab.CodeEditor && selectedMesh.AttachedScript != null)
                            {
                                codeEditorTextBox.Text = selectedMesh.AttachedScript.SourceCode;
                            }

                            if (CurrentTool == EditorTool.FacePaint)
                            {
                                selectedFace.BaseColor = CurrentPaintColor;
                                selectedFace.TextureImage = LoadedImageTexture;
                            }
                            else { selectedFace.IsSelected = true; selectedMesh.IsSelected = true; }
                        }
                        else { selectedMesh = null; selectedFace = null; }

                        lastMousePos = e.Location;
                    }
                }
            };

            MouseUp += (s, e) =>
            {
                if (e.Button == MouseButtons.Right) isRightMouseDown = false;
                if (e.Button == MouseButtons.Left)
                {
                    if (isGizmoDragging && selectedMesh != null)
                    {
                        RecordKeyframeAtCurrentFrame();
                    }
                    isGizmoDragging = false;
                    activeGizmoAxis = GizmoAxis.None;
                }
            };

            MouseMove += (s, e) =>
            {
                int vpX = 330, vpY = 25, vpW = 740, vpH = 690;
                Rectangle vpRect = new Rectangle(vpX, vpY, vpW, vpH);

                if (CurrentGameMode == GameMode.Editor && selectedMesh != null && worldObjects.Count > 0 && selectedMesh != worldObjects[0] && !isGizmoDragging && !isRightMouseDown)
                {
                    if (e.X >= vpX && e.X <= vpX + vpW && e.Y >= vpY && e.Y <= vpY + vpH)
                    {
                        hoveredGizmoAxis = GetClickedGizmoAxis(e.Location, selectedMesh, vpRect);
                    }
                    else hoveredGizmoAxis = GizmoAxis.None;
                }

                if (isRightMouseDown)
                {
                    float dx = e.X - lastMousePos.X, dy = e.Y - lastMousePos.Y;
                    editorCamera.Yaw += dx * 0.005f; editorCamera.Pitch -= dy * 0.005f;
                    editorCamera.Pitch = Math.Max(-1.5f, Math.Min(1.5f, editorCamera.Pitch));
                    lastMousePos = e.Location;
                }
                else if (isGizmoDragging && selectedMesh != null)
                {
                    float dx = (e.X - lastMousePos.X) * 0.05f;
                    float dy = (e.Y - lastMousePos.Y) * 0.05f;
                    float delta = dx - dy;

                    Vector3 axisDir = new Vector3();
                    if (activeGizmoAxis == GizmoAxis.X) axisDir = new Vector3(1, 0, 0);
                    if (activeGizmoAxis == GizmoAxis.Y) axisDir = new Vector3(0, 1, 0);
                    if (activeGizmoAxis == GizmoAxis.Z) axisDir = new Vector3(0, 0, 1);

                    Matrix4x4 matRotX = Matrix4x4.RotationX(selectedMesh.Rotation.X);
                    Matrix4x4 matRotY = Matrix4x4.RotationY(selectedMesh.Rotation.Y);
                    Matrix4x4 matRotZ = Matrix4x4.RotationZ(selectedMesh.Rotation.Z);
                    Matrix4x4 localRot = Matrix4x4.Multiply(Matrix4x4.Multiply(matRotX, matRotY), matRotZ);

                    Matrix4x4 orientationMatrix = localRot;
                    if (selectedMesh.Parent != null)
                    {
                        Matrix4x4 parentWorldRot = selectedMesh.Parent.GetWorldMatrix();
                        parentWorldRot.M[3, 0] = 0; parentWorldRot.M[3, 1] = 0; parentWorldRot.M[3, 2] = 0;
                        orientationMatrix = Matrix4x4.Multiply(localRot, parentWorldRot);
                    }

                    if (CurrentGizmoSpace == GizmoSpace.Local || selectedMesh.Parent != null)
                    {
                        axisDir = Matrix4x4.MultiplyVector(orientationMatrix, axisDir).Normalized();
                    }

                    if (CurrentTool == EditorTool.Move)
                    {
                        float moveVal = dx - dy;
                        selectedMesh.Position = selectedMesh.Position + (axisDir * moveVal);
                    }
                    else if (CurrentTool == EditorTool.Rotate)
                    {
                        float rotSpeed = 0.05f;
                        if (activeGizmoAxis == GizmoAxis.X) selectedMesh.Rotation.X += delta * rotSpeed;
                        if (activeGizmoAxis == GizmoAxis.Y) selectedMesh.Rotation.Y += delta * rotSpeed;
                        if (activeGizmoAxis == GizmoAxis.Z) selectedMesh.Rotation.Z += delta * rotSpeed;
                    }
                    else if (CurrentTool == EditorTool.Scale)
                    {
                        float scaleFactor = 1.0f + delta * 0.1f;
                        if (activeGizmoAxis == GizmoAxis.X) selectedMesh.Scale.X = Math.Max(0.1f, selectedMesh.Scale.X * scaleFactor);
                        if (activeGizmoAxis == GizmoAxis.Y) selectedMesh.Scale.Y = Math.Max(0.1f, selectedMesh.Scale.Y * scaleFactor);
                        if (activeGizmoAxis == GizmoAxis.Z) selectedMesh.Scale.Z = Math.Max(0.1f, selectedMesh.Scale.Z * scaleFactor);
                    }

                    lastMousePos = e.Location;
                }
            };

            Application.Idle += (s, e) =>
            {
                while (!NativeMessageWaiting()) { UpdateEngine(); Invalidate(); }
            };
        }

        private void RecordKeyframeAtCurrentFrame()
        {
            if (selectedMesh == null) return;
            currentClip.Keyframes.RemoveAll(k => k.FrameIndex == currentFrame);
            currentClip.Keyframes.Add(new Keyframe(currentFrame, selectedMesh.Position, selectedMesh.Rotation, selectedMesh.Scale));
            currentClip.Keyframes = currentClip.Keyframes.OrderBy(k => k.FrameIndex).ToList();
        }

        private List<(Mesh mesh, int depth)> GetHierarchyDisplayList()
        {
            List<(Mesh, int)> list = new List<(Mesh, int)>();
            void AddRecursive(Mesh m, int depth)
            {
                if (m != null && worldObjects.Contains(m))
                {
                    list.Add((m, depth));
                    foreach (var child in worldObjects.Where(o => o != null && o.Parent == m))
                    {
                        AddRecursive(child, depth + 1);
                    }
                }
            }

            foreach (var obj in worldObjects.Where(o => o != null && o.Parent == null))
            {
                AddRecursive(obj, 0);
            }
            return list;
        }

        private GizmoAxis GetClickedGizmoAxis(Point mousePos, Mesh mesh, Rectangle vpRect)
        {
            if (mesh == null) return GizmoAxis.None;
            Matrix4x4 matView = editorCamera.GetViewMatrix();
            Matrix4x4 matWorld = mesh.GetWorldMatrix();
            Vector3 origin = new Vector3(matWorld.M[3, 0], matWorld.M[3, 1], matWorld.M[3, 2]);
            float handleLength = 1.8f;

            PointF WorldToScreen(Vector3 p)
            {
                Vector3 v = Matrix4x4.MultiplyVector(matView, p);
                if (v.Z > -0.1f) return new PointF(-1, -1);
                Vector3 proj = Matrix4x4.MultiplyVector(renderer.ProjectionMatrix, v);
                float sx = vpRect.X + (proj.X + 1.0f) * 0.5f * vpRect.Width;
                float sy = vpRect.Y + (1.0f - proj.Y) * 0.5f * vpRect.Height;
                return new PointF(sx, sy);
            }

            Vector3 xDir = new Vector3(1, 0, 0);
            Vector3 yDir = new Vector3(0, 1, 0);
            Vector3 zDir = new Vector3(0, 0, 1);

            Matrix4x4 matRotX = Matrix4x4.RotationX(mesh.Rotation.X);
            Matrix4x4 matRotY = Matrix4x4.RotationY(mesh.Rotation.Y);
            Matrix4x4 matRotZ = Matrix4x4.RotationZ(mesh.Rotation.Z);
            Matrix4x4 localRot = Matrix4x4.Multiply(Matrix4x4.Multiply(matRotX, matRotY), matRotZ);

            Matrix4x4 orientationMatrix = localRot;
            if (mesh.Parent != null)
            {
                Matrix4x4 parentWorldRot = mesh.Parent.GetWorldMatrix();
                parentWorldRot.M[3, 0] = 0; parentWorldRot.M[3, 1] = 0; parentWorldRot.M[3, 2] = 0;
                orientationMatrix = Matrix4x4.Multiply(localRot, parentWorldRot);
            }

            if (CurrentGizmoSpace == GizmoSpace.Local || mesh.Parent != null)
            {
                xDir = Matrix4x4.MultiplyVector(orientationMatrix, xDir).Normalized();
                yDir = Matrix4x4.MultiplyVector(orientationMatrix, yDir).Normalized();
                zDir = Matrix4x4.MultiplyVector(orientationMatrix, zDir).Normalized();
            }

            PointF pX, pY, pZ;
            if (CurrentTool == EditorTool.Rotate)
            {
                pX = WorldToScreen(origin + yDir * 1.3f);
                pY = WorldToScreen(origin + zDir * 1.3f);
                pZ = WorldToScreen(origin + xDir * 1.3f);
            }
            else
            {
                pX = WorldToScreen(origin + xDir * handleLength);
                pY = WorldToScreen(origin + yDir * handleLength);
                pZ = WorldToScreen(origin + zDir * handleLength);
            }

            if (pX.X < 0 && pY.X < 0 && pZ.X < 0) return GizmoAxis.None;

            float grabRadius = 14.0f;
            if (pX.X >= 0 && Math.Abs(mousePos.X - pX.X) <= grabRadius && Math.Abs(mousePos.Y - pX.Y) <= grabRadius) return GizmoAxis.X;
            if (pY.X >= 0 && Math.Abs(mousePos.X - pY.X) <= grabRadius && Math.Abs(mousePos.Y - pY.Y) <= grabRadius) return GizmoAxis.Y;
            if (pZ.X >= 0 && Math.Abs(mousePos.X - pZ.X) <= grabRadius && Math.Abs(mousePos.Y - pZ.Y) <= grabRadius) return GizmoAxis.Z;

            return GizmoAxis.None;
        }

        private void SaveAnimationToFile()
        {
            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "Super FX Animation (*.sfxanim)|*.sfxanim";
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try { currentClip.SaveToFile(sfd.FileName); MessageBox.Show("Animation saved successfully!"); }
                    catch (Exception ex) { MessageBox.Show("Error saving: " + ex.Message); }
                }
            }
        }

        private void LoadAnimationFromFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Super FX Animation (*.sfxanim)|*.sfxanim";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        currentClip = AnimationClip.LoadFromFile(ofd.FileName);
                        MessageBox.Show($"Loaded animation '{currentClip.Name}' with {currentClip.Keyframes.Count} keyframes.");
                    }
                    catch (Exception ex) { MessageBox.Show("Error loading: " + ex.Message); }
                }
            }
        }

        private void LoadImageTextureFromFile()
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Image Files|*.png;*.jpg;*.jpeg;*.bmp";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    try { LoadedImageTexture = new Bitmap(ofd.FileName); }
                    catch (Exception ex) { MessageBox.Show("Failed to load image: " + ex.Message); }
                }
            }
        }

        private void SetupDefaultScene()
        {
            worldObjects.Clear();
            Mesh ground = Mesh.CreateGroundGrid(60.0f, 25);
            ground.Position = new Vector3(0, 0, 0); worldObjects.Add(ground);

            Mesh fighter = Mesh.CreateStarFoxFighter();
            fighter.Position = new Vector3(0, 1.5f, 5.0f);
            fighter.AttachedScript = new PoloniumScriptEngine();
            worldObjects.Add(fighter);

            CameraObject camObj = CameraObject.CreateCameraGizmoMesh();
            camObj.Position = new Vector3(0, 4, -5);
            worldObjects.Add(camObj);

            editorCamera.Position = new Vector3(0, 3, -12);
            editorCamera.Pitch = 0.2f;
            editorCamera.Yaw = 0.0f;
        }

        private void SpawnObject(Mesh m)
        {
            if (m == null) return;
            m.Position = editorCamera.Position + editorCamera.Forward * 6.0f;
            m.AttachedScript = new PoloniumScriptEngine();
            worldObjects.Add(m);
            selectedMesh = m;
        }

        private void DeleteSelectedMesh()
        {
            if (selectedMesh != null && worldObjects.Count > 0 && selectedMesh != worldObjects[0])
            {
                foreach (var child in worldObjects.Where(o => o != null && o.Parent == selectedMesh)) child.Parent = null;
                worldObjects.Remove(selectedMesh); selectedMesh = null; selectedFace = null;
                codeEditorTextBox.Visible = false;
            }
        }

        private void SwitchGameMode(GameMode mode)
        {
            CurrentGameMode = mode; gameScore = 0;
            if (mode == GameMode.FlightGame)
            {
                SetupDefaultScene();
                playerShip = worldObjects.Count > 1 ? worldObjects[1] : null;
                for (int i = 1; i <= 8; i++)
                {
                    Mesh cube = Mesh.CreateCube(1.5f, Color.Magenta);
                    cube.Position = new Vector3((i % 2 == 0 ? -3 : 3), 2.0f, i * 12.0f);
                    worldObjects.Add(cube);
                }
            }
            else if (mode == GameMode.PlatformerGame)
            {
                SetupDefaultScene(); playerShip = Mesh.CreateCube(1.2f, Color.Cyan);
                playerShip.Position = new Vector3(0, 1.0f, 3.0f); worldObjects.Add(playerShip);
                for (int i = 1; i <= 6; i++)
                {
                    Mesh plat = Mesh.CreateCube(3.0f, Color.Orange);
                    plat.Scale = new Vector3(1.5f, 0.3f, 1.5f);
                    plat.Position = new Vector3((i % 2 == 0 ? -3 : 3), i * 1.5f, i * 8.0f);
                    worldObjects.Add(plat);
                }
            }
            else
            {
                SetupDefaultScene();
            }
        }

        private void UpdateEngine()
        {
            long currentTime = timer.ElapsedMilliseconds;
            float deltaTime = (currentTime - lastTime) / 1000.0f;
            if (deltaTime > 0.1f) deltaTime = 0.1f;
            lastTime = currentTime;

            foreach (var obj in worldObjects)
            {
                if (obj != null && obj.ScriptEnabled && obj.AttachedScript != null)
                {
                    obj.AttachedScript.Execute(obj, activeKeys, isLeftMouseClickedThisFrame);
                }
            }
            isLeftMouseClickedThisFrame = false;

            if (isPlayingAnimation && CurrentGameMode == GameMode.Editor)
            {
                currentFrame++;
                if (currentFrame > currentClip.TotalFrames) currentFrame = 0;
            }

            if (selectedMesh != null && currentClip.Keyframes != null && currentClip.Keyframes.Count > 0 && CurrentGameMode == GameMode.Editor && !isGizmoDragging)
            {
                currentClip.Evaluate(currentFrame, out selectedMesh.Position, out selectedMesh.Rotation, out selectedMesh.Scale);
            }

            if (CurrentGameMode == GameMode.Editor)
            {
                float speed = 8.0f * deltaTime;
                if (activeKeys.Contains(Keys.W) && CurrentViewTab != ViewTab.CodeEditor) editorCamera.Position += editorCamera.Forward * speed;
                if (activeKeys.Contains(Keys.S) && CurrentViewTab != ViewTab.CodeEditor) editorCamera.Position -= editorCamera.Forward * speed;
                if (activeKeys.Contains(Keys.A)) editorCamera.Position -= editorCamera.Right * speed;
                if (activeKeys.Contains(Keys.D)) editorCamera.Position += editorCamera.Right * speed;
                if (activeKeys.Contains(Keys.Q)) editorCamera.Position.Y += speed;
                if (activeKeys.Contains(Keys.E)) editorCamera.Position.Y -= speed;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(192, 192, 192));
            Rectangle vpRect = new Rectangle(330, 25, 740, 690);

            DrawWin95Button(e.Graphics, btnTablayout, "Layout", CurrentViewTab == ViewTab.Layout);
            DrawWin95Button(e.Graphics, btnTabAnim, "Animation", CurrentViewTab == ViewTab.Animation);
            DrawWin95Button(e.Graphics, btnTabCode, "Code Editor", CurrentViewTab == ViewTab.CodeEditor);
            DrawWin95Button(e.Graphics, btnPlaytestPopup, "▶ Playtest Window", false);

            if (CurrentViewTab != ViewTab.CodeEditor)
            {
                codeEditorTextBox.Visible = false;
                e.Graphics.FillRectangle(Brushes.Black, vpRect);
                DrawWin95Bevel(e.Graphics, vpRect, true);

                renderer.Clear(Color.FromArgb(255, 12, 12, 32));

                if (CurrentViewTab == ViewTab.Animation && onionSkinEnabled && currentFrame > 0 && selectedMesh != null)
                {
                    Vector3 origPos = selectedMesh.Position;
                    Vector3 origRot = selectedMesh.Rotation;
                    Vector3 origScl = selectedMesh.Scale;

                    currentClip.Evaluate(currentFrame - 1, out selectedMesh.Position, out selectedMesh.Rotation, out selectedMesh.Scale);
                    renderer.RenderMesh(selectedMesh, editorCamera, Color.DarkSlateGray, 0.4f);

                    selectedMesh.Position = origPos;
                    selectedMesh.Rotation = origRot;
                    selectedMesh.Scale = origScl;
                }

                foreach (var obj in worldObjects)
                {
                    if (obj != null) renderer.RenderMesh(obj, editorCamera);
                }

                if (CurrentGameMode == GameMode.Editor && selectedMesh != null && worldObjects.Count > 0 && selectedMesh != worldObjects[0])
                {
                    renderer.RenderGizmo(selectedMesh, editorCamera, CurrentTool, activeGizmoAxis, hoveredGizmoAxis, CurrentGizmoSpace);
                }

                Bitmap frame = renderer.GetFrameBitmap();
                e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;
                e.Graphics.DrawImage(frame, vpRect.X, vpRect.Y, vpRect.Width, vpRect.Height);

                if (CurrentViewTab == ViewTab.Animation)
                {
                    DrawAnimationWindow(e.Graphics);
                }
            }
            else
            {
                codeEditorTextBox.Visible = true;
                using (Brush codeBg = new SolidBrush(Color.FromArgb(30, 30, 40)))
                {
                    e.Graphics.FillRectangle(codeBg, vpRect);
                }
                DrawWin95Bevel(e.Graphics, vpRect, true);
            }

            Draw90sToolbox(e.Graphics);
        }

        private void DrawAnimationWindow(Graphics g)
        {
            using (Font font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular))
            using (Font boldFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(192, 192, 192)))
            {
                g.FillRectangle(bgBrush, animWindowRect);
                DrawWin95Bevel(g, animWindowRect, false);
                DrawGroupBox(g, animWindowRect, "ANIMATION TIMELINE WINDOW (UP TO 10,000 FRAMES)");

                DrawWin95Button(g, btnAnimPlay, isPlayingAnimation ? "Pause ■" : "Play ▶", isPlayingAnimation);
                DrawWin95Button(g, btnAnimPrev, "◄", false);
                DrawWin95Button(g, btnAnimNext, "►", false);

                DrawWin95Button(g, btnSubFrameCount, "-", false);
                DrawWin95Button(g, btnFrameCountLabel, $"Length: {currentClip.TotalFrames}F", false);
                DrawWin95Button(g, btnAddFrameCount, "+", false);

                DrawWin95Button(g, btnAddKey, "+ Keyframe", false);
                DrawWin95Button(g, btnToggleOnion, onionSkinEnabled ? "Onion: ON" : "Onion: OFF", onionSkinEnabled);
                DrawWin95Button(g, btnSaveAnim, "Save...", false);
                DrawWin95Button(g, btnLoadAnim, "Load...", false);

                g.FillRectangle(Brushes.White, timelineTrackRect);
                DrawWin95Bevel(g, timelineTrackRect, true);

                float frameWidth = currentClip.TotalFrames > 0 ? (float)timelineTrackRect.Width / currentClip.TotalFrames : 1.0f;
                int step = Math.Max(1, currentClip.TotalFrames / 6);
                for (int i = 0; i <= currentClip.TotalFrames; i += step)
                {
                    int x = timelineTrackRect.Left + (int)(i * frameWidth);
                    g.DrawLine(Pens.Gray, x, timelineTrackRect.Top + 2, x, timelineTrackRect.Top + 8);
                    g.DrawString(i.ToString(), font, Brushes.Black, x - 10, timelineTrackRect.Top + 10);
                }

                if (currentClip.Keyframes != null)
                {
                    foreach (var kf in currentClip.Keyframes)
                    {
                        int kx = timelineTrackRect.Left + (int)(kf.FrameIndex * frameWidth);
                        int ky = timelineTrackRect.Bottom - 16;
                        g.FillRectangle(Brushes.Navy, kx - 3, ky - 3, 6, 6);
                    }
                }

                int playX = timelineTrackRect.Left + (int)(currentFrame * frameWidth);
                g.DrawLine(Pens.Red, playX, timelineTrackRect.Top + 2, playX, timelineTrackRect.Bottom - 2);
                g.DrawString($"Frame: {currentFrame}/{currentClip.TotalFrames}", boldFont, Brushes.Navy, timelineTrackRect.Right - 110, timelineTrackRect.Top + 4);
            }
        }

        private void Draw90sToolbox(Graphics g)
        {
            using (Font font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Regular))
            using (Font boldFont = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold))
            using (Brush bgBrush = new SolidBrush(Color.FromArgb(192, 192, 192)))
            {
                Rectangle toolboxRect = new Rectangle(10, 25, 310, 820);
                g.FillRectangle(bgBrush, toolboxRect);
                DrawWin95Bevel(g, toolboxRect, false);

                DrawGroupBox(g, new Rectangle(14, 30, 287, 92), "EDIT & TRANSFORM GIZMOS");
                DrawWin95Button(g, btnMove, "1. Move", CurrentTool == EditorTool.Move);
                DrawWin95Button(g, btnRotate, "2. Rotate", CurrentTool == EditorTool.Rotate);
                DrawWin95Button(g, btnScale, "3. Scale", CurrentTool == EditorTool.Scale);
                DrawWin95Button(g, btnPaint, "4. Paint", CurrentTool == EditorTool.FacePaint);
                DrawWin95Button(g, btnColl, "5. Collision", CurrentTool == EditorTool.Collision);
                DrawWin95Button(g, btnDel, "6. Delete", CurrentTool == EditorTool.Delete);
                DrawWin95Button(g, btnToggleSpace, $"Space: {CurrentGizmoSpace}", false);

                DrawGroupBox(g, new Rectangle(14, 130, 287, 118), "SCENE HIERARCHY & PARENTING");
                g.FillRectangle(Brushes.White, hierarchyBox);
                DrawWin95Bevel(g, hierarchyBox, true);

                var treeItems = GetHierarchyDisplayList();
                for (int i = 0; i < treeItems.Count; i++)
                {
                    int itemY = hierarchyBox.Top + 4 + (i * 16);
                    if (itemY + 16 < hierarchyBox.Bottom)
                    {
                        Rectangle itemRect = new Rectangle(hierarchyBox.Left + 2, itemY, hierarchyBox.Width - 4, 16);
                        var item = treeItems[i];
                        string indent = new string(' ', item.depth * 3) + (item.depth > 0 ? "\\-- " : "");
                        string displayText = $"{indent}{item.mesh.Name}";

                        if (item.mesh == selectedMesh)
                        {
                            g.FillRectangle(Brushes.Navy, itemRect);
                            g.DrawString(displayText, font, Brushes.White, itemRect.Left + 2, itemRect.Top + 1);
                        }
                        else
                        {
                            g.DrawString(displayText, font, Brushes.Black, itemRect.Left + 2, itemRect.Top + 1);
                        }
                    }
                }

                DrawWin95Button(g, btnMarkParent, "Mark Parent", markedParentMesh == selectedMesh && selectedMesh != null);
                DrawWin95Button(g, btnAttachChild, "Attach Child", false);
                DrawWin95Button(g, btnUnparent, "Unparent", false);

                DrawGroupBox(g, new Rectangle(14, 256, 287, 82), "OBJECT INSPECTOR WINDOW");
                g.FillRectangle(Brushes.White, inspectorBox);
                DrawWin95Bevel(g, inspectorBox, true);

                string objName = selectedMesh != null ? selectedMesh.Name : "None Selected";
                string parentTxt = selectedMesh?.Parent != null ? $" [Child of {selectedMesh.Parent.Name}]" : "";
                g.DrawString($"Name: {objName}{parentTxt}", boldFont, Brushes.Black, inspectorBox.Left + 6, inspectorBox.Top + 6);

                if (selectedMesh != null)
                {
                    g.DrawString($"X:{selectedMesh.Position.X,5:F1}", font, Brushes.Black, inspectorBox.Left + 6, inspectorBox.Top + 24);
                    DrawWin95Button(g, btnInspectorSubX, "-", false);
                    DrawWin95Button(g, btnInspectorAddX, "+", false);

                    g.DrawString($"Y:{selectedMesh.Position.Y,5:F1}", font, Brushes.Black, inspectorBox.Left + 101, inspectorBox.Top + 24);
                    DrawWin95Button(g, btnInspectorSubY, "-", false);
                    DrawWin95Button(g, btnInspectorAddY, "+", false);

                    g.DrawString($"Z:{selectedMesh.Position.Z,5:F1}", font, Brushes.Black, inspectorBox.Left + 196, inspectorBox.Top + 24);
                    DrawWin95Button(g, btnInspectorSubZ, "-", false);
                    DrawWin95Button(g, btnInspectorAddZ, "+", false);
                }
                else
                {
                    g.DrawString("Select an object in hierarchy", font, Brushes.Gray, inspectorBox.Left + 6, inspectorBox.Top + 26);
                }

                DrawGroupBox(g, new Rectangle(14, 346, 287, 38), "IMAGE FACE TEXTURE");
                DrawWin95Button(g, btnLoadTex, LoadedImageTexture != null ? "Texture Loaded ✓" : "Load Image...", false);
                DrawWin95Button(g, btnClearTex, "Clear Texture", false);

                DrawGroupBox(g, scriptAttachBox, "POLONIUM SCRIPT (.pscript)");
                bool scriptActive = selectedMesh != null && selectedMesh.ScriptEnabled;
                DrawWin95Button(g, btnToggleScript, scriptActive ? "Script: Active ✓" : "Attach Script", scriptActive);
                DrawWin95Button(g, btnOpenCodeView, "Open Code View", CurrentViewTab == ViewTab.CodeEditor);

                DrawGroupBox(g, new Rectangle(14, 460, 287, 60), "SPAWN ENTITIES");
                DrawWin95Button(g, btnSpawnCube, "Cube", false);
                DrawWin95Button(g, btnSpawnPyr, "Pyramid", false);
                DrawWin95Button(g, btnSpawnFighter, "Fighter", false);
                DrawWin95Button(g, btnSpawnCamera, "Spawn Camera Object", false);

                DrawGroupBox(g, new Rectangle(14, 524, 287, 42), "GAME SIMULATOR");
                DrawWin95Button(g, btnPlayEditor, "Editor [F1]", CurrentGameMode == GameMode.Editor);
                DrawWin95Button(g, btnPlayFlight, "Flight [F2]", CurrentGameMode == GameMode.FlightGame);
                DrawWin95Button(g, btnPlayPlatform, "Platform [F3]", CurrentGameMode == GameMode.PlatformerGame);

                DrawGroupBox(g, new Rectangle(14, 570, 287, 48), "SUPER FX PALETTE");
                for (int i = 0; i < PaletteColors.Length; i++)
                {
                    int bx = 22 + (i % 8) * 33, by = 584 + (i / 8) * 18;
                    Rectangle colorBox = new Rectangle(bx, by, 28, 16);
                    using (Brush b = new SolidBrush(PaletteColors[i])) g.FillRectangle(b, colorBox);
                    if (PaletteColors[i].ToArgb() == CurrentPaintColor.ToArgb())
                        g.DrawRectangle(Pens.Black, colorBox.X - 1, colorBox.Y - 1, colorBox.Width + 2, colorBox.Height + 2);
                    else DrawWin95Bevel(g, colorBox, false);
                }

                DrawGroupBox(g, new Rectangle(14, 624, 287, 182), "CONSOLE LOG");
                g.FillRectangle(Brushes.Black, 20, 640, 275, 160);
                g.DrawString("SUPFX_OS v1.57.1 [Null-Safe Fix Added]\n> Added defensive checks for meshes,\n  cameras, and triangles to prevent\n  NullReferenceException.", new Font("Consolas", 8), Brushes.LimeGreen, 24, 644);

                Rectangle statusRect = new Rectangle(0, ClientSize.Height - 22, ClientSize.Width, 22);
                using (Brush bg = new SolidBrush(Color.FromArgb(192, 192, 192)))
                {
                    g.FillRectangle(bg, statusRect);
                }
                g.DrawLine(Pens.White, 0, ClientSize.Height - 22, ClientSize.Width, ClientSize.Height - 22);
                string statusText = isGizmoDragging ? $"Mode: {CurrentGameMode} | GRAB ACTIVE ({activeGizmoAxis}-Axis - {CurrentGizmoSpace})" : $"Tab: {CurrentViewTab} | Mode: {CurrentGameMode} | Tool: {CurrentTool}";
                g.DrawString(statusText, font, Brushes.Black, 10, ClientSize.Height - 19);
            }
        }

        private void DrawWin95Button(Graphics g, Rectangle rect, string text, bool isPressed)
        {
            using (Brush bg = new SolidBrush(Color.FromArgb(192, 192, 192))) { g.FillRectangle(bg, rect); }
            if (isPressed)
            {
                g.DrawRectangle(Pens.Gray, rect);
                g.DrawLine(Pens.White, rect.Right, rect.Top, rect.Right, rect.Bottom);
                g.DrawLine(Pens.White, rect.Left, rect.Bottom, rect.Right, rect.Bottom);
                rect.Offset(1, 1);
            }
            else DrawWin95Bevel(g, rect, false);

            using (Font f = new Font("Microsoft Sans Serif", 7.5f))
            using (StringFormat sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString(text, f, Brushes.Black, rect, sf);
            }
        }

        private void DrawWin95Bevel(Graphics g, Rectangle rect, bool inset)
        {
            Color light = inset ? Color.FromArgb(128, 128, 128) : Color.White;
            Color dark = inset ? Color.White : Color.FromArgb(128, 128, 128);
            Color darkShadow = inset ? Color.FromArgb(64, 64, 64) : Color.FromArgb(64, 64, 64);

            g.DrawLine(new Pen(light), rect.Left, rect.Top, rect.Right - 1, rect.Top);
            g.DrawLine(new Pen(light), rect.Left, rect.Top, rect.Left, rect.Bottom - 1);
            g.DrawLine(new Pen(dark), rect.Right - 1, rect.Top, rect.Right - 1, rect.Bottom - 1);
            g.DrawLine(new Pen(dark), rect.Left, rect.Bottom - 1, rect.Right - 1, rect.Bottom - 1);

            if (!inset)
            {
                g.DrawLine(new Pen(darkShadow), rect.Right, rect.Top, rect.Right, rect.Bottom);
                g.DrawLine(new Pen(darkShadow), rect.Left, rect.Bottom, rect.Right, rect.Bottom);
            }
        }

        private void DrawGroupBox(Graphics g, Rectangle rect, string title)
        {
            DrawWin95Bevel(g, rect, true);
            using (Font f = new Font("Microsoft Sans Serif", 8, FontStyle.Bold))
            using (Brush bg = new SolidBrush(Color.FromArgb(192, 192, 192)))
            {
                SizeF size = g.MeasureString(title, f);
                g.FillRectangle(bg, rect.Left + 10, rect.Top - 6, size.Width + 6, 12);
                g.DrawString(title, f, Brushes.Navy, rect.Left + 12, rect.Top - 8);
            }
        }

        [DllImport("user32.dll")]
        private static extern bool PeekMessage(out NativeMessage lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax, uint wRemoveMsg);
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMessage { public IntPtr hWnd; public uint msg; public IntPtr wParam; public IntPtr lParam; public uint time; public Point pt; }
        private bool NativeMessageWaiting() => PeekMessage(out _, IntPtr.Zero, 0, 0, 0);
    }

    public static class Prompt
    {
        public static string ShowDialog(string text, string caption, string defaultValue)
        {
            Form prompt = new Form()
            {
                Width = 320,
                Height = 180,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };
            Label textLabel = new Label() { Left = 20, Top = 15, Text = text, Width = 260 };
            TextBox textBox = new TextBox() { Left = 20, Top = 40, Width = 260, Height = 40, Text = defaultValue, Multiline = true, ScrollBars = ScrollBars.Vertical };
            Button confirmation = new Button() { Text = "OK", Left = 200, Width = 80, Top = 100, DialogResult = DialogResult.OK };
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text : defaultValue;
        }
    }

    internal class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SuperFXForm());
        }
    }
}