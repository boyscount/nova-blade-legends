using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Hero legends (HERO-STORIES.md): a short story mode per hero, four stages of 3-5 minutes. The hero starts with plain attacks and gets one
    // skill a stage; the last stage is a boss fight with all three. A dialogue with the speakers' cards before and after each stage.
    // Normal: anyone can finish it. Legend (hard): the bosses hit harder and have more health, no potions; finishing it earns the legend reward.
    // The pilot is Ming's "Embers Still Warm"; Leon's follows the same shape.
    public static class Legends
    {
        public class Line { public string who, th, en; public string Text => Loc.S(th, en); }
        public class Step
        {
            public string theme, th, en;          // theme: the stage it is fought on (Data.Themes id); th/en: the step's title
            public (string foe, int n)[][] waves; // the monsters, wave by wave
            public BossDef boss;                  // null: the step ends when the last wave falls
            public int skills;                    // skills open in this step (0-3)
            public Line[] before, after;
            public string Title => Loc.S(th, en);
        }
        public class Legend { public string hero, th, en, rewardTh, rewardEn; public Step[] steps; public string Name => Loc.S(th, en); }

        static Line S(string who, string th, string en) => new() { who = who, th = th, en = en };
        // a hero fought as a boss: a fair duel (Main.DuelSync sizes it to the player: hits = the player's light blows to fell it, takes = its blows to fell the player)
        static BossDef HeroBoss(string id, string th, string en, Color col, float hits, float takes, string[] near, string[] far) => new()
        {
            model = "hero_" + id, name = Loc.S(th, en), scale = 1.08f, hp = 6000, dmg = 120, spd = 3.8f, poise = 280, hits = hits, takes = takes,
            tint = Color.white, glow = col * .12f, rage = col * .35f, color = col, near = near, far = far,
        };

        public static readonly Legend Ming = new()
        {
            hero = "monk", th = "เถ้าที่ยังอุ่น", en = "Embers Still Warm", rewardTh = "สกินตำนาน \"ผู้สืบเพลิง\"", rewardEn = "The legend skin \"Heir of the Flame\"",
            steps = new[]
            {
                new Step
                {
                    theme = "castle", th = "ซากอาราม", en = "The Burnt Monastery", skills = 0,
                    waves = new[] { new[] { ("skeleton", 3), ("candle", 2) }, new[] { ("zombie", 3), ("skeleton", 2), ("candle", 2) } },
                    before = new[]
                    {
                        S("monk", "สิบปีแล้ว… ที่นี่ยังมีกลิ่นควันอยู่เลย", "Ten years… and it still smells of smoke."),
                        S("monk", "ป้ายชื่อศิษย์ที่ท่านเจ้าอาวาสทิ้งไว้ ครบทุกคน", "The names of the students. The abbot left every one of them."),
                        S("monk", "ลมนี่… ดำผิดปกติ อะไรกำลังลุกขึ้นมา?", "This wind is wrong. Black. Something is getting up…"),
                    },
                    after = new[]
                    {
                        S("monk", "ศพของศิษย์พี่ถูกปลุกด้วยลมดำ ใครกล้าทำแบบนี้", "My brothers, raised by a black wind. Who would dare?"),
                        S("monk", "ถ้าอยากรู้ความจริง ต้องลงจากเขาไปถามคนที่สั่งเผา", "If I want the truth, I go down the mountain and ask whoever gave the order."),
                    },
                },
                new Step
                {
                    theme = "valley", th = "ทางเขาสู่ป่า", en = "Down the Mountain Path", skills = 1,
                    waves = new[] { new[] { ("goblin", 4), ("rat", 2) }, new[] { ("orc", 2), ("goblin", 3) }, new[] { ("orc", 3), ("goblin", 3) } },
                    boss = new BossDef { model = "rig_e_orc", name = Loc.S("หัวหน้าออร์กปล้นทาง", "Orc Road Chief"), scale = 1.5f, hp = 6500, tint = new Color(.85f, 1f, .8f), glow = new Color(.1f, .3f, .06f), rage = new Color(.25f, .5f, .05f), color = new Color(.5f, 1f, .35f), summon = "goblin", summonN = 2 },
                    before = new[]
                    {
                        S("tiger", "หมิง! จะไปไหนคนเดียว ทางลงเขามีออร์กดักปล้นเต็มไปหมด", "Ming! Going alone? The path down is full of orc raiders."),
                        S("monk", "ไท่หู่ อารามสอนให้ข้านิ่ง", "Taihu. The monastery taught me to be still."),
                        S("tiger", "ส่วนถนนสอนให้ทุบ! ส่งแรงลงพื้นแล้วปล่อยขึ้นมาทีเดียว แบบนี้!", "And the street taught me to smash! Push down into the ground, then let it all up at once. Like this!"),
                        S("monk", "(ได้สกิลแรก) ไฟจากพื้น… เข้าใจแล้ว", "(First skill learned) Fire from the ground… I see."),
                    },
                    after = new[]
                    {
                        S("tiger", "ไม่เลวนี่ศิษย์อาราม ต่อไปจะไปไหน", "Not bad for a monastery boy. Where now?"),
                        S("monk", "วิหารสุริยันจันทรา คนที่สั่งเผาอยู่ที่นั่น", "The Temple of Sun and Moon. Whoever ordered the burning is there."),
                    },
                },
                new Step
                {
                    theme = "kingdom", th = "ประตูวิหาร", en = "The Temple Gate", skills = 2,
                    waves = new[] { new[] { ("demon", 2), ("rat", 3) }, new[] { ("demon", 3), ("goblin", 3) } },
                    boss = HeroBoss("sunpriest", "โซเลีย ผู้เฝ้าประตูวิหาร", "Solia, Keeper of the Gate", new Color(1f, .85f, .4f), 36, 9,
                        new[] { "combo", "guard", "combo", "sidestep" }, new[] { "lunge", "combo" }),
                    before = new[]
                    {
                        S("sunpriest", "ศิษย์อารามเพลิงทอง วิหารไม่ต้อนรับผู้ใช้ไฟนอกรีต", "A Golden Flame student. The temple does not welcome heretic fire."),
                        S("monk", "ข้าไม่ได้มาขอพร ข้ามาถามว่าใครสั่งเผาอารามเมื่อสิบปีก่อน", "I'm not here for blessings. I'm here to ask who ordered the monastery burned ten years ago."),
                        S("sunpriest", "ถ้าอยากได้คำตอบ ก็ผ่านข้าไปให้ได้ก่อน", "Then get past me first."),
                        S("monk", "(ได้สกิลที่สอง) หมุนตัวให้ไฟเป็นพายุ ท่านอาจารย์เคยสอนไว้", "(Second skill learned) Turn the fire into a storm, as the master taught."),
                    },
                    after = new[]
                    {
                        S("sunpriest", "…วิหารไม่เคยสั่งเผาใคร", "…The temple never ordered anyone burned."),
                        S("sunpriest", "แต่คืนนั้นเซราฟีน่านำกองเพลิงออกไปเอง เธออยู่ในป่าแสงจันทร์", "But that night Seraphina led the flame guard out on her own. She is in the Moonlit Forest."),
                        S("monk", "ขอบคุณ… และขอโทษที่ต้องสู้กัน", "Thank you. And I'm sorry we had to fight."),
                    },
                },
                new Step
                {
                    theme = "forest", th = "เปลวไฟเดียวกัน", en = "The Same Flame", skills = 3,
                    waves = new[] { new[] { ("demon", 2), ("candle", 3), ("bat", 2) } },
                    boss = HeroBoss("flamepriest", "เซราฟีน่า เพลิงพิพากษา", "Seraphina, the Judging Flame", new Color(1f, .45f, .2f), 48, 7,
                        new[] { "combo", "sweep", "combo", "guard", "sidestep" }, new[] { "lunge", "lunge", "combo" }),
                    before = new[]
                    {
                        S("flamepriest", "เด็กที่รอดจากกองเพลิง สิบปีผ่านไปเจ้ามาหาข้าเอง", "The child who lived through the fire. Ten years, and you come to me."),
                        S("monk", "ทำไมต้องเผา อารามไม่เคยทำร้ายใคร", "Why burn it? The monastery never harmed anyone."),
                        S("flamepriest", "ไฟในหมัดพวกเจ้าคือนอกรีต ข้าเผาเพื่อชำระมัน", "The fire in your fists is heresy. I burned it to make it clean."),
                        S("monk", "(ได้สกิลสุดท้าย) งั้นมาดูกันว่าไฟของใครแท้กว่า", "(Last skill learned) Then let us see whose fire is true."),
                    },
                    after = new[]
                    {
                        S("monk", "ข้าไม่ฆ่าท่าน อารามไม่ได้สอนให้แค้น", "I won't kill you. The monastery never taught revenge."),
                        S("flamepriest", "…คืนนั้นมีคนใส่ผ้าคลุมสีพายุมาบอกข้า ว่าอารามซ่อนเวทพายุไว้ใต้ดิน", "…That night a man in a storm-grey cloak came to me. He said the monastery hid storm magic beneath it."),
                        S("flamepriest", "ข้าเชื่อเขา ไฟของข้าเผาแต่ความจริงไม่เคยถูกเผา", "I believed him. My fire burned everything but the truth."),
                        S("monk", "ผ้าคลุมสีพายุ… ลมดำที่ปลุกศพศิษย์พี่ก็คงมาจากคนเดียวกัน", "A storm-grey cloak… The black wind that raised my brothers came from the same hand."),
                    },
                },
            },
        };

        public static readonly Legend[] All = { Ming };
        public static Legend For(string hero) => All.FirstOrDefault(l => l.hero == hero);

        // progress in the save: "monk=2;monk!=1" = two steps of Ming's legend cleared on normal, one on legend difficulty
        public static int Cleared(Main.Save s, Legend l, bool hard)
        {
            string key = l.hero + (hard ? "!" : "") + "=";
            foreach (var part in (s.legendProg ?? "").Split(';')) if (part.StartsWith(key) && int.TryParse(part.Substring(key.Length), out int n)) return n;
            return 0;
        }
        public static void SetCleared(Main.Save s, Legend l, bool hard, int n)
        {
            string key = l.hero + (hard ? "!" : "") + "=";
            var parts = (s.legendProg ?? "").Split(';').Where(p => p.Length > 0 && !p.StartsWith(key)).ToList(); parts.Add(key + n); s.legendProg = string.Join(";", parts);
        }
        // a step is open once the one before it is cleared (on the same difficulty; legend difficulty opens after normal is finished)
        public static bool Open(Main.Save s, Legend l, bool hard, int step) => (!hard || Cleared(s, l, false) >= l.steps.Length) && step <= Cleared(s, l, hard);
        public const int StepGold = 120, HardStepGold = 300;
        public const float HardHp = 1.3f, HardDmg = 1.25f;   // legend difficulty: the bosses
    }

    public partial class Main
    {
        public Legends.Legend legend; public int legendStep; public bool legendHardOn; ThemeDef legendTheme;
        public bool LegendRun => legend != null && mode != Mode.Lobby && !lane;
        public bool legendHard => LegendRun && legendHardOn;
        public int SkillsOpen => LegendRun ? legend.steps[legendStep].skills : 3;
        public Legends.Step LegendNow => LegendRun ? legend.steps[legendStep] : null;
        public void EndLegend() { legend = null; legendTheme = null; }

        public void StartLegend(Legends.Legend l, int step, bool hard)
        {
            var st = l.steps[Mathf.Clamp(step, 0, l.steps.Length - 1)];
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = false; soul = false; ClearPreview();
            legend = l; legendStep = Mathf.Clamp(step, 0, l.steps.Length - 1); legendHardOn = hard;
            legendTheme = new ThemeDef { id = st.theme, name = st.Title, waves = st.waves, boss = st.boss };
            save.hero = l.hero; Persist();
            stage = legendStep + 1; SetTheme(Mathf.Max(0, System.Array.FindIndex(Data.Themes, t => t.id == st.theme)));
            kills = 0; gold = 0; time = 0; banked = 0; between = 0; wave = 0;
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame(); PreloadStage();
            camPos = new Vector3(0, 9, 14);
            ui.ShowHud();
            ui.Banner(l.Name, Loc.S($"บทที่ {legendStep + 1} · {st.th}{(hard ? " · ระดับตำนาน" : "")}", $"Chapter {legendStep + 1} · {st.en}{(hard ? " · Legend" : "")}"));
            paused = true; Time.timeScale = 0;   // the story first, then the fight
            ui.Dialogue(st.before, () =>
            {
                paused = false; Time.timeScale = 1;
                if (st.skills > 0 && hero) ui.Banner(Loc.S("ปลดล็อกสกิลใหม่!", "New skill!"), hero.SkillName(st.skills - 1));
                StartWave(0);
            });
        }
        // the end of a legend step: the story after it, the reward, then the result screen
        void LegendFinish(bool win)
        {
            if (!win) { ui.ShowResult(false); return; }
            int was = Legends.Cleared(save, legend, legendHardOn);
            if (legendStep + 1 > was) { Legends.SetCleared(save, legend, legendHardOn, legendStep + 1); int bonus = legendHardOn ? Legends.HardStepGold : Legends.StepGold; save.gold += bonus; gold += bonus; banked += bonus; }
            if (legendHardOn && legendStep + 1 >= legend.steps.Length)
            {   // the legend's reward: the hero's legend skin
                var h = System.Array.Find(Data.Heroes, x => x.id == legend.hero);
                if (h != null) for (int i = 0; i < Data.SkinCount(h); i++) if (Data.ModelOf(h, i).EndsWith("_legend")) { var key = Shop.Key(ItemKind.HeroSkin, $"{h.id}:{i}"); if (!save.owned.Contains(key)) save.owned.Add(key); }
            }
            Persist();
            ui.Dialogue(legend.steps[legendStep].after, () => ui.ShowResult(true));
        }
        // -legendtest <dir>: Ming's legend start to end: the picker, each chapter's dialogue and skill lock, waves cleared, the bosses, the save
        public System.Collections.IEnumerator LegendTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            save.legendProg = ""; ui.TestOpenLegends(false); yield return new WaitForSecondsRealtime(.8f); Snap("picker"); log.AppendLine($"picker open {ui.LegendsOpen}");
            ui.CloseLegends();
            var l = Legends.Ming;
            for (int k = 0; k < l.steps.Length; k++)
            {
                StartLegend(l, k, false); yield return new WaitForSecondsRealtime(1.2f);
                log.AppendLine($"chapter {k + 1} {l.steps[k].en}: hero {hero.def.id} · skills open {SkillsOpen} · paused {paused} · dialogue \"{ui.TestDialogueText}\"");
                if (k == 0 || k == 3) Snap($"dlg{k + 1}");
                int guard = 0; while (ui.DialogueOpen && guard++ < 20) { ui.TestDialogueNext(); yield return null; }
                yield return new WaitForSecondsRealtime(1); log.AppendLine($"  after dialogue: paused {paused} wave {wave} theme {Theme.id} boss {(Theme.boss != null ? Theme.boss.name : "-")}");
                float r0 = Time.realtimeSinceStartup; bool snapped = false; TestSpeed = 3;
                while (mode == Mode.Game && Time.realtimeSinceStartup - r0 < 60)
                {
                    if (boss && boss.alive)
                    {
                        if (!snapped) { snapped = true; TestSpeed = 1; MoveTo(boss.Pos + new Vector3(0, 0, 5)); log.AppendLine($"  boss {boss.d.name}: hp {boss.maxHp:0} dmg {boss.d.dmg:0} (hero hp {hero.max:0}, attack {hero.AtkPow:0})"); yield return new WaitForSecondsRealtime(5); Snap($"boss{k + 1}"); log.AppendLine($"  after 5 s with the boss: hero {hero.hp:0}/{hero.max:0} boss {boss.hp:0}/{boss.maxHp:0}"); hero.hp = hero.max; }
                        boss.Damage(1e7f, hero.Pos);
                    }
                    else foreach (var f in foes.ToList()) if (f && f.alive && !f.spawning && f.team == 1) f.Damage(1e7f, hero.Pos);
                    hero.hp = hero.max; yield return new WaitForSecondsRealtime(.3f);
                }
                TestSpeed = 1; yield return new WaitForSecondsRealtime(.5f);
                log.AppendLine($"  end: mode {mode} dialogue \"{ui.TestDialogueText}\" waves {wave}");
                guard = 0; while (ui.DialogueOpen && guard++ < 20) { ui.TestDialogueNext(); yield return null; }
                yield return new WaitForSecondsRealtime(.6f); if (k == 3) Snap("result4"); log.AppendLine($"  progress {save.legendProg} gold {save.gold}");
            }
            ui.TestOpenLegends(true); yield return new WaitForSecondsRealtime(.8f); Snap("picker_hard"); ui.CloseLegends();
            StartLegend(l, 3, true); yield return new WaitForSecondsRealtime(.5f); while (ui.DialogueOpen) { ui.TestDialogueNext(); yield return null; }
            foreach (var f in foes.ToList()) if (f && f.alive) f.Damage(1e7f, hero.Pos); TestSpeed = 3; float r1 = Time.realtimeSinceStartup;
            while (!boss && Time.realtimeSinceStartup - r1 < 30) { foreach (var f in foes.ToList()) if (f && f.alive && !f.spawning && f.team == 1) f.Damage(1e7f, hero.Pos); yield return new WaitForSecondsRealtime(.3f); }
            TestSpeed = 1; log.AppendLine($"hard boss: {(boss ? $"hp {boss.maxHp:0} dmg {boss.d.dmg:0}" : "none")} · potion button hidden {legendHard}");
            var mh = System.Array.Find(Data.Heroes, x => x.id == "monk"); int li = Enumerable.Range(0, Data.SkinCount(mh)).FirstOrDefault(i => Data.ModelOf(mh, i).EndsWith("_legend"));
            string lk = $"monk:{li}"; log.AppendLine($"legend skin {Data.ModelOf(mh, li)} owned before {Shop.Owns(ItemKind.HeroSkin, lk)} · buy refused {!Shop.Buy(ItemKind.HeroSkin, lk, out var bm)} ({bm}) · leon legend free {Shop.Owns(ItemKind.HeroSkin, "spartan:" + Enumerable.Range(0, Data.SkinCount(System.Array.Find(Data.Heroes, x => x.id == "spartan"))).First(i => Data.ModelOf(System.Array.Find(Data.Heroes, x => x.id == "spartan"), i).EndsWith("_legend")))}");
            if (boss) { boss.Damage(1e7f, hero.Pos); yield return new WaitForSecondsRealtime(3); int g2 = 0; while (ui.DialogueOpen && g2++ < 20) { ui.TestDialogueNext(); yield return null; } }
            log.AppendLine($"after the hard clear: owned {Shop.Owns(ItemKind.HeroSkin, lk)} · progress {save.legendProg}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "legend.txt"), log.ToString()); Application.Quit();
        }
        // a legend boss: the fair-duel heroes are sized to the player; legend difficulty adds to every boss
        void LegendBoss(Foe f)
        {
            if (f.bd != null && f.bd.hits > 0) DuelSync(f, f.bd);
            if (legendHardOn) { f.hp *= Legends.HardHp; f.maxHp *= Legends.HardHp; f.d.dmg *= Legends.HardDmg; }
        }
    }
}
