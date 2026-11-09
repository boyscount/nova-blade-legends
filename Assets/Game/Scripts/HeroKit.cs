using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaBlade
{
    // MOBA kits for the lane battles (MOBA-SKILLS.md): every hero has a passive and three skills of its own, each with a shape that is
    // shown before it is let go. Numbers are multiples of the hero's attack power (P). The campaign keeps the older class skills.
    public enum KShape { Self, Point, Line, Cone, Dash, Leap, Blink, Target, Ally }
    public class KSkill
    {
        public string n; public KShape shape; public float range, size, cd, dmg, hits = 1, dur, delay, stun, slow, knock, pullIn;
        public float shieldSelf, shieldAll, heal, atkBuff, buffT, invuln, invis, dot, dotT, cut, cutT, mark, tether, counter, trap, back;
        public bool allies; public string anim = "atk2";
    }
    public class Kit { public string pid, pn, pd; public KSkill[] s; }

    public static class Kits
    {
        static KSkill K(string n, KShape sh, float range, float size, float cd, float dmg, string anim = "atk2") => new() { n = n, shape = sh, range = range, size = size, cd = cd, dmg = dmg, anim = anim };
        static readonly Dictionary<string, Kit> all = new()
        {
            ["warrior"] = new Kit { pid = "rage", pn = Loc.S("เลือดคลั่ง", "Blood Rage"), pd = Loc.S("เลือดต่ำกว่า 50%: ATK +20% และตีปกติดูดเลือด 8%", "Under 50% health: +20% attack, basic hits heal 8%"), s = new[] {
                With(K(Loc.S("พายุดาบ", "Blade Storm"), KShape.Self, 0, 3.5f, 7, .8f, "spin"), k => { k.hits = 3; k.dur = .9f; }),
                With(K(Loc.S("ทุบปฐพี", "Earth Slam"), KShape.Leap, 8, 3, 10, 1.8f, "leap"), k => k.stun = .6f),
                With(K(Loc.S("คำรามศึก", "War Cry"), KShape.Self, 0, 6, 14, .6f, "cast"), k => { k.slow = 2; k.atkBuff = .2f; k.buffT = 5; k.allies = true; }) } },
            ["spartan"] = new Kit { pid = "front", pn = Loc.S("โล่สปาร์ตัน", "Spartan Shield"), pd = Loc.S("ดาเมจจากด้านหน้าลดลง 20%", "20% less damage from the front"), s = new[] {
                With(K(Loc.S("กระแทกโล่", "Shield Bash"), KShape.Dash, 5, 1.4f, 9, 1.2f, "atk1"), k => k.stun = .8f),
                With(K(Loc.S("พุ่งหอก", "Spear Throw"), KShape.Line, 10, 1.2f, 7, 1.6f, "atk0"), k => k.slow = 1.5f),
                With(K(Loc.S("แถวโล่", "Phalanx"), KShape.Self, 0, 5, 16, 0, "guard"), k => { k.shieldSelf = .15f; k.shieldAll = .08f; k.allies = true; }) } },
            ["samurai"] = new Kit { pid = "petal", pn = Loc.S("กลีบประทับ", "Petal Mark"), pd = Loc.S("ตีโดนฮีโร่ทิ้งกลีบ ครบ 3 กลีบระเบิด 0.8P", "Hits on a hero leave a petal; three burst for 0.8P"), s = new[] {
                K(Loc.S("อิไอโดะ", "Iaido"), KShape.Dash, 7, 1.2f, 6, 1.9f, "atk2"),
                K(Loc.S("พายุซากุระ", "Sakura Storm"), KShape.Cone, 4, 90, 8, 1.8f, "atk1"),
                With(K(Loc.S("ระบำดาบ", "Blade Dance"), KShape.Self, 0, 2.5f, 12, 2.0f, "spin"), k => { k.invuln = .6f; k.delay = .6f; }) } },
            ["ninja"] = new Kit { pid = "shadow", pn = Loc.S("เงาราตรี", "Night Shade"), pd = Loc.S("ไม่ได้สู้ 4 วินาที: ตีครั้งถัดไป +50%", "After 4 s out of a fight: next hit +50%"), s = new[] {
                K(Loc.S("ดาวกระจาย", "Shuriken Fan"), KShape.Cone, 9, 40, 5, 1.8f, "atk0"),
                K(Loc.S("ก้าวเงา", "Shadow Step"), KShape.Blink, 6, 1, 9, 1.6f, "atk2"),
                With(K(Loc.S("ระเบิดควัน", "Smoke Bomb"), KShape.Self, 0, 3, 13, 1.4f, "spin"), k => k.invis = 1.5f) } },
            ["hood"] = new Kit { pid = "hunt", pn = Loc.S("ล่าเหยื่อ", "Hunter"), pd = Loc.S("ใส่เป้าเลือดต่ำกว่า 35% +30%; ฆ่าหรือช่วยฆ่าฮีโร่ รีเซ็ต Q", "+30% on targets under 35%; a hero takedown resets Q"), s = new[] {
                K(Loc.S("กริชเกี่ยว", "Hook Dagger"), KShape.Dash, 8, 1, 7, 1.9f, "atk2"),
                With(K(Loc.S("ใบมีดพิษ", "Venom Blades"), KShape.Cone, 4, 70, 9, 1.0f, "atk1"), k => { k.dot = 1.2f; k.dotT = 4; k.slow = 2; }),
                K(Loc.S("ปลิดชีพ", "Execute"), KShape.Target, 2.5f, 1, 14, 2.8f, "atk2") } },
            ["harpy"] = new Kit { pid = "wind", pn = Loc.S("ปีกลม", "Tailwind"), pd = Loc.S("เดินเร็วขึ้น 10%; หลังใช้ W ยิง 2 นัดถัดไป +40%", "10% faster; after W the next 2 shots +40%"), s = new[] {
                K(Loc.S("ห่าขนนก", "Feather Rain"), KShape.Cone, 9, 60, 5, 2.1f, "bowshot"),
                K(Loc.S("ดิ่งฟ้า", "Sky Dive"), KShape.Leap, 8, 2.8f, 10, 1.6f, "leap"),
                With(K(Loc.S("พายุปีก", "Wing Gale"), KShape.Line, 14, 1.6f, 11, 2.6f, "bowshot"), k => k.knock = 4) } },
            ["elf"] = new Kit { pid = "third", pn = Loc.S("ลูกที่สาม", "Third Arrow"), pd = Loc.S("ยิงปกติลูกที่ 3 แรงขึ้น 60% และทำให้ช้า", "Every third shot +60% and slows"), s = new[] {
                With(K(Loc.S("ฝนธนู", "Arrow Rain"), KShape.Point, 10, 3, 6, .45f, "bowshot"), k => { k.hits = 5; k.dur = 1.5f; }),
                K(Loc.S("ลูกศรทะลวง", "Piercing Arrow"), KShape.Line, 16, 1, 9, 2.4f, "bowshot"),
                With(K(Loc.S("ก้าวใบไม้", "Leaf Step"), KShape.Dash, 6, 1, 10, 0, "roll"), k => k.back = 1) } },
            ["rowan"] = new Kit { pid = "ranger", pn = Loc.S("ทักษะพราน", "Ranger"), pd = Loc.S("ยิงเป้าที่ช้าหรือถูกตรึง +20%", "+20% on slowed or held targets"), s = new[] {
                With(K(Loc.S("กับดักหนีบ", "Bear Trap"), KShape.Point, 8, 1.4f, 8, 1.0f, "atk0"), k => { k.trap = 15; k.stun = 1.2f; }),
                K(Loc.S("ระดมยิง", "Volley"), KShape.Cone, 9, 45, 7, 2.9f, "bowshot"),
                With(K(Loc.S("เหยี่ยวหมายหัว", "Hawk Mark"), KShape.Target, 12, 1, 12, 1.2f, "bowshot"), k => k.mark = 5) } },
            ["mage"] = new Kit { pid = "stack", pn = Loc.S("พลังสะสม", "Arcane Charge"), pd = Loc.S("สกิลโดนฮีโร่ได้ 1 สแต็ก (สูงสุด 3) สกิลถัดไป +15% ต่อสแต็ก", "Skill hits on heroes stack (max 3): next skill +15% each"), s = new[] {
                K(Loc.S("เลเซอร์", "Laser"), KShape.Line, 12, 1, 7, 2.0f, "cast1"),
                With(K(Loc.S("อุกกาบาต", "Meteor"), KShape.Point, 11, 3.5f, 10, 2.1f, "cast2"), k => k.delay = .8f),
                With(K(Loc.S("บาเรียน้ำแข็ง", "Frost Ward"), KShape.Self, 0, 3.5f, 14, 0, "cast"), k => { k.shieldSelf = .25f; k.slow = 2; }) } },
            ["darkmage"] = new Kit { pid = "drain", pn = Loc.S("ดูดวิญญาณ", "Soul Drain"), pd = Loc.S("ดาเมจจากสกิลฮีลตัวเอง 15%", "Skill damage heals 15% of it"), s = new[] {
                K(Loc.S("ลูกไฟเงา", "Shadow Bolt"), KShape.Target, 11, 1, 6, 1.4f, "cast1"),
                With(K(Loc.S("คำสาป", "Curse"), KShape.Point, 9, 3, 10, 0, "cast2"), k => { k.dot = 2.1f; k.dotT = 4; k.slow = 4; }),
                With(K(Loc.S("ดูดชีวิต", "Life Siphon"), KShape.Target, 7, 1, 12, 1.3f, "cast"), k => { k.tether = 3; k.heal = .08f; }) } },
            ["sorceress"] = new Kit { pid = "tide", pn = Loc.S("กระแสวารี", "Undertow"), pd = Loc.S("สกิลโดนศัตรูทำให้ช้า", "Skill hits slow enemies"), s = new[] {
                With(K(Loc.S("คลื่นยักษ์", "Tidal Wave"), KShape.Cone, 7, 70, 7, 2.3f, "cast1"), k => k.knock = 3),
                With(K(Loc.S("คุกฟองน้ำ", "Bubble Prison"), KShape.Point, 9, 1.8f, 10, 1.3f, "cast2"), k => k.stun = 1.2f),
                With(K(Loc.S("หอกน้ำแข็ง", "Ice Shards"), KShape.Self, 0, 3.2f, 13, 1.0f, "cast"), k => { k.hits = 4; k.dur = 6; }) } },
            ["sunpriest"] = new Kit { pid = "aura", pn = Loc.S("พรแห่งแสง", "Blessing"), pd = Loc.S("เพื่อนในรัศมี 6 ม. ฟื้นเลือด 1% ต่อวินาที", "Allies within 6 m heal 1% a second"), s = new[] {
                With(K(Loc.S("แสงเยียวยา", "Healing Light"), KShape.Self, 0, 6, 12, 0, "cast"), k => { k.heal = .18f; k.allies = true; }),
                With(K(Loc.S("สถานศักดิ์สิทธิ์", "Sanctuary"), KShape.Point, 8, 4, 16, 0, "cast2"), k => { k.dot = 1.5f; k.dotT = 5; k.cut = .2f; k.cutT = 5; }),
                With(K(Loc.S("พิพากษา", "Holy Smite"), KShape.Point, 9, 1.5f, 9, 2.2f, "cast1"), k => { k.stun = .6f; k.delay = .3f; }) } },
            ["lyra"] = new Kit { pid = "moon", pn = Loc.S("แสงจันทร์", "Moonlight"), pd = Loc.S("ฮีลและโล่แรงขึ้น 20% กับเป้าเลือดต่ำกว่า 40%", "+20% heals and shields on targets under 40%"), s = new[] {
                With(K(Loc.S("โล่จันทรา", "Moon Ward"), KShape.Ally, 8, 1, 8, 0, "cast"), k => k.shieldAll = .22f),
                With(K(Loc.S("ดาวตก", "Starfall"), KShape.Point, 10, 3, 10, 3.0f, "cast2"), k => { k.slow = 1.5f; k.delay = .5f; }),
                With(K(Loc.S("บทเพลงชำระ", "Cleansing Song"), KShape.Self, 0, 6, 15, 0, "cast1"), k => { k.atkBuff = .3f; k.buffT = 3; k.allies = true; }) } },
            ["monk"] = new Kit { pid = "qi", pn = Loc.S("ชี่ไหลเวียน", "Flowing Qi"), pd = Loc.S("ตีปกติโดนฮีโร่ลดคูลดาวน์ทุกสกิล 0.4 วินาที", "Basic hits on heroes cut all cooldowns by 0.4 s"), s = new[] {
                With(K(Loc.S("ฝ่ามือพิฆาต", "Palm Strike"), KShape.Dash, 5, 1.4f, 7, 1.7f, "atk2"), k => { k.knock = 3; k.stun = .3f; }),
                With(K(Loc.S("หมัดพายุ", "Fist Storm"), KShape.Cone, 3, 60, 9, .4f, "atk1"), k => { k.hits = 5; k.dur = .8f; }),
                With(K(Loc.S("ระเบิดชี่", "Qi Burst"), KShape.Self, 0, 4, 13, 1.6f, "spin"), k => { k.pullIn = 1; k.shieldSelf = .08f; }) } },
            ["tenzin"] = new Kit { pid = "focus", pn = Loc.S("สมาธิ", "Focus"), pd = Loc.S("ทุก 8 วินาที กันการโจมตีครั้งถัดไปได้หมด", "Every 8 s the next hit is blocked"), s = new[] {
                With(K(Loc.S("เตะเสย", "Rising Kick"), KShape.Line, 4, 1.4f, 8, 2.0f, "atk2"), k => k.stun = .7f),
                With(K(Loc.S("ท่าสวนกลับ", "Counter Stance"), KShape.Self, 0, 3, 10, 2.6f, "guard"), k => { k.counter = 1; k.stun = .6f; }),
                With(K(Loc.S("กายเหล็ก", "Iron Body"), KShape.Self, 0, 3, 14, 0, "guard"), k => { k.cut = .3f; k.cutT = 3; }) } },
        };
        static KSkill With(KSkill k, System.Action<KSkill> f) { f(k); return k; }
        public static Kit For(string heroId) => heroId != null && all.TryGetValue(heroId, out var k) ? k : null;
        // a skill described from its own numbers (the lobby's lane-skill box)
        public static string Describe(KSkill s)
        {
            var p = new System.Collections.Generic.List<string>();
            string shape = s.shape switch
            {
                KShape.Self => Loc.S($"วงรอบตัว {s.size:0.#} ม.", $"circle round you {s.size:0.#} m"),
                KShape.Point => Loc.S($"วงที่จุดเล็ง ไกล {s.range:0} ม. กว้าง {s.size:0.#} ม.", $"circle at the aim, {s.range:0} m away, {s.size:0.#} m wide"),
                KShape.Line => Loc.S($"เส้นตรง {s.range:0} ม.", $"line {s.range:0} m"),
                KShape.Cone => Loc.S($"กรวย {s.size:0}° ยาว {s.range:0} ม.", $"cone {s.size:0}°, {s.range:0} m"),
                KShape.Dash => s.back > 0 ? Loc.S($"กระโดดถอย {s.range:0} ม.", $"leap back {s.range:0} m") : Loc.S($"พุ่ง {s.range:0} ม.", $"dash {s.range:0} m"),
                KShape.Leap => Loc.S($"กระโดดไปจุดเล็ง {s.range:0} ม.", $"leap to the aim, {s.range:0} m"),
                KShape.Blink => Loc.S($"วาร์ปไปหลังเป้า {s.range:0} ม.", $"blink behind a target, {s.range:0} m"),
                KShape.Target => Loc.S($"เป้าเดียว {s.range:0} ม.", $"one target, {s.range:0} m"),
                _ => Loc.S($"เพื่อนเลือดน้อยสุดในรัศมี {s.range:0} ม.", $"the most hurt ally within {s.range:0} m"),
            };
            p.Add(shape);
            if (s.dmg > 0) p.Add(s.hits > 1 ? Loc.S($"ดาเมจ {s.hits:0} × {s.dmg:0.##}P", $"damage {s.hits:0} × {s.dmg:0.##}P") : Loc.S($"ดาเมจ {s.dmg:0.##}P", $"damage {s.dmg:0.##}P"));
            if (s.dot > 0) p.Add(Loc.S($"ต่อเนื่อง {s.dot:0.#}P ใน {s.dotT:0} วิ", $"{s.dot:0.#}P over {s.dotT:0}s"));
            if (s.stun > 0) p.Add(s.trap > 0 ? Loc.S($"ตรึง {s.stun:0.#} วิ", $"holds {s.stun:0.#}s") : Loc.S($"มึน {s.stun:0.#} วิ", $"stun {s.stun:0.#}s"));
            if (s.slow > 0) p.Add(Loc.S($"ทำให้ช้า {s.slow:0.#} วิ", $"slow {s.slow:0.#}s"));
            if (s.knock > 0) p.Add(Loc.S("ผลักกระเด็น", "knockback"));
            if (s.pullIn > 0) p.Add(Loc.S("ดึงศัตรูเข้าหา", "pulls enemies in"));
            if (s.heal > 0) p.Add(Loc.S($"ฮีล {s.heal * 100:0}%{(s.allies ? " ทั้งทีม" : "")}", $"heal {s.heal * 100:0}%{(s.allies ? " to the team" : "")}"));
            if (s.shieldSelf > 0) p.Add(Loc.S($"โล่ตัวเอง {s.shieldSelf * 100:0}%", $"shield {s.shieldSelf * 100:0}%"));
            if (s.shieldAll > 0) p.Add(Loc.S($"โล่เพื่อน {s.shieldAll * 100:0}%", $"ally shield {s.shieldAll * 100:0}%"));
            if (s.atkBuff > 0) p.Add(Loc.S($"ATK +{s.atkBuff * 100:0}% {s.buffT:0} วิ", $"ATK +{s.atkBuff * 100:0}% {s.buffT:0}s"));
            if (s.cut > 0) p.Add(Loc.S($"รับดาเมจ -{s.cut * 100:0}%", $"-{s.cut * 100:0}% damage taken"));
            if (s.invuln > 0) p.Add(Loc.S($"อมตะ {s.invuln:0.#} วิ", $"untouchable {s.invuln:0.#}s"));
            if (s.invis > 0) p.Add(Loc.S($"ล่องหน {s.invis:0.#} วิ", $"invisible {s.invis:0.#}s"));
            if (s.counter > 0) p.Add(Loc.S("โดนตีระหว่างตั้งท่า = สวนกลับ", "struck while braced = counter"));
            if (s.trap > 0) p.Add(Loc.S($"กับดักอยู่ {s.trap:0} วิ", $"trap lasts {s.trap:0}s"));
            if (s.mark > 0) p.Add(Loc.S($"เป้ารับดาเมจ +15% {s.mark:0} วิ", $"target takes +15% for {s.mark:0}s"));
            if (s.tether > 0) p.Add(Loc.S($"โยงสาย {s.tether:0} วิ", $"tether {s.tether:0}s"));
            return string.Join(" · ", p) + Loc.S($" · คูลดาวน์ {s.cd:0} วิ", $" · CD {s.cd:0}s");
        }
        public static Color SideCol(int team) => team == 0 ? new Color(.35f, .8f, 1f) : new Color(1, .4f, .3f);
    }

    public partial class Hero
    {
        // ---------- MOBA kit (lane battles only) ----------
        public Kit kit; public bool KitOn => G.lane && kit != null;
        int aimI = -1; bool aimMouse; SkillAim aim;
        float shieldHp, shieldEnd, cutK, cutEnd, atkK, atkEnd, lastFight = -99, focusReady, counterEnd, invisEnd;
        int shots, boostShots, stacks; readonly Dictionary<Foe, int> petals = new(); readonly Dictionary<Foe, float> marked = new();
        public float SkillCdMax(int i) => KitOn ? kit.s[i].cd : Data.Skill(def.skills[i]).cd;
        public string SkillName(int i) => KitOn ? kit.s[i].n : Data.SkillLook(def.id, def.skills[i]).n;
        public bool Invisible => KitOn && Time.time < invisEnd;
        Color KitCol(int i) => Data.SkillLook(def.id, def.skills[i]).c;
        float SkillCostOf(int i) => SkillCost(SkillCdMax(i));
        public float KitSpeed => (KitOn && kit.pid == "wind" ? 1.1f : 1) * (slowT > 0 ? .6f : 1);
        // hit by an AI hero's skill: held in place (stun) or slowed for a while
        public float stunT, slowT;
        public void Afflict(float stun, float slow)
        {
            if (dead || inv > .5f) return;
            if (stun > 0 && stun > stunT) { stunT = stun; Fx.Burst(Pos + Vector3.up * 2.1f, 10, new Color(1, .9f, .4f), 2, .6f, 0); G.Number(Pos + Vector3.up * 2.4f, Loc.S("มึน", "Stunned"), "hurt"); }
            if (slow > 0) slowT = Mathf.Max(slowT, slow);
        }
        public void GiveShield(float share) { shieldHp = Mathf.Max(shieldHp, max * share); shieldEnd = Time.time + 4; Fx.Ring(Pos, 1.4f, new Color(.5f, .85f, 1), .8f, .5f); }

        void KitBegin() { stunT = slowT = 0; kit = Kits.For(def.id); aimI = -1; shieldHp = 0; stacks = 0; shots = 0; boostShots = 0; petals.Clear(); marked.Clear(); focusReady = Time.time; if (aim) aim.Hide(); }

        // input: keys (Q W E on the web PC, U I O on the Windows build) or the skill buttons; held = aim, let go = cast
        Key[] KitKeys => G.ui.WebPc ? new[] { Key.Q, Key.W, Key.E } : new[] { Key.U, Key.I, Key.O };
        bool KitReady(int i) => cds[i] <= 0 && state != St.Skill && state != St.Dodge && mp >= SkillCostOf(i);
        void KitInput()
        {
            var ui = G.ui; var kb = Keyboard.current; var m = Mouse.current;
            if (!aim) aim = SkillAim.Make();
            if (stunT > 0) { for (int i = 0; i < 3; i++) { ui.skP[i] = false; ui.aimDown[i] = false; } aimI = -1; aim.Hide(); return; }   // stunned: no skills
            for (int i = 0; i < 3; i++) ui.skP[i] = false;   // the old one-press skills are not used here
            if (aimI < 0)
            {
                for (int i = 0; i < 3 && aimI < 0; i++)
                {
                    bool key = kb != null && (kb[KitKeys[i]].wasPressedThisFrame || kb[Key.Digit1 + i].wasPressedThisFrame);
                    bool btn = ui.aimDown[i];
                    if (!key && !btn) continue;
                    ui.aimDown[i] = false;
                    if (cds[i] > 0) continue;
                    if (mp < SkillCostOf(i)) { G.Number(Pos + Vector3.up * 2.2f, Loc.S("พลังไม่พอ", "Not enough power"), "hurt"); continue; }
                    aimI = i; aimMouse = key && m != null; ui.aimDrag = Vector2.zero;
                }
                if (aimI < 0) { if (Time.frameCount - testAimFrame > 2) aim.Hide(); return; }
            }
            var s = kit.s[aimI]; var at = AimPoint(s); aim.Show(this, s, at, KitCol(aimI));
            bool cancel = m != null && m.rightButton.wasPressedThisFrame || ui.aimCancel;
            bool release = aimMouse ? kb == null || !(kb[KitKeys[aimI]].isPressed || kb[Key.Digit1 + aimI].isPressed) : ui.aimUp[aimI];
            if (cancel) { ui.aimCancel = false; ui.aimUp[aimI] = false; aimI = -1; aim.Hide(); return; }
            if (!release) return;
            ui.aimUp[aimI] = false; int k = aimI; aimI = -1; aim.Hide();
            if (!KitReady(k)) return;
            mp -= SkillCostOf(k); cds[k] = s.cd; skillCo = StartCoroutine(KitRun(k, s, at));
        }
        Vector3 AimPoint(KSkill s)
        {
            var me = Pos; Vector3 p;
            if (aimMouse && Mouse.current != null && Camera.main)
            {
                var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
                p = new Plane(Vector3.up, new Vector3(0, me.y, 0)).Raycast(ray, out float t) ? ray.GetPoint(t) : me + Main.Dir(face) * s.range;
            }
            else if (G.ui.aimDrag.magnitude > .2f && Camera.main)
            {   // dragged from the button: the drag's direction on screen, its length the share of the range
                var f = Camera.main.transform.forward; f.y = 0; f.Normalize(); var r = Camera.main.transform.right; r.y = 0; r.Normalize();
                var d = r * G.ui.aimDrag.x + f * G.ui.aimDrag.y; p = me + d.normalized * Mathf.Max(1, s.range) * Mathf.Clamp01(G.ui.aimDrag.magnitude);
            }
            else
            {   // tapped: the nearest enemy (heroes first) in reach, else straight ahead
                ITarget best = null; float bd = Mathf.Max(s.range, s.size) + 2;
                foreach (var o in G.Opponents(0)) { float d = Main.Flat(o.Pos - me).magnitude - (o is Foe f && f.d.kind == "champ" ? 3 : 0); if (d < bd) { bd = d; best = o; } }
                p = best != null ? best.Pos : me + Main.Dir(face) * Mathf.Max(2, s.range * .7f);
            }
            if (s.shape == KShape.Self) return me;
            var off = Main.Flat(p - me); if (off.magnitude > s.range && s.range > 0) off = off.normalized * s.range;
            if (off.magnitude < .3f) off = Main.Dir(face) * .3f;
            return me + off;
        }

        // ---------- casting ----------
        IEnumerator KitRun(int i, KSkill s, Vector3 at)
        {
            state = St.Skill; inAtk = false; var col = KitCol(i); var dir = Main.Flat(at - Pos); if (dir.sqrMagnitude > .01f) face = Main.Ang(dir); var n = dir.sqrMagnitude > .01f ? dir.normalized : Main.Dir(face);
            Play(s.anim, .06f, 1.3f); if (anim && !anim.HasState(0, Animator.StringToHash(s.anim))) Play("atk2", .06f, 1.3f);
            float boost = 1; if (kit.pid == "stack" && stacks > 0) { boost = 1 + .15f * stacks; stacks = 0; }
            float P = AtkPow * (Time.time < atkEnd ? 1 + atkK : 1) * boost;
            if (s.invuln > 0) inv = Mathf.Max(inv, s.invuln);
            if (s.invis > 0) invisEnd = Time.time + s.invis;
            if (s.counter > 0) { counterEnd = Time.time + s.counter; counterSkill = s; counterP = P; Fx.Ring(Pos, 1.6f, col, .9f, .25f); yield return new WaitForSeconds(s.counter); state = St.Idle; skillCo = null; yield break; }
            if (s.delay > 0 && s.shape != KShape.Point) yield return new WaitForSeconds(s.delay);
            switch (s.shape)
            {
                case KShape.Self:
                    if (s.dmg > 0 || s.slow > 0 || s.pullIn > 0)
                    {
                        int hits = Mathf.Max(1, (int)s.hits);
                        for (int h = 0; h < hits; h++)
                        {
                            Fx.Ring(Pos, s.size, col, .45f, .3f); Fx.Slash(Pos + Vector3.up * .9f, face + h * 2.1f, col, s.size, 6.2f);
                            foreach (var o in Enemies(Pos, s.size)) { if (s.pullIn > 0 && o is Foe pf && pf.d.kind != "tower") pf.transform.position = Pos + Main.Flat(pf.Pos - Pos).normalized * 1.2f; Strike(o, s, P * s.dmg, col); }
                            if (hits > 1) yield return new WaitForSeconds(s.dur / hits);
                        }
                    }
                    if (s.heal > 0 || s.shieldSelf > 0 || s.shieldAll > 0 || s.atkBuff > 0 || s.cut > 0) Support(s, Pos, col);
                    break;
                case KShape.Point:
                    if (s.trap > 0) { StartCoroutine(Trap(at, s, P, col)); break; }
                    if (s.delay > 0) { Fx.Telegraph(at, s.size, s.delay); yield return new WaitForSeconds(s.delay); }
                    if (s.dotT > 0 && s.dmg <= 0) { StartCoroutine(Zone(at, s, P, col)); break; }
                    int ticks = Mathf.Max(1, (int)s.hits);
                    for (int h = 0; h < ticks; h++)
                    {
                        Fx.Ring(at, s.size, col, .5f, .35f); Fx.Spawn("ground_hit", at, s.size * .45f, 2); Fx.Burst(at + Vector3.up * .5f, 18, col, 5);
                        foreach (var o in Enemies(at, s.size)) Strike(o, s, P * s.dmg, col);
                        if (ticks > 1) yield return new WaitForSeconds(s.dur / ticks);
                    }
                    break;
                case KShape.Line:
                    {
                        var end = Pos + n * s.range; Fx.Beam(Pos + Vector3.up * 1.1f, end + Vector3.up * 1.1f, col, .25f, s.size * .35f);
                        foreach (var o in G.Opponents(0).ToList()) { if (InLine(o.Pos, Pos, n, s.range, s.size / 2 + o.Rad)) Strike(o, s, P * s.dmg, col, n); }
                    }
                    break;
                case KShape.Cone:
                    {
                        float half = s.size * Mathf.Deg2Rad / 2; int ticks2 = Mathf.Max(1, (int)s.hits);
                        for (int h = 0; h < ticks2; h++)
                        {
                            Fx.Slash(Pos + Vector3.up * .8f, face, col, s.range, half * 2);
                            foreach (var o in G.Opponents(0).ToList()) { var d = Main.Flat(o.Pos - Pos); if (d.magnitude - o.Rad <= s.range && (d.magnitude < .8f || Mathf.Abs(Main.AngDiff(face, Main.Ang(d))) <= half)) Strike(o, s, P * s.dmg, col, n); }
                            if (ticks2 > 1) yield return new WaitForSeconds(s.dur / ticks2);
                        }
                    }
                    break;
                case KShape.Dash:
                    {
                        var start = Pos; var dn = s.back > 0 ? -n : n; float dist = s.range;
                        if (s.dmg > 0 && kit.pid == "hunt")
                        {   // the hook stops at the first enemy on the line
                            foreach (var o in G.Opponents(0)) { var d = Main.Flat(o.Pos - Pos); float along = Vector3.Dot(d, n); if (along > 0 && along < dist && InLine(o.Pos, Pos, n, s.range, s.size / 2 + o.Rad)) dist = Mathf.Max(0, along - 1); }
                        }
                        float tt = 0, T = .18f + dist * .02f; var hit = new HashSet<ITarget>();
                        while (tt < T)
                        {
                            float step = dist / T * Time.deltaTime; tt += Time.deltaTime; MoveBy(dn.x * step, dn.z * step);
                            if (s.dmg > 0) foreach (var o in Enemies(Pos, s.size / 2 + .6f)) if (hit.Add(o)) Strike(o, s, P * s.dmg, col, dn);
                            Fx.Burst(Pos + Vector3.up * .6f, 2, col, 2, .3f, 0);
                            yield return null;
                        }
                        if (s.back > 0) { boostShots = 1; Fx.Ring(Pos, 1.2f, col, .4f); }
                    }
                    break;
                case KShape.Leap:
                    {
                        var start = Pos; float T = .45f, tt = 0; Play("leap", .05f, 1.2f);
                        while (tt < T) { tt += Time.deltaTime; var p = Vector3.Lerp(start, at, tt / T); transform.position = G.Clamp(p) + Vector3.up * Mathf.Sin(tt / T * Mathf.PI) * 2.2f; yield return null; }
                        transform.position = G.Clamp(at); Fx.Ring(Pos, s.size, col, .5f, .4f); Fx.Spawn("ground_hit", Pos, s.size * .5f, 2); G.shake = Mathf.Max(G.shake, .3f);
                        foreach (var o in Enemies(Pos, s.size)) Strike(o, s, P * s.dmg, col);
                        if (kit.pid == "wind") boostShots = 2;
                    }
                    break;
                case KShape.Blink:
                case KShape.Target:
                    {
                        ITarget t = null; float bd = s.range + 1.5f;
                        foreach (var o in G.Opponents(0)) { float d = Main.Flat(o.Pos - at).magnitude; if (Main.Flat(o.Pos - Pos).magnitude - o.Rad <= s.range + .5f && d < bd) { bd = d; t = o; } }
                        if (t == null) { Fx.Ring(at, 1, col, .3f); break; }
                        if (s.shape == KShape.Blink) { var behind = t.Pos + Main.Flat(t.Pos - Pos).normalized * 1.2f; Fx.Puff(Pos, col); transform.position = G.Clamp(behind); face = Main.Ang(t.Pos - Pos); Fx.Puff(Pos, col); }
                        if (s.tether > 0)
                        {   // a siphon: damage and healing through a beam over its time
                            float tt = 0, tick = .5f, per = P * s.dmg / (s.tether / tick);
                            while (tt < s.tether && !t.Down && Main.Flat(t.Pos - Pos).magnitude < s.range + 2) { Fx.Beam(Pos + Vector3.up * 1.2f, t.Pos + Vector3.up, col, tick, .1f); Strike(t, s, per, col); Heal(max * s.heal / (s.tether / tick), true); tt += tick; yield return new WaitForSeconds(tick); }
                            break;
                        }
                        Fx.Beam(Pos + Vector3.up * 1.2f, t.Pos + Vector3.up, col, .2f, .12f); Fx.Burst(t.Pos + Vector3.up, 20, col, 5);
                        Strike(t, s, P * s.dmg, col);
                        if (s.mark > 0 && t is Foe mf) marked[mf] = Time.time + s.mark;
                    }
                    break;
                case KShape.Ally:
                    {   // the ally (or the hero) with the least health in reach
                        ITarget best = this; float low = hp / max;
                        foreach (var f in G.foes) if (f && f.alive && f.team == 0 && f.d.kind == "champ" && Main.Flat(f.Pos - Pos).magnitude < s.range && f.hp / f.maxHp < low) { low = f.hp / f.maxHp; best = f; }
                        Shield(best, s.shieldAll, col);
                    }
                    break;
            }
            yield return new WaitForSeconds(.15f);
            state = St.Idle; skillCo = null;
        }
        KSkill counterSkill; float counterP;
        // tests (-kittest): show the aim of skill i at a point, or cast it there
        int testAimFrame = -9; public string AimDebug => aim ? aim.Debug() : "no aim";
        public void KitTestAim(int i, Vector3 at) { if (!aim) aim = SkillAim.Make(); testAimFrame = Time.frameCount; aim.Show(this, kit.s[i], at, KitCol(i)); }
        public void KitTestCast(int i, Vector3 at) { aim?.Hide(); cds[i] = 0; state = St.Idle; skillCo = StartCoroutine(KitRun(i, kit.s[i], at)); }

        static bool InLine(Vector3 p, Vector3 a, Vector3 n, float len, float half) { var d = Main.Flat(p - a); float along = Vector3.Dot(d, n); if (along < -.5f || along > len) return false; return (d - n * along).magnitude <= half; }
        List<ITarget> Enemies(Vector3 c, float r) => G.Opponents(0).Where(o => Main.Flat(o.Pos - c).magnitude - o.Rad <= r).ToList();

        void Strike(ITarget o, KSkill s, float dmg, Color col, Vector3? push = null)
        {
            if (o == null || o.Down || dmg <= 0 && s.stun <= 0 && s.slow <= 0) return;
            if (o is Foe f)
            {
                bool tower = f.d.kind == "tower";
                var hit = new Main.Hit { col = col, kb = tower ? 0 : s.knock, slow = tower ? 0 : s.slow + (kit.pid == "tide" ? 1.2f : 0), skill = true };
                if (dmg > 0) f.Damage(dmg, push.HasValue ? f.Pos - push.Value : Pos, hit); else if (!tower) f.slow = Mathf.Max(f.slow, hit.slow);
                if (!tower && f.alive)
                {
                    if (s.stun > 0) { f.stag = Mathf.Max(f.stag, s.stun); Fx.Burst(f.Pos + Vector3.up * (f.h + .3f), 8, new Color(1, .9f, .4f), 2, .5f, 0); }
                    if (s.dot > 0 && s.dmg > 0) StartCoroutine(Dot(f, AtkPow * s.dot, s.dotT, col));
                }
            }
            else o.Take(dmg, Pos);
        }
        IEnumerator Dot(Foe f, float total, float T, Color col)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(T / .5f));
            for (int i = 0; i < n && f && f.alive; i++) { yield return new WaitForSeconds(T / n); if (f && f.alive) f.Damage(total / n, f.Pos, new Main.Hit { col = col, skill = true }); }
        }
        IEnumerator Zone(Vector3 at, KSkill s, float P, Color col)
        {   // a lasting circle: enemies inside take damage over its time (and are slowed), allies inside take less
            float tt = 0, tick = .5f; int n = Mathf.Max(1, Mathf.RoundToInt(s.dotT / tick));
            for (int i = 0; i < n; i++)
            {
                Fx.Ring(at, s.size, col, .55f, .12f);
                foreach (var o in Enemies(at, s.size)) if (o is Foe f && f.d.kind != "tower") { f.Damage(P * s.dot / n, at, new Main.Hit { col = col, slow = s.slow > 0 ? .6f : 0, skill = true }); } else o.Take(P * s.dot / n, at);
                if (s.cut > 0 && Main.Flat(Pos - at).magnitude < s.size) { cutK = s.cut; cutEnd = Time.time + tick + .05f; }
                tt += tick; yield return new WaitForSeconds(tick);
            }
        }
        IEnumerator Trap(Vector3 at, KSkill s, float P, Color col)
        {   // a hidden trap: the first enemy to step on it is held fast and hurt
            float end = Time.time + s.trap; Fx.Ring(at, s.size, col, .6f, .3f);
            while (Time.time < end && G.lane && G.mode == Main.Mode.Game)
            {
                var o = Enemies(at, s.size).FirstOrDefault(x => x is Foe f && f.d.kind != "tower");
                if (o != null) { Fx.Spawn("ground_hit", at, .8f, 2); Strike(o, s, P * s.dmg, col); yield break; }
                if (Random.value < .1f) Fx.Ring(at, s.size * .7f, col * .5f, .4f, .2f);
                yield return new WaitForSeconds(.1f);
            }
        }
        void Support(KSkill s, Vector3 c, Color col)
        {
            Fx.Ring(c, s.size, col, .7f, .4f); Fx.Burst(c + Vector3.up, 30, col, 4, .8f, -1);
            if (s.heal > 0 && s.tether <= 0) { Heal(max * s.heal * MoonK(hp / max)); if (s.allies) foreach (var f in Allies(c, s.size)) { f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * s.heal * MoonK(f.hp / f.maxHp)); G.Number(f.Pos + Vector3.up * 2, "+" + Mathf.RoundToInt(f.maxHp * s.heal), "heal"); } }
            if (s.shieldSelf > 0) Shield(this, s.shieldSelf, col);
            if (s.shieldAll > 0 && s.allies) foreach (var f in Allies(c, s.size)) Shield(f, s.shieldAll, col);
            if (s.atkBuff > 0) { atkK = s.atkBuff; atkEnd = Time.time + s.buffT; }
            if (s.cut > 0 && s.dotT <= 0) { cutK = s.cut; cutEnd = Time.time + s.cutT; }
        }
        float MoonK(float share) => kit != null && kit.pid == "moon" && share < .4f ? 1.2f : 1;
        IEnumerable<Foe> Allies(Vector3 c, float r) => G.foes.Where(f => f && f.alive && f.team == 0 && f.d.kind == "champ" && Main.Flat(f.Pos - c).magnitude < r).ToList();
        void Shield(ITarget t, float share, Color col)
        {
            if (t is Hero h) { h.shieldHp = Mathf.Max(h.shieldHp, h.max * share * MoonK(h.hp / h.max)); h.shieldEnd = Time.time + 4; G.Number(h.Pos + Vector3.up * 2.3f, Loc.S("โล่ ", "Shield ") + Mathf.RoundToInt(h.shieldHp), "heal"); }
            else if (t is Foe f) { f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * share * .6f); G.Number(f.Pos + Vector3.up * 2.3f, "+" + Mathf.RoundToInt(f.maxHp * share * .6f), "heal"); }   // AI heroes have no shield: part of it as health
            Fx.Ring(t.Pos, 1.4f, col, .8f, .5f);
        }

        // ---------- passives ----------
        void KitTick(float dt)
        {
            if (aimI >= 0 && (dead || state == St.Dead)) { aimI = -1; aim?.Hide(); }
            if (kit.pid == "aura")
            {   // the priestess's blessing: allies near her (and she) heal a little all the time
                if (!dead) Heal(max * .01f * dt, true);
                foreach (var f in Allies(Pos, 6)) f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * .01f * dt);
            }
            if (shieldHp > 0 && Time.time > shieldEnd) shieldHp = 0;
            stunT = Mathf.Max(0, stunT - dt); slowT = Mathf.Max(0, slowT - dt);
            if (kit.pid == "focus" && Time.time >= focusReady && Random.value < dt * 2) Fx.Burst(Pos + Vector3.up * 1.6f, 1, new Color(1, .9f, .5f), 1, .4f, 0);
        }
        // damage this hero deals (basic hits and skills) on its way to a foe
        public float KitOut(Foe f, Main.Hit o)
        {
            float k = 1; lastFightMark();
            if (Time.time < atkEnd && !o.skill) k *= 1 + atkK;
            switch (kit.pid)
            {
                case "rage": if (hp < max * .5f) k *= 1.2f; break;
                case "shadow": if (Time.time - lastFight > 4) { k *= 1.5f; Fx.Burst(f.Pos + Vector3.up, 14, new Color(.6f, .4f, 1), 4); } break;
                case "hunt": if (f.hp < f.maxHp * .35f) k *= 1.3f; break;
                case "ranger": if (f.slow > 0 || f.stag > .2f) k *= 1.2f; break;
                case "third": if (!o.skill && ++shots % 3 == 0) { k *= 1.6f; o.slow = Mathf.Max(o.slow, 1); } break;
            }
            if (!o.skill && boostShots > 0) { boostShots--; k *= kit.pid == "third" ? 2 : 1.4f; }
            if (marked.TryGetValue(f, out float until) && Time.time < until) k *= 1.15f;
            lastFight = Time.time; return k;
        }
        void lastFightMark() { }
        // after a hit landed (dmg as dealt)
        public void KitHit(Foe f, Main.Hit o, float dmg)
        {
            bool hero = f.d.kind == "champ";
            switch (kit.pid)
            {
                case "rage": if (!o.skill && hp < max * .5f) Heal(dmg * .08f, true); break;
                case "drain": if (o.skill) Heal(dmg * .15f, true); break;
                case "stack": if (o.skill && hero) stacks = Mathf.Min(3, stacks + 1); break;
                case "qi": if (!o.skill && hero) for (int i = 0; i < 3; i++) cds[i] = Mathf.Max(0, cds[i] - .4f); break;
                case "petal":
                    if (hero && f.alive && !bursting)
                    {
                        petals.TryGetValue(f, out int c); c++;
                        if (c >= 3) { c = 0; Fx.Burst(f.Pos + Vector3.up, 30, new Color(1, .55f, .8f), 6); bursting = true; f.Damage(AtkPow * .8f, f.Pos, new Main.Hit { col = new Color(1, .55f, .8f), skill = true }); bursting = false; }   // the burst itself leaves no petal
                        petals[f] = c;
                    }
                    break;
            }
        }
        bool bursting;
        public void KitTakedown() { if (kit != null && kit.pid == "hunt") { cds[0] = 0; G.Number(Pos + Vector3.up * 2.4f, Loc.S("Q พร้อม!", "Q ready!"), "crit"); } }
        // damage coming in (after armour), returns what is left
        float KitIn(float d, Vector3 from)
        {
            lastFight = Time.time;
            if (Time.time < counterEnd && counterSkill != null)
            {   // the counter stance: the blow is turned back on everyone near
                counterEnd = 0; Fx.Ring(Pos, counterSkill.size, new Color(1, .85f, .4f), .5f, .4f); Fx.Spawn("ground_hit", Pos, 1.2f, 2); Play("atk2", .05f, 1.5f);
                foreach (var o in Enemies(Pos, counterSkill.size)) Strike(o, counterSkill, counterP * counterSkill.dmg, new Color(1, .85f, .4f));
                return 0;
            }
            if (kit.pid == "focus" && Time.time >= focusReady) { focusReady = Time.time + 8; G.Number(Pos + Vector3.up * 2.2f, Loc.S("กันได้", "Blocked"), ""); Fx.Ring(Pos, 1.3f, new Color(1, .9f, .5f), .4f); return 0; }
            if (kit.pid == "front" && Vector3.Angle(Main.Dir(face), Main.Flat(from - Pos)) < 60) d *= .8f;
            if (Time.time < cutEnd) d *= 1 - cutK;
            if (shieldHp > 0) { float take = Mathf.Min(shieldHp, d); shieldHp -= take; d -= take; if (take > 0) G.Number(Pos + Vector3.up * 2.4f, Loc.S("โล่ -", "Shield -") + Mathf.RoundToInt(take), ""); }
            return d;
        }
    }

    // the shape of a skill on the ground while it is held: the reach as a faint ring round the hero, the area it will hit filled in
    public class SkillAim : MonoBehaviour
    {
        MeshRenderer reach, area; MeshFilter areaF; Material reachM, areaM; KSkill shown;
        public static SkillAim Make()
        {
            var go = new GameObject("SkillAim"); var a = go.AddComponent<SkillAim>();
            a.reach = a.Part("reach", out _, out a.reachM); a.area = a.Part("area", out a.areaF, out a.areaM); a.reach.GetComponent<MeshFilter>().sharedMesh = Annulus(.97f, 1, 72); go.SetActive(false); return a;
        }
        MeshRenderer Part(string n, out MeshFilter mf, out Material m)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false); mf = go.AddComponent<MeshFilter>(); var r = go.AddComponent<MeshRenderer>();
            m = new Material(Catalog.I.glowMat) { renderQueue = 3010, mainTexture = Texture2D.whiteTexture }; m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 10); m.SetFloat("_UseVC", 0); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return r;
        }
        public void Hide() { if (gameObject.activeSelf) gameObject.SetActive(false); shown = null; }
        public string Debug() => $"aim go {gameObject.activeInHierarchy} reach {reach.gameObject.activeSelf} {reach.transform.lossyScale} area {area.transform.position} {area.transform.lossyScale} verts {areaF.sharedMesh?.vertexCount} col {areaM.color} q {areaM.renderQueue} vis {area.isVisible}/{reach.isVisible} shader {areaM.shader.name} cull {(areaM.HasProperty("_Cull") ? areaM.GetFloat("_Cull") : -1)} ztest {(areaM.HasProperty("_ZTest") ? areaM.GetFloat("_ZTest") : -1)}";
        public void Show(Hero h, KSkill s, Vector3 at, Color c)
        {
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            var me = h.Pos + Vector3.up * .08f; transform.position = me; transform.rotation = Quaternion.identity;
            reach.gameObject.SetActive(s.range > 0); reach.transform.localScale = new Vector3(s.range, 1, s.range);
            float top = Mathf.Max(.01f, Mathf.Max(c.r, Mathf.Max(c.g, c.b))); var flat = new Color(c.r / top, c.g / top, c.b / top);   // the hue without its glow (HDR colours burned out white)
            var rc = Color.Lerp(flat, Color.white, .3f); rc.a = .75f; reachM.SetColor("_Color", rc); var ac = flat * .9f; ac.a = .38f; areaM.SetColor("_Color", ac);
            if (shown != s) { shown = s; areaF.sharedMesh = s.shape switch { KShape.Line or KShape.Dash => Rect(s.size, s.range), KShape.Cone => Cone(s.range, s.size * Mathf.Deg2Rad), _ => Disc(1, 48) }; }
            var dir = Main.Flat(at - h.Pos); float yaw = dir.sqrMagnitude > .01f ? Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg : h.face * Mathf.Rad2Deg;
            var t = area.transform;
            switch (s.shape)
            {
                case KShape.Line: case KShape.Dash: case KShape.Cone: t.localPosition = Vector3.zero; t.localRotation = Quaternion.Euler(0, yaw + (s.back > 0 ? 180 : 0), 0); t.localScale = Vector3.one; break;
                case KShape.Self: t.localPosition = Vector3.zero; t.localScale = new Vector3(s.size, 1, s.size); break;
                case KShape.Ally: t.localPosition = Vector3.zero; t.localScale = new Vector3(s.range, 1, s.range); break;
                default: t.position = new Vector3(at.x, me.y, at.z); t.localScale = Vector3.one * Mathf.Max(.8f, s.size); break;   // a circle where it lands
            }
        }
        static Mesh Both(Mesh m) { var t = m.triangles; var b = new int[t.Length * 2]; for (int i = 0; i < t.Length; i += 3) { b[i] = t[i]; b[i + 1] = t[i + 1]; b[i + 2] = t[i + 2]; b[t.Length + i] = t[i]; b[t.Length + i + 1] = t[i + 2]; b[t.Length + i + 2] = t[i + 1]; } m.triangles = b; m.RecalculateBounds(); return m; }   // seen from either side
        static Mesh Disc(float r, int seg) { var v = new List<Vector3> { Vector3.zero }; var tri = new List<int>(); for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; v.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r); if (i > 0) tri.AddRange(new[] { 0, i, i + 1 }); } var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); return Both(m); }
        static Mesh Annulus(float r0, float r1, int seg) { var v = new List<Vector3>(); var tri = new List<int>(); for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); v.Add(d * r0); v.Add(d * r1); if (i < seg) { int k = i * 2; tri.AddRange(new[] { k, k + 1, k + 2, k + 1, k + 3, k + 2 }); } } var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); return Both(m); }
        static Mesh Rect(float w, float len) { var m = new Mesh(); m.SetVertices(new List<Vector3> { new(-w / 2, 0, 0), new(-w / 2, 0, len), new(w / 2, 0, len), new(w / 2, 0, 0) }); m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0); return Both(m); }
        static Mesh Cone(float r, float span) { var v = new List<Vector3> { Vector3.zero }; var tri = new List<int>(); int seg = 24; for (int i = 0; i <= seg; i++) { float a = -span / 2 + span * i / seg; v.Add(new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r); if (i > 0) tri.AddRange(new[] { 0, i, i + 1 }); } var m = new Mesh(); m.SetVertices(v); m.SetTriangles(tri, 0); return Both(m); }
    }
}
