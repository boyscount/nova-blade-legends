using System.Collections.Generic;
using System.Linq;

namespace NovaBlade
{
    // Basic quests: goals counted over the whole save, any order, reward claimed in the lobby. Counters live in the save.
    public class QuestDef { public string id, name, desc, kind; public int goal, gold, gems; }

    public static class Quests
    {
        public static readonly QuestDef[] All =
        {
            Q("kill1", "ล่าครั้งแรก", "First Hunt", "ปราบมอนสเตอร์ 10 ตัว", "Defeat 10 monsters", "kill", 10, 100, 0),
            Q("wear1", "พร้อมลุย", "Gear Up", "สวมอุปกรณ์ 1 ชิ้น", "Wear a piece of gear", "wear", 1, 100, 0),
            Q("clear1", "ผ่านด่านแรก", "Stage Cleared", "ผ่านด่าน 1 ครั้ง", "Clear a stage", "clear", 1, 200, 0),
            Q("orb1", "นักเก็บลูกแก้ว", "Orb Collector", "เก็บลูกแก้ว 30 ลูก", "Collect 30 orbs", "orb", 30, 150, 0),
            Q("enh1", "ลูกมือช่างตี", "Smith's Apprentice", "ตีบวกอุปกรณ์สำเร็จ 1 ครั้ง", "Enhance gear once", "enhance", 1, 150, 0),
            Q("lv5", "ฮีโร่ดาวรุ่ง", "Rising Hero", "ฮีโร่ถึง Lv.5", "Reach Lv.5 with a hero", "level", 5, 300, 10),
            Q("kill2", "นักล่ามอนสเตอร์", "Monster Hunter", "ปราบมอนสเตอร์ 50 ตัว", "Defeat 50 monsters", "kill", 50, 300, 10),
            Q("boss1", "ผู้พิชิตบอส", "Boss Slayer", "ปราบบอส 3 ตัว", "Defeat 3 bosses", "boss", 3, 500, 20),
            Q("orb2", "เจ้าแห่งลูกแก้ว", "Orb Master", "เก็บลูกแก้ว 150 ลูก", "Collect 150 orbs", "orb", 150, 500, 10),
            Q("clear2", "นักเดินทาง", "Road Warrior", "ผ่านด่าน 5 ครั้ง", "Clear 5 stages", "clear", 5, 600, 20),
            Q("lv10", "ฮีโร่ในตำนาน", "Hero of Legend", "ฮีโร่ถึง Lv.10", "Reach Lv.10 with a hero", "level", 10, 800, 30),
            Q("kill3", "ตำนานนักล่า", "Legendary Hunter", "ปราบมอนสเตอร์ 200 ตัว", "Defeat 200 monsters", "kill", 200, 800, 30),
        };
        static QuestDef Q(string id, string th, string en, string dth, string den, string kind, int goal, int gold, int gems) =>
            new QuestDef { id = id, name = Loc.S(th, en), desc = Loc.S(dth, den), kind = kind, goal = goal, gold = gold, gems = gems };

        public static int Count(Main.Save s, string kind)
        {
            if (kind == "level") return s.xpKeys == null || s.xpVals == null || s.xpVals.Count == 0 ? 1 : s.xpVals.Max(Data.Level);
            int i = s.questKeys == null ? -1 : s.questKeys.IndexOf(kind); return i < 0 ? 0 : s.questVals[i];
        }
        public static void Bump(Main.Save s, string kind, int n = 1)
        {
            s.questKeys ??= new(); s.questVals ??= new();
            int i = s.questKeys.IndexOf(kind); if (i < 0) { s.questKeys.Add(kind); s.questVals.Add(n); } else s.questVals[i] += n;
        }
        public static int Progress(Main.Save s, QuestDef q) => System.Math.Min(Count(s, q.kind), q.goal);
        public static bool Done(Main.Save s, QuestDef q) => Count(s, q.kind) >= q.goal;
        public static bool Claimed(Main.Save s, QuestDef q) => s.questClaimed != null && s.questClaimed.Contains(q.id);
        public static bool Ready(Main.Save s, QuestDef q) => Done(s, q) && !Claimed(s, q);
        public static int ReadyCount(Main.Save s) => All.Count(q => Ready(s, q));
        // claimable first, then under way, then finished; each group keeps the list's order
        public static IEnumerable<QuestDef> Sorted(Main.Save s) => All.OrderBy(q => Ready(s, q) ? 0 : Claimed(s, q) ? 2 : 1);
        public static bool Claim(Main.Save s, QuestDef q)
        {
            if (!Ready(s, q)) return false;
            s.questClaimed ??= new(); s.questClaimed.Add(q.id); s.gold += q.gold; s.gems += q.gems; return true;
        }
        public static string Reward(QuestDef q) => q.gems > 0 ? $"{q.gold} Gold + {q.gems} Gem" : $"{q.gold} Gold";
    }
}
