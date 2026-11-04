using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Playables;

namespace NovaBlade
{
    // Game flow: lobby -> stage (3 waves + boss) -> result. Owns enemies, shots, drops, camera and the save.
    public class Main : MonoBehaviour
    {
        public static Main G;
        public enum Mode { Load, Lobby, Game, Result }
        public Mode mode = Mode.Load;
        public bool paused;
        public bool lane; public int laneSize = 3; float minionT;
        public bool soul;   // Soulslike mode (third-person, lock-on, block/parry/stamina) — see SoulController   // lane battle mode (see Lane.cs)
        public int stage = 1, wave, kills, gold, banked;
        public float time, between, hitStop, shake;
        public readonly List<Foe> foes = new();
        public readonly List<Shot> shots = new();
        readonly List<Drop> drops = new();
        readonly Queue<string> queue = new(); float queueT;
        public Foe boss;
        public Hero hero; public Pet pet;
        public Camera cam; Vector3 camPos, camLook; float gameZoom = 1;   // battle camera distance, mouse wheel
        public Ui ui;
        public Transform[] stageRoots; StageLook stageLook;
        public static int CamStyle = -1;   // battle camera: -1 = the player's choice (saved), 0 high, 1 mid, 2 low (test flag -camstyle n)
        public int ViewStyle => CamStyle >= 0 ? CamStyle : PlayerPrefs.GetInt("novaCam", ui && ui.touchUi ? 2 : 0);   // phones start low
        public void CycleView() { PlayerPrefs.SetInt("novaCam", (ViewStyle + 1) % 3); PlayerPrefs.Save(); CamStyle = -1; }
        public void ZoomBy(float d) => gameZoom = Mathf.Clamp(gameZoom + d, .6f, 1.8f);
        public ThemeDef Theme => Data.Themes[Data.ThemeOf(stage)];

        [System.Serializable]
        public class Save
        {
            public string hero = "warrior", sword = "w_lava", pet = "candle"; public int gold, best, stage = 1; public List<string> skinKeys = new(); public List<int> skinVals = new();
            public int gems = Shop.TestGems;                       // test-only premium currency (no real payments yet)
            public List<string> owned = new(), ledger = new();     // ownership and purchase history, kept apart from what is equipped
            public List<string> wsKeys = new(), wsVals = new();    // equipped weapon skin per weapon
            public string sword2 = "";                             // off-hand weapon ("" = none), swapped in during a fight
            public List<string> mastKeys = new(); public List<int> mastVals = new();   // weapon mastery points per weapon
            public int Mastery(string weapon) { int i = mastKeys.IndexOf(weapon); return i < 0 ? 0 : mastVals[i]; }
            public void AddMastery(string weapon, int n) { int i = mastKeys.IndexOf(weapon); if (i < 0) { mastKeys.Add(weapon); mastVals.Add(n); } else mastVals[i] += n; }
            public string shield = "";                             // shield on the left arm ("" = none; warriors and clerics)
            public string wings = ""; public bool petWings = true;
            // the bag: gear pieces (one entry each), potions and crafting shards as stacks, worn gear in its four slots
            public List<string> bag = new(); public int potions = 3, bagCap = 40; public List<int> mats = new() { 0, 0, 0, 0, 0 };
            public string helm = "", armor = "", boots = "", acc = "";
            public int BagUsed => bag.Count + (potions > 0 ? 1 : 0) + mats.Count(m => m > 0); // equipped wings ("" = none) and whether the pet wears a pair too
            public string WeaponSkin(string weapon) { int i = wsKeys.IndexOf(weapon); return i < 0 ? null : wsVals[i]; }
            public void SetWeaponSkin(string weapon, string model) { int i = wsKeys.IndexOf(weapon); if (i < 0) { wsKeys.Add(weapon); wsVals.Add(model); } else wsVals[i] = model; }
        }
        // lobby try-on: shown on the hero in the lobby only, never taken into a match unless owned
        [System.NonSerialized] public string pvSword, pvWeaponSkin, pvWings, pvShield; [System.NonSerialized] public int pvSkin = -1;   // runtime only (a serialized string would come back as "")
        public void ClearPreview() { pvSword = pvWeaponSkin = pvWings = pvShield = null; pvSkin = -1; }
        // wings on show: a pair being tried on in the lobby, else the equipped pair if owned ("" = none)
        public string WingsShown => mode == Mode.Lobby && pvWings != null ? pvWings : Shop.Owns(ItemKind.Wings, save.wings) && Data.Wing(save.wings) != null ? save.wings : null;
        public Save save;
        public int Skin(string id) { int i = save.skinKeys.IndexOf(id); return i < 0 ? 0 : save.skinVals[i]; }
        public void SetSkin(string id, int v) { int i = save.skinKeys.IndexOf(id); if (i < 0) { save.skinKeys.Add(id); save.skinVals.Add(v); } else save.skinVals[i] = v; Persist(); }
        public void Persist() { PlayerPrefs.SetString("novaBlade", JsonUtility.ToJson(save)); PlayerPrefs.Save(); }
        public int MaxStage => Mathf.Max(Data.Themes.Length, Mathf.Min(99, save.best + 1));   // every stage is open from the start

