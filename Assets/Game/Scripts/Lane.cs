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
        public static readonly FoeDef Minion = new() { id = "minion", model = "rig_minion", name = Loc.S("ทหาร", "Soldier"), hp = 380, spd = 3.0f, dmg = 32, range = 1.6f, kind = "melee", scale = 1.0f, rad = .45f, gold = 5 };
        public static FoeDef Champ(HeroDef h) => new() { id = "champ", model = "hero_" + h.id, name = h.name, hp = h.hp * 1.1f, spd = h.spd * .85f, dmg = 62 * h.atk, range = 2.2f, kind = "champ", scale = 1, rad = .5f, gold = 40 };
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
            var pf = Catalog.I.Model($"tower_{(team == 0 ? "p" : "f")}{tier}");
            if (pf)
            {
                var t = Object.Instantiate(pf, root.transform); t.transform.localRotation = Quaternion.Euler(0, team == 0 ? 180 : 0, 0);
                foreach (var r in t.GetComponentsInChildren<Renderer>()) foreach (var mt in r.materials) { if (mt.HasProperty("_Metallic")) mt.SetFloat("_Metallic", 0); if (mt.HasProperty("_Smoothness")) mt.SetFloat("_Smoothness", .25f); }
                var rs = t.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float want = core ? 5.2f : tier == 2 ? 4.4f : 4f; t.transform.localScale *= want / Mathf.Max(b.size.y, .1f); TopY = want * .85f;
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.Destroy(ring.GetComponent<Collider>()); ring.transform.SetParent(root.transform, false);
                ring.transform.localPosition = Vector3.up * .03f; ring.transform.localScale = new Vector3(core ? 5.6f : 4.4f, .02f, core ? 5.6f : 4.4f);
                var rm = new Material(Catalog.I.glowMat); rm.SetFloat("_UseVC", 0); rm.SetFloat("_DstBlend", 1); var rc = TeamColor(team) * .35f; rc.a = 1; rm.SetColor("_Color", rc); ring.GetComponent<Renderer>().sharedMaterial = rm;
                var gl = new GameObject("glow").AddComponent<Light>(); gl.transform.SetParent(root.transform, false); gl.transform.localPosition = Vector3.up * want * .7f; gl.type = LightType.Point; gl.color = TeamColor(team) / 2.2f; gl.intensity = 5; gl.range = 10;
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
        public const float LaneHalf = 5, JungleR = 12.5f, BaseR = 10.5f, Lookahead = 7;
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
        public static bool Inside(Vector2 q, float pad = 0)
        {
            if (Vector2.Distance(q, Jungle) <= JungleR + pad || Vector2.Distance(q, BaseOf(0)) <= BaseR + pad || Vector2.Distance(q, BaseOf(1)) <= BaseR + pad) return true;
            for (int l = 0; l < Paths.Length; l++) if (Nearest(l, q, out _, out _) <= LaneHalf + pad) return true;
            return false;
        }
        // keep a point on walkable ground: unchanged when inside, else onto the nearest edge
        public static Vector3 Walk(Vector3 p, float margin = 0)
        {
            var q = new Vector2(p.x, p.z); if (Inside(q, margin)) return p;
            Vector2 best = q; float bd = 1e9f;
            void Try(Vector2 c, float r) { var d = q - c; float m = d.magnitude; var e = m > 1e-4f ? c + d / m * r : c + Vector2.right * r; float dd = Vector2.Distance(q, e); if (dd < bd) { bd = dd; best = e; } }
            Try(Jungle, JungleR + margin); Try(BaseOf(0), BaseR + margin); Try(BaseOf(1), BaseR + margin);
            for (int l = 0; l < Paths.Length; l++) { Nearest(l, q, out var c, out _); Try(c, LaneHalf + margin); }
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
