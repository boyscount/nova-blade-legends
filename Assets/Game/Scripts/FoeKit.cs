using System.Collections;
using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // AI heroes in the lane battles use the same kits as the player (MOBA-SKILLS.md), each skill shown in red on the ground
    // before it lands so it can be dodged. Their power P is their basic hit scaled up to about a hero's attack power.
    public partial class Foe
    {
        Kit aiKit; bool aiKitChecked; readonly float[] aiCd = new float[3]; float aiNext, kitBusy, aiAtkEnd, aiAtkK, aiCut, aiCutEnd;
        const float AiPScale = 1.6f, AiCdScale = 1.15f, AiGap = 1.2f;
        public int AiCasts; public string AiLog = "";   // tests
        public Kit AiKit
        {
            get
            {
                if (!aiKitChecked)
                {
                    aiKitChecked = true;
                    if (G.lane && d.kind == "champ" && d.model != null && d.model.StartsWith("hero_")) aiKit = Kits.For(d.model.Substring(5));
                    for (int i = 0; i < 3; i++) aiCd[i] = Random.Range(1.5f, 4.5f);   // not all at once at the start
                }
                return aiKit;
            }
        }
        float AiP => d.dmg / G.AiLv.dmg * AiPScale * G.AiLv.skill * (Time.time < aiAtkEnd ? 1 + aiAtkK : 1);   // skills scale with the difficulty's skill power (not twice with its basic damage)

        static bool Support(KSkill s) => s.dmg <= 0 && s.dot <= 0 && s.stun <= 0 && s.trap <= 0 && s.shape != KShape.Dash;

        // called each frame while chasing; true when a skill was started
        bool KitAi(float dt, ITarget t, float dd)
        {
            for (int i = 0; i < 3; i++) aiCd[i] -= dt * (hasteT > 0 ? Jungle.HasteK : 1); AiAura(dt);
            if (Time.time < aiNext || kitBusy > 0) return false;
            int near = G.Opponents(team).Count(o => Main.Flat(o.Pos - Pos).magnitude < 7);
            for (int i = 0; i < 3; i++)
            {
                var s = aiKit.s[i]; if (aiCd[i] > 0) continue;
                bool want;
                if (Support(s)) want = s.atkBuff > 0 ? near > 0 && t != null : hp < maxHp * .6f || (s.allies || s.shape == KShape.Ally) && MateHurt(Mathf.Max(s.size, s.range));   // heal or shield the team when a teammate (or the player on its side) is hurt
                else if (t == null || t.Down) want = false;
                else if (s.shape == KShape.Dash && s.back > 0) want = dd < 3 && hp < maxHp * .7f;
                else want = dd <= s.shape switch { KShape.Self => s.size, KShape.Point => s.range + s.size * .5f, _ => s.range };
                if (!want) continue;
                aiCd[i] = s.cd * AiCdScale * G.AiLv.cd; aiNext = Time.time + AiGap * G.AiLv.gap; AiCasts++; AiLog += "QWE"[i];
                StartCoroutine(AiCast(s, t)); return true;
            }
            return false;
        }

        IEnumerator AiCast(KSkill s, ITarget t)
        {
            var at = t != null ? t.Pos : Pos; var dir = Main.Flat(at - Pos); var n = dir.sqrMagnitude > .01f ? dir.normalized : Main.Dir(face); face = Main.Ang(n);
            if (s.shape is KShape.Point or KShape.Leap && dir.magnitude > s.range) at = Pos + n * s.range;
            bool support = Support(s); float wind = support ? .25f : s.shape is KShape.Self or KShape.Point or KShape.Leap ? .6f : .45f;
            kitBusy = wind + s.delay + .45f;
            if (!support)
                switch (s.shape)
                {   // the warning, in red, for as long as the wind-up
                    case KShape.Self: Fx.Telegraph(Pos, s.size, wind); break;
                    case KShape.Point: case KShape.Leap: Fx.Telegraph(at, s.size, wind + s.delay); break;
                    case KShape.Line: case KShape.Dash: Fx.Telegraph(Pos, s.range, wind, face, Mathf.Atan2(s.size / 2, s.range)); break;
                    case KShape.Cone: Fx.Telegraph(Pos, s.range, wind, face, s.size * Mathf.Deg2Rad / 2); break;
                    default: if (t != null) Fx.Telegraph(t.Pos, 1.2f, wind); break;
                }
            if (anim) { var c = Catalog.I.Clip(animSet, "atk2"); anim.SetFloat("Speed", 1.3f); anim.CrossFadeInFixedTime("atk2", .06f, 0, Mathf.Max(0, c.hit - wind)); animState = null; }
            yield return new WaitForSeconds(wind + (s.shape == KShape.Point ? s.delay : 0));
            if (!alive) yield break;
            float P = AiP; var col = Kits.SideCol(team); string hid = d.model.Substring(5); int si = System.Array.IndexOf(aiKit.s, s);
            bool late = s.shape is KShape.Point or KShape.Leap or KShape.Dash; if (!late) Kits.Show(hid, si, s, Pos, t != null ? t.Pos : at, n);
            switch (s.shape)
            {
                case KShape.Self:
                    if (!support)
                    {
                        int hits = Mathf.Max(1, (int)s.hits);
                        for (int h = 0; h < hits && alive; h++)
                        {
                            Fx.Ring(Pos, s.size, col, .45f, .3f);
                            foreach (var o in Opp(Pos, s.size)) { if (s.pullIn > 0 && o is Foe pf && pf.d.kind != "tower") pf.transform.position = Pos + Main.Flat(pf.Pos - Pos).normalized * 1.2f; AiStrike(o, s, P * s.dmg, Pos); }
                            if (hits > 1) yield return new WaitForSeconds(s.dur / hits);
                        }
                    }
                    AiSupport(s, col);
                    break;
                case KShape.Point:
                    if (s.trap > 0) { StartCoroutine(AiTrap(at, s, P, col)); break; }
                    if (s.dotT > 0 && s.dmg <= 0)
                    {
                        int nT = Mathf.Max(1, Mathf.RoundToInt(s.dotT / .5f));
                        for (int i = 0; i < nT && alive; i++) { Fx.Ring(at, s.size, col, .55f, .12f); foreach (var o in Opp(at, s.size)) AiStrike(o, s, P * s.dot / nT, at, false); yield return new WaitForSeconds(.5f); }
                        break;
                    }
                    int ticks = Mathf.Max(1, (int)s.hits);
                    for (int h = 0; h < ticks && alive; h++)
                    {
                        Fx.Ring(at, s.size, col, .5f, .35f); Fx.Spawn("ground_hit", at, s.size * .45f, 2);
                        foreach (var o in Opp(at, s.size)) AiStrike(o, s, P * s.dmg, at);
                        if (ticks > 1) yield return new WaitForSeconds(s.dur / ticks);
                    }
                    break;
                case KShape.Line:
                    Fx.Beam(Pos + Vector3.up * 1.1f, Pos + n * s.range + Vector3.up * 1.1f, col, .25f, s.size * .35f);
                    foreach (var o in G.Opponents(team).ToList()) { if (!InLine(o.Pos, Pos, n, s.range, s.size / 2 + o.Rad)) continue; AiStrike(o, s, P * s.dmg, Pos); if (s.drag > 0) { if (o is Hero dh) dh.transform.position = G.Clamp(Pos + n * 1.4f); else if (o is Foe df && df.d.kind != "tower") df.transform.position = G.Clamp(Pos + n * 1.4f); break; } }
                    break;
                case KShape.Cone:
                    {
                        float half = s.size * Mathf.Deg2Rad / 2; int ticks2 = Mathf.Max(1, (int)s.hits);
                        for (int h = 0; h < ticks2 && alive; h++)
                        {
                            Fx.Slash(Pos + Vector3.up * .8f, face, col, s.range, half * 2);
                            foreach (var o in G.Opponents(team).ToList()) { var dv = Main.Flat(o.Pos - Pos); if (dv.magnitude - o.Rad <= s.range && (dv.magnitude < .8f || Mathf.Abs(Main.AngDiff(face, Main.Ang(dv))) <= half)) AiStrike(o, s, P * s.dmg, Pos); }
                            if (ticks2 > 1) yield return new WaitForSeconds(s.dur / ticks2);
                        }
                    }
                    break;
                case KShape.Dash:
                    {
                        var dn = s.back > 0 ? -n : n; float T = .18f + s.range * .02f, tt = 0; var hit = new System.Collections.Generic.HashSet<ITarget>();
                        while (tt < T && alive)
                        {
                            float step = s.range / T * Time.deltaTime; tt += Time.deltaTime; transform.position = G.Clamp(Pos + dn * step); if (Lane.Lanes == 2) transform.position = SkyLanes.KeepOut(Pos, team);
                            if (s.dmg > 0) foreach (var o in Opp(Pos, s.size / 2 + .6f)) if (hit.Add(o)) AiStrike(o, s, P * s.dmg, Pos);
                            yield return null;
                        }
                    }
                    break;
                case KShape.Leap:
                    {
                        var start = Pos; float T = .45f, tt = 0;
                        while (tt < T && alive) { tt += Time.deltaTime; transform.position = G.Clamp(Vector3.Lerp(start, at, tt / T)) + Vector3.up * Mathf.Sin(tt / T * Mathf.PI) * 2; yield return null; }
                        transform.position = G.Clamp(at); Fx.Ring(Pos, s.size, col, .5f, .4f); Fx.Spawn("ground_hit", Pos, s.size * .5f, 2);
                        foreach (var o in Opp(Pos, s.size)) AiStrike(o, s, P * s.dmg, Pos);
                    }
                    break;
                case KShape.Blink:
                case KShape.Target:
                    if (t == null || t.Down || Main.Flat(t.Pos - Pos).magnitude > s.range + 1.5f) break;
                    if (s.shape == KShape.Blink) { Fx.Puff(Pos, col); transform.position = G.Clamp(t.Pos + Main.Flat(t.Pos - Pos).normalized * 1.2f); face = Main.Ang(t.Pos - Pos); Fx.Puff(Pos, col); }
                    if (s.tether > 0)
                    {
                        int nT = Mathf.Max(1, Mathf.RoundToInt(s.tether / .5f));
                        for (int i = 0; i < nT && alive && !t.Down; i++) { Fx.Beam(Pos + Vector3.up * 1.2f, t.Pos + Vector3.up, col, .5f, .1f); AiStrike(t, s, P * s.dmg / nT, Pos); hp = Mathf.Min(maxHp, hp + maxHp * s.heal / nT); yield return new WaitForSeconds(.5f); }
                        break;
                    }
                    Fx.Beam(Pos + Vector3.up * 1.2f, t.Pos + Vector3.up, col, .2f, .12f); AiStrike(t, s, P * s.dmg, Pos);
                    break;
                case KShape.Ally:
                    AiSupport(s, col);
                    break;
            }
            if (late && alive) Kits.Show(hid, si, s, Pos, at, n);
        }
        // the assassins' passive on AI heroes too: +30% on a target under 35% health
        // AI heroes' passives (as the player's, HeroKit.KitOut/KitIn), on basic hits and skills: the damage multiplier for a hit on o
        int aiShots, aiStacks; float aiLastFight = -99, aiFocusReady;
        public float AiHunt(ITarget o, bool skill = false) => AiHuntBase(o, skill) * (mightT > 0 ? Jungle.MightK : 1);   // the Giant's Might buff (jungle)
        float AiHuntBase(ITarget o, bool skill)
        {
            if (G.lane && d.kind == "champ" && o is Foe tw && tw.towerTier >= 0) return Lane.HeroVsTower(tw, team);   // heroes against structures (see Lane)
            if (!G.lane || AiKit == null) return 1;
            float k = 1; bool hero = o is Hero || o is Foe fc && fc.d.kind == "champ";
            bool low = o is Foe f && f.hp < f.maxHp * .35f || o is Hero h && h.hp < h.max * .35f;
            switch (aiKit.pid)
            {
                case "hunt": if (low) k *= 1.3f; break;
                case "petal": if (low) k *= 1.3f; if (!skill && hero && ++aiShots % 3 == 0) { k *= 1.6f; Fx.Burst(o.Pos + Vector3.up, 16, new Color(1, .55f, .8f), 4); } break;   // the third hit on a hero bursts its petals
                case "shadow": if (low) k *= 1.3f; if (Time.time - aiLastFight > 4) k *= 1.5f; break;
                case "rage": if (hp < maxHp * .5f) k *= 1.25f; break;
                case "third": if (!skill && ++aiShots % 3 == 0) { k *= 1.6f; if (o is Hero h3) h3.Afflict(0, 1); else if (o is Foe f3) f3.slow = Mathf.Max(f3.slow, 1); } break;
                case "ranger": if (o is Foe fr && (fr.slow > 0 || fr.stag > .2f) || o is Hero hr && (hr.slowT > 0 || hr.stunT > 0)) k *= 1.2f; break;
                case "stack": if (skill) { k *= 1 + .15f * aiStacks; aiStacks = hero ? Mathf.Min(3, aiStacks + 1) : 0; } break;
                case "qi": if (!skill && hero) for (int i = 0; i < 3; i++) aiCd[i] -= .4f; break;
                case "chains": if (o is Foe fc2 && (fc2.stag > .2f || fc2.slow > 0) || o is Hero hc2 && (hc2.stunT > 0 || hc2.slowT > 0)) k *= 1.25f; break;
                case "tigerfour": if (!skill && ++aiShots % 4 == 0) k *= 1.5f; break;
                case "burn": if (!skill) k *= 1.1f; break;   // the burn, as a little more on the hit
            }
            aiLastFight = Time.time; return k;
        }
        // damage coming in to an AI hero: its own cuts and shields (iron body, sanctuary), the spartan's front, the monk's focus block
        public float AiDamageTaken(float dmg, Vector3 from)
        {
            if (aiCut > 0 && Time.time < aiCutEnd) dmg *= 1 - aiCut;
            if (!G.lane || AiKit == null) return dmg;
            aiLastFight = Time.time;
            if (aiKit.pid == "front" && Vector3.Angle(Main.Dir(face), Main.Flat(from - Pos)) < 60) dmg *= .8f;
            if (aiKit.pid == "focus" && Time.time >= aiFocusReady) { aiFocusReady = Time.time + 8; G.Number(Pos + Vector3.up * (h + .5f), Loc.S("กันได้", "Blocked"), ""); return 0; }
            return dmg;
        }
        // a teammate hero (or the player on its side) within 4 m of this one's target: someone up front to hold the enemy
        bool FrontlineNear() { if (tgt == null) return false; var tp = tgt.Pos; foreach (var f in G.foes) if (f && f != this && f.alive && f.team == team && f.d.kind == "champ" && Main.Flat(f.Pos - tp).magnitude < 4) return true; return team == 0 && G.hero && !G.hero.dead && Main.Flat(G.hero.Pos - tp).magnitude < 4; }
        public float AiSpeedK => G.lane && AiKit != null && aiKit.pid == "wind" ? 1.1f : 1;
        void AiAura(float dt)
        {   // the priestess's blessing: she and her teammates near her heal 1% a second
            if (aiKit.pid != "aura") return;
            foreach (var f in G.foes) if (f && f.alive && f.team == team && f.d.kind == "champ" && Main.Flat(f.Pos - Pos).magnitude < 6) f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * .006f * dt);
            if (team == 0 && G.hero && !G.hero.dead && Main.Flat(G.hero.Pos - Pos).magnitude < 6) G.hero.Heal(G.hero.max * .006f * dt, true);
        }
        bool IsAssassin => d.model is "hero_samurai" or "hero_ninja" or "hero_hood";
        bool MateHurt(float r) => G.foes.Any(f => f && f != this && f.alive && f.team == team && f.d.kind == "champ" && f.hp < f.maxHp * .6f && Main.Flat(f.Pos - Pos).magnitude < r) || team == 0 && G.hero && !G.hero.dead && G.hero.hp < G.hero.max * .6f && Main.Flat(G.hero.Pos - Pos).magnitude < r;
        System.Collections.Generic.List<ITarget> Opp(Vector3 c, float r) => G.Opponents(team).Where(o => Main.Flat(o.Pos - c).magnitude - o.Rad <= r).ToList();
        static bool InLine(Vector3 p, Vector3 a, Vector3 n, float len, float half) { var d = Main.Flat(p - a); float along = Vector3.Dot(d, n); if (along < -.5f || along > len) return false; return (d - n * along).magnitude <= half; }

        void AiStrike(ITarget o, KSkill s, float dmg, Vector3 from, bool cc = true)
        {
            if (o == null || o.Down) return;
            dmg *= AiHunt(o, true);
            if (o is Hero h) { if (dmg > 0) h.Take(dmg, from); if (cc) h.Afflict(s.stun, s.slow + (aiKit.pid == "tide" ? 1.2f : 0)); return; }
            if (o is not Foe f) { o.Take(dmg, from); return; }
            bool tower = f.d.kind == "tower";
            if (dmg > 0) f.Damage(dmg, from, new Main.Hit { byFoe = true, col = Kits.SideCol(team), kb = tower || !cc ? 0 : s.knock, slow = tower || !cc ? 0 : s.slow });
            if (!tower && cc && f.alive && s.stun > 0) f.stag = Mathf.Max(f.stag, s.stun);
            if (dmg > 0 && aiKit.pid == "drain") hp = Mathf.Min(maxHp, hp + dmg * .1f);
        }
        IEnumerator AiTrap(Vector3 at, KSkill s, float P, Color col)
        {
            float end = Time.time + s.trap; Fx.Ring(at, s.size, col, .6f, .3f);
            while (Time.time < end && G.lane && G.mode == Main.Mode.Game)
            {
                var o = Opp(at, s.size).FirstOrDefault(x => !(x is Foe f && f.d.kind == "tower"));
                if (o != null) { Fx.Spawn("ground_hit", at, .8f, 2); AiStrike(o, s, P * s.dmg, at); yield break; }
                yield return new WaitForSeconds(.1f);
            }
        }
        void AiSupport(KSkill s, Color col)
        {
            bool any = s.heal > 0 || s.shieldSelf > 0 || s.shieldAll > 0 || s.atkBuff > 0 || s.cut > 0; if (!any) return;
            Fx.Ring(Pos, Mathf.Max(1.5f, s.size), col, .7f, .4f); Fx.Burst(Pos + Vector3.up, 24, col, 4, .8f, -1);
            var mates = G.foes.Where(f => f && f.alive && f.team == team && f.d.kind == "champ" && f != this && Main.Flat(f.Pos - Pos).magnitude < Mathf.Max(s.size, s.range)).ToList();
            var hero = G.hero; bool heroNear = team == 0 && hero && !hero.dead && Main.Flat(hero.Pos - Pos).magnitude < Mathf.Max(s.size, s.range);
            if (s.heal > 0 && s.tether <= 0) { hp = Mathf.Min(maxHp, hp + maxHp * s.heal); if (s.allies) { foreach (var f in mates) f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * s.heal); if (heroNear) hero.Heal(hero.max * s.heal); } }
            if (s.shieldSelf > 0) hp = Mathf.Min(maxHp, hp + maxHp * s.shieldSelf * .6f);   // no shield bar on AI heroes: part of it as health
            if (s.shieldAll > 0)
            {
                if (s.shape == KShape.Ally) { var low = mates.OrderBy(f => f.hp / f.maxHp).FirstOrDefault(); if (low) low.hp = Mathf.Min(low.maxHp, low.hp + low.maxHp * s.shieldAll * .6f); else hp = Mathf.Min(maxHp, hp + maxHp * s.shieldAll * .6f); }
                else { foreach (var f in mates) f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * s.shieldAll * .6f); }
                if (heroNear) hero.GiveShield(s.shieldAll);
            }
            if (s.atkBuff > 0) { aiAtkK = s.atkBuff; aiAtkEnd = Time.time + s.buffT; }
            if (s.cut > 0) { aiCut = s.cut; aiCutEnd = Time.time + Mathf.Max(s.cutT, s.dotT); }
        }
    }
}
