using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Level-up picks in Sky Lanes (owner, 2026-11-11, "option C" instead of an item shop): at match levels 3, 6 and 9 a hero takes one of
    // three powers. The player picks on a panel (tap, or 1/2/3; a random one after a few seconds); AI heroes pick when their side reaches
    // the level. Picks are kept by side and hero name, so an AI hero that dies and comes back still has them. Lane battles only.
    public static class Perks
    {
        public class P { public string id, name, desc, cat; public Color col; public bool ai; public int rare; }   // cat: offense / utility / defense; rare: 1 rare, 2 epic, 3 legendary (shown on the card)
        public const float MightK = 1.15f, HasteK = 1.18f, SwiftK = 1.12f, ArmorK = .88f, VigorK = 1.12f, SiegeK = 1.3f, Leech = .08f, WardAt = .3f, WardTime = 3, WardEvery = 45;
        public static readonly P[] All =
        {
            new() { id = "might", name = Loc.S("ตีแรง", "Power"), desc = Loc.S("ดาเมจ +15%", "+15% damage"), col = new Color(1f, .45f, .3f), ai = true, cat = "offense", rare = 1 },
            new() { id = "haste", name = Loc.S("สกิลไว", "Quick Skills"), desc = Loc.S("คูลดาวน์เร็วขึ้น 18%", "Skills recharge 18% faster"), col = new Color(.4f, .85f, 1f), ai = true, cat = "utility", rare = 2 },
            new() { id = "leech", name = Loc.S("ดูดเลือด", "Lifesteal"), desc = Loc.S("ฟื้นเลือด 8% ของดาเมจที่ทำ", "Heal 8% of the damage you deal"), col = new Color(.95f, .3f, .4f), ai = false, cat = "offense", rare = 2 },
            new() { id = "swift", name = Loc.S("ฝีเท้าไว", "Swift"), desc = Loc.S("วิ่งเร็วขึ้น 12%", "+12% move speed"), col = new Color(.5f, 1f, .6f), ai = true, cat = "utility", rare = 1 },
            new() { id = "ward", name = Loc.S("โล่ฉุกเฉิน", "Last Ward"), desc = Loc.S("เลือดต่ำกว่า 30%: โล่ 3 วิ (ทุก 45 วิ)", "Under 30% health: a 3 s shield (every 45 s)"), col = new Color(1f, .85f, .4f), ai = false, cat = "defense", rare = 3 },
            new() { id = "siege", name = Loc.S("ทุบป้อม", "Siege"), desc = Loc.S("ตีป้อมแรงขึ้น 30%", "+30% damage to towers"), col = new Color(.9f, .62f, .35f), ai = true, cat = "utility", rare = 2 },
            new() { id = "armor", name = Loc.S("เกราะหนา", "Armor"), desc = Loc.S("รับดาเมจน้อยลง 12%", "Take 12% less damage"), col = new Color(.72f, .78f, .9f), ai = true, cat = "defense", rare = 1 },
            new() { id = "vigor", name = Loc.S("เลือดหนา", "Vigor"), desc = Loc.S("เลือดสูงสุด +12%", "+12% max health"), col = new Color(.45f, .92f, .5f), ai = true, cat = "defense", rare = 1 },
        };
        public static readonly int[] At = { 3, 6, 9 };
        public const string Me = "me";
        static readonly Dictionary<string, HashSet<string>> taken = new();
        public static void Begin() => taken.Clear();
        public static string Key(int team, string name) => team + ":" + name;
        static HashSet<string> Of(string key) { if (!taken.TryGetValue(key, out var s)) taken[key] = s = new HashSet<string>(); return s; }
        public static bool Has(string key, string id) => Main.G && Main.G.SkyMatch && taken.TryGetValue(key, out var s) && s.Contains(id);
        public static int Count(string key) => taken.TryGetValue(key, out var s) ? s.Count : 0;
        public static int Due(int level) => At.Count(a => level >= a);   // how many picks a hero of this level should have
        public static P Get(string id) => All.FirstOrDefault(p => p.id == id);
        public static IEnumerable<P> Taken(string key) => taken.TryGetValue(key, out var s) ? s.Select(Get) : Enumerable.Empty<P>();
        // three different powers the hero does not have yet
        public static string[] Offer(string key, bool ai = false)
        {
            var have = Of(key); return All.Where(p => !have.Contains(p.id) && (!ai || p.ai)).OrderBy(_ => Random.value).Take(3).Select(p => p.id).ToArray();
        }
        public static void Take(string key, string id) { if (Get(id) != null) Of(key).Add(id); }
        // an AI hero catches up on the picks its level is due (a respawned one already has them)
        public static void AiCatchUp(Foe f, int level)
        {
            if (f == null || f.d == null || f.d.kind != "champ") return; var key = Key(f.team, f.d.name);
            while (Count(key) < Due(level)) { var o = Offer(key, true); if (o.Length == 0) break; Take(key, o[0]); }
            if (f.Perk("vigor") && !f.vigorOn) { f.vigorOn = true; f.maxHp *= VigorK; f.hp *= VigorK; }
        }
    }
}
