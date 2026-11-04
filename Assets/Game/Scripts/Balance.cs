using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Game numbers in one editable file: Assets/Game/Resources/Data/balance.json (see BALANCE.md). Read at start-up and laid
    // over the defaults in Data.cs, matched by id; anything missing from the file keeps its default, unknown ids are
    // ignored. Editor: Nova Blade > Export balance.json writes the current values out (Balance.Export).
    public static class Balance
    {
        [System.Serializable] public class File { public Weapon[] weapons; public Hero[] heroes; public Foe[] monsters; public Skill[] skills; public Stage[] stages; public Wing[] wings; public Mastery mastery; public Prices prices; }
        [System.Serializable] public class Weapon { public string id; public int price; public float damage, speed, range, crit, lifesteal; }
        [System.Serializable] public class Hero { public string id; public float hp, speed, attack; }
        [System.Serializable] public class Foe { public string id; public float hp, speed, damage, range, size; public int gold; }
        [System.Serializable] public class Skill { public string id; public float cooldown; }
        // waves: one line per wave, e.g. "goblin x4, rat x3"
        [System.Serializable] public class Stage { public string id; public float bossHp; public string[] waves; }
        [System.Serializable] public class Wing { public string id; public int gems; }
        [System.Serializable] public class Mastery { public int[] tierPoints; public float[] damageBonus, critBonus; public float masterFinisher; public int bossPoints; public float affinityMain, affinitySecond; }
        [System.Serializable] public class Prices { public int[] heroSkinGems; public int weaponSkinGems; }

        public static void Load()
        {
            var t = Resources.Load<TextAsset>("Data/balance"); if (!t) return;
            File f; try { f = JsonUtility.FromJson<File>(t.text); } catch (System.Exception e) { Debug.LogError("[Balance] balance.json: " + e.Message); return; }
            if (f == null) return;
            foreach (var w in f.weapons ?? new Weapon[0]) { var d = Data.Swords.FirstOrDefault(x => x.id == w.id); if (d == null) continue; d.price = w.price; d.dmg = w.damage; d.spd = w.speed; d.len = w.range; d.crit = w.crit; d.steal = w.lifesteal; }
            foreach (var h in f.heroes ?? new Hero[0]) { var d = Data.Heroes.FirstOrDefault(x => x.id == h.id); if (d == null) continue; d.hp = h.hp; d.spd = h.speed; d.atk = h.attack; }
            foreach (var m in f.monsters ?? new Foe[0]) { var d = Data.Foe(m.id); if (d == null) continue; d.hp = m.hp; d.spd = m.speed; d.dmg = m.damage; d.range = m.range; d.scale = m.size; d.gold = m.gold; }
            foreach (var k in f.skills ?? new Skill[0]) { var d = Data.Skill(k.id); if (d != null) d.cd = k.cooldown; }
            foreach (var st in f.stages ?? new Stage[0])
            {
                var th = Data.Themes.FirstOrDefault(x => x.id == st.id); if (th == null) continue;
                if (st.bossHp > 0 && th.boss != null) th.boss.hp = st.bossHp;
                if (st.waves != null && st.waves.Length > 0) th.waves = st.waves.Select(ParseWave).Where(w => w.Length > 0).ToArray();
            }
            foreach (var w in f.wings ?? new Wing[0]) { var d = Data.Wing(w.id); if (d != null) d.gems = w.gems; }
            var ms = f.mastery;
            if (ms != null)
            {
                if (ms.tierPoints?.Length == 5) Data.MasteryXp = ms.tierPoints; if (ms.damageBonus?.Length == 5) Data.MasteryDmg = ms.damageBonus; if (ms.critBonus?.Length == 5) Data.MasteryCrit = ms.critBonus;
                if (ms.masterFinisher > 0) Data.MasterFinisher = ms.masterFinisher; if (ms.bossPoints > 0) Data.BossMasteryXp = ms.bossPoints;
                Data.AffinityMain = ms.affinityMain; Data.AffinitySecond = ms.affinitySecond;
            }
            if (f.prices != null) { if (f.prices.heroSkinGems?.Length > 0) Shop.HeroSkinGems = f.prices.heroSkinGems; if (f.prices.weaponSkinGems > 0) Shop.WeaponSkinGems = f.prices.weaponSkinGems; }
            Debug.Log("[Balance] balance.json applied");
        }
        // "goblin x4, rat x3" -> (goblin, 4), (rat, 3); unknown monsters are dropped
        static (string foe, int n)[] ParseWave(string line) => line.Split(',').Select(p => p.Trim().Split('x', 'X')).Where(p => p.Length == 2 && Data.Foe(p[0].Trim()) != null && int.TryParse(p[1].Trim(), out _))
            .Select(p => (p[0].Trim(), int.Parse(p[1].Trim()))).ToArray();

        // the current numbers as a file (editor export, and the shape the loader expects)
        public static File Current() => new File
        {
            weapons = Data.Swords.Select(w => new Weapon { id = w.id, price = w.price, damage = w.dmg, speed = w.spd, range = w.len, crit = w.crit, lifesteal = w.steal }).ToArray(),
            heroes = Data.Heroes.Select(h => new Hero { id = h.id, hp = h.hp, speed = h.spd, attack = h.atk }).ToArray(),
            monsters = Data.Foes.Select(m => new Foe { id = m.id, hp = m.hp, speed = m.spd, damage = m.dmg, range = m.range, size = m.scale, gold = m.gold }).ToArray(),
            skills = Data.Skills.Select(k => new Skill { id = k.id, cooldown = k.cd }).ToArray(),
            stages = Data.Themes.Select(t => new Stage { id = t.id, bossHp = t.boss?.hp ?? 0, waves = t.waves.Select(w => string.Join(", ", w.Select(x => $"{x.foe} x{x.n}"))).ToArray() }).ToArray(),
            wings = Data.Wings.Select(w => new Wing { id = w.id, gems = w.gems }).ToArray(),
            mastery = new Mastery { tierPoints = Data.MasteryXp, damageBonus = Data.MasteryDmg, critBonus = Data.MasteryCrit, masterFinisher = Data.MasterFinisher, bossPoints = Data.BossMasteryXp, affinityMain = Data.AffinityMain, affinitySecond = Data.AffinitySecond },
            prices = new Prices { heroSkinGems = Shop.HeroSkinGems, weaponSkinGems = Shop.WeaponSkinGems },
        };
    }
}
