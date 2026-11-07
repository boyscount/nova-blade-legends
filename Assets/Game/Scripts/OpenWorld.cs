using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaBlade
{
    // The zone's land, shared by the terrain builder, every character and the grass: a shallow stream to wade or bridge, a hill to climb, a low rise.
    public static class WildsMap
    {
        public const float StreamHalf = 2.4f, StreamDepth = .45f, HillH = 8, RiseH = 3.5f;
        public static readonly Vector2 Hill = new(30, -34), Rise = new(-32, 30), House = new(-20, 16), Cave = new(8, -54);
        public static readonly Vector2[] Groves = { new(10, 44), new(-13, 46), new(18, 7), new(-13, -21) };   // the blossom trees; the falling petals come from here
        const float HillR = 22, HillTop = 6, RiseR = 18;
        static readonly Vector2[] Trail = { new(2, -12), new(16, -20), new(24, -44), new(38, -40), new(36, -30), new(30, -34) };   // the worn way up the hill
        public static float StreamZ(float x) => -6 + Mathf.Sin(x * .07f) * 7;
        public static float StreamSlope(float x) => .49f * Mathf.Cos(x * .07f);
        public static bool Dry(float x, float z, float pad = 0) => Mathf.Abs(z - StreamZ(x)) > StreamHalf + 1.5f + pad;
        static float Mound(float x, float z, Vector2 c, float r, float top, float h) => h * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(top, r, Vector2.Distance(new Vector2(x, z), c))));
        public static float Height(float x, float z)
        {
            float bed = -StreamDepth * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(StreamHalf - .6f, StreamHalf + 1.2f, Mathf.Abs(z - StreamZ(x)))));
            return bed + Mound(x, z, Hill, HillR, HillTop, HillH) + Mound(x, z, Rise, RiseR, 4, RiseH);
        }
        public static float TrailDistance(float x, float z)
        {
            var p = new Vector2(x, z); float best = 1e9f;
            for (int i = 0; i + 1 < Trail.Length; i++) { var a = Trail[i]; var d = Trail[i + 1] - a; float u = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude); best = Mathf.Min(best, Vector2.Distance(p, a + d * u)); }
            return best;
        }
    }
    // Open World zone 1, Driftpetal Wilds (design: OPEN-WORLD-ZONE1.md): a walkable area reached from the city gate, a house to enter and fight in, and the way back.
    public partial class OpenWorld : MonoBehaviour
    {
        public const float Radius = 60, RoomHalf = 8;
        public static readonly Vector3 Start = new(0, 0, 54), Gate = new(0, 0, 57.5f), RoomStart = new(0, 0, 3.6f), RoomExit = new(0, 0, 7.6f), Chest = new(0, 0, -6.2f);
        const float TalkRange = 3.4f, GlowEvery = 1.1f;
        const int ChestGold = 60;
        // each area keeps its own monsters: they appear as the hero comes near, are gone when the hero is far away, and those killed return after a while
        class Slot { public Foe foe; public float backAt; }
        class Area { public Vector2 at; public float r; public string[] kinds; public Slot[] slots; public bool night; }
        static Area A(float x, float z, float r, int n, params string[] kinds) { var a = new Area { at = new Vector2(x, z), r = r, kinds = kinds, slots = new Slot[n] }; for (int i = 0; i < n; i++) a.slots[i] = new Slot(); return a; }
        static Area NightArea { get { var a = A(-38, -28, 12, 4, "zombie", "demon"); a.night = true; return a; } }   // the deep forest belongs to the night
        Area[] areas;
        const float SpawnNear = 26, RemoveFar = 46, RespawnMin = 60, RespawnMax = 90, CheckEvery = .5f, LeashExtra = 12;
        float checkT; readonly List<Object> kept = new();
        void NewAreas()
        {
            areas = new[] { A(18, 32, 11, 5, "goblin", "rat"), A(-38, 8, 11, 5, "mushroom", "spider"), A(-6, -28, 12, 5, "skeleton", "bat"), A(42, -14, 11, 6, "orc", "goblin"), NightArea };
            kept.Clear(); foreach (var a in areas) foreach (var k in a.kinds) { var m = Data.Foe(k)?.model; if (m != null && m.StartsWith("rig_")) { var pf = Resources.Load<GameObject>("Heroes/Hero_" + m.Substring(4)); if (pf) kept.Add(pf); } else if (m != null && Catalog.I.Model(m) is GameObject q) kept.Add(q); }
        }
        public int AliveInAreas { get { int n = 0; if (areas != null) foreach (var a in areas) foreach (var s in a.slots) if (s.foe && s.foe.alive) n++; return n; } }
        public string AreaReport { get { var parts = new List<string>(); foreach (var a in areas) { int n = 0; foreach (var s in a.slots) if (s.foe && s.foe.alive) n++; parts.Add($"{a.kinds[0]}:{n}"); } return string.Join(" ", parts); } }
        void UpdateAreas()
        {
            int alive = AliveInAreas, cap = Main.IsPhone ? 12 : 20; var hero = G.hero.Pos;
            foreach (var a in areas)
            {
                if (a.night && !Clock.IsNight) { foreach (var s in a.slots) if (s.foe && Main.Flat(s.foe.Pos - hero).magnitude > NightLeaveNear) { Destroy(s.foe.gameObject); s.foe = null; alive--; } continue; }   // gone with the dawn, unless still in a fight
                float d = Vector2.Distance(new Vector2(hero.x, hero.z), a.at);
                foreach (var s in a.slots)
                {
                    if (s.foe && !s.foe.alive) { s.foe = null; s.backAt = Time.time + Random.Range(RespawnMin, RespawnMax); }
                    if (s.foe && d > a.r + RemoveFar) { Destroy(s.foe.gameObject); s.foe = null; alive--; continue; }
                    if (s.foe || Time.time < s.backAt || d > a.r + SpawnNear || alive >= cap) continue;
                    var p = a.at + Random.insideUnitCircle * a.r; if (!WildsMap.Dry(p.x, p.y, 1)) continue;
                    var at = G.Clamp(new Vector3(p.x, 0, p.y)); s.foe = G.SpawnFoe(a.kinds[Random.Range(0, a.kinds.Length)], at); s.foe.home = at; s.foe.leash = a.r + LeashExtra; alive++; if (Clock.IsNight) Strengthen(s.foe, NightFoePower); ShowSpirit(s.foe, Clock.Darkness); if (a.night) Wisps(s.foe.transform);
                }
            }
        }
        // ---------- day and night ----------
        public readonly WorldClock Clock = new();
        public const float NightFoePower = 1.25f, NightGold = 1.5f;
        const float NightLeaveNear = 15, LightEvery = .2f, MainBridgeX = 1.5f, BridgeSink = 1, BridgeTilt = 14;
        static readonly Color MoonCol = new(.62f, .72f, 1f), SkyNight = new(.44f, .52f, .78f), EqNight = new(.27f, .31f, .44f), GroundNight = new(.1f, .11f, .15f), FogNight = new(.05f, .08f, .14f);
        const float MoonI = 1.35f, FogNightDensity = .009f;
        StageLook look; Color sunDayCol; float sunDayI; bool sunKept, wasNight, testedNight; float lightT, shownDark = -1, labelSec = -1; string timeLabel;
        BridgeDeck bridge; Vector3 bridgePos; Quaternion bridgeRot;
        static readonly Color SpiritGlow = new(.14f, .95f, 1f), SpiritLift = new(1.25f, 2.5f, 2.7f), WispCol = new(.45f, 1f, .95f, .9f); static Material wispMat;
        static void ShowSpirit(Foe f, float n) => f.Spirit(SpiritGlow * n, Color.Lerp(Color.white, SpiritLift, n));
        // the deep forest's dead trail a few drifting motes; nothing else in the zone carries particles
        static void Wisps(Transform on)
        {
            var go = new GameObject("Wisps"); go.transform.SetParent(on, false); go.transform.localPosition = Vector3.up; var ps = go.AddComponent<ParticleSystem>(); ps.Stop();
            var m = ps.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1, 1.8f); m.startSpeed = .25f; m.startSize = new ParticleSystem.MinMaxCurve(.09f, .18f); m.startColor = WispCol; m.maxParticles = 18; m.gravityModifier = -.06f; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var e = ps.emission; e.rateOverTime = 9; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = .6f;
            var c = ps.colorOverLifetime; c.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) }); c.color = g;
            if (!wispMat) { wispMat = new Material(Catalog.I.glowMat) { mainTexture = Atmos.Dot() }; wispMat.SetFloat("_SrcBlend", 1); wispMat.SetFloat("_DstBlend", 1); }
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = wispMat; ps.Play();
        }
        static void Strengthen(Foe f, float by) { f.hp *= by; f.maxHp *= by; }
        public string TimeLabel
        {
            get
            {
                float sec = Mathf.Ceil(Clock.SecondsLeft); if (sec == labelSec && timeLabel != null) return timeLabel; labelSec = sec;
                string left = $"{(int)sec / 60}:{(int)sec % 60:00}";
                return timeLabel = Clock.IsNight ? Loc.S($"กลางคืน · เช้าใน {left}", $"NIGHT · DAWN IN {left}") : Loc.S($"กลางวัน · ค่ำใน {left}", $"DAY · DUSK IN {left}");
            }
        }
        // the zone's own light values eased towards moonlight; no lights are added
        public void ShowTime(bool force)
        {
            if (!G.wilds || !look) return; SetBridge(Clock.IsNight); if (caveSeal && caveSeal.activeSelf == Clock.IsNight) { caveSeal.SetActive(!Clock.IsNight); if (caveLight) caveLight.SetActive(Clock.IsNight); } float n = Clock.Darkness; if (!force && Mathf.Abs(n - shownDark) < .004f) return; shownDark = n;
            if (look.sun) { look.sun.color = Color.Lerp(sunDayCol, MoonCol, n); look.sun.intensity = Mathf.Lerp(sunDayI, MoonI, n); }
            Color sky = Color.Lerp(look.skyAmb, SkyNight, n), eq = Color.Lerp(look.eqAmb, EqNight, n), ground = Color.Lerp(look.groundAmb, GroundNight, n), fog = Color.Lerp(look.fog, FogNight, n);
            RenderSettings.fogColor = fog; RenderSettings.fogDensity = Mathf.Lerp(look.fogDensity, FogNightDensity, n); RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(eq); sh.AddDirectionalLight(Vector3.up, (sky - eq) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (ground - eq) * 1.6f, 1);
            RenderSettings.ambientProbe = sh; Shader.SetGlobalVector("_NovaAmbient", (sky + eq) * .5f);
            if (Camera.main) Camera.main.backgroundColor = fog;
            if (areas != null) foreach (var a in areas) foreach (var sl in a.slots) if (sl.foe && sl.foe.alive) ShowSpirit(sl.foe, n);
        }
        void SetBridge(bool broken)
        {
            if (!bridge || bridge.enabled == !broken) return; bridge.enabled = !broken;
            bridge.transform.SetPositionAndRotation(broken ? bridgePos + Vector3.down * BridgeSink : bridgePos, broken ? Quaternion.AngleAxis(BridgeTilt, bridge.axis) * bridgeRot : bridgeRot);
        }
        void TurnTime(bool night)
        {
            if (areas != null) foreach (var a in areas) foreach (var s in a.slots) if (s.foe && s.foe.alive) Strengthen(s.foe, night ? NightFoePower : 1 / NightFoePower);   // none when the duel came from the city
            DuelsTurn(night);
            if (G.wilds) G.ui.Banner(night ? Loc.S("ค่ำแล้ว", "Night falls") : Loc.S("เช้าแล้ว", "Dawn"), night ? Loc.S("ศัตรูแรงขึ้น ทองเพิ่ม 50% สะพานหลักพัง", "Stronger foes, +50% gold, the main bridge is down") : Loc.S("ศัตรูกลับเป็นปกติ สะพานใช้ได้แล้ว", "Foes calm down, the bridge is back"));
        }
        // ---------- the secret cave: found by following the petals, open only at night, one rare weapon per real day (the player's own clock until there is a server) ----------
        public bool InCave;
        public static string Today => System.DateTime.Now.ToString("yyyy-MM-dd");
        public bool CaveTaken => G.save.caveDay == Today;
        Vector3 caveDoor; GameObject caveSeal, caveLight; PetalTrail trail;
        const int CaveGold = 150, RareWeaponPrice = 800;
        static readonly Color CaveGlowCol = new(.5f, 1, .95f);
        public Vector3 CaveDoor => caveDoor;
        public void TryCave()
        {
            if (!Clock.IsNight) { G.ui.Banner(Loc.S("ประตูหินปิดสนิท", "The stone door will not move"), Loc.S("รอยสลักรูปจันทร์เรืองจาง ๆ", "A carved moon glimmers faintly")); return; }
            if (CaveTaken) { G.ui.Banner(Loc.S("หีบในถ้ำว่างเปล่า", "The cave's chest stands empty"), Loc.S("กลับมาใหม่พรุ่งนี้", "Come back tomorrow")); return; }
            G.EnterCave();
        }
        void OpenCaveChest()
        {
            if (CaveTaken) return; G.save.caveDay = Today; nearKey = null; G.ui.SetCityPrompt(null, null);
            Fx.Burst(Chest + Vector3.up, 80, CaveGlowCol, 8); G.DropLoot(Chest + new Vector3(0, 0, 2), true, CaveGold);
            var unowned = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(Data.Swords, w => !Shop.Owns(ItemKind.Weapon, w.id)));
            var rare = unowned.FindAll(w => w.price >= RareWeaponPrice); var pool = rare.Count > 0 ? rare : unowned;
            if (pool.Count > 0) { var w = pool[Random.Range(0, pool.Count)]; G.save.owned.Add(Shop.Key(ItemKind.Weapon, w.id)); G.ui.Banner(Loc.S("ได้อาวุธหายาก!", "A rare weapon!"), w.name); }
            else G.ui.Banner(Loc.S("เปิดหีบใหญ่!", "The great chest!"), Loc.S("ได้ของรางวัลพิเศษ", "A special reward"));
            G.Persist();
        }
        bool ChestReady => InCave ? !CaveTaken : !HouseCleared;
        public bool HouseCleared { get; private set; }   // the chest has been opened on this visit to the zone
        Main G => Main.G; Transform petals; string nearKey; float glowT; Vector3 door;
        public Vector3 Door => door;

        public void Enter()
        {
            HouseCleared = false; nearKey = null; NewAreas();
            var zone = G.Stage("Stage_wilds"); if (!petals) { petals = LobbyNight.PetalsAt(zone); var box = petals.GetComponent<ParticleSystem>().shape; box.scale = new Vector3(30, .5f, 18); }
            var d = zone.Find("HouseDoor"); door = d ? d.position : Start;
            InCave = false; var cd = zone.Find("CaveDoorMark"); caveDoor = cd ? cd.position : Start; var seal = zone.Find("CaveSeal"); caveSeal = seal ? seal.gameObject : null; var cl = zone.Find("CaveLight"); caveLight = cl ? cl.gameObject : null;
            if (!trail) { trail = gameObject.AddComponent<PetalTrail>(); trail.Set(LobbyNight.PetalsAt(zone)); }
            look = zone.GetComponent<StageLook>(); if (look && look.sun && !sunKept) { sunKept = true; sunDayCol = look.sun.color; sunDayI = look.sun.intensity; }
            if (!bridge) { foreach (var b in zone.GetComponentsInChildren<BridgeDeck>(true)) if (!bridge || Mathf.Abs(b.center.x - MainBridgeX) < Mathf.Abs(bridge.center.x - MainBridgeX)) bridge = b; if (bridge) { bridgePos = bridge.transform.position; bridgeRot = bridge.transform.rotation; } }
            if (!testedNight && Crumb.Has("night")) { testedNight = true; Clock.t = WorldClock.DayLen - WorldClock.Fade - 5; }   // test switch in the page address: dusk comes in 5 seconds
            wasNight = Clock.IsNight; ShowTime(true); DuelsEnter(zone);
        }
        void Glow(Vector3 at, Color c) => Fx.Ring(at + Vector3.up * .06f, 2.4f, c, 1.1f, .22f);
        void Use(string key)
        {
            if (key == "gate") G.StartCity();
            else if (key == "door") G.EnterHouse();
            else if (key == "exit") G.LeaveHouse();
            else if (key == "chest") { if (InCave) OpenCaveChest(); else OpenChest(); }
            else if (key == "cave" || key == "cave_open") TryCave();
            else UseDuel(key);
        }
        public void OpenChest()
        {
            if (HouseCleared) return; HouseCleared = true; nearKey = null; G.ui.SetCityPrompt(null, null);
            Fx.Burst(Chest + Vector3.up, 60, new Color(1, .85f, .4f), 7); G.DropLoot(Chest + new Vector3(0, 0, 2), true, ChestGold); G.ui.Banner(Loc.S("เปิดหีบสมบัติ!", "Treasure!"), "");
        }
        void Update()
        {
            if (heroLight && heroLight.enabled && !(G && G.sky)) heroLight.enabled = false;   // left the duel any way at all (pause menu to the city too)
            if (G && (G.wilds || G.house || G.sky) && G.mode == Main.Mode.Game)
            {
                Clock.Tick(Time.deltaTime); if (Clock.IsNight != wasNight) { wasNight = Clock.IsNight; TurnTime(wasNight); }
                if (Time.time > lightT) { lightT = Time.time + LightEvery; ShowTime(false); }
            }
            if (G && G.wilds && G.hero && !G.hero.dead && G.mode == Main.Mode.Game && Time.time > checkT) { checkT = Time.time + CheckEvery; UpdateAreas(); }
            if (G && (G.wilds || G.sky) && G.hero && G.mode == Main.Mode.Game) DuelsTick();
            string key = null; string text = null;
            if (G && G.hero && !G.hero.dead && G.mode == Main.Mode.Game && (G.wilds || G.house || G.sky) && !G.ui.CityPanelOpen)
            {
                bool Near(Vector3 at) => Main.Flat(at - G.hero.Pos).magnitude < TalkRange;
                if (Time.time > glowT)
                {
                    glowT = Time.time + GlowEvery;
                    if (G.sky) DuelGlow();
                    else if (G.wilds) { Glow(Gate, new Color(1, .72f, .86f)); Glow(door, new Color(1, .8f, .45f)); if (Clock.IsNight && !CaveTaken) Glow(caveDoor, CaveGlowCol); DuelGlow(); }
                    else { Glow(RoomExit, new Color(1, .8f, .45f)); if (G.HouseFought && ChestReady) Glow(Chest, InCave ? CaveGlowCol : new Color(1, .85f, .4f)); }
                }
                if (G.sky) key = DuelKey(Near, out text);
                else if (G.wilds) { if (Near(Gate)) { key = "gate"; text = Loc.S("กลับเมือง", "BACK TO CITY"); } else if (Near(door)) { key = "door"; text = Loc.S("เข้าบ้าน", "ENTER THE HOUSE"); } else if (Near(caveDoor)) { key = Clock.IsNight && !CaveTaken ? "cave_open" : "cave"; text = Clock.IsNight && !CaveTaken ? Loc.S("เข้าถ้ำลับ", "ENTER THE CAVE") : Loc.S("ตรวจดูประตูหิน", "EXAMINE THE DOOR"); } else key = DuelKey(Near, out text); }
                else if (Near(RoomExit)) { key = "exit"; text = InCave ? Loc.S("ออกจากถ้ำ", "LEAVE THE CAVE") : Loc.S("ออกจากบ้าน", "LEAVE THE HOUSE"); }
                else if (G.HouseFought && ChestReady && Near(Chest)) { key = "chest"; text = Loc.S("เปิดหีบ", "OPEN THE CHEST"); }
            }
            if (key != nearKey) { nearKey = key; if (G && G.ui) { if (key != null) { var k = key; G.ui.SetCityPrompt(text, () => Use(k)); } else G.ui.SetCityPrompt(null, null); } }
            var kb = Keyboard.current; if (nearKey != null && kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) Use(nearKey);
        }
        const float PetalsThin = 4, PetalsThick = 44, GroveNear = 10, GroveFar = 26, PetalsEvery = .5f; float petalT;
        void PetalRate() { float d = 1e9f; var h = new Vector2(G.hero.Pos.x, G.hero.Pos.z); foreach (var g in WildsMap.Groves) d = Mathf.Min(d, Vector2.Distance(h, g)); var em = petals.GetComponent<ParticleSystem>().emission; em.rateOverTime = Mathf.Lerp(PetalsThick, PetalsThin, Mathf.InverseLerp(GroveNear, GroveFar, d)); }
        void LateUpdate() { if (G && G.wilds && petals && Time.time > petalT) { petalT = Time.time + PetalsEvery; PetalRate(); } if (G && G.wilds && petals) petals.position = G.hero.Pos + new Vector3(0, 6, -9); }
    }
}
