using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Enemies and bosses. Static monster models move with code-driven hops/leans; animated Quaternius
    // characters play their own Walk/Attack/Hit/Death clips (glTFast legacy Animation).
    public partial class Foe : MonoBehaviour, ITarget
    {
        public FoeDef d, baseDef; public BossDef bd; public bool isBoss;   // baseDef: an AI hero's def before the difficulty was applied (respawns start from it)
        public void Spirit(Color c, Color lift) => look?.Spirit(c, lift);
        public Vector3 home; public float leash; bool aggro; const float AggroRange = 10, LoseRange = 22;   // open world: stays around home until the hero comes close or strikes it; leash 0 = always hunts
        public float hp, maxHp, rad, h, face, slow, pull, stag, burn, burnD, burnT, atkCD, st, t, flash, kx, kz, y;
        public bool alive = true, spawning = true;
        public float poise, poiseMax, brokenT;   // stagger meter: full -> broken (open to a finisher)
        public bool Broken => brokenT > 0;
        public int team = 1;   // 1 = enemies of the player, 0 = the player's side (lane mode)
        public Jungle.Camp camp; public ITarget lastHitBy; public float mightT, hasteT; Jungle.Camp jungleCamp; public Jungle.Camp JungleCamp => jungleCamp; public ITarget Tgt => tgt;   // Sky Lanes jungle: a camp monster (team 2) and who struck it last; an AI hero's buffs and the camp it is clearing
        public Vector3 Pos => transform.position;
        public float Rad => rad; public bool Down => !alive || spawning; public int Team => team;
        public int laneIdx = 1;   // which lane this unit walks (three-lane mode)
        public int aiLv = 1;   // Sky Lanes: an AI hero's level this match (its side's XP), its health and damage grown to it like the player's
        public int towerTier = -1;   // lane maps: 0 the outermost tower, then 1, 2 ... inward; the core is one above its lane's last tower (-1: not a tower)
        // towers fall in order: an inner tower takes no damage while its lane's outer tower stands, the core none while every lane still has a tower
        public bool Shielded
        {
            get
            {
                if (towerTier < 1 || !G.lane) return false;
                bool Stands(int lane, int tier) { foreach (var f in G.foes) if (f && f.alive && f.team == team && f.towerTier == tier && (lane < 0 || f.laneIdx == lane)) return true; return false; }
                if (d.id != "core") return Stands(laneIdx, towerTier - 1);   // the tower in front of it still stands
                bool AnyTower(int lane) { foreach (var f in G.foes) if (f && f.alive && f.team == team && f.d.id == "tower" && f.laneIdx == lane) return true; return false; }
                int lanes = Lane.Lanes == 2 ? 2 : Lane.Lanes; for (int l = 0; l < lanes; l++) if (!AnyTower(l)) return false;   // a lane cleared: the core is open
                return true;
            }
        }
        float shieldNoteT;
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
            if (isBoss) d = new FoeDef { id = "boss", model = boss.model, name = boss.name, hp = boss.hp, spd = boss.spd > 0 ? boss.spd : 2.6f, dmg = boss.dmg > 0 ? boss.dmg : 150, range = 4.5f, kind = "boss", scale = boss.scale, rad = boss.model.StartsWith("hero_") ? .6f : 1, gold = 0, fly = boss.fly };
            transform.position = p;
            GameObject go;
            if (d.model.StartsWith("@")) go = Lane.Model(d.model, team, transform);
            else if (d.model.StartsWith("hero_"))
            {   // AI hero: the real rigged hero with its Mixamo clips and a random weapon
                go = Instantiate(Resources.Load<GameObject>("Heroes/Hero_" + d.model.Substring(5)), transform); FootWeights.Matte(go); anim = go.GetComponentInChildren<Animator>(); SleeveHang.Attach(go); Grip.Attach(go);   // hanging sleeves and closed hands, as on the player
                animSet = Data.AnimSet(d.model.Substring(5)); if (animSet != null && Resources.Load<RuntimeAnimatorController>("Anim/" + animSet) is RuntimeAnimatorController oc) anim.runtimeAnimatorController = oc;
                var sock = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SwordSocket");
                if (sock) { var w = Data.Swords[Random.Range(0, Data.Swords.Length)]; Instantiate(Catalog.I.Model(w.id), sock); }
            }
            else if (d.model.StartsWith("rig_"))
            {   // realistic monster, auto-rigged like the heroes: Humanoid avatar + the shared clips; its weapon is part of the model
                var id = d.model == "rig_minion" ? (team == 0 ? "m_blue" : "m_red") : d.model.Substring(4);   // (KayKit knights Hero_kk_knight_* exist for a toy-style map; the painted Sky Lanes uses the realistic soldiers)
                go = Instantiate(Resources.Load<GameObject>("Heroes/Hero_" + id), transform); anim = go.GetComponentInChildren<Animator>(); SleeveHang.Attach(go); Grip.Attach(go);   // hanging sleeves and closed hands, as on the player
                if (anim && G.lane) anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;   // soldiers off the screen stop working their bones (dozens of animators were the cost on the web)
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
            if (Main.IsPhone && !isBoss) foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // phones: a crowd draws once, not twice
            if (isBoss) look.Set(boss.tint, boss.glow);
            else if (team == 0) look.Set(Color.white, new Color(.02f, .08f, .25f));
            else if (anim || d.kind == "tower") look.Set(Color.white, new Color(.2f, .02f, .01f));
            else look.Set(new Color(1, .76f, .72f), new Color(.25f, .02f, .01f));
            float sc = G.lane ? 1 : 1 + (G.stage - 1) * (isBoss ? .3f : .35f); hp = maxHp = d.hp * sc;
            poiseMax = isBoss ? (boss.poise > 0 ? boss.poise : 320) : d.kind == "tower" ? 1e9f : Mathf.Max(40, d.hp * .35f);
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
        public static System.Action<Foe, float, Vector3, Main.Hit> OnDamage;   // tests: who hits whom
        public void Damage(float dmg, Vector3 from, Main.Hit o = null)
        {
            aggro = true;
            if (!alive) return; o ??= new Main.Hit(); var hero = G.hero; OnDamage?.Invoke(this, dmg, from, o);
            if (camp != null) Jungle.Struck(this, from, o);
            if (towerTier >= 0 && G.lane && !o.byFoe) dmg *= Lane.HeroVsTower(this, 0);   // the player's hits on a structure (AI heroes: FoeKit.AiHunt)
            if (towerTier >= 1 && Shielded)
            {   // no shortcuts: the towers before it must fall first
                if (Time.time > shieldNoteT) { shieldNoteT = Time.time + .8f; G.Number(Pos + Vector3.up * (h + .6f), d.id == "core" ? Loc.S("ทำลายป้อมให้หมดหนึ่งเลนก่อน", "Clear a lane's towers first") : Loc.S("ทำลายป้อมด้านหน้าก่อน", "Break the outer tower first"), ""); Fx.Ring(Pos, rad + .8f, Lane.TeamColor(team) * .4f, .5f, .15f); }
                return;
            } var sw = o.byFoe ? new SwordDef { color = o.col ?? Color.white } : hero.sword;
            bool kitHit = !o.byFoe && G.lane && hero && hero.KitOn; if (kitHit) dmg *= hero.KitOut(this, o); dmg = AiDamageTaken(dmg, from);   // the hero's passive
            if (evading && !o.byFoe) { G.Number(Pos + Vector3.up * (y + h), Loc.S("หลบ", "Miss"), ""); return; }
            if (isBoss && !o.byFoe && bossMode == "chase" && stag <= 0 && brokenT <= 0 && bd.near != null && System.Array.IndexOf(bd.near, "sidestep") >= 0 && Random.value < SidestepChance)
            {   // a duellist reads the swing and steps out of it
                StartMove("sidestep", 0, face, raged ? .7f : 1); G.Number(Pos + Vector3.up * (y + h), Loc.S("หลบ", "Miss"), ""); return;
            }
            float poiseK = 1;
            if (guarding && !o.byFoe && Mathf.Abs(Main.AngDiff(face, Main.Ang(Main.Flat(from - Pos)))) < 1.5f)
            {   // a raised guard turns blows from the front aside and answers them; a kick or a counter breaks it
                if (o.poise >= 25 || o.kb >= 5) { guarding = false; G.Number(Pos + Vector3.up * (y + h + .3f), Loc.S("ทำลายการ์ด!", "Guard broken!"), "crit"); Break(); }
                else { dmg *= .1f; poiseK = .35f; Fx.Burst(Pos + Vector3.up * 1.3f + Main.Dir(face) * .5f, 12, Color.white, 4); G.Number(Pos + Vector3.up * (y + h), Loc.S("กัน!", "Blocked!"), ""); if (bossMode == "guard") Riposte(); }
            }
            bool crit = !o.skill && Random.value < (o.byFoe ? .05f : (o.melee && sw.crit > 0 ? sw.crit : .12f) + hero.CritBonus);
            dmg = Mathf.Round(CombatRules.Current.ModifyDamage(dmg) * (crit ? 1.8f : 1) * (brokenT > 0 ? 1.3f : 1) * Random.Range(.9f, 1.1f)); hp -= dmg; flash = .12f; if (kitHit) hero.KitHit(this, o, dmg);
            if (o.poise > 0 && brokenT <= 0) { poise += CombatRules.Current.ModifyStun(o.poise) * poiseK; if (poise >= poiseMax) Break(); }
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
            if (camp != null) Jungle.Down(this);
            alive = false; s = S.Dead; st = 0; if (d.kind == "champ") G.HeroKilled(Pos, team, d.name, false); G.TeamXp(1 - team, isBoss ? Data.BossXp : d.gold); if (team == 1) { G.kills++; G.QuestBump("kill"); if (isBoss) G.QuestBump("boss"); if (G.hero && !G.hero.dead) G.AddXp(isBoss ? Data.BossXp : d.gold); if (G.hero && !G.hero.dead) G.save.AddMastery(G.hero.sword.id, isBoss ? Data.BossMasteryXp : 1); }   // mastery for the weapon in hand
            Fx.Burst(Pos + Vector3.up * h * .5f, isBoss ? 140 : 26, isBoss ? bd.color : new Color(1, .48f, .29f), isBoss ? 10 : 6);
            Fx.Dust(Pos, isBoss ? 1.8f : .8f, isBoss ? 26 : 10); Fx.Burst(Pos + Vector3.up * h * .45f, isBoss ? 40 : 14, new Color(1, .55f, .25f), isBoss ? 6 : 4, .45f, 1.6f, .08f);
            if (team == 1) G.DropLoot(Pos, isBoss, d.gold);
            if (clipAnim) { cur = null; Play("Death"); }
            if (anim) { anim.SetFloat("Speed", 1.2f); anim.CrossFadeInFixedTime("death", .1f, 0, 1.4f); }
            G.OnFoeDown(this);
            if (isBoss) { G.hitStop = .25f; G.shake = .6f; G.ui.HideBoss(); guarding = evading = false; if (body) body.gameObject.SetActive(true); Invoke(nameof(Win), 2.2f); }
        }
        void Win() { if (G.sky) G.Zone.DuelWon(this); else G.Finish(true); }

        public void Break()
        {
            brokenT = 2.6f; stag = 2.6f; poise = 0; s = S.Chase; skillWind = false; if (isBoss) EndMove(1);
            G.Number(Pos + Vector3.up * (y + h + .3f), Loc.S("เสียหลัก!", "Staggered!"), "crit"); Fx.Ring(Pos, 2.4f, new Color(1, .8f, .3f), .5f);
            if (clipAnim) { cur = null; Play("HitRecieve", "HitReact"); }
            if (anim) { animState = null; anim.CrossFadeInFixedTime("hurt", .05f); }
        }
        // the player parried this attack: lose balance (and break if the meter fills)
        public void Parried() { if (!alive) return; if (isBoss) EndMove(1.1f); s = S.Rec; st = .6f; poise += poiseMax * .55f; if (poise >= poiseMax) Break(); else stag = Mathf.Max(stag, .7f); }
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
            retarget -= dt; if (retarget <= 0 || tgt == null || tgt.Down) { retarget = .3f; tgt = camp != null ? Jungle.TargetOf(this) : G.PickTarget(this); }
            bool has = tgt != null && !tgt.Down;
            if (leash > 0) { float hd = Main.Flat(hero.Pos - Pos).magnitude; if (hd < AggroRange) aggro = true; else if (hd > LoseRange || Main.Flat(hero.Pos - home).magnitude > leash) aggro = false; if (!aggro) has = false; }
            // no target in lane mode: march toward the other side's base
            Vector3? plan = G.lane && d.kind == "champ" && !G.duelOnly ? ChampPlan(ref has) : null;
            var goal = plan ?? (has ? tgt.Pos : camp != null ? home : G.lane ? Lane.Goal(Pos, team, laneIdx) : leash > 0 ? home : Pos);
            if (camp != null && !has && Main.Flat(home - Pos).magnitude < 1.2f) { hp = Mathf.Min(maxHp, hp + maxHp * .35f * dt); goal = Pos; }
            if (d.kind == "champ" && G.SkyMatch) goal = Jungle.Route(Pos, goal);   // into and out of the jungle by its stone paths
            mightT -= dt; hasteT -= dt;
            var to = Main.Flat(goal - Pos); float dd = has ? Mathf.Max(0, to.magnitude - tgt.Rad + .45f) : 99, want = Main.Ang(to);
            if (pull > 0) { pull -= dt; if (dd > 1.3f && d.kind != "tower") transform.position += to.normalized * 9 * dt; }
            slow -= dt; float sp = d.spd * AiSpeedK * (slow > 0 ? .45f : 1) * (raged ? 1.3f : 1) * (!d.fly && River.InWater(Pos.x, Pos.z) ? .7f : 1);
            transform.position += new Vector3(kx, 0, kz) * dt; kx *= Mathf.Exp(-dt * 8); kz *= Mathf.Exp(-dt * 8);
            bool moving = false; float lean = 0, squash = 1;
            if (stag > 0) { stag -= dt; lean = -.25f; }
            else if (kitBusy > 0) kitBusy -= dt;   // an AI hero mid-skill stands where it is
            else if (isBoss) moving = BossUpdate(dt, to, dd, want, sp);
            else if (s == S.Chase)
            {
                atkCD -= dt; skillCD -= dt; if (d.kind != "tower") face += Main.AngDiff(face, want) * Mathf.Min(1, dt * 8);
                float mv = 0;
                if (d.kind == "tower") mv = 0;
                else if (!has) mv = G.lane || (leash > 0 && to.magnitude > 1.5f) ? 1 : 0;
                else if (d.kind == "ranged") { if (dd > 7) mv = 1; else if (dd < 4.5f) mv = -.7f; }
                else if (d.shoot) { if (dd > d.range * .9f) mv = 1; else if (dd < d.range * .3f) mv = -.3f; else if (dd < d.range * .6f && G.lane && FrontlineNear()) mv = -.7f; }   // with a teammate in front to take the hits, the shooter keeps its distance (MOBA backline)   // steps back only a little when crowded: backing off at full speed made melee AI heroes all but harmless   // an archer or caster AI hero backs off to its range
                else if (dd > d.range * .8f) mv = 1;
                if (mv != 0) { transform.position += Main.Dir(face) * sp * mv * dt; moving = true; }
                if (d.kind == "champ" && G.lane && !Main.NoKitAi && AiKit != null) { skillCD = 99; if (KitAi(dt, has ? tgt : null, dd)) moving = false; }   // its own kit instead of the shared spin
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
            if (d.kind != "tower") foreach (var o in G.foes) { if (!o || o == this || !o.alive || o.spawning) continue; var v = Main.Flat(Pos - o.Pos); float l = v.magnitude, m = rad + o.rad; if (l < m && l > .001f) transform.position += v / l * (m - l) * (o.d.kind == "tower" ? 1 : .5f); }   // towers and cores never move: everyone else steps round them
            if (has && tgt is Hero && d.kind != "tower") { float dist = to.magnitude; if (dist < rad + .45f && dist > .001f) transform.position -= to / dist * (rad + .45f - dist); }
            if (d.kind != "tower") transform.position = G.Clamp(transform.position, G.lane && Lane.Lanes == 2 ? 0 : 1);
            if (d.kind != "tower" && G.lane && Lane.Lanes == 2) transform.position = SkyLanes.KeepOut(transform.position, team);   // the other side's spawn pool is closed to it   // the floating map: no standing over the drop
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
            if (d.shoot)
            {   // an AI hero that fights from range: a bolt in its team colour
                var from = Pos + Vector3.up * 1.2f + Main.Dir(face) * .5f; var dir = Main.Flat(tgt.Pos - Pos).normalized;
                Shot.Fire(from, dir * 15, d.dmg * AiHunt(tgt), Lane.TeamColor(team) / 2.2f, "orb", d.range / 15 + .3f, enemy: team == 1, trail: true, byFoe: true); return;
            }
            if (d.kind == "melee" || d.kind == "champ") { if (dd < d.range + .5f && Mathf.Abs(Main.AngDiff(face, want)) < .9f) tgt.Take(d.dmg * AiHunt(tgt), Pos); if (d.kind == "champ" || isBoss || d.scale > 1.5f) Fx.SlashFx("sx_basic", Pos + Vector3.up + Main.Dir(face) * d.range * .5f, face, .8f); }
            else if (d.kind == "slam") { G.HitTeamArea(team, slamAt, 2.4f, d.dmg); Fx.Ring(slamAt, 2.4f, new Color(.6f, 1, .35f), .4f); Fx.Spawn("ground_hit", slamAt, 1, 2); G.shake = Mathf.Max(G.shake, .12f); }
            else if (d.kind == "ranged" || d.kind == "tower")
            {
                bool tower = d.kind == "tower"; float dmg = d.dmg;
                if (tower && G.lane)
                {   // a hero under a tower with none of its own soldiers there takes heavy, growing shots (no diving towers alone)
                    bool heroTgt = tgt is Hero || tgt is Foe tf && tf.d.kind == "champ";
                    bool covered = G.foes.Any(m => m && m.alive && m.team == tgt.Team && Lane.Soldier(m.d) && Main.Flat(m.Pos - Pos).magnitude < d.range + 1);
                    if (heroTgt && !covered) { streak = tgt == streakOn ? streak + 1 : 0; streakOn = tgt; dmg *= TowerAlone * Mathf.Min(TowerRampMax, 1 + streak * TowerRamp); }
                    else { streak = 0; streakOn = null; if (heroTgt) dmg *= TowerVsHero; }   // heroes take heavier shots than soldiers even with soldiers about
                }
                if (d.id == "siege" && tgt is Foe ttw && ttw.d.kind == "tower") dmg *= Lane.SiegeVsTower;
                var from = Pos + (tower ? Vector3.up * Lane.TopY : Vector3.up + Main.Dir(face) * .5f);
                var dir = (tgt.Pos + Vector3.up - from).normalized;
                Shot.Fire(from, dir * (tower ? 16 : 9), dmg, tower ? Lane.TeamColor(team) / 2.2f : new Color(1, .42f, .12f), tower ? "big" : "orb", 2.5f, enemy: team == 1, trail: true, byFoe: true);
            }
        }

        // ---------- AI heroes in lane battles ----------
        // Goes home to its fountain when hurt, does not walk under an enemy tower without its own soldiers there, steps back when
        // outnumbered, and picks its target: the weakest hero in reach, a soldier it can finish, else the nearest.
        const float TowerVsHero = 1.8f, TowerAlone = 2.5f, TowerRamp = .35f, TowerRampMax = 2.2f, RetreatAt = .3f, BackAt = .9f, SeeRange = 12, DiveHp = .25f;
        int streak; ITarget streakOn; bool retreat;
        Foe EnemyTowerOver(Vector3 p, float pad)
        {
            foreach (var f in G.foes) if (f && f.alive && f.team != team && f.d.kind == "tower" && Main.Flat(f.Pos - p).magnitude < f.d.range + pad) return f;
            return null;
        }
        int OwnSoldiersNear(Vector3 p, float r) { int n = 0; foreach (var f in G.foes) if (f && f.alive && f.team == team && Lane.Soldier(f.d) && Main.Flat(f.Pos - p).magnitude < r) n++; return n; }
        bool SafeToReach(ITarget t) { var tw = EnemyTowerOver(t.Pos, .5f); return !tw || OwnSoldiersNear(tw.Pos, tw.d.range + 1) >= 2 || (t is Foe ft && ft.hp < ft.maxHp * DiveHp) || (t is Hero th && th.hp < th.max * DiveHp); }
        bool OpponentFocusing(ITarget target) => G.foes.Any(o => o && o.alive && o.team != team && o.d.kind == "champ" && o.tgt == target);
        public ITarget ChampTarget()
        {
            ITarget best = null; float bs = 1e9f; var jc = Jungle.ForAi(this); const float JungleFightReach = 5;   // a jungler with a camp up looks only at heroes on the way
            foreach (var o in G.Opponents(team))
            {
                float dist = Main.Flat(o.Pos - Pos).magnitude - o.Rad; if (dist > SeeRange * (G.AiLv.focus ? 1.25f : 1) || !SafeToReach(o) || o is Foe so && so.Shielded) continue;
                float hpK = o is Foe of ? of.hp / of.maxHp : o is Hero oh ? oh.hp / oh.max : 1; bool isHero = o is Hero || o is Foe oc && oc.d.kind == "champ"; bool tower = o is Foe ot && ot.d.kind == "tower";
                if (jc != null && (!isHero || dist > JungleFightReach)) continue;   // on its way to a camp: only an enemy hero right beside it is worth a fight (it used to stay in lane, always some hero in sight)
                float score = dist - (isHero ? 2 + (1 - hpK) * (G.AiLv.focus ? 11 : 6) : 0)   // hard AI hunts the weakest hero
                    - (!isHero && !tower && hpK < .35f ? 2.5f : 0) + (tower ? 3 : 0);
                if (isHero && OpponentFocusing(o)) score -= 3;   // join a teammate's fight instead of spreading damage
                if (isHero && IsAssassin && (o is Foe so2 && so2.d.shoot || o is Hero oh2 && oh2.def.ranged)) score -= 4;   // assassins dive for the shooters at the back
                if (score < bs) { bs = score; best = o; }
            }
            if (jc != null && best == null)
            {   // the jungler at its camp: a camp monster unless an enemy hero is about
                var m = Jungle.Member(jc, Pos, 9); if (m) return m;
            }
            return best;
        }
        Vector3? ChampPlan(ref bool has)
        {
            var homeCore = Lane.CoreOf(team); bool Lanes2 = Lane.Lanes == 2;
            if (hp < maxHp * RetreatAt) retreat = true; else if (retreat && hp > maxHp * BackAt) retreat = false;
            if (retreat) { has = false; return Lanes2 ? Lane.HealAt(team) : homeCore + new Vector3(0, 0, -Lane.Side(team) * 3); }   // back to the fountain
            int foesNear = 0, friendsNear = 1;
            foreach (var o in G.Opponents(team)) if ((o is Hero || o is Foe oc && oc.d.kind == "champ") && Main.Flat(o.Pos - Pos).magnitude < SeeRange) foesNear++;
            foreach (var f in G.foes) if (f && f != this && f.alive && f.team == team && f.d.kind == "champ" && Main.Flat(f.Pos - Pos).magnitude < SeeRange) friendsNear++;
            if (team == 0 && G.hero && !G.hero.dead && Main.Flat(G.hero.Pos - Pos).magnitude < SeeRange) friendsNear++;
            if (foesNear > friendsNear && hp < maxHp * .7f) { has = false; return Pos + (homeCore - Pos).normalized * 6; }   // outnumbered and hurt: give ground
            jungleCamp = Jungle.ForAi(this);   // the side's jungler: to the best camp up, past the soldiers in lane; it stops for enemy heroes and for its camp
            if (jungleCamp != null && !(has && (tgt is Hero || tgt is Foe tj && (tj.d.kind == "champ" || tj.camp != null)))) { has = false; return jungleCamp.at; }
            if (has) return null;
            var next = Lane.Goal(Pos, team, laneIdx); var tw = EnemyTowerOver(next, 1);
            if (tw && OwnSoldiersNear(tw.Pos, tw.d.range + 1) < 2) { var back = Main.Flat(Pos - tw.Pos); return tw.Pos + (back.sqrMagnitude > .01f ? back.normalized : -Main.Dir(face)) * (tw.d.range + 2); }   // wait for the soldiers before the tower
            return null;
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
                if (bossMt <= 0 && !hero.dead && bd.near != null) StartMove(PickMove(dd), dd, want, wind);
                else if (bossMt <= 0 && !hero.dead)
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
                    int n = raged ? 16 : 12; for (int r = 0; r < (raged ? 2 : 1); r++) for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2 + r * .2f; Shot.Fire(Pos + Vector3.up * 1.2f, Main.Dir(a) * (6 + r * 2), bd.hits > 0 ? d.dmg * .55f : 85, C, "shard", 3.5f, enemy: true); }
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
                st -= dt; if (st <= 0) { for (int i = 0; i < bd.summonN; i++) { float a = Random.Range(0, Mathf.PI * 2); G.SpawnFoe(bd.summon, G.Clamp(Pos + Main.Dir(a) * 2.5f, -1)); } bossMode = "chase"; bossMt = Random.Range(1.5f, 2.5f); }
            }
            else moving = DuelMove(dt, to, dd, want, sp, wind);
            if (raged && Random.value < .4f) Fx.Burst(Pos + new Vector3(Random.Range(-.5f, .5f), Random.Range(.5f, 2.5f), Random.Range(-.5f, .5f)), 1, bd.rage * 3, 1, .6f, -.3f);
            return moving;
        }

        // ---------- duel moves (open world duellists: BossDef.near / far) ----------
        // every blow is shown first (a red cone or circle on the ground) and every move leaves an opening after it
        bool guarding, evading; int side; Vector3 moveFrom, moveTo, moveDir;
        const float SidestepChance = .2f, ComboReach = 2.9f, ComboHalf = .95f, LeapR = 2.8f, LeapH = 3.2f, LungeSpeed = 16, LungeMax = 10;
        public string Move => bossMode;   // for the test log
        string PickMove(float dd)
        {
            var pool = dd < 5 ? bd.near : bd.far ?? bd.near; int adds = G.foes.Count(o => o && o.alive && !o.isBoss);
            for (int i = 0; i < 6; i++) { var m = pool[Random.Range(0, pool.Length)]; if (m == "summon" && (bd.summon == null || adds >= bd.summonN)) continue; if (m == "guard" && dd > 4) continue; return m; }
            return dd < 5 ? "combo" : "lunge";
        }
        // the clip's strike lands 'lead' seconds from now
        void Act(string clip, float lead, float speed = 1)
        {
            if (!anim) { if (clip != "roll" && clip != "blockidle") PlayAttack(); return; }
            animState = null; anim.SetFloat("Speed", speed); var c = Catalog.I.Clip(animSet, clip); anim.CrossFadeInFixedTime(clip, .06f, 0, Mathf.Max(0, c.hit - lead * speed));
        }
        void EndMove(float wait)
        {
            guarding = evading = false; bossMode = "chase"; bossMt = wait; bossStage = 0; if (body) body.gameObject.SetActive(true); if (body && !anim) body.localRotation = Quaternion.identity; if (!bd.fly) y = 0;
        }
        void StartMove(string m, float dd, float want, float wind)
        {
            bossMode = m; bossStage = 0; var hero = G.hero; face = m == "sidestep" ? face : want;
            switch (m)
            {
                case "combo": st = .6f * wind; ComboTell(); break;
                case "leap": moveFrom = Pos; moveTo = G.Clamp(Main.Flat(hero.Pos), -1); st = 1.05f * wind; Fx.Telegraph(moveTo, LeapR, st); Act("heavy", st); break;
                case "guard": guarding = true; st = Random.Range(1.3f, 1.9f); side = Random.value < .5f ? -1 : 1; Act("blockidle", 0); Fx.Ring(Pos, 1.6f, Color.white * .6f, .4f); break;
                case "sidestep": evading = true; st = .42f; side = Random.value < .5f ? -1 : 1; moveDir = Main.Dir(face + side * 1.9f); if (anim) { animState = null; anim.SetFloat("Speed", 2.2f); anim.CrossFadeInFixedTime("roll", .04f, 0, .93f); } break;
                case "lunge": st = .5f * wind; float r = Mathf.Min(dd + 1.6f, LungeMax); Fx.Telegraph(Pos, r, st + .2f, face, .22f); Act("atk1", st + .2f); break;
                case "sweep": Act("heavy", wind); Fx.Telegraph(Pos, 5, wind, face, 1.1f); st = wind; break;
                case "souls": st = .9f * wind; Fx.Ring(Pos, 2.5f, bd.color, .9f); Act("cast2", st); break;
                case "dash": st = .45f; Fx.Burst(Pos + Vector3.up * 1.4f, 40, bd.color, 4, .6f, -.2f); Act("roll", 0); break;
                case "summon": st = 1; Fx.Burst(Pos + Vector3.up * 2.5f, 40, bd.color, 4, .7f, -.4f); Act("cast2", st); break;
            }
        }
        void ComboTell() { Fx.Telegraph(Pos, ComboReach + .4f, st, face, ComboHalf); Act("atk" + bossStage % 3, st, 1.3f); }
        // struck while guarding: an answer comes back quickly; parry it or roll
        void Riposte()
        {
            guarding = false; bossMode = "riposte"; st = raged ? .26f : .32f; face = Main.Ang(G.hero.Pos - Pos);
            Fx.Telegraph(Pos, ComboReach + .3f, st, face, ComboHalf); Act("atk2", st, 1.4f); Fx.Burst(Pos + Vector3.up * 1.5f, 10, new Color(1, .9f, .5f), 3);
        }
        bool Strikes(float dd, Vector3 to, float reach, float half) => dd < reach && Mathf.Abs(Main.AngDiff(face, Main.Ang(to))) < half;
        bool DuelMove(float dt, Vector3 to, float dd, float want, float sp, float wind)
        {
            var hero = G.hero; bool moving = false; st -= dt;
            switch (bossMode)
            {
                case "combo":
                    if (st > .12f) face += Main.AngDiff(face, want) * Mathf.Min(1, dt * 5);
                    if (dd > 1.4f) { transform.position += Main.Dir(face) * sp * .9f * dt; moving = true; }
                    if (st > 0) break;
                    bool last = bossStage == 2; if (Strikes(dd, to, ComboReach, ComboHalf)) hero.Hurt(d.dmg * (last ? .8f : .5f), Pos);
                    Fx.SlashFx(last ? "sx_double" : "sx_basic", Pos + Vector3.up * 1.1f + Main.Dir(face) * 1.4f, face, last ? 1.4f : 1.1f, bossStage == 1 ? 180 : 0);
                    if (++bossStage >= 3) EndMove(Random.Range(1.1f, 1.9f) * wind); else { st = (bossStage == 2 ? .62f : .38f) * wind; ComboTell(); }   // the third blow waits a beat longer: rolling early gets caught
                    break;
                case "leap":
                    float air = .55f * wind;
                    if (st < air) { float k = 1 - Mathf.Max(0, st) / air; var p = Vector3.Lerp(moveFrom, moveTo, k); transform.position = new Vector3(p.x, transform.position.y, p.z); y = Mathf.Sin(k * Mathf.PI) * LeapH; }
                    if (st > 0) break;
                    y = 0; if (Main.Flat(hero.Pos - moveTo).magnitude < LeapR + .3f) hero.Hurt(d.dmg * 1.1f, moveTo);
                    Fx.Spawn("ground_hit", moveTo, 1.6f, 2); Fx.Ring(moveTo, LeapR + .4f, bd.color, .5f, .25f); Fx.Dust(moveTo, 1.5f, 20); G.shake = Mathf.Max(G.shake, .35f);
                    EndMove(Random.Range(1.4f, 2.1f) * wind); break;
                case "guard":
                    face += Main.AngDiff(face, want) * Mathf.Min(1, dt * 8);
                    transform.position += Main.Dir(face + side * Mathf.PI / 2) * 1.3f * dt;
                    if (st <= 0) { guarding = false; if (dd < 3.5f) StartMove("combo", dd, want, wind); else EndMove(.5f); }
                    break;
                case "riposte":
                    if (st > 0) break;
                    if (Strikes(dd, to, ComboReach + .2f, ComboHalf)) hero.Hurt(d.dmg * .8f, Pos);
                    Fx.SlashFx("sx_shiny", Pos + Vector3.up * 1.1f + Main.Dir(face) * 1.4f, face, 1.3f); EndMove(Random.Range(1f, 1.6f) * wind); break;
                case "sidestep":
                    transform.position += moveDir * 7.5f * Mathf.Clamp01(st / .42f + .2f) * dt;
                    if (st <= 0) { evading = false; if (dd < 4.5f && Random.value < .6f) StartMove("combo", dd, want, wind); else EndMove(.4f); }
                    break;
                case "lunge":
                    if (bossStage == 0) { if (st > .1f) face += Main.AngDiff(face, want) * Mathf.Min(1, dt * 6); if (st <= 0) { bossStage = 1; st = .32f; } break; }
                    transform.position += Main.Dir(face) * LungeSpeed * dt; moving = true; Fx.Burst(Pos + Vector3.up, 1, bd.color, 1, .3f, 0);
                    if (st > 0 && dd > 1.1f) break;
                    if (Strikes(dd, to, 2.4f, .8f)) hero.Hurt(d.dmg * .9f, Pos);
                    Fx.SlashFx("sx_basic", Pos + Vector3.up * 1.1f + Main.Dir(face) * 1.2f, face, 1.2f); EndMove(Random.Range(1.2f, 1.8f) * wind); break;
                default: EndMove(1); break;
            }
            return moving;
        }
    }
}