        void Awake()
        {
            Balance.Load();   // Resources/Data/balance.json over the defaults in Data.cs
            if (Camera.main && !Camera.main.GetComponent<OcclusionFader>()) Camera.main.gameObject.AddComponent<OcclusionFader>();   // trees in the way turn see-through
            G = this;
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) QualitySettings.SetQualityLevel(0, true);   // phones (also mobile browsers): the Mobile URP asset
            else if (Camera.main && Camera.main.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var cd))
                cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;   // PC: the pipeline's 4x MSAA keeps edges sharp; FXAA would soften the whole frame
            save = JsonUtility.FromJson<Save>(PlayerPrefs.GetString("novaBlade", "{}")) ?? new Save();
            if (save.skinKeys == null) { save.skinKeys = new(); save.skinVals = new(); }
            save.owned ??= new(); save.ledger ??= new(); save.wsKeys ??= new(); save.wsVals ??= new();
            Fx.Init(transform);
            if (!cam) cam = Camera.main;
        }

        void Start()
        {
            hero = new GameObject("Hero").AddComponent<Hero>();
            pet = new GameObject("Pet").AddComponent<Pet>();
            ui = gameObject.AddComponent<Ui>();
            ShowLobby();
            var args = System.Environment.GetCommandLineArgs(); int ai = System.Array.IndexOf(args, "-novashot");
            if (ai >= 0 && ai + 1 < args.Length) StartCoroutine(AutoShot(args[ai + 1]));
            int ci = System.Array.IndexOf(args, "-clip"); if (ci >= 0 && ci + 1 < args.Length) StartCoroutine(Clip(args[ci + 1]));
            int kt = System.Array.IndexOf(args, "-skilltest"); if (kt >= 0 && kt + 1 < args.Length) StartCoroutine(SkillTest(args[kt + 1]));
            int p2 = System.Array.IndexOf(args, "-phase2test"); if (p2 >= 0 && p2 + 1 < args.Length) StartCoroutine(Phase2Test(args[p2 + 1]));
            int cs = System.Array.IndexOf(args, "-camstyle"); if (cs >= 0 && cs + 1 < args.Length) CamStyle = int.Parse(args[cs + 1]);
            int si = System.Array.IndexOf(args, "-stageshot"); if (si >= 0 && si + 2 < args.Length) StartCoroutine(StageShot(int.Parse(args[si + 1]), args[si + 2]));
            int xi = System.Array.IndexOf(args, "-slashtest"); if (xi >= 0 && xi + 1 < args.Length) StartCoroutine(SlashTest(args[xi + 1]));
            int wa = System.Array.IndexOf(args, "-wingall"); if (wa >= 0 && wa + 1 < args.Length) StartCoroutine(WingAll(args[wa + 1]));
            int wi = System.Array.IndexOf(args, "-wingtest"); if (wi >= 0 && wi + 1 < args.Length) StartCoroutine(WingTest(args[wi + 1]));
            int hi = System.Array.IndexOf(args, "-heroaudit"); if (hi >= 0 && hi + 1 < args.Length) StartCoroutine(HeroAudit(args[hi + 1]));
            int pk = System.Array.IndexOf(args, "-posecheck"); if (pk >= 0 && pk + 2 < args.Length) StartCoroutine(PoseCheck(args[pk + 1], args[pk + 2]));
            int nt = System.Array.IndexOf(args, "-newtest"); if (nt >= 0 && nt + 1 < args.Length) StartCoroutine(NewTest(args[nt + 1]));
            int bt = System.Array.IndexOf(args, "-bagtest"); if (bt >= 0 && bt + 1 < args.Length) StartCoroutine(BagTest(args[bt + 1]));
            int mt = System.Array.IndexOf(args, "-movetest"); if (mt >= 0 && mt + 1 < args.Length) StartCoroutine(MoveTest(args[mt + 1]));
            int wt = System.Array.IndexOf(args, "-weapontest"); if (wt >= 0 && wt + 1 < args.Length) StartCoroutine(WeaponTest(args[wt + 1]));
        }

        // Test mode (-skilltest <dir>): the class-reworked heroes cast each skill in a fight; a shot mid-cast of each, then quit.
        System.Collections.IEnumerator SkillTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            foreach (var id in new[] { "mage", "darkmage", "sorceress", "sunpriest", "monk", "elf" })
            {
                save.hero = id; save.best = 5; StartGame(1); yield return new WaitForSecondsRealtime(2.5f);
                for (int k = 0; k < 3; k++)
                {
                    hero.hp = hero.max; hero.mp = hero.mpMax; for (int c = 0; c < 3; c++) hero.cds[c] = 0;
                    ui.skP[k] = true; yield return new WaitForSecondsRealtime(k == 0 && id == "elf" ? .5f : .7f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{id}_{k}.png")); yield return new WaitForSecondsRealtime(1.3f);
                }
            }
            Application.Quit();
        }
        // Test mode (-phase2test <dir>): weapon mastery, affinity and the off-hand swap, checked and logged (phase2.txt), then quit.
        System.Collections.IEnumerator Phase2Test(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            var log = new System.Text.StringBuilder(); void Shot(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            save.hero = "warrior"; save.sword = "w_lava"; foreach (var w in new[] { "w_fantasy", "w_daggers" }) save.owned.Add(Shop.Key(ItemKind.Weapon, w));
            save.sword2 = "w_daggers"; save.mastKeys.Clear(); save.mastVals.Clear(); save.AddMastery("w_lava", 130);
            hero.Equip(); ui.SetTab("sword"); yield return new WaitForSecondsRealtime(1.5f); Shot("weapons_tab");
            log.AppendLine($"lava xp {save.Mastery("w_lava")} tier {Data.Tier(save.Mastery("w_lava"))} ({Data.MasteryName[Data.Tier(save.Mastery("w_lava"))]})");
            log.AppendLine($"warrior + sword affinity {Data.AffinityBonus("warrior", "sword")}, + dagger {Data.AffinityBonus("warrior", "dagger")}, mage + sword {Data.AffinityBonus("mage", "sword")}");
            log.AppendLine($"AtkPow lava (Expert + sword affinity) = {hero.AtkPow:0.0}  crit bonus {hero.CritBonus}");
            save.best = 5; StartGame(1); yield return new WaitForSecondsRealtime(2);
            int xp0 = save.Mastery("w_lava");
            for (float t = 0; t < 6; t += Time.unscaledDeltaTime) { if (Random.value < .1f) ui.atkP = true; hero.hp = Mathf.Max(hero.hp, hero.max * .5f); yield return null; }
            log.AppendLine($"kills {kills}, lava xp {xp0} -> {save.Mastery("w_lava")}");
            Shot("before_swap"); log.AppendLine($"in hand: {hero.sword.id}, off-hand: {hero.OffHand?.id}");
            ui.swapP = true; yield return new WaitForSecondsRealtime(.3f); Shot("after_swap");
            log.AppendLine($"after swap: {hero.sword.id} AtkPow {hero.AtkPow:0.0} (dagger, warrior second affinity)");
            yield return new WaitForSecondsRealtime(.8f); ui.swapP = true; yield return new WaitForSecondsRealtime(.3f); log.AppendLine($"swapped back: {hero.sword.id}");
            log.AppendLine($"balance loaded: dmg tiers {string.Join(",", Data.MasteryDmg)}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "phase2.txt"), log.ToString());
            Application.Quit();
        }
        // Test mode (-stageshot <stage> <dir>): fight one stage on autopilot (shots at 3 s and 7 s), then its boss, then quit.
        System.Collections.IEnumerator StageShot(int st, string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            save.hero = "warrior"; save.best = 9; StartGame(st);
            for (float t = 0, n = 0; t < 8; t += Time.unscaledDeltaTime)
            {
                if (Random.value < .08f) ui.atkP = true; ui.Joy = new Vector2(Mathf.Sin(t * .6f), Mathf.Cos(t * .4f)) * .7f; hero.hp = Mathf.Max(hero.hp, hero.max * .5f);
                if (n < 2 && t > 3 + n * 4) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"stage{st}_{n}.png")); n++; }
                yield return null;
            }
            ui.Joy = Vector2.zero; foreach (var f in foes) if (f) Destroy(f.gameObject); foes.Clear(); wave = Theme.waves.Length; SpawnBoss();
            yield return new WaitForSecondsRealtime(3.5f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"stage{st}_boss.png")); yield return new WaitForSecondsRealtime(.5f);
            Application.Quit();
        }
        // Test mode (-slashtest <dir>): each slash effect next to the hero in a stage, caught early and at its peak, then a
        // few seconds of real combo and skills, then quit.
        System.Collections.IEnumerator SlashTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            save.hero = "warrior"; save.best = 5; StartGame(3); yield return new WaitForSecondsRealtime(1.5f);
            foreach (var f in foes) if (f) Destroy(f.gameObject); foes.Clear();
            foreach (var id in new[] { "sx_basic", "sx_double", "sx_explode", "sx_multi", "sx_shiny", "sx_mesh" })
            {
                Fx.SlashFx(id, hero.Pos + Vector3.up * 1.05f + Main.Dir(hero.face) * 1.2f, hero.face, 1.2f);
                yield return new WaitForSecondsRealtime(.12f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, id + "_a.png"));
                yield return new WaitForSecondsRealtime(.25f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, id + "_b.png"));
                yield return new WaitForSecondsRealtime(1.4f);
            }
            Application.Quit();
        }
        // Test mode (-wingall <dir>): every hero wearing wings (the four pairs in turn) as the lobby first shows it, then
        // every pet wearing wings on the pet tab, then quit.
        System.Collections.IEnumerator WingAll(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            void Shot(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            save.petWings = true; int k = 0;
            foreach (var h in Data.Heroes)
            {
                save.hero = h.id; pvWings = Data.Wings[k++ % Data.Wings.Length].id; hero.Equip(); pet.Equip(); hero.PlaceLobby(); pet.PlaceLobby(); ui.SetTab("wings");
                ui.lobbyZoom = 0; ui.heroYaw = 0; yield return new WaitForSecondsRealtime(2.5f); Shot($"hero_{h.id}"); yield return new WaitForSecondsRealtime(.4f);
            }
            save.hero = "warrior"; k = 0;
            foreach (var p in Data.Pets)
            {
                save.pet = p.id; pvWings = Data.Wings[k++ % Data.Wings.Length].id; hero.Equip(); pet.Equip(); hero.PlaceLobby(); pet.PlaceLobby(); ui.SetTab("pet");
                ui.lobbyZoom = .3f; ui.petYaw = 0; yield return new WaitForSecondsRealtime(2.5f); Shot($"pet_{p.id}_front"); ui.petYaw = 180; yield return new WaitForSecondsRealtime(1.5f); Shot($"pet_{p.id}_back"); yield return new WaitForSecondsRealtime(.4f);
            }
            Application.Quit();
        }
        // Test mode (-wingtest <dir>): each pair of wings tried on in the lobby (back and side views, pet too), then a short
        // fight wearing them, then quit.
        System.Collections.IEnumerator WingTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            void Shot(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            ui.SetTab("wings"); save.hero = "sorceress"; save.petWings = true;
            foreach (var w in Data.Wings)
            {
                pvWings = w.id; hero.Equip(); pet.Equip(); hero.PlaceLobby(); pet.PlaceLobby(); ui.ShowLobby(); ui.SetTab("wings");
                ui.lobbyZoom = 0; ui.heroYaw = 0; yield return new WaitForSecondsRealtime(1.8f); Shot($"wing_{w.id}_front");   // what a player sees first
                ui.heroYaw = 200; yield return new WaitForSecondsRealtime(1.2f); Shot($"wing_{w.id}_back");
            }
            pvWings = null; save.wings = "fire"; save.owned.Add(Shop.Key(ItemKind.Wings, "fire")); save.best = 5; StartGame(3);
            for (float t = 0, n = 0; t < 8; t += Time.unscaledDeltaTime)
            {
                if (Random.value < .08f) ui.atkP = true; hero.hp = Mathf.Max(hero.hp, hero.max * .5f);
                if (n < 2 && t > 3 + n * 3) { Shot($"game_{n}"); n++; }
                yield return null;
            }
            Application.Quit();
        }
        // Test mode (-posecheck <hero> <dir>): the hero on the lobby stage close up, posed mid-clip in its run and attacks,
        // seen from four sides, then quit. For checking skinning (stretching cloth) and how the weapon sits.
        System.Collections.IEnumerator PoseCheck(string id, string dir)
        {
            System.IO.Directory.CreateDirectory(dir); save.wings = ""; save.shield = ""; save.sword = "w_lava"; yield return new WaitForSecondsRealtime(3);
            save.hero = id; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.lobbyZoom = .3f; yield return new WaitForSecondsRealtime(1);
            var an = hero.GetComponentInChildren<Animator>();
            var clips = an.runtimeAnimatorController.animationClips.GroupBy(c => c.name).Select(g => g.First()).ToList();
            foreach (var key in new[] { "show", "run", "slash", "spin" })
            {
                var c = key == "show" ? null : clips.FirstOrDefault(x => x.name.ToLower().Contains(key)); UnityEngine.Playables.PlayableGraph g = default;
                if (c) { var pc = UnityEngine.Animations.AnimationClipPlayable.Create(g = UnityEngine.Playables.PlayableGraph.Create(), c); UnityEngine.Animations.AnimationPlayableOutput.Create(g, "o", an).SetSourcePlayable(pc); pc.SetTime(c.length * .3f); pc.SetSpeed(0); g.Play(); }
                foreach (var yaw in new[] { 0, 90, 180, 270 })
                {
                    ui.heroYaw = yaw; yield return new WaitForSecondsRealtime(.6f);
                    ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{id}_{key}_{yaw}.png")); yield return null;
                }
                if (c) g.Destroy();
            }
            Application.Quit();
        }
        // Test mode (-newtest <dir>): the hero details screen, a walk across a valley bridge (heights logged), and the
        // see-through trees in a low-camera fight; shots and new.txt, then quit.
        System.Collections.IEnumerator NewTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            foreach (var h in new[] { "samurai", "sunpriest" })
            {
                save.hero = h; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); yield return new WaitForSecondsRealtime(1);
                ui.TestSkills(); yield return new WaitForSecondsRealtime(.6f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"skills_{h}.png")); yield return new WaitForSecondsRealtime(.4f);
            }
            CamStyle = 2; save.hero = "warrior";
            int vi = System.Array.FindIndex(stageRoots, t => t && t.name == "Stage_valley") + 1;
            StartGame(vi); yield return new WaitForSecondsRealtime(2);
            foreach (var f in G.Live.ToList()) if (f) f.gameObject.SetActive(false);
            var decks = FindObjectsByType<BridgeDeck>(FindObjectsSortMode.None);
            log.AppendLine($"valley stage {vi}: decks {decks.Length} valleyRiver={River.Valley}");
            foreach (var d in decks)
            {
                log.AppendLine($"deck at {d.center} len {d.halfLen * 2:0.0} wid {d.halfWid * 2:0.0} h {string.Join(",", d.h.Select(y => y.ToString("0.00")))}");
                var start = d.center - d.axis * (d.halfLen + 1.5f); hero.transform.position = new Vector3(start.x, 0, start.z);
                float maxY = -9, minY = 9;
                for (float w = 0; w < 2.2f; w += Time.unscaledDeltaTime)
                {
                    // carried along the bridge at walking pace; the hero's own ground logic sets the height
                    var q = start + d.axis * (w / 2.2f) * (2 * d.halfLen + 3); var p = hero.transform.position; p.x = q.x; p.z = q.z; hero.transform.position = p;
                    maxY = Mathf.Max(maxY, p.y); minY = Mathf.Min(minY, p.y);
                    if (w > 1.1f && w < 1.1f + Time.unscaledDeltaTime * 1.5f) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"bridge_{System.Array.IndexOf(decks, d)}.png"));
                    yield return null;
                }
                ui.Joy = Vector2.zero; log.AppendLine($"  walked: hero y {minY:0.00}..{maxY:0.00}, ends at {hero.transform.position}");
                yield return new WaitForSecondsRealtime(.3f);
            }
            // the river away from the bridges: wading
            var wz = -3 + Mathf.Sin(0 * .09f) * 4.5f; hero.transform.position = new Vector3(0, 0, wz); yield return new WaitForSecondsRealtime(.6f);
            log.AppendLine($"in the river at x=0: ground {River.Ground(0, wz):0.00} hero y {hero.transform.position.y:0.00} inWater {River.InWater(0, wz)}");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "wade.png")); yield return new WaitForSecondsRealtime(.5f);
            // a tree between the camera and the hero: placed just behind one, seen from the low camera
            var fd = Camera.main.GetComponent<OcclusionFader>();
            var trees = stageRoots[vi - 1].GetComponentsInChildren<MeshRenderer>().Where(r => r.name.ToLower().Contains("tree") && r.name.Contains("LOD0") && r.bounds.size.y > 3 && new Vector2(r.bounds.center.x, r.bounds.center.z).magnitude < 20).Take(40).ToList();
            var cdir = Vector3.ProjectOnPlane(Camera.main.transform.position - hero.transform.position, Vector3.up).normalized;
            for (int k = 0; k < 2 && k < trees.Count; k++)
            {
                var tp = trees[k * 7 % trees.Count].bounds.center; var hp2 = tp - cdir * 2.2f; hero.transform.position = new Vector3(hp2.x, 0, hp2.z); yield return new WaitForSecondsRealtime(1.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"behindtree_{k}.png")); yield return null;
                log.AppendLine($"behind tree {k}: faded {(fd ? fd.FadedCount : -1)}  {(fd ? fd.Report : "")}");
            }
            // trees: the forest, low camera, monsters alive
            ShowLobby(); yield return new WaitForSecondsRealtime(.5f); StartGame(1); yield return new WaitForSecondsRealtime(3);
            var fader = Camera.main.GetComponent<OcclusionFader>();
            for (int k = 0; k < 3; k++)
            {
                hero.transform.position = new Vector3(-14 + k * 14, 0, 14 - k * 4); yield return new WaitForSecondsRealtime(1.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"trees_{k}.png")); yield return null;
                log.AppendLine($"trees shot {k}: fader {(fader ? fader.FadedCount : -1)} faded renderers");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "new.txt"), log.ToString());
            Application.Quit();
        }
        // Test mode (-bagtest <dir>): a bag with some of everything, the bag screen (a piece chosen, then worn), a boss's loot
        // dropping and being picked up in a fight, a potion drunk; shots and bag.txt, then quit.
        System.Collections.IEnumerator BagTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            save.hero = "samurai"; save.bag.Clear(); save.helm = save.armor = save.boots = save.acc = "";
            foreach (var id in new[] { "helm_iron", "armor_knight", "boots_leather", "ring_shadow", "helm_dragon", "armor_leather", "boots_knight", "ring_leather" }) save.bag.Add(id);
            save.potions = 5; save.mats = new List<int> { 3, 1, 0, 2, 0 }; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); yield return new WaitForSecondsRealtime(1);
            ui.TestBag("g:4"); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "bag_select.png")); yield return new WaitForSecondsRealtime(.4f);
            // wear four pieces the way the buttons do
            save.helm = "helm_dragon"; save.armor = "armor_knight"; save.boots = "boots_knight"; save.acc = "ring_shadow"; save.bag.RemoveAll(i => i is "helm_dragon" or "armor_knight" or "boots_knight" or "ring_shadow");
            ui.TestBag("worn:Armor"); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "bag_worn.png")); yield return new WaitForSecondsRealtime(.4f);
            var gb = Items.Bonus(save); log.AppendLine($"gear bonus hp {gb.hp} def {gb.def:0.000} spd {gb.spd:0.00} crit {gb.crit:0.000} atk {gb.atk:0.000}");
            ui.ShowBag(false); StartGame(1); yield return new WaitForSecondsRealtime(2);
            log.AppendLine($"in fight: max hp {hero.max} (base {hero.def.hp}) atk {hero.AtkPow:0}");
            int bagBefore = save.bag.Count, potBefore = save.potions;
            DropLoot(hero.Pos + new Vector3(2.5f, 0, 0), true, 10); yield return new WaitForSecondsRealtime(.9f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "loot_drop.png")); yield return new WaitForSecondsRealtime(4);
            log.AppendLine($"after boss loot: bag {bagBefore} -> {save.bag.Count}, potions {potBefore} -> {save.potions}");
            hero.hp = hero.max * .4f; float h0 = hero.hp; hero.UsePotion(); yield return new WaitForSecondsRealtime(.4f);
            log.AppendLine($"potion: hp {h0:0} -> {hero.hp:0}, potions left {save.potions}");
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "potion.png")); yield return new WaitForSecondsRealtime(.5f);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "bag.txt"), log.ToString());
            Application.Quit();
        }
        // Test mode (-movetest <dir>): every hero runs forward on the joystick; logs the playing state, its speed, how far the
        // body moves vs how fast the feet sweep, and the lowest foot above the ground (feet should touch: ~0), then quit.
        System.Collections.IEnumerator MoveTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            var log = new System.Text.StringBuilder();
            foreach (var d in Data.Heroes)
            {
                save.hero = d.id; save.best = 5; StartGame(1); yield return new WaitForSecondsRealtime(2);
                foreach (var f in G.Live.ToList()) if (f) f.gameObject.SetActive(false);
                var an = hero.GetComponentInChildren<Animator>();
                Transform B(HumanBodyBones b) => an && an.isHuman ? an.GetBoneTransform(b) : null;
                var lf = B(HumanBodyBones.LeftFoot); var rf = B(HumanBodyBones.RightFoot); var hips = B(HumanBodyBones.Hips);
                float idleHip = hips ? hips.position.y - hero.transform.position.y : 0;
                ui.Joy = new Vector2(0, 1); yield return new WaitForSecondsRealtime(.6f);
                var slides = new List<float>(); Transform lastPl = null; Vector3 lastP = default;
                float minF = 99, maxF = -99, minLow = 99, t0 = Time.time; var p0 = hero.transform.position; float hipSum = 0; int n = 0; string st = "-"; float spd = 0;
                for (float w = 0; w < 1.6f; w += Time.unscaledDeltaTime)
                {
                    float gy = River.Ground(hero.transform.position.x, hero.transform.position.z);
                    if (lf && rf) { float lo = Mathf.Min(lf.position.y, rf.position.y) - gy; minLow = Mathf.Min(minLow, lo); maxF = Mathf.Max(maxF, lo);
                        var pl = lf.position.y < rf.position.y ? lf : rf; if (pl == lastPl && Time.deltaTime > 0) slides.Add(Main.Flat(pl.position - lastP).magnitude / Time.deltaTime); lastPl = pl; lastP = pl.position; }
                    if (hips) { hipSum += hips.position.y - hero.transform.position.y; n++; }
                    if (an) { var ci = an.GetCurrentAnimatorClipInfo(0); st = ci.Length > 0 ? ci[0].clip.name : "-"; spd = an.GetFloat("Speed"); }
                    if (w > .7f && w < .7f + Time.unscaledDeltaTime * 1.5f) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, d.id + "_run.png"));
                    yield return null;
                }
                float dist = Main.Flat(hero.transform.position - p0).magnitude, v = dist / (Time.time - t0);
                ui.Joy = Vector2.zero;
                if (an) { var si = an.GetCurrentAnimatorStateInfo(0); log.AppendLine($"  diag en={an.enabled} act={an.isActiveAndEnabled} spd={an.speed} ctrl={(an.runtimeAnimatorController ? an.runtimeAnimatorController.name : "null")} run={si.IsName("run")} idle={si.IsName("idle")} nt={si.normalizedTime:0.00} len={si.length:0.00} clips={an.GetCurrentAnimatorClipInfoCount(0)} cull={an.cullingMode} ts={Time.timeScale} human={an.isHuman} avatar={(an.avatar ? an.avatar.name + "/" + an.avatar.isValid : "none")} anims={hero.GetComponentsInChildren<Animator>(true).Length}"); }
                log.AppendLine($"{d.id}	set={hero.animSet ?? "-"}	clip={st}	animSpeed={spd:0.00}	move={v:0.00}m/s	gaitClip={Catalog.I.Clip(hero.animSet, "run").speed:0.00}	scale={hero.transform.lossyScale.y:0.00}	lowestFoot={minLow:0.00}..{maxF:0.00}	hipIdle={idleHip:0.00}	hipRun={(n > 0 ? hipSum / n : 0):0.00}	plantedFootSpeed={(slides.Count > 0 ? slides.OrderBy(x => x).ElementAt(slides.Count / 10) : -1):0.00}m/s");
                ShowLobby(); yield return new WaitForSecondsRealtime(.8f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "move.txt"), log.ToString());
            Application.Quit();
        }
        // Test mode (-weapontest <dir>): each class weapon on a hero of its class, in the lobby (three-quarter) and mid-attack, then quit.
        System.Collections.IEnumerator WeaponTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            var log = new System.Text.StringBuilder();
            var only = System.Environment.GetCommandLineArgs().Contains("-round2");
            var list = new[] { ("warrior", "w_greataxe", ""), ("samurai", "w_katana", ""), ("samurai", "w_kunai", ""), ("sunpriest", "w_mace", ""), ("sunpriest", "w_holystaff", ""), ("mage", "w_arcanestaff", ""), ("sorceress", "w_orb", ""),
                ("harpy", "w_longbow", ""), ("harpy", "w_crossbow", ""), ("elf", "w_longbow", ""), ("monk", "w_gauntlet", ""), ("monk", "w_claws", ""), ("warrior", "w_lava", "sh_knight"), ("sunpriest", "w_mace", "sh_knight") };
            foreach (var (h, w, sh) in only ? list.Skip(7) : list)
            {
                save.hero = h; save.sword = w; save.owned.Add(Shop.Key(ItemKind.Weapon, w)); save.shield = sh; if (sh != "") save.owned.Add(Shop.Key(ItemKind.Shield, sh));
                if (only) save.wings = "";
                hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.lobbyZoom = only ? 0 : .7f; ui.heroYaw = 35; yield return new WaitForSecondsRealtime(1.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{h}_{w}{sh}_lobby.png"));
                if (only) { ui.heroYaw = -70; yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{h}_{w}{sh}_left.png")); }
                log.AppendLine($"{h}	{w}	equipped={hero.sword.id}	aff={Data.AffinityBonus(hero.def.cls, hero.sword.type)}	pow={hero.AtkPow:0}");
                yield return new WaitForSecondsRealtime(.4f);
                save.best = 5; StartGame(1); yield return new WaitForSecondsRealtime(2.5f);
                ui.atkP = true; yield return new WaitForSecondsRealtime(.35f); ui.atkP = false;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{h}_{w}{sh}_fight.png")); yield return new WaitForSecondsRealtime(.8f);
                ShowLobby(); yield return new WaitForSecondsRealtime(1);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "weapons.txt"), log.ToString());
            Application.Quit();
        }
        // Test mode (-heroaudit <dir>): every hero on the lobby stage, front and three-quarter shots, and how far the
        // hand and head move over a second (0 = frozen), then quit.
        System.Collections.IEnumerator HeroAudit(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            var log = new System.Text.StringBuilder();
            foreach (var d in Data.Heroes)
            {
                save.hero = d.id; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.lobbyZoom = .55f; ui.heroYaw = 0;
                yield return new WaitForSecondsRealtime(1.5f);
                var an = hero.GetComponentInChildren<Animator>();
                Transform B(HumanBodyBones b) => an && an.isHuman ? an.GetBoneTransform(b) : null;
                var bones = new[] { B(HumanBodyBones.Head), B(HumanBodyBones.RightHand), B(HumanBodyBones.LeftHand), B(HumanBodyBones.Spine) };
                var p0 = bones.Select(t => t ? t.position : Vector3.zero).ToArray();
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, d.id + "_a.png"));
                float mv = 0;   // furthest any of these bones gets from where it started, over 1.5 s
                for (float w = 0; w < 1.5f; w += Time.unscaledDeltaTime) { for (int i = 0; i < bones.Length; i++) if (bones[i]) mv = Mathf.Max(mv, (bones[i].position - p0[i]).magnitude); yield return null; }
                ui.heroYaw = 40; yield return new WaitForSecondsRealtime(.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, d.id + "_b.png"));
                var st = an ? an.GetCurrentAnimatorClipInfo(0) : null;
                log.AppendLine($"{d.id}\tanim={(an ? an.enabled.ToString() : "none")}\tset={hero.animSet ?? "-"}\tclip={(st != null && st.Length > 0 ? st[0].clip.name : "-")}\tmove={mv:0.000}m");
                yield return new WaitForSecondsRealtime(.4f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "audit.txt"), log.ToString());
            Application.Quit();
        }
        // Test mode (-novashot <dir>): screenshots of the lobby and each stage while the hero fights on autopilot, then quit.
        // -inputtest: drives the real Input System (arrow keys, right-click) in a stage and logs whether the hero responds
        // -clip <dir>: a ~30 s showcase recorded frame by frame at a fixed 30 fps (frames as JPG, joined into a video by ffmpeg)
        int clipFrame; string clipDir; bool clipOn;
        System.Collections.IEnumerator ClipRec()
        {
            while (clipOn)
            {
                yield return new WaitForEndOfFrame();
                var t = ScreenCapture.CaptureScreenshotAsTexture(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(clipDir, $"f{clipFrame++:00000}.jpg"), t.EncodeToJPG(92)); Destroy(t);
            }
        }
        System.Collections.IEnumerator ClipFight(float secs)
        {   // autopilot: walk at the nearest enemy, swing, cast a skill now and then, never die
            for (float t = 0; t < secs; t += 1 / 30f)
            {
                var near = foes.Where(f => f && f.alive && f.team != 0).OrderBy(f => (f.Pos - hero.Pos).sqrMagnitude).FirstOrDefault();
                if (near) { var d = Flat(near.Pos - hero.Pos); ui.Joy = d.magnitude > 2.6f ? new Vector2(-d.x, d.z).normalized : Vector2.zero; }
                if (Random.value < .12f) ui.atkP = true; if (Random.value < .03f) ui.skP[Random.Range(0, 3)] = true; if (Random.value < .006f) ui.dodgeP = true;
                hero.hp = Mathf.Max(hero.hp, hero.max * .6f); hero.mp = Mathf.Max(hero.mp, 40);
                yield return null;
            }
            ui.Joy = Vector2.zero;
        }
        System.Collections.IEnumerator Clip(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); clipDir = dir; Time.captureFramerate = 30;
            yield return new WaitForSecondsRealtime(4);
            save.hero = "sorceress"; save.pet = "candle"; hero.Equip(); pet.Equip(); hero.PlaceLobby(); pet.PlaceLobby(); ui.ShowLobby();
            clipOn = true; StartCoroutine(ClipRec());
            for (int i = 0; i < 75; i++) { ui.heroYaw = Mathf.Sin(i / 75f * Mathf.PI) * 70; ui.lobbyZoom = Mathf.Clamp01(i / 75f) * .55f; yield return null; }   // lobby: turn and zoom on the hero
            ui.lobbyZoom = 0; ui.heroYaw = 0; ui.TestFilter("mage"); for (int i = 0; i < 30; i++) yield return null; ui.TestFilter("all");
            foreach (var (st, h) in new[] { (3, "spartan"), (4, "elf"), (1, "ninja") }) { save.hero = h; save.best = 5; StartGame(st); yield return ClipFight(st == 1 ? 6 : 7.5f); }

            save.hero = "warrior"; StartSoul(); yield return ClipFight(7);
            clipOn = false; yield return null; Time.captureFramerate = 0; Application.Quit();
        }
        System.Collections.IEnumerator InputTest()
        {
            save.hero = "warrior"; StartGame(1); yield return new WaitForSecondsRealtime(2);
            var kb = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>(); var ms = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            Debug.Log($"[InputTest] webPc={ui.WebPc} kb.current={UnityEngine.InputSystem.Keyboard.current == kb} mode={mode} paused={paused} ts={Time.timeScale} state={hero.state}");
            var p0 = hero.Pos;
            for (int i = 0; i < 40; i++) { UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.RightArrow)); yield return null; }
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState()); yield return null;
            Debug.Log($"[InputTest] arrows moved {(hero.Pos - p0).magnitude:0.00} m");
            var target = hero.Pos + new Vector3(-4, 0, 0); var sp = (Vector2)cam.WorldToScreenPoint(target); p0 = hero.Pos;
            var st = new UnityEngine.InputSystem.LowLevel.MouseState { position = sp }; UnityEngine.InputSystem.InputSystem.QueueStateEvent(ms, st); yield return null;
            Debug.Log($"[InputTest] pointer over ui={ui.PointerOverUi} pick={ui.PickName()} at {sp}");
            st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, true); for (int i = 0; i < 6; i++) { UnityEngine.InputSystem.InputSystem.QueueStateEvent(ms, st); yield return null; }
            st = st.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Right, false); UnityEngine.InputSystem.InputSystem.QueueStateEvent(ms, st);
            yield return new WaitForSecondsRealtime(1.2f);
            Debug.Log($"[InputTest] right-click moved {(hero.Pos - p0).magnitude:0.00} m");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.Q)); yield return null; yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(kb, new UnityEngine.InputSystem.LowLevel.KeyboardState()); yield return new WaitForSecondsRealtime(.3f);
            Debug.Log($"[InputTest] Q skill cooldown {hero.cds[0]:0.0} state={hero.state}");
        }
        System.Collections.IEnumerator AutoShot(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            yield return new WaitForSecondsRealtime(3);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby.png")); yield return new WaitForSecondsRealtime(1);
            ui.lobbyZoom = .85f; ui.heroYaw = 35; yield return new WaitForSecondsRealtime(1.5f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby_zoom.png")); yield return new WaitForSecondsRealtime(.5f);
            ui.SetTab("pet"); ui.lobbyZoom = .5f; ui.petYaw = -40; yield return new WaitForSecondsRealtime(1.5f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby_pet.png")); yield return new WaitForSecondsRealtime(.5f);
            ui.SetTab("hero"); ui.lobbyZoom = 0; ui.heroYaw = ui.petYaw = 0;
            ui.TestFilter("mage"); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby_filter.png")); yield return new WaitForSecondsRealtime(.5f); ui.TestFilter("all");
            ui.SetTab("stage"); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby_stages.png")); yield return new WaitForSecondsRealtime(.5f); ui.SetTab("hero");
            {   // design rule: cosmetics never change combat stats
                save.sword = Data.Swords[0].id; hero.Equip(); float pow = hero.AtkPow, len = hero.sword.len, spd = hero.sword.spd;
                pvWeaponSkin = "w_gold2"; pvSkin = 3; hero.Equip(); bool ok = Mathf.Approximately(pow, hero.AtkPow) && hero.sword.len == len && hero.sword.spd == spd && hero.ModelId == "w_gold2";
                ClearPreview(); hero.Equip(); Debug.Log("[Check] skins keep combat stats: " + (ok ? "OK" : "FAILED"));
            }
            string[] heroes = { "spartan", "ninja", "darkmage", "sorceress", "elf", "monk" };   // stages 5–6 are the newest
            if (System.Environment.GetCommandLineArgs().Contains("-inputtest")) { yield return InputTest(); Application.Quit(); yield break; }
            foreach (int st in new[] { 4, 3 })
            {
                save.hero = heroes[st % heroes.Length]; save.best = 5; StartGame(st);
                float t = 0; int shot = 0;
                while (t < 10)
                {
                    float dt = Time.unscaledDeltaTime; t += dt;
                    if (Random.value < .08f) ui.atkP = true;
                    if (Random.value < .025f) ui.skP[Random.Range(0, 3)] = true;
                    hero.hp = Mathf.Max(hero.hp, hero.max * .5f);
                    if (shot < 2 && t > 4 + shot * 4) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"stage{st}_{shot}.png")); shot++; }
                    yield return null;
                }
            }
            foreach (int st in new[] { 1, 5 })
            {   // the rigged bosses: skip straight to the boss and catch a wind-up and a hit
                save.hero = "monk"; save.best = 5; StartGame(st); yield return null;
                foreach (var f in foes) if (f) Destroy(f.gameObject); foes.Clear(); wave = Theme.waves.Length; SpawnBoss();
                for (float bt = 0, bs = 0; bt < 9; bt += Time.unscaledDeltaTime)
                {
                    if (Random.value < .08f) ui.atkP = true; hero.hp = Mathf.Max(hero.hp, hero.max * .5f);
                    if (bs < 2 && bt > 3 + bs * 4) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"boss{st}_{bs}.png")); bs++; }
                    yield return null;
                }
            }
            StartLane(3, 3);   // three-lane battle on autopilot
            int ls = 0;
            for (float lt = 0; lt < 30; lt += Time.unscaledDeltaTime)
            {
                if (Random.value < .1f) ui.atkP = true; if (Random.value < .01f) ui.skP[Random.Range(0, 3)] = true;
                ui.Joy = new Vector2(0, -.6f); hero.hp = Mathf.Max(hero.hp, hero.max * .5f);
                if (ls == 0 && lt > 8) { ls = 1; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lane0.png")); }
                if (ls == 1 && lt > 22) { ls = 2; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lane1.png")); }
                yield return null;
            }
            // Soulslike duel on autopilot: lock on, light/heavy attacks, block and dodge
            save.hero = "warrior"; save.stage = 2; StartSoul(); yield return new WaitForSecondsRealtime(1.5f);
            ui.lockP = true; int ss = 0;
            for (float st2 = 0; st2 < 24; st2 += Time.unscaledDeltaTime)
            {
                float r = Random.value; ui.Joy = new Vector2(Mathf.Sin(st2 * .7f) * .5f, -.4f);
                if (r < .05f) ui.atkP = true; else if (r < .065f) ui.heavyP = true; else if (r < .075f) ui.dodgeP = true;
                ui.blockHold = Mathf.Repeat(st2, 6) > 5;
                hero.hp = Mathf.Max(hero.hp, hero.max * .5f);
                if (ss < 3 && st2 > 5 + ss * 7) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"soul{ss}.png")); ss++; }
                yield return null;
            }
            Debug.Log($"[Check] soul mode: boss hp {(boss ? boss.hp : 0):0}/{(boss ? boss.maxHp : 0):0} poise {(boss ? boss.poise : 0):0} stamina {(hero.soul ? hero.soul.stamina : -1):0}");
            Application.Quit();
        }

        // ---------- stages ----------
        public void SetTheme(int i)
        {
            LobbyNight.Set(false);
            for (int k = 0; k < stageRoots.Length; k++) if (stageRoots[k]) stageRoots[k].gameObject.SetActive(k == i);
            River.Active = stageRoots[i] && stageRoots[i].name == "Stage_river"; River.Valley = stageRoots[i] && stageRoots[i].name == "Stage_valley";
            var look = stageRoots[i] ? stageRoots[i].GetComponent<StageLook>() : null; stageLook = look;
            if (look) look.Apply();
            Obstacles.Use(stageRoots[i]);
        }

        public void ShowLobby()
        {
            ClearWorld(); mode = Mode.Lobby; paused = false; Time.timeScale = 1; lane = false; soul = false; if (hero && hero.soul) hero.soul.End();
            stage = Mathf.Clamp(save.stage, 1, MaxStage);
            SetTheme(Data.ThemeOf(1)); LobbyNight.Set(true);   // lobby backdrop: the forest at night, whatever stage is picked
            hero.Equip(); pet.Equip();
            hero.PlaceLobby(); pet.PlaceLobby();
            ui.ShowLobby();
        }

        public void StartGame(int st, bool carry = false)
        {
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = false; soul = false; ClearPreview();
            stage = st; save.stage = st; Persist(); SetTheme(Data.ThemeOf(st));
            if (!carry) { kills = 0; gold = 0; time = 0; banked = 0; }
            between = 0; wave = 0;
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame();
            camPos = new Vector3(0, 9, 14);
            if (River.Active) StartCoroutine(After(.1f, () => ui.Banner(Loc.S("ลุยน้ำได้ แต่จะช้าลง", "You can wade, but water slows you"), Loc.S("ข้ามสะพานไม้เพื่อความเร็ว", "Cross the bridges to keep your speed"))));
            ui.ShowHud();
            StartWave(0);
        }

        void StartWave(int k)
        {
            wave = k; queue.Clear();
            var list = new List<string>();
            foreach (var (f, n) in Theme.waves[k]) for (int i = 0; i < n + (stage - 1) / 2; i++) list.Add(f);
            foreach (var f in list.OrderBy(_ => Random.value)) queue.Enqueue(f);
            queueT = .6f;
            ui.Banner(Loc.S($"คลื่นที่ {k + 1}", $"Wave {k + 1}"), Loc.S($"ด่าน {stage} · {Theme.name}", $"Stage {stage} · {Theme.name}"));
        }

        void UpdWaves(float dt)
        {
            if (queue.Count > 0)
            {
                queueT -= dt; if (queueT > 0) return; queueT = .45f;
                float a = Random.Range(0, Mathf.PI * 2), r = Random.Range(12f, 18f);
                var p = hero.Pos + new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)) * r; float rr = p.magnitude; if (rr > Data.ArenaR - 1) p *= (Data.ArenaR - 1) / rr;
                SpawnFoe(queue.Dequeue(), p); return;
            }
            if (boss) return;
            if (!foes.Any(f => f.alive))
            {
                between += dt; if (between < 1.8f) return; between = 0;
                if (wave + 1 < Theme.waves.Length) StartWave(wave + 1);
                else { wave = Theme.waves.Length; ui.Banner(Theme.boss.name, Loc.S("บอสปรากฏตัว!", "The boss appears!")); SpawnBoss(); }
            }
        }

        public Foe SpawnFoe(string id, Vector3 p) => SpawnUnit(Data.Foe(id), p, 1);
        public Foe SpawnUnit(FoeDef def, Vector3 p, int team)
        {
            var f = new GameObject($"Unit_{def.id}_{team}").AddComponent<Foe>(); f.Init(def, p, null, team); foes.Add(f); return f;
        }
        void SpawnBoss()
        {
            var f = new GameObject("Boss").AddComponent<Foe>(); f.Init(null, new Vector3(0, 0, -14), Theme.boss); foes.Add(f); boss = f;
            ui.ShowBoss(Loc.S($"{Theme.boss.name} · ด่าน {stage}", $"{Theme.boss.name} · Stage {stage}")); shake = .4f;
        }

        public void ClearWorld()
        {
            foreach (var f in foes) if (f) Destroy(f.gameObject); foes.Clear(); boss = null;
            foreach (var s in shots) if (s) Destroy(s.gameObject); shots.Clear();
            foreach (var d in drops) if (d.go) Destroy(d.go); drops.Clear();
            queue.Clear(); Fx.Clear(); ui?.ClearNums();
        }

        public void Finish(bool win)
        {
            if (mode != Mode.Game) return;
            mode = Mode.Result; save.gold += gold - banked; banked = gold; if (hero.soul) hero.soul.End();
            if (win) save.best = Mathf.Max(save.best, stage);
            Persist();
            ui.ShowResult(win);
        }

        // ---------- combat helpers ----------
        public IEnumerable<Foe> Live => foes.Where(f => f && f.alive && !f.spawning && f.team == 1);   // what the player and pet can hit
        public Foe Nearest(Vector3 p, float maxD = 99)
        {
            Foe best = null; float bd = maxD;
            foreach (var f in Live) { float d = Flat(f.Pos - p).magnitude - f.rad; if (d < bd) { bd = d; best = f; } }
            return best;
        }
        public static Vector3 Flat(Vector3 v) { v.y = 0; return v; }
        public static float Ang(Vector3 dir) => Mathf.Atan2(dir.x, dir.z);
        public static Vector3 Dir(float a) => new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
        public static float AngDiff(float a, float b) { float d = (b - a) % (Mathf.PI * 2); if (d > Mathf.PI) d -= Mathf.PI * 2; if (d < -Mathf.PI) d += Mathf.PI * 2; return d; }

        public class Hit { public float face, half = 9, kb, poise; public bool melee, byFoe; public float slow; public Color? col; public HashSet<Foe> skip; public float steal; }
        public int HitArea(Vector3 c, float r, float dmg, Hit o = null)
        {
            o ??= new Hit(); int n = 0;
            foreach (var f in Live.ToList())
            {
                var d = Flat(f.Pos - c); float dist = d.magnitude; if (dist > r + f.rad) continue;
                if (o.half < Mathf.PI && dist > .6f && Mathf.Abs(AngDiff(o.face, Ang(d))) > o.half) continue;
                if (o.skip != null) { if (o.skip.Contains(f)) continue; o.skip.Add(f); }
                f.Damage(dmg, c, o); n++;
            }
            return n;
        }

        public void Number(Vector3 p, string txt, string cls) => ui?.Number(p, txt, cls);

        // ---------- drops ----------
        class Drop { public GameObject go; public bool orb; public int v; public Vector3 p, vel; public float t; public string item; }
        // an item on the ground: a glowing gem in its rarity colour with a beam of light, picked up by walking over it
        void AddItemDrop(Vector3 p, string id)
        {
            var d = Items.Get(id); if (d == null) return; var c = Items.RarityColor(d.rarity);
            var go = new GameObject("item_" + id); go.AddComponent<MeshFilter>().sharedMesh = SphereMesh();
            var m = new Material(Catalog.I.glowMat); m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 0); m.SetFloat("_UseVC", 0); m.SetColor("_Color", c * 2.2f);
            go.AddComponent<MeshRenderer>().sharedMaterial = m; go.transform.localScale = new Vector3(.34f, .42f, .34f);
            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(beam.GetComponent<Collider>()); beam.transform.SetParent(go.transform, false);
            beam.transform.localPosition = new Vector3(0, 3.2f, 0); beam.transform.localScale = new Vector3(.18f, 3.2f, .18f);
            var bm = new Material(Catalog.I.glowMat); bm.SetColor("_Color", new Color(c.r, c.g, c.b, .35f)); beam.GetComponent<Renderer>().sharedMaterial = bm;
            drops.Add(new Drop { go = go, item = id, p = p + Vector3.up * .6f, vel = new Vector3(Random.Range(-2f, 2f), Random.Range(4f, 6f), Random.Range(-2f, 2f)) });
        }
        // into the bag (potions and shards stack); a full bag turns the piece into gold
        public bool AddItem(string id)
        {
            var d = Items.Get(id); if (d == null) return false;
            if (d.slot == Slot.Potion) save.potions++;
            else if (d.slot == Slot.Material) save.mats[d.rarity]++;
            else if (save.BagUsed >= save.bagCap) { gold += d.Sell; Number(hero.Pos + Vector3.up * 2.2f, Loc.S("กระเป๋าเต็ม +" + d.Sell, "Bag full +" + d.Sell), "gold"); return false; }
            else save.bag.Add(id);
            return true;
        }
        static Mesh coinMesh; static Material coinMat, orbMat;
        public void DropLoot(Vector3 p, bool isBoss, int g)
        {
            int n = isBoss ? 24 : Random.Range(1, 4);
            for (int i = 0; i < n; i++) AddDrop(p, false, isBoss ? 8 : Mathf.CeilToInt(g / 2f));
            if (Random.value < (isBoss ? 1 : .12f)) AddDrop(p, true, 0);
            // items: one monster in eight drops something, a boss always drops two pieces and a potion
            if (isBoss) { AddItemDrop(p, Items.Roll(true, save.stage)); AddItemDrop(p, Items.Roll(true, save.stage)); AddItemDrop(p, "potion"); }
            else if (Random.value < .13f) AddItemDrop(p, Items.Roll(false, save.stage));
        }
        void AddDrop(Vector3 p, bool orb, int v)
        {
            if (!coinMesh)
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); coinMesh = c.GetComponent<MeshFilter>().sharedMesh; Destroy(c);
                coinMat = new Material(Catalog.I.glowMat); coinMat.SetFloat("_SrcBlend", 1); coinMat.SetFloat("_DstBlend", 0); coinMat.SetFloat("_UseVC", 0); coinMat.SetColor("_Color", new Color(1.6f, 1.1f, .3f));
                orbMat = new Material(coinMat); orbMat.SetColor("_Color", new Color(.4f, 2.6f, .8f));
            }
            var go = new GameObject(orb ? "orb" : "coin"); go.AddComponent<MeshFilter>().sharedMesh = orb ? SphereMesh() : coinMesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = orb ? orbMat : coinMat;
            go.transform.localScale = orb ? Vector3.one * .3f : new Vector3(.28f, .02f, .28f);
            drops.Add(new Drop { go = go, orb = orb, v = v, p = p + Vector3.up * .6f, vel = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 6f), Random.Range(-2.5f, 2.5f)) });
        }
        static Mesh sphere; static Mesh SphereMesh() { if (!sphere) { var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = s.GetComponent<MeshFilter>().sharedMesh; Destroy(s); } return sphere; }

        void UpdDrops(float dt)
        {
            float mag = pet.def != null && pet.def.gold ? 8 : 4;
            for (int i = drops.Count - 1; i >= 0; i--)
            {
                var d = drops[i]; d.t += dt;
                if (d.p.y > .25f || d.vel.y > 0) { d.vel.y -= 18 * dt; d.p += d.vel * dt; if (d.p.y < .25f) { d.p.y = .25f; d.vel = Mathf.Abs(d.vel.y) > 2 ? new Vector3(d.vel.x * .5f, -d.vel.y * .35f, d.vel.z * .5f) : Vector3.zero; } }
                var to = Flat(hero.Pos - d.p); float dd = to.magnitude;
                if (d.t > .5f && dd < mag && !hero.dead) { float k = Mathf.Min(1, dt * (12 / Mathf.Max(dd, .5f))); d.p += new Vector3(to.x * k, (1 - d.p.y) * k, to.z * k); }
                bool dead = d.t > (d.item != null ? 60 : 25);
                if (d.t > .5f && dd < .6f)
                {
                    dead = true;
                    if (d.item != null)
                    {
                        var it = Items.Get(d.item); var c = Items.RarityColor(it.rarity);
                        if (AddItem(d.item)) { Number(hero.Pos + Vector3.up * 2.4f, "+ " + it.name, "crit"); Fx.Burst(hero.Pos + Vector3.up, 20, c, 4); Persist(); }
                    }
                    else if (!d.orb) { int v = Mathf.RoundToInt(d.v * (pet.def != null && pet.def.gold ? 1.5f : 1)); gold += v; Number(hero.Pos + Vector3.up * 1.8f, "+" + v, "gold"); }
                    else { hero.Heal(hero.max * .1f); Fx.Burst(hero.Pos + Vector3.up, 14, new Color(.4f, 1, .55f), 3); }
                }
                d.go.transform.position = d.p + (d.orb ? Vector3.up * Mathf.Sin(d.t * 4) * .08f : Vector3.zero);
                d.go.transform.rotation = d.item != null ? Quaternion.Euler(0, d.t * 120, 0) : Quaternion.Euler(d.orb ? 0 : 90, d.t * 280, 0);
                if (dead) { Destroy(d.go); drops.RemoveAt(i); }
            }
        }

        // ---------- loop ----------
        void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1 / 25f);
            Shader.SetGlobalVector("_CozyWind", new Vector4(Time.time, 1, 0, 0));
            Shader.SetGlobalFloat("_CozyDim", 1); Shader.SetGlobalFloat("_CozyNight", 0);   // CozyFishing/Water globals
            if (paused || mode == Mode.Load) return;
            // hit stop: a few frames of near-freeze on heavy hits (animators and particles slow down too)
            if (hitStop > 0) hitStop -= dt;
            Time.timeScale = hitStop > 0 ? .06f : 1;
            if (mode == Mode.Game) { time += dt; if (lane) UpdLane(dt); else UpdWaves(dt); UpdDrops(dt); }
            for (int i = foes.Count - 1; i >= 0; i--) if (!foes[i]) foes.RemoveAt(i);
        }

        void LateUpdate()
        {
            if (!cam) return;
            float dt = Time.unscaledDeltaTime;
            if (mode == Mode.Lobby)
            {   // the lobby panel covers screen-right, which is world -X
                float side = cam.aspect > 1.25f ? -.95f : 0; bool bot = hero.def != null && !hero.def.rig; float z = ui.lobbyZoom; Vector3 look, want;
                if (ui.FocusPet && pet && pet.def != null)
                {   // pet tab: frame the pet, zoom toward it
                    float d = Mathf.Lerp(2.6f, 1.05f, z); var pc = pet.transform.position + Vector3.up * pet.def.h * .5f;
                    look = pc + new Vector3(side * d / 4.1f, 0, 0); want = look + new Vector3(0, .3f * d / 2.6f + .15f, d);
                }
                else
                {   // zoom moves in toward the chest/face; the side offset scales with distance so the hero keeps its place left of the panel
                    bool winged = WingsShown != null;   // wings reach above the head and out to the sides: frame the whole spread
                    float d = Mathf.Lerp(bot ? 5 : 4.1f, 1.6f, z) * (winged ? Mathf.Lerp(1.22f, 1.05f, z) : 1);
                    look = new Vector3(side * (bot ? 1.25f : 1) * d / 4.1f, Mathf.Lerp(bot ? .75f : .95f, bot ? .7f : 1.42f, z) + (winged ? .22f * (1 - z) : 0), 0); want = look + new Vector3(0, Mathf.Lerp(.4f, .08f, z), d);
                }
                camPos = Vector3.Lerp(camPos, want, 1 - Mathf.Exp(-dt * 6)); camLook = Vector3.Lerp(camLook, look, 1 - Mathf.Exp(-dt * 6));
                hero.transform.rotation = Quaternion.Euler(0, ui.heroYaw, 0); if (pet) pet.transform.rotation = Quaternion.Euler(0, 20 + ui.petYaw, 0);
            }
            else if (hero && soul && hero.soul && mode != Mode.Load)
                hero.soul.CameraUpdate(cam, dt, ref camPos, ref camLook);
            else if (hero)
            {
                // mouse wheel zooms the battle camera in and out (web / PC)
                var ms = UnityEngine.InputSystem.Mouse.current;
                if (ms != null && mode == Mode.Game && !ui.PointerOverUi) { float sc = ms.scroll.ReadValue().y; if (Mathf.Abs(sc) > .01f) gameZoom = Mathf.Clamp(gameZoom - Mathf.Sign(sc) * .1f, .6f, 1.8f); }
                float far = (cam.aspect < 1.3f ? 1.3f : 1) * (boss ? 1.15f : 1) * gameZoom * (lane ? 1.35f : 1);   // lanes: higher and further back so whole towers fit
                if (hero.dead) far *= .42f;   // death: close in on the fallen hero
                // camera height: high (top-down), mid, or low (three-quarter: the top of the screen shows the skyline)
                int style = lane ? 0 : stageLook && stageLook.lowCam ? 2 : ViewStyle;
                var off = style == 2 ? new Vector3(0, 4.1f, 9.6f) : style == 1 ? new Vector3(0, 6.2f, 8.8f) : new Vector3(0, 8.6f, 7.4f);
                var focus = ui.Pan ?? hero.Pos; var want = focus + off * far;
                camPos = Vector3.Lerp(camPos, want, 1 - Mathf.Exp(-dt * (hero.dead ? 2.2f : 6)));
                camLook = new Vector3(camPos.x, hero.dead ? .5f : lane ? 1.6f : style == 2 ? 1.3f : style == 1 ? 1.1f : .9f, camPos.z - off.z * far);
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, soul && mode != Mode.Lobby ? 55 : 40, 1 - Mathf.Exp(-dt * 6));
            shake = Mathf.Max(0, shake - dt);
            cam.transform.position = camPos + Random.insideUnitSphere * shake * .35f;
            cam.transform.LookAt(camLook);
        }

        // ======================= teams & lane battle =======================
        public IEnumerable<ITarget> Opponents(int team)
        {
            if (team == 1 && hero && !hero.dead) yield return hero;
            foreach (var f in foes) if (f && f.alive && !f.spawning && f.team != team) yield return f;
        }
        public ITarget PickTarget(Foe f)
        {
            if (!lane) return hero && !hero.dead ? hero : null;
            float aggro = f.d.kind == "tower" ? f.d.range : 10; ITarget best = null; float bd = aggro;
            foreach (var o in Opponents(f.team)) { float d = Flat(o.Pos - f.Pos).magnitude - o.Rad; if (d < bd) { bd = d; best = o; } }
            return best;
        }
        public void HitTeamArea(int team, Vector3 c, float r, float dmg)
        {
            foreach (var o in Opponents(team).ToList()) if (Flat(o.Pos - c).magnitude < r + o.Rad) o.Take(dmg, c);
        }
        public Vector3 Clamp(Vector3 p, float margin = 0)
        {
            if (lane)
            {
                p.x = Mathf.Clamp(p.x, -Lane.HalfW - margin, Lane.HalfW + margin); p.z = Mathf.Clamp(p.z, -Lane.HalfL - margin, Lane.HalfL + margin);
                foreach (var tw in foes)
                {   // towers are solid: nobody walks through them (destroyed ones too, until they have sunk)
                    if (!tw || tw.d.kind != "tower") continue; var d = Flat(p - tw.Pos); float rr = tw.rad + .45f;
                    if (d.sqrMagnitude < rr * rr) { d = d.sqrMagnitude < 1e-4f ? Vector3.forward : d.normalized; p.x = tw.Pos.x + d.x * rr; p.z = tw.Pos.z + d.z * rr; }
                }
                return Obstacles.Push(p);
            }
            var f = Flat(p); float r = f.magnitude, R = Data.ArenaR + margin; if (r > R) { f *= R / r; p.x = f.x; p.z = f.z; }
            return Obstacles.Push(p);
        }

        // Soulslike duel (Phase 1 of the Hybrid World plan): third-person fight with the stage boss and two of its minions
        public void StartSoul()
        {
            var hd = Data.Heroes.FirstOrDefault(x => x.id == save.hero); if (hd == null || !hd.rig) save.hero = "warrior";   // needs a rigged hero
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = false; soul = true; ClearPreview();
            stage = Mathf.Clamp(save.stage, 1, MaxStage); SetTheme(Data.ThemeOf(stage));
            kills = 0; gold = 0; time = 0; banked = 0; between = 0; wave = Theme.waves.Length;   // no waves: straight to the boss
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame();
            camPos = hero.Pos + new Vector3(0, 3, 6);
            ui.ShowHud(); ui.Banner(Loc.S("ดวลบอส · Soulslike", "Boss Duel · Soulslike"), Application.isMobilePlatform ? Loc.S("ลากจอขวาหมุนกล้อง · ปุ่มเป้าล็อกศัตรู", "Drag the right side to turn the camera · Target button locks on") : Loc.S("Tab ล็อกเป้า · คลิกซ้าย/ขวา โจมตี · Q ป้องกัน · Space หลบ", "Tab lock-on · Left/Right click attack · Q block · Space dodge"));
            SpawnBoss(); foreach (var p in new[] { new Vector3(-5, 0, -8), new Vector3(5, 0, -8) }) SpawnFoe(Theme.waves[0][0].foe, p);
        }

        public int laneCount = 1;
        public void StartLane(int size, int lanes = 1)
        {
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = true; soul = false; ClearPreview(); laneSize = size; laneCount = lanes; Lane.Lanes = lanes;
            kills = 0; gold = 0; time = 0; banked = 0; minionT = 3; wave = 0;
            SetTheme(lanes == 3 ? stageRoots.Length - 1 : stageRoots.Length - 2);   // the two lane maps are the last stage roots
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame();
            for (int t = 0; t < 2; t++)
            {
                float side = Lane.Side(t);
                for (int l = 0; l < lanes; l++) foreach (var z in Lane.TowerZ) { var u = SpawnUnit(Lane.Tower, new Vector3(Lane.LaneX(lanes == 3 ? l : 1), 0, side * z), t); if (u) u.laneIdx = l; }
                SpawnUnit(Lane.Core, new Vector3(0, 0, side * Lane.CoreZ), t);
            }
            // AI heroes: in three lanes our two helpers take the side lanes (the player usually goes middle), the enemy has one per lane
            int[] ourLanes = lanes == 3 ? new[] { 0, 2 } : new[] { 1, 1 }, theirLanes = lanes == 3 ? new[] { 0, 1, 2 } : new[] { 1, 1, 1 };
            var others = Data.Heroes.Where(h => h.rig && h.id != hero.def.id).OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < size - 1; i++) { var u = SpawnUnit(Lane.Champ(others[i % others.Count]), new Vector3(lanes == 3 ? Lane.LaneX(ourLanes[i]) * .4f : (i == 0 ? -3 : 3), 0, Lane.SpawnZ), 0); if (u) u.laneIdx = ourLanes[i % 2]; }
            var foesH = Data.Heroes.Where(h => h.rig).OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < size; i++) { var u = SpawnUnit(Lane.Champ(foesH[i % foesH.Count]), new Vector3(lanes == 3 ? Lane.LaneX(theirLanes[i % 3]) * .4f : (i - 1) * 3, 0, -(Lane.SpawnZ)), 1); if (u) u.laneIdx = theirLanes[i % 3]; }
            camPos = new Vector3(0, 9, Lane.CoreZ + 10);
            ui.ShowHud(); ui.Banner(lanes == 3 ? Loc.S($"สนามรบ 3 เลน · {size} ต่อ {size}", $"3-Lane Battle · {size} vs {size}") : Loc.S($"สนามรบ {size} ต่อ {size}", $"Lane Battle · {size} vs {size}"), lanes == 3 ? Loc.S("ทำลายป้อมในแต่ละเลน แล้วทุบคริสตัลหลักของศัตรู", "Destroy the towers in each lane, then smash the enemy Core Crystal") : Loc.S("ทำลายคริสตัลหลักของฝ่ายตรงข้าม", "Destroy the enemy Core Crystal"));
        }
        void UpdLane(float dt)
        {
            minionT -= dt; if (minionT > 0) return; minionT = Lane.Lanes == 3 ? 30 : 24; wave++;
            for (int t = 0; t < 2; t++)
                for (int l = 0; l < Lane.Lanes; l++)
                    for (int i = 0; i < 3; i++)
                    {
                        int li = Lane.Lanes == 3 ? l : 1; float x = Lane.Lanes == 3 ? Lane.LaneX(li) * .3f : 0;
                        var u = SpawnUnit(Lane.Minion, new Vector3(x + (i - 1) * 1.6f, 0, Lane.Side(t) * (Lane.CoreZ - 3)), t); if (u) u.laneIdx = li;
                    }
        }
        public int TowersLeft(int team) => foes.Count(f => f && f.alive && f.team == team && f.d.kind == "tower");
        public void OnFoeDown(Foe f)
        {
            if (!lane || mode != Mode.Game) return;
            if (f.d.id == "core") { ui.Banner(f.team == 1 ? Loc.S("ชัยชนะ!", "Victory!") : Loc.S("พ่ายแพ้", "Defeat"), f.team == 1 ? Loc.S("คริสตัลฝ่ายศัตรูถูกทำลาย", "The enemy crystal was destroyed") : Loc.S("คริสตัลของเราถูกทำลาย", "Our crystal was destroyed")); StartCoroutine(After(2.2f, () => Finish(f.team == 1))); }
            else if (f.d.id == "tower") ui.Banner(f.team == 1 ? Loc.S("ทำลายป้อมศัตรู!", "Enemy tower destroyed!") : Loc.S("ป้อมของเราถูกทำลาย", "Our tower was destroyed"), "");
            else if (f.d.kind == "champ") { var def = f.d; int team = f.team, li = f.laneIdx; StartCoroutine(After(10, () => { if (lane && mode == Mode.Game) { var u = SpawnUnit(def, new Vector3(Random.Range(-2f, 2f), 0, Lane.Side(team) * (Lane.SpawnZ)), team); if (u) u.laneIdx = li; } })); }
        }
        System.Collections.IEnumerator After(float s, System.Action a) { yield return new WaitForSeconds(s); a(); }
    }
}
