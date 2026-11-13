using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaBlade
{
    // The player: movement, 3-hit combo, dodge roll, 12 skills, damage. Rigged heroes play Mixamo clips through a
    // Humanoid avatar; the toy robot hovers and swings a floating sword in code.
    public partial class Hero : MonoBehaviour, ITarget
    {
        public float Rad => .45f; public bool Down => dead; public int Team => 0;
        public void Take(float dmg, Vector3 from) => Hurt(dmg, from);
        public HeroDef def; public SwordDef sword; public string ModelId;   // sword = stats, ModelId = what is shown (weapon skin)
        public float mp = 100, mpMax = 100;   // power: each skill costs some, refills over time
        public static float SkillCost(float cd) => Mathf.Round(Mathf.Clamp(cd * 2.6f, 15, 40));
        public float hp = 1, max = 1, face, inv, buffT, shieldT, dodgeCD, orbAtkT;
        public readonly float[] cds = new float[3];
        public bool dead;
        public enum St { Idle, Atk, Skill, Dodge, Hurt, Dead }
        public St state;
        public string skillId;
        GameObject model, swordGo, bubble;
        Animator anim; Look look; SwordTrail trail;
        Transform socket, pivot, holder, tipT, baseT;
        float buf, chain, hurtT, flashT, spin, kbx, kbz; int lastI = -1;
        (int i, float t, float d, float ht, bool hit) atk; bool inAtk;
        Vector2 dodgeDir; float dodgeT; const float DodgeSpeed = 9f, DodgeTime = .4f;   // distance = speed × time × 3/4
        Coroutine skillCo;
        Main G => Main.G;
        public Vector3 Pos => transform.position;

        // ---------- equip / visuals ----------
        public void Equip()
        {
            var s = G.save;
            def = Data.Heroes.FirstOrDefault(h => h.id == s.hero) ?? Data.Heroes[0];
            sword = Data.Swords.FirstOrDefault(w => w.id == s.sword) ?? Data.Swords[0];
            if (!Shop.Owns(ItemKind.Weapon, sword.id)) sword = Data.Swords[0];
            if (G.mode == Main.Mode.Lobby) offActive = false; else if (offActive && OffHand != null) sword = OffHand;
            bool lobby = G.mode == Main.Mode.Lobby;
            if (lobby && G.pvSword != null) sword = Data.Swords.First(w => w.id == G.pvSword);
            if (model) Destroy(model);
            if (def.rig)
            {
                modelKey = G.ModelKey(def, lobby); var mpf = SkinPacks.Get(modelKey);
                if (!mpf) { if (SkinPacks.IsPack(modelKey)) { var want = modelKey; SkinPacks.Fetch(want, () => { if (this && G.ModelKey(def, G.mode == Main.Mode.Lobby) == want) { Equip(); if (G.mode == Main.Mode.Lobby) PlaceLobby(); } }); } modelKey = def.id; mpf = Resources.Load<GameObject>("Heroes/Hero_" + def.id); }   // a classic skin comes from its bundle: the hero's own model until it is here   // a model skin, or the hero's own
                model = Instantiate(mpf, transform); SkinPacks.Animate(model, def.id); FootWeights.Matte(model); SleeveHang.Attach(model); Grip.Attach(model);
                anim = model.GetComponentInChildren<Animator>();
                animSet = Data.AnimSet(def.id); if (animSet != null) animSet += def.female ? "_f" : "_m";   // each class set comes in a women's and a men's walk, run and stance
                if (anim && animSet != null && Resources.Load<RuntimeAnimatorController>("Anim/" + animSet) is RuntimeAnimatorController oc) anim.runtimeAnimatorController = oc;
                // lobby: a bowed head warps the faces of the auto-rigged heroes, so the head keeps its sculpted angle
                // on the neck (the neck and body still move; see LateUpdate)
                lockBones = lobby && anim ? model.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLower() == "head").ToArray() : null;
                lockRot = lockBones?.Select(t => t.localRotation).ToArray();
                socket = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SwordSocket");
                BuildMounts();
                stanceYaw = 0; footL = anim ? anim.GetBoneTransform(HumanBodyBones.LeftFoot) : null; footR = anim ? anim.GetBoneTransform(HumanBodyBones.RightFoot) : null; groundDrop = 0;
                footRest = footL && footR ? Mathf.Min(footL.position.y, footR.position.y) - transform.position.y : 0;   // still in its rest pose here
                holder = model.transform; pivot = null;
            }
            else
            {
                model = new GameObject("toybot"); model.transform.SetParent(transform, false);
                holder = Instantiate(Catalog.I.Model("toybot"), model.transform).transform;
                pivot = new GameObject("pivot").transform; pivot.SetParent(model.transform, false); pivot.localPosition = new Vector3(0, .62f, 0);
                socket = new GameObject("socket").transform; socket.SetParent(pivot, false); socket.localPosition = new Vector3(.62f, 0, 0);
                anim = null; animSet = null; mFistR = mFistL = mBowL = mCrossR = mShieldL = null;
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            int skinIdx = G.SkinOf(def, lobby);
            look = new Look(model); var sk = Data.IsModelSkin(def, skinIdx) ? def.skins[0] : def.skins[skinIdx]; look.Set(sk.tint, sk.glow);   // a model skin is worn untinted
            EquipSword(lobby); EquipShield(lobby);
            WingRig.Wear(gameObject, anim ? anim.GetBoneTransform(HumanBodyBones.Chest) ?? anim.GetBoneTransform(HumanBodyBones.Spine) : model.transform, G.WingsShown, !anim);   // every hero wears the chosen wings, Aeris too (like the other archers)
            state = St.Idle; Play(G.mode == Main.Mode.Lobby ? Show : "idle", .1f);
        }
        // Mounts for the round-2 weapons, made while the model is still in its rest pose (arms out): frames on the bones
        // with X along the arm or the grip and Y toward the chest's front, so each weapon sits the same on every rig.
        Transform mFistR, mFistL, mBowL, mCrossR, mShieldL; GameObject offGo, shieldGo; public ShieldDef ShieldOn;
        static Quaternion Basis(Vector3 x, Vector3 y) { x.Normalize(); y = (y - x * Vector3.Dot(x, y)).normalized; return Quaternion.LookRotation(Vector3.Cross(x, y), y); }   // model X -> x exactly, model Y -> y (made square to x)
        void BuildMounts()
        {
            mFistR = mFistL = mBowL = mCrossR = mShieldL = null;
            if (!anim || !anim.isHuman) return;
            Transform rh = anim.GetBoneTransform(HumanBodyBones.RightHand), lh = anim.GetBoneTransform(HumanBodyBones.LeftHand),
                rf = anim.GetBoneTransform(HumanBodyBones.RightLowerArm), lf = anim.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            if (!rh || !lh || !rf || !lf) return;
            Vector3 right = (rh.position - lh.position).normalized, F = Vector3.Cross(right, Vector3.up).normalized, aR = (rh.position - rf.position).normalized, aL = (lh.position - lf.position).normalized;
            Transform Mk(string n, Transform bone, Vector3 p, Quaternion r) { var t = new GameObject(n).transform; t.SetParent(bone, false); t.SetPositionAndRotation(p, r); return t; }
            mFistR = Mk("MountFistR", rh, rh.position + aR * .03f, Basis(aR, F));
            mFistL = Mk("MountFistL", lh, lh.position + aL * .03f, Basis(aL, F));
            mCrossR = Mk("MountCrossR", rh, rh.position + aR * .07f, Basis(aR, F));
            mBowL = Mk("MountBowL", lh, lh.position + aL * .07f, Basis(F * .5f + aL * .87f, -aL));   // limbs through the fist, tipped 60° toward the arm so the bow hangs upright at rest; string toward the body
            mShieldL = Mk("MountShieldL", lf, Vector3.Lerp(lf.position, lh.position, .45f) + Vector3.up * .07f, Basis(-aL, F));   // strapped along the forearm: top toward the elbow, face out over the back of the arm
        }
        // which mount a weapon type uses (null = the sword socket)
        Transform MountFor(string type) => type switch { "bow" => mBowL, "crossbow" => mCrossR, "fist" or "claw" => mFistR, _ => null };
        static bool Special(string type) => type is "bow" or "crossbow" or "fist" or "claw";
        void EquipShield(bool lobby)
        {
            if (shieldGo) Destroy(shieldGo); ShieldOn = null;
            string id = lobby && G.pvShield != null ? G.pvShield : Shop.Owns(ItemKind.Shield, G.save.shield) ? G.save.shield : "";
            var sd = Data.Shields.FirstOrDefault(x => x.id == id); if (sd == null || !mShieldL || !Data.ShieldFits(def)) return;
            shieldGo = Instantiate(Catalog.I.Model(sd.id), mShieldL); shieldGo.transform.localPosition = Vector3.zero; shieldGo.transform.localRotation = Quaternion.identity;
            foreach (var r in shieldGo.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (!lobby) ShieldOn = sd;
        }
        void EquipSword(bool lobby)
        {
            if (swordGo) Destroy(swordGo); if (offGo) Destroy(offGo);
            // the weapon's stats stay "sword"; a weapon skin only swaps the model that is shown
            ModelId = lobby && G.pvWeaponSkin != null ? G.pvWeaponSkin : Shop.ModelFor(sword);
            if (def.id is "elf" or "rowan" or "harpy" && Data.Swords.First(w => w.id == ModelId).type is not ("bow" or "crossbow")) ModelId = "w_longbow";   // the archers always show a bow: their models hold none of their own
            var mdef = Data.Swords.First(w => w.id == ModelId);
            // sword: weapon models run along the glTF X axis from the grip (mirrored by glTFast); find which way the blade points
            swordGo = Instantiate(Catalog.I.Model(ModelId), socket ? socket : model.transform);
            swordGo.transform.localPosition = Vector3.zero; swordGo.transform.localRotation = Quaternion.identity;
            if (!def.rig) { swordGo.transform.localScale = Vector3.one * .85f; swordGo.transform.localRotation = Quaternion.Euler(0, 180, 0); }
            if (def.rig && ModelId == "w_daggers") swordGo.transform.localScale = Vector3.one * 1.15f;
            // bows and fists: the weapon's stats still count, its model is not shown; casters do show a weapon of their own kind (a staff, an orb)
            bool fits = System.Array.IndexOf(Data.Affinity(def.cls), mdef.type) >= 0;
            swordGo.SetActive(Special(mdef.type) ? fits && !def.ownWeapon && !(def.ownBow && mdef.type is "bow" or "crossbow") : def.blade || fits);
            var mount = MountFor(mdef.type);
            if (mount && Special(mdef.type))
            {   // bows, the crossbow and fist weapons: on their own mount; the bulk of the model points along the arm / the limbs run through the fist
                swordGo.transform.SetParent(mount, false); swordGo.transform.localPosition = Vector3.zero; swordGo.transform.localScale = Vector3.one;
                swordGo.transform.localRotation = mdef.type != "bow" && BladeSign(swordGo) < 0 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
                if (mdef.type is "fist" or "claw" && mFistL)
                {   // the left hand wears a mirror image
                    offGo = Instantiate(swordGo, mFistL); offGo.transform.localPosition = Vector3.zero; offGo.transform.localRotation = swordGo.transform.localRotation;
                    offGo.transform.localScale = new Vector3(1, 1, -1); offGo.SetActive(swordGo.activeSelf);
                }
            }
            if (def.ownWeapon) foreach (var r in swordGo.GetComponentsInChildren<Renderer>()) r.enabled = false;   // the model holds its own blade; keep the tip for the trail
            float sign = BladeSign(swordGo);
            tipT = new GameObject("tip").transform; tipT.SetParent(swordGo.transform, false); tipT.localPosition = new Vector3(sign * mdef.m * .9f, 0, 0);
            baseT = new GameObject("base").transform; baseT.SetParent(swordGo.transform, false); baseT.localPosition = new Vector3(sign * mdef.m * .25f, 0, 0);
            if (!trail) trail = gameObject.AddComponent<SwordTrail>();
            trail.tipT = tipT; trail.baseT = baseT; trail.color = MasteryTier >= 4 ? Color.Lerp(sword.color, new Color(1, .85f, .4f), .5f) : sword.color;   // Master: a golden edge to the trail
        }
        static float BladeSign(GameObject sw)
        {
            float sum = 0;
            foreach (var mf in sw.GetComponentsInChildren<MeshFilter>()) { var c = sw.transform.InverseTransformPoint(mf.transform.TransformPoint(mf.sharedMesh.bounds.center)); sum += c.x; }
            return sum < 0 ? -1 : 1;
        }

        public void PlaceLobby() { transform.position = new Vector3(0, def.rig ? 0 : .35f, 0); face = 0; transform.rotation = Quaternion.identity; }
        // gear changed in the pause bag: new max health at the same share of health (so swapping pieces can never heal)
        // Every Sky Lanes match starts at level 1 and levels with the XP of that match (G.matchXp); campaign XP is still saved for other modes.
        public int Level => G.SkyMatch ? Data.MatchLevel(G.matchXp) : Data.Level(G.save.HeroXp(def.id));
        float BaseHp => def.hp * (1 + (Level - 1) * Data.LevelHp) * (Perk("vigor") ? Perks.VigorK : 1);
        public bool Perk(string id) => Perks.Has(Perks.Me, id);   // level-up picks (Perks, Sky Lanes)
        float wardT;
        // MOBA fairness (owner, 2026-11-11): in lane battles everyone starts equal: the lobby's weapon stats, mastery, gear, shield block
        // and the story stage do not count (looks only); the hero grows by its match level, jungle buffs and later its level-up picks
        bool Fair => G && G.lane;
        GearBonus Gear => Fair ? default : Items.Bonus(G.save);
        float WDmg => Fair ? 1 : sword.dmg; float WSpd => Fair ? 1 : sword.spd;
        public void RefreshGear() { float share = max > 0 ? hp / max : 1; max = BaseHp + Gear.hp; if (!dead) hp = Mathf.Clamp(max * share, 1, max); }
        public void ResetForGame()
        {
            KitBegin();
            StopWalk(); mp = mpMax; offActive = false; swapCD = 0; G.runLoot.Clear(); G.runXp = 0;
            transform.position = G.lane ? Lane.SpawnOf(0) : new Vector3(0, 0, 6); face = Mathf.PI; max = hp = BaseHp + Gear.hp; potCD = 0; dead = false; state = St.Idle; inv = 1;
            buffT = shieldT = dodgeCD = orbAtkT = mightT = hasteT = 0; for (int i = 0; i < 3; i++) cds[i] = 0; spin = 0; kbx = kbz = 0; buf = 0;
            if (skillCo != null) StopCoroutine(skillCo); skillCo = null; if (bubble) bubble.SetActive(false);
            Play("idle", .05f);
            if (G.soul) { if (!soul) soul = gameObject.AddComponent<SoulController>(); soul.Begin(this); } else if (soul) soul.End();
        }

        public SoulController soul;   // Soulslike mode driver (Main.soul)
        public void PlayAnim(string st, float fade = .1f, float speed = 1, float from = 0) => Play(st, fade, speed, from);
        public void Step(Vector3 d) => MoveBy(d.x, d.z);
        void Play(string st, float fade = .1f, float speed = 1, float from = 0)
        {
            if (!anim) return;
            anim.SetFloat("Speed", speed);
            anim.CrossFadeInFixedTime(st, fade, 0, from);
        }
        string curLoop;
        void Loop(string st, float speed = 1) { if (!anim) return; anim.SetFloat("Speed", speed); if (curLoop == st && (anim.IsInTransition(0) ? anim.GetNextAnimatorStateInfo(0).IsName(st) : anim.GetCurrentAnimatorStateInfo(0).IsName(st))) return; curLoop = st; anim.CrossFadeInFixedTime(st, .15f); }
        // (already blending into this loop: let the blend finish; starting it again every frame kept heroes frozen in idle, gliding)

        // ---------- input ----------
        Vector3? clickGoal; ITarget clickFoe;   // web on a PC: right-click destination / target
        public void StopWalk() { clickGoal = null; clickFoe = null; }   // moved to another place: the old right-click goal belongs to the old place
        Vector2 Move()
        {
            if (G.city && G.ui.CityPanelOpen) return Vector2.zero;
            var j = G.ui.Joy; var kb = Keyboard.current;
            if (G.ui.WebPc)
            {   // arrow keys still steer; otherwise walk to the clicked spot or into range of the clicked enemy
                float ax = kb == null ? 0 : (kb.rightArrowKey.isPressed ? 1 : 0) - (kb.leftArrowKey.isPressed ? 1 : 0), az = kb == null ? 0 : (kb.downArrowKey.isPressed ? 1 : 0) - (kb.upArrowKey.isPressed ? 1 : 0);
                if (ax != 0 || az != 0) { clickGoal = null; clickFoe = null; var v = new Vector2(-ax, az).normalized; return v; }
                if (clickFoe != null && clickFoe.Down) clickFoe = null;
                var goal = clickFoe != null ? clickFoe.Pos : clickGoal; if (goal == null) return Vector2.zero;
                var d = Main.Flat(goal.Value - Pos); float stop = clickFoe != null ? AtkRange + clickFoe.Rad - .2f : .25f;
                if (d.magnitude <= stop) { if (clickFoe == null) clickGoal = null; return Vector2.zero; }
                return new Vector2(d.x, d.z).normalized;
            }
            if (kb != null)
            {
                float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                float z = (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0) - (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0);
                if (x != 0 || z != 0) j = new Vector2(x, z).normalized;
            }
            j.x = -j.x;   // camera faces -Z, so screen-right is world -X
            return Vector2.ClampMagnitude(j, 1);
        }
        float AtkRange => def.ranged ? 10 : 1.9f * sword.len;
        bool ClickAttacking => clickFoe != null && !clickFoe.Down && Main.Flat(clickFoe.Pos - Pos).magnitude <= AtkRange + clickFoe.Rad;
        void ReadKeys()
        {
            var kb = Keyboard.current; var ui = G.ui;
            swapCD -= Time.deltaTime; if (ui.swapP) { ui.swapP = false; SwapWeapon(); }
            potCD -= Time.deltaTime; if (ui.potP) { ui.potP = false; if (!G.legendHard) UsePotion(); }   // no potions on legend difficulty
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) UsePotion();
            if (ui.WebPc)
            {
                if (kb != null)
                {
                    if (kb.qKey.wasPressedThisFrame || !ui.PerksOpen && kb.digit1Key.wasPressedThisFrame) ui.skP[0] = true;
                    if (kb.wKey.wasPressedThisFrame || !ui.PerksOpen && kb.digit2Key.wasPressedThisFrame) ui.skP[1] = true;
                    if (kb.eKey.wasPressedThisFrame || !ui.PerksOpen && kb.digit3Key.wasPressedThisFrame) ui.skP[2] = true;
                    if (kb.rKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame) ui.dodgeP = true;
                    if (kb.aKey.wasPressedThisFrame || kb.jKey.wasPressedThisFrame) ui.atkP = true;
                    if (kb.fKey.wasPressedThisFrame) ui.farmP = true;
                    if (kb.sKey.wasPressedThisFrame) { clickGoal = null; clickFoe = null; }   // S = stop
                    if (kb.xKey.wasPressedThisFrame) ui.swapP = true;
                    if (kb.cKey.wasPressedThisFrame) G.CycleView();
                }
                var m = Mouse.current;
                if (m != null && m.rightButton.isPressed && !ui.PointerOverUi && Camera.main)
                {   // held right button keeps steering, like a MOBA
                    var ray = Camera.main.ScreenPointToRay(m.position.ReadValue());
                    if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t))
                    {
                        var p = ray.GetPoint(t); ITarget best = null; float bd = 1.8f;
                        foreach (var o in G.Opponents(0)) { float dd = Main.Flat(o.Pos - p).magnitude - o.Rad; if (dd < bd) { bd = dd; best = o; } }
                        if (m.rightButton.wasPressedThisFrame) Fx.Ring(best != null ? best.Pos : p, best != null ? 1.2f : .7f, best != null ? new Color(1, .3f, .25f) : new Color(.4f, 1, .6f), .35f, .12f);
                        clickFoe = best; clickGoal = best == null ? p : null; if (best is Foe bf) lockOn = bf;
                    }
                }
                if (m != null && m.leftButton.wasPressedThisFrame && !ui.PointerOverUi) ui.atkP = true;
                return;
            }
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame) ui.atkP = true;
                if (kb.hKey.wasPressedThisFrame) ui.farmP = true;
                if (kb.uKey.wasPressedThisFrame || !ui.PerksOpen && kb.digit1Key.wasPressedThisFrame) ui.skP[0] = true;
                if (kb.iKey.wasPressedThisFrame || !ui.PerksOpen && kb.digit2Key.wasPressedThisFrame) ui.skP[1] = true;
                if (kb.oKey.wasPressedThisFrame || !ui.PerksOpen && kb.digit3Key.wasPressedThisFrame) ui.skP[2] = true;
                if (kb.spaceKey.wasPressedThisFrame || kb.kKey.wasPressedThisFrame || kb.leftShiftKey.wasPressedThisFrame) ui.dodgeP = true;
                if (kb.xKey.wasPressedThisFrame || kb.lKey.wasPressedThisFrame) ui.swapP = true;
            }
            var ms = Mouse.current;
            if (ms != null && ms.leftButton.wasPressedThisFrame && !ui.PointerOverUi) ui.atkP = true;
        }
        bool AtkHeld => G.ui.atkHold || ClickAttacking || (Keyboard.current?.jKey.isPressed ?? false) || (Mouse.current != null && Mouse.current.leftButton.isPressed && !G.ui.PointerOverUi);

        // ---------- helpers ----------
        public float mightT, hasteT;   // Sky Lanes jungle buffs (Jungle): more damage, faster skills
        public float AtkPow => (mightT > 0 ? Jungle.MightK : 1) * (Jungle.Blessed(0) ? Jungle.BossK : 1) * (Perk("might") ? Perks.MightK : 1) * 100 * def.atk * (1 + (Level - 1) * Data.LevelAtk) * WDmg * (1 + (Fair ? 0 : Data.AffinityBonus(def.cls, sword.type) + Data.MasteryDmg[MasteryTier]) + Gear.atk) * (buffT > 0 ? 1.5f : 1) * (orbAtkT > 0 ? 1.25f : 1);
        public int MasteryTier => Data.Tier(G.save.Mastery(sword.id));
        public float CritBonus => (Fair ? 0 : Data.MasteryCrit[MasteryTier]) + Gear.crit;
        // a healing potion from the bag: 35% of health, 3 s apart
        public float potCD;
        public void UsePotion()
        {
            if (dead || potCD > 0 || G.save.potions <= 0 || G.mode != Main.Mode.Game || G.lane) return;   // no potions in lane battles (MOBA): health comes back at the base and on level-ups
            if (G.sky) { if (G.duelPotions <= 0) { G.Number(Pos + Vector3.up * 2.2f, Loc.S("ยาในศึกหมดแล้ว", "No potions left this duel"), "hurt"); return; } G.duelPotions--; G.Number(Pos + Vector3.up * 2.6f, Loc.S($"ยาเหลือ {G.duelPotions}", $"{G.duelPotions} left"), "heal"); }   // a duel allows a few
            G.save.potions--; potCD = 3; Heal(max * .35f); Fx.Burst(Pos + Vector3.up, 26, new Color(1, .35f, .4f), 4); Fx.Spawn("mx_heal_circle", Pos + Vector3.up * .05f, .8f, 1.2f); G.Persist();
        }
        // off-hand: a second owned weapon swapped in mid-fight (X on PC, the swap button on phones); a short cooldown
        public bool offActive; float swapCD;
        public SwordDef OffHand { get { var s = G.save; return Data.Swords.FirstOrDefault(w => w.id == s.sword2 && w.id != s.sword && Shop.Owns(ItemKind.Weapon, w.id)); } }
        public void SwapWeapon()
        {
            if (swapCD > 0 || dead || OffHand == null || G.mode != Main.Mode.Game) return;
            offActive = !offActive; swapCD = .6f;
            var main = Data.Swords.FirstOrDefault(w => w.id == G.save.sword) ?? Data.Swords[0];
            sword = offActive ? OffHand : main; EquipSword(false);
            Fx.Burst(Pos + Vector3.up * 1.2f, 18, sword.color, 3, .5f, 0); G.Number(Pos + Vector3.up * 2.4f, sword.name, "crit");
        }
        void FaceTo(Foe f) { if (f) face = Main.Ang(f.Pos - Pos); }
        // ---------- lane battle targets (owner, 2026-11-11) ----------
        // the attack button goes for enemy heroes first; the second button (farmAtk) for soldiers and towers first; an enemy tapped
        // (or right-clicked) stays the target until it falls or gets well out of reach, with a red ring under it
        public Foe lockOn; public bool farmAtk; float lockRingT;
        const float LockKeep = 6;   // beyond the attack reach, how far a locked enemy may get before the lock lets go
        Foe LanePick(float r)
        {
            if (lockOn && lockOn.alive && Main.Flat(lockOn.Pos - Pos).magnitude <= r + 2) return lockOn;
            Foe hero = null, other = null; float bh = r, bo = r;
            foreach (var f in G.Live) { float d = Main.Flat(f.Pos - Pos).magnitude - f.rad; if (d >= r) continue; if (f.d.kind == "champ") { if (d < bh) { bh = d; hero = f; } } else if (d < bo) { bo = d; other = f; } }
            return farmAtk ? (other ?? hero) : (hero ?? other);
        }
        void LaneTargets(float dt)
        {
            if (lockOn && (!lockOn.alive || Main.Flat(lockOn.Pos - Pos).magnitude > AtkRange + LockKeep)) lockOn = null;
            var tp = UnityEngine.InputSystem.Touchscreen.current;   // a tap on an enemy in the world picks it
            if (tp != null && tp.primaryTouch.press.wasPressedThisFrame && !G.ui.PointerOverUi && Camera.main)
            {
                var ray = Camera.main.ScreenPointToRay(tp.primaryTouch.position.ReadValue());
                if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t))
                {
                    var p = ray.GetPoint(t); Foe best = null; float bd = 2.2f;
                    foreach (var f in G.Live) { if (f.team == 0) continue; float dd = Main.Flat(f.Pos - p).magnitude - f.rad; if (dd < bd) { bd = dd; best = f; } }
                    if (best) { lockOn = best; lockRingT = 0; }
                }
            }
            lockRingT -= dt; if (lockOn && lockRingT <= 0) { lockRingT = .45f; Fx.Ring(lockOn.Pos + Vector3.up * .05f, lockOn.rad + .7f, new Color(1, .25f, .2f), .4f, .1f); }
        }
        void AimAuto(float r = 6)
        {
            var mv = Move();
            if (G.lane && (mv.magnitude <= .3f || lockOn || farmAtk)) { var lt = LanePick(r); if (lt) { FaceTo(lt); return; } }   // lane battles: heroes first, or soldiers and towers with the second button, or the tapped one
            if (mv.magnitude > .3f) { face = Mathf.Atan2(mv.x, mv.y); var e = G.Nearest(Pos, 2.5f); if (e && Mathf.Abs(Main.AngDiff(face, Main.Ang(e.Pos - Pos))) < .9f) FaceTo(e); }
            else FaceTo(G.Nearest(Pos, r));
        }
        void MoveBy(float dx, float dz) { var p = G.Clamp(Pos + new Vector3(dx, 0, dz)); if (G.lane && Lane.Lanes == 2) p = SkyLanes.KeepOut(p, 0); transform.position = p; }   // never into the enemy's spawn pool
        public bool fountainHeal;
        public void Heal(float v, bool quiet = false)
        {
            v = Mathf.Round(Mathf.Min(v, max - hp)); if (v <= 0) return; hp += v; if (G.lane && !fountainHeal) G.Stat(def.name, 0, true).heal += v;   // the end screen's healing: not the fountain's
            if (!quiet || v > 15) G.Number(Pos + Vector3.up * 2, "+" + v, "heal");
        }

        public void Hurt(float dmg, Vector3 from)
        {
            if (inv > 0 || dead || G.mode != Main.Mode.Game) return;
            if (G.soul && soul) { dmg = soul.Intercept(dmg, from); if (dmg <= 0) return; }
            if (Perk("armor")) dmg *= Perks.ArmorK;
            CancelChannel();   // a hit stops a recall or a warp
            if (Perk("ward") && wardT <= 0 && hp < max * Perks.WardAt) { shieldT = Mathf.Max(shieldT, Perks.WardTime); wardT = Perks.WardEvery; Fx.Ring(Pos, 1.8f, new Color(1f, .85f, .4f), .5f, .2f); }   // Last Ward
            float d = CombatRules.Current.ModifyDamage(dmg) * (G.ZoneNight ? OpenWorld.NightFoePower : 1) * (Fair ? 1 : 1 + (G.stage - 1) * .15f) * (ShieldOn != null && !Fair ? 1 - ShieldOn.block : 1) * (1 - Gear.def); if (shieldT > 0) { d *= .2f; Zap(); } if (buffT > 0) d *= .85f;
            if (KitOn) { d = KitIn(d, from); if (d <= 0) return; }
            d = Mathf.Round(d * Random.Range(.9f, 1.1f)); hp -= d; if (G.lane) G.Stat(def.name, 0, true).taken += d; inv = .45f; G.shake = Mathf.Max(G.shake, .22f);
            look.Flash(.5f); flashT = .12f; G.Number(Pos + Vector3.up * 2.1f, "-" + d, "hurt");
            Fx.Spawn("hit_red", Pos + Vector3.up * 1.1f, .8f, 2); Fx.Puff(Pos + Vector3.up * 1.1f);
            if (hp <= 0) { hp = 0; Die(); return; }
            if (G.soul && soul) { if (d > max * .06f && !soul.Blocking) { soul.OnHurt(); Play("hurt", .06f, 1.4f); } return; }
            if (d > max * .06f && state != St.Skill && state != St.Dodge)
            {
                state = St.Hurt; hurtT = .32f; inAtk = false; Play("hurt", .06f, 1.4f);
                var away = Main.Flat(Pos - from).normalized; kbx = away.x * 6; kbz = away.z * 6;
            }
        }
        void Zap()
        {
            var e = G.Nearest(Pos, 4); if (!e) return;
            e.Damage(AtkPow * .5f, Pos, new Main.Hit { col = new Color(.37f, .96f, 1) });
            Fx.Beam(Pos + Vector3.up, e.Pos + Vector3.up * e.h * .5f, new Color(.37f, .96f, 1), .12f, .06f);
        }
        void Die()
        {
            dead = true; state = St.Dead; inAtk = false; if (skillCo != null) StopCoroutine(skillCo); skillCo = null;
            Play("death", .1f, 1.1f, 1.4f); Fx.Dust(Pos, .9f, 12);
            if (G.lane) { G.HeroKilled(Pos, 0, def.name, true); float back = Lane.RespawnTime(Level); G.respawnAt = Time.time + back; Invoke(nameof(Respawn), back); } else Invoke(nameof(Lose), 2.6f);
        }
        void Lose() { if (G.mode != Main.Mode.Game) return; if (G.wilds || G.house || G.sky) G.FallInZone(); else G.Finish(false); }
        // lane mode: back at our base after a short wait
        void Respawn()
        {
            if (!G.lane || G.mode != Main.Mode.Game) return;
            transform.position = Lane.SpawnOf(0); dead = false; hp = max; inv = 2; state = St.Idle; Play("idle", .05f);
            Fx.Ring(Pos, 2, Lane.TeamColor(0) / 2.2f, .6f); G.ui.Banner(Loc.S("กลับสู่สนาม", "Back in the fight"), "");
        }

        // ---------- combo ----------
        void StartAtk(int i)
        {
            if (def.ranged)
            {   // archers draw and loose, casters alternate one- and two-hand casts; the shot leaves at ht
                AimAuto(11); float rd = (i == 2 ? .8f : .55f) / WSpd;
                atk = (i, 0, rd, rd * .5f, false); inAtk = true; state = St.Atk; buf = 0;
                Play(def.cls == "archer" ? (i == 2 ? "bowdraw" : "bowshot") : i == 2 ? "cast2" : "cast1", .06f, 1.25f * WSpd); return;
            }
            AimAuto(5); float ts = 1.3f * WSpd;
            float d, ht, from = 0;
            if (def.rig)
            {
                var c = Catalog.I.Clip(animSet, "atk" + i);
                from = Mathf.Max(0, c.hit - .4f); float end = Mathf.Min(c.length, c.hit + .3f);
                d = (end - from) / ts; ht = (c.hit - from) / ts;
            }
            else { d = new[] { .42f, .42f, .56f }[i] / WSpd; ht = d * .45f; }
            atk = (i, 0, d, ht, false); inAtk = true; state = St.Atk; buf = 0;
            Play("atk" + i, .06f, ts, from);
        }
        void MeleeHit(int i)
        {
            if (def.ranged)
            {
                bool bow = def.cls == "archer"; var col = bow ? new Color(.9f, .97f, 1) : def.cls == "cleric" ? new Color(1, .85f, .45f) : sword.color;
                var o = Pos + Vector3.up * 1.3f + Main.Dir(face) * .6f; float mul = new[] { 1f, 1.1f, 1.6f }[i];
                for (int k = i == 2 ? -1 : 0; k <= (i == 2 ? 1 : 0); k++)
                {
                    float a = face + k * .18f;
                    Shot.Fire(o, Main.Dir(a) * (bow ? 24 : 16), AtkPow * mul * (i == 2 ? .7f : 1), col, bow ? "feather" : "orb", .75f, pierce: i == 2, kb: 1);
                }
                return;
            }
            float reach = (1.9f + (i == 2 ? .6f : 0)) * sword.len + (def.rig ? 0 : .3f);
            int n = G.HitArea(Pos + Main.Dir(face) * .2f, reach, AtkPow * new[] { 1f, 1.15f, 1.8f }[i] * (i == 2 && MasteryTier >= 4 ? Data.MasterFinisher : 1), new Main.Hit { face = face, half = i == 2 ? 1.6f : 1.25f, kb = i == 2 ? 5 : 2, melee = true });
            var at = Pos + Vector3.up * 1.05f + Main.Dir(face) * reach * .45f;
            if (def.blade || def.ownWeapon) Fx.SlashFx(i == 0 ? "sx_basic" : i == 1 || def.id == "warrior" ? "sx_double" : "sx_explode", at,   // Valka: no fiery burst on the third swing (the owner asked)
                 face, (i == 2 ? 1.25f : 1) * Mathf.Max(.8f, sword.len), i == 1 ? 180 : 0);
            else Fx.SlashFx("sx_shiny", at, face, i == 2 ? 1.1f : .8f);
            if (n > 0) { G.hitStop = i == 2 ? .08f : .045f; G.shake = Mathf.Max(G.shake, i == 2 ? .2f : .08f); }
        }

        // ---------- update ----------
        void Update()
        {
            if (G == null || def == null) return;
            if (G.mode == Main.Mode.Lobby) { LobbyIdle(); return; }
            if (G.mode != Main.Mode.Game && G.mode != Main.Mode.Result) return;
            float dt = Time.deltaTime; var ui = G.ui;
            inv -= dt; dodgeCD -= dt; buffT -= dt; orbAtkT -= dt; shieldT -= dt; chain -= dt; mightT -= dt; hasteT -= dt; wardT -= dt; for (int i = 0; i < 3; i++) cds[i] -= dt * (hasteT > 0 ? Jungle.HasteK : 1) * (Perk("haste") ? Perks.HasteK : 1);
            TickChannel(dt);   // recall / tower warp (HeroTravel)
            if (flashT > 0) { flashT -= dt; if (flashT <= 0) look.Flash(0); }
            if (bubble) { bubble.SetActive(shieldT > 0); bubble.transform.position = Pos + Vector3.up * (def.rig ? 1 : .65f); }
            trail.on = def.blade && (state == St.Atk || (state == St.Skill && skillId != "shield" && skillId != "cry" && skillId != "missile" && skillId != "heal" && skillId != "laser"));
            if (dead || G.mode != Main.Mode.Game) { ui.ConsumeInputs(); return; }
            if (G.soul && soul)
            {   // Soulslike mode: the SoulController drives movement, actions and animation
                if (G.sky && (ui.potP || (Keyboard.current?.hKey.wasPressedThisFrame ?? false)))
                {   // potions in a duel: a few, counted
                    ui.potP = false; UsePotion();
                }
                soul.Tick(dt);
                trail.on = def.blade && soul.act is SoulController.Act.Light or SoulController.Act.Heavy or SoulController.Act.Counter or SoulController.Act.Finisher or SoulController.Act.Kick;
                var sp = transform.position; sp.y = Mathf.Lerp(sp.y, River.Ground(sp.x, sp.z), Mathf.Min(1, dt * 12)); transform.position = sp;
                transform.rotation = Quaternion.Euler(0, face * Mathf.Rad2Deg, 0);
                return;
            }
            ReadKeys();
            var mv = Move(); if (stunT > 0) { mv = Vector2.zero; ui.atkP = false; }   // held by a stun
            if (kbx != 0 || kbz != 0) { MoveBy(kbx * dt, kbz * dt); kbx *= Mathf.Exp(-dt * 9); kbz *= Mathf.Exp(-dt * 9); if (Mathf.Abs(kbx) < .05f) kbx = kbz = 0; }
            if (ui.dodgeP) { ui.dodgeP = false; if (dodgeCD <= 0 && state != St.Skill) StartDodge(mv); }
            mp = Mathf.Min(mpMax, mp + 7 * Time.deltaTime);
            if (KitOn) { KitInput(); KitTick(Time.deltaTime); }
            else for (int i = 0; i < 3; i++) if (ui.skP[i])
            {
                ui.skP[i] = false; if (i >= G.SkillsOpen || cds[i] > 0 || state == St.Skill || state == St.Dodge) continue;   // a legend opens the skills one stage at a time
                float cost = SkillCost(Data.Skill(def.skills[i]).cd); if (mp < cost) { G.Number(Pos + Vector3.up * 2.2f, Loc.S("พลังไม่พอ", "Not enough power"), "hurt"); continue; }
                mp -= cost; CastSkill(i);
            }
            if (G.city) { ui.atkP = ui.dodgeP = false; for (int i = 0; i < ui.skP.Length; i++) ui.skP[i] = false; }   // nobody fights in the city
            if (ui.farmP) { buf = .3f; farmAtk = true; ui.farmP = false; ui.atkP = false; } else if (ui.atkP) { buf = .3f; farmAtk = false; ui.atkP = false; }
            if (G.lane) LaneTargets(dt);
            if (AtkHeld) buf = Mathf.Max(buf, .12f); buf -= dt; if (stunT > 0) buf = 0;

            if (state == St.Dodge)
            {
                dodgeT += dt; float s = DodgeSpeed * (1 - dodgeT / DodgeTime * .5f); MoveBy(dodgeDir.x * s * dt, dodgeDir.y * s * dt);   // about 2.7 m (was 4.2: the owner found it too far)
                if (Random.value < .7f) Fx.Burst(Pos + Vector3.up * .8f, 2, new Color(.62f, .9f, 1), 1, .6f, 0);
                if (dodgeT >= DodgeTime) state = St.Idle;
            }
            else if (state == St.Hurt) { hurtT -= dt; if (hurtT <= 0) state = St.Idle; }
            else if (state == St.Atk)
            {
                atk.t += dt;
                if (atk.t < atk.ht) { var f = Main.Dir(face) * ((atk.i == 2 ? 4 : 1.6f) * dt); MoveBy(f.x, f.z); }
                if (!atk.hit && atk.t >= atk.ht) { atk.hit = true; MeleeHit(atk.i); }
                if (atk.hit && buf > 0 && atk.t >= atk.ht + .06f) StartAtk((atk.i + 1) % 3);
                else if (atk.t >= atk.d) { state = St.Idle; chain = .35f; lastI = atk.i; inAtk = false; }
                else if (atk.hit && mv.magnitude > .3f && atk.t > atk.ht + .12f) { state = St.Idle; inAtk = false; }
            }
            if (state == St.Idle)
            {
                if (buf > 0) StartAtk(chain > 0 ? (lastI + 1) % 3 : 0);
                else if (mv.magnitude > .08f)
                {
                    float sp = (def.spd + Gear.spd) * KitSpeed * (Perk("swift") ? Perks.SwiftK : 1) * (buffT > 0 ? 1.2f : 1) * mv.magnitude * (River.InWater(Pos.x, Pos.z) ? .6f : 1); var n = mv.normalized;
                    if (River.InWater(Pos.x, Pos.z) && Random.value < .5f) Fx.Burst(new Vector3(Pos.x, River.SplashY(Pos), Pos.z), 2, new Color(.7f, .9f, 1), 2, .4f, 1.5f, .1f);
                    MoveBy(n.x * sp * dt, n.y * sp * dt);
                    face += Main.AngDiff(face, Mathf.Atan2(mv.x, mv.y)) * Mathf.Min(1, dt * 14);
                    var gc = Catalog.I.Clip(animSet, "run"); Loop("run", gc.speed > .1f ? Mathf.Clamp(sp / (gc.speed * transform.lossyScale.y), .6f, 1.8f) : Mathf.Max(.6f, mv.magnitude) * 1.05f);   // feet stay planted
                }
                else Loop(G.mode == Main.Mode.Lobby ? Show : "idle");
            }
            if (!def.rig) ToyBot(dt, mv);
            else { var gp = transform.position; gp.y = Mathf.Lerp(gp.y, River.Ground(gp.x, gp.z), Mathf.Min(1, dt * 12)); transform.position = gp; }
            transform.rotation = Quaternion.Euler(0, (face + spin) * Mathf.Rad2Deg, 0);
            const bool blink = false;   // a hit shows as a puff of smoke (Fx.Puff in Hurt); the body stays in view
            if (model.activeSelf == blink) { model.SetActive(!blink); var wp = transform.Find("Wings"); if (wp) wp.gameObject.SetActive(!blink); }   // the wings hang off the root, so they blink with the body
        }
        // character select: women and men each have a stance of their own once its clip is in the controller (else the shared "show")
        bool GenderShow => anim && anim.HasState(0, Animator.StringToHash(def.female ? "show_f" : "show_m"));
        string Show => "show";   // each class set has its own lobby stance
        public string animSet; Transform[] lockBones; Quaternion[] lockRot;
        // the run clips leave both feet up to 30 cm in the air and some idles stand a few cm high: while standing or running the
        // body comes down so the lower foot stays at its resting height (skills and jumps keep their own height)
        Transform footL, footR; float footRest, groundDrop; const float MaxDrop = .25f;
        // lobby stances turn the body aside: turn the model back so the hero mostly faces the camera (dragging still turns the whole hero)
        float stanceYaw; const float FaceShare = .75f, MaxStanceYaw = 50;
        void FaceCamera()
        {
            var la = anim.GetBoneTransform(HumanBodyBones.LeftUpperArm); var ra = anim.GetBoneTransform(HumanBodyBones.RightUpperArm); if (!la || !ra) return;
            var fwd = Vector3.Cross(ra.position - la.position, Vector3.up); if (fwd.sqrMagnitude < .001f) return;
            float body = Vector3.SignedAngle(transform.forward, fwd, Vector3.up) - stanceYaw;   // the stance's own turn, without the correction already applied
            float want = Mathf.Clamp(-body * FaceShare, -MaxStanceYaw, MaxStanceYaw);
            stanceYaw = Mathf.LerpAngle(stanceYaw, want, Mathf.Min(1, Time.deltaTime * 2.5f)); model.transform.localRotation = Quaternion.Euler(0, stanceYaw, 0);
        }
        void KeepFeetDown()
        {
            if (!anim || !anim.isHuman || !footL || !footR || !model) return;
            float now = Mathf.Min(footL.position.y, footR.position.y) - transform.position.y - model.transform.localPosition.y;
            float want = state == St.Idle && !soul ? Mathf.Clamp(now - footRest, 0, MaxDrop) : 0;
            groundDrop = Mathf.Lerp(groundDrop, want, Mathf.Min(1, Time.deltaTime * 24));
            var p = model.transform.localPosition; p.y = -groundDrop; model.transform.localPosition = p;
        }
        void LateUpdate()
        {
            KeepFeetDown();
            if (G.mode != Main.Mode.Lobby || !anim || !anim.isHuman) return;
            FaceCamera();
            // every hero breathes and shifts its weight the same way on top of its own idle, so none stands frozen
            float t = Time.time, breath = Mathf.Sin(t * 1.7f), shift = Mathf.Sin(t * .55f);
            void Turn(HumanBodyBones b, Vector3 e) { var tr = anim.GetBoneTransform(b); if (tr) tr.localRotation *= Quaternion.Euler(e); }
            Turn(HumanBodyBones.Spine, new Vector3(breath * 1.2f, shift * 3.5f, shift * 1.8f));
            Turn(HumanBodyBones.Chest, new Vector3(-breath * 2.6f, shift * 2f, 0));
            Turn(HumanBodyBones.LeftUpperArm, new Vector3(0, 0, breath * 2.5f + shift * 2));
            Turn(HumanBodyBones.RightUpperArm, new Vector3(0, 0, -breath * 2.5f + shift * 2));
            if (lockBones != null) for (int i = 0; i < lockBones.Length; i++) if (lockBones[i]) lockBones[i].localRotation = lockRot[i];
        }
        void LobbyIdle() { if (!def.rig) { transform.position = new Vector3(transform.position.x, .35f + Mathf.Sin(Time.time * 3) * .08f, transform.position.z); pivot.localRotation = Quaternion.Euler(0, .35f * Mathf.Rad2Deg, 0); socket.localRotation = Quaternion.Euler(0, 0, 57); } }

        void ToyBot(float dt, Vector2 mv)
        {
            float pv = .35f, tilt = 1f, lean = 0;
            if (state == St.Atk) { float k = atk.t / atk.d; int i = atk.i; pv = i == 2 ? .4f - k * Mathf.PI * 2 : i == 0 ? .6f - k * 3.8f : -3.2f + k * 3.8f; tilt = .15f; lean = .25f; }
            else if (state == St.Skill && skillId != "shield") { pv = -Mathf.PI / 2; tilt = .05f; lean = -.15f; }
            else if (mv.magnitude > .1f) lean = .22f;
            float cur = pivot.localEulerAngles.y * Mathf.Deg2Rad; cur += Main.AngDiff(cur, -pv) * (state == St.Atk ? 1 : Mathf.Min(1, dt * 8));
            pivot.localRotation = Quaternion.Euler(0, cur * Mathf.Rad2Deg, 0);
            socket.localRotation = Quaternion.Slerp(socket.localRotation, Quaternion.Euler(0, 0, tilt * Mathf.Rad2Deg), Mathf.Min(1, dt * 14));
            holder.localRotation = Quaternion.Slerp(holder.localRotation, Quaternion.Euler(lean * Mathf.Rad2Deg, 0, 0), Mathf.Min(1, dt * 8));
            var p = transform.position; p.y = Mathf.Max(River.Ground(p.x, p.z), River.Wet ? River.WaterY : 0) + .35f + Mathf.Sin(Time.time * 3) * .08f; transform.position = p;
        }

        public void TestDodge(Vector2 mv) => StartDodge(mv);   // tests
        public string modelKey;   // which model is worn: the hero id, or a model skin's   // tests
        public const float DodgeCd = 15;   // the roll (R / Space / the arrow button): once every 15 s (owner: it came back too fast)
        void StartDodge(Vector2 mv)
        {
            float a = mv.magnitude > .2f ? Mathf.Atan2(mv.x, mv.y) : face; face = a; dodgeDir = new Vector2(Mathf.Sin(a), Mathf.Cos(a)); dodgeT = 0;
            dodgeCD = DodgeCd; inv = Mathf.Max(inv, DodgeTime + .03f); inAtk = false; state = St.Dodge;
            Play("roll", .04f, 2.2f, .93f); Fx.Burst(Pos + Vector3.up * .5f, 14, new Color(.62f, .9f, 1), 3, .5f, 1, .14f, true);
        }

        // ---------- skills ----------
        void CastSkill(int i)
        {
            skillId = def.skills[i]; cds[i] = Data.Skill(skillId).cd; inAtk = false; AimAuto(9); state = St.Skill;
            skillCo = StartCoroutine(Run(skillId));
        }
        IEnumerator Run(string id)
        {
            // classes rework the shared skills into their own: mages call meteors and freeze, the cleric holds a sanctuary
            // and smites, the monk fights with fists and qi, the forest archer rains and drives arrows
            IEnumerator body = (def.cls, id) switch
            {
                ("mage", "missile") => Meteors(), ("mage", "shield") => FrostWard(),
                ("cleric", "shield") => Sanctuary(), ("cleric", "cry") => Smite(),
                ("monk", "slam") => Palm(), ("monk", "whirl") => Flurry(), ("monk", "cry") => QiBurst(),
                ("archer", "dive") when def.id != "harpy" => ArrowRain(), ("archer", "gale") when def.id != "harpy" => Pierce(),
                _ => Shared(id),
            };
            yield return body;
            spin = 0; state = St.Idle; skillCo = null;
        }
        IEnumerator Shared(string id)
        {
            IEnumerator body = id switch
            {
                "whirl" => Whirl(), "slam" => Leap(9, 5, "leap", 1.6f, 1.5f, 3.8f, 3.2f, new Color(1, .78f, .35f)), "cry" => Cry(),
                "iaido" => Iaido(), "petal" => Petal(), "dance" => Dance(),
                "feather" => Feather(), "dive" => Leap(10, 6, "jump", .95f, .8f, 3.4f, 2.8f, new Color(.62f, .9f, 1), true), "gale" => Gale(),
                "laser" => Laser(), "missile" => Missile(), "heal" => Heal(), _ => Shield(),
            };
            yield return body;
        }

        // ---------- class skills ----------
        Vector3 TargetSpot(float maxD, float ahead) { var e = G.Nearest(Pos, maxD); if (e) { FaceTo(e); return e.Pos; } return Pos + Main.Dir(face) * ahead; }
        // mage: a shower of meteors on the nearest crowd
        IEnumerator Meteors()
        {
            Play("cast2", .08f, 1.2f); var at = TargetSpot(12, 6);
            Fx.Spawn("mx_meteors", at, 1.2f, 3.5f); yield return Wait(.85f);
            for (int i = 0; i < 3; i++) { G.HitArea(at, 4.6f, AtkPow * .9f, new Main.Hit { kb = 3, col = new Color(1, .6f, .3f) }); G.shake = Mathf.Max(G.shake, .25f); Fx.Spawn("mx_spark_y", at + Main.Dir(i * 2.1f) * 1.4f + Vector3.up * .3f, 1, 2); yield return Wait(.28f); }
        }
        // mage: the ward, and a burst of frost that throws back and slows everything close
        IEnumerator FrostWard()
        {
            Fx.Spawn("mx_snow", Pos, 1.1f, 3); G.HitArea(Pos, 4.2f, AtkPow * .9f, new Main.Hit { kb = 5, slow = 2.5f, col = new Color(.6f, .9f, 1) });
            yield return Shield();
        }
        // cleric: a holy circle on the ground; standing in it heals, enemies in it burn
        IEnumerator Sanctuary()
        {
            Play("cast2", .08f, 1.2f); var c = Pos; Fx.Spawn("mx_heal_circle", c + Vector3.up * .05f, 1.6f, 5.2f); yield return Wait(.35f);
            StartCoroutine(SanctuaryZone(c));   // the circle keeps working on its own while she fights on
        }
        IEnumerator SanctuaryZone(Vector3 c)
        {
            for (float t = 0; t < 5 && !dead; t += .5f)
            {
                if (Main.Flat(Pos - c).magnitude < 3.8f) { float amt = max * .035f; hp = Mathf.Min(max, hp + amt); G.Number(Pos + Vector3.up * 2.2f, "+" + Mathf.RoundToInt(amt), "heal"); }
                G.HitArea(c, 3.8f, AtkPow * .4f, new Main.Hit { col = new Color(1, .9f, .5f) });
                yield return Wait(.5f);
            }
        }
        // cleric: pillars of light strike the three nearest enemies
        IEnumerator Smite()
        {
            Play("cast1", .08f, 1.1f); yield return Wait(.25f);
            var tg = G.Live.Where(e => Main.Flat(e.Pos - Pos).magnitude < 11).OrderBy(e => (e.Pos - Pos).sqrMagnitude).Take(3).ToList();
            if (tg.Count == 0) { var p = Pos + Main.Dir(face) * 5; Fx.Spawn("mx_holy_hit", p, 1.6f, 2); G.HitArea(p, 2.4f, AtkPow * 2, new Main.Hit { kb = 3 }); }
            foreach (var e in tg)
            {
                if (!e || !e.alive) continue; Fx.Spawn("mx_holy_hit", e.Pos, 1.6f, 2); Fx.Spawn("mx_spark_y", e.Pos + Vector3.up, 1, 2);
                G.HitArea(e.Pos, 2.4f, AtkPow * 2.1f, new Main.Hit { kb = 3, poise = 30, col = new Color(1, .9f, .5f) }); G.shake = Mathf.Max(G.shake, .18f); yield return Wait(.16f);
            }
        }
        // monk: a driving palm that dashes through the line and hurls them back
        IEnumerator Palm()
        {
            Play("atk0", .04f, 1.6f, Mathf.Max(0, Catalog.I.Clip(animSet, "atk0").hit - .35f)); var f = Main.Dir(face); var s = Pos; var hit = new HashSet<Foe>(); inv = Mathf.Max(inv, .4f);
            var cf = Fx.Spawn("mx_charge", Pos + Vector3.up, 1, 1.5f, Quaternion.Euler(0, face * Mathf.Rad2Deg, 0));
            for (float t = 0; t < .35f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / .25f); var n = s + f * k * 5; MoveBy(n.x - Pos.x, n.z - Pos.z);
                G.HitArea(Pos + f * .8f, 1.8f, AtkPow * 2.2f, new Main.Hit { skip = hit, kb = 8, melee = true, poise = 25 }); yield return null;
            }
            Fx.SlashFx("sx_shiny", Pos + Vector3.up + f, face, 1.4f); if (hit.Count > 0) { G.hitStop = .08f; G.shake = .25f; }
            yield return Wait(.15f);
        }
        // monk: a flurry of punches finished with a kick
        IEnumerator Flurry()
        {
            for (int i = 0; i < 8; i++)
            {
                var e = G.Nearest(Pos, 3.5f); if (e) FaceTo(e); var f = Main.Dir(face);
                Play(i % 2 == 0 ? "atk0" : "atk1", .03f, 2.6f, Mathf.Max(0, Catalog.I.Clip(animSet, i % 2 == 0 ? "atk0" : "atk1").hit - .2f));
                G.HitArea(Pos + f * 1.1f, 1.9f, AtkPow * .5f, new Main.Hit { face = face, half = 1.1f, kb = 1, melee = true });
                Fx.SlashFx("sx_shiny", Pos + Vector3.up * 1.2f + f * 1.1f, face, .7f, Random.Range(-60, 60)); yield return Wait(.12f);
            }
            Play("atk2", .03f, 1.6f, Mathf.Max(0, Catalog.I.Clip(animSet, "atk2").hit - .3f)); yield return Wait(.2f);
            G.HitArea(Pos + Main.Dir(face) * 1.2f, 2.4f, AtkPow * 1.8f, new Main.Hit { face = face, half = 1.3f, kb = 7, melee = true, poise = 30 });
            Fx.Spawn("mx_spark_w", Pos + Vector3.up + Main.Dir(face) * 1.4f, 1, 2); G.hitStop = .07f; G.shake = .2f; yield return Wait(.2f);
        }
        // monk: inner qi bursts out through the ground; he fights harder for a while
        IEnumerator QiBurst()
        {
            Play("cast", .08f, 1.3f, .25f); yield return Wait(.45f);
            Fx.SlashFx("sx_explode", Pos + Vector3.up * .5f, face, 1.5f); Fx.Spawn("mx_spark_y", Pos + Vector3.up * .4f, 1.3f, 2); G.HitArea(Pos, 4.4f, AtkPow * 1.4f, new Main.Hit { kb = 8, poise = 40 });
            buffT = 6; Attach("mx_buff", 6); G.shake = .35f; G.hitStop = .06f; yield return Wait(.25f);
        }
        // archer: a rain of arrows on the nearest crowd
        IEnumerator ArrowRain()
        {
            Play("bowdraw", .06f, 1.4f); var at = TargetSpot(13, 7); yield return Wait(.35f);
            var col = Data.SkillLook(def.id, "dive").c;
            for (int i = 0; i < 22; i++)
            {
                var p = at + new Vector3(Random.Range(-2.6f, 2.6f), 9, Random.Range(-2.6f, 2.6f));
                Shot.Fire(p, new Vector3(Random.Range(-1, 1), -20, Random.Range(-1, 1)), AtkPow * .55f, col, "feather", 1, aoe: .9f);
                yield return Wait(.05f);
            }
        }
        // archer: a drawn, charged arrow that drives through the whole line
        IEnumerator Pierce()
        {
            var e = G.Nearest(Pos, 16); if (e) FaceTo(e);
            Play("bowdraw", .06f, .9f); var glow = Fx.Spawn("mx_spark_w", Pos + Vector3.up * 1.4f + Main.Dir(face) * .6f, .7f, 1); yield return Wait(.5f);
            Play("bowshot", .04f, 1.2f); var f = Main.Dir(face);
            Shot.Fire(Pos + Vector3.up * 1.3f + f * .7f, f * 34, AtkPow * 3.2f, Data.SkillLook(def.id, "gale").c, "feather", 1.1f, pierce: true, kb: 5, r: .8f);
            Fx.SlashFx("sx_mesh", Pos + Vector3.up * 1.3f + f * 3, face, 1.6f); G.shake = .15f; yield return Wait(.3f);
        }
        IEnumerator Wait(float s) { float t = 0; while (t < s) { t += Time.deltaTime; yield return null; } }

        IEnumerator Whirl()
        {
            Play("spin", .06f, 1.04f); var spinFx = Fx.Spawn(sword.burn == "fire" ? "aoe_orange" : sword.slow ? "aoe_blue" : "aoe_green", Pos + Vector3.up * .2f, 1.4f * sword.len, 2.2f); if (spinFx) spinFx.transform.SetParent(transform);
            float t = 0, tick = 0;
            while (t < 1.8f)
            {
                float dt = Time.deltaTime; t += dt; var mv = Move(); MoveBy(mv.x * def.spd * .7f * dt, mv.y * def.spd * .7f * dt); spin += dt * 10;
                tick -= dt; if (tick <= 0) { tick = .22f; G.HitArea(Pos, 2.9f * sword.len, AtkPow * .55f, new Main.Hit { kb = 2.5f, melee = true }); Fx.SlashFx("sx_multi", Pos + Vector3.up, face + spin, 1.2f * sword.len); }
                yield return null;
            }
        }
        IEnumerator Leap(float maxD, float defD, string clip, float ts, float impactClip, float r, float mul, Color col, bool feathers = false)
        {
            var e = G.Nearest(Pos, maxD); var s = Pos; var tgt = Pos + Main.Dir(face) * defD;
            if (e) { FaceTo(e); tgt = Pos + Main.Dir(face) * Mathf.Max(0, Main.Flat(e.Pos - Pos).magnitude - 1); }
            Play(clip, .06f, ts); float impact = impactClip / ts, dur = Catalog.I.Clip(animSet, clip).length / ts; if (clip == "leap") dur = Mathf.Min(dur, impact + .45f);
            float t = 0; bool done = false;
            while (t < dur)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01((t - .15f) / (impact - .2f)); k = k < .5f ? 2 * k * k : 1 - Mathf.Pow(-2 * k + 2, 2) / 2;
                var p = Vector3.Lerp(s, tgt, k); transform.position = new Vector3(p.x, transform.position.y, p.z); if (t < impact) inv = Mathf.Max(inv, .1f);
                if (!done && t >= impact)
                {
                    done = true; G.HitArea(Pos, r, AtkPow * mul, new Main.Hit { kb = 8 });
                    Fx.Ring(Pos, r + .2f, col, .5f, .25f); Fx.Spawn("mx_spark_y", Pos + Vector3.up * .3f, 1.2f, 2);   // Hovl's dust/ground smoke renders black in URP: sparks + the slash explosion instead Fx.Burst(Pos + Vector3.up * .2f, 50, col, 8, .6f, 1.2f, .16f, true);
                    G.shake = .45f; G.hitStop = .08f; Fx.SlashFx("sx_explode", Pos + Vector3.up * .6f, face, 1.7f);
                    if (feathers) for (int i = 0; i < 10; i++) { float a = i / 10f * Mathf.PI * 2; Shot.Fire(Pos + Vector3.up * .8f, Main.Dir(a) * 12, AtkPow * .5f, new Color(.85f, .94f, 1), "feather", .5f, pierce: true); }
                }
                yield return null;
            }
        }
        IEnumerator Cry()
        {
            Play("cast", .08f, 1.3f, .25f); Fx.Burst(Pos + Vector3.up * 1.6f, 20, new Color(1, .3f, .37f), 2, .8f, -.3f);
            yield return Wait(.68f);
            buffT = 7; Fx.Ring(Pos, 4.5f, new Color(1, .3f, .37f), .6f, .3f); Fx.Ring(Pos, 2.4f, new Color(1, .45f, .4f), .8f, .12f); Attach("mx_buff", 7);
            G.HitArea(Pos, 4.2f, AtkPow * .8f, new Main.Hit { kb = 6 }); G.shake = .25f;
            yield return Wait(.27f);
        }
        IEnumerator Iaido()
        {
            Play("atk2", .04f, 1.6f, Mathf.Max(0, Catalog.I.Clip(animSet, "atk2").hit - .4f)); var f = Main.Dir(face); var s = Pos; var hit = new HashSet<Foe>(); inv = Mathf.Max(inv, .6f);
            float t = 0; bool fx = false;
            while (t < .55f)
            {
                t += Time.deltaTime; float k = Mathf.Clamp01((t - .05f) / .22f); k = k < .5f ? 2 * k * k : 1 - Mathf.Pow(-2 * k + 2, 2) / 2;
                var n = s + f * k * 7.5f; MoveBy(n.x - Pos.x, n.z - Pos.z);
                Fx.Burst(Pos + Vector3.up, 3, new Color(1, .37f, .7f), 1.5f, .5f, 0);
                G.HitArea(Pos, 1.6f, AtkPow * 2.4f, new Main.Hit { skip = hit, kb = 3, melee = true });
                if (t > .3f && !fx) { fx = true; Fx.SlashFx("sx_mesh", (s + Pos) / 2 + Vector3.up, face + Mathf.PI / 2, 2.2f); Fx.SlashFx("sx_shiny", Pos + Vector3.up, face, 1.3f); if (hit.Count > 0) { G.hitStop = .1f; G.shake = .25f; } }
                yield return null;
            }
        }
        IEnumerator Petal()
        {
            Play("atk1", .06f, 1.3f, Mathf.Max(0, Catalog.I.Clip(animSet, "atk1").hit - .4f)); int n = 0; float t = 0;
            while (t < .7f)
            {
                t += Time.deltaTime;
                for (int i = 0; i < 3; i++) { float a = face + Random.Range(-.75f, .75f); Fx.Burst(Pos + Vector3.up + Main.Dir(a) * Random.Range(.5f, 5.5f), 1, Random.value < .5f ? new Color(1, .54f, .78f) : new Color(1, .82f, .9f), 2, .5f, .3f); }
                if (n < 3 && t > .12f + n * .18f) { n++; G.HitArea(Pos, 6.8f, AtkPow * .95f, new Main.Hit { face = face, half = .75f, kb = 1.5f, col = new Color(1, .54f, .78f) }); Fx.SlashFx("sx_multi", Pos + Vector3.up + Main.Dir(face) * 2.5f, face + (n - 2) * .4f, 1.6f); }
                yield return null;
            }
        }
        IEnumerator Dance()
        {
            var tg = G.Live.OrderBy(e => (e.Pos - Pos).sqrMagnitude).Where(e => Main.Flat(e.Pos - Pos).magnitude < 10).Take(6).ToList();
            inv = Mathf.Max(inv, tg.Count * .2f + .3f);
            for (int i = 0; i < Mathf.Max(1, tg.Count); i++)
            {
                if (i < tg.Count && tg[i] && tg[i].alive)
                {
                    var e = tg[i]; float a = Main.Ang(Pos - e.Pos) + Random.Range(-1f, 1f);
                    Fx.Burst(Pos + Vector3.up, 12, new Color(1, .37f, .54f), 2, .5f, 0);
                    var p = e.Pos + Main.Dir(a) * 1.2f; MoveBy(p.x - Pos.x, p.z - Pos.z); FaceTo(e);
                }
                var c = Catalog.I.Clip(animSet, "atk" + (i % 3)); Play("atk" + (i % 3), .03f, 2.6f, Mathf.Max(0, c.hit - .25f));
                G.HitArea(Pos, 2.4f, AtkPow * 1.7f, new Main.Hit { face = face, half = 1.2f, kb = 2, melee = true });
                G.hitStop = .04f; Fx.SlashFx(i % 2 == 0 ? "sx_double" : "sx_shiny", Pos + Vector3.up + Main.Dir(face) * .8f, face, 1.1f, i * 50);
                yield return Wait(.2f);
            }
        }
        IEnumerator Feather()
        {
            Play("atk0", .06f, 1.8f, Mathf.Max(0, Catalog.I.Clip(animSet, "atk0").hit - .3f)); yield return Wait(.12f);
            for (int i = -3; i <= 3; i++) { float a = face + i * .16f; Shot.Fire(Pos + Vector3.up * 1.2f + Main.Dir(a) * .6f, Main.Dir(a) * 17, AtkPow * 1.05f, new Color(.85f, .94f, 1), "feather", .8f, pierce: true, kb: 1.2f); }
            yield return Wait(.3f);
        }
        IEnumerator Gale()
        {
            Play("guard", .08f, 1, .3f);
            foreach (var e in G.Live) if (!e.isBoss && e.d.kind != "tower" && Main.Flat(e.Pos - Pos).magnitude < 8.5f) e.pull = .6f;
            Fx.Cylinder(Pos, 2.6f, .4f, 3.2f, new Color(.5f, .85f, 1, .45f), .9f, (go, k) => { go.transform.Rotate(0, 900 * Time.deltaTime, 0); float s = 1.4f - k * .6f; go.transform.localScale = new Vector3(s, 1, s); });
            Fx.Ring(Pos, 3.2f, new Color(.5f, .85f, 1), .6f, .1f);
            float t = 0; bool done = false;
            while (t < .9f)
            {
                t += Time.deltaTime; Fx.Burst(Pos + new Vector3(Random.Range(-3f, 3f), Random.Range(.2f, 2.5f), Random.Range(-3f, 3f)), 2, new Color(.5f, .85f, 1), 3, .5f, 0);
                if (!done && t > .65f) { done = true; Play("cast", .06f, 1.3f, 1.0f); G.HitArea(Pos, 3.4f, AtkPow * 2.2f, new Main.Hit { kb = 7 }); Fx.Ring(Pos, 3.6f, new Color(.5f, .85f, 1), .4f); G.shake = .25f; G.hitStop = .05f; }
                yield return null;
            }
        }
        IEnumerator Laser()
        {
            if (def.rig) Play("cast1", .08f, .7f);
            var go = new GameObject("laser"); var lr = go.AddComponent<LineRenderer>(); lr.positionCount = 2; lr.sharedMaterial = new Material(Catalog.I.glowMat); lr.sharedMaterial.SetColor("_Color", new Color(2.6f, .5f, .5f));
            float t = 0, tick = 0;
            while (t < 1.5f)
            {
                float dt = Time.deltaTime; t += dt; var mv = Move(); if (mv.magnitude > .2f) face += Mathf.Clamp(Main.AngDiff(face, Mathf.Atan2(mv.x, mv.y)), -2.2f * dt, 2.2f * dt);
                var f = Main.Dir(face); var o = Pos + f * .55f; o.y = def.rig ? 1.3f : .75f; lr.SetPosition(0, o); lr.SetPosition(1, o + f * 10); lr.widthMultiplier = .3f + Mathf.Sin(t * 40) * .05f;
                tick -= dt;
                if (tick <= 0)
                {
                    tick = .11f;
                    foreach (var e in G.Live.ToList()) { var r = Main.Flat(e.Pos - o); float along = Vector3.Dot(r, f), perp = Mathf.Abs(r.x * f.z - r.z * f.x); if (along > 0 && along < 10 && perp < .6f + e.rad) e.Damage(AtkPow * .42f, Pos, new Main.Hit { col = new Color(1, .23f, .23f), kb = .6f }); }
                }
                Fx.Burst(o, 1, new Color(1, .23f, .23f), 2, .4f, 0);
                yield return null;
            }
            Destroy(lr.sharedMaterial); Destroy(go);
        }
        IEnumerator Missile()
        {
            if (def.rig) Play("cast2", .08f, 1.3f);
            for (int n = 0; n < 8; n++)
            {
                float a = face + (n % 2 == 1 ? 1 : -1) * (1.2f + n * .08f);
                Shot.Fire(Pos + Vector3.up * 1.1f, Main.Dir(a) * 9 + Vector3.up * 3, AtkPow * 1.1f, new Color(1, .78f, .35f), "missile", 2.2f, homing: 5, aoe: 1.4f, grav: 3, trail: true);
                yield return Wait(.06f);
            }
            yield return Wait(.07f);
        }
        IEnumerator Shield()
        {
            shieldT = 5; if (def.rig) Play("cast1", .08f, 1.4f);
            if (def.rig && Attach(def.cls == "cleric" ? "mx_shield_gold" : "mx_shield", 5))
            {   // heroes get the magic shield effect; the glass bubble stays for the robot
                Fx.Ring(Pos, 2.5f, new Color(.37f, .96f, 1), .5f); yield return Wait(.2f); yield break;
            }
            if (!bubble)
            {
                bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(bubble.GetComponent<Collider>()); bubble.transform.localScale = Vector3.one * 2.2f;
                var m = new Material(Catalog.I.glowMat); m.SetFloat("_UseVC", 0); m.SetColor("_Color", new Color(.1f, .5f, .6f)); bubble.GetComponent<Renderer>().sharedMaterial = m;
            }
            bubble.SetActive(true); Fx.Ring(Pos, 2.5f, new Color(.37f, .96f, 1), .5f); Fx.Spawn("electric", Pos + Vector3.up * .6f, 1, 2);
            yield return Wait(.2f);
        }

        IEnumerator Heal()
        {
            Play("cast2", .08f, 1.2f); Fx.Burst(Pos + Vector3.up * 1.5f, 16, new Color(1, .9f, .55f), 1.5f, .9f, -.3f);
            yield return Wait(.5f);
            float amt = max * .3f; hp = Mathf.Min(max, hp + amt); G.Number(Pos + Vector3.up * 2.2f, "+" + Mathf.RoundToInt(amt), "heal");
            Fx.Ring(Pos, 3.5f, new Color(1, .85f, .4f), .7f, .2f); Fx.Burst(Pos + Vector3.up * .3f, 30, new Color(1, .9f, .5f), 2.5f, 1, -.6f, .1f, true);
            Fx.Spawn("mx_heal_circle", Pos + Vector3.up * .05f, 1.1f, 3); Attach("mx_healing", 2.5f);
            yield return Wait(.3f);
        }

        // effect that follows the hero for a while (auras, shields)
        bool Attach(string fx, float life) { var go = Fx.Spawn(fx, Pos, 1, life); if (!go) return false; go.transform.SetParent(transform, true); return true; }

        void OnDestroy() { if (bubble) Destroy(bubble); }
    }
}
