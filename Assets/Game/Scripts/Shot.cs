using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Projectiles for both sides: orbs, feathers, missiles, shards, fireballs. Optional homing, gravity, area damage.
    public class Shot : MonoBehaviour
    {
        bool byFoe; Vector3 vel; float dmg, life, r = .4f, homing, aoe, grav, kb = 1, slow; bool pierce, enemy, trail; Color col; string kind;
        readonly HashSet<Foe> hit = new();
        static readonly Dictionary<string, Mesh> meshes = new();
        static Material baseMat;

        public static Shot Fire(Vector3 p, Vector3 v, float dmg, Color col, string kind = "orb", float life = 2, bool pierce = false, float homing = 0,
            float aoe = 0, float grav = 0, bool enemy = false, bool trail = false, float kb = 1, float slow = 0, float r = .4f, bool byFoe = false)
        {
            var go = new GameObject("shot_" + kind); var s = go.AddComponent<Shot>();
            go.transform.position = p;
            s.byFoe = byFoe; s.vel = v; s.dmg = dmg; s.col = col; s.kind = kind; s.life = life; s.pierce = pierce; s.homing = homing; s.aoe = aoe; s.grav = grav; s.enemy = enemy; s.trail = trail; s.kb = kb; s.slow = slow; s.r = r;
            go.AddComponent<MeshFilter>().sharedMesh = MeshFor(kind);
            if (!baseMat) { baseMat = new Material(Catalog.I.glowMat); baseMat.SetFloat("_UseVC", 0); }
            var m = new Material(baseMat); m.SetColor("_Color", col * 2.4f); go.AddComponent<MeshRenderer>().sharedMaterial = m;
            go.transform.localScale = kind switch { "feather" => new Vector3(.12f, .12f, .6f), "missile" => new Vector3(.12f, .12f, .45f), "big" => Vector3.one * .64f, "shard" => Vector3.one * .4f, _ => Vector3.one * .32f };
            if (kind == "big" || kind == "missile") { var f = Fx.Spawn("fire_small", p, .4f, life + 1); if (f) f.transform.SetParent(go.transform); }
            Main.G.shots.Add(s); return s;
        }
        static Mesh MeshFor(string kind)
        {
            string key = kind == "feather" || kind == "missile" ? "capsule" : kind == "shard" ? "cube" : "sphere";
            if (meshes.TryGetValue(key, out var m)) return m;
            var prim = GameObject.CreatePrimitive(key == "capsule" ? PrimitiveType.Capsule : key == "cube" ? PrimitiveType.Cube : PrimitiveType.Sphere);
            m = prim.GetComponent<MeshFilter>().sharedMesh; Destroy(prim); meshes[key] = m; return m;
        }

        void Update()
        {
            var G = Main.G; float dt = Time.deltaTime; life -= dt;
            if (homing > 0 && !enemy)
            {
                var tg = G.Nearest(transform.position, 14);
                if (tg) { float cur = Mathf.Atan2(vel.x, vel.z), want = Main.Ang(tg.Pos - transform.position), sp = new Vector2(vel.x, vel.z).magnitude; float na = cur + Mathf.Clamp(Main.AngDiff(cur, want), -homing * dt, homing * dt); vel = new Vector3(Mathf.Sin(na) * sp, vel.y, Mathf.Cos(na) * sp); }
            }
            vel.y -= grav * dt; transform.position += vel * dt;
            if (vel.sqrMagnitude > .01f && (kind == "feather" || kind == "missile")) transform.rotation = Quaternion.LookRotation(vel);
            else transform.Rotate(300 * dt, 200 * dt, 0);
            if (trail && Random.value < .6f) Fx.Burst(transform.position, 1, col, .6f, .5f, 0);
            var p = transform.position;
            if (!enemy)
            {
                foreach (var e in G.Live)
                {
                    if (hit.Contains(e)) continue;
                    if (Main.Flat(e.Pos - p).magnitude < r + e.rad && Mathf.Abs(p.y - (e.Pos.y + e.h * .5f)) < e.h * .7f + .5f)
                    {
                        hit.Add(e);
                        if (aoe > 0) { Explode(); return; }
                        e.Damage(dmg, p - vel * .1f, new Main.Hit { kb = byFoe ? 0 : kb, slow = slow, col = col, byFoe = byFoe }); Fx.Burst(p, 6, col, 3);
                        if (!pierce) { life = 0; break; }
                    }
                }
                if (aoe > 0 && grav > 0 && p.y <= .1f) { Explode(); return; }
            }
            else
            {   // enemy shots hit the player and, in lane mode, the player's side
                foreach (var t in G.Opponents(1))
                    if (Main.Flat(t.Pos - p).magnitude < r + t.Rad && p.y < 2.6f) { t.Take(dmg, p); Fx.Burst(p, 10, col, 3); life = 0; break; }
            }
            if (Main.Flat(p).magnitude > 34 || p.y < -1) life = 0;
            if (life <= 0) Kill();
        }
        void Explode()
        {
            var p = transform.position; var G = Main.G;
            G.HitArea(p, aoe, dmg, new Main.Hit { kb = 3, slow = slow, col = col }); Fx.Ring(p, aoe, col, .4f); Fx.Spawn("explosion", p, aoe * .5f, 3); Fx.Burst(p, 26, col, 6);
            G.shake = Mathf.Max(G.shake, .12f); Kill();
        }
        void Kill() { Main.G.shots.Remove(this); Destroy(GetComponent<MeshRenderer>().sharedMaterial); Destroy(gameObject); }
    }
}
