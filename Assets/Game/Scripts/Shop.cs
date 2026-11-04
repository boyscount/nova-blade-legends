using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    public enum Currency { Gold, Gem }
    public enum ItemKind { Weapon, HeroSkin, WeaponSkin, Wings, Shield }

    // Monetization prototype (design doc v2, Part E): a mock catalog, ownership kept apart from what is equipped,
    // Gold (earned by playing) for gameplay weapons and test-only Premium Gems for cosmetics. Everything is local until a
    // backend exists, but the rules already hold: prices come from this catalog (never from the UI), buying something
    // already owned charges nothing, every purchase goes into a ledger, and skins never touch combat stats.
    public static class Shop
    {
        public const int TestGems = 500;   // test grant, not real money
        public static int[] HeroSkinGems = { 0, 120, 180, 250 };   // prices can be overridden by balance.json
        public static int WeaponSkinGems = 150;
        static Main.Save S => Main.G.save;

        // ids: Weapon "w_lava" · HeroSkin "warrior:2" · WeaponSkin = the model id ("w_gold2"), usable on weapons of the same type
        public static string Key(ItemKind k, string id) => $"{k}:{id}";
        public static (int price, Currency cur) Price(ItemKind k, string id) => k switch
        {
            ItemKind.Weapon => ((Data.Swords.FirstOrDefault(w => w.id == id)?.price) ?? 0, Currency.Gold),
            ItemKind.Wings => (Data.Wing(id)?.gems ?? 0, Currency.Gem),
            ItemKind.Shield => ((Data.Shields.FirstOrDefault(x => x.id == id)?.price) ?? 0, Currency.Gold),
            ItemKind.HeroSkin => (HeroSkinGems[Mathf.Clamp(int.Parse(id.Split(':')[1]), 0, HeroSkinGems.Length - 1)], Currency.Gem),
            _ => (WeaponSkinGems, Currency.Gem),
        };
        public static bool Owns(ItemKind k, string id) => Price(k, id).price == 0 || S.owned.Contains(Key(k, id));
        public static int Balance(Currency c) => c == Currency.Gold ? S.gold : S.gems;
        public static string PriceText(ItemKind k, string id) { var (p, c) = Price(k, id); return c == Currency.Gold ? $"{p:N0} Gold" : $"{p:N0} Gem"; }

        public static bool Buy(ItemKind k, string id, out string msg)
        {
            if (Owns(k, id)) { msg = Loc.S("มีอยู่แล้ว", "Already owned"); return true; }   // idempotent: never charge twice
            var (p, c) = Price(k, id);
            if (Balance(c) < p) { msg = c == Currency.Gold ? Loc.S("Gold ไม่พอ", "Not enough Gold") : Loc.S("Gem ไม่พอ", "Not enough Gems"); return false; }
            if (c == Currency.Gold) S.gold -= p; else S.gems -= p;
            S.owned.Add(Key(k, id));
            S.ledger.Add($"{System.DateTime.UtcNow:o}|{Key(k, id)}|{p}|{c}");
            Main.G.Persist(); msg = Loc.S("ซื้อสำเร็จ", "Purchased"); return true;
        }

        // weapon skins: same weapon type, never the weapon's own model
        public static bool Fits(SwordDef weapon, SwordDef skin) => weapon.type == skin.type && weapon.id != skin.id;
        // the model shown for a weapon: the equipped skin if it is owned and fits, otherwise the weapon's own model
        public static string ModelFor(SwordDef weapon)
        {
            var skin = S.WeaponSkin(weapon.id); var sd = Data.Swords.FirstOrDefault(w => w.id == skin);
            return sd != null && Fits(weapon, sd) && Owns(ItemKind.WeaponSkin, skin) ? skin : weapon.id;
        }
    }
}
