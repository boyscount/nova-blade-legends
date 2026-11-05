using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    public enum Slot { Helm, Armor, Boots, Accessory, Potion, Material }

    // A piece of gear, a potion or a crafting material. Gear comes in five rarities (one set per rarity) and adds to the
    // hero's stats while worn; potions heal in a fight; materials come from dismantling gear and pay for enhancing it.
// An enhanced piece keeps its level in its id ("helm_iron+3"), so old saves and the bag's plain id lists keep working.
    public class ItemDef
    {
        public string id, name, desc; public Slot slot; public int rarity;
        public float hp, def, spd, crit, atk;   // hp: flat; def: share of damage taken away; spd: m/s; crit, atk: fractions
        public int Sell => slot == Slot.Potion ? 15 : slot == Slot.Material ? 5 * (rarity + 1) : 12 * (rarity + 1) * (rarity + 1);
    }

    public struct GearBonus { public float hp, def, spd, crit, atk; }
    public enum EnhanceResult { Max, NoShards, NoGold, Failed, Success }

    public static class Items
    {
        public static readonly string[] RarityTh = { "ธรรมดา", "ดี", "หายาก", "มหากาพย์", "ตำนาน" }, RarityEn = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
        public static string RarityName(int r) => Loc.S(RarityTh[r], RarityEn[r]);
        public static Color RarityColor(int r) => r switch { 0 => new Color(.72f, .74f, .76f), 1 => new Color(.38f, .9f, .42f), 2 => new Color(.32f, .62f, 1), 3 => new Color(.72f, .42f, 1), _ => new Color(1, .62f, .18f) };
        public static string SlotName(Slot s) => s switch
        {
            Slot.Helm => Loc.S("หมวก", "Helm"), Slot.Armor => Loc.S("เกราะ", "Armor"), Slot.Boots => Loc.S("รองเท้า", "Boots"),
            Slot.Accessory => Loc.S("เครื่องประดับ", "Accessory"), Slot.Potion => Loc.S("ยา", "Potion"), _ => Loc.S("วัตถุดิบ", "Material"),
        };

        public static readonly List<ItemDef> All = Build();
        static List<ItemDef> Build()
        {
            var sets = new[] { ("leather", "หนัง", "Leather"), ("iron", "เหล็ก", "Iron"), ("knight", "อัศวิน", "Knight's"), ("shadow", "เงาราตรี", "Shadow"), ("dragon", "มังกร", "Dragon") };
            var list = new List<ItemDef>();
            for (int r = 0; r < 5; r++)
            {
                var (sid, th, en) = sets[r]; float k = r + 1;
                list.Add(new ItemDef { id = $"helm_{sid}", slot = Slot.Helm, rarity = r, name = Loc.S($"หมวก{th}", $"{en} Helm"), desc = Loc.S("ปกป้องศีรษะ เพิ่มพลังชีวิต", "Guards the head: more health"), hp = 40 * k, def = .015f * k });
                list.Add(new ItemDef { id = $"armor_{sid}", slot = Slot.Armor, rarity = r, name = Loc.S($"เกราะ{th}", $"{en} Armor"), desc = Loc.S("เกราะลำตัว ลดดาเมจที่โดน", "Body armour: takes the edge off every hit"), hp = 80 * k, def = .025f * k });
                list.Add(new ItemDef { id = $"boots_{sid}", slot = Slot.Boots, rarity = r, name = Loc.S($"รองเท้า{th}", $"{en} Boots"), desc = Loc.S("เดินเร็วขึ้น", "Lighter on your feet"), spd = .12f * k, def = .008f * k });
                list.Add(new ItemDef { id = $"ring_{sid}", slot = Slot.Accessory, rarity = r, name = Loc.S($"แหวน{th}", $"{en} Ring"), desc = Loc.S("เพิ่มพลังโจมตีและคริติคอล", "More attack and critical chance"), crit = .015f * k, atk = .03f * k });
                list.Add(new ItemDef { id = $"shard_{r}", slot = Slot.Material, rarity = r, name = Loc.S($"ผลึก{RarityTh[r]}", $"{RarityEn[r]} Shard"), desc = Loc.S("ได้จากการย่อยของ ใช้ตีบวกอุปกรณ์ระดับเดียวกัน", "From dismantled gear; enhances gear of the same rarity") });
            }
            list.Add(new ItemDef { id = "potion", slot = Slot.Potion, rarity = 1, name = Loc.S("ยาฟื้นพลัง", "Healing Potion"), desc = Loc.S("ใช้ตอนสู้: ฟื้นพลังชีวิต 35% (ปุ่ม H หรือปุ่มยาบนจอ)", "In a fight: restores 35% health (H, or the potion button)") });
            return list;
        }
        public static ItemDef Get(string id) { var b = Base(id); return All.FirstOrDefault(i => i.id == b); }
        public static bool IsGear(ItemDef d) => d != null && d.slot <= Slot.Accessory;

        // what the worn gear adds up to
        public static GearBonus Bonus(Main.Save s)
        {
            var b = new GearBonus();
            foreach (var id in new[] { s.helm, s.armor, s.boots, s.acc })
            {
                var d = Get(id); if (d == null) continue; float k = Power(Level(id));
                b.hp += d.hp * k; b.def += d.def * k; b.spd += d.spd * k; b.crit += d.crit * k; b.atk += d.atk * k;
            }
            b.def = Mathf.Min(b.def, .6f); return b;
        }
        public static string Worn(Main.Save s, Slot slot) => slot switch { Slot.Helm => s.helm, Slot.Armor => s.armor, Slot.Boots => s.boots, _ => s.acc };
        public static void SetWorn(Main.Save s, Slot slot, string id) { switch (slot) { case Slot.Helm: s.helm = id; break; case Slot.Armor: s.armor = id; break; case Slot.Boots: s.boots = id; break; default: s.acc = id; break; } }

        // the stat lines of an item, for the details box
        public static IEnumerable<(string k, string v)> Lines(ItemDef d, int level = 0)
        {
            float k = Power(level);
            if (d.hp > 0) yield return (Loc.S("พลังชีวิต", "HP"), $"+{d.hp * k:0}");
            if (d.def > 0) yield return (Loc.S("ลดดาเมจ", "DEF"), $"+{d.def * k * 100:0.#}%");
            if (d.spd > 0) yield return (Loc.S("ความเร็ว", "SPD"), $"+{d.spd * k:0.##}");
            if (d.atk > 0) yield return (Loc.S("โจมตี", "ATK"), $"+{d.atk * k * 100:0.#}%");
            if (d.crit > 0) yield return (Loc.S("คริติคอล", "CRIT"), $"+{d.crit * k * 100:0.#}%");
        }

        // a piece against what is worn in its slot: each stat either has, its value and the change
        static float[] Vals(string id) { var d = Get(id); if (d == null) return new float[5]; float k = Power(Level(id)); return new[] { d.hp * k, d.def * k * 100, d.spd * k, d.atk * k * 100, d.crit * k * 100 }; }
        public static IEnumerable<(string k, string v, string diff, int sign)> Compare(string id, string worn)
        {
            float[] a = Vals(id), b = Vals(worn);
            string[] names = { Loc.S("พลังชีวิต", "HP"), Loc.S("ลดดาเมจ", "DEF"), Loc.S("ความเร็ว", "SPD"), Loc.S("โจมตี", "ATK"), Loc.S("คริติคอล", "CRIT") }, fmt = { "0", "0.#", "0.##", "0.#", "0.#" }, unit = { "", "%", "", "%", "%" };
            for (int i = 0; i < 5; i++)
            {
                if (a[i] <= 0 && b[i] <= 0) continue;
                float d = a[i] - b[i]; int sign = Mathf.Abs(d) < .005f ? 0 : d > 0 ? 1 : -1;
                string m = Mathf.Abs(d).ToString(fmt[i]) + unit[i];
                yield return (names[i], "+" + a[i].ToString(fmt[i]) + unit[i], sign == 0 ? Loc.S("เท่าเดิม", "same") : sign > 0 ? Loc.S($"เพิ่ม {m}", $"+{m}") : Loc.S($"ลด {m}", $"-{m}"), sign);
            }
        }

        // ---------- enhancing: +1 to +10, each level adds 8% of the piece's stats; a failed try costs the shards and gold, never a level ----------
        public const int MaxLevel = 10; const float LevelStep = .08f;
        static readonly float[] Chances = { 1, 1, 1, .9f, .8f, .7f, .6f, .5f, .4f, .3f };
        public static string Base(string id) { if (string.IsNullOrEmpty(id)) return id; int p = id.IndexOf('+'); return p < 0 ? id : id.Substring(0, p); }
        public static int Level(string id) { if (string.IsNullOrEmpty(id)) return 0; int p = id.IndexOf('+'); return p >= 0 && int.TryParse(id.Substring(p + 1), out int n) ? Mathf.Clamp(n, 0, MaxLevel) : 0; }
        public static string WithLevel(string id, int level) => level > 0 ? $"{Base(id)}+{level}" : Base(id);
        public static string Name(string id) { var d = Get(id); if (d == null) return ""; int n = Level(id); return n > 0 ? $"{d.name} +{n}" : d.name; }
        public static float Power(int level) => 1 + level * LevelStep;
        // the price of going up from this level
        public static float Chance(int level) => Chances[Mathf.Clamp(level, 0, MaxLevel - 1)];
        public static int ShardCost(int level) => 1 + level / 3;
        public static int GoldCost(int rarity, int level) => 20 * (rarity + 1) * (level + 1);
        public static EnhanceResult Enhance(Main.Save s, ref string id)
        {
            var d = Get(id); int n = Level(id); if (d == null || !IsGear(d) || n >= MaxLevel) return EnhanceResult.Max;
            int shards = ShardCost(n), gold = GoldCost(d.rarity, n);
            if (s.mats[d.rarity] < shards) return EnhanceResult.NoShards;
            if (s.gold < gold) return EnhanceResult.NoGold;
            s.mats[d.rarity] -= shards; s.gold -= gold;
            if (Random.value >= Chance(n)) return EnhanceResult.Failed;
            id = WithLevel(id, n + 1); return EnhanceResult.Success;
        }
        // an enhanced piece gives some of what went into it back: half the shards, and a higher price
        public static int DismantleShards(string id) { var d = Get(id); int n = Level(id), spent = 0; for (int i = 0; i < n; i++) spent += ShardCost(i); return 1 + d.rarity / 2 + spent / 2; }
        public static int SellPrice(string id) { var d = Get(id); return Mathf.RoundToInt(d.Sell * (1 + Level(id) * .25f)); }
        // what the bulk-sell button takes: common and uncommon pieces that were never enhanced
        public static List<string> Junk(Main.Save s) => s.bag.Where(i => (Get(i)?.rarity ?? 9) <= 1 && Level(i) == 0).ToList();

        // a random drop: monsters mostly give common pieces, bosses give better ones; the stage lifts the odds a little
        public static string Roll(bool boss, int stage)
        {
            float u = Random.value; if (!boss && u < .12f) return "potion"; if (!boss && u < .2f) return "shard_0";
            var gear = new[] { Slot.Helm, Slot.Armor, Slot.Boots, Slot.Accessory }[Random.Range(0, 4)];
            float x = Random.value * (boss ? .55f : 1) - stage * .015f; int r = x < .006f ? 4 : x < .035f ? 3 : x < .13f ? 2 : x < .4f ? 1 : 0;
            if (boss) r = Mathf.Max(r, 1);
            return All.First(i => i.slot == gear && i.rarity == r).id;
        }

        // placeholder pictures until the painted icons arrive (ITEM-PROMPTS.md): simple shapes per slot, drawn in the rarity colour
        public static void Icon(MeshGenerationContext ctx, Slot slot, float w, Color c)
        {
            var p = ctx.painter2D; float k = w / 24; Vector2 P(float x, float y) => new(x * k, y * k);
            p.fillColor = c; p.strokeColor = c; p.lineWidth = 1.6f * k; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            switch (slot)
            {
                case Slot.Helm:   // dome with a visor slit and a nose guard
                    p.BeginPath(); p.MoveTo(P(4, 16)); p.BezierCurveTo(P(4, 4), P(20, 4), P(20, 16)); p.LineTo(P(20, 19)); p.LineTo(P(4, 19)); p.ClosePath(); p.Fill();
                    p.fillColor = new Color(0, 0, 0, .55f); p.BeginPath(); p.MoveTo(P(6, 13)); p.LineTo(P(18, 13)); p.LineTo(P(18, 15)); p.LineTo(P(6, 15)); p.ClosePath(); p.Fill();
                    p.fillColor = c; p.BeginPath(); p.MoveTo(P(11, 12)); p.LineTo(P(13, 12)); p.LineTo(P(13, 20)); p.LineTo(P(11, 20)); p.ClosePath(); p.Fill(); break;
                case Slot.Armor:   // breastplate with shoulders
                    p.BeginPath(); p.MoveTo(P(3, 7)); p.LineTo(P(8, 4)); p.LineTo(P(12, 6)); p.LineTo(P(16, 4)); p.LineTo(P(21, 7)); p.LineTo(P(19, 11)); p.LineTo(P(18, 20)); p.LineTo(P(12, 22)); p.LineTo(P(6, 20)); p.LineTo(P(5, 11)); p.ClosePath(); p.Fill();
                    p.strokeColor = new Color(0, 0, 0, .45f); p.BeginPath(); p.MoveTo(P(12, 7)); p.LineTo(P(12, 21)); p.Stroke(); break;
                case Slot.Boots:   // a boot
                    p.BeginPath(); p.MoveTo(P(7, 3)); p.LineTo(P(14, 3)); p.LineTo(P(14, 14)); p.LineTo(P(21, 17)); p.LineTo(P(21, 21)); p.LineTo(P(6, 21)); p.LineTo(P(7, 14)); p.ClosePath(); p.Fill(); break;
                case Slot.Accessory:   // a ring with a gem
                    p.lineWidth = 2.6f * k; p.BeginPath(); p.Arc(P(12, 14), 6.5f * k, 0, 360); p.Stroke();
                    p.BeginPath(); p.MoveTo(P(12, 2)); p.LineTo(P(16, 6)); p.LineTo(P(12, 10)); p.LineTo(P(8, 6)); p.ClosePath(); p.Fill(); break;
                case Slot.Potion:   // round flask with a cork
                    p.fillColor = new Color(1, .25f, .3f); p.BeginPath(); p.Arc(P(12, 15), 7 * k, 0, 360); p.Fill();
                    p.strokeColor = new Color(1, 1, 1, .85f); p.BeginPath(); p.Arc(P(12, 15), 7 * k, 0, 360); p.Stroke();
                    p.fillColor = new Color(.85f, .9f, .95f); p.BeginPath(); p.MoveTo(P(10, 3)); p.LineTo(P(14, 3)); p.LineTo(P(14, 9)); p.LineTo(P(10, 9)); p.ClosePath(); p.Fill();
                    p.fillColor = new Color(.6f, .4f, .2f); p.BeginPath(); p.MoveTo(P(9.5f, 1.5f)); p.LineTo(P(14.5f, 1.5f)); p.LineTo(P(14.5f, 4)); p.LineTo(P(9.5f, 4)); p.ClosePath(); p.Fill(); break;
                default:   // a cut crystal
                    p.BeginPath(); p.MoveTo(P(12, 2)); p.LineTo(P(19, 9)); p.LineTo(P(12, 22)); p.LineTo(P(5, 9)); p.ClosePath(); p.Fill();
                    p.strokeColor = new Color(1, 1, 1, .6f); p.BeginPath(); p.MoveTo(P(5, 9)); p.LineTo(P(19, 9)); p.MoveTo(P(12, 2)); p.LineTo(P(12, 22)); p.Stroke(); break;
            }
        }
    }
}
