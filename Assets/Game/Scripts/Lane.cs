using UnityEngine;

namespace NovaBlade
{
    // Anything that can be fought: the player hero and every Foe (enemies, minions, AI heroes, towers, cores).
    public interface ITarget { Vector3 Pos { get; } float Rad { get; } bool Down { get; } int Team { get; } void Take(float dmg, Vector3 from); }

    // Lane battle (1v1 .. 3v3): one long lane, or three lanes (left / middle / right) with a jungle and a river between them.
    // Each lane has two towers per side; both layouts share one core per side. Team 0 (the player's) holds the near end (+Z).
    public static class Lane
    {
        public static int Lanes = 1;                                   // 1 or 3, set when a match starts; 2 = the floating Sky Lanes map (SkyLanes)
        public static float HalfW => Lanes == 3 ? 30f : Lanes == 2 ? 42f : 7.2f;
        public const float HalfL = 44f;
        public static readonly float[] LaneX3 = { -22, 0, 22 };
        public static float LaneX(int lane) => Lanes == 3 ? LaneX3[Mathf.Clamp(lane, 0, 2)] : 0;
        // where a lane unit heads when it has nothing to fight: along its lane, then in to the enemy core near the far end
        public static Vector3 Goal(Vector3 pos, int team, int lane)
        {
            if (Lanes == 2) return SkyLanes.Goal(pos, team, lane);
            float side = Side(team), enemyEnd = -side * (HalfL + 5);
            if (Lanes == 1) return new Vector3(pos.x * .5f, 0, enemyEnd);
            bool nearEnemyBase = pos.z * side < -30;
            return nearEnemyBase ? new Vector3(0, 0, enemyEnd) : new Vector3(LaneX(lane), 0, pos.z - side * 8);
        }
        public static readonly float[] TowerZ = { 14, 28 };
        public const float CoreZ = 40;
        public const float SpawnZ = CoreZ - 9;   // heroes (re)spawn here, in front of their base
        public static float Side(int team) => team == 0 ? 1 : -1;   // +Z for team 0
        public static readonly FoeDef Tower = new() { id = "tower", model = "@tower", name = Loc.S("ป้อม", "Tower"), hp = 3200, spd = 0, dmg = 90, range = 8.5f, kind = "tower", scale = 1, rad = 1.7f, gold = 60 };
        public static readonly FoeDef Core = new() { id = "core", model = "@core", name = Loc.S("คริสตัลหลัก", "Core Crystal"), hp = 6000, spd = 0, dmg = 130, range = 8, kind = "tower", scale = 1, rad = 2.4f, gold = 0 };
        public static readonly FoeDef Minion = new() { id = "minion", model = "rig_minion", name = Loc.S("ทหาร", "Soldier"), hp = 380, spd = 3.0f, dmg = 32, range = 1.4f, kind = "melee", scale = .7f, rad = .33f, gold = 5 };   // 70% of a hero: soldiers read as small at a glance
        public static FoeDef Champ(HeroDef h) => new() { id = "champ", model = "hero_" + h.id, name = h.name, hp = h.hp * 1.1f, spd = h.spd * .85f, dmg = (h.ranged ? 50 : 62) * h.atk, range = h.ranged ? 6.5f : 2.2f, kind = "champ", scale = 1, rad = .5f, gold = 40, shoot = h.ranged };
        public static Vector3 CoreOf(int team) => new(0, 0, Side(team) * CoreZ);
        public const float FountainR = 9, FountainHeal = .08f;   // standing near your own core heals 8% of health a second
        // where a side's heroes (re)spawn and heal: Sky Lanes has a spawn pool behind each core, the other maps the ground in front of the base
        public static Vector3 SpawnOf(int team) => Lanes == 2 ? SkyLanes.Fountain(team) : new Vector3(0, 0, Side(team) * SpawnZ);
        public static Vector3 HealAt(int team) => Lanes == 2 ? SkyLanes.Fountain(team) : CoreOf(team);
        public static float HealR => Lanes == 2 ? SkyLanes.FountainR + .3f : FountainR;
        public static float HealRate => Lanes == 2 ? .15f : FountainHeal;
        // a flat ring lying on the ground, and an eight-sided gem (both made once)
        static Mesh gemMesh;
        // Sky Lanes' core: a stone dais with a large crystal of the side's colour turning over it, a halo, a strong light
        static GameObject SkyCore(GameObject root, int team)
        {
            var stone = new Material(Shader.Find("Universal Render Pipeline/Lit")); stone.SetColor("_BaseColor", new Color(.46f, .48f, .45f)); stone.SetFloat("_Smoothness", .1f);
            foreach (var (r, h, y) in new[] { (2.9f, .35f, .175f), (2.3f, .35f, .5f), (1.5f, .5f, .9f) })
            { var step = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(step.GetComponent<Collider>()); step.transform.SetParent(root.transform, false); step.transform.localPosition = Vector3.up * y; step.transform.localScale = new Vector3(r * 2, h / 2, r * 2); step.GetComponent<Renderer>().sharedMaterial = stone; }
            Color c = team == 0 ? new Color(.4f, .8f, 1.7f) : new Color(1.7f, .62f, .25f);
            var gem = new GameObject("crystal"); gem.transform.SetParent(root.transform, false); gem.transform.localPosition = Vector3.up * 3.3f; gem.transform.localScale = new Vector3(2.1f, 3.4f, 2.1f);
            gem.AddComponent<MeshFilter>().sharedMesh = Gem(); var gm = new Material(Catalog.I.glowMat); gm.SetFloat("_UseVC", 0); gm.SetFloat("_DstBlend", 0); gm.SetColor("_Color", c); gem.AddComponent<MeshRenderer>().sharedMaterial = gm; gem.AddComponent<Spin>();
            var ring = new GameObject("ring"); ring.transform.SetParent(root.transform, false); ring.transform.localPosition = Vector3.up * .37f; ring.AddComponent<MeshFilter>().sharedMesh = Annulus(2.6f, 2.95f);
            var rm = new Material(Catalog.I.glowMat); rm.SetFloat("_UseVC", 0); rm.SetFloat("_DstBlend", 1); var rc = TeamColor(team) * .5f; rc.a = 1; rm.SetColor("_Color", rc); ring.AddComponent<MeshRenderer>().sharedMaterial = rm;
            var gl = new GameObject("glow").AddComponent<Light>(); gl.transform.SetParent(root.transform, false); gl.transform.localPosition = Vector3.up * 3.3f; gl.type = LightType.Point; gl.color = TeamColor(team) / 2.2f; gl.intensity = 4; gl.range = 14;
            TopY = 4.2f; return root;
        }
        static Mesh Annulus(float r0, float r1)
        {
            const int seg = 48; var v = new Vector3[(seg + 1) * 2]; var t = new int[seg * 6];
            for (int i = 0; i <= seg; i++) { float a = -i / (float)seg * Mathf.PI * 2; v[i * 2] = new Vector3(Mathf.Cos(a) * r0, 0, Mathf.Sin(a) * r0); v[i * 2 + 1] = new Vector3(Mathf.Cos(a) * r1, 0, Mathf.Sin(a) * r1); if (i < seg) { int k = i * 6, b = i * 2; t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2; t[k + 3] = b + 1; t[k + 4] = b + 3; t[k + 5] = b + 2; } }
            var m = new Mesh { name = "Annulus", vertices = v, triangles = t }; m.RecalculateBounds(); return m;
        }
        static Mesh Gem()
        {
            if (gemMesh) return gemMesh;
            var v = new System.Collections.Generic.List<Vector3> { new(0, .7f, 0), new(0, -.7f, 0) }; for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; v.Add(new Vector3(Mathf.Cos(a) * .32f, 0, Mathf.Sin(a) * .32f)); }
            var t = new System.Collections.Generic.List<int>(); for (int i = 0; i < 6; i++) { int a = 2 + i, b = 2 + (i + 1) % 6; t.AddRange(new[] { 0, b, a, 1, a, b }); }
            gemMesh = new Mesh { name = "TowerGem", vertices = v.ToArray(), triangles = t.ToArray() }; gemMesh.RecalculateNormals(); gemMesh.RecalculateBounds(); return gemMesh;
        }
        public static Color TeamColor(int team) => team == 0 ? new Color(.3f, .75f, 2.2f) : new Color(2.2f, .35f, .3f);

