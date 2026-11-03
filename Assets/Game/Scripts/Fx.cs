using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Short-lived combat effects: CartoonFX prefabs plus procedural rings, slash arcs, ground telegraphs and sparks.
    public class Fx : MonoBehaviour
    {
        static Fx I;
        Material add, alpha;
        ParticleSystem sparks, dust;
        class Timed { public GameObject go; public float t, life; public System.Action<Timed, float> upd; public Material mat; }
        readonly List<Timed> live = new();

        public static void Init(Transform parent)
        {
            var go = new GameObject("Fx"); go.transform.SetParent(parent); I = go.AddComponent<Fx>();
            var sh = Catalog.I.glowMat.shader;
            I.add = new Material(sh); I.add.SetFloat("_SrcBlend", 1); I.add.SetFloat("_DstBlend", 1);
            I.alpha = new Material(sh); I.alpha.SetFloat("_SrcBlend", 1); I.alpha.SetFloat("_DstBlend", 10);
            // sparks: one particle system fed with Emit()
            var pgo = new GameObject("Sparks"); pgo.transform.SetParent(go.transform);
            I.sparks = pgo.AddComponent<ParticleSystem>();
            I.sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = I.sparks.main; main.loop = false; main.playOnAwake = false; main.maxParticles = 2000; main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 1.2f;
            var em = I.sparks.emission; em.enabled = false; var shp = I.sparks.shape; shp.enabled = false;
            var col = I.sparks.colorOverLifetime; col.enabled = true;
            var gr = new Gradient(); gr.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) }); col.color = gr;
            var sz = I.sparks.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, .2f));
            var pr = pgo.GetComponent<ParticleSystemRenderer>(); var pm = new Material(I.add) { mainTexture = Soft() }; pr.sharedMaterial = pm;
            // dust: soft alpha-blended puffs that grow and fade (spawns, deaths, dashes) instead of cartoon smoke
            var dgo = new GameObject("Dust"); dgo.transform.SetParent(go.transform);
            I.dust = dgo.AddComponent<ParticleSystem>();
            I.dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var dm = I.dust.main; dm.loop = false; dm.playOnAwake = false; dm.maxParticles = 600; dm.simulationSpace = ParticleSystemSimulationSpace.World; dm.gravityModifier = -.05f;
            var dem = I.dust.emission; dem.enabled = false; var dsh = I.dust.shape; dsh.enabled = false;
            var dl = I.dust.limitVelocityOverLifetime; dl.enabled = true; dl.limit = .4f; dl.dampen = .12f;
            var dc = I.dust.colorOverLifetime; dc.enabled = true;
            var dg = new Gradient(); dg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); dc.color = dg;
            var ds = I.dust.sizeOverLifetime; ds.enabled = true; ds.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .4f, 1, 1.6f));
            var drot = I.dust.rotationOverLifetime; drot.enabled = true; drot.z = new ParticleSystem.MinMaxCurve(-.6f, .6f);
            dgo.GetComponent<ParticleSystemRenderer>().sharedMaterial = new Material(I.alpha) { mainTexture = Soft() };
        }

        static Texture2D Soft()
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f; float a = Mathf.Clamp01(1 - d); a *= a; t.SetPixel(x, y, new Color(1, 1, 1, a)); }
            t.Apply(); return t;
        }

        // ---------- CartoonFX ----------
        // combat effects are drawn procedurally (soft sparks, rings, dust): the cartoon prefabs' outlined smoke puffs read as dark purple blobs
        public static GameObject Spawn(string id, Vector3 pos, float scale = 1, float life = 3, Quaternion? rot = null)
        {
            switch (id)
            {
                case "hit_red": Burst(pos, 10, new Color(1, .35f, .35f), 4, .35f); return null;
                case "hit_yellow": Burst(pos, 14, new Color(1, .85f, .45f), 5, .35f); Ring(pos - Vector3.up * pos.y, .8f * scale, new Color(1, .85f, .45f), .25f, .2f); return null;
                case "ground_hit": Dust(pos, .7f * scale, 8); Ring(pos, 1.6f * scale, new Color(1, .9f, .7f), .35f, .12f); return null;
                case "explosion": Burst(pos, 20, new Color(1, .6f, .3f), 5 * scale, .45f); Dust(pos, .6f * scale, 6, new Color(.45f, .4f, .36f, .4f)); return null;
                case "electric": Burst(pos, 16, new Color(.55f, .95f, 1), 4, .3f, 0, .1f); return null;
                case "aura": case "wind": case "souls": case "poof": case "death": case "boss_death": case "fire_small": return null;
            }
            var pf = Catalog.I.Fx(id); if (!pf) return null;
            var go = Instantiate(pf, pos, rot ?? Quaternion.identity);
            go.transform.localScale *= scale;
            Destroy(go, life);
            return go;
        }

        // ---------- sparks ----------
        public static void Burst(Vector3 p, int n, Color c, float speed = 5, float life = .5f, float grav = 1.2f, float size = .14f, bool flat = false)
        {
            if (!I) return;
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3(Random.Range(-1f, 1f), flat ? Random.Range(0, .3f) : Random.Range(-.2f, 1.2f), Random.Range(-1f, 1f)).normalized * speed * Random.Range(.3f, 1f);
                ep.position = p + Random.insideUnitSphere * .1f; ep.velocity = v; ep.startLifetime = life * Random.Range(.6f, 1.4f);
                ep.startSize = size * Random.Range(.6f, 1.4f); ep.startColor = c * 2.2f;
                I.sparks.Emit(ep, 1);
            }
            var main = I.sparks.main; main.gravityModifier = grav;
        }

        // low, earthy dust cloud on the ground (size = radius in metres)
        public static void Dust(Vector3 p, float size = 1, int n = 10, Color? c = null)
        {
            if (!I) return; var col = c ?? new Color(.42f, .38f, .32f, .55f);
            var ep = new ParticleSystem.EmitParams();
            for (int i = 0; i < n; i++)
            {
                var d = Random.insideUnitCircle; var v = new Vector3(d.x, Random.Range(.05f, .35f), d.y) * size * Random.Range(1.2f, 2.6f);
                ep.position = p + new Vector3(d.x, 0, d.y) * size * .3f + Vector3.up * Random.Range(.1f, .4f) * size; ep.velocity = v;
                ep.startLifetime = Random.Range(.9f, 1.5f); ep.startSize = size * Random.Range(.9f, 1.5f); ep.rotation = Random.Range(0, 360f);
                var cc = col * Random.Range(.85f, 1.1f); cc.a = col.a; ep.startColor = cc;
                I.dust.Emit(ep, 1);
            }
        }

        // ---------- procedural meshes ----------
        static Mesh Sector(float r0, float r1, float start, float span, int seg = 32)
        {
            var m = new Mesh(); var v = new List<Vector3>(); var c = new List<Color>(); var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = start + span * i / seg; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(d * r0); v.Add(d * r1);
                float edge = Mathf.Sin(Mathf.PI * i / seg);
                c.Add(new Color(1, 1, 1, r0 > 0 ? 0 : 1)); c.Add(new Color(1, 1, 1, edge));
                if (i < seg) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            }
            m.SetVertices(v); m.SetColors(c); m.SetTriangles(tri, 0); return m;
        }
        Timed Add(Mesh mesh, Material mat, Color col, Vector3 pos, float yaw, float life, System.Action<Timed, float> upd)
        {
            var go = new GameObject("fx"); go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var m = new Material(mat); m.SetColor("_Color", col); go.AddComponent<MeshRenderer>().sharedMaterial = m;
            var t = new Timed { go = go, life = life, upd = upd, mat = m }; live.Add(t); return t;
        }
        public static void Ring(Vector3 p, float r, Color c, float life = .5f, float w = .18f)
        {
            if (!I) return; var col = c * 2;
            I.Add(Sector(1 - w, 1, 0, Mathf.PI * 2, 48), I.add, col, p + Vector3.up * .06f, 0, life, (t, k) =>
            { float s = .3f + r * Mathf.Sqrt(k); t.go.transform.localScale = new Vector3(s, 1, s); t.mat.SetColor("_Color", col * (1 - k)); });
        }
        public static void Slash(Vector3 p, float face, Color c, float r = 2, float span = 2.4f, float life = .22f)
        {
            if (!I) return; var col = c * 1.5f; col.a = .8f;
            I.Add(Sector(r * .5f, r, -span / 2, span), I.add, col, p, face, life, (t, k) =>
            { t.mat.SetColor("_Color", col * (1 - k)); float s = 1 + k * .25f; t.go.transform.localScale = new Vector3(s, 1, s); });
        }
        public static void Telegraph(Vector3 p, float r, float dur, float face = 0, float half = Mathf.PI)
        {
            if (!I) return; var red = new Color(1, .16f, .12f, .32f);
            I.Add(Sector(0, r, -half, half * 2, 40), I.alpha, red * new Color(1, 1, 1, .5f), p + Vector3.up * .05f, face, dur, (t, k) => t.mat.SetColor("_Color", new Color(1, .16f, .12f, .14f + .08f * Mathf.Sin(k * 20))));
            I.Add(Sector(0, r, -half, half * 2, 40), I.alpha, red, p + Vector3.up * .055f, face, dur, (t, k) => t.go.transform.localScale = Vector3.one * Mathf.Max(.01f, k));
        }
        public static void Beam(Vector3 a, Vector3 b, Color c, float life = .15f, float w = .08f)
        {
            if (!I) return; var go = new GameObject("beam"); var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2; lr.SetPosition(0, a); lr.SetPosition(1, b); lr.widthMultiplier = w * 2; lr.sharedMaterial = new Material(I.add); lr.sharedMaterial.SetColor("_Color", c * 2.5f);
            I.live.Add(new Timed { go = go, life = life, upd = (t, k) => lr.widthMultiplier = w * 2 * (1 - k) });
        }
        public static GameObject Cylinder(Vector3 p, float rTop, float rBot, float h, Color c, float life, System.Action<GameObject, float> upd)
        {
            if (!I) return null;
            var m = new Mesh(); var v = new List<Vector3>(); var col = new List<Color>(); var tri = new List<int>(); int seg = 24;
            for (int i = 0; i <= seg; i++) { float a = Mathf.PI * 2 * i / seg; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); v.Add(d * rBot); v.Add(d * rTop + Vector3.up * h); col.Add(Color.white); col.Add(new Color(1, 1, 1, 0)); if (i < seg) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); } }
            m.SetVertices(v); m.SetColors(col); m.SetTriangles(tri, 0);
            var t = I.Add(m, I.add, c, p, 0, life, (tt, k) => upd?.Invoke(tt.go, k)); return t.go;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var t = live[i]; t.t += dt; float k = Mathf.Clamp01(t.t / t.life);
                if (t.go) t.upd?.Invoke(t, k);
                if (t.t >= t.life) { if (t.go) Destroy(t.go); if (t.mat) Destroy(t.mat); live.RemoveAt(i); }
            }
        }
        public static void Clear() { if (!I) return; foreach (var t in I.live) { if (t.go) Destroy(t.go); if (t.mat) Destroy(t.mat); } I.live.Clear(); I.sparks.Clear(); }
    }

    // Glowing ribbon between the sword's base and tip while swinging.
    public class SwordTrail : MonoBehaviour
    {
        public Transform tipT, baseT; public Color color = Color.cyan; public bool on;
        const int N = 14; readonly Vector3[] a = new Vector3[N], b = new Vector3[N]; readonly bool[] has = new bool[N];
        Mesh mesh; Vector3[] verts; Color[] cols; Material mat;
        void Awake()
        {
            mesh = new Mesh(); mesh.MarkDynamic(); verts = new Vector3[N * 2]; cols = new Color[N * 2]; var tri = new List<int>();
            for (int i = 0; i < N - 1; i++) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); }
            mesh.vertices = verts; mesh.colors = cols; mesh.SetTriangles(tri, 0);
            var go = new GameObject("trail"); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mat = new Material(Catalog.I.glowMat);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
        void LateUpdate()
        {
            for (int i = N - 1; i > 0; i--) { a[i] = a[i - 1]; b[i] = b[i - 1]; has[i] = has[i - 1]; }
            has[0] = on && tipT && baseT; if (has[0]) { a[0] = baseT.position; b[0] = tipT.position; }
            Vector3 fa = Vector3.down * 99, fb = fa; for (int i = 0; i < N; i++) if (has[i]) { fa = a[i]; fb = b[i]; break; }
            for (int i = 0; i < N; i++)
            {
                float k = has[i] ? (1 - i / (float)N) * 1.8f : 0;
                verts[i * 2] = has[i] ? a[i] : fa; verts[i * 2 + 1] = has[i] ? b[i] : fb;
                cols[i * 2] = color * k * .3f; cols[i * 2 + 1] = color * k;
            }
            mesh.vertices = verts; mesh.colors = cols; mesh.RecalculateBounds();
        }
    }
}
