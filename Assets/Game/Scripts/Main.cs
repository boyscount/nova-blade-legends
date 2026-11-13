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
        public bool duelOnly;
        // AI difficulty: health and basic damage of AI heroes, their skill power and cooldowns, the pause between skills, and (hard) ganging up on the weakest
        public static readonly (float hp, float dmg, float skill, float cd, float gap, bool focus)[] AiLevels = { (.9f, .85f, .85f, 1.3f, 1.8f, false), (1.15f, 1.12f, 1.12f, .9f, .8f, false), (1.4f, 1.3f, 1.3f, .75f, .55f, true) };   // owner, 2026-11-11: "wins too easily": every level stronger (and lobby gear no longer counts in lane battles)
        public (float hp, float dmg, float skill, float cd, float gap, bool focus) AiLv => duelOnly ? AiLevels[1] : AiLevels[Mathf.Clamp(save.aiLevel, 0, 2)];
        // the teams for the next lane battle, rolled for the VS screen (two helpers and the enemy side); used once by StartLane / StartSkyLanes
        public List<HeroDef> nextAllies, nextEnemies;
        public void RollTeams(int size)
        {
            var pool = Data.Heroes.Where(h => h.rig && Kits.For(h.id) != null).OrderBy(_ => Random.value).ToList();
            nextAllies = pool.Where(h => h.id != hero.def.id).Take(size - 1).ToList();
            nextEnemies = pool.Where(h => h.id != hero.def.id && !nextAllies.Contains(h)).Take(size).ToList();
        }
        List<HeroDef> TakeAllies() { var a = nextAllies; nextAllies = null; return a; }
        List<HeroDef> TakeEnemies() { var e = nextEnemies; nextEnemies = null; return e; }
        public void StartBattle() { switch (save.laneMode) { case 0: StartLane(1, 1); break; case 1: StartLane(3, 1); break; case 2: StartLane(3, 3); break; default: if (Stage("Stage_skylanes")) StartLane(3, 2); else StartLane(3, 3); break; } } public float TestSpeed = 1;   // tests: AI heroes only fight each other (no retreat, no lane push, the player left out)
        public static readonly bool NoKitAi = System.Environment.GetCommandLineArgs().Contains("-nokitai");   // tests: AI heroes back on the old shared skill
        public enum Mode { Load, Lobby, Game, Result }
        public Mode mode = Mode.Load;
        public bool paused;
        public bool city, cameFromCity; public Transform cityRoot; City cityCtl; readonly List<GameObject> autumnTrees = new();   // the walkable main city
        public void StartCity()
        {
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = false; soul = false; ClearPreview(); BankGold(); kills = 0; gold = 0; time = 0; banked = 0;
            var square = System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_kingdom"); SetTheme(square); city = true; cameFromCity = true; cityRoot.gameObject.SetActive(true); Obstacles.Add(cityRoot);
            autumnTrees.Clear(); foreach (Transform t in stageRoots[square]) if (t.gameObject.activeSelf && (t.name.StartsWith("SmallTreeRound") || t.name.StartsWith("SM_Env_Tree"))) { t.gameObject.SetActive(false); autumnTrees.Add(t.gameObject); }
            if (!cityCtl) cityCtl = gameObject.AddComponent<City>(); cityCtl.Enter();
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame(); hero.transform.position = City.Start; pet.transform.position = City.Start + new Vector3(1.2f, 0, 1); camPos = City.Start + new Vector3(0, 9, 12);
            ui.ShowHud();
        }
        // Open World zone 1: free walking in a wider area, no waves; reached from the city gate
        public bool wilds; OpenWorld wildsCtl; public OpenWorld Zone => wildsCtl; public bool ZoneNight => (wilds || sky) && wildsCtl && wildsCtl.Clock.IsNight;
        public bool sky;   // the floating duel court reached through the hilltop portal (OpenWorld.Duels)
        public bool house; public bool HouseFought => house && !foes.Any(f => f && f.alive);   // inside the zone's house; its monsters are gone
        public float ArenaRadius => sky ? OpenWorld.SkyHalf + 1 : house ? OpenWorld.RoomHalf + 1 : wilds ? OpenWorld.Radius : Data.ArenaR;
        public Transform Stage(string name) => System.Array.Find(stageRoots, r => r && r.name == name);
        public void StartWilds()
        {
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = false; soul = false; ClearPreview(); BankGold(); kills = 0; gold = 0; time = 0; banked = 0; stage = 1;
            SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_wilds")); wilds = true; cameFromCity = true;
            if (!wildsCtl) wildsCtl = gameObject.AddComponent<OpenWorld>(); wildsCtl.Enter();
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame(); hero.transform.position = OpenWorld.Start; pet.transform.position = OpenWorld.Start + new Vector3(1.2f, 0, 1); camPos = OpenWorld.Start + new Vector3(0, 9, 12);
            ui.ShowHud();
        }
        void MoveTo(Vector3 at) { hero.StopWalk(); hero.transform.position = at; pet.transform.position = at + new Vector3(1.2f, 0, 1); camPos = at + new Vector3(0, 9, 12); }
        static readonly string[] HouseFoes = { "orc", "orc", "goblin", "goblin", "goblin", "skeleton" };
        // the zone's house: a room of its own, with its monsters until the chest has been opened on this visit
        public void EnterHouse()
        {
            ClearWorld(); ui.SetCityPrompt(null, null); SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_house")); house = true; MoveTo(OpenWorld.RoomStart);
            if (wildsCtl.HouseCleared) return;
            for (int i = 0; i < HouseFoes.Length; i++) { float a = (i + .5f) / HouseFoes.Length * Mathf.PI, r = Random.Range(4f, 6.5f); SpawnFoe(HouseFoes[i], new Vector3(Mathf.Cos(a) * r, 0, -Mathf.Sin(a) * r - 1)); }
            ui.Banner(Loc.S("บ้านร้าง", "Abandoned House"), Loc.S("ปราบให้หมดแล้วเปิดหีบ", "Defeat them all, then open the chest"));
        }
        static readonly string[] CaveFoes = { "skeleton", "skeleton", "zombie", "zombie", "demon" };
        static readonly Color CaveSun = new(.45f, .68f, 1f), CaveAmb = new(.16f, .24f, .34f), CaveFog = new(.015f, .04f, .07f); const float CaveSunI = .7f;
        // the secret cave uses the house's room, in cold moonlight, with guards of its own
        public void EnterCave()
        {
            ClearWorld(); ui.SetCityPrompt(null, null); SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_house")); house = true; wildsCtl.InCave = true; MoveTo(OpenWorld.RoomStart);
            var sun = RenderSettings.sun; if (sun) { roomSunCol = sun.color; roomSunI = sun.intensity; sun.color = CaveSun; sun.intensity = CaveSunI; }
            RenderSettings.fogColor = CaveFog; RenderSettings.ambientSkyColor = CaveAmb; RenderSettings.ambientEquatorColor = CaveAmb * .7f; if (Camera.main) Camera.main.backgroundColor = CaveFog;
            for (int i = 0; i < CaveFoes.Length; i++) { float a = (i + .5f) / CaveFoes.Length * Mathf.PI, r = Random.Range(4f, 6.5f); var f = SpawnFoe(CaveFoes[i], new Vector3(Mathf.Cos(a) * r, 0, -Mathf.Sin(a) * r - 1)); f.Spirit(new Color(.14f, .95f, 1f), new Color(1.25f, 2.5f, 2.7f)); }
            ui.Banner(Loc.S("ถ้ำลับ", "The Secret Cave"), Loc.S("ปราบผู้เฝ้าถ้ำ แล้วเปิดหีบใหญ่", "Defeat its guards, then open the great chest"));
        }
        Color roomSunCol; float roomSunI;
        public void LeaveHouse() => BackToZone(wildsCtl.InCave ? wildsCtl.CaveDoor + new Vector3(0, 0, 1.5f) : wildsCtl.Door + new Vector3(0, 0, 1.5f));
        public void BackToZone(Vector3 at) { SetSoul(false); wildsCtl.LeftSky(); if (wildsCtl.InCave) { var sun = RenderSettings.sun; if (sun) { sun.color = roomSunCol; sun.intensity = roomSunI; } wildsCtl.InCave = false; } ClearWorld(); ui.SetCityPrompt(null, null); SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_wilds")); wilds = true; wildsCtl.ShowTime(true); MoveTo(at); }
        // the duel court: a stage of its own, the duellist across from the hero, the duel controls on
        public void EnterSky(OpenWorld.Duelist duel, bool night)
        {
            ClearWorld(); ui.SetCityPrompt(null, null); SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_sky")); sky = true; MoveTo(OpenWorld.SkyStart); hero.face = Mathf.PI; hero.hp = hero.max;
            var sun = RenderSettings.sun; if (sun) { if (!skySunKept) { skySunKept = true; skySunCol = sun.color; skySunI = sun.intensity; } sun.color = night ? SkyMoon : skySunCol; sun.intensity = night ? SkyMoonI : skySunI; }
            if (night) { RenderSettings.fogColor = SkyNightFog; RenderSettings.ambientSkyColor = SkyNightAmb; RenderSettings.ambientEquatorColor = SkyNightAmb * .7f; RenderSettings.ambientGroundColor = SkyNightAmb * .3f; var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(SkyNightAmb * .7f); RenderSettings.ambientProbe = sh; if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = SkyNightFog; } }
            var f = new GameObject("Boss").AddComponent<Foe>(); f.Init(null, OpenWorld.SkyFoe, duel.boss); foes.Add(f); boss = f; f.face = 0; float edge = DuelSync(f, duel.boss); duelPotions = DuelPotions;
            if (night)
            {   // stronger by night, and lit: a glow in its own colour and a light that goes with it, so it stands out of the dark
                f.hp *= OpenWorld.NightFoePower; f.maxHp *= OpenWorld.NightFoePower; f.Spirit(duel.boss.color * .14f, new Color(1.35f, 1.35f, 1.45f));
                var l = new GameObject("DuelLight").AddComponent<Light>(); l.transform.SetParent(f.transform, false); l.transform.localPosition = new Vector3(0, 2.4f, .6f); l.type = LightType.Point; l.color = Color.Lerp(duel.boss.color, Color.white, .5f); l.intensity = 2.4f; l.range = 7; l.shadows = LightShadows.None;
            }
            ui.ShowBoss(duel.boss.name); ui.Banner(duel.boss.name, duel.title + Loc.S($" · ดวลยุติธรรม: เลเวลและความชำนาญได้เปรียบ +{edge * 100:0}% · ยา {DuelPotions} ขวด", $" · Fair duel: level and mastery +{edge * 100:0}% · {DuelPotions} potions")); shake = .3f;
        }
        // A fair duel: the duellist is sized to the hero, so timing decides and not gear. Its health is so many of the hero's light blows, its blows
        // take so many of the hero's health (after armour and shield); weapons differ in speed and reach, not power. Level and weapon
        // mastery still count, for at most a 15% edge (shorter fights, lighter hits). The pet watches, the potions are counted.
        public const int DuelPotions = 2; public int duelPotions;
        public float DuelEdge => Mathf.Clamp01((hero.Level - 1) / (float)(Data.MaxLevel - 1)) * .1f + hero.MasteryTier / 4f * .05f;
        float DuelSync(Foe f, BossDef b)
        {
            if (b.hits <= 0) return 0; hero.orbAtkT = hero.buffT = 0;   // every duel starts clean: no picked-up fury
            float edge = DuelEdge, guard = Mathf.Max(.4f, 1 - Items.Bonus(save).def) * (hero.ShieldOn != null ? 1 - hero.ShieldOn.block : 1);
            f.hp = f.maxHp = b.hits * hero.AtkPow / (1 + edge); f.d.dmg = hero.max / b.takes / guard / (1 + edge);
            return edge;
        }
        bool skySunKept; Color skySunCol; float skySunI;
        static readonly Color SkyMoon = new(.66f, .75f, 1f), SkyNightFog = new(.06f, .09f, .17f), SkyNightAmb = new(.46f, .5f, .68f); const float SkyMoonI = 1.9f;   // a bright moon: the duel has to stay readable
        // the duel controls (third-person camera, block, parry, stamina) on or off in the middle of a visit
        public void SetSoul(bool on)
        {
            if (soul == on) return; soul = on; hero.state = Hero.St.Idle;
            if (on) { if (!hero.soul) hero.soul = hero.gameObject.AddComponent<SoulController>(); hero.soul.Begin(hero); hero.soul.yaw = hero.face; camPos = hero.Pos + new Vector3(0, 3, 6); }
            else if (hero.soul) hero.soul.End();
            ui.ShowHud(); if (boss && boss.alive) ui.ShowBoss(boss.d.name);
        }
        // the city gate's duels (Ui gate panel): the Sky Court without the zone
        public void StartArena(string id) { if (!wildsCtl) wildsCtl = gameObject.AddComponent<OpenWorld>(); wildsCtl.FromCity(id); }
        public void CityAtGate() { StartCity(); MoveTo(City.Gate + new Vector3(0, 0, 2.6f)); }
        // fallen in the zone: wake at its gate, a fifth of this trip's gold lost
        public void FallInZone()
        {
            SetSoul(false);
            int lost = Mathf.RoundToInt(gold * .2f); gold -= lost; banked = Mathf.Min(banked, gold);
            if (wildsCtl && wildsCtl.FromCityDuel) { wildsCtl.LeftSky(); CityAtGate(); }   // a duel taken at the city gate: back at the gate
            else { hero.ResetForGame(); pet.ResetForGame(); BackToZone(OpenWorld.Start); }
            ui.Banner(Loc.S("พ่ายแพ้", "You fell"), lost > 0 ? Loc.S($"เสียทอง {lost}", $"Lost {lost} gold") : "");
        }
        // leaving a stage or the pause menu: back where the player came from
        public void LeaveStage() { if (city) { cameFromCity = false; ShowLobby(); } else if (cameFromCity) StartCity(); else ShowLobby(); }
        readonly HashSet<string> crumbed = new();
        // a Sky Lanes wave is let out a few units a frame instead of twenty-odd in one frame (owner: "it stutters when a wave comes out")
        readonly Queue<(FoeDef d, Vector3 p, int team, int lane)> waveQueue = new(); const int WavePerFrame = 3;
        void QueueUnit(FoeDef d, Vector3 p, int team, int lane) => waveQueue.Enqueue((d, p, team, lane));
        void LetOutWave() { for (int i = 0; i < WavePerFrame && waveQueue.Count > 0; i++) { var (d, p, t, l) = waveQueue.Dequeue(); var u = SpawnUnit(d, p, t); if (u) u.laneIdx = l; } }
        public bool lane; public int laneSize = 3; float minionT; readonly bool[] brutesOn = new bool[2];
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
        public static int CamStyle = -1;   // battle camera: -1 = the player's choice (saved), 0 high, 1 mid, 2 low, 3 close behind the shoulder (test flag -camstyle n)
        public int ViewStyle => CamStyle >= 0 ? CamStyle : PlayerPrefs.GetInt("novaCam", ui && ui.touchUi ? 2 : 0);   // phones start low
        public void CycleView() { PlayerPrefs.SetInt("novaCam", (ViewStyle + 1) % 4); PlayerPrefs.Save(); CamStyle = -1; }
        public void ZoomBy(float d) => gameZoom = Mathf.Clamp(gameZoom + d, .6f, 1.8f);
        public ThemeDef Theme => Data.Themes[Data.ThemeOf(stage)];

        [System.Serializable]
        public class Save
        {
            public string caveDay = "";   // the local date the secret cave's chest was last opened
            public string legendDay = "";   // the local date the lurking legend of the zone was beaten (once per day)
            public string hero = "warrior", sword = "w_lava", pet = "candle"; public int gold, best, stage = 1; public List<string> skinKeys = new(); public List<int> skinVals = new();
            public int gems = Shop.TestGems;                       // test-only premium currency (no real payments yet)
            public List<string> owned = new(), ledger = new();     // ownership and purchase history, kept apart from what is equipped
            public List<string> wsKeys = new(), wsVals = new();    // equipped weapon skin per weapon
            public string sword2 = "";                             // off-hand weapon ("" = none), swapped in during a fight
            public List<string> mastKeys = new(); public List<int> mastVals = new();   // weapon mastery points per weapon
            public int Mastery(string weapon) { int i = mastKeys.IndexOf(weapon); return i < 0 ? 0 : mastVals[i]; }
            public void AddMastery(string weapon, int n) { int i = mastKeys.IndexOf(weapon); if (i < 0) { mastKeys.Add(weapon); mastVals.Add(n); } else mastVals[i] += n; }
            public List<string> xpKeys = new(); public List<int> xpVals = new();   // EXP per hero
            public int HeroXp(string id) { if (xpKeys == null) return 0; int i = xpKeys.IndexOf(id); return i < 0 ? 0 : xpVals[i]; }
            public void AddHeroXp(string id, int n) { xpKeys ??= new(); xpVals ??= new(); int i = xpKeys.IndexOf(id); if (i < 0) { xpKeys.Add(id); xpVals.Add(n); } else xpVals[i] += n; }
            public List<string> questKeys = new(), questClaimed = new(); public List<int> questVals = new();   // quest counters by kind, and rewards already taken
            public string shield = "";                             // shield on the left arm ("" = none; warriors and clerics)
            public string wings = ""; public bool petWings = true;
            public int laneMode = 3, aiLevel = 1, rankPts = 0;   // rankPts: lane battle rank (Ui.Ranks): a win +25 (+10 hard, -10 easy), a loss -15   // battle settings (lobby): 0 1v1, 1 one lane 3v3, 2 three lanes 3v3, 3 Sky Lanes 3v3 · AI 0 easy, 1 normal, 2 hard
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
        // the model a hero shows for the player: its chosen (owned) skin, or the one being previewed in the lobby
        public int SkinOf(HeroDef h, bool preview = false) { int i = Mathf.Clamp(Skin(h.id), 0, Data.SkinCount(h) - 1); if (!Shop.Owns(ItemKind.HeroSkin, $"{h.id}:{i}")) i = 0; if (preview && pvSkin >= 0) i = Mathf.Clamp(pvSkin, 0, Data.SkinCount(h) - 1); if (!Data.SkinOffered(h, i)) i = 0; return i; }
        public string ModelKey(HeroDef h, bool preview = false) => Data.ModelOf(h, SkinOf(h, preview));
        public void SetSkin(string id, int v) { int i = save.skinKeys.IndexOf(id); if (i < 0) { save.skinKeys.Add(id); save.skinVals.Add(v); } else save.skinVals[i] = v; Persist(); }
        public void Persist() { PlayerPrefs.SetString("novaBlade", JsonUtility.ToJson(save)); PlayerPrefs.Save(); }
        public int MaxStage => Mathf.Max(Data.Themes.Length, Mathf.Min(99, save.best + 1));   // every stage is open from the start

        void Awake()
        {
            Balance.Load();   // Resources/Data/balance.json over the defaults in Data.cs
            if (Camera.main && !Camera.main.GetComponent<OcclusionFader>()) Camera.main.gameObject.AddComponent<OcclusionFader>();   // trees in the way turn see-through
            G = this;
            Application.targetFrameRate = 60;
            if (IsPhone) QualitySettings.SetQualityLevel(0, true);
            if (System.Environment.GetCommandLineArgs().Contains("-phonequality")) { QualitySettings.SetQualityLevel(0, true); Studio.PhoneLook = true; }   // tests: the phones' render settings on Windows   // phones (also mobile browsers): the Mobile URP asset
            Crumb.Begin(); Crumb.Mark("game opened");
            if (Crumb.Has("lowtex")) QualitySettings.globalTextureMipmapLimit = 1;
            if (Crumb.Has("noshadow")) UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.shadowDistance = 0;
            else if (Camera.main && Camera.main.TryGetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(out var cd))
                cd.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;   // PC: the pipeline's 4x MSAA keeps edges sharp; FXAA would soften the whole frame
            save = JsonUtility.FromJson<Save>(PlayerPrefs.GetString("novaBlade", "{}")) ?? new Save();
            if (save.skinKeys == null) { save.skinKeys = new(); save.skinVals = new(); }
            save.owned ??= new(); save.ledger ??= new(); save.wsKeys ??= new(); save.wsVals ??= new();
            Fx.Init(transform);
            if (!cam) cam = Camera.main;
        }

        // GameObject.CreatePrimitive adds colliders from native code: naming them here keeps web build stripping from removing them
        static readonly System.Type[] keepColliders = { typeof(SphereCollider), typeof(CapsuleCollider), typeof(BoxCollider), typeof(MeshCollider) };
        void Start()
        {
            gameObject.AddComponent<AutoQuality>(); gameObject.AddComponent<HeroLight>();
            hero = new GameObject("Hero").AddComponent<Hero>();
            pet = new GameObject("Pet").AddComponent<Pet>();
            ui = gameObject.AddComponent<Ui>();
            ShowLobby();
            var args = System.Environment.GetCommandLineArgs(); int ai = System.Array.IndexOf(args, "-novashot");
            if (ai >= 0 && ai + 1 < args.Length) StartCoroutine(AutoShot(args[ai + 1]));
            int ci = System.Array.IndexOf(args, "-clip"); if (ci >= 0 && ci + 1 < args.Length) StartCoroutine(Clip(args[ci + 1]));
            int sc = System.Array.IndexOf(args, "-storyclip"); if (sc >= 0 && sc + 1 < args.Length) StartCoroutine(StoryClip(args[sc + 1]));
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
            int pf = System.Array.IndexOf(args, "-perftest"); if (pf >= 0 && pf + 1 < args.Length) StartCoroutine(PerfTest(args[pf + 1]));
            int ls = System.Array.IndexOf(args, "-skincompare"); if (ls >= 0 && ls + 2 < args.Length) StartCoroutine(SkinCompare(args[ls + 1], args[ls + 2]));
            int nh = System.Array.IndexOf(args, "-newherotest"); if (nh >= 0 && nh + 1 < args.Length) StartCoroutine(NewHeroTest(args[nh + 1]));
            if (System.Array.IndexOf(args, "-hitchtest") >= 0 || Application.absoluteURL.Contains("hitch")) StartCoroutine(HitchTest(System.Array.IndexOf(args, "-hitchtest") >= 0));
            int bk = System.Array.IndexOf(args, "-bakecards"); if (bk >= 0 && bk + 1 < args.Length) StartCoroutine(BakeCards(args[bk + 1]));
            int shots = System.Array.IndexOf(args, "-stageshots"); if (shots >= 0 && shots + 1 < args.Length) StartCoroutine(StageShots(args[shots + 1]));
            int ft = System.Array.IndexOf(args, "-foottest"); if (ft >= 0 && ft + 2 < args.Length) StartCoroutine(FootTest(args[ft + 1], args[ft + 2]));
            int gt = System.Array.IndexOf(args, "-gaittest"); if (gt >= 0 && gt + 1 < args.Length) StartCoroutine(GaitTest(args[gt + 1]));
            int ct = System.Array.IndexOf(args, "-citytest"); if (ct >= 0 && ct + 1 < args.Length) StartCoroutine(CityTest(args[ct + 1]));
            int qt = System.Array.IndexOf(args, "-questtest"); if (qt >= 0 && qt + 1 < args.Length) StartCoroutine(QuestTest(args[qt + 1]));
            int bt = System.Array.IndexOf(args, "-bagtest"); if (bt >= 0 && bt + 1 < args.Length) StartCoroutine(BagTest(args[bt + 1]));
            int mt = System.Array.IndexOf(args, "-movetest"); if (mt >= 0 && mt + 1 < args.Length) StartCoroutine(MoveTest(args[mt + 1]));
            int cw = System.Array.IndexOf(args, "-crowdtest"); if (cw >= 0 && cw + 1 < args.Length) StartCoroutine(CrowdTest(args[cw + 1]));
            int gw = System.Array.IndexOf(args, "-gatewalk"); if (gw >= 0 && gw + 1 < args.Length) StartCoroutine(GateWalk(args[gw + 1]));
            int cvt = System.Array.IndexOf(args, "-cavetest"); if (cvt >= 0 && cvt + 1 < args.Length) StartCoroutine(CaveTest(args[cvt + 1]));
            int slk = System.Array.IndexOf(args, "-storylooks"); if (slk >= 0 && slk + 1 < args.Length) StartCoroutine(StoryLooks(args[slk + 1]));
            int mth = System.Array.IndexOf(args, "-modethumbs"); if (mth >= 0 && mth + 1 < args.Length) StartCoroutine(ModeThumbs.Run(this, args[mth + 1]));
            int bst = System.Array.IndexOf(args, "-battlesettings"); if (bst >= 0 && bst + 1 < args.Length) StartCoroutine(BattleSettingsTest(args[bst + 1]));
            int tbl = System.Array.IndexOf(args, "-teambalance"); if (tbl >= 0 && tbl + 1 < args.Length) StartCoroutine(TeamBalance(args[tbl + 1], tbl + 2 < args.Length && int.TryParse(args[tbl + 2], out int tf) ? tf : 120));
            int skt = System.Array.IndexOf(args, "-skintest"); if (skt >= 0 && skt + 1 < args.Length) StartCoroutine(SkinTest(args[skt + 1]));
            int brt = System.Array.IndexOf(args, "-brutetest"); if (brt >= 0 && brt + 1 < args.Length) StartCoroutine(BruteTest(args[brt + 1]));
            int sct = System.Array.IndexOf(args, "-scrolltest"); if (sct >= 0 && sct + 1 < args.Length) StartCoroutine(ScrollTest(args[sct + 1]));
            int gsg = System.Array.IndexOf(args, "-gripsign"); if (gsg >= 0 && gsg + 1 < args.Length) Grip.Sign = float.Parse(args[gsg + 1], System.Globalization.CultureInfo.InvariantCulture);   // tests
            int phr = System.Array.IndexOf(args, "-posehero"); if (phr >= 0 && phr + 1 < args.Length) Studio.PoseOnly = args[phr + 1];
            int pst = System.Array.IndexOf(args, "-posesheet"); if (pst >= 0 && pst + 1 < args.Length) { Studio.PoseSheet(args[pst + 1]); Application.Quit(); }
            int mpt = System.Array.IndexOf(args, "-matchprep"); if (mpt >= 0 && mpt + 1 < args.Length) StartCoroutine(MatchPrepTest(args[mpt + 1], mpt + 2 < args.Length && !args[mpt + 2].StartsWith("-") ? args[mpt + 2] : "pc"));
            int dbl = System.Array.IndexOf(args, "-duelbalance"); if (dbl >= 0 && dbl + 1 < args.Length) StartCoroutine(DuelBalance(args[dbl + 1], dbl + 2 < args.Length && int.TryParse(args[dbl + 2], out int dr) ? dr : 2, dbl + 3 < args.Length && int.TryParse(args[dbl + 3], out int dm) ? dm : 0));
            int klb = System.Array.IndexOf(args, "-kitlobby"); if (klb >= 0 && klb + 1 < args.Length) StartCoroutine(KitLobby(args[klb + 1]));
            int akt = System.Array.IndexOf(args, "-aikittest"); if (akt >= 0 && akt + 1 < args.Length) StartCoroutine(AiKitTest(args[akt + 1]));
            int ktt = System.Array.IndexOf(args, "-kittest"); if (ktt >= 0 && ktt + 1 < args.Length) StartCoroutine(KitTest(args[ktt + 1]));
            int stt = System.Array.IndexOf(args, "-skytimes"); if (stt >= 0 && stt + 1 < args.Length) StartCoroutine(SkyTimes(args[stt + 1]));
            int ent = System.Array.IndexOf(args, "-endtest"); if (ent >= 0 && ent + 1 < args.Length) StartCoroutine(EndTest(args[ent + 1]));
            int pkt = System.Array.IndexOf(args, "-perktest"); if (pkt >= 0 && pkt + 1 < args.Length) StartCoroutine(PerkTest(args[pkt + 1]));
            int jgt = System.Array.IndexOf(args, "-jungletest"); if (jgt >= 0 && jgt + 1 < args.Length) StartCoroutine(JungleTest(args[jgt + 1]));
            int mlt = System.Array.IndexOf(args, "-matchlen"); if (mlt >= 0 && mlt + 1 < args.Length) StartCoroutine(MatchLenTest(args[mlt + 1], mlt + 2 < args.Length && int.TryParse(args[mlt + 2], out var mlr) ? mlr : 3));
            int slt = System.Array.IndexOf(args, "-skylanestest"); if (slt >= 0 && slt + 1 < args.Length) StartCoroutine(SkyLanesTest(args[slt + 1]));
            int dlt = System.Array.IndexOf(args, "-duelstest"); if (dlt >= 0 && dlt + 1 < args.Length) StartCoroutine(DuelsTest(args[dlt + 1]));
            int wlt = System.Array.IndexOf(args, "-wildstest"); if (wlt >= 0 && wlt + 1 < args.Length) StartCoroutine(WildsTest(args[wlt + 1]));
            int al = System.Array.IndexOf(args, "-archerlook"); if (al >= 0 && al + 1 < args.Length) StartCoroutine(ArcherLook(args[al + 1]));
            int cl = System.Array.IndexOf(args, "-classlook"); if (cl >= 0 && cl + 1 < args.Length) StartCoroutine(ClassLook(args[cl + 1]));
            int wc = System.Array.IndexOf(args, "-weaponcards"); if (wc >= 0 && wc + 1 < args.Length) StartCoroutine(WeaponCardsTest(args[wc + 1]));
            int n5 = System.Array.IndexOf(args, "-new5test"); if (n5 >= 0 && n5 + 1 < args.Length) StartCoroutine(New5Test(args[n5 + 1]));
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
        // Test mode (-skincompare <hero> <dir>): the hero close up in the lobby at full textures and at half size (one mip level down), then quit.
        System.Collections.IEnumerator SkinCompare(string id, string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); save.hero = id; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.lobbyZoom = 1; ui.heroYaw = 0; yield return new WaitForSecondsRealtime(4);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "full.png")); yield return new WaitForSecondsRealtime(1);
            {   // the same face with no animation at all, to tell the model from the pose
                var anim = hero.GetComponentInChildren<Animator>(); anim.enabled = false; anim.Rebind(); anim.Update(0); yield return new WaitForEndOfFrame(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "rest.png")); yield return new WaitForSecondsRealtime(1); anim.enabled = true;
                var head = anim.GetBoneTransform(HumanBodyBones.Head); var neck = anim.GetBoneTransform(HumanBodyBones.Neck); var sb = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(.5f);
                sb.AppendLine($"head {(head ? head.name : "none")} euler {(head ? head.localEulerAngles : Vector3.zero)} neck {(neck ? neck.name : "none")} euler {(neck ? neck.localEulerAngles : Vector3.zero)}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "bones.txt"), sb.ToString());
            }
            QualitySettings.globalTextureMipmapLimit = 1; yield return new WaitForSecondsRealtime(2);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "half.png")); yield return new WaitForSecondsRealtime(1); QualitySettings.globalTextureMipmapLimit = 0;
            // what the materials are, then the same hero with its shine turned down
            var log = new System.Text.StringBuilder();
            foreach (var r in hero.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials)
            {
                string props = ""; foreach (var p in new[] { "_Smoothness", "_Metallic", "_BumpScale", "_OcclusionStrength", "_MetallicGlossMap", "_BumpMap", "_OcclusionMap", "metallicFactor", "roughnessFactor", "metallicRoughnessTexture", "normalTexture", "normalTexture_scale" }) if (m.HasProperty(p)) props += " " + p + (p.Contains("Map") || p.EndsWith("Texture") ? "=" + (m.GetTexture(p) ? m.GetTexture(p).width.ToString() : "none") : "=" + m.GetFloat(p).ToString("0.00"));
                log.AppendLine($"{r.name}: {m.shader.name}{props}");
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", m.GetFloat("_Smoothness") * .45f);
                if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", m.GetFloat("_Metallic") * .6f);
                if (m.HasProperty("roughnessFactor")) m.SetFloat("roughnessFactor", Mathf.Clamp01(m.GetFloat("roughnessFactor") * 1.6f + .25f));
                if (m.HasProperty("metallicFactor")) m.SetFloat("metallicFactor", m.GetFloat("metallicFactor") * .6f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "materials.txt"), log.ToString()); yield return new WaitForSecondsRealtime(1.5f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "matte.png")); yield return new WaitForSecondsRealtime(1); Application.Quit();
        }
        // Test mode (-perftest <dir>): the same fight under one setting change at a time, frame times in perf.txt, then quit.
        System.Collections.IEnumerator PerfTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; yield return new WaitForSecondsRealtime(3);
            var rp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset; float dist = rp.shadowDistance; int cas = rp.shadowCascadeCount, msaa = rp.msaaSampleCount;
            log.AppendLine($"{SystemInfo.graphicsDeviceName} · {Screen.width}x{Screen.height} · msaa {msaa} · shadows {dist}m x{cas} · grass clumps {GrassField.Stats}");
            StartGame(1); hero.max = hero.hp = 999999; pet.gameObject.SetActive(false); yield return new WaitForSecondsRealtime(6);   // (the pet would kill foes and change the load)
            System.Collections.IEnumerator Measure(string label)
            {
                yield return new WaitForSecondsRealtime(1); var ms = new List<float>();
                for (float t = 0; t < 6; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); hero.hp = hero.max; yield return null; }
                ms.Sort(); float avg = 0; foreach (var m in ms) avg += m; avg /= ms.Count;
                log.AppendLine($"{label,-26} {1000 / avg,6:0.0} fps  avg {avg,6:0.0} ms  worst 5% {ms[(int)(ms.Count * .95f)],6:0.0} ms  foes {foes.Count}  grass {GrassField.Stats}");
            }
            var grass = new List<GrassField>(FindObjectsByType<GrassField>(FindObjectsSortMode.None)); var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var stageRoot = System.Array.Find(stageRoots, r => r && r.gameObject.activeSelf); var hudRoot = ui.GetComponent<UnityEngine.UIElements.UIDocument>();
            yield return Measure("as is");
            cam.enabled = false; yield return Measure("nothing drawn (CPU only)"); cam.enabled = true;
            hudRoot.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None; yield return Measure("no UI"); hudRoot.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
            stageRoot.gameObject.SetActive(false); yield return Measure("no stage"); stageRoot.gameObject.SetActive(true);
            foreach (var g in grass) g.enabled = false; yield return Measure("no grass"); foreach (var g in grass) g.enabled = true;
            var fa = new List<Foe>(foes); foreach (var f in fa) if (f) f.gameObject.SetActive(false); yield return Measure("no foes"); foreach (var f in fa) if (f) f.gameObject.SetActive(true);
            rp.shadowDistance = 0; yield return Measure("no shadows"); rp.shadowDistance = dist;
            rp.msaaSampleCount = 1; yield return Measure("no MSAA"); rp.msaaSampleCount = msaa;
            cd.renderPostProcessing = false; yield return Measure("no post"); cd.renderPostProcessing = true;
            yield return Measure("as is again");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "perf.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-bagtest <dir>): a bag with some of everything, the bag screen (a piece chosen, then worn), a boss's loot
        // dropping and being picked up in a fight, a potion drunk; shots and bag.txt, then quit.
        // Test mode (-newherotest <dir>): each regenerated hero standing in the lobby, then fighting with skills in a stage.
        System.Collections.IEnumerator NewHeroTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); save.wings = "";
            foreach (var h in new[] { "sorceress", "darkmage", "mage", "hood", "harpy", "monk" })
            {
                save.hero = h; hero.Equip(); pet.Equip(); hero.PlaceLobby(); pet.PlaceLobby(); ui.ShowLobby(); ui.lobbyZoom = 0; ui.heroYaw = 0; ui.TestFilter(hero.def.cls); yield return new WaitForSecondsRealtime(2);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"lobby_{h}.png")); yield return new WaitForSecondsRealtime(.5f); ui.TestFilter("all");
                save.best = 5; StartGame(1); int shot = 0;
                for (float t = 0; t < 7; t += Time.deltaTime)
                {
                    hero.inv = 0; var near = foes.Where(f => f && f.alive && f.team != 0).OrderBy(f => (f.Pos - hero.Pos).sqrMagnitude).FirstOrDefault();
                    if (near) { var d = Flat(near.Pos - hero.Pos); ui.Joy = d.magnitude > 2.6f ? new Vector2(-d.x, d.z).normalized : Vector2.zero; }
                    if (Random.value < .12f) ui.atkP = true; if (Random.value < .03f) ui.skP[Random.Range(0, 3)] = true; hero.hp = hero.max; hero.mp = hero.mpMax;
                    if (shot == 0 && t > 5) { ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"fight_{h}.png")); shot = 1; }
                    yield return null;
                }
                ui.Joy = Vector2.zero; ShowLobby(); yield return new WaitForSecondsRealtime(.5f);
            }
            Application.Quit();
        }
        // -bakecards <dir>: writes every card picture as a file for Resources/UI/Cards, then quits. Run it again when a hero, pet, wing or weapon changes.
        System.Collections.IEnumerator BakeCards(string dir)
        {
            yield return new WaitForSecondsRealtime(3); System.IO.Directory.CreateDirectory(dir); int n = 0;
            bool fetched = false;
            foreach (var h in Data.Heroes) foreach (var (_, m) in Data.ModelSkins(h.id))
            {   // model skins live in bundles: fetch every one, then shoot all the cards again with them in hand
                bool done = false; SkinPacks.Fetch(m, () => done = true); float t0 = Time.realtimeSinceStartup;
                while (!done && SkinPacks.Get(m) == null && Time.realtimeSinceStartup - t0 < 20) yield return null; fetched = true;
            }
            if (fetched) Studio.Build();
            foreach (var kv in Studio.Rendered) { System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, kv.Key + ".png"), kv.Value.EncodeToPNG()); n++; }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "baked.txt"), n.ToString()); Application.Quit();
        }
        // -stageshots <dir>: writes the stage cards as files for Resources/UI/Stages, then quits. Run it again when a stage's look changes.
        System.Collections.IEnumerator StageShots(string dir)
        {
            yield return new WaitForSecondsRealtime(3); System.IO.Directory.CreateDirectory(dir);
            for (int i = 0; i < Data.Themes.Length; i++) System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"stage_{i}.jpg"), Studio.Stage(i).EncodeToJPG(90));
            Application.Quit();
        }
        // Test mode (-hitchtest, or ?hitch in the web address): how long each step of picking a hero blocks the game, written to the log
        System.Collections.IEnumerator HitchTest(bool quit)
        {
            hitchLog = false; Debug.Log($"[Hitch] first frames at {Time.realtimeSinceStartup:0.0}s"); yield return new WaitForSecondsRealtime(8); Debug.Log("[Hitch] " + Studio.Timing);
            if (Application.absoluteURL.Contains("wildsparts"))
            {   // ?wildsparts: what the zone costs part by part, at the quality the web starts at
                AutoQuality.Paused = true; save.hero = "warrior"; StartWilds(); hero.transform.position = new Vector3(0, 0, 8); yield return new WaitForSecondsRealtime(6); var zone = Stage("Stage_wilds"); var rp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset;
                System.Collections.IEnumerator M(string label) { var ms = new List<float>(); yield return new WaitForSecondsRealtime(1.5f); for (float t = 0; t < 4; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); yield return null; } ms.Sort(); Debug.Log($"[Hitch] part {label} median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms"); }
                yield return M("everything");
                var grass = zone.GetComponentInChildren<GrassField>(); grass.enabled = false; yield return M("no grass"); grass.enabled = true;
                var trees = zone.GetComponentsInChildren<Renderer>().Where(r => r.name.ToLower().Contains("tree") || r.transform.root != r.transform && r.transform.parent.name.ToLower().Contains("tree")).ToArray(); foreach (var r in trees) r.enabled = false; yield return M($"no trees ({trees.Length})"); foreach (var r in trees) r.enabled = true;
                float sd = rp.shadowDistance; rp.shadowDistance = 0; yield return M("no shadows"); rp.shadowDistance = sd;
                var ter = zone.GetComponentInChildren<Terrain>(); if (ter) { ter.drawHeightmap = false; yield return M("no terrain"); ter.drawHeightmap = true; }
                hero.max = hero.hp = 999999; hero.transform.position = new Vector3(42, 0, -14); yield return new WaitForSecondsRealtime(4); ui.atkP = true; yield return M("at the orc camp with monsters"); ui.atkP = false; Debug.Log($"[Hitch] part monsters alive {wildsCtl.AliveInAreas}");
                wildsCtl.Clock.t = 245; yield return new WaitForSecondsRealtime(2); ui.atkP = true; yield return M("the camp at night"); ui.atkP = false;
                Debug.Log("[Hitch] parts done"); yield break;
            }
            if (Application.absoluteURL.Contains("hitchsettle"))
            {   // ?hitchsettle: what the lobby and a fight run at once the automatic quality has settled
                var ms = new List<float>(); yield return new WaitForSecondsRealtime(25); for (float t = 0; t < 6; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); yield return null; }
                ms.Sort(); Debug.Log($"[Hitch] settled lobby level {AutoQuality.Level} median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms");
                save.hero = "warrior"; StartGame(1); hero.max = hero.hp = 999999; yield return new WaitForSecondsRealtime(20); ms.Clear(); for (float t = 0; t < 6; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); hero.hp = hero.max; yield return null; }
                ms.Sort(); Debug.Log($"[Hitch] settled fight level {AutoQuality.Level} median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms"); yield return CrowdMeasure(); StartCity(); yield return new WaitForSecondsRealtime(8); ms.Clear(); for (float t = 0; t < 6; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); yield return null; } ms.Sort(); Debug.Log($"[Hitch] settled city level {AutoQuality.Level} median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms"); if (!Stage("Stage_wilds")) yield break; StartWilds(); yield return new WaitForSecondsRealtime(8); ms.Clear(); for (float t = 0; t < 6; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); yield return null; } ms.Sort(); Debug.Log($"[Hitch] settled wilds level {AutoQuality.Level} median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms"); Debug.Log("[Hitch] done"); yield break;
            }
            AutoQuality.Paused = true;
            {   // the lobby piece by piece: what the frame time drops to with each part off
                var rp = UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset; var cd = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); var doc = ui.GetComponent<UnityEngine.UIElements.UIDocument>();
                Debug.Log($"[Hitch] gpu {SystemInfo.graphicsDeviceName} | {SystemInfo.graphicsDeviceVendor} | {Screen.width}x{Screen.height} dpi {Screen.dpi:0} scale {rp.renderScale} msaa {rp.msaaSampleCount} target {Application.targetFrameRate}");
                System.Collections.IEnumerator M(string label)
                {
                    yield return new WaitForSecondsRealtime(1.5f); var ms = new List<float>(); for (float t = 0; t < 4; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); yield return null; }
                    ms.Sort(); Debug.Log($"[Hitch] lobby {label,-16} median {ms[ms.Count / 2],6:0.0} ms  worst5% {ms[(int)(ms.Count * .95f)],6:0.0} ms  frames {ms.Count}");
                }
                yield return M("as is");
                doc.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.None; yield return M("no UI"); doc.rootVisualElement.style.display = UnityEngine.UIElements.DisplayStyle.Flex;
                var parts = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None); foreach (var p in parts) p.gameObject.SetActive(false); yield return M("no particles"); foreach (var p in parts) if (p) p.gameObject.SetActive(true);
                hero.gameObject.SetActive(false); pet.gameObject.SetActive(false); yield return M("no hero or pet"); hero.gameObject.SetActive(true); pet.gameObject.SetActive(true);
                var grass = FindObjectsByType<GrassField>(FindObjectsSortMode.None); foreach (var g in grass) g.enabled = false; yield return M("no grass"); foreach (var g in grass) g.enabled = true;
                var sr = System.Array.Find(stageRoots, r => r && r.gameObject.activeSelf); if (sr) { sr.gameObject.SetActive(false); yield return M("no stage"); sr.gameObject.SetActive(true); }
                float sd = rp.shadowDistance; rp.shadowDistance = 0; yield return M("no shadows"); rp.shadowDistance = sd;
                cd.renderPostProcessing = false; yield return M("no post effects"); cd.renderPostProcessing = true;
                int ma = rp.msaaSampleCount; rp.msaaSampleCount = 1; yield return M("no MSAA"); rp.msaaSampleCount = ma;
                float rs = rp.renderScale; rp.renderScale = .5f; yield return M("half resolution"); rp.renderScale = rs;
                cam.enabled = false; yield return M("no 3D at all"); cam.enabled = true;
            }
            var sw = new System.Diagnostics.Stopwatch();
            float Lap(System.Action a) { sw.Restart(); a(); sw.Stop(); return (float)sw.Elapsed.TotalMilliseconds; }
            for (int round = 1; round <= 2; round++)
                foreach (var h in new[] { "warrior", "samurai", "harpy", "sunpriest", "sorceress", "monk", "spartan" })
                {
                    save.hero = h; float eq = Lap(hero.Equip), pe = Lap(pet.Equip), sv = Lap(Persist), info = Lap(ui.TestRenderInfo), cards = Lap(ui.TestRenderCards); Debug.Log($"[Hitch] picked {h}"); yield return new WaitForSecondsRealtime(1.5f);
                    Debug.Log($"[Hitch] round {round} {h,-10} equip {eq,6:0.0} pet {pe,5:0.0} save {sv,5:0.0} info {info,5:0.0} cards {cards,6:0.0} ms");
                }
            if (quit) Application.Quit();
        }
        // Test mode (-gaittest <dir>): every hero's lobby stance and run: how high the feet stay off the ground, how fast the run clip plays, pictures of both
        // Test mode (-foottest <dir> <ids|all>): how long each foot of the skin is, standing and while walking, and close side pictures of the feet mid-stride
        System.Collections.IEnumerator FootTest(string dir, string ids)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; save.helm = save.armor = save.boots = save.acc = ""; save.shield = "";
            var cam = new GameObject("FootCam").AddComponent<Camera>(); cam.enabled = false; cam.fieldOfView = 30; cam.nearClipPlane = .05f; var rt = new RenderTexture(640, 400, 24); cam.targetTexture = rt;
            void Shot(string n) { cam.Render(); var was = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(640, 400, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 640, 400), 0, 0); RenderTexture.active = was; System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, n + ".png"), t.EncodeToPNG()); Destroy(t); }
            var baked = new Mesh();
            // the furthest skin point from the ankle, among the points below the ankle and near it: the length of the shoe
            float Sole(SkinnedMeshRenderer sk, Transform foot) { sk.BakeMesh(baked, true); var v = baked.vertices; var m = sk.transform.localToWorldMatrix; float best = 0; var f = foot.position; foreach (var x in v) { var w = m.MultiplyPoint3x4(x); if (w.y > f.y + .02f) continue; var d = w - f; if (d.magnitude > .45f) continue; best = Mathf.Max(best, new Vector2(d.x, d.z).magnitude); } return best; }
            foreach (var d in Data.Heroes)
            {
                if (ids != "all" && !ids.Split(',').Contains(d.id)) continue; if (!d.rig) continue;
                save.hero = d.id; StartCity(); pet.gameObject.SetActive(false); hero.max = hero.hp = 999999; yield return new WaitForSecondsRealtime(1.2f);
                var an = hero.GetComponentInChildren<Animator>(); var sk = hero.GetComponentInChildren<SkinnedMeshRenderer>(); if (!an || !sk || !an.isHuman) { log.AppendLine($"{d.id}: no humanoid"); continue; } log.AppendLine($"{d.id}: readable {sk.sharedMesh.isReadable} {FootWeights.Report(sk)}");
                var fl = an.GetBoneTransform(HumanBodyBones.LeftFoot); var fr = an.GetBoneTransform(HumanBodyBones.RightFoot); var tl = an.GetBoneTransform(HumanBodyBones.LeftToes);
                float standL = Sole(sk, fl), standR = Sole(sk, fr), walkL = 0, walkR = 0; float toeRest = tl ? Vector3.Distance(fl.position, tl.position) : 0, toeMax = toeRest;
                int k = 0; for (float t = 0; t < 2.6f; t += Time.deltaTime) { hero.inv = 0; ui.Joy = new Vector2(1, 0); if (t > .8f) { walkL = Mathf.Max(walkL, Sole(sk, fl)); walkR = Mathf.Max(walkR, Sole(sk, fr)); if (tl) toeMax = Mathf.Max(toeMax, Vector3.Distance(fl.position, tl.position)); }
                    if (t > 1.2f && k < 6 && Time.frameCount % 5 == 0) { var p = hero.Pos; cam.transform.position = p + new Vector3(0, .35f, -2.1f); cam.transform.LookAt(p + Vector3.up * .25f); Shot($"{d.id}_{k++}"); } yield return null; }
                ui.Joy = Vector2.zero; pet.gameObject.SetActive(true);
                log.AppendLine($"{d.id,-10} sole stand L {standL:0.00} R {standR:0.00} walk max L {walkL:0.00} R {walkR:0.00} (x{Mathf.Max(walkL / Mathf.Max(standL, .01f), walkR / Mathf.Max(standR, .01f)):0.00}) foot-toe bone {toeRest:0.000} max {toeMax:0.000} toes {(tl ? tl.name : "none")}");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "foot.txt"), log.ToString()); Application.Quit();
        }
        System.Collections.IEnumerator GaitTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; save.helm = save.armor = save.boots = save.acc = ""; save.shield = ""; save.sword = Data.Swords[0].id;
            foreach (var d in Data.Heroes)
            {
                save.hero = d.id; ShowLobby(); ui.lobbyZoom = 0; ui.heroYaw = 0; yield return new WaitForSecondsRealtime(2.2f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"lobby_{d.id}.png")); yield return new WaitForSecondsRealtime(.4f);
                save.best = 5; StartGame(1); pet.gameObject.SetActive(false); hero.max = hero.hp = 999999; yield return new WaitForSecondsRealtime(1.2f); float idle = 9, run = 9, top = -9, rate = 0; int n = 0;
                float Foot() { var a = hero.GetComponentInChildren<Animator>(); if (!a || !a.isHuman) return 0; float m = 9; foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot, HumanBodyBones.LeftToes, HumanBodyBones.RightToes }) { var t = a.GetBoneTransform(b); if (t) m = Mathf.Min(m, t.position.y - hero.Pos.y); } return m; }
                for (float t = 0; t < 1; t += Time.deltaTime) { hero.inv = 0; idle = Mathf.Min(idle, Foot()); yield return null; }
                bool shot = false;
                for (float t = 0; t < 3.2f; t += Time.deltaTime) { hero.inv = 0; hero.hp = hero.max; ui.Joy = new Vector2(Mathf.Sin(t), -1).normalized; if (d.id == "samurai" && Time.frameCount % 6 == 0 && t > 1.2f && t < 2.2f) ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"walk_{d.id}_{Mathf.RoundToInt(t * 60)}.png")); if (t > .6f) { float f = Foot(); run = Mathf.Min(run, f); top = Mathf.Max(top, f); var a = hero.GetComponentInChildren<Animator>(); if (a) { rate += a.GetFloat("Speed"); n++; } if (!shot && t > 2) { shot = true; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"run_{d.id}.png")); } } yield return null; }
                ui.Joy = Vector2.zero; pet.gameObject.SetActive(true);
                log.AppendLine($"{d.id,-10} {d.name,-8} speed {d.spd:0.00} set {Data.AnimSet(d.id) ?? "base",-8} clip rate {(n > 0 ? rate / n : 0):0.00}  lowest foot idle {idle:0.000} run {run:0.000} highest {top:0.000}");
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "gait.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-citytest <dir>): walk the city, open each NPC's panel, go through the gate and come back, with pictures of each
        System.Collections.IEnumerator CityTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; save.hero = "samurai"; hero.Equip(); hero.PlaceLobby();
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            ui.ShowLobby(); yield return new WaitForSecondsRealtime(1); Snap("lobby"); yield return new WaitForSecondsRealtime(.5f);
            StartCity(); yield return new WaitForSecondsRealtime(2); Snap("start"); yield return new WaitForSecondsRealtime(.5f);
            log.AppendLine($"city {city} mode {mode} foes {foes.Count} hero {hero.Pos} plate ok");
            foreach (var (name, at, panel) in new[] { ("quest", new Vector3(-11.5f, 0, 3), "quest"), ("shop", new Vector3(0, 0, -12.5f), "shop"), ("smith", new Vector3(11.5f, 0, 3), "bag"), ("gate", new Vector3(-7, 0, -12), "gate") })
            {
                hero.transform.position = at; yield return new WaitForSecondsRealtime(1.4f); Snap("near_" + name); yield return new WaitForSecondsRealtime(.4f); log.AppendLine($"{name}: prompt shown {ui.CityPromptShown}");
                ui.OpenCityPanel(panel); yield return new WaitForSecondsRealtime(1); Snap("panel_" + name); yield return new WaitForSecondsRealtime(.4f); log.AppendLine($"{name}: panel open {ui.CityPanelOpen}");
                ui.CloseCityPanel(); yield return new WaitForSecondsRealtime(.4f); log.AppendLine($"{name}: closed {!ui.CityPanelOpen} hud ok");
            }
            foreach (var (name, at) in new[] { ("tree1", new Vector3(11, 0, -3.5f)), ("tree2", new Vector3(-15, 0, -7)), ("tree3", new Vector3(5, 0, -16)) }) { hero.transform.position = at; yield return new WaitForSecondsRealtime(1); Snap(name); yield return new WaitForSecondsRealtime(.4f); }
            ui.atkP = true; yield return null; yield return null; log.AppendLine($"attack in city blocked: state {hero.state}");
            StartGame(1); cameFromCity = true; yield return new WaitForSecondsRealtime(3); log.AppendLine($"stage: city {city} foes {foes.Count}"); Snap("stage");
            yield return new WaitForSecondsRealtime(.4f); LeaveStage(); yield return new WaitForSecondsRealtime(2); log.AppendLine($"back: city {city} foes {foes.Count} hero {hero.Pos}"); Snap("back");
            yield return new WaitForSecondsRealtime(.4f); LeaveStage(); yield return new WaitForSecondsRealtime(1); log.AppendLine($"leave city: mode {mode} city {city}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "city.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-questtest <dir>): counters, claiming once only, a finished quest in a real fight, and the quest panel on screen.
        System.Collections.IEnumerator QuestTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            save.questKeys = new(); save.questVals = new(); save.questClaimed = new(); save.gold = 1000; save.gems = 100; save.hero = "warrior"; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby();
            var first = Quests.All[0]; log.AppendLine($"start {first.id}: {Quests.Progress(save, first)}/{first.goal} ready {Quests.Ready(save, first)}");
            log.AppendLine($"claim too early: {Quests.Claim(save, first)} gold {save.gold}");
            Quests.Bump(save, "kill", 12); Quests.Bump(save, "orb", 7); Quests.Bump(save, "wear"); Quests.Bump(save, "clear");
            log.AppendLine($"after counts: {first.id} {Quests.Progress(save, first)}/{first.goal} ready {Quests.Ready(save, first)}, ready total {Quests.ReadyCount(save)}");
            ui.ShowLobby(); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby.png")); yield return new WaitForSecondsRealtime(.5f);
            ui.ShowQuests(true); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "quests.png")); yield return new WaitForSecondsRealtime(.5f);
            int g0 = save.gold; log.AppendLine($"claim: {Quests.Claim(save, first)} gold {g0} -> {save.gold}"); log.AppendLine($"claim twice: {Quests.Claim(save, first)} gold {save.gold}");
            var trip = JsonUtility.FromJson<Save>(JsonUtility.ToJson(save)); log.AppendLine($"save round trip: kill {Quests.Count(trip, "kill")} claimed {Quests.Claimed(trip, first)}");
            var oldSave = JsonUtility.FromJson<Save>("{\"gold\":5}"); log.AppendLine($"old save: kill {Quests.Count(oldSave, "kill")} level {Quests.Count(oldSave, "level")} ready {Quests.ReadyCount(oldSave)}");
            ui.ShowQuests(true); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "quests_claimed.png")); yield return new WaitForSecondsRealtime(.5f); ui.ShowQuests(false);
            save.hero = "warrior"; StartGame(1); hero.inv = 0; int k0 = Quests.Count(save, "kill"), o0 = Quests.Count(save, "orb"); AddDrop(hero.Pos + Vector3.right, "gold", 5);
            for (float t = 0; t < 14 && Quests.Count(save, "kill") < k0 + 3; t += Time.deltaTime) { foreach (var f in foes) if (f && f.alive && f.team != 0) f.hp = 1; var near = foes.Where(f => f && f.alive && f.team != 0).OrderBy(f => (f.Pos - hero.Pos).sqrMagnitude).FirstOrDefault(); if (near) { var d = Flat(near.Pos - hero.Pos); ui.Joy = d.magnitude > 2.6f ? new Vector2(-d.x, d.z).normalized : Vector2.zero; } ui.atkP = true; hero.hp = hero.max; yield return null; }
            ui.Joy = Vector2.zero; log.AppendLine($"in fight: kills {k0} -> {Quests.Count(save, "kill")}, orbs {o0} -> {Quests.Count(save, "orb")}");
            save.questKeys = new(); save.questVals = new(); Quests.Bump(save, "kill", first.goal - 1); var nf = foes.FirstOrDefault(f => f && f.alive && f.team != 0);
            while (!nf) { nf = foes.FirstOrDefault(f => f && f.alive && f.team != 0); yield return null; }
            nf.Damage(99999, nf.Pos); yield return null; ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "toast.png")); yield return new WaitForSecondsRealtime(.5f);
            log.AppendLine($"last kill: {Quests.Progress(save, first)}/{first.goal}");
            hero.inv = 1; yield return null; var wings = hero.transform.Find("Wings"); log.AppendLine($"blink test: wings present {(bool)wings}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "quests.txt"), log.ToString()); Application.Quit();
        }
        System.Collections.IEnumerator BagTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            save.hero = "samurai"; save.bag.Clear(); save.helm = save.armor = save.boots = save.acc = "";
            foreach (var id in new[] { "helm_iron", "armor_knight", "boots_leather", "ring_shadow", "helm_dragon", "armor_leather", "boots_knight", "ring_leather" }) save.bag.Add(id);
            save.potions = 5; save.mats = new List<int> { 3, 1, 0, 2, 0 }; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby.png")); yield return new WaitForSecondsRealtime(.4f);
            ui.TestBag("g:4"); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "bag_select.png")); yield return new WaitForSecondsRealtime(.4f);
            // wear four pieces the way the buttons do
            save.helm = "helm_dragon"; save.armor = "armor_knight"; save.boots = "boots_knight"; save.acc = "ring_shadow"; save.bag.RemoveAll(i => i is "helm_dragon" or "armor_knight" or "boots_knight" or "ring_shadow");
            ui.TestBag("worn:Armor"); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "bag_worn.png")); yield return new WaitForSecondsRealtime(.4f);
            var gb = Items.Bonus(save); log.AppendLine($"gear bonus hp {gb.hp} def {gb.def:0.000} spd {gb.spd:0.00} crit {gb.crit:0.000} atk {gb.atk:0.000}");
            // enhancing: rules checked directly, then the enhance box on screen
            {
                save.gold = 1000000; save.mats = new List<int> { 500, 500, 500, 500, 500 }; string t = "helm_iron"; float hp0 = Items.Get(t).hp; int tries = 0, fails = 0; bool dropped = false;
                while (Items.Level(t) < Items.MaxLevel && tries < 5000) { int b4 = Items.Level(t); var r = Items.Enhance(save, ref t); tries++; if (r == EnhanceResult.Failed) fails++; if (Items.Level(t) < b4) dropped = true; }
                log.AppendLine($"enhance to max: {t} after {tries} tries, {fails} failed, level ever dropped: {dropped}");
                log.AppendLine($"past max: {Items.Enhance(save, ref t)} id {t}");
                var rich = new Save { gold = int.MaxValue }; rich.mats = new List<int> { int.MaxValue, 0, 0, 0, 0 };   // measured success rate at each level against the table
                for (int lv = 0; lv < Items.MaxLevel; lv++) { int wins = 0; for (int i = 0; i < 20000; i++) { string x = Items.WithLevel("helm_leather", lv); if (Items.Enhance(rich, ref x) == EnhanceResult.Success) wins++; rich.gold = int.MaxValue; } log.AppendLine($"+{lv}: table {Items.Chance(lv) * 100:0}% measured {wins / 200f:0.0}%"); }
                string w = save.helm; save.helm = t; var g10 = Items.Bonus(save); save.helm = "helm_iron"; var g0 = Items.Bonus(save); save.helm = w;
                log.AppendLine($"+10 adds hp {g10.hp - g0.hp:0.0} (base {hp0}, expected {hp0 * .8f:0.0})");
                var poor = new Save { gold = 0 }; poor.mats = new List<int> { 0, 0, 0, 0, 0 }; string p1 = "ring_iron"; var noShard = Items.Enhance(poor, ref p1); poor.mats[1] = 5; var noGold = Items.Enhance(poor, ref p1);
                log.AppendLine($"no shards: {noShard}, no gold: {noGold}, untouched: {p1} shards {poor.mats[1]}");
                log.AppendLine($"old id: level {Items.Level("helm_iron")} get {Items.Get("helm_iron")?.id} · name {Items.Name("helm_iron+3")} · bad id level {Items.Level("helm_iron+x")}");
                var trip = JsonUtility.FromJson<Save>(JsonUtility.ToJson(new Save { helm = "helm_iron+7" })); log.AppendLine($"save round trip: {trip.helm}");
                save.bag.Add("ring_leather+3"); save.bag.Add("ring_leather"); log.AppendLine($"bulk sell takes: {string.Join(",", Items.Junk(save))}");
                log.AppendLine($"+3 dismantle {Items.DismantleShards("ring_leather+3")} shards vs {Items.DismantleShards("ring_leather")}, sell {Items.SellPrice("ring_leather+3")} vs {Items.SellPrice("ring_leather")}");
                save.gold = 300; save.mats = new List<int> { 3, 1, 0, 2, 0 }; save.armor = "armor_knight+4"; ui.TestBag("worn:Armor"); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "bag_enhance.png")); yield return new WaitForSecondsRealtime(.4f);
                ui.TestBag("g:" + (save.bag.Count - 2)); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "bag_enhance_bagpiece.png")); yield return new WaitForSecondsRealtime(.4f);
            }
            ui.ShowBag(false); StartGame(1); yield return new WaitForSecondsRealtime(2);
            // one of each orb out of magnet range, to see them at rest
            { int oi = 0; foreach (var k in new[] { "gold", "heal", "mana", "fury", "quick" }) AddDrop(hero.Pos + new Vector3(-6 + 3 * oi++, 0, -6.5f), k, 5); yield return new WaitForSecondsRealtime(1.2f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "orbs.png")); yield return new WaitForSecondsRealtime(.4f); }
            log.AppendLine($"in fight: max hp {hero.max} (base {hero.def.hp}) atk {hero.AtkPow:0}");
            int bagBefore = save.bag.Count, potBefore = save.potions; hero.mp = 10; for (int c = 0; c < 3; c++) hero.cds[c] = 20; float atk0 = hero.AtkPow; int gold0 = gold;
            DropLoot(hero.Pos + new Vector3(2.5f, 0, 0), true, 10); yield return new WaitForSecondsRealtime(.9f);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "loot_drop.png")); yield return new WaitForSecondsRealtime(4);
            log.AppendLine($"after boss loot: bag {bagBefore} -> {save.bag.Count}, potions {potBefore} -> {save.potions}");
            log.AppendLine($"orbs: gold {gold0} -> {gold}, mp 10 -> {hero.mp:0}, cooldown 20 -> {hero.cds[0]:0.0}, fury left {hero.orbAtkT:0.0}, atk {atk0:0} -> {hero.AtkPow:0}");
            // the pause bag: new gear first, worn straight from a fight at the same share of health, nothing moves while it is open
            hero.hp = hero.max * .5f; save.acc = ""; save.boots = ""; hero.RefreshGear(); log.AppendLine($"run loot: {string.Join(",", runLoot)}");
            if (System.Environment.GetCommandLineArgs().Contains("-bigloot")) for (int k = 0; k < 4; k++) { DropLoot(hero.Pos + new Vector3(2.5f, 0, k), true, 10); yield return new WaitForSecondsRealtime(4); }   // a big haul, for the wrapping grid
            ui.OpenFieldBag(); yield return new WaitForSecondsRealtime(.8f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "field_new.png")); yield return new WaitForSecondsRealtime(.4f);
            log.AppendLine($"opened: paused {paused} timeScale {Time.timeScale}");
            float max0 = hero.max, hp0f = hero.hp; string atkLine = ui.FieldWearFirst(); log.AppendLine($"wore {atkLine}: max {max0:0} -> {hero.max:0}, hp {hp0f:0} -> {hero.hp:0}, run loot left {runLoot.Count}");
            yield return new WaitForSecondsRealtime(.5f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "field_worn.png")); yield return new WaitForSecondsRealtime(.4f);
            ui.CloseFieldBag(); yield return new WaitForSecondsRealtime(.3f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "field_pause.png")); yield return new WaitForSecondsRealtime(.3f);
            ui.ResumeFromPause(); yield return new WaitForSecondsRealtime(.3f); log.AppendLine($"resumed: paused {paused} timeScale {Time.timeScale}");
            { int g0 = save.gold, run = gold, b0 = banked; BankGold(); int once = save.gold; BankGold(); log.AppendLine($"bank on leaving: run gold {run}, already banked {b0}, saved {g0} -> {once}, second time {save.gold}"); }
            {   // levels: one level up restores health and raises both stats; EXP table and the cap
                int lv0 = hero.Level; float a0 = hero.AtkPow, m0 = hero.max; hero.hp = hero.max * .3f; AddXp(Data.XpFor(lv0 + 1) - save.HeroXp(hero.def.id));
                log.AppendLine($"level {lv0} -> {hero.Level}: max hp {m0:0} -> {hero.max:0}, hp full {hero.hp >= hero.max - .5f}, atk {a0:0} -> {hero.AtkPow:0}, run exp {runXp}");
                log.AppendLine($"exp table: L2 {Data.XpFor(2)} L3 {Data.XpFor(3)} L5 {Data.XpFor(5)} L10 {Data.XpFor(10)} L50 {Data.XpFor(50)} · level at 279 = {Data.Level(279)}, at 280 = {Data.Level(280)}, at a huge total = {Data.Level(100000000)} · old save {new Save().HeroXp("x")}");
                yield return new WaitForSecondsRealtime(.7f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "level_up.png")); yield return new WaitForSecondsRealtime(.4f);
            }
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
        // a crowded fight with the hero attacking non-stop: what players see at the worst moments
        const int CrowdSize = 16;
        System.Collections.IEnumerator CrowdMeasure()
        {
            queue.Clear(); for (int i = 0; i < CrowdSize; i++) { float a = i * Mathf.PI * 2 / CrowdSize; var f = SpawnFoe(i % 3 == 0 ? "orc" : "goblin", hero.Pos + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 4); f.hp = f.maxHp = 1e7f; }
            yield return new WaitForSecondsRealtime(3); var ms = new List<float>();
            for (float t = 0; t < 8; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); hero.hp = hero.max; hero.mp = hero.mpMax; ui.atkP = true; if (Random.value < .02f) ui.skP[Random.Range(0, 3)] = true; yield return null; }
            ui.atkP = false; ms.Sort(); Debug.Log($"[Hitch] settled crowd level {AutoQuality.Level} median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms");
        }
        // Test mode (-crowdtest <dir>): the crowded fight in pictures, with its frame time in the log.
        System.Collections.IEnumerator CrowdTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); save.hero = "warrior"; StartGame(2); hero.max = hero.hp = 999999; yield return new WaitForSecondsRealtime(2);
            yield return CrowdMeasure(); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "crowd.png")); yield return new WaitForSecondsRealtime(.5f); Application.Quit();
        }
        // Test mode (-gatewalk <dir>): walk (through the same limits as the player) up to every place in the city and the zone; can the prompt be reached?
        // Test mode (-cavetest <dir>): the petal trail, the door by day and by night, the fight, the chest once per day, the way out. Leaves the save as it found it.
        System.Collections.IEnumerator CaveTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; var keep = JsonUtility.ToJson(save); save.caveDay = "";
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            StartCity(); yield return new WaitForSecondsRealtime(1); StartWilds(); yield return new WaitForSecondsRealtime(1.5f); hero.max = hero.hp = 999999; var z = wildsCtl; var door = z.CaveDoor; log.AppendLine($"door {door}");
            MoveTo(new Vector3(-6, 0, -18)); yield return new WaitForSecondsRealtime(4); Snap("trail_a"); yield return new WaitForSecondsRealtime(2); Snap("trail_b");
            MoveTo(new Vector3(-2, 0, -38)); yield return new WaitForSecondsRealtime(3); Snap("trail_c"); yield return new WaitForSecondsRealtime(.2f);
            MoveTo(door + new Vector3(0, 0, 2.4f)); yield return new WaitForSecondsRealtime(1.5f); Snap("door_day"); yield return new WaitForSecondsRealtime(.2f); z.TryCave(); log.AppendLine($"by day: in cave {z.InCave}"); yield return new WaitForSecondsRealtime(.3f); Snap("door_day_msg");
            z.Clock.t = 239; yield return new WaitForSecondsRealtime(2.5f); Snap("door_night"); yield return new WaitForSecondsRealtime(.2f); log.AppendLine($"night {z.Clock.IsNight}"); int ownedBefore = save.owned.Count;
            z.TryCave(); yield return new WaitForSecondsRealtime(1.5f); log.AppendLine($"by night: in cave {z.InCave} house {house} foes {foes.Count(f => f && f.alive)} chest ready {HouseFought}"); Snap("cave_room");
            foreach (var x in foes.ToArray()) if (x && x.alive) x.Damage(1e7f, x.Pos); yield return new WaitForSecondsRealtime(.6f); log.AppendLine($"guards down: chest ready {HouseFought}");
            MoveTo(OpenWorld.Chest + new Vector3(0, 0, 2)); yield return new WaitForSecondsRealtime(.6f); Snap("cave_chest"); var kb = z.GetType().GetMethod("OpenCaveChest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); kb.Invoke(z, null); yield return new WaitForSecondsRealtime(.5f); Snap("cave_open"); log.AppendLine($"opened: weapons {ownedBefore} -> {save.owned.Count} ({(save.owned.Count > ownedBefore ? save.owned[save.owned.Count - 1] : "")}) caveDay {save.caveDay} taken {z.CaveTaken}"); int o2 = save.owned.Count; kb.Invoke(z, null); log.AppendLine($"opened again: weapons {o2} -> {save.owned.Count}");
            LeaveHouse(); yield return new WaitForSecondsRealtime(1); log.AppendLine($"left: wilds {wilds} house {house} inCave {z.InCave} at {hero.Pos} sun {RenderSettings.sun.intensity:0.00}"); Snap("left"); z.TryCave(); log.AppendLine($"same day again: in cave {z.InCave}");
            save.caveDay = "2000-01-01"; log.AppendLine($"next day: taken {z.CaveTaken}"); z.TryCave(); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"next day: in cave {z.InCave}"); FallInZone(); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"fell in cave: wilds {wilds} inCave {z.InCave} at {hero.Pos}");
            EnterHouse(); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"house after cave: inCave {z.InCave} foes {foes.Count(f => f && f.alive)} sun {RenderSettings.sun.intensity:0.00}"); Snap("house_after");
            BackToZone(OpenWorld.Start + new Vector3(0, 0, -4)); yield return new WaitForSecondsRealtime(1.5f); wildsCtl.Clock.t = 100; yield return new WaitForSecondsRealtime(.5f); hero.inv = 0; hero.Hurt(40, hero.Pos + new Vector3(0, 0, -2)); yield return null; Snap("hit_a"); yield return new WaitForSecondsRealtime(.15f); Snap("hit_b"); yield return new WaitForSecondsRealtime(.25f); Snap("hit_c"); wildsCtl.Clock.t = 250; yield return new WaitForSecondsRealtime(.5f); hero.inv = 0; hero.Hurt(40, hero.Pos + new Vector3(0, 0, -2)); yield return new WaitForSecondsRealtime(.12f); Snap("hit_night"); log.AppendLine($"hit: model shown {hero.GetComponentsInChildren<SkinnedMeshRenderer>().Length > 0}");
            save = JsonUtility.FromJson<Save>(keep); Persist();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "cave.txt"), log.ToString()); yield return new WaitForSecondsRealtime(.5f); Application.Quit();
        }
        // Test mode (-duelstest <dir>): the hilltop portal, each duel on the Sky Court (moves seen, guard and sidestep against swings), winning
        // and its rewards, the merchant, the wanderer gone after his loss, the legend's hint and ambush by night, losing a duel. Leaves the save as it found it.
        System.Collections.IEnumerator DuelsTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; save.hero = "warrior"; hero.Equip(); var keep = JsonUtility.ToJson(save); save.legendDay = "";
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            var useDuel = typeof(OpenWorld).GetMethod("UseDuel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Collections.IEnumerator Watch(string tag, float secs, bool swing)
            {   // the hero cannot fall; logs every move the duellist makes, and the frame time
                hero.max = hero.hp = 999999; var seen = new Dictionary<string, int>(); string last = null; float ms = 0; int frames = 0, shot = 0, blocked = 0, missed = 0; float swingT = 0;
                for (float t = 0; t < secs; t += Time.unscaledDeltaTime)
                {
                    var b = boss; if (b && b.alive && b.Move != last) { last = b.Move; seen[last] = seen.TryGetValue(last, out var k) ? k + 1 : 1; }
                    if (swing && b && b.alive && t > swingT) { swingT = t + .45f; float hpWas = b.hp; b.Damage(40, hero.Pos, new Hit { melee = true, poise = 12, face = hero.face }); if (b.hp >= hpWas - 1) missed++; else if (b.hp > hpWas - 30) blocked++; }
                    if (b && b.alive) { var to = Flat(hero.Pos - b.Pos); if (to.magnitude > 4) hero.transform.position -= to.normalized * Time.deltaTime * 3; }   // stay close: the hero walks toward it
                    ms += Time.unscaledDeltaTime * 1000; frames++;
                    if (t > shot * secs / 3 + 1.5f && shot < 3) Snap($"{tag}_{shot++}");
                    yield return null;
                }
                log.AppendLine($"{tag}: moves {string.Join(" ", seen.Select(kv => kv.Key + "x" + kv.Value))} · swings blocked {blocked} missed {missed} · avg frame {ms / Mathf.Max(1, frames):0.0} ms · boss hp {(boss ? boss.hp : 0):0}/{(boss ? boss.maxHp : 0):0}");
            }
            string Sync() => boss ? $"sync: hero Lv{hero.Level} atk {hero.AtkPow:0} hp {hero.max:0} edge +{DuelEdge * 100:0}% · {boss.d.name} hp {boss.maxHp:0} = {boss.maxHp / hero.AtkPow:0.0} light blows, full hit {boss.d.dmg:0} = {hero.max / boss.d.dmg:0.0} to fall (before armour)" : "sync: no boss";
            System.Collections.IEnumerator Fell()
            {
                var b = boss; for (float t = 0; t < 4 && b && b.alive; t += .1f) { b.Damage(1e7f, b.Pos + Vector3.forward); yield return new WaitForSecondsRealtime(.1f); }
                yield return new WaitForSecondsRealtime(3); hero.RefreshGear(); hero.hp = hero.max;   // the test's endless health goes back to the real one
            }
            StartCity(); yield return new WaitForSecondsRealtime(1); StartWilds(); yield return new WaitForSecondsRealtime(1.5f); var z = wildsCtl; z.Clock.t = 30;
            MoveTo(z.HillTop + new Vector3(0, 0, 4)); yield return new WaitForSecondsRealtime(2.5f); Snap("hill_portal"); MoveTo(z.HillTop + new Vector3(0, 0, 2)); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"hill: elite here {z.EliteHere} prompt {ui.CityPromptShown}");
            useDuel.Invoke(z, new object[] { "portal" }); yield return new WaitForSecondsRealtime(2); Snap("sky_elite"); log.AppendLine(Sync()); log.AppendLine($"elite: sky {sky} soul {soul} boss {(boss ? boss.d.name : "none")} hp {(boss ? boss.maxHp : 0):0} hero at {hero.Pos}");
            yield return Watch("elite", 14, false);
            yield return Fell(); Snap("sky_won"); log.AppendLine($"elite won: fighting {z.Fighting} soul {soul} boss {(boss ? "left" : "none")}");
            MoveTo(OpenWorld.SkyExit + new Vector3(0, 0, -2)); yield return new WaitForSecondsRealtime(1); Snap("sky_exit"); log.AppendLine($"exit prompt {ui.CityPromptShown}");
            useDuel.Invoke(z, new object[] { "sky_exit" }); yield return new WaitForSecondsRealtime(2.5f); Snap("hill_merchant"); log.AppendLine($"back: wilds {wilds} sky {sky} soul {soul} at {hero.Pos} · elite here {z.EliteHere} merchant {z.MerchantHere}");
            MoveTo(z.RisePos + new Vector3(0, 0, 3.5f)); yield return new WaitForSecondsRealtime(2); Snap("wanderer"); MoveTo(z.RisePos + new Vector3(0, 0, 2)); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"rise: wanderer {z.WandererHere} prompt {ui.CityPromptShown}");
            int matsWas = save.mats[2]; useDuel.Invoke(z, new object[] { "wanderer" }); yield return new WaitForSecondsRealtime(1.5f); log.AppendLine(Sync()); log.AppendLine($"wanderer: {(boss ? boss.d.name : "none")} hp {(boss ? boss.maxHp : 0):0}");
            yield return Watch("wanderer", 14, true);
            yield return Fell(); log.AppendLine($"wanderer won: rare stones {matsWas} -> {save.mats[2]}"); useDuel.Invoke(z, new object[] { "sky_exit" }); yield return new WaitForSecondsRealtime(1.5f); log.AppendLine($"after: wanderer still here {z.WandererHere}");
            z.Clock.t = WorldClock.DayLen + 5; yield return new WaitForSecondsRealtime(2); log.AppendLine($"night {z.Clock.IsNight} lurk at {z.LurkAt} wanderer {z.WandererHere}");
            MoveTo(z.LurkAt + new Vector3(0, 0, 14)); yield return new WaitForSecondsRealtime(2.5f); Snap("lurk_hint");
            int ownedWas = save.owned.Count; MoveTo(z.LurkAt + new Vector3(0, 0, 4)); yield return new WaitForSecondsRealtime(.4f); Snap("ambush"); yield return new WaitForSecondsRealtime(2.5f); Snap("sky_legend"); log.AppendLine(Sync()); log.AppendLine($"ambush: sky {sky} fighting {z.Fighting} boss {(boss ? boss.d.name : "none")} hp {(boss ? boss.maxHp : 0):0}");
            yield return Watch("legend", 14, true);
            yield return Fell(); log.AppendLine($"legend won: weapons {ownedWas} -> {save.owned.Count} ({(save.owned.Count > ownedWas ? save.owned[save.owned.Count - 1] : "")}) day {save.legendDay} taken {z.LegendTaken}");
            useDuel.Invoke(z, new object[] { "sky_exit" }); yield return new WaitForSecondsRealtime(1.5f);
            {   // the same duel for a level 1 hero: the fight should be about as long
                int xi = save.xpKeys.IndexOf(hero.def.id); int xpWas = xi >= 0 ? save.xpVals[xi] : 0; if (xi >= 0) save.xpVals[xi] = 0; hero.RefreshGear(); hero.hp = hero.max;
                z.EnterDuel(OpenWorld.Elite, z.HillTop); yield return new WaitForSecondsRealtime(1); log.AppendLine("level 1 " + Sync());
                if (xi >= 0) save.xpVals[xi] = xpWas; hero.RefreshGear(); useDuel.Invoke(z, new object[] { "sky_exit" }); BackToZone(z.HillTop); yield return new WaitForSecondsRealtime(1);
            }
            z.EnterDuel(OpenWorld.Wanderer, z.RisePos); yield return new WaitForSecondsRealtime(1.5f); gold = 100; hero.inv = 0; hero.Hurt(1e8f, hero.Pos + Vector3.forward); yield return new WaitForSecondsRealtime(4.5f);
            Snap("fell"); log.AppendLine($"fell in a duel: wilds {wilds} sky {sky} soul {soul} dead {hero.dead} gold {gold} at {hero.Pos} fighting {z.Fighting ?? "none"}");
            {   // the city gate's arena (the phone build's way in): refused while resting, won and walked home, lost and woken at the gate
                StartCity(); yield return new WaitForSecondsRealtime(1.5f); MoveTo(City.Gate + new Vector3(0, 0, 2)); yield return new WaitForSecondsRealtime(.5f); ui.OpenCityPanel("gate"); yield return new WaitForSecondsRealtime(.6f); Snap("gate_arena"); ui.CloseCityPanel();
                StartArena("elite"); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"arena elite while resting: sky {sky} city {city}");
                StartArena("wanderer"); yield return new WaitForSecondsRealtime(1.5f); Snap("arena_wanderer"); log.AppendLine($"arena wanderer: sky {sky} fighting {z.Fighting} from city {z.FromCityDuel} boss {(boss ? boss.d.name : "none")}");
                yield return Fell(); MoveTo(OpenWorld.SkyExit + new Vector3(0, 0, -2)); yield return new WaitForSecondsRealtime(.6f); log.AppendLine($"arena won: exit prompt {ui.CityPromptShown}");
                useDuel.Invoke(z, new object[] { "sky_exit" }); yield return new WaitForSecondsRealtime(1.5f); Snap("arena_home"); log.AppendLine($"home: city {city} sky {sky} at {hero.Pos} (gate {City.Gate})");
                StartArena("wanderer"); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"wanderer again at once: sky {sky}");
                typeof(OpenWorld).GetField("eliteBackAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(z, 0f);
                StartArena("elite"); yield return new WaitForSecondsRealtime(1.5f); gold = 50; hero.inv = 0; hero.Hurt(1e8f, hero.Pos + Vector3.forward); yield return new WaitForSecondsRealtime(4.5f);
                Snap("arena_fell"); log.AppendLine($"arena fell: city {city} sky {sky} dead {hero.dead} gold {gold} at {hero.Pos}");
            }
            foreach (var ln in new[] { 1, 3 }) { StartLane(3, ln); yield return new WaitForSecondsRealtime(1); log.AppendLine($"lane battle {ln} lane(s): stage {System.Array.Find(stageRoots, r => r && r.gameObject.activeSelf)?.name}"); if (ln == 3) Snap("lane3"); }   // picked by name, not by place in the list
            save = JsonUtility.FromJson<Save>(keep); Persist();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "duels.txt"), log.ToString()); yield return new WaitForSecondsRealtime(.5f); Application.Quit();
        }
        // Test mode (-skylanestest <dir>): the Sky Lanes 3v3 map. Views of each part, then the match runs on its own for a while (the hero
        // cannot fall and stays home): towers, soldiers and heroes per side, how far the waves get, and anything standing off the ground.
        // Test mode (-skytimes <dir>): Sky Lanes by day, at dusk and by night, the same views each time (play, our base, the jungle, the curve)
        // Test mode (-kittest <dir>): every hero with a MOBA kit in Sky Lanes: each skill aimed (picture) and cast at a sturdy enemy hero standing still,
        // damage written down against the kit's numbers (P = attack power)
        System.Collections.IEnumerator KitTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); var log = new System.Text.StringBuilder();
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            foreach (var hd in Data.Heroes.Where(h => Kits.For(h.id) != null))
            {
                save.hero = hd.id; hero.Equip(); StartLane(3, 2); yield return new WaitForSecondsRealtime(1.2f);
                foreach (var f in foes.ToArray()) if (f && f.d.kind is "champ" or "minion") { f.alive = false; Destroy(f.gameObject); }
                foes.RemoveAll(f => !f || !f.alive); MoveTo(new Vector3(0, 0, 8)); hero.max = hero.hp = 999999; hero.mp = 999;
                var dummy = SpawnUnit(Lane.Champ(Data.Heroes.First(h => h.id == "spartan")), new Vector3(0, 0, 4), 1); dummy.maxHp = dummy.hp = 1e6f; dummy.d.dmg = 0; dummy.d.spd = 0; yield return new WaitForSecondsRealtime(1.2f);   // past its spawning
                var kit = Kits.For(hd.id); var line = new System.Text.StringBuilder($"{hd.id,-10} P {hero.AtkPow:0} passive {kit.pid}:");
                for (int i = 0; i < 3; i++)
                {
                    var s = kit.s[i]; hero.transform.position = new Vector3(0, hero.Pos.y, 8); hero.face = Mathf.PI; dummy.transform.position = new Vector3(0, dummy.Pos.y, 4); dummy.stag = 0; dummy.slow = 0;
                    var at = s.shape == KShape.Self ? hero.Pos : dummy.Pos;
                    if (s.shape is KShape.Self or KShape.Ally) { hero.transform.position = new Vector3(0, hero.Pos.y, 4 + Mathf.Min(1.5f, s.size * .5f)); at = hero.Pos; }
                    if (s.shape is KShape.Target or KShape.Blink && s.range < 4) hero.transform.position = new Vector3(0, hero.Pos.y, 5.5f);
                    if (s.shape == KShape.Cone && s.range < 5) hero.transform.position = new Vector3(0, hero.Pos.y, 6);
                    for (int k = 0; k < 20; k++) { hero.KitTestAim(i, at); yield return null; }
                    if (i == 0 || hd.id is "warrior" or "elf" or "mage") { Snap($"{hd.id}_aim{i}"); hero.KitTestAim(i, at); yield return null; }   // the picture is taken at the end of the frame: keep the aim up until then
                    log.AppendLine($"{hd.id} aim{i} {s.shape}: {hero.AimDebug}");
                    float before = dummy.hp; float hpBefore = hero.hp; hero.KitTestCast(i, at);
                    { float w0 = s.shape is KShape.Point or KShape.Leap or KShape.Dash ? s.delay + (s.shape == KShape.Leap ? .5f : s.shape == KShape.Dash ? .35f : .05f) + .25f : .3f; yield return new WaitForSecondsRealtime(w0); Snap($"{hd.id}_cast{i}"); }   // the skill's own effect
                    float maxStag = 0, maxSlow = 0;
                    for (float w = 0; w < Mathf.Max(1.6f, s.dur + s.delay + s.tether + s.dotT + .8f); w += Time.unscaledDeltaTime) { maxStag = Mathf.Max(maxStag, dummy.stag); maxSlow = Mathf.Max(maxSlow, dummy.slow); yield return null; }
                    if (s.trap > 0) { dummy.transform.position = at; yield return new WaitForSecondsRealtime(.6f); }
                    float dealt = before - dummy.hp;
                    line.Append($" | {"QWE"[i]} {s.shape} {dealt / hero.AtkPow:0.00}P (table {s.dmg * Mathf.Max(1, s.hits) + s.dot:0.00}P) stun {maxStag:0.0} slow {maxSlow:0.0}");
                    dummy.hp = dummy.maxHp;
                }
                log.AppendLine(line.ToString());
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "kittest.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-aikittest <dir>): each AI hero alone against the player in Sky Lanes for 12 s: how many skills it used, the damage it did,
        // whether it stunned or slowed, with a picture while its warning is on the ground
        System.Collections.IEnumerator AiKitTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); var log = new System.Text.StringBuilder();
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            save.hero = "spartan"; hero.Equip();
            foreach (var hd in Data.Heroes.Where(h => Kits.For(h.id) != null))
            {
                StartLane(3, 2); yield return new WaitForSecondsRealtime(1);
                foreach (var f in foes.ToArray()) if (f && f.d.kind is "champ" or "minion") { f.alive = false; Destroy(f.gameObject); }
                foes.RemoveAll(f => !f || !f.alive); minionT = 999; MoveTo(new Vector3(0, 0, 1)); hero.max = hero.hp = 1e6f;   // mid-map, out of every tower's reach
                var ai = SpawnUnit(Lane.Champ(hd), new Vector3(0, 0, -4), 1); ai.maxHp = ai.hp = 1e6f;
                var mate = SpawnUnit(Lane.Champ(Data.Heroes.First(h => h.id == "spartan")), new Vector3(1.5f, 0, -5), 1); mate.d.dmg = 0; mate.d.spd = 0; mate.d.model = "dummy"; mate.maxHp = 10000; mate.hp = 5000;   // a hurt teammate beside it, with no kit of its own
                yield return new WaitForSecondsRealtime(1);
                float hpBefore = hero.hp, stunned = 0, slowed = 0; bool shot = false; var hits = new Dictionary<string, float>();
                Foe.OnDamage = (f, dmg, from, o) => { if (f != mate) return; string k = $"{(o.byFoe ? "foe" : "hero")}{(o.skill ? "-skill" : "")} from {Flat(from - f.Pos).magnitude:0.0}m"; hits[k] = hits.GetValueOrDefault(k) + dmg; };
                for (float t = 0; t < 12; t += Time.unscaledDeltaTime)
                {
                    if (hero.stunT > 0) stunned += Time.unscaledDeltaTime; if (hero.slowT > 0) slowed += Time.unscaledDeltaTime;
                    hero.transform.position = new Vector3(Mathf.Sin(t * .7f) * 1.5f, hero.Pos.y, 1);   // the hero stands about, the AI comes to it
                    if (!shot && ai.AiCasts > 0 && t > 2) { shot = true; Snap("ai_" + hd.id); }
                    yield return null;
                }
                log.AppendLine($"{hd.id,-10} kit {Kits.For(hd.id).pid,-7} casts {ai.AiCasts} damage to hero {hpBefore - hero.hp:0} stunned {stunned:0.0}s slowed {slowed:0.0}s · hurt teammate 5000 -> {(mate ? mate.hp : 0):0} · skills {ai.AiLog} · mate hit by {string.Join(", ", hits.Select(kv => $"{kv.Key} {kv.Value:0}"))}"); Foe.OnDamage = null;
                if (mate) { mate.alive = false; Destroy(mate.gameObject); }
                if (ai) { ai.alive = false; Destroy(ai.gameObject); }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "aikittest.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-kitlobby <dir>): the hero details panel with the lane-skill box, for a few heroes
        System.Collections.IEnumerator KitLobby(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            foreach (var h in new[] { "knight", "flamepriest", "tiger", "warrior" })
            { save.hero = h; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); yield return new WaitForSecondsRealtime(1); ui.TestSkills(); yield return new WaitForSecondsRealtime(.6f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"kitlobby_{h}.png")); yield return new WaitForSecondsRealtime(.3f); }
            Application.Quit();
        }
        // Test mode (-duelbalance <dir> [rounds]): every pair of AI heroes with kits fights one on one in the middle of Sky Lanes (sides swapped
        // each round), time sped up; win rates per hero and per class go to duelbalance.txt
        System.Collections.IEnumerator DuelBalance(string dir, int rounds, int maxDuels = 0)
        {
            System.IO.Directory.CreateDirectory(dir); System.IO.File.Delete(System.IO.Path.Combine(dir, "progress.txt")); yield return new WaitForSecondsRealtime(3); var log = new System.Text.StringBuilder();
            save.hero = "warrior"; hero.Equip(); StartLane(3, 2); yield return new WaitForSecondsRealtime(1);
            var ids = Data.Heroes.Where(h => Kits.For(h.id) != null).ToList(); var wins = ids.ToDictionary(h => h.id, _ => 0f); var games = ids.ToDictionary(h => h.id, _ => 0);
            var vs = new Dictionary<string, float>(); float totalT = 0; int n = 0, draws = 0;
            TestSpeed = 8; duelOnly = true;
            for (int a = 0; a < ids.Count; a++) for (int b = a + 1; b < ids.Count; b++) for (int r = 0; r < rounds; r++)
            {
                foreach (var f in foes.ToArray()) if (f && f.d.kind is "champ" or "minion") { f.alive = false; Destroy(f.gameObject); }
                foes.RemoveAll(f => !f || !f.alive); minionT = 999; hero.transform.position = Lane.SpawnOf(0); hero.max = hero.hp = 1e7f; hero.inv = 999;
                var (h0, h1) = r % 2 == 0 ? (ids[a], ids[b]) : (ids[b], ids[a]);
                var f0 = SpawnUnit(Lane.Champ(h0), new Vector3(0, 0, 4), 0); var f1 = SpawnUnit(Lane.Champ(h1), new Vector3(0, 0, -4), 1);
                float t = 0; while (t < 60 && f0 && f0.alive && f1 && f1.alive) { t += Time.deltaTime; yield return null; }
                bool w0 = f0 && f0.alive && !(f1 && f1.alive), w1 = f1 && f1.alive && !(f0 && f0.alive);
                games[h0.id]++; games[h1.id]++; n++; totalT += t;
                System.IO.File.AppendAllText(System.IO.Path.Combine(dir, "progress.txt"), $"{n} {h0.id} v {h1.id}: {(w0 ? h0.id : w1 ? h1.id : "draw")} in {t:0.0}s · hp {(f0 ? f0.hp : 0):0}/{(f0 ? f0.maxHp : 0):0} v {(f1 ? f1.hp : 0):0}/{(f1 ? f1.maxHp : 0):0} · dist {(f0 && f1 ? Flat(f0.Pos - f1.Pos).magnitude : -1):0.0} · casts {(f0 ? f0.AiCasts : 0)}/{(f1 ? f1.AiCasts : 0)}" + System.Environment.NewLine);
                if (maxDuels > 0 && n >= maxDuels) goto done;
                if (w0) { wins[h0.id]++; vs[$"{h0.id}>{h1.id}"] = vs.GetValueOrDefault($"{h0.id}>{h1.id}") + 1; }
                else if (w1) { wins[h1.id]++; vs[$"{h1.id}>{h0.id}"] = vs.GetValueOrDefault($"{h1.id}>{h0.id}") + 1; }
                else { draws++; wins[h0.id] += .5f; wins[h1.id] += .5f; }
            }
            done:
            TestSpeed = 1; duelOnly = false;
            log.AppendLine($"duels {n} · draws (60 s) {draws} · average length {totalT / Mathf.Max(1, n):0.0} s (game time)");
            foreach (var h in ids.OrderByDescending(h => wins[h.id] / games[h.id])) log.AppendLine($"{h.id,-10} {h.cls,-9} win {wins[h.id] / games[h.id] * 100:0}%  ({wins[h.id]:0.#}/{games[h.id]})");
            log.AppendLine("by class:");
            foreach (var g in ids.GroupBy(h => h.cls)) log.AppendLine($"  {g.Key,-9} {g.Sum(h => wins[h.id]) / g.Sum(h => games[h.id]) * 100:0}%");
            log.AppendLine("pairs (wins):"); foreach (var kv in vs.OrderBy(k => k.Key)) log.AppendLine($"  {kv.Key} {kv.Value:0}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "duelbalance.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-battlesettings <dir>): the settings panel, then each AI difficulty: one AI hero (Valka) against the player for 12 s,
        // damage taken and skills used; the save's settings are put back
        // -storylooks <dir>: the lobby with each of the story banner's three looks (the owner picks one)
        System.Collections.IEnumerator StoryLooks(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); ui.ShowLobby(); yield return new WaitForSecondsRealtime(1);
            for (int k = 0; k < 3; k++) { ui.StoryLook(k); yield return new WaitForSecondsRealtime(1.2f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"look{k + 1}.png")); yield return new WaitForSecondsRealtime(1.2f); }
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "end.png")); yield return new WaitForSecondsRealtime(1); Application.Quit();
        }
        System.Collections.IEnumerator BattleSettingsTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); var log = new System.Text.StringBuilder(); int keepM = save.laneMode, keepA = save.aiLevel;
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            ui.ShowLobby(); yield return new WaitForSecondsRealtime(1); Snap("lobby_battle"); yield return new WaitForSecondsRealtime(.6f); Snap("lobby_only"); yield return new WaitForSecondsRealtime(.6f); ui.ShowBattleSettings(); yield return new WaitForSecondsRealtime(.6f); Snap("battle_settings");
            save.hero = "spartan"; hero.Equip();
            for (int lv = 0; lv < 3; lv++)
            {
                save.aiLevel = lv; save.laneMode = 3; StartBattle(); yield return new WaitForSecondsRealtime(1);
                foreach (var f in foes.ToArray()) if (f && f.d.kind is "champ" or "minion") { f.alive = false; Destroy(f.gameObject); }
                foes.RemoveAll(f => !f || !f.alive); minionT = 999; MoveTo(new Vector3(0, 0, 1)); hero.max = hero.hp = 1e6f;
                var ai = SpawnUnit(Lane.Champ(Data.Heroes.First(h => h.id == "warrior")), new Vector3(0, 0, -4), 1); float aiHp = ai.maxHp, aiDmg = ai.d.dmg; ai.maxHp = ai.hp = 1e6f; yield return new WaitForSecondsRealtime(1);
                float before = hero.hp; for (float t = 0; t < 12; t += Time.unscaledDeltaTime) { hero.transform.position = new Vector3(Mathf.Sin(t * .7f) * 1.5f, hero.Pos.y, 1); yield return null; }
                log.AppendLine($"AI {lv}: Valka hp {aiHp:0} basic dmg {aiDmg:0} · in 12 s: damage to hero {before - hero.hp:0}, skills {ai.AiCasts}");
                if (ai) { ai.alive = false; Destroy(ai.gameObject); }
            }
            save.laneMode = keepM; save.aiLevel = keepA; Persist();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "battlesettings.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-teambalance <dir> [fights]): random three-a-side teams of AI heroes (no hero twice in a fight) fight in the middle of
        // Sky Lanes until one side is down (90 s at most), time sped up; win rate of each hero and class as a team member (teambalance.txt)
        System.Collections.IEnumerator TeamBalance(string dir, int fights)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); var log = new System.Text.StringBuilder(); var prog = System.IO.Path.Combine(dir, "teamprogress.txt"); System.IO.File.Delete(prog);
            save.hero = "warrior"; hero.Equip(); StartLane(3, 2); yield return new WaitForSecondsRealtime(1);
            var ids = Data.Heroes.Where(h => Kits.For(h.id) != null).ToList(); var wins = ids.ToDictionary(h => h.id, _ => 0f); var games = ids.ToDictionary(h => h.id, _ => 0); int draws = 0; float totalT = 0;
            TestSpeed = 8; duelOnly = true; var rng = new System.Random(11);
            for (int n = 0; n < fights; n++)
            {
                foreach (var f in foes.ToArray()) if (f && f.d.kind is "champ" or "minion") { f.alive = false; Destroy(f.gameObject); }
                foes.RemoveAll(f => !f || !f.alive); minionT = 999; hero.transform.position = Lane.SpawnOf(0); hero.max = hero.hp = 1e7f; hero.inv = 999;
                var pick = ids.OrderBy(_ => rng.Next()).Take(6).ToList(); var teams = new[] { pick.Take(3).ToList(), pick.Skip(3).ToList() };
                var units = new List<Foe>[2] { new(), new() };
                for (int t = 0; t < 2; t++) for (int i = 0; i < 3; i++) units[t].Add(SpawnUnit(Lane.Champ(teams[t][i]), new Vector3((i - 1) * 2.5f, 0, t == 0 ? 5 : -5), t));
                float time = 0; bool Alive(int t) => units[t].Any(u => u && u.alive);
                while (time < 90 && Alive(0) && Alive(1)) { time += Time.deltaTime; yield return null; }
                int win = Alive(0) && !Alive(1) ? 0 : Alive(1) && !Alive(0) ? 1 : -1; totalT += time;
                for (int t = 0; t < 2; t++) foreach (var h in teams[t]) { games[h.id]++; wins[h.id] += win == t ? 1 : win < 0 ? .5f : 0; }
                if (win < 0) draws++;
                System.IO.File.AppendAllText(prog, $"{n + 1} [{string.Join(",", teams[0].Select(h => h.id))}] v [{string.Join(",", teams[1].Select(h => h.id))}]: {(win < 0 ? "draw" : win == 0 ? "first" : "second")} in {time:0}s" + System.Environment.NewLine);
            }
            TestSpeed = 1; duelOnly = false;
            log.AppendLine($"fights {fights} · draws (90 s) {draws} · average {totalT / Mathf.Max(1, fights):0.0} s");
            foreach (var h in ids.OrderByDescending(h => wins[h.id] / Mathf.Max(1, games[h.id]))) log.AppendLine($"{h.id,-10} {h.cls,-9} win {wins[h.id] / Mathf.Max(1, games[h.id]) * 100:0}%  ({wins[h.id]:0.#}/{games[h.id]})");
            log.AppendLine("by class:"); foreach (var g in ids.GroupBy(h => h.cls)) log.AppendLine($"  {g.Key,-9} {g.Sum(h => wins[h.id]) / Mathf.Max(1, g.Sum(h => games[h.id])) * 100:0}%");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "teambalance.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-skintest <dir>): Garland's skins tab (the classic look as a model skin), wearing it in the lobby, on the VS screen and in a match
        System.Collections.IEnumerator SkinTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            int keepSkin = Skin("knight"); string keepHero = save.hero;
            save.hero = "knight"; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.SetTab("skin"); yield return new WaitForSecondsRealtime(1.2f); Snap("skin_tab_new");
            var kd = Data.Heroes.First(h => h.id == "knight"); int classic = kd.skins.Length;
            log.AppendLine($"knight skins: {Data.SkinCount(kd)} ({string.Join(", ", Enumerable.Range(0, Data.SkinCount(kd)).Select(i => Data.SkinName(kd, i) + (Data.IsModelSkin(kd, i) ? " [model " + Data.ModelOf(kd, i) + "]" : "")))}) · classic free {Shop.Owns(ItemKind.HeroSkin, $"knight:{classic}")}");
            SetSkin("knight", classic); hero.Equip(); hero.PlaceLobby(); ui.SetTab("skin"); yield return new WaitForSecondsRealtime(1.2f); Snap("skin_tab_classic");
            log.AppendLine($"wearing: {hero.modelKey} · card {(Studio.HeroFull("knight_classic") ? "yes" : "no")}");
            save.laneMode = 3; ui.ShowHeroPick(); yield return new WaitForSecondsRealtime(.5f); ui.TestPick("knight", "warrior"); yield return new WaitForSecondsRealtime(.6f); Snap("skin_pick");
            ui.TestConfirm(); yield return new WaitForSecondsRealtime(4.3f); Snap("skin_vs"); yield return new WaitForSecondsRealtime(2.5f); Snap("skin_match"); log.AppendLine($"in match: {hero.modelKey}");
            SetSkin("knight", keepSkin); save.hero = keepHero; Persist();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skin.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-brutetest <dir>): Sky Lanes, every enemy outer tower knocked down: the next wave brings a War Brute down each lane; the dash length
        System.Collections.IEnumerator BruteTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            save.hero = "knight"; hero.Equip(); StartLane(3, 2); yield return new WaitForSecondsRealtime(2);
            {   // the jungle stream: wading in it, and its look from above
                var sp = new Vector3(9, 0, 0); log.AppendLine($"stream: in water at (9,0) {River.InWater(9, 0)} · at (9,3) {River.InWater(9, 3)} · Guardian's pool (14.4,0) {River.InWater(14.4f, 0)} · lane (0,20) {River.InWater(0, 20)}");
                MoveTo(sp); yield return new WaitForSecondsRealtime(1.2f); Snap("stream");
                foreach (var r in Resources.FindObjectsOfTypeAll<MeshRenderer>()) if (r.name.Contains("JungleStream")) log.AppendLine($"stream renderer: active {r.gameObject.activeInHierarchy} enabled {r.enabled} bounds {r.bounds.center} {r.bounds.size} mat {(r.sharedMaterial ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name + " q" + r.sharedMaterial.renderQueue : "none")} mesh {(r.GetComponent<MeshFilter>()?.sharedMesh ? r.GetComponent<MeshFilter>().sharedMesh.vertexCount : -1)} · hero y {hero.Pos.y:0.00}");
            }
            hero.inv = 999; var p0 = hero.Pos; hero.TestDodge(new Vector2(1, 0)); yield return new WaitForSecondsRealtime(.6f); log.AppendLine($"dash: {Flat(hero.Pos - p0).magnitude:0.00} m");
            {   // a skill held and dragged far past its ring, then let go: called off, no cast, no cooldown
                hero.mp = hero.mpMax; hero.cds[0] = 0; ui.aimDown[0] = true; yield return null; yield return null; ui.aimDrag = new Vector2(2.2f, 0); yield return new WaitForSecondsRealtime(.3f); Snap("aim_cancel_red");
                ui.aimCancel = true; ui.aimUp[0] = true; yield return new WaitForSecondsRealtime(.3f); ui.aimDrag = Vector2.zero;
                log.AppendLine($"drag-out cancel: cooldown after release {hero.cds[0]:0.0} (0 = not cast)");
            }
            {   // an AI hero grows with its side's level: Lv 5 = health x1.16, damage x1.12
                var mate = foes.FirstOrDefault(f => f && f.alive && f.team == 0 && f.d.kind == "champ");
                if (mate) { float h0 = mate.maxHp, d0 = mate.d.dmg; TeamXp(0, Data.XpFor(5) - teamXp[0]); log.AppendLine($"AI grows: {mate.d.name} Lv{mate.aiLv} hp x{mate.maxHp / h0:0.000} dmg x{mate.d.dmg / d0:0.000}"); }
                var foe = foes.FirstOrDefault(f => f && f.alive && f.team == 1 && f.d.kind == "champ"); if (foe) log.AppendLine($"enemy side stays: {foe.d.name} Lv{foe.aiLv}");
            }
            MoveTo(new Vector3(SkyLanes.BaseOf(1).x, 0, SkyLanes.BaseOf(1).y) + new Vector3(0, 0, 12)); yield return new WaitForSecondsRealtime(1.2f); Snap("red_base_paths");
            foreach (var f in foes.ToArray()) if (f && f.alive && f.team == 1 && f.towerTier == 0) f.Damage(1e8f, f.Pos);
            yield return new WaitForSecondsRealtime(.5f); minionT = 0; yield return new WaitForSecondsRealtime(.8f); Snap("brute_banner");
            var br = foes.Where(f => f && f.alive && f.d.id == "brute").ToList(); log.AppendLine($"brutes after the outer towers fell: ours {br.Count(f => f.team == 0)} theirs {br.Count(f => f.team == 1)} · lanes {string.Join(",", br.Select(f => f.laneIdx))}");
            if (br.Count > 0) { MoveTo(br[0].Pos + new Vector3(3, 0, 2)); yield return new WaitForSecondsRealtime(4); Snap("brute_march"); log.AppendLine($"brute walked to {br[0].Pos} hp {br[0].hp:0}"); }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "brute.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-scrolltest <dir>): screens with long lists, to see the themed scroll bars
        System.Collections.IEnumerator ScrollTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            ui.ShowLobby(); yield return new WaitForSecondsRealtime(1);
            ui.ShowHeroPick(); yield return new WaitForSecondsRealtime(1); Snap("scroll_pick");
            ui.ShowLobby(); ui.SetTab("hero"); yield return new WaitForSecondsRealtime(1); Snap("scroll_cards");
            ui.ShowQuests(true); yield return new WaitForSecondsRealtime(1); Snap("scroll_quests"); ui.ShowQuests(false);
            ui.ShowBag(true); yield return new WaitForSecondsRealtime(1); Snap("scroll_bag");
            yield return new WaitForSecondsRealtime(.5f); Application.Quit();
        }
        // Test mode (-matchprep <dir> [tag]): the hero pick screen, a class tab, the VS screen, and that the match starts with the VS teams
        System.Collections.IEnumerator MatchPrepTest(string dir, string tag)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); var log = new System.Text.StringBuilder(); int keepM = save.laneMode;
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{tag}_{n}.png"));
            save.laneMode = 3; ui.ShowLobby(); yield return new WaitForSecondsRealtime(1);
            ui.ShowHeroPick(); yield return new WaitForSecondsRealtime(.8f); Snap("pick");
            ui.TestPick("knight", "warrior"); yield return new WaitForSecondsRealtime(.8f); Snap("pick_warrior"); ui.TestPickScrolled(); yield return new WaitForSecondsRealtime(1.2f); log.AppendLine("pick after scroll: " + ui.PickScrollInfo); Snap("pick_scrolled"); yield return new WaitForSecondsRealtime(.8f);
            ui.TestConfirm(); yield return new WaitForSecondsRealtime(.3f); Snap("vs_start"); yield return new WaitForSecondsRealtime(4.3f); Snap("vs_full");   // the bar is full at 4.4 s
            var allies = nextAllies?.Select(h => h.id).ToList() ?? new List<string>(); var enemies = nextEnemies?.Select(h => h.id).ToList() ?? new List<string>();
            yield return new WaitForSecondsRealtime(2.2f);   // the match starts at 5.5 s
            var champs = foes.Where(f => f && f.d.kind == "champ").ToList();
            log.AppendLine($"hero {save.hero} · VS ours [{string.Join(",", allies)}] theirs [{string.Join(",", enemies)}] · in match ours [{string.Join(",", champs.Where(f => f.team == 0).Select(f => f.d.model))}] theirs [{string.Join(",", champs.Where(f => f.team == 1).Select(f => f.d.model))}] · mode {mode} lane {lane}");
            Snap("match");
            save.laneMode = keepM; Persist();
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, $"matchprep_{tag}.txt"), log.ToString()); Application.Quit();
        }
        System.Collections.IEnumerator SkyTimes(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); save.hero = "warrior"; hero.Equip();
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            StartLane(3, 2); hero.max = hero.hp = 999999; var log = new System.Text.StringBuilder();
            { var eg = Stage("Stage_skylanes")?.Find("SkyLanesRoadEdge"); var er = eg ? eg.GetComponent<Renderer>() : null; log.AppendLine(er ? $"edge: on {er.enabled} active {eg.gameObject.activeInHierarchy} verts {eg.GetComponent<MeshFilter>().sharedMesh?.vertexCount} bounds {er.bounds} mat {er.sharedMaterial?.name} clip {er.sharedMaterial?.IsKeywordEnabled("_ALPHATEST_ON")} tex {er.sharedMaterial?.mainTexture?.name}" : "edge: none"); }
            { var bz = Stage("Stage_skylanes")?.Find("Braziers"); if (bz) foreach (var ps in bz.GetComponentsInChildren<ParticleSystem>(true)) log.AppendLine($"fx {ps.name} active {ps.gameObject.activeInHierarchy} playing {ps.isPlaying} n {ps.particleCount} at {ps.transform.position} scale {ps.transform.lossyScale.x:0.00} mat {(ps.GetComponent<ParticleSystemRenderer>()?.sharedMaterial ? ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.shader.name : "none")}"); }
            foreach (var (t, n) in new[] { (0f, "0_day"), (120f, "1_sunset"), (160f, "2_dusk"), (270f, "3_nightfall"), (310f, "4_night") })
            {   // the match clock moved on: the sky follows it (Tick runs in UpdLane)
                time = t; yield return new WaitForSecondsRealtime(2.5f); log.AppendLine($"t {t}s phase {SkyTime.Phase:0.00}"); Snap(n + "_play"); yield return new WaitForSecondsRealtime(.2f);
                ZoomBy(1.2f); ui.Pan = new Vector3(17, 0, 2); yield return new WaitForSecondsRealtime(1.2f); Snap(n + "_jungle"); ui.Pan = new Vector3(0, 0, 44); yield return new WaitForSecondsRealtime(1.2f); Snap(n + "_base"); ui.Pan = null; ZoomBy(-1.2f);
                if (t == 0) { ZoomBy(-.5f); ui.Pan = new Vector3(3, 0, 22); yield return new WaitForSecondsRealtime(1.2f); Snap("road_close"); ui.Pan = null; ZoomBy(.5f); } yield return new WaitForSecondsRealtime(.3f);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skytimes.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-matchlen <dir> [rounds]): whole Sky Lanes matches with AI on both sides (the player stands aside, out of the fight), time sped up;
        // how long each lasts in game minutes, who won, towers left. To tune the match length (owner: about 10 minutes).
        List<HeroDef> pairA, pairB;
        System.Collections.IEnumerator MatchLenTest(string dir, int rounds)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); const float Speed = 6, Cap = 30 * 60;
            for (int r = 0; r < rounds; r++)
            {
                // rounds in pairs: the same six heroes, sides swapped in the second round (to tell a side's edge from the heroes' luck)
                if (r % 2 == 0) { var pool = Data.Heroes.Where(h => h.rig && h.id != hero.def.id).OrderBy(_ => Random.value).Take(6).ToList(); pairA = pool.Take(3).ToList(); pairB = pool.Skip(3).ToList(); }
                var ours = r % 2 == 0 ? pairA : pairB; var theirs = r % 2 == 0 ? pairB : pairA; nextAllies = ours.Take(2).ToList(); nextEnemies = theirs.ToList();
                StartLane(3, 2); yield return null;
                var mate = ours[2]; var u = SpawnUnit(Lane.Champ(mate), Lane.SpawnOf(0), 0); if (u) u.laneIdx = 0;
                log.AppendLine($"round {r + 1} blue: {string.Join(",", ours.Select(h => h.name))} · red: {string.Join(",", theirs.Select(h => h.name))}");   // the player's place goes to an AI
                hero.dead = true; hero.max = hero.hp = 1e9f; hero.inv = 1e9f; hero.transform.position = Lane.SpawnOf(0) + new Vector3(0, -40, 0); foreach (var rr in hero.GetComponentsInChildren<Renderer>()) rr.enabled = false;
                TestSpeed = Speed; float t0 = time; int lastMin = -1;
                float real0 = Time.realtimeSinceStartup; while (mode == Mode.Game && time - t0 < Cap && Time.realtimeSinceStartup - real0 < Cap / Speed + 60) { if ((int)((time - t0) / 60) != lastMin || Time.frameCount % 600 == 0) { lastMin = (int)((time - t0) / 60); System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "matchlen.txt"), log.ToString() + $"now: game {time - t0:0} s real {Time.realtimeSinceStartup - real0:0} s mode {mode} scale {Time.timeScale} paused {paused}"); log.AppendLine($"  round {r + 1} min {lastMin}: towers ours {TowersLeft(0)} theirs {TowersLeft(1)} · kills {teamKills[0]}:{teamKills[1]} · Lv {Data.Level(teamXp[0])}:{Data.Level(teamXp[1])}"); } yield return null; }
                log.AppendLine($"jungle round {r + 1}: {Jungle.Events.Count} camps cleared · " + string.Join(" | ", Jungle.Events));
                TestSpeed = 1; float m = (time - t0) / 60; log.AppendLine($"round {r + 1}: {m:0.0} min · winner {(TowersLeft(1) == 0 || !foes.Any(f => f && f.alive && f.team == 1 && f.d.id == "core") ? "blue" : TowersLeft(0) == 0 || !foes.Any(f => f && f.alive && f.team == 0 && f.d.id == "core") ? "red" : "none (cap)")} · towers ours {TowersLeft(0)} theirs {TowersLeft(1)} · kills {teamKills[0]}:{teamKills[1]}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "matchlen.txt"), log.ToString()); hero.dead = false; foreach (var rr in hero.GetComponentsInChildren<Renderer>()) rr.enabled = true; yield return new WaitForSecondsRealtime(2.5f);
            }
            Application.Quit();
        }
        // -endtest <dir>: recall from mid-lane, the warp list and a warp to a tower, the perk button, then the end screen's two pages
        System.Collections.IEnumerator EndTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            { var ea = System.Environment.GetCommandLineArgs(); int eh = System.Array.IndexOf(ea, "-endhero"); if (eh >= 0 && eh + 1 < ea.Length) { save.hero = ea[eh + 1]; hero.Equip(); } }   // tests: the end screen for another hero's order
            StartLane(3, 2); yield return new WaitForSecondsRealtime(2);
            MoveTo(new Vector3(0, 0, 4)); yield return new WaitForSecondsRealtime(.5f); bool ok = hero.StartRecall(); yield return new WaitForSecondsRealtime(3.5f); Snap("recall"); yield return new WaitForSecondsRealtime(3.2f);
            log.AppendLine($"recall started {ok}: now {hero.Pos} · from the fountain {Flat(hero.Pos - Lane.HealAt(0)).magnitude:0.0} m · in base {hero.InBase}");
            ui.TestWarpPick(); yield return new WaitForSecondsRealtime(.8f); Snap("warp_pick"); yield return new WaitForSecondsRealtime(.6f); ui.TestWarpPick();
            var tw = hero.WarpTowers.FirstOrDefault(t => t.towerTier == 0); ok = hero.StartWarp(tw); yield return new WaitForSecondsRealtime(2); Snap("warping"); yield return new WaitForSecondsRealtime(2.6f);
            log.AppendLine($"warp started {ok} to {(tw ? tw.Pos.ToString() : "-")}: now {hero.Pos} · from the tower {(tw ? Flat(hero.Pos - tw.Pos).magnitude : -1):0.0} m · cooldown {hero.warpCd:0}");
            AddXp(Data.XpFor(3) - matchXp); yield return new WaitForSecondsRealtime(1.2f); Snap("perk_button"); yield return new WaitForSecondsRealtime(.6f); ui.ShowPerks(new[] { "ward", "might", "vigor" }, 3); ui.TestOpenPerks(); yield return new WaitForSecondsRealtime(.9f); ui.TestHoverPerk(1); yield return new WaitForSecondsRealtime(.3f); Snap("perk_cards"); yield return new WaitForSecondsRealtime(.6f); ui.TestReroll(); yield return new WaitForSecondsRealtime(.9f); Snap("perk_reroll"); yield return new WaitForSecondsRealtime(.5f); ui.TestChoosePerk(1); yield return new WaitForSecondsRealtime(.25f); Snap("perk_choose"); yield return new WaitForSecondsRealtime(.8f); log.AppendLine($"perks after choosing: {string.Join(",", Perks.Taken(Perks.Me).Select(x => x.id))} · cards open {ui.PerksOpen}");
            var me = Stat(hero.def.name, 0, true); me.k = 6; me.d = 1; me.a = 4; me.dmg = 18400; me.towers = 2; me.camps = 4; me.bosses = 1; myFirstBlood = true; bestMulti = 2; bestSpree = 4; gold = 860;
            foreach (var s in stats.Where(x => !x.me)) { s.k = Random.Range(0, 6); s.d = Random.Range(1, 6); s.a = Random.Range(0, 8); s.dmg = Random.Range(6000, 16000); }
            me.taken = 7200; me.heal = 3100; chron.Clear(); chron.Add((135, "firstblood", 0, "")); chron.Add((272, "double", 0, "")); chron.Add((440, "tower", 0, "")); chron.Add((520, "guardian", 0, "")); time = 579; sideTowers[0] = 7; sideTowers[1] = 2; sideCamps[0] = 9; sideCamps[1] = 5; sideBosses[0] = 2; sideBosses[1] = 1; teamKills[0] = 14; teamKills[1] = 9;
            Finish(true); yield return new WaitForSecondsRealtime(.5f); Snap("end_intro"); yield return new WaitForSecondsRealtime(2.2f); Snap("end_page1"); yield return new WaitForSecondsRealtime(.8f); ui.TestEndPage2(true); yield return new WaitForSecondsRealtime(1); Snap("end_page2"); yield return new WaitForSecondsRealtime(.8f);
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "end.txt"), log.ToString()); Application.Quit();
        }
        // -perktest <dir>: the level 3 pick panel (shot), a pick and what it changes, AI picks at level 9 and after a respawn
        System.Collections.IEnumerator PerkTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            StartLane(3, 2); yield return new WaitForSecondsRealtime(2);
            AddXp(Data.XpFor(3) - matchXp); yield return new WaitForSecondsRealtime(1); Snap("perk_button"); yield return new WaitForSecondsRealtime(.8f);
            log.AppendLine($"level {hero.Level}: pick due {ui.PerkPending}, cards open {ui.PerksOpen}"); ui.TestOpenPerks(); yield return new WaitForSecondsRealtime(.8f); Snap("perk_panel"); yield return new WaitForSecondsRealtime(.8f); log.AppendLine($"cards open {ui.PerksOpen}");
            float a0 = hero.AtkPow, m0 = hero.max; ui.PickPerk(0); yield return new WaitForSecondsRealtime(.5f);
            log.AppendLine($"picked: {string.Join(",", Perks.Taken(Perks.Me).Select(p => p.id))} · attack {a0:0} -> {hero.AtkPow:0} · max hp {m0:0} -> {hero.max:0} · panel open {ui.PerksOpen}");
            AddXp(Data.XpFor(6) - matchXp); yield return new WaitForSecondsRealtime(1); log.AppendLine($"level {hero.Level}: pick due {ui.PerkPending}"); yield return new WaitForSecondsRealtime(32);
            log.AppendLine($"after waiting: auto-picked {string.Join(",", Perks.Taken(Perks.Me).Select(p => p.id))} · panel open {ui.PerksOpen}");
            TeamXp(1, Data.XpFor(9)); yield return null;
            foreach (var f in foes.Where(f => f && f.alive && f.d.kind == "champ")) log.AppendLine($"AI {f.d.name} T{f.team} lv {f.aiLv}: {string.Join(",", Perks.Taken(Perks.Key(f.team, f.d.name)).Select(p => p.id))} hp {f.maxHp:0}");
            var victim = foes.FirstOrDefault(f => f && f.alive && f.team == 1 && f.d.kind == "champ"); string vn = victim ? victim.d.name : "";
            if (victim) { victim.Damage(1e7f, victim.Pos); yield return new WaitForSecondsRealtime(Lane.RespawnTime(9) + 5); var back = foes.FirstOrDefault(f => f && f.alive && f.d.name == vn && f.team == 1); log.AppendLine($"respawned {vn}: {(back ? string.Join(",", Perks.Taken(Perks.Key(1, vn)).Select(p => p.id)) + $" hp {back.maxHp:0} vigor {back.vigorOn}" : "not back")}"); }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "perks.txt"), log.ToString()); Application.Quit();
        }
        // -jungletest <dir>: the camps come up on time, a camp fights back and leashes home, the player clears the Giant and gets its buff
        System.Collections.IEnumerator JungleTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            StartLane(3, 2); yield return null; time = 49; yield return new WaitForSecondsRealtime(1.5f);
            log.AppendLine("camps up at 0:50: " + string.Join(", ", Jungle.camps.Select(c => $"{c.id} {(c.Alive ? "up" : "down")} x{c.members.Count(m => m && m.alive)} at {c.at}")));
            var g = Jungle.camps.First(c => c.id == "might").members.First(m => m);
            MoveTo(g.Pos + new Vector3(0, 0, 3.5f)); yield return new WaitForSecondsRealtime(.8f); Snap("camp_giant");
            float hp0 = hero.hp; g.Damage(10, hero.Pos); yield return new WaitForSecondsRealtime(3); log.AppendLine($"giant struck: targets player {g.lastHitBy == (ITarget)hero} · player hp {hp0:0} -> {hero.hp:0}");
            MoveTo(g.home + new Vector3(0, 0, 14)); yield return new WaitForSecondsRealtime(4); log.AppendLine($"player ran off: giant target {(g.lastHitBy == null ? "none" : "still")} · giant from home {Flat(g.Pos - g.home).magnitude:0.0} m · hp {g.hp:0}/{g.maxHp:0}");
            MoveTo(g.Pos + new Vector3(0, 0, 2.5f)); yield return null; float atk0 = hero.AtkPow; g.Damage(1e7f, hero.Pos); yield return new WaitForSecondsRealtime(1);
            log.AppendLine($"giant down: player might {hero.mightT:0} s · attack {atk0:0} -> {hero.AtkPow:0} · camp back at {Jungle.camps.First(c => c.id == "might").nextAt:0} s · events {string.Join(" | ", Jungle.Events)}");
            Snap("buff_might");
            time = 241; yield return new WaitForSecondsRealtime(1.5f); var bc = Jungle.camps.First(c => c.id == "boss");
            var bm = bc.members.FirstOrDefault(m => m && m.alive); log.AppendLine($"guardian at 4:01: {(bm ? $"up, hp {bm.hp:0}, at {bm.Pos}" : "missing")}");
            if (bm) { MoveTo(bm.Pos + new Vector3(0, 0, 6)); yield return new WaitForSecondsRealtime(1); Snap("guardian"); MoveTo(Lane.SpawnOf(0)); yield return new WaitForSecondsRealtime(.5f); }
            time = 300; TestSpeed = 4; float r0 = Time.realtimeSinceStartup, nextLog = 0;
            while (Time.realtimeSinceStartup - r0 < 45 && mode == Mode.Game)
            {   // where each side's jungler is and what it is doing
                if (Time.realtimeSinceStartup - r0 >= nextLog)
                {
                    nextLog += 3; var js = foes.Where(f => f && f.alive && Jungle.IsJungler(f)).ToList();
                    log.AppendLine($"  {time:0}s junglers: " + string.Join(" ; ", js.Select(f => $"{f.d.name} T{f.team} at ({f.Pos.x:0},{f.Pos.z:0}) hp {f.hp / f.maxHp:0.00} camp {(f.JungleCamp != null ? f.JungleCamp.id : "-")} tgt {(f.Tgt == null ? "-" : f.Tgt is Hero ? "player" : f.Tgt is Foe tf ? tf.d.id : "?")}")) + $" · camps up: {string.Join(",", Jungle.camps.Where(c => c.Alive).Select(c => c.id))}");
                }
                yield return null;
            }
            TestSpeed = 1;
            log.AppendLine($"after ~3 min of AI play: {Jungle.Events.Count} clears · {string.Join(" | ", Jungle.Events)}");
            log.AppendLine($"AI buffs now: {string.Join(", ", foes.Where(f => f && f.alive && f.d.kind == "champ").Select(f => $"{f.d.name} T{f.team} might {f.mightT:0} haste {f.hasteT:0}"))}");
            bm = bc.members.FirstOrDefault(m => m && m.alive); log.AppendLine($"guardian after the AI play: {(bm ? $"up, hp {bm.hp / bm.maxHp:0.00}" : "down")} · blessed blue {Jungle.Blessed(0)} red {Jungle.Blessed(1)}");
            if (bm) { MoveTo(bm.Pos + new Vector3(0, 0, 4)); yield return null; float a0 = hero.AtkPow; bm.Damage(1e7f, hero.Pos); yield return new WaitForSecondsRealtime(1); log.AppendLine($"player fells the guardian: blessed blue {Jungle.Blessed(0)} ({Jungle.BlessLeft(0):0} s) · attack {a0:0} -> {hero.AtkPow:0}"); Snap("guardian_down"); }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "jungle.txt"), log.ToString()); Application.Quit();
        }
        System.Collections.IEnumerator SkyLanesTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; save.hero = "warrior"; hero.Equip();
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            StartLane(3, 2); yield return new WaitForSecondsRealtime(2);
            {   // the score bar and a kill call: an enemy champion falls beside the hero
                var victim = foes.FirstOrDefault(f => f && f.alive && f.team == 1 && f.d.kind == "champ");
                if (victim) { MoveTo(victim.Pos + new Vector3(1.5f, 0, 0)); yield return null; victim.Damage(1e7f, hero.Pos); yield return new WaitForSecondsRealtime(.5f); Snap("killcall"); log.AppendLine($"kill call: score {teamKills[0]}:{teamKills[1]} kda {myK}/{myD}/{myA}"); yield return new WaitForSecondsRealtime(.2f); }
                log.AppendLine($"lane drops on the ground after a kill: {drops.Count} (orbs and items are off in lane battles)");
                {   // an AI hero's damage stays the same after it dies and comes back (the difficulty used to compound)
                    var champ = foes.FirstOrDefault(f => f && f.alive && f.team == 1 && f.d.kind == "champ"); string nm = champ ? champ.d.name : ""; float dmg0 = champ ? champ.d.dmg : 0;
                    if (champ) { champ.Damage(1e7f, champ.Pos); yield return new WaitForSecondsRealtime(10.6f); var back = foes.FirstOrDefault(f => f && f.alive && f.d.name == nm); log.AppendLine($"respawned AI {nm}: dmg {dmg0:0.0} -> {(back ? back.d.dmg : -1):0.0}"); }
                }
                ui.ShowBoard(true); yield return new WaitForSecondsRealtime(.4f); Snap("scoreboard"); log.AppendLine($"scoreboard levels: {string.Join(", ", stats.Select(x => $"{x.name} T{x.team} Lv{LevelOf(x)}"))} · team XP {teamXp[0]} : {teamXp[1]}"); yield return new WaitForSecondsRealtime(.2f); ui.ShowBoard(false);
                hero.inv = 0; hero.Hurt(1e7f, hero.Pos + Vector3.forward); yield return new WaitForSecondsRealtime(1.5f); Snap("respawn_timer"); log.AppendLine($"player down: respawn in {respawnAt - Time.time:0.0} s · stats {string.Join(", ", stats.Select(s => $"{s.name}({s.team}) {s.k}/{s.d}/{s.a}"))}"); yield return new WaitForSecondsRealtime(5);
                MoveTo(Lane.SpawnOf(0)); yield return new WaitForSecondsRealtime(.5f);
            }
            hero.max = hero.hp = 999999; hitchLog = true;   // slow frames go to the log with what happened before them
            log.AppendLine($"stage {System.Array.Find(stageRoots, r => r && r.gameObject.activeSelf)?.name} · towers+core ours {TowersLeft(0)} theirs {TowersLeft(1)} · heroes {foes.Count(f => f && f.d.kind == "champ")} · lane lengths {SkyLanes.Length(0):0} / {SkyLanes.Length(1):0} m");
            foreach (var (x, z) in new[] { (5f, 50f), (40f, 40f), (-20f, 10f) }) log.AppendLine($"clamp ({x}, {z}) -> {Clamp(new Vector3(x, 0, z))}");
            {   // the real way a hero walks (Clamp: walkable ground, towers, the stage's obstacles), from each gate to the jungle's middle;
                // and every obstacle circle inside the jungle, with the scenery it came from
                var to = new Vector3(SkyLanes.Jungle.x, 0, SkyLanes.Jungle.y);
                foreach (var (ga, _) in SkyLanes.Gates) { var p = new Vector3(ga.x, 0, ga.y); for (int i = 0; i < 300; i++) p = Clamp(Vector3.MoveTowards(p, to, .2f)); log.AppendLine($"walk (Clamp) from gate {ga}: ends {Flat(p - to).magnitude:0.0} m from the jungle middle at ({p.x:0.0}, {p.z:0.0})"); }
                foreach (var o in Obstacles.Current) if (Vector2.Distance(new Vector2(o.x, o.y), SkyLanes.Jungle) < SkyLanes.JungleR + 2) log.AppendLine($"  obstacle in the jungle at ({o.x:0.0}, {o.y:0.0}) r {o.z:0.0} {Obstacles.NameOf(o)}");
                var onWalk = Obstacles.Current.Where(o => SkyLanes.Inside(new Vector2(o.x, o.y), -o.z * .5f) && Vector2.Distance(new Vector2(o.x, o.y), SkyLanes.Jungle) > SkyLanes.JungleR).ToList();
                log.AppendLine($"obstacles {Obstacles.Current.Count}, on lane or base ground {onWalk.Count}: " + string.Join(", ", onWalk.GroupBy(o => Obstacles.NameOf(o)).Select(g => $"{g.Key} x{g.Count()}")));
            }
            foreach (var (ga, _) in SkyLanes.Gates)
            {   // walk in by each jungle gate: from the lane end straight for the jungle's middle, kept on the walkable ground at every step
                var p = new Vector3(ga.x, 0, ga.y); var to = new Vector3(SkyLanes.Jungle.x, 0, SkyLanes.Jungle.y);
                for (int i = 0; i < 200; i++) p = SkyLanes.Walk(Vector3.MoveTowards(p, to, .25f));
                log.AppendLine($"gate {ga}: walked in to {Vector3.Distance(p, to):0.0} m from the jungle middle");
            }
            log.AppendLine($"land: our core {SkyLanes.Height(0, Lane.CoreZ):0.00} m, lane mid {SkyLanes.Height(0, 0):0.00}, jungle mid {SkyLanes.Height(SkyLanes.Jungle.x, SkyLanes.Jungle.y):0.00} · hero stands at y {hero.Pos.y:0.00} (ground {River.Ground(hero.Pos.x, hero.Pos.z):0.00})");
            {   // the spawn pools: where we start, the pool heals, an enemy hero put in our pool is pushed out
                var ours = SkyLanes.Fountain(0); log.AppendLine($"pool: hero starts {Flat(hero.Pos - ours).magnitude:0.0} m from our pool centre");
                hero.hp = hero.max * .5f; float was = hero.hp; yield return new WaitForSecondsRealtime(1); log.AppendLine($"pool: heal in 1 s {(hero.hp - was) / hero.max * 100:0}% of max");
                var foe = foes.FirstOrDefault(f => f && f.alive && f.team == 1 && f.d.kind == "champ"); if (foe) { foe.transform.position = ours; yield return null; yield return null; log.AppendLine($"pool: enemy hero put in our pool ends {Flat(foe.Pos - ours).magnitude:0.0} m from centre (pool radius {SkyLanes.FountainR})"); }
                ZoomBy(-.4f); ui.Pan = ours + new Vector3(0, 0, 1); yield return new WaitForSecondsRealtime(1.5f); Snap("pool_ours"); yield return new WaitForSecondsRealtime(.2f); ui.Pan = SkyLanes.Fountain(1) + new Vector3(0, 0, 1);   // the view centres about a metre short of what it follows yield return new WaitForSecondsRealtime(1.2f); Snap("pool_theirs"); ui.Pan = null; ZoomBy(.4f); hero.max = hero.hp = 999999;
            }
            ZoomBy(1.2f);
            foreach (var (n, at) in new[] { ("view_ourbase", new Vector3(0, 0, 38)), ("view_jungle", new Vector3(17, 0, 2)), ("view_curve", new Vector3(32, 0, 8)), ("view_theirbase", new Vector3(0, 0, -36)) })
            { ui.Pan = at; yield return new WaitForSecondsRealtime(1.2f); Snap(n); yield return new WaitForSecondsRealtime(.2f); }
            ui.Pan = null; ZoomBy(-1.2f);
            {   // diving a tower alone: the hero stands under the enemy's outer straight-lane tower with no soldiers about; shots grow heavier
                MoveTo(new Vector3(0, 0, -Lane.TowerZ[0] + 4)); hero.max = hero.hp = 999999; float was = hero.hp; var hits = new List<float>(); float last = hero.hp;
                for (float t = 0; t < 4; t += Time.unscaledDeltaTime) { if (hero.hp < last - 1) hits.Add(last - hero.hp); last = hero.hp; hero.inv = 0; yield return null; }
                Snap("dive"); log.AppendLine($"alone under a tower 4 s: took {was - hero.hp:0} in {hits.Count} hits ({string.Join(", ", hits.Select(h => h.ToString("0")))}) · plain tower shot {Lane.Tower.dmg}");
                MoveTo(new Vector3(0, 0, Lane.SpawnZ)); hero.max = hero.hp = 999999;
            }
            var towerAt = foes.Where(f => f && f.d.kind == "tower").ToDictionary(f => f, f => f.Pos);   // towers must end where they began
            int shot = 0; float worstFrame = 0, ms = 0, snapAt = -1, closeAt = -1; int frames = 0;
            for (float t = 0; t < 75; t += Time.unscaledDeltaTime)
            {
                hero.hp = hero.max; ms += Time.unscaledDeltaTime * 1000; frames++; worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime * 1000);
                if (snapAt > 0 && t >= snapAt) { snapAt = -1; Snap($"match_{shot}"); if (shot == 1) { ZoomBy(-.5f); closeAt = t + .9f; } }
                if (closeAt > 0 && t >= closeAt) { closeAt = -1; Snap("soldiers_close"); ZoomBy(.5f); }
                if (t > 15 * (shot + 1))
                {
                    var live = foes.Where(f => f && f.alive && !f.spawning).ToList(); var off = live.Count(f => f.d.kind != "tower" && !SkyLanes.Inside(new Vector2(f.Pos.x, f.Pos.z), .3f));
                    var front0 = live.Where(f => f.team == 0 && f.d.kind == "minion").Select(f => f.Pos.z).DefaultIfEmpty(99).Min(); var front1 = live.Where(f => f.team == 1 && f.d.kind == "minion").Select(f => f.Pos.z).DefaultIfEmpty(-99).Max();
                    log.AppendLine($"t {t:0}s: soldiers {live.Count(f => f.team == 0 && f.d.kind == "melee" && f.d.id == "minion")} v {live.Count(f => f.team == 1 && f.d.id == "minion")} · heroes {live.Count(f => f.team == 0 && f.d.kind == "champ")} v {live.Count(f => f.team == 1 && f.d.kind == "champ")} · towers {TowersLeft(0)} v {TowersLeft(1)} · off the ground {off} · wave {wave}");
                    var lead = live.Where(f => f.team == 0 && f.d.id == "minion").OrderBy(f => f.Pos.z).FirstOrDefault(); if (lead) ui.Pan = lead.Pos;
                    shot++; snapAt = t + .9f;   // the camera eases over first
                }
                yield return null;
            }
            ui.Pan = null; hitchLog = false; log.AppendLine($"frames: avg {ms / Mathf.Max(1, frames):0.0} ms, worst {worstFrame:0} ms");
            {   // towers in order: the enemy's inner tower and core shrug off hits until the ones before them fall
                Foe T(int tier, int ln = 0) => foes.FirstOrDefault(f => f && f.alive && f.team == 1 && f.towerTier == tier && (tier == 2 || f.laneIdx == ln));
                float Hit(Foe f) { if (!f) return -1; float was = f.hp; f.Damage(500, f.Pos + Vector3.forward * 3, new Hit { melee = true }); return was - f.hp; }
                log.AppendLine($"order: inner while outer stands takes {Hit(T(1)):0}, core takes {Hit(T(2)):0}");
                var outer = T(0); if (outer) outer.Damage(1e9f, outer.Pos); yield return null; log.AppendLine($"order: outer down, inner now takes {Hit(T(1)):0}, core still takes {Hit(T(2)):0}");
                var inner = T(1); if (inner) inner.Damage(1e9f, inner.Pos); yield return null; log.AppendLine($"order: lane cleared, core now takes {Hit(T(2)):0}");
            }
            log.AppendLine($"towers moved: furthest {towerAt.Where(kv => kv.Key && kv.Key.alive).Select(kv => Flat(kv.Key.Pos - kv.Value).magnitude).DefaultIfEmpty(0).Max():0.00} m");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "skylanes.txt"), log.ToString()); yield return new WaitForSecondsRealtime(.5f); Application.Quit();
        }
        System.Collections.IEnumerator GateWalk(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); StartCity(); yield return new WaitForSecondsRealtime(2);
            float Walk(Vector3 from, Vector3 to) { var p = from; for (int i = 0; i < 2000; i++) { var d = Flat(to - p); if (d.magnitude < .05f) break; p = Clamp(p + d.normalized * .05f); } return Flat(to - p).magnitude; }
            var gc = cityRoot.Find("GateCircle"); log.AppendLine($"gate circle found {gc != null}");
            foreach (var (n, at) in new[] { ("gate", City.Gate), ("quest", new Vector3(-14, 0, 2)), ("shop", new Vector3(0, 0, -15.5f)), ("smith", new Vector3(14, 0, 2)) }) log.AppendLine($"city {n}: stops {Walk(City.Start, at):0.0} m away (prompt needs under 3.4)");
            hero.transform.position = City.Gate + new Vector3(0, 0, 2); yield return new WaitForSecondsRealtime(1); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "gate_prompt.png")); yield return new WaitForSecondsRealtime(.5f);
            StartWilds(); yield return new WaitForSecondsRealtime(1); log.AppendLine($"zone door: stops {Walk(OpenWorld.Start, wildsCtl.Door):0.0} m away; zone gate: {Walk(new Vector3(0, 0, 40), OpenWorld.Gate):0.0} m away");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "gatewalk.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-wildstest <dir>): the gate panel, the zone from several places, its edge, the way back; logs frame times.
        System.Collections.IEnumerator WildsTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); var log = new System.Text.StringBuilder(); yield return new WaitForSecondsRealtime(3); save.wings = ""; save.hero = "warrior"; hero.Equip();
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            StartCity(); yield return new WaitForSecondsRealtime(2); ui.OpenCityPanel("gate"); yield return new WaitForSecondsRealtime(1); Snap("gate"); yield return new WaitForSecondsRealtime(.5f); ui.CloseCityPanel();
            float t0 = Time.realtimeSinceStartup; StartWilds(); yield return null; log.AppendLine($"enter frame {(Time.realtimeSinceStartup - t0) * 1000:0} ms");
            yield return new WaitForSecondsRealtime(2); Snap("start"); yield return new WaitForSecondsRealtime(.5f);
            foreach (var (n, at) in new[] { ("middle", new Vector3(0, 0, 8)), ("stream", new Vector3(-16, 0, WildsMap.StreamZ(-16))), ("bridge", new Vector3(1.5f, 0, WildsMap.StreamZ(1.5f))), ("hillside", new Vector3(18, 0, -28)), ("hilltop", new Vector3(WildsMap.Hill.x, 0, WildsMap.Hill.y)), ("rise", new Vector3(WildsMap.Rise.x, 0, WildsMap.Rise.y)) })
            {
                hero.transform.position = at; yield return new WaitForSecondsRealtime(2.5f); Snap(n); log.AppendLine($"{n}: ground {River.Ground(at.x, at.z):0.00} hero y {hero.Pos.y:0.00} in water {River.InWater(at.x, at.z)}"); var ms = new List<float>(); for (float t = 0; t < 3; t += Time.unscaledDeltaTime) { ms.Add(Time.unscaledDeltaTime * 1000); yield return null; } ms.Sort(); log.AppendLine($"{n}: median {ms[ms.Count / 2]:0.0} ms worst5% {ms[(int)(ms.Count * .95f)]:0.0} ms");
            }
            hero.transform.position = wildsCtl.Door + new Vector3(0, 0, 1); yield return new WaitForSecondsRealtime(1.5f); Snap("house_door"); yield return new WaitForSecondsRealtime(.5f);
            EnterHouse(); yield return new WaitForSecondsRealtime(2); Snap("house_in"); log.AppendLine($"house: foes {foes.Count(x => x && x.alive)} chest ready {HouseFought} edge {Clamp(new Vector3(500, 0, 500)):0.0}"); yield return new WaitForSecondsRealtime(.5f);
            hero.max = hero.hp = 999999; ui.atkP = true; yield return new WaitForSecondsRealtime(.35f); ui.atkP = false; Snap("house_fight"); yield return new WaitForSecondsRealtime(.5f);
            foreach (var x in foes.ToArray()) if (x && x.alive) x.Damage(1e7f, x.Pos); yield return new WaitForSecondsRealtime(1); hero.transform.position = OpenWorld.Chest + new Vector3(0, 0, 2.2f); yield return new WaitForSecondsRealtime(1); Snap("house_chest"); log.AppendLine($"house: all down, chest ready {HouseFought}"); yield return new WaitForSecondsRealtime(.5f);
            int dr = drops.Count; wildsCtl.OpenChest(); log.AppendLine($"chest: drops {dr} -> {drops.Count}, cleared {wildsCtl.HouseCleared}"); yield return new WaitForSecondsRealtime(1.2f); Snap("house_loot"); yield return new WaitForSecondsRealtime(.5f);
            LeaveHouse(); yield return new WaitForSecondsRealtime(1); log.AppendLine($"left: wilds {wilds} house {house} at door {Vector3.Distance(hero.Pos, wildsCtl.Door):0.0} m"); EnterHouse(); yield return new WaitForSecondsRealtime(1); log.AppendLine($"second visit: foes {foes.Count(x => x && x.alive)}"); gold = 100; hero.inv = 0; hero.Hurt(1e8f, hero.Pos + Vector3.forward); yield return new WaitForSecondsRealtime(4); log.AppendLine($"fell: gold {gold} (was 100) dead {hero.dead} wilds {wilds} house {house} at {hero.Pos} mode {mode}"); Snap("fell"); yield return new WaitForSecondsRealtime(.5f);
            { var camp = new Vector3(42, 0, -14); hero.transform.position = OpenWorld.Start; yield return new WaitForSecondsRealtime(1); log.AppendLine($"areas at the gate: {wildsCtl.AreaReport}");
              hero.max = hero.hp = 999999; hero.transform.position = camp; yield return new WaitForSecondsRealtime(4); log.AppendLine($"areas at the camp: {wildsCtl.AreaReport} (alive {wildsCtl.AliveInAreas})"); Snap("camp"); yield return new WaitForSecondsRealtime(.5f); ui.atkP = true; yield return new WaitForSecondsRealtime(.4f); ui.atkP = false; Snap("camp_fight");
              var msf = new List<float>(); for (float t = 0; t < 4; t += Time.unscaledDeltaTime) { hero.hp = hero.max; msf.Add(Time.unscaledDeltaTime * 1000); yield return null; } msf.Sort(); log.AppendLine($"camp fight: median {msf[msf.Count / 2]:0.0} ms worst5% {msf[(int)(msf.Count * .95f)]:0.0} ms");
              float Away() { float sum = 0; int n = 0; foreach (var x in foes) if (x && x.alive && x.leash > 0 && Flat(x.Pos - camp).magnitude < 40) { sum += Flat(x.Pos - x.home).magnitude; n++; } return n > 0 ? sum / n : -1; }
              log.AppendLine($"chasing: {Away():0.0} m from home on average"); hero.transform.position = camp + new Vector3(-30, 0, 18); yield return new WaitForSecondsRealtime(8); log.AppendLine($"hero 35 m away, 8 s later: {Away():0.0} m from home (leash)");
              hero.transform.position = new Vector3(-50, 0, -50); yield return new WaitForSecondsRealtime(2); log.AppendLine($"moved far away: {wildsCtl.AreaReport}");
              hero.transform.position = camp; yield return new WaitForSecondsRealtime(3); foreach (var x in foes.ToArray()) if (x && x.alive) x.Damage(1e7f, x.Pos); yield return new WaitForSecondsRealtime(2); log.AppendLine($"back at the camp, all killed: {wildsCtl.AreaReport}"); Snap("after_kill");
              yield return new WaitForSecondsRealtime(40); log.AppendLine($"42 s later: {wildsCtl.AreaReport}"); for (int t = 0; t < 52; t++) { hero.hp = hero.max; yield return new WaitForSecondsRealtime(1); } log.AppendLine($"94 s later: {wildsCtl.AreaReport}"); Snap("after_wait"); hero.transform.position = new Vector3(10, 0, 51); yield return new WaitForSecondsRealtime(2); Snap("grove"); hero.transform.position = new Vector3(-13, 0, 40); yield return new WaitForSecondsRealtime(3); Snap("grove2");
              var clock = wildsCtl.Clock; var br = new Vector3(1.5f, 0, WildsMap.StreamZ(1.5f)); clock.t = 100; hero.inv = 0; hero.hp = hero.max; hero.Hurt(1000, hero.Pos); float hurtDay = hero.max - hero.hp; hero.hp = hero.max; var someone = foes.FirstOrDefault(x => x && x.alive && x.leash > 0); float hpDay = someone ? someone.maxHp : 0; bool deckDay = BridgeDeck.Height(br.x, br.z, out _);
              hero.transform.position = br + new Vector3(0, 0, 7); yield return new WaitForSecondsRealtime(1.5f); Snap("day_bridge");
              hero.transform.position = new Vector3(42, 0, -14); clock.t = 238; yield return new WaitForSecondsRealtime(3); hero.inv = 0; hero.hp = hero.max; hero.Hurt(1000, hero.Pos); float hurtNight = hero.max - hero.hp; hero.hp = hero.max; Snap("night_camp");
              int before = drops.Sum(x => x.v); DropLoot(hero.Pos, false, 10); log.AppendLine($"night: isNight {clock.IsNight} foe hp {hpDay:0} -> {(someone ? someone.maxHp : 0):0}; hurt {hurtDay:0} -> {hurtNight:0}; 10 gold becomes {drops.Sum(x => x.v) - before}; bridge deck {deckDay} -> {BridgeDeck.Height(br.x, br.z, out _)}; label {wildsCtl.TimeLabel}");
              hero.transform.position = br + new Vector3(0, 0, 7); yield return new WaitForSecondsRealtime(1.5f); Snap("night_bridge");
              hero.transform.position = new Vector3(-30, 0, -24); yield return new WaitForSecondsRealtime(3); log.AppendLine($"night forest: {wildsCtl.AreaReport}"); Snap("night_forest");
              clock.t = 359; yield return new WaitForSecondsRealtime(3); log.AppendLine($"dawn near them: {wildsCtl.AreaReport} bridge {BridgeDeck.Height(br.x, br.z, out _)} night {clock.IsNight} label {wildsCtl.TimeLabel}");
              hero.transform.position = new Vector3(-6, 0, -10); yield return new WaitForSecondsRealtime(2); log.AppendLine($"dawn walked off: {wildsCtl.AreaReport}"); Snap("dawn"); }
            log.AppendLine($"edge: 500 m east is limited to {Clamp(new Vector3(500, 0, 0)).x:0.0} (zone radius {OpenWorld.Radius})");
            hero.transform.position = OpenWorld.Gate + new Vector3(1.5f, 0, -1); yield return new WaitForSecondsRealtime(1.5f); Snap("gate_back"); yield return new WaitForSecondsRealtime(.5f);
            log.AppendLine($"foes in zone: {foes.Count}, wilds {wilds}"); StartCity(); yield return new WaitForSecondsRealtime(2); log.AppendLine($"back: city {city}, wilds {wilds}, edge {Clamp(new Vector3(500, 0, 0)).x:0.0}"); Snap("back_city"); yield return new WaitForSecondsRealtime(.5f);
            StartGame(1); hero.max = hero.hp = 999999; yield return new WaitForSecondsRealtime(5); ui.atkP = true; yield return new WaitForSecondsRealtime(.35f); ui.atkP = false; Snap("fight"); yield return new WaitForSecondsRealtime(.5f); log.AppendLine($"stage 1: wilds {wilds}, foes {foes.Count}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "wilds.txt"), log.ToString()); Application.Quit();
        }
        // Test mode (-classlook <dir>): the character select stage for one hero of each class.
        System.Collections.IEnumerator ClassLook(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); save.shield = "";
            foreach (var h in new[] { "warrior", "samurai", "mage", "elf", "sunpriest", "monk" })
            {
                save.hero = h; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.TestFilterTop(hero.def.cls); yield return new WaitForSecondsRealtime(1.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, h + ".png")); yield return new WaitForSecondsRealtime(.4f);
            }
            Application.Quit();
        }
        // Test mode (-archerlook <dir>): the three archers in the lobby with a sword and wings chosen, then Aeris close up in the city, a stage and Sky Lanes (are heroes pale in play?)
        System.Collections.IEnumerator ArcherLook(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3); var keep = JsonUtility.ToJson(save);
            void Snap(string n) => ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, n + ".png"));
            var wing = Data.Wings.Length > 0 ? Data.Wings[0].id : ""; save.sword = "w_lava"; if (!save.owned.Contains(Shop.Key(ItemKind.Wings, wing))) save.owned.Add(Shop.Key(ItemKind.Wings, wing)); save.wings = wing;
            foreach (var h in new[] { "harpy", "elf", "rowan" }) { save.hero = h; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.SetTab("hero"); yield return new WaitForSecondsRealtime(1.6f); Snap("lobby_" + h); yield return new WaitForSecondsRealtime(.3f); }
            foreach (var hd in Data.Heroes)
            {   // every hero: in the lobby, then close behind the shoulder on stage 1 (same light every time), to compare how pale each looks in play
                save.hero = hd.id; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.SetTab("hero"); yield return new WaitForSecondsRealtime(1.2f); Snap("all_lobby_" + hd.id); yield return new WaitForSecondsRealtime(.2f);
                CamStyle = 3; StartGame(1); foreach (var f in foes.ToArray()) if (f) Destroy(f.gameObject); foes.Clear(); hero.face = 0; yield return new WaitForSecondsRealtime(1.6f); Snap("all_game_" + hd.id); yield return new WaitForSecondsRealtime(.2f); CamStyle = -1; ShowLobby(); yield return new WaitForSecondsRealtime(.3f);
            }
            save.hero = "harpy"; CamStyle = 3;
            StartCity(); yield return new WaitForSecondsRealtime(2); Snap("play_city"); yield return new WaitForSecondsRealtime(.3f);
            StartGame(1); yield return new WaitForSecondsRealtime(2.5f); Snap("play_stage1"); yield return new WaitForSecondsRealtime(.3f);
            if (Stage("Stage_skylanes")) { StartLane(3, 2); yield return new WaitForSecondsRealtime(2.5f); Snap("play_skylanes"); yield return new WaitForSecondsRealtime(.3f); }
            CamStyle = -1; save = JsonUtility.FromJson<Save>(keep); Persist(); yield return new WaitForSecondsRealtime(.5f); Application.Quit();
        }
        // Test mode (-weaponcards <dir>): the weapons tab for one hero of each kind, suited weapons first.
        System.Collections.IEnumerator WeaponCardsTest(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            foreach (var hd in Data.Heroes) if (Studio.HeroFull(hd.id) is Texture2D card && card.isReadable) System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, "card_" + hd.id + ".png"), card.EncodeToPNG());
            foreach (var h in new[] { "sunpriest", "lyra", "spartan", "warrior" })
            {
                save.hero = h; hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.SetTab("hero"); yield return new WaitForSecondsRealtime(1.2f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, "lobby_" + h + ".png")); yield return new WaitForSecondsRealtime(.4f);
            }
            save.shield = ""; foreach (var h in new[] { "sunpriest", "lyra", "spartan", "warrior" }) { save.hero = h; save.best = 9; StartGame(1); yield return new WaitForSecondsRealtime(2.5f); hero.max = hero.hp = 999999; hero.mp = hero.mpMax; for (int i = 0; i < 3; i++) { ui.skP[i] = true; yield return new WaitForSecondsRealtime(1.1f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{h}_skill{i}.png")); yield return new WaitForSecondsRealtime(.4f); hero.mp = hero.mpMax; } ShowLobby(); yield return new WaitForSecondsRealtime(1); }
            Application.Quit();
        }
        // Test mode (-new5test <dir>): the five empty-handed heroes holding chosen weapons, then the two bosses that replaced the dragon.
        System.Collections.IEnumerator New5Test(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            foreach (var (h, w, sh) in new[] { ("warrior", "w_greataxe", ""), ("samurai", "w_katana", ""), ("hood", "w_daggers", ""), ("ninja", "w_kunai", ""), ("spartan", "w_lava", "sh_knight") })
            {
                save.hero = h; save.sword = w; save.owned.Add(Shop.Key(ItemKind.Weapon, w)); save.shield = sh; if (sh != "") save.owned.Add(Shop.Key(ItemKind.Shield, sh)); save.wings = "";
                hero.Equip(); hero.PlaceLobby(); ui.ShowLobby(); ui.heroYaw = 0; yield return new WaitForSecondsRealtime(1.6f);
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{h}_lobby.png")); yield return new WaitForSecondsRealtime(.4f);
                save.best = 9; StartGame(1); yield return new WaitForSecondsRealtime(2.5f);
                ui.atkP = true; yield return new WaitForSecondsRealtime(.35f); ui.atkP = false;
                ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"{h}_fight.png")); yield return new WaitForSecondsRealtime(.8f);
                ShowLobby(); yield return new WaitForSecondsRealtime(1);
            }
            foreach (var id in new[] { "river", "valley" })
            {
                int st = System.Array.FindIndex(Data.Themes, x => x.id == id) + 1; save.hero = "warrior"; save.best = 9; StartGame(st); yield return null;
                hero.max = hero.hp = 999999; foreach (var f in foes) if (f) Destroy(f.gameObject); foes.Clear(); wave = Theme.waves.Length; SpawnBoss();
                for (int i = 0; i < 3; i++) { yield return new WaitForSecondsRealtime(2.2f); ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(dir, $"boss_{id}_{i}.png")); }
                ShowLobby(); yield return new WaitForSecondsRealtime(1);
            }
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
        int clipFrame, clipSuper = 1; string clipDir; bool clipOn;
        System.Collections.IEnumerator ClipRec()
        {
            while (clipOn)
            {
                yield return new WaitForEndOfFrame();
                var t = ScreenCapture.CaptureScreenshotAsTexture(clipSuper); System.IO.File.WriteAllBytes(System.IO.Path.Combine(clipDir, $"f{clipFrame++:00000}.jpg"), t.EncodeToJPG(92)); Destroy(t);
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

            clipOn = false; yield return null; Time.captureFramerate = 0; Application.Quit();
        }
        // -storyclip <dir>: the 30 s story clip at twice the window size, so a tall crop stays sharp: pick Ming, pick Fire Wings,
        // wear loot from the Moon Arc mid-fight, then fight with orbs and end on stage 3. Creeps are weakened and orbs drop more often than in play.
        System.Collections.IEnumerator StoryClip(string dir)
        {
            System.IO.Directory.CreateDirectory(dir); clipDir = dir; clipSuper = 2;
            yield return new WaitForSecondsRealtime(4); Time.captureFramerate = 30;
            System.Collections.IEnumerator Hold(float secs) { for (int i = 0, n = Mathf.RoundToInt(secs * 30); i < n; i++) yield return null; }
            void Wear(string tab) { ui.SetTab(tab); ui.TestPick(); }
            save.owned.Add(Shop.Key(ItemKind.Wings, "fire")); save.owned.Add(Shop.Key(ItemKind.Weapon, "w_fantasy")); save.sword = "w_fantasy"; save.pet = "candle"; save.wings = ""; save.petWings = false; save.bag.Clear(); save.helm = save.armor = save.boots = save.acc = ""; save.potions = 5;
            save.hero = "warrior"; ui.ShowLobby(); Wear("hero"); ui.lobbyZoom = 0; ui.heroYaw = 0; yield return Hold(.5f);
            clipOn = true; StartCoroutine(ClipRec());
            yield return Hold(1.2f); save.hero = "samurai"; Wear("hero"); yield return Hold(1.2f); ui.TestFilter("monk"); save.hero = "monk"; Wear("hero");
            for (int i = 0; i < 90; i++) { ui.lobbyZoom = i / 90f * .3f; yield return null; }
            ui.SetTab("wings"); yield return Hold(1); save.wings = "fire"; Wear("wings");
            for (int i = 0; i < 150; i++) { ui.heroYaw = Mathf.SmoothStep(0, 1, Mathf.PingPong(i / 75f, 1)) * 190; yield return null; }
            ui.heroYaw = 0; ui.lobbyZoom = 0; save.best = 5; StartGame(1); hero.inv = 0; yield return ClipFight(1.2f);
            foreach (var id in new[] { "helm_dragon", "armor_knight", "ring_shadow" }) { save.bag.Add(id); runLoot.Add(id); }
            ui.OpenFieldBag(); yield return Hold(1.6f);
            for (int i = 0; i < 3; i++) { ui.FieldWearFirst(); yield return Hold(1.2f); }
            ui.CloseFieldBag(); ui.ResumeFromPause();
            var kinds = new[] { "fury", "gold", "quick", "gold" }; int nextOrb = 0;
            System.Collections.IEnumerator Fight(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    hero.inv = 0; foreach (var f in foes) if (f && f.alive && f.team != 0) f.hp = Mathf.Min(f.hp, f.maxHp * .35f);
                    if (i % 60 == 30) AddDrop(hero.Pos + Quaternion.Euler(0, i * 3.1f, 0) * Vector3.forward * 4.5f, kinds[nextOrb++ % kinds.Length], 5);
                    var near = foes.Where(f => f && f.alive && f.team != 0).OrderBy(f => (f.Pos - hero.Pos).sqrMagnitude).FirstOrDefault();
                    if (near) { var d = Flat(near.Pos - hero.Pos); ui.Joy = d.magnitude > 2.6f ? new Vector2(-d.x, d.z).normalized : Vector2.zero; }
                    if (Random.value < .12f) ui.atkP = true; if (Random.value < .04f) ui.skP[Random.Range(0, 3)] = true;
                    hero.hp = Mathf.Max(hero.hp, hero.max * .6f); hero.mp = Mathf.Max(hero.mp, 40);
                    yield return null;
                }
            }
            yield return Fight(135); StartGame(3); hero.inv = 0; yield return Fight(300);
            ui.Joy = Vector2.zero; clipOn = false; yield return null; Time.captureFramerate = 0; Application.Quit();
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
            Application.Quit();
        }

        // ---------- stages ----------
        public void SetTheme(int i)
        {
            LobbyNight.Set(false); city = false; wilds = false; house = false; sky = false; if (cityRoot) cityRoot.gameObject.SetActive(false); foreach (var t in autumnTrees) if (t) t.SetActive(true); autumnTrees.Clear();
            for (int k = 0; k < stageRoots.Length; k++) if (stageRoots[k]) stageRoots[k].gameObject.SetActive(k == i);
            River.Active = stageRoots[i] && stageRoots[i].name == "Stage_river"; River.Valley = stageRoots[i] && stageRoots[i].name == "Stage_valley"; River.Wilds = stageRoots[i] && stageRoots[i].name == "Stage_wilds"; River.Sky = stageRoots[i] && stageRoots[i].name == "Stage_skylanes";
            var look = stageRoots[i] ? stageRoots[i].GetComponent<StageLook>() : null; stageLook = look;
            if (look) look.Apply();
            StageRender.For(stageRoots[i] ? stageRoots[i].name : null);   // a stage's own AO and shadow reach, put back for the others
            Obstacles.Use(stageRoots[i]);
        }

        public void ShowLobby()
        {
            cameFromCity = false;
            ClearWorld(); mode = Mode.Lobby; paused = false; Time.timeScale = 1; lane = false; soul = false; if (hero && hero.soul) hero.soul.End();
            stage = Mathf.Clamp(save.stage, 1, MaxStage);
            SetTheme(Data.ThemeOf(1)); LobbyNight.Set(true);   // lobby backdrop: the forest at night, whatever stage is picked
            hero.Equip(); pet.Equip();
            hero.PlaceLobby(); pet.PlaceLobby();
            ui.ShowLobby();
        }

        public void StartGame(int st, bool carry = false)
        {
            Crumb.Mark($"stage {st}: pressed"); ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = false; soul = false; ClearPreview();
            stage = st; save.stage = st; Persist(); SetTheme(Data.ThemeOf(st)); Crumb.Mark($"stage {st}: scene on");
            if (!carry) { kills = 0; gold = 0; time = 0; banked = 0; }
            between = 0; wave = 0;
            hero.Equip(); Crumb.Mark($"stage {st}: hero ready"); pet.Equip(); hero.ResetForGame(); pet.ResetForGame(); Crumb.Mark($"stage {st}: pet ready"); StartCoroutine(StageCrumbs(st)); PreloadStage();
            camPos = new Vector3(0, 9, 14);
            if (River.Active) StartCoroutine(After(.1f, () => ui.Banner(Loc.S("ลุยน้ำได้ แต่จะช้าลง", "You can wade, but water slows you"), Loc.S("ข้ามสะพานไม้เพื่อความเร็ว", "Cross the bridges to keep your speed"))));
            ui.ShowHud();
            StartWave(0);
        }

        System.Collections.IEnumerator StageCrumbs(int st)
        {
            yield return null; Crumb.Mark($"stage {st}: frame 1"); yield return null; Crumb.Mark($"stage {st}: frame 2");
            foreach (var s in new[] { 1, 3, 6, 10 }) { yield return new WaitForSecondsRealtime(s == 1 ? 1 : s == 3 ? 2 : s == 6 ? 3 : 4); if (mode != Mode.Game) yield break; Crumb.Mark($"stage {st}: alive {s}s"); }
        }
        // what this fight will use, loaded on the way in: otherwise the first swing, skill and each new monster each stall a frame while they load
        readonly List<Object> preloaded = new();
        void PreloadStage()
        {
            preloaded.Clear(); var ids = new List<string> { "sx_basic", "sx_double", "sx_explode", "sx_multi", "sx_shiny", "sx_mesh", "mx_spark_y", "mx_spark_w" };
            ids.AddRange(hero.def.cls switch { "cleric" => new[] { "mx_heal_circle", "mx_holy_hit" }, "mage" => new[] { "mx_snow", "mx_meteors", "mx_charge" }, _ => new string[0] });
            foreach (var id in ids) if (Catalog.I.Fx(id) is GameObject fx) preloaded.Add(fx);
            foreach (var wave in Theme.waves) foreach (var (foe, _) in wave)
            {
                var model = Data.Foe(foe)?.model; if (string.IsNullOrEmpty(model)) continue;
                var pf = model.StartsWith("rig_") ? Resources.Load<GameObject>("Heroes/Hero_" + model.Substring(4)) : model.StartsWith("hero_") || model.StartsWith("@") ? null : Catalog.I.Model(model);
                if (pf) preloaded.Add(pf);
            }
            Crumb.Mark($"stage {stage}: preloaded {preloaded.Count}");
        }
        void StartWave(int k)
        {
            wave = k; queue.Clear();
            var list = new List<string>();
            foreach (var (f, n) in Theme.waves[k]) for (int i = 0; i < n + (stage - 1) / 2; i++) list.Add(f);
            waveTotal = list.Count;
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
            var src = def; bool scale = lane && def.kind == "champ" && !duelOnly; if (scale) def = def.Copy();   // the difficulty goes on a copy: a respawn starts again from the base (it used to compound)
            if (crumbed.Add(def.id)) Crumb.Mark("spawn " + def.id);   // once per kind: a log line and a localStorage write for every soldier made each wave stutter
            var f = new GameObject($"Unit_{def.id}_{team}").AddComponent<Foe>(); f.Init(def, p, null, team); foes.Add(f); f.baseDef = src;
            if (scale) { var k = AiLevels[Mathf.Clamp(save.aiLevel, 0, 2)]; f.maxHp *= k.hp; f.hp = f.maxHp; f.d.dmg *= k.dmg; }   // AI heroes sized to the chosen difficulty
            if (lane && def.kind == "champ") Stat(def.name, team);
            if (lane && def.kind == "champ") GrowAi(f);   // a hero coming back joins at its side's level
            if (lane && Lane.Soldier(def) && Jungle.Blessed(team)) { f.d = f.d.Copy(); f.d.dmg *= Jungle.BossSoldierK; f.maxHp *= Jungle.BossSoldierK; f.hp = f.maxHp; }   // the Guardian's Blessing
            return f;
        }
        void SpawnBoss()
        {
            var f = new GameObject("Boss").AddComponent<Foe>(); f.Init(null, new Vector3(0, 0, -14), Theme.boss); foes.Add(f); boss = f;
            ui.ShowBoss(Loc.S($"{Theme.boss.name} · ด่าน {stage}", $"{Theme.boss.name} · Stage {stage}")); shake = .4f;
        }

        public void ClearWorld()
        {
            StartCoroutine(After(.3f, () => Resources.UnloadUnusedAssets()));   // what the last place used goes back, once its objects are gone
            foreach (var f in foes) if (f) Destroy(f.gameObject); foes.Clear(); boss = null;
            foreach (var s in shots) if (s) Destroy(s.gameObject); shots.Clear();
            foreach (var d in drops) { if (d.halo) Destroy(d.halo); if (d.go) Destroy(d.go); } drops.Clear();
            queue.Clear(); Fx.Clear(); ui?.ClearNums();
        }

        public void Finish(bool win)
        {
            if (mode != Mode.Game) return;
            mode = Mode.Result; save.gold += gold - banked; banked = gold; if (hero.soul) hero.soul.End();
            if (win) { save.best = Mathf.Max(save.best, stage); QuestBump("clear"); }
            if (lane) Chron(win ? "victory" : "defeat", win ? 0 : 1);
            if (lane) { int was = save.rankPts; rankWas = was; save.rankPts = Mathf.Max(0, save.rankPts + (win ? 25 + (save.aiLevel - 1) * 10 : -15)); var (rn, _, _) = Ui.RankOf(save.rankPts); ui.Banner(win ? Loc.S("ชนะ!", "Victory!") : Loc.S("แพ้", "Defeat"), Loc.S($"แต้มอันดับ {save.rankPts - was:+0;-0} · {rn}", $"Rank points {save.rankPts - was:+0;-0} · {rn}")); }
            Persist();
            ui.ShowResult(win);
        }

        // ---------- combat helpers ----------
        public IEnumerable<Foe> Live => foes.Where(f => f && f.alive && !f.spawning && (f.team == 1 || f.team == Jungle.Neutral));   // what the player and pet can hit
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

        public class Hit { public float face, half = 9, kb, poise; public bool melee, byFoe, skill; public float slow; public Color? col; public HashSet<Foe> skip; public float steal; }
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
        class Drop { public GameObject go; public string orb; public Material halo; public int v; public Vector3 p, vel; public float t; public string item; }
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
        public int waveTotal;   // how many foes the current wave sent (the stage plate shows left / total)
        // a phone or tablet: the mobile platforms, plus any browser on the phone build at the site root (an iPad reports itself as a Mac; PC browsers are on /pc/, ?phone is the desktop test)
        public static bool IsPhone => Application.isMobilePlatform || (Application.platform == RuntimePlatform.WebGLPlayer && !Application.absoluteURL.Contains("/pc/") && !Application.absoluteURL.Contains("phone"));
        public int runXp;   // EXP earned since this fight began (the result screen shows it)
        // counts towards quests and says so when one is finished
        public void QuestBump(string kind, int n = 1)
        {
            var open = Quests.All.Where(q => q.kind == kind && !Quests.Done(save, q)).ToList(); Quests.Bump(save, kind, n);
            if (hero) foreach (var q in open) if (Quests.Done(save, q)) PickupText(Loc.S("เควสต์สำเร็จ: ", "Quest done: ") + q.name, "crit");
        }
        // lane battles: the score (hero kills per side), the player's kills / deaths / assists, and the calls on screen when a hero falls
        public readonly int[] teamKills = new int[2]; public int myK, myD, myA; int mySpree, myMulti; float myLastKill = -99;
        const float KillReach = 12, MultiWindow = 10;
        void ResetScore() { chron.Clear(); sideTowers[0] = sideTowers[1] = sideCamps[0] = sideCamps[1] = sideBosses[0] = sideBosses[1] = 0; bestMulti = bestSpree = 0; myFirstBlood = false; teamKills[0] = teamKills[1] = 0; teamXp[0] = teamXp[1] = 0; myK = myD = myA = mySpree = myMulti = 0; myLastKill = -99; stats.Clear(); respawnAt = 0; if (hero && hero.def != null) Stat(hero.def.name, 0, true); }
        // the scoreboard: every hero in the match, its kills, deaths and assists (the player's row marked)
        public class HeroStat { public string name; public int team, k, d, a, towers, camps, bosses; public float dmg, taken, heal; public bool me; public float Score => k * 3 + a * 1.5f - d + dmg / 1500f + towers + bosses * 2; }
        // the end screen's numbers (owner, 2026-11-12): damage by hero, structures, camps and the Guardian per side, the player's feats
        public readonly int[] sideTowers = new int[2], sideCamps = new int[2], sideBosses = new int[2]; public int bestMulti, bestSpree, rankWas; public bool myFirstBlood;
        // the Battle Chronicle: the match's big moments in order (first blood, the player's multi-kills, towers, the Guardian, the end)
        public readonly List<(float t, string kind, int side, string who)> chron = new();
        public void Chron(string kind, int side, string who = "") { if (lane) chron.Add((time, kind, side, who)); }
        public void CreditAi(Foe f, float dmg) { if (lane && f && f.d.kind == "champ" && dmg > 0) Stat(f.d.name, f.team).dmg += dmg; }
        public void CreditMe(float dmg) { if (lane && hero && dmg > 0) Stat(hero.def.name, 0, true).dmg += dmg; }
        public readonly List<HeroStat> stats = new(); public float respawnAt;
        // Sky Lanes: each side's XP this match, from the enemy units that die (the AI heroes' level: they don't keep a level of their own)
        public readonly int[] teamXp = new int[2];
        public void TeamXp(int side, int n) { if (!SkyMatch || side is not (0 or 1)) return; teamXp[side] += n; foreach (var f in foes) if (f && f.alive && f.team == side && f.d.kind == "champ") GrowAi(f); }
        // an AI hero grows with its side's level the way the player grows with theirs: +4% health and +3% damage (skills too) a level
        public const float LaneLevelHeal = .1f;   // owner, 2026-11-10: a level-up in a lane battle no longer fills the health bar
        public void GrowAi(Foe f)
        {
            if (!SkyMatch || !f || f.d.kind != "champ") return; int lv = Data.Level(teamXp[f.team]); if (lv == f.aiLv) return;
            float hk = (1 + (lv - 1) * Data.LevelHp) / (1 + (f.aiLv - 1) * Data.LevelHp), dk = (1 + (lv - 1) * Data.LevelAtk) / (1 + (f.aiLv - 1) * Data.LevelAtk);
            f.maxHp *= hk; f.hp *= hk; f.d.dmg *= dk; bool up = lv > f.aiLv; f.aiLv = lv; Perks.AiCatchUp(f, lv);
            if (up && f.alive) { f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * LaneLevelHeal); Fx.Burst(f.Pos + Vector3.up, 18, new Color(1, .85f, .35f), 3, .6f, 1, .12f, true); }   // a gold flash: level up, and the same tenth of health the player gets
        }
        public int LevelOf(HeroStat s) => s.me && hero ? hero.Level : SkyMatch ? Data.Level(teamXp[s.team]) : 0;   // 0: no match level outside Sky Lanes
        public HeroStat Stat(string name, int team, bool me = false) { var s = stats.Find(x => x.name == name && x.team == team); if (s == null) stats.Add(s = new HeroStat { name = name, team = team, me = me }); return s; }
        // a hero is down (the player or an AI champion): the nearest living enemy hero takes the kill, the player nearby on that side an assist;
        // with no enemy hero near, a tower or soldiers did it (still a point for that side)
        public void HeroKilled(Vector3 at, int victimTeam, string victimName, bool victimIsPlayer)
        {
            if (!lane || mode != Mode.Game) return;
            int side = 1 - victimTeam; string killer = null; bool playerKilled = false; float best = KillReach;
            if (side == 0 && hero && !hero.dead) { float d = Flat(hero.Pos - at).magnitude; if (d < best) { best = d; killer = hero.def.name; playerKilled = true; } }
            foreach (var f in foes) if (f && f.alive && f.team == side && f.d.kind == "champ") { float d = Flat(f.Pos - at).magnitude; if (d < best) { best = d; killer = f.d.name; playerKilled = false; } }
            teamKills[side]++; bool first = teamKills[0] + teamKills[1] == 1;
            Stat(victimName, victimTeam, victimIsPlayer).d++; if (killer != null) Stat(killer, side, playerKilled).k++;
            foreach (var f in foes) if (f && f.alive && f.team == side && f.d.kind == "champ" && f.d.name != killer && Flat(f.Pos - at).magnitude < KillReach) Stat(f.d.name, side).a++;   // assists: the killer's teammates close by
            if (victimIsPlayer) { myD++; mySpree = 0; myMulti = 0; TeamXp(side, 40); }   // the player is worth a hero's XP to the other side
            if (playerKilled || side == 0 && hero && !hero.dead && Flat(hero.Pos - at).magnitude < KillReach) hero.KitTakedown();
            if (playerKilled) { myK++; mySpree++; myMulti = time - myLastKill < MultiWindow ? myMulti + 1 : 1; myLastKill = time; bestMulti = Mathf.Max(bestMulti, myMulti); bestSpree = Mathf.Max(bestSpree, mySpree); if (first) myFirstBlood = true; if (myMulti == 2) Chron("double", 0, hero.def.name); else if (myMulti >= 3) Chron("triple", 0, hero.def.name); }
            if (first) Chron("firstblood", side, killer ?? "");
            else if (side == 0 && hero && !hero.dead && Flat(hero.Pos - at).magnitude < KillReach) { myA++; Stat(hero.def.name, 0, true).a++; }
            string head = first ? Loc.S("เลือดแรก!", "FIRST BLOOD!")
                : playerKilled && myMulti >= 3 ? Loc.S("ทริปเปิลคิล!", "TRIPLE KILL!") : playerKilled && myMulti == 2 ? Loc.S("ดับเบิลคิล!", "DOUBLE KILL!")
                : playerKilled && mySpree >= 5 ? Loc.S("ไม่มีใครหยุดได้!", "UNSTOPPABLE!") : playerKilled && mySpree >= 3 ? Loc.S("กำลังเดือด!", "KILLING SPREE!")
                : killer == null ? Loc.S("ถูกประหาร!", "EXECUTED!") : victimIsPlayer ? Loc.S("คุณถูกสังหาร!", "YOU HAVE BEEN SLAIN!") : Loc.S("สังหาร!", "SLAIN!");
            static string Blue(string n) => $"<color=#7fcfff>{n}</color>"; static string Red(string n) => $"<color=#ff7a6e>{n}</color>";   // names in their side's colour
            string vic = victimTeam == 0 ? Blue(victimName) : Red(victimName), kil = killer == null ? null : side == 0 ? Blue(killer) : Red(killer);
            ui.KillCall(head, kil, vic, Color.white);   // "killer [swords] victim", the swords drawn by the HUD
        }
        public int matchXp;   // Sky Lanes: XP of this match only, the hero's level in it (starts at level 1)
        public bool SkyMatch => lane && Lane.Lanes == 2;
        // the player's level-up pick: offered when the match level is due one (3, 6, 9) and no panel is up
        public void PerkCheck() { if (!SkyMatch || !hero || ui.PerkPending || Perks.Count(Perks.Me) >= Perks.Due(hero.Level)) return; var o = Perks.Offer(Perks.Me); if (o.Length > 0) ui.ShowPerks(o, Perks.At[Mathf.Min(Perks.Count(Perks.Me), Perks.At.Length - 1)]); }
        public void AddXp(int n)
        {
            if (n <= 0 || !hero) return; int before = hero.Level; save.AddHeroXp(hero.def.id, n); runXp += n; if (SkyMatch) matchXp += n; if (hero.Level <= before) return;
            if (!SkyMatch) foreach (var q in Quests.All) if (q.kind == "level" && q.goal == hero.Level && !Quests.Claimed(save, q)) PickupText(Loc.S("เควสต์สำเร็จ: ", "Quest done: ") + q.name, "crit");
            if (SkyMatch) PerkCheck();
            hero.RefreshGear(); hero.hp = lane ? Mathf.Min(hero.max, hero.hp + hero.max * LaneLevelHeal) : hero.max;   // lane battles (MOBA): the health share is kept, plus a tenth; elsewhere a full heal
            Fx.Ring(hero.Pos, 3.5f, new Color(1, .85f, .4f), .7f, .2f); Fx.Burst(hero.Pos + Vector3.up, 40, new Color(1, .85f, .45f), 5);
            ui.Banner(Loc.S($"เลเวล {hero.Level}!", $"LEVEL {hero.Level}!"), Loc.S("พลังชีวิตและพลังโจมตีเพิ่มขึ้น", "More health and attack")); Persist();
        }
        // leaving a stage early keeps the gold picked up so far
        public void BankGold() { save.gold += gold - banked; banked = gold; Persist(); }
        // pickup words stack upward instead of landing on each other; gold adds up into one number
        float pickT, goldShowT; int pickN, goldPend;
        void PickupText(string t, string cls) { if (Time.time - pickT > .7f) pickN = 0; pickT = Time.time; Number(hero.Pos + Vector3.up * (2.4f + pickN++ * .55f), t, cls); }
        public readonly List<string> runLoot = new();   // gear picked up since this fight began (the pause bag shows it first)
        public bool AddItem(string id)
        {
            var d = Items.Get(id); if (d == null) return false;
            if (d.slot == Slot.Potion) save.potions++;
            else if (d.slot == Slot.Material) save.mats[d.rarity]++;
            else if (save.BagUsed >= save.bagCap) { gold += d.Sell; Number(hero.Pos + Vector3.up * 2.2f, Loc.S("กระเป๋าเต็ม +" + d.Sell, "Bag full +" + d.Sell), "gold"); return false; }
            else { save.bag.Add(id); runLoot.Add(id); }
            return true;
        }
        // glass orbs instead of coins: gold, heal (green), mana (purple), fury (red: harder hits for a while), quick (white: skills come back sooner)
        static Mesh orbQuad; static readonly Dictionary<string, Material> orbMats = new();
        static Material OrbMat(string kind)
        {
            if (orbMats.TryGetValue(kind, out var m) && m) return m;
            m = new Material(Catalog.I.glowMat) { mainTexture = Resources.Load<Texture2D>("UI/Orbs/orb_" + OrbFile(kind)) }; m.SetFloat("_SrcBlend", 5); m.SetFloat("_DstBlend", 10); m.SetFloat("_UseVC", 0); m.SetColor("_Color", Color.white); m.renderQueue = 3000;
            return orbMats[kind] = m;
        }
        static string OrbFile(string kind) => kind switch { "heal" => "green", "mana" => "purple", "fury" => "red", "quick" => "white", _ => "gold" };
        static Color OrbGlow(string kind) => kind switch { "heal" => new Color(.3f, 1, .4f), "mana" => new Color(.75f, .35f, 1), "fury" => new Color(1, .3f, .2f), "quick" => new Color(.8f, .88f, 1), _ => new Color(1, .75f, .25f) };
        public void DropLoot(Vector3 p, bool isBoss, int g)
        {
            if (lane) { gold += g; goldPend += g; return; }   // lane battles: no orbs (heal, power, fury, quick) and no items, skill decides; gold goes straight in
            if (ZoneNight) g = Mathf.CeilToInt(g * OpenWorld.NightGold);
            if (isBoss) { for (int i = 0; i < 24; i++) AddDrop(p, "gold", 8); foreach (var k in new[] { "heal", "mana", "fury", "quick" }) AddDrop(p, k, 0); }
            else
            {   // one gold orb worth what the coins came to; the others by chance
                AddDrop(p, "gold", g);
                if (Random.value < .12f) AddDrop(p, "heal", 0); if (Random.value < .1f) AddDrop(p, "mana", 0); if (Random.value < .05f) AddDrop(p, "fury", 0); if (Random.value < .05f) AddDrop(p, "quick", 0);
            }
            // items: one monster in eight drops something, a boss always drops two pieces and a potion
            if (isBoss) { AddItemDrop(p, Items.Roll(true, save.stage)); AddItemDrop(p, Items.Roll(true, save.stage)); AddItemDrop(p, "potion"); }
            else if (Random.value < .13f) AddItemDrop(p, Items.Roll(false, save.stage));
        }
        void AddDrop(Vector3 p, string kind, int v)
        {
            if (!orbQuad) { var q = GameObject.CreatePrimitive(PrimitiveType.Quad); orbQuad = q.GetComponent<MeshFilter>().sharedMesh; Destroy(q); }
            var go = new GameObject("orb_" + kind); go.AddComponent<MeshFilter>().sharedMesh = orbQuad; go.AddComponent<MeshRenderer>().sharedMaterial = OrbMat(kind); go.transform.localScale = Vector3.one * (kind == "gold" ? .62f : .72f);
            var halo = new GameObject("halo"); halo.transform.SetParent(go.transform, false); halo.transform.localPosition = new Vector3(0, 0, .02f); halo.transform.localScale = Vector3.one * 2.1f; halo.AddComponent<MeshFilter>().sharedMesh = orbQuad;
            var hm = new Material(Catalog.I.glowMat) { mainTexture = Fx.SoftTex }; hm.SetFloat("_SrcBlend", 1); hm.SetFloat("_DstBlend", 1); hm.SetFloat("_UseVC", 0); hm.SetColor("_Color", OrbGlow(kind) * .3f); hm.renderQueue = 2990; halo.AddComponent<MeshRenderer>().sharedMaterial = hm;
            drops.Add(new Drop { go = go, orb = kind, halo = hm, v = v, p = p + Vector3.up * .6f, vel = new Vector3(Random.Range(-2.5f, 2.5f), Random.Range(3f, 6f), Random.Range(-2.5f, 2.5f)) });
        }
        static Mesh sphere; static Mesh SphereMesh() { if (!sphere) { var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere = s.GetComponent<MeshFilter>().sharedMesh; Destroy(s); } return sphere; }

        void UpdDrops(float dt)
        {
            float mag = pet.def != null && pet.def.gold ? 8 : 4;
            if (goldPend > 0 && Time.time - goldShowT > .3f) { Number(hero.Pos + Vector3.up * 1.8f, "+" + goldPend, "gold"); goldPend = 0; goldShowT = Time.time; }
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
                        if (AddItem(d.item)) { PickupText("+ " + it.name, "crit"); Fx.Burst(hero.Pos + Vector3.up, 20, c, 4); Persist(); }
                    }
                    else
                    {
                        Fx.Burst(hero.Pos + Vector3.up, 14, OrbGlow(d.orb), 3); QuestBump("orb");
                        switch (d.orb)
                        {
                            case "heal": hero.Heal(hero.max * .1f); break;
                            case "mana": hero.mp = Mathf.Min(hero.mpMax, hero.mp + 30); PickupText("+30 MP", "heal"); break;
                            case "fury": hero.orbAtkT = 10; PickupText(Loc.S("พลังโจมตี +25%", "ATK +25%"), "crit"); break;
                            case "quick": for (int c = 0; c < 3; c++) hero.cds[c] = Mathf.Max(0, hero.cds[c] - 3); PickupText(Loc.S("สกิลเร็วขึ้น -3 วิ", "Skills -3s"), "crit"); break;
                            default: int v = Mathf.RoundToInt(d.v * (pet.def != null && pet.def.gold ? 1.5f : 1)); gold += v; goldPend += v; break;
                        }
                    }
                }
                d.go.transform.position = d.p + (d.orb != null ? Vector3.up * (.28f + Mathf.Sin(d.t * 3) * .07f) : Vector3.zero);
                d.go.transform.rotation = d.item != null ? Quaternion.Euler(0, d.t * 120, 0) : cam.transform.rotation;   // orbs always face the camera
                if (d.halo != null) d.halo.SetColor("_Color", OrbGlow(d.orb) * (.26f + .08f * Mathf.Sin(d.t * 5)));
                if (dead) { if (d.halo) Destroy(d.halo); Destroy(d.go); drops.RemoveAt(i); }
            }
        }

        // ---------- loop ----------
        bool hitchLog;
        void Update()
        {
            if (hitchLog && Time.unscaledDeltaTime > .05f) Debug.Log($"[Hitch] slow frame at {Time.realtimeSinceStartup:0.0}s: {Time.unscaledDeltaTime * 1000:0} ms");
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1 / 25f);
            Shader.SetGlobalVector("_CozyWind", new Vector4(Time.time, 1, 0, 0));
            Shader.SetGlobalFloat("_CozyDim", 1); Shader.SetGlobalFloat("_CozyNight", 0);   // CozyFishing/Water globals
            if (paused || mode == Mode.Load) return;
            // hit stop: a few frames of near-freeze on heavy hits (animators and particles slow down too)
            if (hitStop > 0) hitStop -= dt;
            Time.timeScale = (hitStop > 0 ? .06f : 1) * TestSpeed;
            if (skyPetals && lane && Lane.Lanes == 2 && hero) skyPetals.position = hero.Pos + new Vector3(0, 6, -7);
            if (mode == Mode.Game) { float gdt = dt * TestSpeed; time += gdt; if (lane) UpdLane(gdt); else if (!city && !wilds && !house && !sky) UpdWaves(gdt); UpdDrops(dt); }   // the match clock and waves follow a test's speed-up too
            for (int i = foes.Count - 1; i >= 0; i--) if (!foes[i]) foes.RemoveAt(i);
        }

        void LateUpdate()
        {
            if (!cam) return;
            float dt = Time.unscaledDeltaTime;
            if (mode == Mode.Lobby)
            {   // the lobby panel covers screen-right, which is world -X
                // bag open: the hero steps right, between the worn-slot arc and the panel, seen whole
                float side = cam.aspect > 1.25f ? (ui.BagOpen ? -.6f : -.95f) : 0; bool bot = hero.def != null && !hero.def.rig; float z = ui.BagOpen ? 0 : ui.lobbyZoom; Vector3 look, want;
                if (ui.FocusPet && pet && pet.def != null)
                {   // pet tab: frame the pet, zoom toward it
                    float d = Mathf.Lerp(2.6f, 1.05f, z); var pc = pet.transform.position + Vector3.up * pet.def.h * .5f;
                    look = pc + new Vector3(side * d / 4.1f, 0, 0); want = look + new Vector3(0, .3f * d / 2.6f + .15f, d);
                }
                else
                {   // zoom moves in toward the chest/face; the side offset scales with distance so the hero keeps its place left of the panel
                    bool winged = WingsShown != null;   // wings reach above the head and out to the sides: frame the whole spread
                    float d = Mathf.Lerp(bot ? 5 : 3.5f, 1.6f, z) * (winged ? Mathf.Lerp(1.22f, 1.05f, z) : 1);
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
                int style = lane ? 0 : ViewStyle == 3 ? 3 : stageLook && stageLook.lowCam ? 2 : ViewStyle;
                var off = style == 3 ? new Vector3(0, 2.6f, 5.6f) : style == 2 ? new Vector3(0, 4.1f, 9.6f) : style == 1 ? new Vector3(0, 6.2f, 8.8f) : new Vector3(0, 8.6f, 7.4f);
                if (lane && Lane.Lanes == 2) { off = SkyCam; far /= 1.35f; }   // Sky Lanes: a MOBA view, steep (about 58 degrees) and further out, so lanes, jungle and the fight around read at once
                var focus = ui.Pan ?? hero.Pos; var want = focus + off * far;
                camPos = Vector3.Lerp(camPos, want, 1 - Mathf.Exp(-dt * (hero.dead ? 2.2f : 6)));
                camLook = new Vector3(camPos.x, (wilds || River.Sky ? camPos.y - off.y * far : 0) + (hero.dead ? .5f : lane ? 1.6f : style == 3 ? 1.5f : style == 2 ? 1.3f : style == 1 ? 1.1f : .9f), camPos.z - off.z * far - (style == 3 ? 1.6f : 0));   // on the zone's hills the view climbs with the hero
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, soul && mode != Mode.Lobby ? 55 : 40, 1 - Mathf.Exp(-dt * 6));
            shake = Mathf.Max(0, shake - dt);
            cam.transform.position = camPos + Random.insideUnitSphere * shake * .175f;
            cam.transform.LookAt(camLook);
        }

        // ======================= teams & lane battle =======================
        // camps: include the jungle's monsters (team 2), left out by default so soldiers, towers and heroes in lane pass them by;
        // the monsters themselves (team 2) fight only heroes
        public IEnumerable<ITarget> Opponents(int team, bool camps = false)
        {
            if (team != 0 && hero && !hero.dead && !hero.Invisible && !duelOnly) yield return hero;
            foreach (var f in foes)
            {
                if (!f || !f.alive || f.spawning || f.team == team) continue;
                if (f.team == Jungle.Neutral && !camps) continue;
                if (team == Jungle.Neutral && f.d.kind != "champ") continue;
                yield return f;
            }
        }
        public ITarget PickTarget(Foe f)
        {
            if (!lane) return hero && !hero.dead ? hero : null;
            if (f.d.kind == "champ") return f.ChampTarget();
            float aggro = f.d.kind == "tower" ? f.d.range : 10; ITarget best = null; float bd = aggro;
            if (f.d.kind == "tower")
            {   // a tower shoots the soldiers in its range first, and a hero only when no soldier is there to take the shots
                foreach (var o in Opponents(f.team)) { if (o is not Foe m || !Lane.Soldier(m.d)) continue; float d = Flat(o.Pos - f.Pos).magnitude - o.Rad; if (d < bd) { bd = d; best = o; } }
                if (best != null) return best;
            }
            foreach (var o in Opponents(f.team)) { if (o is Foe sf && sf.Shielded) continue; float d = Flat(o.Pos - f.Pos).magnitude - o.Rad; if (d < bd) { bd = d; best = o; } }   // a shielded tower is not worth a swing
            return best;
        }
        public void HitTeamArea(int team, Vector3 c, float r, float dmg)
        {
            foreach (var o in Opponents(team, true).ToList()) if (Flat(o.Pos - c).magnitude < r + o.Rad) o.Take(dmg, c);
        }
        public Vector3 Clamp(Vector3 p, float margin = 0)
        {
            if (lane)
            {
                if (Lane.Lanes == 2) p = SkyLanes.Walk(p, margin);   // the floating map: only the lanes, the jungle and the bases are ground
                else { p.x = Mathf.Clamp(p.x, -Lane.HalfW - margin, Lane.HalfW + margin); p.z = Mathf.Clamp(p.z, -Lane.HalfL - margin, Lane.HalfL + margin); }
                foreach (var tw in foes)
                {   // towers are solid: nobody walks through them (destroyed ones too, until they have sunk)
                    if (!tw || tw.d.kind != "tower") continue; var d = Flat(p - tw.Pos); float rr = tw.rad + .45f;
                    if (d.sqrMagnitude < rr * rr) { d = d.sqrMagnitude < 1e-4f ? Vector3.forward : d.normalized; p.x = tw.Pos.x + d.x * rr; p.z = tw.Pos.z + d.z * rr; }
                }
                return Obstacles.Push(p);
            }
            if (house || sky) { float hw = (sky ? OpenWorld.SkyHalf : OpenWorld.RoomHalf) + margin; p.x = Mathf.Clamp(p.x, -hw, hw); p.z = Mathf.Clamp(p.z, -hw, hw); return Obstacles.Push(p); }
            var f = Flat(p); float r = f.magnitude, R = ArenaRadius + margin; if (r > R) { f *= R / r; p.x = f.x; p.z = f.z; }
            return Obstacles.Push(p);
        }


        public int laneCount = 1;
        // lanes: 1 or 3 on the ground maps, 2 = Sky Lanes (the floating 3v3 map, SKY-LANES-3V3.md)
        public void StartLane(int size, int lanes = 1)
        {
            if (lanes == 2) { StartSkyLanes(); return; }
            ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = true; soul = false; ClearPreview(); laneSize = size; laneCount = lanes; Lane.Lanes = lanes;
            kills = 0; gold = 0; time = 0; banked = 0; minionT = 3; wave = 0; ResetScore();
            SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == (lanes == 3 ? "Stage_lane3" : "Stage_lane")));   // by name: the zone, house and Sky Court come after the lane maps (the phone build leaves some out)
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame();
            for (int t = 0; t < 2; t++)
            {
                float side = Lane.Side(t);
                for (int l = 0; l < lanes; l++) foreach (var z in Lane.TowerZ) { var u = SpawnUnit(Lane.Tower, new Vector3(Lane.LaneX(lanes == 3 ? l : 1), 0, side * z), t); if (u) { u.laneIdx = l; u.towerTier = z == Lane.TowerZ[0] ? 0 : 1; } }
                SpawnUnit(Lane.Core, new Vector3(0, 0, side * Lane.CoreZ), t).towerTier = 2;
            }
            // AI heroes: in three lanes our two helpers take the side lanes (the player usually goes middle), the enemy has one per lane
            int[] ourLanes = lanes == 3 ? new[] { 0, 2 } : new[] { 1, 1 }, theirLanes = lanes == 3 ? new[] { 0, 1, 2 } : new[] { 1, 1, 1 };
            var others = TakeAllies() is { Count: > 0 } pa ? pa : Data.Heroes.Where(h => h.rig && h.id != hero.def.id).OrderBy(_ => Random.value).ToList();   // the VS screen's team when there was one
            for (int i = 0; i < size - 1; i++) { var u = SpawnUnit(Lane.Champ(others[i % others.Count]), new Vector3(lanes == 3 ? Lane.LaneX(ourLanes[i]) * .4f : (i == 0 ? -3 : 3), 0, Lane.SpawnZ), 0); if (u) u.laneIdx = ourLanes[i % 2]; }
            var foesH = TakeEnemies() is { Count: > 0 } pe ? pe : Data.Heroes.Where(h => h.rig).OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < size; i++) { var u = SpawnUnit(Lane.Champ(foesH[i % foesH.Count]), new Vector3(lanes == 3 ? Lane.LaneX(theirLanes[i % 3]) * .4f : (i - 1) * 3, 0, -(Lane.SpawnZ)), 1); if (u) u.laneIdx = theirLanes[i % 3]; }
            camPos = new Vector3(0, 9, Lane.CoreZ + 10);
            ui.ShowHud(); ui.Banner(lanes == 3 ? Loc.S($"สนามรบ 3 เลน · {size} ต่อ {size}", $"3-Lane Battle · {size} vs {size}") : Loc.S($"สนามรบ {size} ต่อ {size}", $"Lane Battle · {size} vs {size}"), lanes == 3 ? Loc.S("ทำลายป้อมในแต่ละเลน แล้วทุบคริสตัลหลักของศัตรู", "Destroy the towers in each lane, then smash the enemy Core Crystal") : Loc.S("ทำลายคริสตัลหลักของฝ่ายตรงข้าม", "Destroy the enemy Core Crystal"));
        }
        // Sky Lanes, always 3 against 3: towers along both lanes, the player and one helper start on the straight lane, the other helper takes the curve; the enemy mirrors it
        public static readonly int[] SkyLaneOf = { 0, 1 };   // our two helpers; the enemy's three: 0, 1, 0
        Transform skyPetals; bool skyRunes, skyPhone;   // sakura petals over Sky Lanes, kept round the hero
        static readonly Vector3 SkyCam = new(0, 13.5f, 8.4f);
        void StartSkyLanes()
        {
            const int size = 3; ClearWorld(); mode = Mode.Game; paused = false; Time.timeScale = 1; lane = true; soul = false; ClearPreview(); laneSize = size; laneCount = 2; Lane.Lanes = 2;
            kills = 0; gold = 0; time = 0; banked = 0; minionT = 3; wave = 0; matchXp = 0; ResetScore(); brutesOn[0] = brutesOn[1] = false;
            SetTheme(System.Array.FindIndex(stageRoots, r => r && r.name == "Stage_skylanes")); SkyTime.Begin(Stage("Stage_skylanes"), 0);
            hero.Equip(); pet.Equip(); hero.ResetForGame(); pet.ResetForGame();
            for (int t = 0; t < 2; t++)
            {
                for (int l = 0; l < 2; l++) for (int k = 0; k < SkyLanes.Towers; k++) { var u = SpawnUnit(Lane.Tower, SkyLanes.Tower(t, l, k), t); if (u) { u.laneIdx = l; u.towerTier = k; } }
                SpawnUnit(Lane.Core, new Vector3(0, 0, Lane.Side(t) * Lane.CoreZ), t).towerTier = SkyLanes.Towers;
            }
            Jungle.Begin();   // the jungle camps (stage B)
            Perks.Begin(); ui.HidePerks();   // level-up picks
            var others = TakeAllies() is { Count: > 0 } pa ? pa : Data.Heroes.Where(h => h.rig && h.id != hero.def.id).OrderBy(_ => Random.value).ToList();   // the VS screen's team when there was one
            for (int i = 0; i < size - 1; i++) { var u = SpawnUnit(Lane.Champ(others[i % others.Count]), Lane.SpawnOf(0) + new Vector3(i == 0 ? -1.5f : 1.5f, 0, 0), 0); if (u) u.laneIdx = SkyLaneOf[i]; }
            var foesH = TakeEnemies() is { Count: > 0 } pe ? pe : Data.Heroes.Where(h => h.rig).OrderBy(_ => Random.value).ToList();
            for (int i = 0; i < size; i++) { var u = SpawnUnit(Lane.Champ(foesH[i % foesH.Count]), Lane.SpawnOf(1) + new Vector3((i - 1) * 1.6f, 0, 0), 1); if (u) u.laneIdx = i == 1 ? 1 : 0; }
            camPos = Lane.SpawnOf(0) + new Vector3(0, 13, 8);
            var stageRoot = Stage("Stage_skylanes"); if (stageRoot && !skyPetals) skyPetals = LobbyNight.PetalsAt(stageRoot);
            if (stageRoot && IsPhone && !skyPhone)
            {   // phones: the same map, lighter: no 3D grass, no brazier and rim lights (the flames stay)
                skyPhone = true;
                foreach (var g in stageRoot.GetComponentsInChildren<GrassField>(true)) g.gameObject.SetActive(false);
                foreach (var l in stageRoot.GetComponentsInChildren<Light>(true)) if (l.type == LightType.Point && l.GetComponent<FireFlicker>() || l.name == "RimLight") l.enabled = false;
            }
            if (stageRoot && !skyRunes)
            {   // glowing rune rings round each spawn pool: gold outside, the side's colour inside
                skyRunes = true;
                for (int t = 0; t < 2; t++) { var c = SkyLanes.Fountain(t); RuneCircle.MakeAt(stageRoot, new Vector3(c.x, SkyLanes.Height(c.x, c.z), c.z), 6.1f, 5.1f, new Color(1, .62f, .25f), Lane.TeamColor(t)); }
            }
            ui.ShowHud(); ui.Banner(Loc.S("สนามรบลอยฟ้า · 3 ต่อ 3", "Sky Lanes · 3 vs 3"), Loc.S("เลนตรงกับเลนโค้งรอบป่า ทำลายป้อมแล้วทุบคริสตัลหลักของศัตรู", "A straight lane and a curved one round the jungle: break the towers, then the enemy Core Crystal"));
        }
        void UpdLane(float dt)
        {
            Fountains(dt);
            if (Lane.Lanes == 2) switch (SkyTime.Tick(time))
            {   // the sky over Sky Lanes darkens with the match
                case 1: ui.Banner(Loc.S("พลบค่ำ", "Dusk"), Loc.S("ตะวันกำลังตกดิน", "The sun is going down")); break;
                case 2: ui.Banner(Loc.S("รัตติกาล", "Night"), Loc.S("ความมืดปกคลุมเกาะลอยฟ้า", "Darkness falls over the islands")); break;
            }
            if (Lane.Lanes == 2) Jungle.Tick(dt);
            if (Lane.Lanes == 2)
            {   // a wave of three soldiers down each lane, from each base
                LetOutWave();
                minionT -= dt; if (minionT > 0) return; minionT = 28; wave++;
                for (int t = 0; t < 2; t++)
                {
                    bool brutes = !foes.Any(f => f && f.alive && f.team == 1 - t && f.towerTier == 0);   // every outer tower of the other side is down
                    if (brutes && !brutesOn[t]) { brutesOn[t] = true; ui.Banner(t == 0 ? Loc.S("ยักษ์ศึกของเราออกรบ!", "Our War Brutes march!") : Loc.S("ศัตรูส่งยักษ์ศึก!", "The enemy sends War Brutes!"), Loc.S("ป้อมนอกพังหมดแล้ว", "All outer towers have fallen")); }
                    for (int l = 0; l < 2; l++)
                    {
                        var s0 = SkyLanes.LaneStart(t, l); for (int i = 0; i < 3; i++) QueueUnit(Lane.Minion, s0 + new Vector3((i - 1) * 1.6f, 0, 0), t, l);
                        for (int i = 0; i < 2; i++) QueueUnit(Lane.Archer, s0 + new Vector3(i == 0 ? -.8f : .8f, 0, Lane.Side(t) * 1.6f), t, l);   // archers behind the front line
                        if (wave % Lane.SiegeEvery == 0) QueueUnit(Lane.Siege, s0 + new Vector3(0, 0, Lane.Side(t) * 3.2f), t, l);
                        if (brutes) QueueUnit(Lane.Brute, s0 + new Vector3(0, 0, Lane.Side(t) * 2.4f), t, l);   // behind the soldiers
                    }
                }
                return;
            }
            minionT -= dt; if (minionT > 0) return; minionT = Lane.Lanes == 3 ? 30 : 24; wave++;   // (fountains run above for every lane map)
            for (int t = 0; t < 2; t++)
                for (int l = 0; l < Lane.Lanes; l++)
                    for (int i = 0; i < 3; i++)
                    {
                        int li = Lane.Lanes == 3 ? l : 1; float x = Lane.Lanes == 3 ? Lane.LaneX(li) * .3f : 0;
                        var u = SpawnUnit(Lane.Minion, new Vector3(x + (i - 1) * 1.6f, 0, Lane.Side(t) * (Lane.CoreZ - 3)), t); if (u) u.laneIdx = li;
                    }
        }
        // each side's core heals its own heroes standing near it (the player too): a reason to go home, and where a hurt AI hero runs to
        void Fountains(float dt)
        {
            if (hero && !hero.dead && hero.hp < hero.max && Flat(hero.Pos - Lane.HealAt(0)).magnitude < Lane.HealR) { hero.fountainHeal = true; hero.Heal(hero.max * Lane.HealRate * dt, true); hero.fountainHeal = false; }
            foreach (var f in foes) if (f && f.alive && f.d.kind == "champ" && f.hp < f.maxHp && Flat(f.Pos - Lane.HealAt(f.team)).magnitude < Lane.HealR) f.hp = Mathf.Min(f.maxHp, f.hp + f.maxHp * Lane.HealRate * dt);
        }
        public int TowersLeft(int team) => foes.Count(f => f && f.alive && f.team == team && f.d.kind == "tower");
        public void OnFoeDown(Foe f)
        {
            if (!lane || mode != Mode.Game) return;
            if (f.d.id == "core") { ui.Banner(f.team == 1 ? Loc.S("ชัยชนะ!", "Victory!") : Loc.S("พ่ายแพ้", "Defeat"), f.team == 1 ? Loc.S("คริสตัลฝ่ายศัตรูถูกทำลาย", "The enemy crystal was destroyed") : Loc.S("คริสตัลของเราถูกทำลาย", "Our crystal was destroyed")); StartCoroutine(After(2.2f, () => Finish(f.team == 1))); }
            else if (f.d.id == "tower") ui.Banner(f.team == 1 ? Loc.S("ทำลายป้อมศัตรู!", "Enemy tower destroyed!") : Loc.S("ป้อมของเราถูกทำลาย", "Our tower was destroyed"), "");
            else if (f.d.kind == "champ") { var def = f.baseDef ?? f.d; int team = f.team, li = f.laneIdx; StartCoroutine(After(Lane.RespawnTime(SkyMatch ? Data.Level(teamXp[team]) : 1) + 4, () => { if (lane && mode == Mode.Game) { var u = SpawnUnit(def, Lane.SpawnOf(team) + new Vector3(Random.Range(-1.5f, 1.5f), 0, 0), team); if (u) u.laneIdx = li; } })); }
        }
        System.Collections.IEnumerator After(float s, System.Action a) { yield return new WaitForSeconds(s); a(); }
    }
}