        // Tower models (Assets/Game/Tower): team 0 gets the present-world water set (P1 cannon, P2 lion fountain, P3 hydro dam),
        // team 1 the future-world plasma set (F1 pylon, F2 waterfall spire, F3 geyser citadel). T1 = outer tower, T2 = inner, T3 = base.
        // Models face +Z, so team 0 (at +Z) turns round to face the enemy. A team-coloured ring and light mark the owner.
        public static int Tier(Vector3 pos, bool core) => core ? 3 : Mathf.Abs(pos.z) > (TowerZ[0] + TowerZ[1]) / 2 ? 2 : 1;
        public static float TopY = 6;   // where tower shots start (set from the model height)
        public static GameObject Model(string id, int team, Transform parent)
        {
            var root = new GameObject(id); root.transform.SetParent(parent, false);
            bool core = id == "@core"; int tier = Tier(parent.position, core);
            bool kay = Lanes == 2;   // Sky Lanes: KayKit towers and castles in the side's colour (CC0, Kay Lousberg)
            if (kay && core) return SkyCore(root, team);   // the painting's core: a great glowing crystal on a stone dais
            var pf = kay ? Resources.Load<GameObject>($"KayKit/building_{(tier == 2 ? "tower_B" : "tower_A")}_{(team == 0 ? "blue" : "red")}") : Catalog.I.Model($"tower_{(team == 0 ? "p" : "f")}{tier}");
            if (pf)
            {
                var t = Object.Instantiate(pf, root.transform); t.transform.localRotation = Quaternion.Euler(0, team == 0 ? 180 : 0, 0);
                if (kay)
                {   // already painted in the side's colours: size it, a glowing ring and a light, nothing else
                    var kr = t.GetComponentsInChildren<Renderer>(); var kb = kr[0].bounds; foreach (var r in kr) kb.Encapsulate(r.bounds);
                    foreach (var r in kr) foreach (var mt in r.materials) if (mt.HasProperty("_BaseColor")) mt.SetColor("_BaseColor", mt.GetColor("_BaseColor") * new Color(.74f, .78f, .74f));   // toned down into the map's muted, mossy stone
                    float kh = core ? 9.5f : tier == 2 ? 6.4f : 5.6f; t.transform.localScale *= kh / Mathf.Max(kb.size.y, .1f); TopY = kh * .8f;
                    float kR = core ? 4.2f : 2.4f; var kring = new GameObject("ring"); kring.transform.SetParent(root.transform, false); kring.transform.localPosition = Vector3.up * .04f; kring.AddComponent<MeshFilter>().sharedMesh = Annulus(kR * .86f, kR);
                    var km = new Material(Catalog.I.glowMat); km.SetFloat("_UseVC", 0); km.SetFloat("_DstBlend", 1); var kc = TeamColor(team) * .45f; kc.a = 1; km.SetColor("_Color", kc); kring.AddComponent<MeshRenderer>().sharedMaterial = km;
                    var kl = new GameObject("glow").AddComponent<Light>(); kl.transform.SetParent(root.transform, false); kl.transform.localPosition = Vector3.up * kh * .75f; kl.type = LightType.Point; kl.color = TeamColor(team) / 2.2f; kl.intensity = 2.5f; kl.range = core ? 14 : 9;
                    return root;
                }
                var tint = team == 0 ? new Color(.62f, .8f, 1.18f) : new Color(1.18f, .6f, .5f);   // the side's colour in the stone: pale blue for ours, reddish for theirs
                foreach (var r in t.GetComponentsInChildren<Renderer>()) foreach (var mt in r.materials) { if (mt.HasProperty("_Metallic")) mt.SetFloat("_Metallic", 0); if (mt.HasProperty("_Smoothness")) mt.SetFloat("_Smoothness", .25f); foreach (var p in new[] { "_BaseColor", "baseColorFactor" }) if (mt.HasProperty(p)) mt.SetColor(p, mt.GetColor(p) * tint); }   // URP and glTFast name the colour differently
                var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float want = core ? 5.2f : tier == 2 ? 4.4f : 4f; t.transform.localScale *= want / Mathf.Max(b.size.y, .1f); TopY = want * .85f;
                // a thin glowing ring on the ground in the side's colour (a filled disc burned out white under bloom)
                float rr = core ? 3f : 2.4f; var ring = new GameObject("ring"); ring.transform.SetParent(root.transform, false); ring.transform.localPosition = Vector3.up * .04f; ring.AddComponent<MeshFilter>().sharedMesh = Annulus(rr * .86f, rr);
                var rm = new Material(Catalog.I.glowMat); rm.SetFloat("_UseVC", 0); rm.SetFloat("_DstBlend", 1); var rc = TeamColor(team) * .45f; rc.a = 1; rm.SetColor("_Color", rc); ring.AddComponent<MeshRenderer>().sharedMaterial = rm;
                // a crystal of the side's colour turning in the air over the top
                var gem = new GameObject("crystal"); gem.transform.SetParent(root.transform, false); gem.transform.localPosition = Vector3.up * (want + (core ? 1.6f : 1.1f)); gem.transform.localScale = new Vector3(1, 1.5f, 1) * (core ? 1.5f : 1f);
                gem.AddComponent<MeshFilter>().sharedMesh = Gem(); var gm = new Material(Catalog.I.glowMat); gm.SetFloat("_UseVC", 0); gm.SetFloat("_DstBlend", 0); gm.SetColor("_Color", team == 0 ? new Color(.45f, .85f, 1.6f) : new Color(1.6f, .5f, .3f)); gem.AddComponent<MeshRenderer>().sharedMaterial = gm; gem.AddComponent<Spin>();
                var halo = new GameObject("halo"); halo.transform.SetParent(gem.transform, false); halo.transform.localScale = new Vector3(1, 1 / 1.5f, 1); halo.AddComponent<MeshFilter>().sharedMesh = Annulus(.55f, .7f); var hm = new Material(rm); hm.SetColor("_Color", TeamColor(team) * .3f); halo.AddComponent<MeshRenderer>().sharedMaterial = hm;
                var gl = new GameObject("glow").AddComponent<Light>(); gl.transform.SetParent(root.transform, false); gl.transform.localPosition = Vector3.up * (want + .6f); gl.type = LightType.Point; gl.color = TeamColor(team) / 2.2f; gl.intensity = 3; gl.range = 9;
                return root;
            }
            var basePf = Catalog.I.Model(core ? "fire_pit" : "column");
            if (basePf) { var b = Object.Instantiate(basePf, root.transform); b.transform.localScale *= core ? 2.2f : 1.3f; }
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.Destroy(crystal.GetComponent<Collider>());
            crystal.transform.SetParent(root.transform, false); crystal.transform.localPosition = Vector3.up * (core ? 2.2f : 6.2f);
            crystal.transform.localRotation = Quaternion.Euler(45, 0, 45); crystal.transform.localScale = new Vector3(.7f, 1.3f, .7f) * (core ? 1.8f : 1);
            var m = new Material(Catalog.I.glowMat); m.SetFloat("_UseVC", 0); m.SetFloat("_DstBlend", 0); m.SetColor("_Color", TeamColor(team)); crystal.GetComponent<Renderer>().sharedMaterial = m;
            crystal.AddComponent<Spin>();
            var l = new GameObject("glow").AddComponent<Light>(); l.transform.SetParent(crystal.transform, false); l.type = LightType.Point; l.color = TeamColor(team) / 2.2f; l.intensity = 6; l.range = 9;
            return root;
        }
    }

    // The floating 3v3 map (SKY-LANES-3V3.md): a straight lane and a curved one round a jungle, a base at each end, open sky between.
    // Walkable ground is the lanes (capsules round each stretch), the jungle disc and the two base discs; everything else is a drop.
    // Mirror-symmetric in Z so both teams get the same map (ready for online play later).
    public static class SkyLanes
    {
        public const float LaneHalf = 4, JungleR = 12.5f, BaseR = 10.5f, Lookahead = 7, BridgeHalf = 3;   // an 8 m lane (6 m on the bridges), closer to the big lane games
        // stretches of the curved lane that cross open sky on a stone bridge (narrower there); mirror-symmetric along the lane
        public static readonly Vector2[] Bridges = { new(30, 36), new(68, 74) };
        public static bool OnBridge(int lane, float along) { if (lane != 1) return false; foreach (var b in Bridges) if (along >= b.x && along <= b.y) return true; return false; }
        public static float HalfAt(int lane, float along) => OnBridge(lane, along) ? BridgeHalf : LaneHalf;
        public static readonly Vector2 Jungle = new(17, 0), Base = new(0, 42);   // Base: team 0's; team 1's is mirrored to -Z
        // each lane from team 0's end (+Z) to team 1's; team 1 walks it backwards
        public static readonly Vector2[][] Paths =
        {
            new Vector2[] { new(0, 37), new(0, -37) },
            new Vector2[] { new(3, 37), new(14, 32), new(26, 22), new(34, 9), new(36, 0), new(34, -9), new(26, -22), new(14, -32), new(3, -37) },
        };
        public static float Length(int lane) { float l = 0; var p = Paths[lane]; for (int i = 0; i + 1 < p.Length; i++) l += Vector2.Distance(p[i], p[i + 1]); return l; }
        public static Vector2 PointAt(int lane, float s)
        {
            var p = Paths[lane];
            for (int i = 0; i + 1 < p.Length; i++) { float l = Vector2.Distance(p[i], p[i + 1]); if (s <= l || i + 2 == p.Length) return Vector2.Lerp(p[i], p[i + 1], Mathf.Clamp01(s / l)); s -= l; }
            return p[p.Length - 1];
        }
        // nearest point of a lane's centre line, and how far along the lane it is
        public static float Nearest(int lane, Vector2 q, out Vector2 at, out float along)
        {
            var p = Paths[lane]; float best = 1e9f, run = 0; at = p[0]; along = 0;
            for (int i = 0; i + 1 < p.Length; i++)
            {
                var a = p[i]; var d = p[i + 1] - a; float l = d.magnitude, u = Mathf.Clamp01(Vector2.Dot(q - a, d) / (l * l)); var c = a + d * u; float dist = Vector2.Distance(q, c);
                if (dist < best) { best = dist; at = c; along = run + u * l; }
                run += l;
            }
            return best;
        }
        public static Vector3 Goal(Vector3 pos, int team, int lane)
        {
            lane = Mathf.Clamp(lane, 0, 1); Nearest(lane, new Vector2(pos.x, pos.z), out _, out float s); float len = Length(lane), ahead = team == 0 ? s + Lookahead : s - Lookahead;
            if (ahead >= len - 1 || ahead <= 1) return new Vector3(0, 0, -Lane.Side(team) * Lane.CoreZ);   // the lane's end: in to the enemy core
            var q = PointAt(lane, ahead); return new Vector3(q.x, 0, q.y);
        }
        public static Vector2 BaseOf(int team) => new(Base.x, Base.y * Lane.Side(team));
        // each side's spawn pool, behind its core: heroes appear and heal in it, the other side cannot set foot in it
        public const float FountainR = 3.6f, FountainZ = 48.5f;
        public static Vector3 Fountain(int team) => new(0, 0, Lane.Side(team) * FountainZ);
        public static Vector3 KeepOut(Vector3 p, int team)
        {
            var f = Fountain(1 - team); var d = new Vector2(p.x - f.x, p.z - f.z); float r = FountainR + .5f, m = d.magnitude;
            if (m >= r) return p; if (m < 1e-3f) d = new Vector2(0, -Lane.Side(1 - team)); else d /= m;
            p.x = f.x + d.x * r; p.z = f.z + d.y * r; return p;
        }
        // the land: each base court raised on its rock, the lanes ramping down from it, the jungle sunk below the lanes (the lanes stay level across it)
        public const float BaseH = 1.6f, Ramp = 7, JungleDrop = 1.3f;
        public static float Height(float x, float z)
        {
            var q = new Vector2(x, z); float h = 0;
            foreach (var b in new[] { BaseOf(0), BaseOf(1) }) h = Mathf.Max(h, BaseH * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(BaseR + .5f, BaseR + .5f + Ramp, Vector2.Distance(q, b)))));
            float dj = Vector2.Distance(q, Jungle); if (dj < JungleR + 1.2f)
            {
                float lane = 99; for (int l = 0; l < Paths.Length; l++) lane = Mathf.Min(lane, Nearest(l, q, out _, out _) - LaneHalf);
                float sink = JungleDrop * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(JungleR - 3.5f, JungleR, dj))) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, 3.5f, lane));   // a slope down from each lane's edge
                h -= sink;
            }
            return h;
        }
        // where the stage builder laid grass (NovaBuild.SkyLanesStage: road = LaneHalf - .4, shoulders 2.2 m beyond the lane, a 3 m green ring round each base, the jungle)
        public static bool Grassy(Vector2 q)
        {
            bool grass = false;   // roads first: they cut through the jungle's and the bases' green
            for (int l = 0; l < Paths.Length; l++) { float d = Nearest(l, q, out _, out float s); if (OnBridge(l, s) && d < LaneHalf + 2.2f) return false; if (d < LaneHalf - .1f) return false; if (d < LaneHalf + 1.9f) grass = true; }
            foreach (var b in new[] { BaseOf(0), BaseOf(1) }) { float d = Vector2.Distance(q, b); if (d <= BaseR + .5f) return false; if (d <= BaseR + 2.7f) grass = true; }
            return grass || Vector2.Distance(q, Jungle) <= JungleR + .6f;
        }
        public static bool Inside(Vector2 q, float pad = 0)
        {
            if (Vector2.Distance(q, Jungle) <= JungleR + pad || Vector2.Distance(q, BaseOf(0)) <= BaseR + pad || Vector2.Distance(q, BaseOf(1)) <= BaseR + pad) return true;
            for (int l = 0; l < Paths.Length; l++) { float d = Nearest(l, q, out _, out float s); if (d <= HalfAt(l, s) + pad) return true; }
            return false;
        }
        // keep a point on walkable ground: unchanged when inside, else onto the nearest edge
        public static Vector3 Walk(Vector3 p, float margin = 0)
        {
            var q = new Vector2(p.x, p.z); if (Inside(q, margin)) return p;
            Vector2 best = q; float bd = 1e9f;
            void Try(Vector2 c, float r) { var d = q - c; float m = d.magnitude; var e = m > 1e-4f ? c + d / m * r : c + Vector2.right * r; float dd = Vector2.Distance(q, e); if (dd < bd) { bd = dd; best = e; } }
            Try(Jungle, JungleR + margin); Try(BaseOf(0), BaseR + margin); Try(BaseOf(1), BaseR + margin);
            for (int l = 0; l < Paths.Length; l++) { Nearest(l, q, out var c, out float s); Try(c, HalfAt(l, s) + margin); }
            p.x = best.x; p.z = best.y; return p;
        }
        // towers: two per lane per team, the outer one further out; the straight lane uses Lane.TowerZ, the curve the same share of its length
        public static Vector3 Tower(int team, int lane, int inner)
        {
            if (lane == 0) return new Vector3(0, 0, Lane.Side(team) * Lane.TowerZ[inner == 1 ? 1 : 0]);
            float len = Length(1), share = inner == 1 ? .2f : .36f; var q = PointAt(1, team == 0 ? share * len : (1 - share) * len); return new Vector3(q.x, 0, q.y);
        }
        public static Vector3 LaneStart(int team, int lane) { lane = Mathf.Clamp(lane, 0, 1); float len = Length(lane); var q = PointAt(lane, team == 0 ? 2 : len - 2); return new Vector3(q.x, 0, q.y); }
    }

    public class Spin : MonoBehaviour { void Update() { transform.Rotate(0, 60 * Time.deltaTime, 0, Space.World); transform.localPosition += Vector3.up * Mathf.Sin(Time.time * 2) * .002f; } }
}
