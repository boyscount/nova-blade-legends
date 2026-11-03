using UnityEngine;

namespace NovaBlade
{
    // Game data (ported from the web prototype arena/index.html).
    public class HeroDef
    {
        public string id, name, title, desc, cls = "warrior"; public float hp, spd, atk; public bool rig, ranged, blade = true; public string[] skills;
        public (string n, Color tint, Color glow)[] skins; public float[] stats;
    }
    public class SwordDef { public string id, name, desc, type = "sword"; public Color color; public float dmg, spd, len, m, crit, steal; public string burn; public bool slow; public int price; }
    public class PetDef { public string id, name, desc, kind; public Color color; public float cd, dmg, h, steal; public bool fly, gold; }
    public class SkillDef { public string id, n; public float cd; public Color c; public string icon; }
    public class FoeDef { public string id, model, name, kind; public float hp, spd, dmg, range, scale, rad; public int gold; public bool fly; }
    public class BossDef { public string model, name, summon; public float scale, hp; public Color tint, glow, rage, color; public bool fly; }
    public class ThemeDef { public string id, name, desc; public (string foe, int n)[][] waves; public BossDef boss; }

    public static class Data
    {
        static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        static Color G(int hex, float k) => C(hex) * k;

        public static readonly HeroDef[] Heroes =
        {
            new HeroDef { id = "warrior", name = "วัลก้า", title = "นักรบเถื่อนแห่งขุนเขา", desc = "สายบู๊ เลือดหนา ปั่นดาบเป็นพายุกลางฝูงศัตรู", hp = 1300, spd = 5.0f, atk = 1.15f, rig = true,
                skills = new[] { "whirl", "slam", "cry" }, stats = new[] { .9f, .8f, .55f, .5f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("เลือดมังกร", C(0xffb4a4), G(0xff3a1a, .25f)), ("เงาราตรี", C(0x9d97c4), G(0x8a4dff, .25f)), ("นักรบทองคำ", C(0xffe2a2), G(0xffb52e, .2f)) } },
            new HeroDef { id = "samurai", cls = "assassin", name = "ซากุระ", title = "ซามูไรกลีบซากุระ", desc = "ว่องไวที่สุด พุ่งฟันทะลุศัตรู ระบำดาบไล่ฟันทีละตัว", hp = 1000, spd = 5.8f, atk = 1.05f, rig = true,
                skills = new[] { "iaido", "petal", "dance" }, stats = new[] { .55f, .75f, .95f, .6f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ซากุระราตรี", C(0xffc8e4), G(0xff5fb4, .22f)), ("หิมะขาว", C(0xdff2ff), G(0x7fd8ff, .2f)), ("อสูรเพลิง", C(0xffa080), G(0xff4a1a, .25f)) } },
            new HeroDef { id = "harpy", cls = "archer", ranged = true, name = "แอริส", title = "ฮาร์ปี้นักรบแห่งฟากฟ้า", desc = "ยิงขนนกระยะไกล ดิ่งฟ้ากระแทก และพายุปีกดูดศัตรู", hp = 950, spd = 5.4f, atk = 1.0f, rig = true,
                skills = new[] { "feather", "dive", "gale" }, stats = new[] { .5f, .7f, .8f, .95f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ปีกอัคคี", C(0xffc29a), G(0xff6a1a, .22f)), ("ปีกพายุ", C(0xb8e6ff), G(0x3fb8ff, .22f)), ("ปีกมรณะ", C(0x8e86a8), G(0xb04dff, .25f)) } },
            new HeroDef { id = "spartan", name = "ไลร่า", title = "นักรบโล่แห่งหุบเขา", desc = "สายนักรบ ถือโล่หนัก ทนทาน ปั่นดาบกลางฝูงศัตรู", cls = "warrior", hp = 1350, spd = 4.9f, atk = 1.15f, rig = true,
                skills = new[] { "whirl", "slam", "cry" }, stats = new[] { 0.95f, 0.8f, 0.5f, 0.5f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ทองสัมฤทธิ์", C(0xffe2a2), G(0xffb52e, .22f)), ("เงาราตรี", C(0x9d97c4), G(0x5a4dff, .22f)), ("เลือดศึก", C(0xffb4a4), G(0xff3a1a, .22f)) } },
            new HeroDef { id = "elf", name = "เอลวีน", title = "พรานธนูแห่งพงไพร", desc = "สายธนู ยิงไกล ปล่อยห่าลูกศร แล้วกระโดดถอยตั้งหลัก", cls = "archer", hp = 900, spd = 5.5f, atk = 1.05f, rig = true, ranged = true, blade = false,
                skills = new[] { "feather", "dive", "gale" }, stats = new[] { 0.5f, 0.75f, 0.8f, 1f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ใบไม้ผลิ", C(0xc8ffc0), G(0x5fff7a, .22f)), ("หิมะเงิน", C(0xdff2ff), G(0x7fd8ff, .22f)), ("ใบไม้ร่วง", C(0xffc89a), G(0xff7a1a, .22f)) } },
            new HeroDef { id = "ninja", name = "คาเงะ", title = "นินจาเงาราตรี", desc = "สายนักฆ่า พุ่งฟันทะลุศัตรู เร็วที่สุดในบรรดาฮีโร่", cls = "assassin", hp = 950, spd = 6.0f, atk = 1.1f, rig = true,
                skills = new[] { "iaido", "petal", "dance" }, stats = new[] { 0.5f, 0.85f, 1f, 0.55f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("เงาแดง", C(0xffb0b0), G(0xff3a3a, .22f)), ("เงาคราม", C(0xb0c8ff), G(0x3a6aff, .22f)), ("เงาทอง", C(0xffe8a8), G(0xffc02e, .22f)) } },
            new HeroDef { id = "hood", name = "เรเวน", title = "มือสังหารใต้ผ้าคลุม", desc = "สายนักฆ่า ไล่ฟันทีละเป้าแล้วหายไปในเงา", cls = "assassin", hp = 1000, spd = 5.8f, atk = 1.1f, rig = true,
                skills = new[] { "dance", "iaido", "petal" }, stats = new[] { 0.55f, 0.85f, 0.95f, 0.55f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("เขม่าเถ้า", C(0xc8c8c8), G(0x8a8a8a, .22f)), ("พิษมรกต", C(0xb0ffc8), G(0x2eff8a, .22f)), ("โลหิต", C(0xffa0a0), G(0xff2a3a, .22f)) } },
            new HeroDef { id = "sunpriest", name = "โซเลีย", title = "นักบวชแห่งสุริยะ", desc = "สายนักบวช ยิงแสงศักดิ์สิทธิ์ ฟื้นพลังชีวิต และกางบาเรีย", cls = "cleric", hp = 1050, spd = 5.0f, atk = 0.95f, rig = true, ranged = true, blade = false,
                skills = new[] { "heal", "shield", "cry" }, stats = new[] { 0.7f, 0.6f, 0.6f, 0.85f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("รุ่งอรุณ", C(0xffd8b0), G(0xff9a3a, .22f)), ("จันทรา", C(0xd8e0ff), G(0x8aa8ff, .22f)), ("สุริยคราส", C(0x9a8a7a), G(0xff5a1a, .22f)) } },
            new HeroDef { id = "monk", name = "หมิง", title = "จอมยุทธ์หมัดเพลิงทอง", desc = "สายหมัด เตะลอยฟ้า บู๊มือเปล่า ทุบพื้น ปั่นตัวเป็นพายุหมัด", cls = "monk", hp = 1250, spd = 5.3f, atk = 1.1f, rig = true, blade = false,
                skills = new[] { "slam", "whirl", "cry" }, stats = new[] { 0.85f, 0.8f, 0.7f, 0.5f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("จีวรแดง", C(0xffb0a0), G(0xff4a2a, .22f)), ("ภูเขาหิมะ", C(0xe0f0ff), G(0x8ad8ff, .22f)), ("มังกรทอง", C(0xffe2a2), G(0xffb52e, .22f)) } },
            new HeroDef { id = "mage", name = "อัลดริค", title = "จอมเวทผ้าคลุมคราม", desc = "สายเวท ยิงลำแสงเวท ปล่อยลูกไฟไล่ล่า และกางบาเรีย", cls = "mage", hp = 850, spd = 5.0f, atk = 1.15f, rig = true, ranged = true, blade = false,
                skills = new[] { "laser", "missile", "shield" }, stats = new[] { 0.45f, 0.95f, 0.6f, 0.95f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("น้ำแข็ง", C(0xd0f0ff), G(0x5fd8ff, .22f)), ("อัคคี", C(0xffc0a0), G(0xff5a1a, .22f)), ("มรกต", C(0xc0ffd8), G(0x3aff8a, .22f)) } },
            new HeroDef { id = "darkmage", name = "ไนต์", title = "จอมเวทเสื้อคลุมดำ", desc = "สายเวท ระดมลูกไฟเวทไล่ล่า แล้วเผาด้วยลำแสง", cls = "mage", hp = 880, spd = 5.1f, atk = 1.15f, rig = true, ranged = true, blade = false,
                skills = new[] { "missile", "laser", "shield" }, stats = new[] { 0.45f, 0.95f, 0.65f, 0.9f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ราตรีม่วง", C(0xc8b0ff), G(0x8a4dff, .22f)), ("เถ้าเพลิง", C(0xffb090), G(0xff4a1a, .22f)), ("เงินจันทร์", C(0xe8eeff), G(0x9ad0ff, .22f)) } },
            new HeroDef { id = "sorceress", name = "เอเรีย", title = "แม่มดวารีสีคราม", desc = "สายเวท คล่องตัว กางบาเรียแล้วยิงลำแสงเวท", cls = "mage", hp = 820, spd = 5.4f, atk = 1.1f, rig = true, ranged = true, blade = false,
                skills = new[] { "laser", "shield", "missile" }, stats = new[] { 0.4f, 0.9f, 0.75f, 0.9f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ทะเลลึก", C(0xb0d0ff), G(0x3a8aff, .22f)), ("ดอกซากุระ", C(0xffc8e4), G(0xff5fb4, .22f)), ("ทองสุริยา", C(0xffe8b0), G(0xffb52e, .22f)) } },
            new HeroDef { id = "toybot", name = "โบลต์", title = "หุ่นจิ๋วพลังไฟฟ้า", desc = "ลอยตัว ยิงเลเซอร์ มิสไซล์ไล่ล่า และบาเรียกันดาเมจ", hp = 1100, spd = 5.2f, atk = 1.0f, rig = false,
                skills = new[] { "laser", "missile", "shield" }, stats = new[] { .7f, .7f, .65f, .85f },
                skins = new[] { ("ดั้งเดิม", Color.white, Color.black), ("ไซเบอร์ชมพู", C(0xffc6ea), G(0xff4fd8, .25f)), ("ทหารพราน", C(0xb9d29a), G(0x8aff4f, .2f)), ("โครเมียมดำ", C(0x6c7380), G(0x5ff6ff, .25f)) } },
        };

        public static readonly SwordDef[] Swords =
        {
            new SwordDef { id = "w_lava", price = 0, name = "ดาบลาวาอัคคี", desc = "ฟันติดไฟ เผาศัตรูต่อเนื่อง", color = C(0xff5a1a), dmg = 1.05f, spd = 1, len = 1, m = 1.1f, burn = "fire" },
            new SwordDef { id = "w_fantasy", price = 400, name = "ดาบมนตร์ม่วง", desc = "สมดุล ฟันเร็วขึ้นเล็กน้อย", color = C(0xb06bff), dmg = 1.05f, spd = 1.08f, len = 1.05f, m = 1.15f },
            new SwordDef { id = "w_plague", price = 600, name = "ดาบโรคระบาด", desc = "พิษกัดกร่อนและทำให้ศัตรูช้าลง", color = C(0x9cff5a), dmg = 1, spd = .95f, len = 1.08f, m = 1.2f, burn = "poison", slow = true },
            new SwordDef { id = "w_fiery", price = 900, name = "ดาบเปลวสุริยะ", desc = "ใบดาบเพลิง แรงและเผาไหม้", color = C(0xffb030), dmg = 1.15f, spd = .95f, len = 1.1f, m = 1.2f, burn = "fire" },
            new SwordDef { id = "w_daggers", price = 500, type = "dagger", name = "มีดสั้นประดับ", desc = "สั้นแต่ไวมาก ติดคริติคอลบ่อย", color = C(0x5fb4ff), dmg = .78f, spd = 1.5f, len = .75f, m = .62f, crit = .3f },
            new SwordDef { id = "w_winged", price = 800, name = "ดาบปีกโลหิต", desc = "โอกาสคริติคอลสูง", color = C(0xff3355), dmg = 1.05f, spd = 1.05f, len = 1, m = 1.1f, crit = .28f },
            new SwordDef { id = "w_gold1", price = 1000, name = "ดาบทองแห่งแสง", desc = "ทุกการฟันดูดเลือดคืน 6%", color = C(0xffe08a), dmg = 1.05f, spd = 1, len = 1.08f, m = 1.2f, steal = .06f },
            new SwordDef { id = "w_gold2", price = 1200, name = "มหาดาบอัศวิน", desc = "หนักและยาว แรงที่สุด แต่ฟันช้า", color = C(0xcfe8ff), dmg = 1.4f, spd = .8f, len = 1.15f, m = 1.25f },
        };

        public static readonly PetDef[] Pets =
        {
            new PetDef { id = "candle", name = "แคนเดิล", desc = "ยิงลูกไฟใส่ศัตรูใกล้สุด", kind = "bolt", color = C(0xff8a2a), cd = 1.4f, dmg = 70, h = .6f },
            new PetDef { id = "bat", name = "แบทบี้", desc = "บินกัดศัตรู ดูดเลือดคืนให้เจ้านาย", kind = "melee", color = C(0xff5f8a), cd = 1.6f, dmg = 80, h = .5f, fly = true, steal = .5f },
            new PetDef { id = "mushroom", name = "เห็ดหมอผี", desc = "ฟื้นพลังชีวิตให้เจ้านายทุก 4 วินาที", kind = "heal", color = C(0x6cff8f), cd = 4, dmg = 0, h = .65f },
            new PetDef { id = "scibot", name = "บิทบอท", desc = "ยิงเลเซอร์แม่นยำระยะไกล", kind = "beam", color = C(0xff3a3a), cd = 1.1f, dmg = 60, h = .55f, fly = true },
            new PetDef { id = "rat", name = "ฟันเฟือง", desc = "กัดศัตรู และดูดเหรียญได้ไกลขึ้น ทอง +50%", kind = "melee", color = C(0xffc65a), cd = 1.3f, dmg = 65, h = .55f, gold = true },
            new PetDef { id = "spider", name = "ไอรอนสไปเดอร์", desc = "พ่นใยทำให้ศัตรูรอบเป้าช้าลง", kind = "web", color = C(0xd0f4ff), cd = 2.6f, dmg = 55, h = .55f },
            new PetDef { id = "wick", name = "วิคคีปเปอร์", desc = "โยนลูกไฟใหญ่ ระเบิดเป็นวง", kind = "fireball", color = C(0xffa040), cd = 3.2f, dmg = 150, h = .7f },
            new PetDef { id = "cat", name = "เหมียวแม่มด", desc = "ปล่อยเวทติดตามเป้า 3 ลูก", kind = "homing", color = C(0xb06bff), cd = 2.4f, dmg = 45, h = .62f },
        };

        public static readonly SkillDef[] Skills =
        {
            new SkillDef { id = "whirl", n = "พายุดาบ", cd = 7, c = C(0xff9a3c), icon = "⟲" }, new SkillDef { id = "slam", n = "ทุบปฐพี", cd = 9, c = C(0xffc65a), icon = "⤓" },
            new SkillDef { id = "cry", n = "คำรามศึก", cd = 14, c = C(0xff4d5e), icon = "◉" }, new SkillDef { id = "iaido", n = "อิไอโดะ", cd = 5, c = C(0xff5fb4), icon = "⟋" },
            new SkillDef { id = "petal", n = "พายุซากุระ", cd = 8, c = C(0xff8ac8), icon = "✿" }, new SkillDef { id = "dance", n = "ระบำดาบ", cd = 12, c = C(0xff5f8a), icon = "✕" },
            new SkillDef { id = "feather", n = "ห่าขนนก", cd = 4, c = C(0xd8f0ff), icon = "➶" }, new SkillDef { id = "dive", n = "ดิ่งฟ้า", cd = 9, c = C(0x9fe8ff), icon = "▼" },
            new SkillDef { id = "gale", n = "พายุปีก", cd = 11, c = C(0x7fd8ff), icon = "≋" }, new SkillDef { id = "laser", n = "เลเซอร์", cd = 6, c = C(0xff3a3a), icon = "━" },
            new SkillDef { id = "missile", n = "มิสไซล์", cd = 9, c = C(0xffc65a), icon = "➹" }, new SkillDef { id = "shield", n = "บาเรีย", cd = 15, c = C(0x5ff6ff), icon = "⬡" },
            new SkillDef { id = "heal", n = "แสงเยียวยา", cd = 12, c = C(0xffe08a), icon = "✚" },
        };
        public static SkillDef Skill(string id) => System.Array.Find(Skills, s => s.id == id);
        // hero classes in the lobby filter order: id, Thai, English, accent colour
        public static readonly (string id, string th, string en, Color c)[] Classes =
        {
            ("all", "ทั้งหมด", "All", C(0xffe6a8)), ("warrior", "นักรบ", "Warrior", C(0xff8a2a)), ("assassin", "นักฆ่า", "Assassin", C(0xb86bff)),
            ("mage", "จอมเวท", "Mage", C(0x4fb4ff)), ("archer", "นักธนู", "Archer", C(0xffc24a)), ("cleric", "นักบวช", "Cleric", C(0x4fe89a)), ("monk", "สายหมัด", "Fighter", C(0xff4d5a)),
        };
        public static (string id, string th, string en, Color c) Class(string id) => System.Array.Find(Classes, k => k.id == id) is var k && k.id != null ? k : Classes[1];
        // heroes that share a skill's mechanics still show it as their own: name and colour per hero (icons: Resources/UI/Skills/skill_<hero>_<skill>.png)
        static readonly System.Collections.Generic.Dictionary<string, (string n, Color c)> HeroSkills = new()
        {
            ["spartan:whirl"] = ("โล่หมุนพายุ", C(0xffa63d)),
            ["spartan:slam"] = ("โล่ทุบปฐพี", C(0xffd36b)),
            ["spartan:cry"] = ("ปลุกใจสปาร์ตา", C(0xff5a45)),
            ["elf:feather"] = ("ห่าศรพงไพร", C(0x7ee86a)),
            ["elf:dive"] = ("กระโจนเหยี่ยว", C(0x5fd8ff)),
            ["elf:gale"] = ("พายุใบไม้", C(0x3fe0a0)),
            ["ninja:iaido"] = ("ฟันเงาพริบตา", C(0xff4d6d)),
            ["ninja:petal"] = ("ดาวกระจาย", C(0xff8a3d)),
            ["ninja:dance"] = ("ระบำเงา", C(0xb36bff)),
            ["hood:dance"] = ("ล่าในเงามืด", C(0x7a7bff)),
            ["hood:iaido"] = ("แทงทะลุหลัง", C(0x3ddc97)),
            ["hood:petal"] = ("พายุใบมีด", C(0x9be15d)),
            ["sunpriest:heal"] = ("แสงเยียวยา", C(0xffd75e)),
            ["sunpriest:shield"] = ("โล่สุริยะ", C(0xffa94d)),
            ["sunpriest:cry"] = ("พรแห่งรุ่งอรุณ", C(0xff7eb3)),
            ["monk:slam"] = ("ฝ่ามือทุบภูผา", C(0xffb13d)),
            ["monk:whirl"] = ("พายุหมัด", C(0xff7043)),
            ["monk:cry"] = ("ลมปราณ", C(0x4fdcc0)),
            ["mage:laser"] = ("ลำแสงน้ำแข็ง", C(0x5fd2ff)),
            ["mage:missile"] = ("ดาวตกเวท", C(0x8c8cff)),
            ["mage:shield"] = ("ม่านเวทคราม", C(0x45e6ff)),
            ["darkmage:missile"] = ("วิญญาณไล่ล่า", C(0xc06bff)),
            ["darkmage:laser"] = ("ลำแสงราตรี", C(0xff4dd2)),
            ["darkmage:shield"] = ("ม่านเงา", C(0x8a7bff)),
            ["sorceress:laser"] = ("ธารวารี", C(0x3db8ff)),
            ["sorceress:shield"] = ("ฟองน้ำคุ้มกัน", C(0x5ff0e0)),
            ["sorceress:missile"] = ("หยดน้ำไล่ล่า", C(0x6b9bff)),
        };
        public static (string n, Color c) SkillLook(string hero, string skill)
        { if (HeroSkills.TryGetValue(hero + ":" + skill, out var v)) return v; var d = Skill(skill); return (d.n, d.c); }
        // basic-attack button icon by class
        public static string AttackIcon(HeroDef h) => h.cls switch { "archer" => "bow", "mage" => "magic", "cleric" => "holy", "monk" => "fist", _ => "sword" };

        public static readonly FoeDef[] Foes =
        {
            new FoeDef { id = "rat", model = "rat", name = "หนูจักรกล", hp = 300, spd = 3.3f, dmg = 45, range = 1.5f, kind = "melee", scale = 1.55f, rad = .55f, gold = 6 },
            new FoeDef { id = "spider", model = "spider", name = "แมงมุมเหล็ก", hp = 260, spd = 4.1f, dmg = 40, range = 1.6f, kind = "melee", scale = 1.6f, rad = .6f, gold = 6 },
            new FoeDef { id = "mushroom", model = "mushroom", name = "เห็ดพิษ", hp = 620, spd = 2.0f, dmg = 75, range = 2.6f, kind = "slam", scale = 1.6f, rad = .6f, gold = 10 },
            new FoeDef { id = "candle", model = "candle", name = "เทียนอาถรรพ์", hp = 280, spd = 2.3f, dmg = 55, range = 9, kind = "ranged", scale = 1.5f, rad = .5f, gold = 8 },
            new FoeDef { id = "bat", model = "bat", name = "ค้างคาวปีศาจ", hp = 170, spd = 4.8f, dmg = 30, range = 1.4f, kind = "melee", scale = 1.6f, rad = .5f, gold = 4, fly = true },
            // animated Quaternius characters: scale = height in metres
            new FoeDef { id = "goblin", model = "q_goblin", name = "ก็อบลิน", hp = 240, spd = 3.8f, dmg = 38, range = 1.5f, kind = "melee", scale = 1.15f, rad = .45f, gold = 5 },
            new FoeDef { id = "orc", model = "q_orc", name = "ออร์ก", hp = 520, spd = 2.8f, dmg = 65, range = 1.8f, kind = "melee", scale = 1.9f, rad = .6f, gold = 9 },
            new FoeDef { id = "skeleton", model = "q_skeleton", name = "โครงกระดูก", hp = 320, spd = 3.2f, dmg = 50, range = 1.7f, kind = "melee", scale = 1.8f, rad = .5f, gold = 7 },
            new FoeDef { id = "zombie", model = "q_zombie", name = "ซอมบี้", hp = 420, spd = 2.2f, dmg = 55, range = 1.6f, kind = "melee", scale = 1.8f, rad = .5f, gold = 7 },
            new FoeDef { id = "giant", model = "q_giant", name = "ยักษ์", hp = 1400, spd = 1.8f, dmg = 110, range = 2.8f, kind = "slam", scale = 3.0f, rad = 1.0f, gold = 20 },
            new FoeDef { id = "demon", model = "q_blue_demon", name = "อสูรน้ำเงิน", hp = 600, spd = 3.0f, dmg = 70, range = 1.9f, kind = "melee", scale = 2.1f, rad = .6f, gold = 12 },
            new FoeDef { id = "ghost", model = "q_ghost", name = "วิญญาณ", hp = 260, spd = 3.6f, dmg = 40, range = 1.6f, kind = "melee", scale = 1.6f, rad = .5f, gold = 6, fly = true },
        };
        public static FoeDef Foe(string id) => System.Array.Find(Foes, f => f.id == id);

        public static readonly ThemeDef River = new ThemeDef { id = "river", name = "หุบธารจันทรา", desc = "ธารน้ำใสคดเคี้ยวกลางหุบเขา แสงอาทิตย์ยามเย็น",
            waves = new[] { new[] { ("goblin", 4), ("rat", 3) }, new[] { ("orc", 2), ("goblin", 3), ("candle", 3) }, new[] { ("orc", 3), ("spider", 3), ("goblin", 3), ("mushroom", 1) } },
            boss = new BossDef { model = "q_dragon", name = "มังกรวารี", scale = 4.2f, hp = 9500, tint = Color.white, glow = new Color(.02f, .1f, .16f), rage = new Color(.05f, .3f, .5f), color = new Color(.4f, .85f, 1f), summon = "goblin", fly = true } };
        // stages: the forest, the river valley, the town square at dusk, the dark castle (kept small on purpose; old grave/river/courtyard builders are retired)
        public static readonly ThemeDef[] Themes =
        {
            new ThemeDef { id = "forest", name = "ป่าแสงจันทร์", desc = "ทุ่งหญ้ากลางป่าลึก แสงเห็ดเรืองยามค่ำ",
                waves = new[] { new[] { ("rat", 4), ("goblin", 3) }, new[] { ("goblin", 3), ("mushroom", 2), ("candle", 3) }, new[] { ("orc", 3), ("spider", 3), ("mushroom", 2), ("goblin", 3) } },
                boss = new BossDef { model = "mushroom", name = "ราชาเห็ดพิษ", scale = 3.4f, hp = 8000, tint = C(0xe0ffd0), glow = G(0x2a6a10, .3f), rage = G(0x6a8a00, .5f), color = C(0x9cff5a), summon = "goblin" } },
            River,
            new ThemeDef { id = "kingdom", name = "จัตุรัสอาณาจักร", desc = "ลานกลางเมืองแฟนตาซี บ้านหลังคาส้ม ใบไม้เปลี่ยนสี",
                waves = new[] { new[] { ("goblin", 4), ("rat", 3) }, new[] { ("orc", 3), ("goblin", 3), ("spider", 2) }, new[] { ("orc", 3), ("giant", 1), ("goblin", 4), ("candle", 2) } },
                boss = new BossDef { model = "reaper", name = "ยมทูตยามสนธยา", scale = 1, fly = true, hp = 10500, tint = C(0xe8e0d8), glow = G(0x3a1a08, .3f), rage = G(0x8a2a08, .5f), color = C(0xffa040), summon = "orc" } },
            new ThemeDef { id = "valley", name = "หุบเขาเขียวขจี", desc = "ทุ่งหญ้าเขียวสด ลำธารคดเคี้ยว สะพานไม้ และป่าสน",
                waves = new[] { new[] { ("rat", 4), ("goblin", 3) }, new[] { ("goblin", 3), ("mushroom", 3), ("spider", 2) }, new[] { ("orc", 3), ("mushroom", 2), ("goblin", 4), ("candle", 2) } },
                boss = new BossDef { model = "q_dragon", name = "มังกรพงไพร", scale = 4.2f, fly = true, hp = 10000, tint = C(0xd8ffc8), glow = G(0x1a5a10, .3f), rage = G(0x4a9a10, .5f), color = C(0x7aff5a), summon = "goblin" } },
            new ThemeDef { id = "castle", name = "ปราสาทร้าง", desc = "ห้องโถงหินมืด แสงคบเพลิงริบหรี่",
                waves = new[] { new[] { ("skeleton", 4), ("spider", 3) }, new[] { ("skeleton", 3), ("candle", 3), ("bat", 3), ("demon", 1) }, new[] { ("demon", 2), ("giant", 1), ("skeleton", 4), ("bat", 3) } },
                boss = new BossDef { model = "captain", name = "กัปตันกะโหลก", scale = 1, hp = 8500, tint = C(0xf0e8e0), glow = G(0x5a0a08, .3f), rage = G(0xa01010, .5f), color = C(0xff5a3a), summon = "skeleton" } },
        };
        public static int ThemeOf(int stage) => (stage - 1) % Themes.Length;
        public const float ArenaR = 22.5f;
    }
}
