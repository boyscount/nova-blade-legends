using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Sky Lanes stage B, step 1 (owner, 2026-11-11): monster camps in the middle jungle. They belong to neither side (team 2), stand at home
    // until struck, fight whoever struck them, give up past a leash and walk home to heal. Two buff camps, one in each half of the jungle:
    // the Ghoul Giant (team 0's half) gives its slayer +20% damage, the Spore King (team 1's half) faster skills, 90 s each; two goblin camps
    // give gold and XP. Camps come at 0:25 (goblins) and 0:50 (buffs) and return 60 / 90 s after they are cleared. One AI hero a side
    // (its "jungler") clears camps when nothing is in front of it; the others keep to their lanes.
    public static class Jungle
    {
        public const int Neutral = 2;
        public const float MightK = 1.2f, HasteK = 1.3f, BuffTime = 90, Leash = 9;
        // stage B step 2: the Sky Guardian, a great treant in the jungle's middle from 4:00 (again 3 minutes after it falls). Its slayers' side
        // gets the Guardian's Blessing for 2 minutes: its heroes deal 15% more, its soldiers come out with 40% more health and damage
        public const float BossK = 1.15f, BossSoldierK = 1.4f, BlessTime = 120, BossLeash = 12;
        static readonly float[] blessUntil = new float[2];
        // AI teamwork at the Guardian: every few seconds each side checks whether two or more of its AI heroes are fit (70%+ health); then the
        // whole side's AI goes to the Guardian for up to 40 s (the player's allies too; the player decides for themself), at most once in 2 min, not while the other side is blessed
        static readonly float[] rallyUntil = new float[2], rallyNext = new float[2]; static float rallyCheck;
        public static bool Rallying(int team) => team is 0 or 1 && G && G.time < rallyUntil[team];
        public static bool Blessed(int team) => team is 0 or 1 && On && G.time < blessUntil[team];
        public static float BlessLeft(int team) => team is 0 or 1 && G ? Mathf.Max(0, blessUntil[team] - G.time) : 0;
        public class Camp
        {
            public string id, buff; public Vector3 at, faceTo; public FoeDef[] defs; public Vector3[] offs; public float first, again;
            public readonly List<Foe> members = new(); public float nextAt; public bool up;
            public bool Alive => up && members.Any(m => m && m.alive);
        }
        public static readonly List<Camp> camps = new();
        public static readonly List<string> Events = new();   // tests: camps cleared and buffs taken this match
        static Main G => Main.G;
        static FoeDef Make(string from, float hp, float dmg, int gold, float scale)
        {
            var b = Data.Foe(from); if (b == null) return null; var d = b.Copy(); d.hp = hp; d.dmg = dmg; d.gold = gold; d.scale = b.scale * scale; return d;
        }
        // the maps with camps: Sky Lanes (the middle jungle) and, stage B step 4, the flat 3-lane map (the woods between the lanes)
        public static bool On => G && G.lane && (Lane.Lanes == 2 || Lane.Lanes == 3);
        static bool Sky => Lane.Lanes == 2;
        // the boss's name for the map: the Sky Guardian over the floating jungle, the River Guardian in the 3-lane map's river
        public static string BossName => Sky ? Loc.S("ผู้พิทักษ์ป่าลอยฟ้า", "Sky Guardian") : Loc.S("ผู้พิทักษ์สายน้ำ", "River Guardian");
        // the 3-lane map: lanes at x -22, 0, 22, the river along z = 0. The Guardian stands in the river in the west woods; each side has a Ghoul
        // Giant (west) and a Spore King (east) in its own half, and two goblin camps sit on the east river bank. Mirror-symmetric in Z
        public const float WoodsX = 11, BuffZ = 13, ClearR = 4.5f;
        static readonly List<Vector3> cleared = new(); public static int Cleared => cleared.Count;   // where the 3-lane woods were cleared for camps (done once: the stage stays loaded)
        // a new match on a map with camps: the camps, none up yet
        public static void Begin()
        {
            camps.Clear(); Events.Clear(); blessUntil[0] = blessUntil[1] = 0; rallyUntil[0] = rallyUntil[1] = rallyNext[0] = rallyNext[1] = 0; rallyCheck = 0; var J = new Vector3(SkyLanes.Jungle.x, 0, SkyLanes.Jungle.y);
            var guard = new FoeDef { id = "skyguardian", model = "rig_b_treant", name = BossName, hp = 4500, spd = 2.2f, dmg = 45, range = 2.8f, kind = "slam", scale = 1.6f, rad = 1.1f, gold = 120 };
            var giant = Make("giant", 1500, 34, 45, 1.15f); var spore = Make("mushroom", 1400, 30, 45, 1.6f); var gob = Make("goblin", 380, 15, 14, 1.1f);   // one AI hero clears a buff camp at about two thirds of its health
            if (giant != null) giant.name = Loc.S("ยักษ์กูลแห่งป่า", "Jungle Ghoul Giant"); if (spore != null) spore.name = Loc.S("ราชาเห็ดสปอร์", "Spore King");
            var gobOffs = new[] { new Vector3(-.8f, 0, 0), new Vector3(.8f, 0, 0) };
            if (Sky)
            {
                camps.Add(new Camp { id = "boss", buff = "boss", at = J, faceTo = J, defs = new[] { guard }, offs = new[] { Vector3.zero }, first = 240, again = 180 });
                if (giant != null) camps.Add(new Camp { id = "might", buff = "might", at = J + new Vector3(-4.5f, 0, 5.5f), faceTo = J, defs = new[] { giant }, offs = new[] { Vector3.zero }, first = 50, again = 90 });
                if (spore != null) camps.Add(new Camp { id = "haste", buff = "haste", at = J + new Vector3(-4.5f, 0, -5.5f), faceTo = J, defs = new[] { spore }, offs = new[] { Vector3.zero }, first = 50, again = 90 });
                if (gob != null) foreach (int s in new[] { 1, -1 })
                    camps.Add(new Camp { id = s > 0 ? "goblins0" : "goblins1", at = J + new Vector3(4, 0, 3.8f * s), faceTo = J, defs = new[] { gob, gob }, offs = gobOffs, first = 25, again = 60 });
            }
            else
            {
                var B = new Vector3(-WoodsX, 0, 0);
                camps.Add(new Camp { id = "boss", buff = "boss", at = B, faceTo = Vector3.zero, defs = new[] { guard }, offs = new[] { Vector3.zero }, first = 240, again = 180 });
                foreach (int s in new[] { 1, -1 })
                {   // s = 1: team 0's half (+Z)
                    string k = s > 0 ? "0" : "1";
                    if (giant != null) camps.Add(new Camp { id = "might" + k, buff = "might", at = new Vector3(-WoodsX, 0, BuffZ * s), faceTo = new Vector3(-WoodsX, 0, 0), defs = new[] { giant }, offs = new[] { Vector3.zero }, first = 50, again = 90 });
                    if (spore != null) camps.Add(new Camp { id = "haste" + k, buff = "haste", at = new Vector3(WoodsX, 0, BuffZ * s), faceTo = new Vector3(WoodsX, 0, 0), defs = new[] { spore }, offs = new[] { Vector3.zero }, first = 50, again = 90 });
                    if (gob != null) camps.Add(new Camp { id = "goblins" + k, at = new Vector3(WoodsX, 0, 4.2f * s), faceTo = new Vector3(WoodsX, 0, 0), defs = new[] { gob, gob }, offs = gobOffs, first = 25, again = 60 });
                }
                ClearWoods();
            }
            foreach (var c in camps) { c.nextAt = c.first; c.up = false; }
        }
        // the 3-lane map's woods were planted before there were camps: hide the trees, bushes and rocks on each camp's ground and drop
        // their obstacle circles, so a camp stands in a glade the heroes can walk round
        static void ClearWoods()
        {
            var spots = camps.Select(c => new Vector2(c.at.x, c.at.z)).ToList();
            bool Near(float x, float z, float pad) => spots.Any(s => Vector2.Distance(s, new Vector2(x, z)) < ClearR + pad);
            var root = G.Stage("Stage_lane3");
            if (root && cleared.Count == 0)
                foreach (Transform t in root)
                {
                    string n = t.name.ToLowerInvariant(); if (n.Contains("ground") || n.Contains("water") || n.Contains("bridge") || n.Contains("light") || n.Contains("sun")) continue;
                    var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue; var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);   // by what is drawn (a prefab's pivot can sit far from it)
                    if (Near(bb.center.x, bb.center.z, 1 + Mathf.Min(bb.extents.x, bb.extents.z) * .6f)) { t.gameObject.SetActive(false); cleared.Add(bb.center); }
                }
            Obstacles.Current = Obstacles.Current.Where(o => !Near(o.x, o.y, o.z + .5f)).ToList();
        }
        public static void Clear() => camps.Clear();
        static float auraT;
        public static void Tick(float dt)
        {
            if (!On) return; float now = G.time;
            foreach (var c in camps)
            {
                if (!c.up && now >= c.nextAt) Spawn(c, c.nextAt <= c.first + .01f);
            }
            rallyCheck -= dt; if (rallyCheck <= 0)
            {
                rallyCheck = 4; var boss = camps.FirstOrDefault(c => c.buff == "boss" && c.Alive);
                for (int t = 0; t < 2; t++)
                {
                    if (boss == null) { rallyUntil[t] = 0; continue; }
                    if (now < rallyUntil[t] || now < rallyNext[t]) continue;
                    int fit = G.foes.Count(f => f && f.alive && f.team == t && f.d.kind == "champ" && f.hp > f.maxHp * .7f);
                    if (fit >= 2 && !Blessed(1 - t)) { rallyUntil[t] = now + 40; rallyNext[t] = now + 120; Events.Add($"{now / 60:0.0}m side {t} rallies to the Guardian ({fit} fit)"); }
                }
            }
            // the buffed glow round whoever carries a buff
            auraT -= dt; if (auraT <= 0)
            {
                auraT = .45f; var h = G.hero;
                if (h && !h.dead) Glow(h.Pos, h.mightT, h.hasteT); else if (h) h.mightT = h.hasteT = 0;   // lost on death
                foreach (var f in G.foes) if (f && f.alive && f.d.kind == "champ") Glow(f.Pos, f.mightT, f.hasteT);
            }
        }
        static void Glow(Vector3 p, float might, float haste)
        {
            if (might > 0) Fx.Burst(p + Vector3.up * .9f, 3, new Color(1f, .35f, .2f), 1.6f, .45f, .6f, .1f, true);
            if (haste > 0) Fx.Burst(p + Vector3.up * .9f, 3, new Color(.35f, .9f, 1f), 1.6f, .45f, .6f, .1f, true);
        }
        static void Spawn(Camp c, bool first)
        {
            c.members.Clear(); c.up = true;
            for (int i = 0; i < c.defs.Length; i++)
            {
                var p = c.at + c.offs[i]; var f = G.SpawnUnit(c.defs[i], p, Neutral); if (!f) continue;
                f.camp = c; f.home = p; f.face = Main.Ang(c.faceTo - p); c.members.Add(f);
            }
            if (c.buff == "boss") G.ui.Banner(Loc.S($"{BossName}ตื่นแล้ว!", $"The {BossName} awakens!"), Loc.S("ทีมที่ล้มได้ ฮีโร่และทหารแรงขึ้น 2 นาที", "The side that fells it: stronger heroes and soldiers for 2 minutes"));
            else if (c.buff != null && first) G.ui.Banner(Loc.S("มอนสเตอร์บัฟปรากฏในป่า!", "Buff monsters in the jungle!"), Loc.S("ยักษ์กูล: ตีแรงขึ้น · ราชาเห็ด: สกิลเร็วขึ้น", "Ghoul Giant: more damage · Spore King: faster skills"));
        }
        // a camp monster was hit: it (and its camp mates) go after the one who struck
        public static void Struck(Foe f, Vector3 from, Main.Hit o)
        {
            ITarget by = null;
            if (!o.byFoe) by = G.hero;
            else
            {
                float bd = 12; foreach (var c in G.foes) if (c && c.alive && c.team != Neutral && c.d.kind == "champ") { float d = Main.Flat(c.Pos - from).magnitude; if (d < bd) { bd = d; by = c; } }
            }
            if (by == null || by.Down) return; f.lastHitBy = by;
            if (f.camp != null) foreach (var m in f.camp.members) if (m && m.alive && (m.lastHitBy == null || m.lastHitBy.Down)) m.lastHitBy = by;
        }
        public static ITarget TargetOf(Foe f)
        {
            var t = f.lastHitBy; if (t == null || t.Down || Main.Flat(t.Pos - f.home).magnitude > (f.camp != null && f.camp.buff == "boss" ? BossLeash : Leash)) { f.lastHitBy = null; return null; }
            return t;
        }
        // a camp monster fell: XP and gold to the side that struck it last; the buff to whoever cleared a buff camp
        public static void Down(Foe f)
        {
            var c = f.camp; var by = f.lastHitBy; int side = by is Hero ? 0 : by is Foe bf ? bf.team : -1;
            if (side is 0 or 1)
            {
                if (c != null && !c.members.Any(m => m && m.alive && m != f)) { if (c.buff == "boss") { G.sideBosses[side]++; G.Chron("guardian", side); } else G.sideCamps[side]++; var st = by is Hero ? G.Stat(G.hero.def.name, 0, true) : by is Foe bs ? G.Stat(bs.d.name, bs.team) : null; if (st != null) { if (c.buff == "boss") st.bosses++; else st.camps++; } }
                G.TeamXp(side, f.d.gold);
                if (side == 0 && G.hero && !G.hero.dead) { G.AddXp(f.d.gold); G.DropLoot(f.Pos, false, f.d.gold); }
            }
            if (c == null || c.members.Any(m => m && m.alive && m != f)) return;
            c.up = false; c.nextAt = G.time + c.again; Events.Add($"{G.time / 60:0.0}m {c.id} cleared by side {side} ({(by is Hero ? "player" : by is Foe bn ? bn.d.name : "?")})");
            if (c.buff == null || side < 0) return;
            if (c.buff == "boss")
            {   // the Guardian's Blessing for the whole side
                blessUntil[side] = G.time + BlessTime; Fx.Ring(f.Pos, 8, new Color(.5f, 1f, .45f), 1.2f, .3f);
                G.ui.Banner(side == 0 ? Loc.S("ได้พรผู้พิทักษ์!", "Guardian's Blessing!") : Loc.S("ศัตรูได้พรผู้พิทักษ์!", "The enemy has the Blessing!"), Loc.S("ฮีโร่ตีแรงขึ้น 15% · ทหารแรงขึ้น 40% · 2 นาที", "Heroes +15% damage · soldiers +40% · 2 minutes"));
                return;
            }
            bool might = c.buff == "might"; string name = might ? Loc.S("พลังยักษ์", "Giant's Might") : Loc.S("สปอร์เร่ง", "Spore Haste"), what = might ? Loc.S("ตีแรงขึ้น 20%", "+20% damage") : Loc.S("สกิลคูลดาวน์เร็วขึ้น 30%", "skills recharge 30% faster");
            if (by is Hero h) { if (might) h.mightT = BuffTime; else h.hasteT = BuffTime; G.ui.Banner(Loc.S($"ได้{name}!", $"{name}!"), Loc.S($"{what} · 90 วินาที", $"{what} · 90 s")); }
            else if (by is Foe ch) { if (might) ch.mightT = BuffTime; else ch.hasteT = BuffTime; G.ui.Banner(side == 0 ? Loc.S($"เพื่อนได้{name}", $"Ally took {name}") : Loc.S($"ศัตรูได้{name}!", $"Enemy took {name}!"), ch.d.name); }
            Fx.Ring(by.Pos, 3, might ? new Color(1f, .35f, .2f) : new Color(.35f, .9f, 1f), .6f, .2f);
        }
        // ---------- getting in and out ----------
        // the jungle meets the lanes only at its four stone paths (SkyLanes.Gates); between them is open sky. A hero walking straight
        // between the jungle and a lane slid along the edge and got stuck, so heroes going in or out are sent through the best path
        static bool InJungle(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), SkyLanes.Jungle) <= SkyLanes.JungleR - .3f;
        public static Vector3 Route(Vector3 from, Vector3 to)
        {
            bool a = InJungle(from), b = InJungle(to); if (a == b) return to;
            var q = new Vector2(from.x, from.z); var t = new Vector2(to.x, to.z); float best = 1e9f; Vector2 near = default, far = default;
            foreach (var g in SkyLanes.Gates)
            {   // g.a: the lane end, g.b: the jungle end
                var n = a ? g.b : g.a; var f = a ? g.a : g.b; float cost = Vector2.Distance(q, n) + Vector2.Distance(n, f) + Vector2.Distance(f, t);
                if (cost < best) { best = cost; near = n; far = f; }
            }
            var d = far - near; float u = Mathf.Clamp01(Vector2.Dot(q - near, d) / d.sqrMagnitude); bool onPath = Vector2.Distance(q, near + d * u) < SkyLanes.GateHalf + .3f && u > .02f;
            var go = onPath || Vector2.Distance(q, near) < 1.2f ? far + (far - near).normalized * 1.5f : near;   // on the path: walk it to its far end and a step beyond
            return new Vector3(go.x, to.y, go.y);
        }
        // ---------- AI junglers ----------
        // one AI hero a side goes to the camps: team 0's first AI ally (the player covers a lane), team 1's first hero on the straight lane
        public static bool IsJungler(Foe f)
        {
            if (!On || f.d.kind != "champ" || f.team > 1 || !Sky && G.laneSize < 3) return false;   // the 3-lane map: junglers only in 3 v 3
            foreach (var c in G.foes) if (c && c.alive && c.team == f.team && c.d.kind == "champ" && (f.team == 0 || c.laneIdx == 0)) return c == f;
            return false;
        }
        // where the jungler should go now: the best camp up (its own half first), or null for its lane
        public static Camp ForAi(Foe f)
        {
            if (!On || f.d.kind != "champ" || f.team > 1) return null;
            var boss = camps.FirstOrDefault(c => c.buff == "boss" && c.Alive);
            if (boss != null && f.hp > f.maxHp * .5f && Main.Flat(boss.at - f.Pos).magnitude < 26 && boss.members.Any(m => m && m.alive && m.lastHitBy != null && !m.lastHitBy.Down && m.lastHitBy.Team == f.team))
                return boss;   // a fight at the Guardian: the side's heroes nearby join it
            if (boss != null && Rallying(f.team) && f.hp > f.maxHp * .4f) return boss;   // the side rallies to the Guardian
            if (!IsJungler(f) || f.hp < f.maxHp * .5f) return null;
            Camp best = null; float bs = 1e9f;
            foreach (var c in camps)
            {
                if (!c.Alive || c.buff != null && f.hp < f.maxHp * (c.buff == "boss" ? .8f : .7f)) continue; float d = Main.Flat(c.at - f.Pos).magnitude; bool own = Mathf.Sign(c.at.z) == Lane.Side(f.team);
                float score = d - (own ? 6 : 0) - (c.buff != null ? 4 : 0); if (score < bs) { bs = score; best = c; }
            }
            return best;
        }
        public static Foe Member(Camp c, Vector3 near, float within)
        {
            Foe best = null; float bd = within; if (c == null) return null;
            foreach (var m in c.members) if (m && m.alive && !m.spawning) { float d = Main.Flat(m.Pos - near).magnitude; if (d < bd) { bd = d; best = m; } }
            return best;
        }
    }
}
