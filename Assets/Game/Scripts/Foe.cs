using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Enemies and bosses. Static monster models move with code-driven hops/leans; animated Quaternius
    // characters play their own Walk/Attack/Hit/Death clips (glTFast legacy Animation).
    public class Foe : MonoBehaviour, ITarget
    {
        public FoeDef d; public BossDef bd; public bool isBoss;
        public float hp, maxHp, rad, h, face, slow, pull, stag, burn, burnD, burnT, atkCD, st, t, flash, kx, kz, y;
        public bool alive = true, spawning = true;
        public float poise, poiseMax, brokenT;   // stagger meter: full -> broken (open to a finisher)
        public bool Broken => brokenT > 0;
        public int team = 1;   // 1 = enemies of the player, 0 = the player's side (lane mode)
        public Vector3 Pos => transform.position;
        public float Rad => rad; public bool Down => !alive || spawning; public int Team => team;
        public int laneIdx = 1;   // which lane this unit walks (three-lane mode)
        public float PowerK => Mathf.Clamp01(1 - skillCD / 6f);   // AI hero power: how close its skill is to ready
        public void Take(float dmg, Vector3 from) => Damage(dmg, from, new Main.Hit { byFoe = true, col = team == 0 ? new Color(1, .4f, .4f) : new Color(1, .85f, .6f) });
        string animSet; ITarget tgt; float retarget, skillCD = 4; bool skillWind; Animator anim;
        enum S { Chase, Wind, Rec, Dead } S s = S.Chase;
        string bossMode = "chase"; float bossMt = 2.5f, st2; int bossStage; bool raged;
        Vector3 slamAt; Transform body; Look look; Animation clipAnim; float baseScale;
        Color burnC; Main G => Main.G;

        public void Init(FoeDef def, Vector3 p, BossDef boss, int team = 1)
        {
            d = def; bd = boss; isBoss = boss != null; this.team = team;
            if (isBoss) d = new FoeDef { id = "boss", model = boss.model, name = boss.name, hp = boss.hp, spd = 2.6f, dmg = 150, range = 4.5f, kind = "boss", scale = boss.scale, rad = 1, gold = 0, fly = boss.fly };
            transform.position = p;
            GameObject go;
            if (d.model.StartsWith("@")) go = Lane.Model(d.model, team, transform);
            else if (d.model.StartsWith("hero_"))
            {   // AI hero: the real rigged hero with its Mixamo clips and a random weapon
                go = Instantiate(Resources.Load<GameObject>("Heroes/Hero_" + d.model.Substring(5)), transform); anim = go.GetComponentInChildren<Animator>();
                animSet = Data.AnimSet(d.model.Substring(5)); if (animSet != null && Resources.Load<RuntimeAnimatorController>("Anim/" + animSet) is RuntimeAnimatorController oc) anim.runtimeAnimatorController = oc;
                var sock = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SwordSocket");
                if (sock) { var w = Data.Swords[Random.Range(0, Data.Swords.Length)]; Instantiate(Catalog.I.Model(w.id), sock); }
            }
            else if (d.model.StartsWith("rig_"))
            {   // realistic monster, auto-rigged like the heroes: Humanoid avatar + the shared clips; its weapon is part of the model
                var id = d.model == "rig_minion" ? (team == 0 ? "m_blue" : "m_red") : d.model.Substring(4);
                go = Instantiate(Resources.Load<GameObject>("Heroes/Hero_" + id), transform); anim = go.GetComponentInChildren<Animator>();
            }
            else go = Instantiate(Catalog.I.Model(d.model), transform);
            body = go.transform;
            clipAnim = go.GetComponentInChildren<Animation>();
            // animated models: normalize to the def's height (scale field = metres tall); static ones use scale directly
            float rawH = Bounds(go).size.y;
            baseScale = clipAnim ? d.scale / Mathf.Max(.01f, rawH) : d.scale;
            body.localScale = Vector3.one * baseScale;
            h = rawH * baseScale; rad = d.rad;
            if (d.kind == "tower") { var tb = Bounds(go); rad = Mathf.Max(d.rad, Mathf.Max(tb.extents.x, tb.extents.z) * .8f); }   // solid footprint = the model's width
            look = new Look(go);
            if (isBoss) look.Set(boss.tint, boss.glow);
            else if (team == 0) look.Set(Color.white, new Color(.02f, .08f, .25f));
            else if (anim || d.kind == "tower") look.Set(Color.white, new Color(.2f, .02f, .01f));
            else look.Set(new Color(1, .76f, .72f), new Color(.25f, .02f, .01f));
            float sc = G.lane ? 1 : 1 + (G.stage - 1) * (isBoss ? .3f : .35f); hp = maxHp = d.hp * sc;
            poiseMax = isBoss ? 320 : d.kind == "tower" ? 1e9f : Mathf.Max(40, d.hp * .35f);
            y = d.fly ? 1.1f : 0; atkCD = Random.Range(.5f, 1.5f); t = Random.Range(0, 10f);
            st = isBoss ? 1.2f : .7f; transform.localScale = Vector3.one * .01f;
            if (isBoss) Fx.Ring(p, 4, boss.color * .6f, 1.2f, .12f); else if (G.lane) Fx.Ring(p, 1.8f, Lane.TeamColor(team) / 2.2f, .7f);
            Fx.Dust(p, isBoss ? 1.6f : .7f, isBoss ? 22 : 9);
            Play("Idle", "Flying_Idle");
        }
        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
        }

        // ---------- clips (animated enemies) ----------
        string cur;
        void Play(params string[] names)
        {
            if (!clipAnim) return;
            foreach (var n in names)
            {
                var st = clipAnim.Cast<AnimationState>().FirstOrDefault(a => a.name == n || a.name.EndsWith("|" + n));
                if (st == null) continue; if (cur == st.name && clipAnim.IsPlaying(st.name)) return;
                cur = st.name; st.wrapMode = (n.Contains("Death") || n.Contains("Attack") || n.Contains("Punch") || n.Contains("Bite") || n.Contains("Hit") || n.Contains("Headbutt") || n.Contains("Weapon")) ? WrapMode.ClampForever : WrapMode.Loop;
                clipAnim.CrossFade(st.name, .12f); return;
            }
        }
        void PlayAttack()
        {
            if (anim) { int k = skillWind ? 2 : Random.Range(0, 2); var c = Catalog.I.Clip(animSet, "atk" + k); anim.SetFloat("Speed", 1.4f); anim.CrossFadeInFixedTime("atk" + k, .06f, 0, Mathf.Max(0, c.hit - .5f)); return; }
            cur = null; Play("Attack", "Punch", "Bite_Front", "Headbutt", "Weapon");
        }
        string animState;
        void AnimLoop(string st) { if (!anim || animState == st) return; animState = st; anim.SetFloat("Speed", 1); anim.CrossFadeInFixedTime(st, .15f); }

        // ---------- damage ----------
        public void Damage(float dmg, Vector3 from, Main.Hit o = null)
        {
            if (!alive) return; o ??= new Main.Hit(); var hero = G.hero; var sw = o.byFoe ? new SwordDef { color = o.col ?? Color.white } : hero.sword;
            bool crit = Random.value < (o.byFoe ? .05f : (o.melee && sw.crit > 0 ? sw.crit : .12f) + hero.CritBonus);
            dmg = Mathf.Round(CombatRules.Current.ModifyDamage(dmg) * (crit ? 1.8f : 1) * (brokenT > 0 ? 1.3f : 1) * Random.Range(.9f, 1.1f)); hp -= dmg; flash = .12f;
            if (o.poise > 0 && brokenT <= 0) { poise += CombatRules.Current.ModifyStun(o.poise); if (poise >= poiseMax) Break(); }
            G.Number(Pos + Vector3.up * (y + h), dmg.ToString(), crit ? "crit" : "");
            if (o.melee && sw.slow) slow = 2; if (o.slow > 0) slow = Mathf.Max(slow, o.slow);
            if (o.melee && sw.burn != null) { burn = 3; burnD = dmg * .14f; burnC = sw.burn == "poison" ? new Color(.6f, 1, .35f) : new Color(1, .48f, .06f); }
            if (o.melee && sw.steal > 0) hero.Heal(dmg * sw.steal, true);
            if (o.steal > 0) hero.Heal(dmg * o.steal, true);
            if (o.kb > 0 && !isBoss && d.kind != "tower")
            {
                var away = Main.Flat(Pos - from).normalized; kx = away.x * o.kb * 2.2f; kz = away.z * o.kb * 2.2f; stag = Mathf.Max(stag, .22f + o.kb * .03f);
                if (s == S.Wind && o.kb >= 4) { s = S.Chase; atkCD = 1; }
                if (clipAnim) { cur = null; Play("HitRecieve", "HitReact"); }
                if (anim) { animState = null; anim.CrossFadeInFixedTime("hurt", .05f); }
            }
            var c = o.col ?? sw.color;
            Fx.Burst(Pos + Vector3.up * (y + h * .55f), crit ? 16 : 8, c, crit ? 7 : 5);
            if (crit) { Fx.Spawn("hit_yellow", Pos + Vector3.up * (y + h * .55f), .7f, 2); if (o.melee) Fx.SlashFx("sx_shiny", Pos + Vector3.up * (y + h * .55f), Main.Ang(Pos - from), .9f, Random.Range(-40, 40)); }
            if (hp <= 0) Kill();
        }

        void Kill()
        {
            alive = false; s = S.Dead; st = 0; if (team == 1) { G.kills++; if (G.hero && !G.hero.dead) G.AddXp(isBoss ? Data.BossXp : d.gold); if (G.hero && !G.hero.dead) G.save.AddMastery(G.hero.sword.id, isBoss ? Data.BossMasteryXp : 1); }   // mastery for the weapon in hand
            Fx.Burst(Pos + Vector3.up * h * .5f, isBoss ? 140 : 26, isBoss ? bd.color : new Color(1, .48f, .29f), isBoss ? 10 : 6);
            Fx.Dust(Pos, isBoss ? 1.8f : .8f, isBoss ? 26 : 10); Fx.Burst(Pos + Vector3.up * h * .45f, isBoss ? 40 : 14, new Color(1, .55f, .25f), isBoss ? 6 : 4, .45f, 1.6f, .08f);
            if (team == 1) G.DropLoot(Pos, isBoss, d.gold);
            if (clipAnim) { cur = null; Play("Death"); }
            if (anim) { anim.SetFloat("Speed", 1.2f); anim.CrossFadeInFixedTime("death", .1f, 0, 1.4f); }
            G.OnFoeDown(this);
            if (isBoss) { G.hitStop = .25f; G.shake = .6f; G.ui.HideBoss(); Invoke(nameof(Win), 2.2f); }
        }
        void Win() { G.Finish(true); }

        public void Break()
        {
            brokenT = 2.6f; stag = 2.6f; poise = 0; s = S.Chase; skillWind = false;
            G.Number(Pos + Vector3.up * (y + h + .3f), Loc.S("เสียหลัก!", "Staggered!"), "crit"); Fx.Ring(Pos, 2.4f, new Color(1, .8f, .3f), .5f);
            if (clipAnim) { cur = null; Play("HitRecieve", "HitReact"); }
            if (anim) { animState = null; anim.CrossFadeInFixedTime("hurt", .05f); }
        }
        // the player parried this attack: lose balance (and break if the meter fills)
        public void Parried() { if (!alive) return; s = S.Rec; st = .6f; poise += poiseMax * .55f; if (poise >= poiseMax) Break(); else stag = Mathf.Max(stag, .7f); }
        public void EndBreak() { brokenT = 0; }

        // ---------- update ----------
        void Update()
        {
            if (G.paused) return;
            float dt = Time.deltaTime; t += dt; flash -= dt;
            if (brokenT > 0) { brokenT -= dt; if (Random.value < .3f) Fx.Burst(Pos + Vector3.up * (y + h + .2f), 1, new Color(1, .85f, .3f), 1.5f, .5f, 0); }
            else if (poise > 0) poise = Mathf.Max(0, poise - poiseMax * .06f * dt); look.Flash(flash > 0 ? .7f : 0);
            if (s == S.Dead)
            {
                st += dt; float k = st / (clipAnim || anim ? 1.6f : d.kind == "tower" ? 1 : .6f);
                if (d.kind == "tower") transform.position += Vector3.down * dt * 3;
                else if (!clipAnim && !anim) { transform.localScale = Vector3.one * Mathf.Max(.01f, 1 - k); transform.Rotate(0, dt * 570, 0); transform.position += Vector3.up * dt; }
                else if (k > .7f) transform.position += Vector3.down * dt * .8f;
                if (k >= 1 && !isBoss) Destroy(gameObject); else if (k >= 1) gameObject.SetActive(false);
                return;
            }
            if (spawning) { st -= dt; transform.localScale = Vector3.one * Mathf.Clamp(1 - st / .7f, .01f, 1); if (st <= 0) { spawning = false; transform.localScale = Vector3.one; } return; }
            if (G.mode != Main.Mode.Game) { Play("Idle", "Flying_Idle"); AnimLoop("idle"); return; }
            if (burn > 0) { burn -= dt; burnT -= dt; if (burnT <= 0) { burnT = .5f; Damage(burnD, Pos, new Main.Hit { col = burnC }); if (!alive) return; } if (Random.value < .3f) Fx.Burst(Pos + Vector3.up * h * .6f, 1, burnC, 1.5f, .5f, -.3f); }
            var hero = G.hero;
            retarget -= dt; if (retarget <= 0 || tgt == null || tgt.Down) { retarget = .3f; tgt = G.PickTarget(this); }
            bool has = tgt != null && !tgt.Down;
            // no target in lane mode: march toward the other side's base
            var goal = has ? tgt.Pos : G.lane ? Lane.Goal(Pos, team, laneIdx) : Pos;
            var to = Main.Flat(goal - Pos); float dd = has ? Mathf.Max(0, to.magnitude - tgt.Rad + .45f) : 99, want = Main.Ang(to);
            if (pull > 0) { pull -= dt; if (dd > 1.3f && d.kind != "tower") transform.position += to.normalized * 9 * dt; }
            slow -= dt; float sp = d.spd * (slow > 0 ? .45f : 1) * (raged ? 1.3f : 1) * (!d.fly && River.InWater(Pos.x, Pos.z) ? .7f : 1);
            transform.position += new Vector3(kx, 0, kz) * dt; kx *= Mathf.Exp(-dt * 8); kz *= Mathf.Exp(-dt * 8);
            bool moving = false; float lean = 0, squash = 1;
            if (stag > 0) { stag -= dt; lean = -.25f; }
            else if (isBoss) moving = BossUpdate(dt, to, dd, want, sp);
            else if (s == S.Chase)
            {
                atkCD -= dt; skillCD -= dt; if (d.kind != "tower") face += Main.AngDiff(face, want) * Mathf.Min(1, dt * 8);
                float mv = 0;
                if (d.kind == "tower") mv = 0;
                else if (!has) mv = G.lane ? 1 : 0;
                else if (d.kind == "ranged") { if (dd > 7) mv = 1; else if (dd < 4.5f) mv = -.7f; } else if (dd > d.range * .8f) mv = 1;
                if (mv != 0) { transform.position += Main.Dir(face) * sp * mv * dt; moving = true; }
                skillWind = d.kind == "champ" && skillCD <= 0 && has && dd < 3.5f;
                if (has && (skillWind || atkCD <= 0 && dd < d.range + .3f))
                {
                    s = S.Wind; st = skillWind ? .8f : d.kind == "slam" ? .9f : d.kind == "ranged" || d.kind == "tower" ? .6f : .5f; face = d.kind == "tower" ? face : want;
                    if (skillWind) { skillCD = Random.Range(6f, 9f); Fx.Telegraph(Pos, 3.4f, st); }
                    else if (d.kind == "slam") { slamAt = Pos + Main.Dir(face) * 1.2f; Fx.Telegraph(slamAt, 2.2f, st); }
                    else if (d.kind == "melee" && team == 1 && !G.lane) Fx.Telegraph(Pos, d.range + .4f, st, face, .75f);
                    PlayAttack();
                }
            }
            else if (s == S.Wind) { st -= dt; lean = -.3f; squash = .85f; if (st <= 0) { Strike(dd, want); s = S.Rec; st = .45f; } }
            else if (s == S.Rec) { st -= dt; lean = .35f * Mathf.Max(0, st / .45f); if (st <= 0) { s = S.Chase; atkCD = Random.Range(1.1f, 2.2f); } }
            // separation from other enemies and from the hero; stay inside the arena
            foreach (var o in G.foes) { if (!o || o == this || !o.alive || o.spawning) continue; var v = Main.Flat(Pos - o.Pos); float l = v.magnitude, m = rad + o.rad; if (l < m && l > .001f) transform.position += v / l * (m - l) * .5f; }
            if (has && tgt is Hero && d.kind != "tower") { float dist = to.magnitude; if (dist < rad + .45f && dist > .001f) transform.position -= to / dist * (rad + .45f - dist); }
            if (d.kind != "tower") transform.position = G.Clamp(transform.position, 1);
            // pose
            float yy = y, sy = 1;
            if (d.fly) { yy = y + Mathf.Sin(t * 6) * .15f; if (!clipAnim) sy = 1 + Mathf.Sin(t * 18) * .06f; }
            else if (moving && !isBoss && !clipAnim && !anim) { float hp_ = Mathf.Abs(Mathf.Sin(t * sp * 3)); yy += hp_ * .16f; sy = 1 + (hp_ - .5f) * .12f; }
            var p = transform.position; p.y = yy + (d.fly ? Mathf.Max(0, River.Ground(p.x, p.z)) : River.Ground(p.x, p.z)); transform.position = p;
            if (d.kind != "tower") transform.rotation = Quaternion.Euler(0, face * Mathf.Rad2Deg, 0);
            if (anim && s == S.Chase && stag <= 0 && (!isBoss || bossMode == "chase"))
            {
                AnimLoop(moving ? "run" : "idle");
                if (moving) { var gc = Catalog.I.Clip(animSet, "run"); if (gc.speed > .1f) anim.SetFloat("Speed", Mathf.Clamp(sp / (gc.speed * baseScale), .6f, 1.9f)); }   // feet stay planted
            }
            else if (anim) animState = null;
            if (clipAnim && cur != null && s != S.Dead) { var cs = clipAnim[cur]; if (cs != null && cs.wrapMode == WrapMode.ClampForever && cs.normalizedTime >= 1 && s != S.Wind) cur = null; }
            if (clipAnim) { if ((s == S.Chase || cur == null) && stag <= 0) { if (moving) Play(sp > 3 ? "Run" : "Walk", "Walk", "Fast_Flying"); else Play("Idle", "Flying_Idle"); } }
            else if (!anim && d.kind != "tower") { body.localRotation = Quaternion.Euler(lean * Mathf.Rad2Deg, 0, 0); float sxz = isBoss ? 1 : Mathf.Sqrt(2 - sy * squash); body.localScale = new Vector3(baseScale * sxz, baseScale * sy * squash, baseScale * sxz); }
        }

        void Strike(float dd, float want)
        {
            if (tgt == null || tgt.Down) { skillWind = false; return; }
            var col = G.lane ? Lane.TeamColor(team) / 2.2f : new Color(1, .3f, .37f);
            if (skillWind) { skillWind = false; G.HitTeamArea(team, Pos, 3.4f, d.dmg * 2.4f); Fx.Ring(Pos, 3.6f, col, .45f, .25f); Fx.Spawn("ground_hit", Pos, 1.3f, 2); return; }
            if (d.kind == "melee" || d.kind == "champ") { if (dd < d.range + .5f && Mathf.Abs(Main.AngDiff(face, want)) < .9f) tgt.Take(d.dmg, Pos); if (d.kind == "champ" || isBoss || d.scale > 1.5f) Fx.SlashFx("sx_basic", Pos + Vector3.up + Main.Dir(face) * d.range * .5f, face, .8f); }
            else if (d.kind == "slam") { G.HitTeamArea(team, slamAt, 2.4f, d.dmg); Fx.Ring(slamAt, 2.4f, new Color(.6f, 1, .35f), .4f); Fx.Spawn("ground_hit", slamAt, 1, 2); G.shake = Mathf.Max(G.shake, .12f); }
            else if (d.kind == "ranged" || d.kind == "tower")
            {
                bool tower = d.kind == "tower";
                var from = Pos + (tower ? Vector3.up * Lane.TopY : Vector3.up + Main.Dir(face) * .5f);
                var dir = (tgt.Pos + Vector3.up - from).normalized;
                Shot.Fire(from, dir * (tower ? 16 : 9), d.dmg, tower ? Lane.TeamColor(team) / 2.2f : new Color(1, .42f, .12f), tower ? "big" : "orb", 2.5f, enemy: team == 1, trail: true, byFoe: true);
            }
        }

        // ---------- boss ----------
        bool BossUpdate(float dt, Vector3 to, float dd, float want, float sp)
        {
            var hero = G.hero; bossMt -= dt; bool moving = false; var C = bd.color;
            if (!raged && hp < maxHp * .5f) { raged = true; look.Set(bd.tint, bd.rage); G.ui.Banner(bd.name + Loc.S("คลั่ง!", " is enraged!"), Loc.S("การโจมตีเร็วและรุนแรงขึ้น", "Attacks are faster and harder")); G.shake = .4f; Fx.Ring(Pos, 3.5f, bd.rage * 3, .8f, .2f); }
            float wind = raged ? .7f : 1;
            if (bd.fly) y = .15f + Mathf.Sin(t * 2) * .12f;
            if (bossMode == "chase")
            {
                face += Main.AngDiff(face, want) * Mathf.Min(1, dt * 4);
                if (dd > 3.2f && !hero.dead) { transform.position += Main.Dir(face) * sp * dt; moving = true; if (!bd.fly) y = Mathf.Abs(Mathf.Sin(t * 5)) * .25f; } else if (!bd.fly) y = 0;
                if (bossMt <= 0 && !hero.dead)
                {
                    int adds = G.foes.Count(o => o && o.alive && !o.isBoss);
                    var pool = dd < 5 ? new[] { "sweep", "sweep", "souls", "dash" } : new[] { "souls", "dash", "sweep" };
                    if (adds < 3) pool = pool.Append("summon").ToArray();
                    bossMode = pool[Random.Range(0, pool.Length)]; bossStage = 0;
                    if (anim) { animState = null; anim.SetFloat("Speed", 1); anim.CrossFadeInFixedTime(bossMode == "sweep" ? "heavy" : bossMode == "dash" ? "roll" : "cast2", .1f); }
                    else if (bossMode == "sweep" || bossMode == "souls") PlayAttack();
                    if (bossMode == "sweep") { face = want; Fx.Telegraph(Pos, 5, wind, face, 1.1f); st = wind; }
                    else if (bossMode == "souls") { st = .9f * wind; Fx.Ring(Pos, 2.5f, C, .9f); }
                    else if (bossMode == "dash") { st = .45f; Fx.Burst(Pos + Vector3.up * 1.4f, 40, C, 4, .6f, -.2f); }
                    else { st = 1; Fx.Burst(Pos + Vector3.up * 2.5f, 40, C, 4, .7f, -.4f); }
                }
            }
            else if (bossMode == "sweep")
            {
                st -= dt; if (st > 0 && !anim) body.localRotation = Quaternion.Euler(0, -30 * (1 - st / wind), 0);
                if (st <= 0 && bossStage == 0)
                {
                    bossStage = 1; st2 = .35f; if (dd < 5.4f && Mathf.Abs(Main.AngDiff(face, Main.Ang(to))) < 1.15f) hero.Hurt(d.dmg, Pos);
                    Fx.SlashFx("sx_explode", Pos + Vector3.up * 1.2f + Main.Dir(face) * 2, face, 2.6f); Fx.Spawn("ground_hit", Pos + Main.Dir(face) * 2.5f, 1.5f, 2); G.shake = Mathf.Max(G.shake, .25f);
                }
                if (bossStage == 1) { st2 -= dt; if (!anim) body.Rotate(0, dt * 900, 0); if (st2 <= 0) { body.localRotation = Quaternion.identity; bossMode = "chase"; bossMt = Random.Range(1.2f, 2.2f) * wind; } }
            }
            else if (bossMode == "souls")
            {
                st -= dt; y += .6f * (1 - Mathf.Max(0, st)) * dt * 3;
                if (st <= 0)
                {
                    int n = raged ? 16 : 12; for (int r = 0; r < (raged ? 2 : 1); r++) for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2 + r * .2f; Shot.Fire(Pos + Vector3.up * 1.2f, Main.Dir(a) * (6 + r * 2), 85, C, "shard", 3.5f, enemy: true); }
                    Fx.Ring(Pos, 3, C * .5f, .5f, .1f); Fx.Dust(Pos, 1.2f, 14); bossMode = "chase"; bossMt = Random.Range(1.4f, 2.4f) * wind;
                }
            }
            else if (bossMode == "dash")
            {
                st -= dt; body.gameObject.SetActive(st > .2f || Mathf.FloorToInt(st * 30) % 2 == 0);
                if (st <= 0 && bossStage == 0)
                {
                    bossStage = 1; float a = hero.face + Mathf.PI + Random.Range(-.6f, .6f); var p = hero.Pos + Main.Dir(a) * 2.6f; transform.position = new Vector3(p.x, 0, p.z); body.gameObject.SetActive(true);
                    Fx.Dust(Pos, 1.3f, 16); face = Main.Ang(hero.Pos - Pos); bossMode = "sweep"; bossStage = 0; st = .6f * wind; Fx.Telegraph(Pos, 5, .6f * wind, face, 1.1f);
                }
            }
            else if (bossMode == "summon")
            {
                st -= dt; if (st <= 0) { for (int i = 0; i < 3; i++) { float a = Random.Range(0, Mathf.PI * 2); G.SpawnFoe(bd.summon, Pos + Main.Dir(a) * 2.5f); } bossMode = "chase"; bossMt = Random.Range(1.5f, 2.5f); }
            }
            if (raged && Random.value < .4f) Fx.Burst(Pos + new Vector3(Random.Range(-.5f, .5f), Random.Range(.5f, 2.5f), Random.Range(-.5f, .5f)), 1, bd.rage * 3, 1, .6f, -.3f);
            return moving;
        }
    }
}
