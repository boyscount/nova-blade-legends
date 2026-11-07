using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaBlade
{
    // PvE and PvP balance rules, kept apart instead of scattering "if (pvp)" checks (design doc §12).
    public interface ICombatRules { float ModifyDamage(float d); float ModifyStun(float s); float ModifyHealing(float h); }
    public sealed class PveRules : ICombatRules { public float ModifyDamage(float d) => d; public float ModifyStun(float s) => s; public float ModifyHealing(float h) => h; }
    public sealed class PvpRules : ICombatRules { public float ModifyDamage(float d) => d * .8f; public float ModifyStun(float s) => s * .6f; public float ModifyHealing(float h) => h * .7f; }
    public static class CombatRules
    {
        static readonly ICombatRules pve = new PveRules(), pvp = new PvpRules();
        public static ICombatRules Current => Main.G && Main.G.lane ? pvp : pve;   // the lane battle stands in for PvP until online play exists
    }

    // Soulslike mode for the player (Phase 1 of the Hybrid World plan): third-person orbit camera with lock-on,
    // light/heavy attacks, dodge with i-frames and perfect dodge, hold-to-block with a parry window, stamina,
    // enemy poise breaks and finishers. Hero keeps HP, animator, sword and effects; this component drives them.
    public class SoulController : MonoBehaviour
    {
        public enum Act { None, Light, Heavy, Kick, Dodge, Block, Counter, Finisher, Hurt, GuardBreak }
        public Act act;
        public float stamina, staminaMax = 100, yaw, pitch = 16;
        public Foe lockTarget;
        public bool Blocking => act == Act.Block;
        public float CounterT => counterT;
        Hero h; Main G => Main.G;
        float t, dur, hitT, regenWait, blockT, counterT, comboT, slowT; bool hitDone, dodgeFree; int combo; Vector3 dodgeDir; string loop;

        public void Begin(Hero hero)
        {
            h = hero; stamina = staminaMax; act = Act.None; lockTarget = null; yaw = Mathf.PI; pitch = 16; combo = 0; loop = null;
            if (!Main.IsPhone) Cursor.lockState = CursorLockMode.Locked;
        }
        public void End() { lockTarget = null; Cursor.lockState = CursorLockMode.None; if (slowT > 0) Time.timeScale = 1; slowT = 0; }

        // ---------- input ----------
        Vector2 MoveInput()   // x = right, y = forward (screen space)
        {
            var j = G.ui.Joy; var v = new Vector2(j.x, -j.y); var kb = Keyboard.current;
            if (kb != null)
            {
                float x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0), z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                if (x != 0 || z != 0) v = new Vector2(x, z).normalized;
            }
            return Vector2.ClampMagnitude(v, 1);
        }
        Vector3 WorldMove(Vector2 m) => Main.Dir(yaw) * m.y + Main.Dir(yaw + Mathf.PI / 2) * m.x;
        bool LightPressed { get { var ui = G.ui; bool k = ui.atkP; ui.atkP = false; return k || (Mouse.current?.leftButton.wasPressedThisFrame ?? false) || (Keyboard.current?.jKey.wasPressedThisFrame ?? false); } }
        bool HeavyPressed { get { var ui = G.ui; bool k = ui.heavyP; ui.heavyP = false; return k || (Mouse.current?.rightButton.wasPressedThisFrame ?? false) || (Keyboard.current?.kKey.wasPressedThisFrame ?? false) || (Keyboard.current?.uKey.wasPressedThisFrame ?? false); } }   // U, I, O: the keys shown on the PC bar
        bool DodgePressed { get { var ui = G.ui; bool k = ui.dodgeP; ui.dodgeP = false; return k || (Keyboard.current?.spaceKey.wasPressedThisFrame ?? false); } }
        bool LockPressed { get { var ui = G.ui; bool k = ui.lockP; ui.lockP = false; return k || (Keyboard.current?.tabKey.wasPressedThisFrame ?? false) || (Mouse.current?.middleButton.wasPressedThisFrame ?? false) || (Keyboard.current?.oKey.wasPressedThisFrame ?? false); } }
        bool BlockHeld => G.ui.blockHold || (Keyboard.current != null && (Keyboard.current.qKey.isPressed || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.iKey.isPressed));

        bool Spend(float cost) { if (stamina <= 0) return false; stamina = Mathf.Max(0, stamina - cost); regenWait = .7f; return true; }

        // ---------- per frame (called by Hero.Update in Soul mode) ----------
        public void Tick(float dt)
        {
            if (slowT > 0) { slowT -= Time.unscaledDeltaTime; if (slowT <= 0) Time.timeScale = 1; }
            counterT -= dt; comboT -= dt; regenWait -= dt;
            if (regenWait <= 0) stamina = Mathf.Min(staminaMax, stamina + (Blocking ? 12 : 32) * dt);
            Look();
            if (LockPressed) ToggleLock();
            if (lockTarget && (!lockTarget.alive || Main.Flat(lockTarget.Pos - h.Pos).magnitude > 24)) lockTarget = null;
            var mv = MoveInput(); var wm = WorldMove(mv);
            bool free = act == Act.None || act == Act.Block;
            // start actions
            if (DodgePressed && act != Act.Dodge && act != Act.Hurt && act != Act.GuardBreak && Spend(20)) StartDodge(wm);
            else if (free || CanCancel())
            {
                bool light = LightPressed, heavy = HeavyPressed;
                if (heavy && Blocking && Spend(18)) Start(Act.Kick, "kick", 1.3f, .5f);
                else if (light && lockTarget && lockTarget.Broken && Main.Flat(lockTarget.Pos - h.Pos).magnitude < 3) Start(Act.Finisher, "heavy", 1.25f, .3f);
                else if (light && counterT > 0 && Spend(8)) { counterT = 0; Start(Act.Counter, "counter", 1.5f, .3f); }
                else if (light && Spend(12)) { combo = comboT > 0 ? (combo + 1) % 3 : 0; Start(Act.Light, "atk" + combo, 1.35f * h.sword.spd, .4f); }
                else if (heavy && Spend(26)) { bool running = !lockTarget && mv.magnitude > .7f; Start(Act.Heavy, running ? "jumpatk" : "heavy", running ? 1.2f : 1.15f, running ? .6f : .45f); }
            }
            if (act == Act.None && BlockHeld && stamina > 0) { act = Act.Block; blockT = 0; h.PlayAnim("blockidle", .1f); loop = "blockidle"; }
            if (act == Act.Block && (!BlockHeld || stamina <= 0)) { act = Act.None; loop = null; }
            // facing: locked = face the target; free = face the move direction
            if (lockTarget) h.face += Main.AngDiff(h.face, Main.Ang(lockTarget.Pos - h.Pos)) * Mathf.Min(1, dt * 12);
            else if (wm.sqrMagnitude > .01f && (free || act == Act.Light && t < hitT * .5f)) h.face += Main.AngDiff(h.face, Main.Ang(wm)) * Mathf.Min(1, dt * 12);
            // act
            switch (act)
            {
                case Act.None:
                case Act.Block:
                    blockT += dt;
                    float sp = Blocking ? 1.6f : lockTarget ? 2.8f : h.def.spd * (River.InWater(h.Pos.x, h.Pos.z) ? .6f : 1);
                    if (wm.sqrMagnitude > .01f) h.Step(wm * sp * mv.magnitude * dt);
                    if (!Blocking) Locomotion(mv);
                    break;
                case Act.Dodge:
                    t += dt; float k = t / dur; h.Step(dodgeDir * (dodgeFree ? 9 : 6) * (1 - k * .6f) * dt);
                    h.inv = Mathf.Max(h.inv, t < .38f ? .05f : 0);
                    if (t >= dur) { act = Act.None; loop = null; }
                    break;
                case Act.Hurt: case Act.GuardBreak:
                    t += dt; if (t >= dur) { act = Act.None; loop = null; }
                    break;
                default: Attack(dt); break;
            }
        }

        bool CanCancel() => (act == Act.Light || act == Act.Counter) && hitDone && t > hitT + .08f;
        void Locomotion(Vector2 mv)
        {
            string want;
            if (mv.magnitude < .1f) want = "idle";
            else if (!lockTarget) want = "run";
            else
            {   // strafe animations relative to the target
                var local = Quaternion.Euler(0, -h.face * Mathf.Rad2Deg, 0) * WorldMove(mv);
                want = Mathf.Abs(local.x) > Mathf.Abs(local.z) ? (local.x > 0 ? "strafeR" : "strafeL") : "walk";
            }
            if (want != loop) { loop = want; h.PlayAnim(want, .15f, want == "run" ? 1.05f : 1); }
        }

        void Start(Act a, string clip, float speed, float lead)
        {
            act = a; t = 0; hitDone = false; loop = null; comboT = 1f;
            var c = Catalog.I.Clip(h.animSet, clip); float from = Mathf.Max(0, c.hit - lead), end = Mathf.Min(c.length, c.hit + (a == Act.Light ? .3f : .55f));
            dur = (end - from) / speed; hitT = (c.hit - from) / speed;
            if (lockTarget) h.face = Main.Ang(lockTarget.Pos - h.Pos);
            h.PlayAnim(clip, .06f, speed, from);
            if (a == Act.Finisher) { G.hitStop = .1f; G.ui.Banner(Loc.S("ปิดฉาก!", "Finisher!"), ""); }
        }

        void Attack(float dt)
        {
            t += dt;
            if (t < hitT) h.Step(Main.Dir(h.face) * (act == Act.Heavy ? 2.6f : act == Act.Kick ? 1.5f : 1.4f) * dt);
            if (!hitDone && t >= hitT)
            {
                hitDone = true;
                float pow = h.AtkPow, reach = 2.3f * h.sword.len;
                var (mul, poise, half, kb) = act switch
                {
                    Act.Light => (new[] { 1f, 1.1f, 1.45f }[combo], new[] { 12f, 14f, 22f }[combo], 1.2f, 1.5f),
                    Act.Heavy => (2.3f, 45f, 1.4f, 4f),
                    Act.Kick => (.5f, 70f, .9f, 6f),
                    Act.Counter => (2.6f, 60f, 1.3f, 3f),
                    _ => (4.5f, 0f, 1.6f, 2f),   // finisher
                };
                int n = G.HitArea(h.Pos + Main.Dir(h.face) * .3f, reach + (act == Act.Heavy ? .5f : 0), pow * mul, new Main.Hit { face = h.face, half = half, kb = kb, melee = true, poise = poise });
                Fx.SlashFx(act == Act.Light ? "sx_basic" : act == Act.Heavy ? "sx_double" : "sx_explode", h.Pos + Vector3.up * 1.05f + Main.Dir(h.face) * reach * .45f, h.face, act == Act.Light ? 1 : 1.3f);
                if (act != Act.Light && act != Act.Heavy) Fx.SlashFx("sx_shiny", h.Pos + Vector3.up + Main.Dir(h.face) * reach * .6f, h.face, 1.2f);
                if (act == Act.Finisher) { var fp = h.Pos + Main.Dir(h.face) * 1.5f; Fx.Burst(fp + Vector3.up, 40, new Color(1, .7f, .35f), 7, .5f, 1.4f, .09f); Fx.Dust(fp, 1.1f, 14); if (lockTarget) lockTarget.EndBreak(); G.shake = .5f; G.hitStop = .2f; }
                else if (n > 0) { G.hitStop = act == Act.Light ? .05f : .1f; G.shake = Mathf.Max(G.shake, act == Act.Light ? .08f : .22f); }
            }
            if (t >= dur) { act = Act.None; loop = null; }
        }

        void StartDodge(Vector3 wm)
        {
            dodgeFree = wm.sqrMagnitude > .04f; t = 0; loop = null;
            if (dodgeFree) { dodgeDir = wm.normalized; h.face = Main.Ang(wm); act = Act.Dodge; dur = .55f; h.PlayAnim("roll", .04f, 2.2f, .93f); }
            else { dodgeDir = -Main.Dir(h.face); act = Act.Dodge; dur = .5f; var c = Catalog.I.Clip(h.animSet, "backstep"); h.PlayAnim("backstep", .04f, 1.4f, Mathf.Max(0, c.hit - .3f)); }
            Fx.Burst(h.Pos + Vector3.up * .5f, 12, new Color(.62f, .9f, 1), 3, .5f, 1, .14f, true);
        }

        // ---------- incoming hits: dodge i-frames, perfect dodge, parry, block, guard break ----------
        // returns the damage that still goes through (0 = fully avoided)
        public float Intercept(float dmg, Vector3 from)
        {
            if (act == Act.Dodge && t < .38f)
            {
                if (t < .2f) { slowT = .45f; Time.timeScale = .35f; stamina = Mathf.Min(staminaMax, stamina + 15); G.Number(h.Pos + Vector3.up * 2.2f, Loc.S("หลบสมบูรณ์!", "Perfect dodge!"), "heal"); }
                return 0;
            }
            bool facing = Mathf.Abs(Main.AngDiff(h.face, Main.Ang(from - h.Pos))) < 1.75f;
            if (act == Act.Block && facing)
            {
                var attacker = G.foes.Where(f => f && f.alive && f.team == 1).OrderBy(f => (f.Pos - from).sqrMagnitude).FirstOrDefault();
                if (blockT < .22f)
                {   // parry: no damage, the attacker loses balance, counter window opens
                    if (attacker) attacker.Parried();
                    counterT = 1.2f; stamina = Mathf.Min(staminaMax, stamina + 10); G.hitStop = .14f; G.shake = .2f;
                    Fx.Burst(h.Pos + Vector3.up * 1.3f + Main.Dir(h.face) * .6f, 30, new Color(1, .9f, .5f), 7); Fx.Spawn("hit_yellow", h.Pos + Vector3.up * 1.3f + Main.Dir(h.face) * .6f, 1, 2);
                    G.Number(h.Pos + Vector3.up * 2.2f, Loc.S("ปัดป้อง!", "Parry!"), "crit"); h.PlayAnim("blockhit", .04f, 1.4f); loop = null;
                    return 0;
                }
                stamina -= dmg * .35f; regenWait = .9f; h.PlayAnim("blockhit", .04f, 1.3f); loop = null; Fx.Burst(h.Pos + Vector3.up * 1.2f + Main.Dir(h.face) * .5f, 10, Color.white, 4);
                if (stamina <= 0) { stamina = 0; act = Act.GuardBreak; t = 0; dur = 1.1f; h.PlayAnim("hurt", .05f, 1); G.Number(h.Pos + Vector3.up * 2.2f, Loc.S("การ์ดแตก!", "Guard broken!"), "hurt"); return dmg * .5f; }
                return dmg * .15f;
            }
            return dmg;
        }
        public void OnHurt() { if (act == Act.Block) return; act = Act.Hurt; t = 0; dur = .45f; loop = null; }

        // ---------- lock-on ----------
        void ToggleLock()
        {
            if (lockTarget) { lockTarget = null; return; }
            var fwd = Main.Dir(yaw);
            lockTarget = G.Live.Where(f => Main.Flat(f.Pos - h.Pos).magnitude < 20)
                .OrderBy(f => (f.isBoss ? -5 : 0) + Main.Flat(f.Pos - h.Pos).magnitude * (1.5f - Vector3.Dot(fwd, Main.Flat(f.Pos - h.Pos).normalized))).FirstOrDefault();
        }

        // ---------- camera ----------
        void Look()
        {
            var d = G.ui.TakeLook();
            if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked) d += Mouse.current.delta.ReadValue() * .12f;
            yaw += d.x * .012f; pitch = Mathf.Clamp(pitch - d.y * .6f, 5, 45);
        }
        public void CameraUpdate(Camera cam, float dt, ref Vector3 camPos, ref Vector3 camLook)
        {
            Vector3 focus = h.Pos + Vector3.up * 1.5f;
            if (lockTarget)
            {   // keep both in view: swing the orbit behind the hero toward the target
                var to = Main.Flat(lockTarget.Pos - h.Pos); yaw += Main.AngDiff(yaw, Main.Ang(to)) * Mathf.Min(1, dt * 5);
                focus = Vector3.Lerp(focus, lockTarget.Pos + Vector3.up * lockTarget.h * .5f, .3f);
            }
            float dist = 5.4f + (lockTarget && lockTarget.isBoss ? 1.5f : 0);
            var off = -Main.Dir(yaw) * dist * Mathf.Cos(pitch * Mathf.Deg2Rad) + Vector3.up * dist * Mathf.Sin(pitch * Mathf.Deg2Rad);
            camPos = Vector3.Lerp(camPos, h.Pos + Vector3.up * 1.4f + off, 1 - Mathf.Exp(-dt * 10));
            camLook = Vector3.Lerp(camLook, focus, 1 - Mathf.Exp(-dt * 10));
        }
    }
}
