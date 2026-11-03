using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
        public Transform[] stageRoots;
        public ThemeDef Theme => Data.Themes[Data.ThemeOf(stage)];

        [System.Serializable]
        public class Save
        {
            public string hero = "warrior", sword = "w_lava", pet = "candle"; public int gold, best, stage = 1; public List<string> skinKeys = new(); public List<int> skinVals = new();
            public int gems = Shop.TestGems;                       // test-only premium currency (no real payments yet)
            public List<string> owned = new(), ledger = new();     // ownership and purchase history, kept apart from what is equipped
            public List<string> wsKeys = new(), wsVals = new();    // equipped weapon skin per weapon
            public string WeaponSkin(string weapon) { int i = wsKeys.IndexOf(weapon); return i < 0 ? null : wsVals[i]; }
            public void SetWeaponSkin(string weapon, string model) { int i = wsKeys.IndexOf(weapon); if (i < 0) { wsKeys.Add(weapon); wsVals.Add(model); } else wsVals[i] = model; }
        }
        // lobby try-on: shown on the hero in the lobby only, never taken into a match unless owned
        [System.NonSerialized] public string pvSword, pvWeaponSkin; [System.NonSerialized] public int pvSkin = -1;   // runtime only (a serialized string would come back as "")
        public void ClearPreview() { pvSword = pvWeaponSkin = null; pvSkin = -1; }
        public Save save;
        public int Skin(string id) { int i = save.skinKeys.IndexOf(id); return i < 0 ? 0 : save.skinVals[i]; }
        public void SetSkin(string id, int v) { int i = save.skinKeys.IndexOf(id); if (i < 0) { save.skinKeys.Add(id); save.skinVals.Add(v); } else save.skinVals[i] = v; Persist(); }
        public void Persist() { PlayerPrefs.SetString("novaBlade", JsonUtility.ToJson(save)); PlayerPrefs.Save(); }
        public int MaxStage => Mathf.Max(Data.Themes.Length, Mathf.Min(99, save.best + 1));   // every stage is open from the start

        void Awake()
        {
            G = this;
            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) QualitySettings.SetQualityLevel(0, true);   // phones (also mobile browsers): the Mobile URP asset
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
            save.hero = "darkmage"; StartLane(3, 3); yield return ClipFight(6);
            save.hero = "warrior"; StartSoul(); yield return ClipFight(5);
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
            River.Active = stageRoots[i] && stageRoots[i].name == "Stage_river";
            var look = stageRoots[i] ? stageRoots[i].GetComponent<StageLook>() : null;
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
        class Drop { public GameObject go; public bool orb; public int v; public Vector3 p, vel; public float t; }
        static Mesh coinMesh; static Material coinMat, orbMat;
        public void DropLoot(Vector3 p, bool isBoss, int g)
        {
            int n = isBoss ? 24 : Random.Range(1, 4);
            for (int i = 0; i < n; i++) AddDrop(p, false, isBoss ? 8 : Mathf.CeilToInt(g / 2f));
            if (Random.value < (isBoss ? 1 : .12f)) AddDrop(p, true, 0);
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
                bool dead = d.t > 25;
                if (d.t > .5f && dd < .6f)
                {
                    dead = true;
                    if (!d.orb) { int v = Mathf.RoundToInt(d.v * (pet.def != null && pet.def.gold ? 1.5f : 1)); gold += v; Number(hero.Pos + Vector3.up * 1.8f, "+" + v, "gold"); }
                    else { hero.Heal(hero.max * .1f); Fx.Burst(hero.Pos + Vector3.up, 14, new Color(.4f, 1, .55f), 3); }
                }
                d.go.transform.position = d.p + (d.orb ? Vector3.up * Mathf.Sin(d.t * 4) * .08f : Vector3.zero);
                d.go.transform.rotation = Quaternion.Euler(d.orb ? 0 : 90, d.t * 280, 0);
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
                    float d = Mathf.Lerp(bot ? 5 : 4.1f, 1.6f, z);
                    look = new Vector3(side * (bot ? 1.25f : 1) * d / 4.1f, Mathf.Lerp(bot ? .75f : .95f, bot ? .7f : 1.42f, z), 0); want = look + new Vector3(0, Mathf.Lerp(.4f, .08f, z), d);
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
                var focus = ui.Pan ?? hero.Pos; var want = focus + new Vector3(0, 8.6f * far, 7.4f * far);
                camPos = Vector3.Lerp(camPos, want, 1 - Mathf.Exp(-dt * (hero.dead ? 2.2f : 6)));
                camLook = new Vector3(camPos.x, hero.dead ? .5f : lane ? 1.6f : .9f, camPos.z - 7.4f * far);
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
