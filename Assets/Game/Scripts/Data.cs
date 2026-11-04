using UnityEngine;

namespace NovaBlade
{
    // Game data (ported from the web prototype arena/index.html).
    public class HeroDef
    {
        public string id, name, title, desc, cls = "warrior"; public float hp, spd, atk; public bool rig, ranged, blade = true, ownWeapon; public string[] skills;
        public (string n, Color tint, Color glow)[] skins; public float[] stats;
    }
    public class SwordDef { public string id, name, desc, type = "sword"; public Color color; public float dmg, spd, len, m, crit, steal; public string burn; public bool slow; public int price; }
    public class WingDef { public string id, name, desc; public Color glow; public int gems; }
    public class PetDef { public string id, name, desc, kind; public Color color; public float cd, dmg, h, steal; public bool fly, gold; }
    public class SkillDef { public string id, n; public float cd; public Color c; public string icon; }
    public class FoeDef { public string id, model, name, kind; public float hp, spd, dmg, range, scale, rad; public int gold; public bool fly; }
    public class BossDef { public string model, name, summon; public float scale, hp; public Color tint, glow, rage, color; public bool fly; }
    public class ThemeDef { public string id, name, desc; public (string foe, int n)[][] waves; public BossDef boss; }

    public static class Data
    {
        // per-class movement and attack clips (AnimatorOverrideController in Resources/Anim/<set>); null = the shared Great Sword set
        public static string AnimSet(string heroId) => heroId switch { "sorceress" or "mage" or "darkmage" or "sunpriest" => "mage", "elf" or "hood" => "bow", "ninja" => "ninja", "monk" => "monk", "warrior" => "warrior", "spartan" => "shield", "samurai" => "samurai", _ => null };
        static Color C(int hex) => new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);
        static Color G(int hex, float k) => C(hex) * k;

