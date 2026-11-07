using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Dense grass drawn with GPU instancing: a few curved-blade clump meshes scattered over the stage
    // (CozyFishing/Grass shader: wind waves, per-clump tint, light through blades). Bare on the dirt paths.
    // The field is cut into square cells: only cells in the camera's view are drawn, and far ones use a lighter clump with wider blades.
    [ExecuteAlways]
    public class GrassField : MonoBehaviour
    {
        public Material material;
        public Color baseCol = new(.06f, .14f, .04f), midCol = new(.22f, .38f, .1f), tipCol = new(.55f, .62f, .26f);
        public float radius = 30, height = .38f, spacing = .42f, pathClear = 1.1f;
        public int seed = 7;
        public bool river;   // skip the river channel (River stage)
        public bool wilds;   // the Open World zone: grass stands on its hills and stays off its stream and trail
        public bool meadow;   // short soft grass spread in a carpet, in drifts of colour, with patches of small flowers (four extra meshes)
        public float maxDistance; public float clearRadius; public Vector4 channel;   // a stage's own river to keep clear: z = x(offset) + sin(px * z(freq)) * y(amp), half width w (w = 0: none)   // keep a plaza of this radius free of grass
        readonly List<Mesh> meshes = new(), farMeshes = new();
        class Cell { public Bounds box; public readonly List<Matrix4x4>[] mats = { new(), new(), new(), new(), new(), new(), new(), new() }; }
        readonly Dictionary<(int, int), Cell> cells = new(); readonly List<Cell> cellList = new();
        const float CellSize = 8, FarDistance = 26;
        static readonly Plane[] planes = new Plane[6];
        RenderParams rp;
        public static string Stats => string.Join(" ", System.Linq.Enumerable.Select(FindObjectsByType<GrassField>(FindObjectsSortMode.None), g => { int n = 0; foreach (var c in g.cellList) foreach (var l in c.mats) n += l.Count; return $"{n} clumps {(g.meshes.Count > 0 ? g.meshes[0].triangles.Length / 3 : 0)}/{(g.farMeshes.Count > 0 ? g.farMeshes[0].triangles.Length / 3 : 0)} tris {g.cellList.Count} cells, last frame {g.lastTris / 1000}k tris"; }));
        long lastTris;
        public Mesh DebugMesh => meshes.Count > 0 ? meshes[0] : null;

        void Start() { if (!Crumb.Has("nograss")) Build(); }
        public void Build()
        {
            meshes.Clear(); farMeshes.Clear(); cells.Clear(); cellList.Clear();
            bool mobile = Main.IsPhone;
            var rnd = new System.Random(seed);
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var rndFar = new System.Random(seed + 1);
            if (meadow) { for (int v = 0; v < 4; v++) { meshes.Add(Clump(rnd, mobile ? 6 : 10, height, v)); farMeshes.Add(Clump(rndFar, 3, height, v, 2)); } foreach (var fc in FlowerColors) { var fm = Flowers(rnd, fc, mobile ? 3 : 5); meshes.Add(fm); farMeshes.Add(fm); } }
            else for (int v = 0; v < 4; v++) { meshes.Add(Clump(rnd, mobile ? 11 : 18, height, v)); farMeshes.Add(Clump(rndFar, mobile ? 4 : 6, height, v, 2)); }
            float step = spacing * (mobile ? 1.35f : 1);
            for (float x = -radius; x < radius; x += step)
                for (float z = -radius; z < radius; z += step)
                {
                    float px = x + R(-step, step) * .45f, pz = z + R(-step, step) * .45f; float r = Mathf.Sqrt(px * px + pz * pz);
                    if (r > radius) continue;
                    float d = Path(px, pz) + Noise(px, pz) * .6f; if (d < pathClear) continue; if (clearRadius > 0 && Mathf.Sqrt(px * px + pz * pz) < clearRadius + Noise(px, pz) * 1.5f) continue; if (channel.w > 0 && Mathf.Abs(pz - (channel.x + Mathf.Sin(px * channel.z) * channel.y)) < channel.w) continue;
                    if (river && (River.Bed(px, pz) < -.08f || River.OnBridge(px, pz))) continue;
                    if (wilds && (!WildsMap.Dry(px, pz, -.6f) || Vector2.Distance(new Vector2(px, pz), WildsMap.House) < 8 || WildsMap.TrailDistance(px, pz) < 1.2f + Noise(px, pz) * .5f)) continue;
                    float gy = wilds ? WildsMap.Height(px, pz) : 0;
                    float clump = Noise(px * .12f, pz * .12f);
                    float s = R(.7f, 1.25f) * (.6f + .8f * clump) * Mathf.Clamp01((d - pathClear) / .8f + .3f);
                    var key = (Mathf.FloorToInt(px / CellSize), Mathf.FloorToInt(pz / CellSize));
                    if (!cells.TryGetValue(key, out var cell)) { cell = new Cell { box = new Bounds(new Vector3((key.Item1 + .5f) * CellSize, height, (key.Item2 + .5f) * CellSize), new Vector3(CellSize + 1, height * 3 + 1, CellSize + 1)) }; cells[key] = cell; cellList.Add(cell); }
                    if (wilds) { cell.box.Encapsulate(new Vector3(px, gy + height * 2, pz)); cell.box.Encapsulate(new Vector3(px, gy, pz)); }
                    cell.mats[meadow ? MeadowKind(px, pz, R(0, 1)) : rnd.Next(4)].Add(Matrix4x4.TRS(new Vector3(px, gy, pz), Quaternion.Euler(0, R(0, 360), 0), new Vector3(s, s * R(.8f, 1.25f), s)));
                }
            rp = new RenderParams(material) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false, lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes, worldBounds = new Bounds(Vector3.zero, Vector3.one * radius * 3) };
        }
        public static float Path(float x, float z) => Mathf.Min(Mathf.Abs(Mathf.Sqrt(x * x + z * z) - 11), Mathf.Abs(x + Mathf.Sin(z * .16f) * 1.6f));
        static float Noise(float x, float z) => Mathf.PerlinNoise(x * .37f + 11.3f, z * .37f + 7.1f);

        static readonly Color[] FlowerColors = { new(1, .34f, .68f), new(1, .86f, .26f), new(.66f, .4f, 1), new(1, .62f, .84f) };
        // which mesh a meadow clump uses: colour drifts follow a slow noise, flowers grow in their own patches, each patch mostly one colour
        static int MeadowKind(float x, float z, float dice)
        {
            float patch = Mathf.PerlinNoise(x * .09f + 40, z * .09f + 17);
            if (patch > .56f && dice < (patch - .56f) * 1.6f) return 4 + Mathf.FloorToInt(Mathf.Repeat(Mathf.PerlinNoise(x * .03f + 9, z * .03f + 3) * 11, 1) * 3.999f);
            return Mathf.Clamp(Mathf.FloorToInt(Mathf.PerlinNoise(x * .05f + 3, z * .05f + 31) * 6 - 1), 0, 3);
        }
        // a few thin stems, each topped with a small flat bloom facing up and out
        Mesh Flowers(System.Random rnd, Color bloom, int count)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int f = 0; f < count; f++)
            {
                float a = R(0, Mathf.PI * 2), h = height * R(1.1f, 1.7f), r = R(.07f, .13f); var root = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * R(0, .24f); var top = root + Vector3.up * h; var side = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * .009f;
                int i0 = v.Count; v.Add(root - side); v.Add(root + side); v.Add(top - side); v.Add(top + side); for (int k = 0; k < 4; k++) n.Add(Vector3.up);
                var stem = midCol; stem.a = 0; c.Add(stem); c.Add(stem); stem.a = 1; c.Add(stem); c.Add(stem); t.AddRange(new[] { i0, i0 + 1, i0 + 2, i0 + 1, i0 + 3, i0 + 2 });
                int i1 = v.Count; const int Petals = 5; var col = bloom; col.a = 1; v.Add(top + Vector3.up * .012f); n.Add(Vector3.up); c.Add(Color.Lerp(col, new Color(1, .9f, .35f, 1), .75f));
                for (int k = 0; k < Petals * 2; k++) { float ang = a + k * Mathf.PI / Petals, rr = k % 2 == 0 ? r : r * .45f; v.Add(top + new Vector3(Mathf.Cos(ang) * rr, 0, Mathf.Sin(ang) * rr)); n.Add(Vector3.up); c.Add(col); }
                for (int k = 0; k < Petals * 2; k++) t.AddRange(new[] { i1, i1 + 1 + (k + 1) % (Petals * 2), i1 + 1 + k });
            }
            var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }
        Mesh Clump(System.Random rnd, int blades, float h, int variant, float wide = 1)
        {
            float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);
            var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
            for (int b = 0; b < blades; b++)
            {
                float a = R(0, Mathf.PI * 2), lean = R(.1f, .45f), len = h * R(.55f, 1.25f), w = R(.014f, .026f) * wide;
                // clump palettes: plain, sun-dried, cool, lush (meadow colour variety)
                var shade = variant == 1 ? new Color(1.15f, 1.08f, .75f) : variant == 2 ? new Color(.88f, 1.02f, .98f) : variant == 3 ? new Color(.92f, 1.12f, .85f) : Color.white; if (meadow) shade = variant == 1 ? new Color(1.4f, 1.2f, .55f) : variant == 2 ? new Color(.7f, 1, .9f) : variant == 3 ? new Color(1, 1.3f, .7f) : Color.white;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = new Vector3(-dir.z, 0, dir.x); var root = dir * R(0, meadow ? .24f : .09f);
                int baseI = v.Count; const int seg = 4;
                for (int i = 0; i <= seg; i++)
                {
                    float k = i / (float)seg; var p = root + dir * (lean * len * k * k) + Vector3.up * len * (k - lean * .3f * k * k);
                    float ww = w * (1 - k * .9f);
                    v.Add(p - side * ww); v.Add(p + side * ww);
                    var nm = Vector3.Cross(side, Vector3.up * len + dir * lean * len * 2 * k).normalized; n.Add(nm); n.Add(nm);
                    var col = (k < .5f ? Color.Lerp(baseCol, midCol, k * 2) : Color.Lerp(midCol, tipCol, (k - .5f) * 2)) * shade; col.a = k; c.Add(col); c.Add(col);
                    if (i < seg) { int q = baseI + i * 2; t.AddRange(new[] { q, q + 1, q + 2, q + 1, q + 3, q + 2 }); }
                }
            }
            var m = new Mesh(); m.SetVertices(v); m.SetNormals(n); m.SetColors(c); m.SetTriangles(t, 0); m.RecalculateBounds(); return m;
        }

        void Update() => DrawFrom(Camera.main);
        // everything, for picture cameras that look from somewhere else
        public void Draw() => DrawFrom(null);
        void DrawFrom(Camera cam)
        {
            if (meshes.Count == 0) Build();
            if (cam) GeometryUtility.CalculateFrustumPlanes(cam, planes);
            float far = FarDistance * FarDistance; var eye = cam ? cam.transform.position : Vector3.zero; lastTris = 0;
            foreach (var cell in cellList)
            {
                if (cam && !GeometryUtility.TestPlanesAABB(planes, cell.box)) continue;
                if (cam && maxDistance > 0 && (cell.box.center - eye).sqrMagnitude > maxDistance * maxDistance) continue;   // a wide zone: grass only around the player
                var set = cam && (cell.box.center - eye).sqrMagnitude > far ? farMeshes : meshes;
                for (int i = 0; i < set.Count; i++)
                {
                    var list = cell.mats[i]; lastTris += list.Count * (long)set[i].GetIndexCount(0) / 3;
                    for (int k = 0; k < list.Count; k += 1023) Graphics.RenderMeshInstanced(rp, set[i], 0, list, Mathf.Min(1023, list.Count - k), k);
                }
            }
        }
    }
}
