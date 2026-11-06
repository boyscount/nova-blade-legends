using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaBlade
{
    // The Sakura Garden city: a safe walkable area with three NPCs and the gate to the stages. No enemies, no attacks.
    public class City : MonoBehaviour
    {
        class Spot { public string id, name, verb, panel, model; public bool female; public Vector3 pos; public float yaw; public Transform go; public Animator anim; }
        public static readonly Vector3 Start = new(0, 0, 13), Gate = new(-8.5f, 0, -14.5f);
        Spot[] spots; const float TalkRange = 3.4f, TurnSpeed = 4;
        Spot near; bool spawned; Transform petals;
        Main G => Main.G;

        void Awake()
        {
            spots = new[]
            {
                new Spot { id = "quest", name = Loc.S("ผู้ดูแลเควสต์", "Quest Keeper"), verb = Loc.S("คุย", "TALK"), panel = "quest", model = "npc_quest", female = true, pos = new Vector3(-14, 0, 2), yaw = 75 },
                new Spot { id = "shop", name = Loc.S("พ่อค้า", "Merchant"), verb = Loc.S("ซื้อของ", "SHOP"), panel = "shop", model = "npc_merchant", pos = new Vector3(0, 0, -15.5f), yaw = 0 },
                new Spot { id = "smith", name = Loc.S("ช่างตีอาวุธ", "Blacksmith"), verb = Loc.S("ตีบวก", "ENHANCE"), panel = "bag", model = "npc_smith", pos = new Vector3(14, 0, 2), yaw = -75 },
                new Spot { id = "gate", name = Loc.S("ประตูเมือง", "City Gate"), verb = Loc.S("ออกเดินทาง", "TRAVEL"), panel = "gate", pos = Gate },
            };
        }
        public void Enter()
        {
            if (spawned) return; spawned = true;
            foreach (var s in spots)
            {
                if (s.model == null) continue;
                var pf = Resources.Load<GameObject>("Heroes/Hero_" + s.model); if (!pf) { Debug.LogError("[City] no model " + s.model); continue; }
                var go = Instantiate(pf, s.pos, Quaternion.Euler(0, s.yaw, 0), G.cityRoot); go.name = s.model; s.go = go.transform;
                s.anim = go.GetComponentInChildren<Animator>();
                if (s.anim && Resources.Load<RuntimeAnimatorController>("Anim/warrior" + (s.female ? "_f" : "_m")) is RuntimeAnimatorController c) { s.anim.runtimeAnimatorController = c; s.anim.SetFloat("Speed", 1); s.anim.Play("show", 0, Random.value); }
            }
            petals = LobbyNight.PetalsAt(G.cityRoot);
            FallenPetals(); WarpBeam();
        }
        // a tall column of pink light over the gate, seen from anywhere in the square: this way to the fight
        Material beam; const float BeamH = 20, BeamW = 4.6f;
        void WarpBeam()
        {
            const int W = 32, H = 64; var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {   // bright core down the middle, gone at the sides, fading out towards the top
                float u = Mathf.Abs((x + .5f) / W * 2 - 1), v = (y + .5f) / H, core = Mathf.Pow(1 - u, 2.6f), up = Mathf.Pow(1 - v, 1.4f) * Mathf.Clamp01(v * 14);
                t.SetPixel(x, y, new Color(1, 1, 1, core * up));
            }
            t.Apply(false);
            var verts = new[] { new Vector3(-BeamW / 2, 0, 0), new Vector3(BeamW / 2, 0, 0), new Vector3(BeamW / 2, BeamH, 0), new Vector3(-BeamW / 2, BeamH, 0), new Vector3(0, 0, -BeamW / 2), new Vector3(0, 0, BeamW / 2), new Vector3(0, BeamH, BeamW / 2), new Vector3(0, BeamH, -BeamW / 2) };
            var uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            var mesh = new Mesh { name = "WarpBeam", vertices = verts, uv = uv, triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6, 4, 5, 6, 4, 6, 7 } }; mesh.RecalculateBounds();
            beam = new Material(Catalog.I.glowMat) { mainTexture = t, renderQueue = 3100 }; beam.SetFloat("_SrcBlend", 1); beam.SetFloat("_DstBlend", 1); beam.SetFloat("_UseVC", 0);
            var go = new GameObject("WarpBeam"); go.transform.SetParent(G.cityRoot, false); go.transform.position = Gate; go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = beam; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // petals lying on the square: one mesh of small flat quads with the same picture
        void FallenPetals()
        {
            var sheet = Resources.Load<Texture2D>("UI/City/petal_sheet"); if (!sheet) return;
            var trees = G.cityRoot.Cast<Transform>().Where(t => t.name.StartsWith("SmallTreeRound")).Select(t => t.position).ToArray();
            const int N = 3200; var v = new Vector3[N * 4]; var uv = new Vector2[N * 4]; var tri = new int[N * 6]; var rng = new System.Random(5);
            float R() => (float)rng.NextDouble();
            for (int i = 0; i < N; i++)
            {
                float a = R() * Mathf.PI * 2, r = Mathf.Sqrt(R()) * 21, x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r, s = .09f + R() * .1f, turn = R() * Mathf.PI * 2, cs = Mathf.Cos(turn) * s, sn = Mathf.Sin(turn) * s; int fr = rng.Next(4); float u = (fr % 2) * .5f, w = (fr / 2) * .5f;
                if (i % 10 < 7 && trees.Length > 0)
                {   // most lie in a soft circle under the trees, thinning out away from the trunk
                    var t0 = trees[rng.Next(trees.Length)]; float ga = R() * Mathf.PI * 2, gr = (R() + R()) * .5f * 4.6f; x = t0.x + Mathf.Cos(ga) * gr; z = t0.z + Mathf.Sin(ga) * gr;
                }
                else if (r < 9.3f) { float g = 9.3f + R() * 2; x = Mathf.Cos(a) * g; z = Mathf.Sin(a) * g; }   // the rest are scattered, not on the raised stone in the middle
                int k = i * 4; bool onGrass = x * x + z * z > 21 * 21; float y = (onGrass ? .3f + R() * .08f : .05f + R() * .02f);   // beyond the square they rest on top of the grass
                v[k] = new Vector3(x - cs + sn, y, z - sn - cs); v[k + 1] = new Vector3(x + cs + sn, y, z + sn - cs); v[k + 2] = new Vector3(x + cs - sn, y, z + sn + cs); v[k + 3] = new Vector3(x - cs - sn, y, z - sn + cs);
                uv[k] = new Vector2(u, w); uv[k + 1] = new Vector2(u + .5f, w); uv[k + 2] = new Vector2(u + .5f, w + .5f); uv[k + 3] = new Vector2(u, w + .5f);
                int t = i * 6; tri[t] = k; tri[t + 1] = k + 2; tri[t + 2] = k + 1; tri[t + 3] = k; tri[t + 4] = k + 3; tri[t + 5] = k + 2;
            }
            var mesh = new Mesh { name = "FallenPetals", vertices = v, uv = uv, triangles = tri }; mesh.RecalculateBounds();
            var mat = new Material(Catalog.I.glowMat) { mainTexture = sheet, renderQueue = 2950 }; mat.SetFloat("_SrcBlend", 5); mat.SetFloat("_DstBlend", 10); mat.SetFloat("_UseVC", 0); mat.SetColor("_Color", new Color(1, .86f, .9f, .95f));
            var go = new GameObject("FallenPetals"); go.transform.SetParent(G.cityRoot, false); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void Update()
        {
            if (!G || !G.city || !G.hero || G.ui.CityPanelOpen) { if (near != null) { near = null; if (G && G.ui) G.ui.SetCityPrompt(null, null); } return; }
            Spot best = null; float bd = TalkRange;
            foreach (var s in spots) { float d = Main.Flat(s.pos - G.hero.Pos).magnitude; if (d < bd) { bd = d; best = s; } }
            if (best != near) { near = best; if (best != null) { var open = best.panel; G.ui.SetCityPrompt($"{best.verb} · {best.name}", () => G.ui.OpenCityPanel(open)); } else G.ui.SetCityPrompt(null, null); }
            if (near != null && near.go)
            {   // the person near you turns to you
                var to = Main.Flat(G.hero.Pos - near.pos); if (to.sqrMagnitude > .01f) near.go.rotation = Quaternion.Slerp(near.go.rotation, Quaternion.LookRotation(to), Time.deltaTime * TurnSpeed);
            }
            var kb = Keyboard.current; if (near != null && kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) G.ui.OpenCityPanel(near.panel);
        }
        void LateUpdate()
        {
            if (!G || !G.city) return; if (petals) petals.position = G.hero.Pos + new Vector3(0, 6, -2);
            if (beam) beam.SetColor("_Color", new Color(1, .3f, .62f, 1) * (.3f + .07f * Mathf.Sin(Time.time * 2.1f)));
        }
    }
}