        public static readonly HeroDef[] Heroes =
        {
            new HeroDef { id = "warrior", name = Loc.S("วัลก้า", "Valka"), title = Loc.S("นักรบเถื่อนแห่งขุนเขา", "Mountain Barbarian"), desc = Loc.S("สายบู๊ เลือดหนา ปั่นดาบเป็นพายุกลางฝูงศัตรู", "Brawler: lots of health, spins a blade storm through crowds"), hp = 1300, spd = 5.0f, atk = 1.15f, rig = true,
                skills = new[] { "whirl", "slam", "cry" }, stats = new[] { .9f, .8f, .55f, .5f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("เลือดมังกร", "Dragon Blood"), C(0xffb4a4), G(0xff3a1a, .25f)), (Loc.S("เงาราตรี", "Night Shade"), C(0x9d97c4), G(0x8a4dff, .25f)), (Loc.S("นักรบทองคำ", "Golden Warrior"), C(0xffe2a2), G(0xffb52e, .2f)) } },
            new HeroDef { id = "samurai", cls = "assassin", name = Loc.S("ซากุระ", "Sakura"), title = Loc.S("ซามูไรกลีบซากุระ", "Cherry-Blossom Samurai"), desc = Loc.S("ว่องไวที่สุด พุ่งฟันทะลุศัตรู ระบำดาบไล่ฟันทีละตัว", "The fastest: dashes through foes and dances from target to target"), hp = 1000, spd = 5.8f, atk = 1.05f, rig = true,
                skills = new[] { "iaido", "petal", "dance" }, stats = new[] { .55f, .75f, .95f, .6f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ซากุระราตรี", "Night Sakura"), C(0xffc8e4), G(0xff5fb4, .22f)), (Loc.S("หิมะขาว", "White Snow"), C(0xdff2ff), G(0x7fd8ff, .2f)), (Loc.S("อสูรเพลิง", "Fire Demon"), C(0xffa080), G(0xff4a1a, .25f)) } },
            new HeroDef { id = "harpy", cls = "archer", ranged = true, name = Loc.S("แอริส", "Aeris"), title = Loc.S("ฮาร์ปี้นักรบแห่งฟากฟ้า", "Sky Harpy Warrior"), desc = Loc.S("ยิงขนนกระยะไกล ดิ่งฟ้ากระแทก และพายุปีกดูดศัตรู", "Long-range feathers, a crushing dive and a gale that pulls foes in"), hp = 950, spd = 5.4f, atk = 1.0f, rig = true,
                skills = new[] { "feather", "dive", "gale" }, stats = new[] { .5f, .7f, .8f, .95f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ปีกอัคคี", "Fire Wings"), C(0xffc29a), G(0xff6a1a, .22f)), (Loc.S("ปีกพายุ", "Storm Wings"), C(0xb8e6ff), G(0x3fb8ff, .22f)), (Loc.S("ปีกมรณะ", "Death Wings"), C(0x8e86a8), G(0xb04dff, .25f)) } },
            new HeroDef { id = "spartan", ownWeapon = true, name = Loc.S("ไลร่า", "Lyra"), title = Loc.S("นักรบโล่แห่งหุบเขา", "Shieldmaiden of the Vale"), desc = Loc.S("สายนักรบ ถือโล่หนัก ทนทาน ปั่นดาบกลางฝูงศัตรู", "Warrior: heavy shield, tough, spins through crowds"), cls = "warrior", hp = 1350, spd = 4.9f, atk = 1.15f, rig = true,
                skills = new[] { "whirl", "slam", "cry" }, stats = new[] { 0.95f, 0.8f, 0.5f, 0.5f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ทองสัมฤทธิ์", "Bronze"), C(0xffe2a2), G(0xffb52e, .22f)), (Loc.S("เงาราตรี", "Night Shade"), C(0x9d97c4), G(0x5a4dff, .22f)), (Loc.S("เลือดศึก", "War Blood"), C(0xffb4a4), G(0xff3a1a, .22f)) } },
            new HeroDef { id = "elf", name = Loc.S("เอลวีน", "Elwyn"), title = Loc.S("พรานธนูแห่งพงไพร", "Forest Ranger"), desc = Loc.S("สายธนู ยิงไกล ปล่อยห่าลูกศร แล้วกระโดดถอยตั้งหลัก", "Archer: long range, arrow volleys, leaps back to reposition"), cls = "archer", hp = 900, spd = 5.5f, atk = 1.05f, rig = true, ranged = true, blade = false,
                skills = new[] { "feather", "dive", "gale" }, stats = new[] { 0.5f, 0.75f, 0.8f, 1f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ใบไม้ผลิ", "Spring Leaf"), C(0xc8ffc0), G(0x5fff7a, .22f)), (Loc.S("หิมะเงิน", "Silver Snow"), C(0xdff2ff), G(0x7fd8ff, .22f)), (Loc.S("ใบไม้ร่วง", "Autumn Leaf"), C(0xffc89a), G(0xff7a1a, .22f)) } },
            new HeroDef { id = "ninja", ownWeapon = true, name = Loc.S("คาเงะ", "Kage"), title = Loc.S("นินจาเงาราตรี", "Night Shadow Ninja"), desc = Loc.S("สายนักฆ่า พุ่งฟันทะลุศัตรู เร็วที่สุดในบรรดาฮีโร่", "Assassin: dashes through foes, the fastest hero"), cls = "assassin", hp = 950, spd = 6.0f, atk = 1.1f, rig = true,
                skills = new[] { "iaido", "petal", "dance" }, stats = new[] { 0.5f, 0.85f, 1f, 0.55f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("เงาแดง", "Crimson Shade"), C(0xffb0b0), G(0xff3a3a, .22f)), (Loc.S("เงาคราม", "Indigo Shade"), C(0xb0c8ff), G(0x3a6aff, .22f)), (Loc.S("เงาทอง", "Golden Shade"), C(0xffe8a8), G(0xffc02e, .22f)) } },
            new HeroDef { id = "hood", ownWeapon = true, name = Loc.S("เรเวน", "Raven"), title = Loc.S("มือสังหารใต้ผ้าคลุม", "Hooded Killer"), desc = Loc.S("สายนักฆ่า ไล่ฟันทีละเป้าแล้วหายไปในเงา", "Assassin: strikes one target after another, then vanishes into shadow"), cls = "assassin", hp = 1000, spd = 5.8f, atk = 1.1f, rig = true,
                skills = new[] { "dance", "iaido", "petal" }, stats = new[] { 0.55f, 0.85f, 0.95f, 0.55f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("เขม่าเถ้า", "Ash"), C(0xc8c8c8), G(0x8a8a8a, .22f)), (Loc.S("พิษมรกต", "Emerald Venom"), C(0xb0ffc8), G(0x2eff8a, .22f)), (Loc.S("โลหิต", "Blood"), C(0xffa0a0), G(0xff2a3a, .22f)) } },
            new HeroDef { id = "sunpriest", name = Loc.S("โซเลีย", "Solia"), title = Loc.S("นักบวชแห่งสุริยะ", "Priestess of the Sun"), desc = Loc.S("สายนักบวช ยิงแสงศักดิ์สิทธิ์ ฟื้นพลังชีวิต และกางบาเรีย", "Cleric: holy light, healing and barriers"), cls = "cleric", hp = 1050, spd = 5.0f, atk = 0.95f, rig = true, ranged = true, blade = false,
                skills = new[] { "heal", "shield", "cry" }, stats = new[] { 0.7f, 0.6f, 0.6f, 0.85f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("รุ่งอรุณ", "Dawn"), C(0xffd8b0), G(0xff9a3a, .22f)), (Loc.S("จันทรา", "Moonlight"), C(0xd8e0ff), G(0x8aa8ff, .22f)), (Loc.S("สุริยคราส", "Eclipse"), C(0x9a8a7a), G(0xff5a1a, .22f)) } },
            new HeroDef { id = "monk", name = Loc.S("หมิง", "Ming"), title = Loc.S("จอมยุทธ์หมัดเพลิงทอง", "Golden Flame Fist"), desc = Loc.S("สายหมัด เตะลอยฟ้า บู๊มือเปล่า ทุบพื้น ปั่นตัวเป็นพายุหมัด", "Fighter: flying kicks, bare-handed brawling, ground slams and fist storms"), cls = "monk", hp = 1250, spd = 5.3f, atk = 1.1f, rig = true, blade = false,
                skills = new[] { "slam", "whirl", "cry" }, stats = new[] { 0.85f, 0.8f, 0.7f, 0.5f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("จีวรแดง", "Crimson Robe"), C(0xffb0a0), G(0xff4a2a, .22f)), (Loc.S("ภูเขาหิมะ", "Snow Peak"), C(0xe0f0ff), G(0x8ad8ff, .22f)), (Loc.S("มังกรทอง", "Golden Dragon"), C(0xffe2a2), G(0xffb52e, .22f)) } },
            new HeroDef { id = "mage", name = Loc.S("อัลดริค", "Aldric"), title = Loc.S("จอมเวทผ้าคลุมคราม", "Azure-Cloak Archmage"), desc = Loc.S("สายเวท ยิงลำแสงเวท ปล่อยลูกไฟไล่ล่า และกางบาเรีย", "Mage: arcane beams, homing fireballs and a barrier"), cls = "mage", hp = 850, spd = 5.0f, atk = 1.15f, rig = true, ranged = true, blade = false,
                skills = new[] { "laser", "missile", "shield" }, stats = new[] { 0.45f, 0.95f, 0.6f, 0.95f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("น้ำแข็ง", "Frost"), C(0xd0f0ff), G(0x5fd8ff, .22f)), (Loc.S("อัคคี", "Inferno"), C(0xffc0a0), G(0xff5a1a, .22f)), (Loc.S("มรกต", "Emerald"), C(0xc0ffd8), G(0x3aff8a, .22f)) } },
            new HeroDef { id = "darkmage", name = Loc.S("ไนต์", "Night"), title = Loc.S("จอมเวทเสื้อคลุมดำ", "Black-Coat Sorcerer"), desc = Loc.S("สายเวท ระดมลูกไฟเวทไล่ล่า แล้วเผาด้วยลำแสง", "Mage: a barrage of homing spells, then a burning beam"), cls = "mage", hp = 880, spd = 5.1f, atk = 1.15f, rig = true, ranged = true, blade = false,
                skills = new[] { "missile", "laser", "shield" }, stats = new[] { 0.45f, 0.95f, 0.65f, 0.9f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ราตรีม่วง", "Violet Night"), C(0xc8b0ff), G(0x8a4dff, .22f)), (Loc.S("เถ้าเพลิง", "Ember Ash"), C(0xffb090), G(0xff4a1a, .22f)), (Loc.S("เงินจันทร์", "Moon Silver"), C(0xe8eeff), G(0x9ad0ff, .22f)) } },
            new HeroDef { id = "sorceress", name = Loc.S("เอเรีย", "Aeria"), title = Loc.S("แม่มดวารีสีคราม", "Azure Tide Witch"), desc = Loc.S("สายเวท คล่องตัว กางบาเรียแล้วยิงลำแสงเวท", "Mage: nimble, raises a barrier and fires arcane beams"), cls = "mage", hp = 820, spd = 5.4f, atk = 1.1f, rig = true, ranged = true, blade = false,
                skills = new[] { "laser", "shield", "missile" }, stats = new[] { 0.4f, 0.9f, 0.75f, 0.9f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ทะเลลึก", "Deep Sea"), C(0xb0d0ff), G(0x3a8aff, .22f)), (Loc.S("ดอกซากุระ", "Cherry Blossom"), C(0xffc8e4), G(0xff5fb4, .22f)), (Loc.S("ทองสุริยา", "Sun Gold"), C(0xffe8b0), G(0xffb52e, .22f)) } },
            new HeroDef { id = "toybot", name = Loc.S("โบลต์", "Bolt"), title = Loc.S("หุ่นจิ๋วพลังไฟฟ้า", "Tiny Volt Robot"), desc = Loc.S("ลอยตัว ยิงเลเซอร์ มิสไซล์ไล่ล่า และบาเรียกันดาเมจ", "Hovers, fires lasers and homing missiles, and raises a damage barrier"), hp = 1100, spd = 5.2f, atk = 1.0f, rig = false,
                skills = new[] { "laser", "missile", "shield" }, stats = new[] { .7f, .7f, .65f, .85f },
                skins = new[] { (Loc.S("ดั้งเดิม", "Original"), Color.white, Color.black), (Loc.S("ไซเบอร์ชมพู", "Cyber Pink"), C(0xffc6ea), G(0xff4fd8, .25f)), (Loc.S("ทหารพราน", "Ranger"), C(0xb9d29a), G(0x8aff4f, .2f)), (Loc.S("โครเมียมดำ", "Black Chrome"), C(0x6c7380), G(0x5ff6ff, .25f)) } },
        };

        public static readonly SwordDef[] Swords =
        {
            new SwordDef { id = "w_lava", price = 0, name = Loc.S("ดาบลาวาอัคคี", "Lava Flame Sword"), desc = Loc.S("ฟันติดไฟ เผาศัตรูต่อเนื่อง", "Flaming slashes that keep burning foes"), color = C(0xff5a1a), dmg = 1.05f, spd = 1, len = 1, m = 1.1f, burn = "fire" },
            new SwordDef { id = "w_fantasy", price = 400, name = Loc.S("ดาบมนตร์ม่วง", "Violet Spellblade"), desc = Loc.S("สมดุล ฟันเร็วขึ้นเล็กน้อย", "Balanced, swings a little faster"), color = C(0xb06bff), dmg = 1.05f, spd = 1.08f, len = 1.05f, m = 1.15f },
            new SwordDef { id = "w_plague", price = 600, name = Loc.S("ดาบโรคระบาด", "Plague Sword"), desc = Loc.S("พิษกัดกร่อนและทำให้ศัตรูช้าลง", "Corrosive poison that slows foes"), color = C(0x9cff5a), dmg = 1, spd = .95f, len = 1.08f, m = 1.2f, burn = "poison", slow = true },
            new SwordDef { id = "w_fiery", price = 900, name = Loc.S("ดาบเปลวสุริยะ", "Solar Flame Sword"), desc = Loc.S("ใบดาบเพลิง แรงและเผาไหม้", "Flame blade: strong and burning"), color = C(0xffb030), dmg = 1.15f, spd = .95f, len = 1.1f, m = 1.2f, burn = "fire" },
            new SwordDef { id = "w_daggers", price = 500, type = "dagger", name = Loc.S("มีดสั้นประดับ", "Jeweled Daggers"), desc = Loc.S("สั้นแต่ไวมาก ติดคริติคอลบ่อย", "Short but very fast, frequent criticals"), color = C(0x5fb4ff), dmg = .78f, spd = 1.5f, len = .75f, m = .62f, crit = .3f },
            new SwordDef { id = "w_winged", price = 800, name = Loc.S("ดาบปีกโลหิต", "Bloodwing Sword"), desc = Loc.S("โอกาสคริติคอลสูง", "High critical chance"), color = C(0xff3355), dmg = 1.05f, spd = 1.05f, len = 1, m = 1.1f, crit = .28f },
            new SwordDef { id = "w_gold1", price = 1000, name = Loc.S("ดาบทองแห่งแสง", "Golden Light Blade"), desc = Loc.S("ทุกการฟันดูดเลือดคืน 6%", "Every hit heals you for 6%"), color = C(0xffe08a), dmg = 1.05f, spd = 1, len = 1.08f, m = 1.2f, steal = .06f },
            new SwordDef { id = "w_gold2", price = 1200, name = Loc.S("มหาดาบอัศวิน", "Knight's Greatsword"), desc = Loc.S("หนักและยาว แรงที่สุด แต่ฟันช้า", "Heavy and long: the strongest, but slow"), color = C(0xcfe8ff), dmg = 1.4f, spd = .8f, len = 1.15f, m = 1.25f },
        };

        // wings: looks only, worn by the hero (and the pet, if chosen); glow = emission, mote and bloom colour
        public static readonly WingDef[] Wings =
        {
            new WingDef { id = "angel", name = Loc.S("ปีกเทวา", "Angel Wings"), desc = Loc.S("ขนนกขาวเรืองแสงทอง", "White feathers with a golden glow"), glow = new Color(1, .9f, .65f), gems = 120 },
            new WingDef { id = "fire", name = Loc.S("ปีกอัคคี", "Fire Wings"), desc = Loc.S("ขนเพลิงฟีนิกซ์ ประกายไฟลอยขึ้น", "Phoenix flame feathers shedding embers"), glow = new Color(1, .45f, .12f), gems = 180 },
            new WingDef { id = "storm", name = Loc.S("ปีกพายุ", "Storm Wings"), desc = Loc.S("ขนสายฟ้า ประกายไฟฟ้าแตกกระจาย", "Lightning feathers crackling with sparks"), glow = new Color(.35f, .7f, 1), gems = 180 },
            new WingDef { id = "death", name = Loc.S("ปีกมรณะ", "Death Wings"), desc = Loc.S("ปีกค้างคาวปีศาจ หมอกม่วงลอยตาม", "Demon bat wings trailing violet mist"), glow = new Color(.65f, .25f, 1), gems = 250 },
        };
        public static WingDef Wing(string id) => string.IsNullOrEmpty(id) ? null : System.Array.Find(Wings, w => w.id == id);
        public static readonly PetDef[] Pets =
        {
            new PetDef { id = "candle", name = Loc.S("แคนเดิล", "Candle"), desc = Loc.S("ยิงลูกไฟใส่ศัตรูใกล้สุด", "Shoots fireballs at the nearest foe"), kind = "bolt", color = C(0xff8a2a), cd = 1.4f, dmg = 70, h = .6f },
            new PetDef { id = "bat", name = Loc.S("แบทบี้", "Batby"), desc = Loc.S("บินกัดศัตรู ดูดเลือดคืนให้เจ้านาย", "Flies in to bite foes and heals its master"), kind = "melee", color = C(0xff5f8a), cd = 1.6f, dmg = 80, h = .5f, fly = true, steal = .5f },
            new PetDef { id = "mushroom", name = Loc.S("เห็ดหมอผี", "Shaman Shroom"), desc = Loc.S("ฟื้นพลังชีวิตให้เจ้านายทุก 4 วินาที", "Heals its master every 4 seconds"), kind = "heal", color = C(0x6cff8f), cd = 4, dmg = 0, h = .65f },
            new PetDef { id = "scibot", name = Loc.S("บิทบอท", "Bitbot"), desc = Loc.S("ยิงเลเซอร์แม่นยำระยะไกล", "Fires a precise long-range laser"), kind = "beam", color = C(0xff3a3a), cd = 1.1f, dmg = 60, h = .55f, fly = true },
            new PetDef { id = "rat", name = Loc.S("ฟันเฟือง", "Cogwheel"), desc = Loc.S("กัดศัตรู และดูดเหรียญได้ไกลขึ้น ทอง +50%", "Bites foes and pulls coins from further away, +50% Gold"), kind = "melee", color = C(0xffc65a), cd = 1.3f, dmg = 65, h = .55f, gold = true },
            new PetDef { id = "spider", name = Loc.S("ไอรอนสไปเดอร์", "Iron Spider"), desc = Loc.S("พ่นใยทำให้ศัตรูรอบเป้าช้าลง", "Spits webs that slow foes around the target"), kind = "web", color = C(0xd0f4ff), cd = 2.6f, dmg = 55, h = .55f },
            new PetDef { id = "wick", name = Loc.S("วิคคีปเปอร์", "Wickkeeper"), desc = Loc.S("โยนลูกไฟใหญ่ ระเบิดเป็นวง", "Throws a big fireball that explodes in a ring"), kind = "fireball", color = C(0xffa040), cd = 3.2f, dmg = 150, h = .7f },
            new PetDef { id = "cat", name = Loc.S("เหมียวแม่มด", "Witch Cat"), desc = Loc.S("ปล่อยเวทติดตามเป้า 3 ลูก", "Casts 3 homing spells"), kind = "homing", color = C(0xb06bff), cd = 2.4f, dmg = 45, h = .62f },
        };

        public static readonly SkillDef[] Skills =
        {
            new SkillDef { id = "whirl", n = Loc.S("พายุดาบ", "Blade Storm"), cd = 7, c = C(0xff9a3c), icon = "⟲" }, new SkillDef { id = "slam", n = Loc.S("ทุบปฐพี", "Earth Slam"), cd = 9, c = C(0xffc65a), icon = "⤓" },
            new SkillDef { id = "cry", n = Loc.S("คำรามศึก", "War Cry"), cd = 14, c = C(0xff4d5e), icon = "◉" }, new SkillDef { id = "iaido", n = Loc.S("อิไอโดะ", "Iaido"), cd = 5, c = C(0xff5fb4), icon = "⟋" },
            new SkillDef { id = "petal", n = Loc.S("พายุซากุระ", "Sakura Storm"), cd = 8, c = C(0xff8ac8), icon = "✿" }, new SkillDef { id = "dance", n = Loc.S("ระบำดาบ", "Blade Dance"), cd = 12, c = C(0xff5f8a), icon = "✕" },
            new SkillDef { id = "feather", n = Loc.S("ห่าขนนก", "Feather Rain"), cd = 4, c = C(0xd8f0ff), icon = "➶" }, new SkillDef { id = "dive", n = Loc.S("ดิ่งฟ้า", "Sky Dive"), cd = 9, c = C(0x9fe8ff), icon = "▼" },
            new SkillDef { id = "gale", n = Loc.S("พายุปีก", "Wing Gale"), cd = 11, c = C(0x7fd8ff), icon = "≋" }, new SkillDef { id = "laser", n = Loc.S("เลเซอร์", "Laser"), cd = 6, c = C(0xff3a3a), icon = "━" },
            new SkillDef { id = "missile", n = Loc.S("มิสไซล์", "Missiles"), cd = 9, c = C(0xffc65a), icon = "➹" }, new SkillDef { id = "shield", n = Loc.S("บาเรีย", "Barrier"), cd = 15, c = C(0x5ff6ff), icon = "⬡" },
            new SkillDef { id = "heal", n = Loc.S("แสงเยียวยา", "Healing Light"), cd = 12, c = C(0xffe08a), icon = "✚" },
        };
        public static SkillDef Skill(string id) => System.Array.Find(Skills, s => s.id == id);
        // hero classes in the lobby filter order: id, Thai, English, accent colour
        public static readonly (string id, string th, string en, Color c)[] Classes =
        {
            ("all", Loc.S("ทั้งหมด", "All"), "All", C(0xffe6a8)), ("warrior", Loc.S("นักรบ", "Warrior"), "Warrior", C(0xff8a2a)), ("assassin", Loc.S("นักฆ่า", "Assassin"), "Assassin", C(0xb86bff)),
            ("mage", Loc.S("จอมเวท", "Mage"), "Mage", C(0x4fb4ff)), ("archer", Loc.S("นักธนู", "Archer"), "Archer", C(0xffc24a)), ("cleric", Loc.S("นักบวช", "Cleric"), "Cleric", C(0x4fe89a)), ("monk", Loc.S("สายหมัด", "Fighter"), "Fighter", C(0xff4d5a)),
        };
        public static (string id, string th, string en, Color c) Class(string id) => System.Array.Find(Classes, k => k.id == id) is var k && k.id != null ? k : Classes[1];
        // heroes that share a skill's mechanics still show it as their own: name and colour per hero (icons: Resources/UI/Skills/skill_<hero>_<skill>.png)
        static readonly System.Collections.Generic.Dictionary<string, (string n, Color c)> HeroSkills = new()
        {
            ["spartan:whirl"] = (Loc.S("โล่หมุนพายุ", "Shield Storm"), C(0xffa63d)),
            ["spartan:slam"] = (Loc.S("โล่ทุบปฐพี", "Shield Slam"), C(0xffd36b)),
            ["spartan:cry"] = (Loc.S("ปลุกใจสปาร์ตา", "Spartan Rally"), C(0xff5a45)),
            ["elf:feather"] = (Loc.S("ห่าศรพงไพร", "Forest Volley"), C(0x7ee86a)),
            ["elf:dive"] = (Loc.S("กระโจนเหยี่ยว", "Hawk Leap"), C(0x5fd8ff)),
            ["elf:gale"] = (Loc.S("พายุใบไม้", "Leaf Storm"), C(0x3fe0a0)),
            ["ninja:iaido"] = (Loc.S("ฟันเงาพริบตา", "Shadow Flash"), C(0xff4d6d)),
            ["ninja:petal"] = (Loc.S("ดาวกระจาย", "Shuriken Storm"), C(0xff8a3d)),
            ["ninja:dance"] = (Loc.S("ระบำเงา", "Shadow Dance"), C(0xb36bff)),
            ["hood:dance"] = (Loc.S("ล่าในเงามืด", "Hunt in Shadow"), C(0x7a7bff)),
            ["hood:iaido"] = (Loc.S("แทงทะลุหลัง", "Backstab"), C(0x3ddc97)),
            ["hood:petal"] = (Loc.S("พายุใบมีด", "Blade Tempest"), C(0x9be15d)),
            ["sunpriest:heal"] = (Loc.S("แสงเยียวยา", "Healing Light"), C(0xffd75e)),
            ["sunpriest:shield"] = (Loc.S("โล่สุริยะ", "Sun Shield"), C(0xffa94d)),
            ["sunpriest:cry"] = (Loc.S("พรแห่งรุ่งอรุณ", "Dawn Blessing"), C(0xff7eb3)),
            ["monk:slam"] = (Loc.S("ฝ่ามือทุบภูผา", "Mountain Palm"), C(0xffb13d)),
            ["monk:whirl"] = (Loc.S("พายุหมัด", "Fist Storm"), C(0xff7043)),
            ["monk:cry"] = (Loc.S("ลมปราณ", "Inner Qi"), C(0x4fdcc0)),
            ["mage:laser"] = (Loc.S("ลำแสงน้ำแข็ง", "Frost Beam"), C(0x5fd2ff)),
            ["mage:missile"] = (Loc.S("ดาวตกเวท", "Arcane Meteors"), C(0x8c8cff)),
            ["mage:shield"] = (Loc.S("ม่านเวทคราม", "Azure Ward"), C(0x45e6ff)),
            ["darkmage:missile"] = (Loc.S("วิญญาณไล่ล่า", "Hunting Souls"), C(0xc06bff)),
            ["darkmage:laser"] = (Loc.S("ลำแสงราตรี", "Night Beam"), C(0xff4dd2)),
            ["darkmage:shield"] = (Loc.S("ม่านเงา", "Shadow Veil"), C(0x8a7bff)),
            ["sorceress:laser"] = (Loc.S("ธารวารี", "Tide Stream"), C(0x3db8ff)),
            ["sorceress:shield"] = (Loc.S("ฟองน้ำคุ้มกัน", "Bubble Ward"), C(0x5ff0e0)),
            ["sorceress:missile"] = (Loc.S("หยดน้ำไล่ล่า", "Seeking Droplets"), C(0x6b9bff)),
        };
        public static (string n, Color c) SkillLook(string hero, string skill)
        { if (HeroSkills.TryGetValue(hero + ":" + skill, out var v)) return v; var d = Skill(skill); return (d.n, d.c); }
        // basic-attack button icon by class
        public static string AttackIcon(HeroDef h) => h.cls switch { "archer" => "bow", "mage" => "magic", "cleric" => "holy", "monk" => "fist", _ => "sword" };

        public static readonly FoeDef[] Foes =
        {
            new FoeDef { id = "rat", model = "rat", name = Loc.S("หนูจักรกล", "Clockwork Rat"), hp = 300, spd = 3.3f, dmg = 45, range = 1.5f, kind = "melee", scale = 1.55f, rad = .55f, gold = 6 },
            new FoeDef { id = "spider", model = "spider", name = Loc.S("แมงมุมเหล็ก", "Iron Spider"), hp = 260, spd = 4.1f, dmg = 40, range = 1.6f, kind = "melee", scale = 1.6f, rad = .6f, gold = 6 },
            new FoeDef { id = "mushroom", model = "mushroom", name = Loc.S("เห็ดพิษ", "Toxic Shroom"), hp = 620, spd = 2.0f, dmg = 75, range = 2.6f, kind = "slam", scale = 1.6f, rad = .6f, gold = 10 },
            new FoeDef { id = "candle", model = "candle", name = Loc.S("เทียนอาถรรพ์", "Cursed Candle"), hp = 280, spd = 2.3f, dmg = 55, range = 9, kind = "ranged", scale = 1.5f, rad = .5f, gold = 8 },
            new FoeDef { id = "bat", model = "bat", name = Loc.S("ค้างคาวปีศาจ", "Demon Bat"), hp = 170, spd = 4.8f, dmg = 30, range = 1.4f, kind = "melee", scale = 1.6f, rad = .5f, gold = 4, fly = true },
            // animated Quaternius characters: scale = height in metres
            new FoeDef { id = "goblin", model = "rig_e_goblin", name = Loc.S("ก็อบลิน", "Goblin"), hp = 240, spd = 3.8f, dmg = 38, range = 1.5f, kind = "melee", scale = .78f, rad = .45f, gold = 5 },
            new FoeDef { id = "orc", model = "rig_e_orc", name = Loc.S("ออร์ก", "Orc"), hp = 520, spd = 2.8f, dmg = 65, range = 1.8f, kind = "melee", scale = 1.25f, rad = .6f, gold = 9 },
            new FoeDef { id = "skeleton", model = "rig_e_skeleton", name = Loc.S("โครงกระดูก", "Skeleton"), hp = 320, spd = 3.2f, dmg = 50, range = 1.7f, kind = "melee", scale = 1.05f, rad = .5f, gold = 7 },
            new FoeDef { id = "zombie", model = "rig_e_zombie", name = Loc.S("ซอมบี้", "Zombie"), hp = 420, spd = 2.2f, dmg = 55, range = 1.6f, kind = "melee", scale = 1.0f, rad = .5f, gold = 7 },
            new FoeDef { id = "giant", model = "rig_e_ghoul", name = Loc.S("ยักษ์กูล", "Ghoul Giant"), hp = 1400, spd = 1.8f, dmg = 110, range = 2.8f, kind = "slam", scale = 1.75f, rad = 1.0f, gold = 20 },
            new FoeDef { id = "demon", model = "rig_e_cultist", name = Loc.S("สาวกลัทธิมืด", "Dark Cultist"), hp = 600, spd = 3.0f, dmg = 70, range = 1.9f, kind = "melee", scale = 1.05f, rad = .6f, gold = 12 },
            new FoeDef { id = "ghost", model = "q_ghost", name = Loc.S("วิญญาณ", "Ghost"), hp = 260, spd = 3.6f, dmg = 40, range = 1.6f, kind = "melee", scale = 1.6f, rad = .5f, gold = 6, fly = true },
        };
        public static FoeDef Foe(string id) => System.Array.Find(Foes, f => f.id == id);

        public static readonly ThemeDef River = new ThemeDef { id = "river", name = Loc.S("หุบธารจันทรา", "Moonstream Valley"), desc = Loc.S("ธารน้ำใสคดเคี้ยวกลางหุบเขา แสงอาทิตย์ยามเย็น", "A clear stream winding through the valley at sunset"),
            waves = new[] { new[] { ("goblin", 4), ("rat", 3) }, new[] { ("orc", 2), ("goblin", 3), ("candle", 3) }, new[] { ("orc", 3), ("spider", 3), ("goblin", 3), ("mushroom", 1) } },
            boss = new BossDef { model = "q_dragon", name = Loc.S("มังกรวารี", "Water Dragon"), scale = 4.2f, hp = 9500, tint = Color.white, glow = new Color(.02f, .1f, .16f), rage = new Color(.05f, .3f, .5f), color = new Color(.4f, .85f, 1f), summon = "goblin", fly = true } };
        // stages: the forest, the river valley, the town square at dusk, the dark castle (kept small on purpose; old grave/river/courtyard builders are retired)
        public static readonly ThemeDef[] Themes =
        {
            new ThemeDef { id = "forest", name = Loc.S("ป่าแสงจันทร์", "Moonlit Forest"), desc = Loc.S("ทุ่งหญ้ากลางป่าลึก แสงเห็ดเรืองยามค่ำ", "A meadow deep in the woods, glowing mushrooms at night"),
                waves = new[] { new[] { ("rat", 4), ("goblin", 3) }, new[] { ("goblin", 3), ("mushroom", 2), ("candle", 3) }, new[] { ("orc", 3), ("spider", 3), ("mushroom", 2), ("goblin", 3) } },
                boss = new BossDef { model = "rig_b_treant", name = Loc.S("ราชาพฤกษาเน่า", "Blighted Treant Lord"), scale = 2.3f, hp = 8000, tint = C(0xe0ffd0), glow = G(0x2a6a10, .3f), rage = G(0x6a8a00, .5f), color = C(0x9cff5a), summon = "goblin" } },
            River,
            new ThemeDef { id = "kingdom", name = Loc.S("จัตุรัสอาณาจักร", "Kingdom Square"), desc = Loc.S("ลานกลางเมืองแฟนตาซี บ้านหลังคาส้ม ใบไม้เปลี่ยนสี", "A fantasy town square at dusk, lanterns and orange roofs"),
                waves = new[] { new[] { ("goblin", 4), ("rat", 3) }, new[] { ("orc", 3), ("goblin", 3), ("spider", 2) }, new[] { ("orc", 3), ("giant", 1), ("goblin", 4), ("candle", 2) } },
                boss = new BossDef { model = "reaper", name = Loc.S("ยมทูตยามสนธยา", "Twilight Reaper"), scale = 1, fly = true, hp = 10500, tint = C(0xe8e0d8), glow = G(0x3a1a08, .3f), rage = G(0x8a2a08, .5f), color = C(0xffa040), summon = "orc" } },
            new ThemeDef { id = "valley", name = Loc.S("หุบเขาเขียวขจี", "Verdant Valley"), desc = Loc.S("ทุ่งหญ้าเขียวสด ลำธารคดเคี้ยว สะพานไม้ และป่าสน", "Lush meadow, a winding stream, wooden bridges and pines"),
                waves = new[] { new[] { ("rat", 4), ("goblin", 3) }, new[] { ("goblin", 3), ("mushroom", 3), ("spider", 2) }, new[] { ("orc", 3), ("mushroom", 2), ("goblin", 4), ("candle", 2) } },
                boss = new BossDef { model = "q_dragon", name = Loc.S("มังกรพงไพร", "Forest Dragon"), scale = 4.2f, fly = true, hp = 10000, tint = C(0xd8ffc8), glow = G(0x1a5a10, .3f), rage = G(0x4a9a10, .5f), color = C(0x7aff5a), summon = "goblin" } },
            new ThemeDef { id = "castle", name = Loc.S("ปราสาทร้าง", "Ruined Castle"), desc = Loc.S("ห้องโถงหินมืด แสงคบเพลิงริบหรี่", "Dark stone halls in flickering torchlight"),
                waves = new[] { new[] { ("skeleton", 4), ("spider", 3) }, new[] { ("skeleton", 3), ("candle", 3), ("bat", 3), ("demon", 1) }, new[] { ("demon", 2), ("giant", 1), ("skeleton", 4), ("bat", 3) } },
                boss = new BossDef { model = "rig_b_lich", name = Loc.S("ราชาลิช", "Lich King"), scale = 2.0f, hp = 8500, tint = C(0xf0e8e0), glow = G(0x5a0a08, .3f), rage = G(0xa01010, .5f), color = C(0xff5a3a), summon = "skeleton" } },
        };
        public static int ThemeOf(int stage) => (stage - 1) % Themes.Length;
        public const float ArenaR = 22.5f;
    }
}
