using UnityEngine;

namespace NovaBlade
{
    // One-on-one fights of the zone (step 5 of OPEN-WORLD-ZONE1.md), all fought on the Sky Court, a floating stage of its own:
    // - the Elite waits beyond the portal on the hilltop; beaten, the hill is quiet for 5 minutes and a travelling merchant comes
    // - a wandering swordsman stands on the low rise by day and takes a challenge; he guards, answers and steps aside; back the next day
    // - a legend lurks somewhere in the zone at night, once per real day: drifting violet petals give the place away, coming close springs it
    public partial class OpenWorld
    {
        public class Duelist { public string id, title; public BossDef boss; }
        public const float SkyHalf = 10.4f;
        public static readonly Vector3 SkyStart = new(0, 0, 6.5f), SkyFoe = new(0, 0, -5.5f), SkyExit = new(0, 0, 9.4f);
        const float EliteBack = 300, LurkNear = 6, LurkHint = 40, LurkBannerDelay = 4, BeamFull = 30, BeamGone = 10, BeamH = 16, BeamW = 2.2f, AmbushDelay = 1.2f, NpcTurn = 4;
        static readonly Vector2[] LurkSpots = { new(-44, -36), new(-26, -46), new(-50, 14), new(46, 30) };
        static readonly Color PortalCol = new(.45f, .8f, 1f), ExitCol = new(1f, .82f, .45f), LegendCol = new(.85f, .45f, 1f);

        static string Other(string a, string b) => Main.G.save.hero == a ? b : a;   // never a mirror of the player's own hero
        static string HeroName(string id) { var h = System.Array.Find(Data.Heroes, x => x.id == id); return h != null ? h.name : id; }
        public static Duelist Elite => new()
        {
            id = "elite", title = Loc.S("ผู้เฝ้าเนินหิน · กระโดดทุบจากไกล ฟันสามจังหวะ จังหวะสุดท้ายช้ากว่า", "Keeper of the hill · leaps from afar, three-blow combo, the last one comes late"),
            boss = new BossDef { model = "rig_e_ghoul", name = Loc.S("ยักษ์กูลผู้เฝ้าลานฟ้า", "Ghoul Warden"), scale = 2.2f, hp = 6500, dmg = 150, spd = 2.5f, poise = 360, hits = 48, takes = 7, tint = new Color(.9f, 1, .85f), glow = new Color(.12f, .2f, .05f), rage = new Color(.4f, .6f, .05f), color = new Color(.7f, 1, .35f), summon = "skeleton", summonN = 2, near = new[] { "combo", "combo", "sweep", "leap" }, far = new[] { "leap", "leap", "summon", "combo" } },
        };
        public static Duelist Wanderer { get { var id = Other("ninja", "spartan"); return new()
        {
            id = "wanderer", title = Loc.S("นักดาบพเนจร · ตั้งการ์ดแล้วสวนกลับ ตีเบาใส่การ์ดจะโดนสวน ใช้จังหวะที่ 3 ของคอมโบหรือสกิลกระแทกทำลายการ์ด", "Wandering blade · guards and answers; break the guard with the third combo blow or a hard-hitting skill"),
            boss = new BossDef { model = "hero_" + id, name = HeroName(id) + Loc.S(" นักดาบพเนจร", " the Wanderer"), scale = 1.05f, hp = 5200, dmg = 120, spd = 3.6f, poise = 260, hits = 40, takes = 8, tint = Color.white, glow = new Color(.04f, .06f, .12f), rage = new Color(.1f, .25f, .6f), color = new Color(.6f, .8f, 1), near = new[] { "combo", "combo", "guard", "guard", "sweep", "sidestep" }, far = new[] { "lunge", "lunge", "combo" } },
        }; } }
        public static Duelist Legend { get { var id = Other("samurai", "ninja"); return new()
        {
            id = "legend", title = Loc.S("ดาบในตำนาน · หายตัวมาข้างหลัง ปล่อยดาบจันทร์ อ่านดาบทุกเล่ม", "The legendary blade · vanishes behind you, casts moon blades, reads every swing"),
            boss = new BossDef { model = "hero_" + id, name = Loc.S("เงาซากุระ ดาบในตำนาน", "Sakura Shade, the Legend"), scale = 1.08f, hp = 8500, dmg = 135, spd = 4.2f, poise = 300, hits = 56, takes = 6, tint = new Color(.62f, .56f, .72f), glow = new Color(.3f, .06f, .45f), rage = new Color(.7f, .1f, .8f), color = LegendCol, near = new[] { "combo", "combo", "guard", "sidestep", "dash", "sweep" }, far = new[] { "dash", "lunge", "souls", "lunge" } },
        }; } }

        float lurkBannerAt = -1; Transform beam; Material beamMat;
        float eliteBackAt, wandererBackAt; bool wandererGone, lurkSprung, duelDone, fromCity; float ambushAt = -1, hintT; Duelist fighting; Vector3 returnTo;
        Vector3 hillTop, risePos, lurkAt; Portal portal, exit; RuneCircle runes; Light heroLight; Transform wanderer, merchant; string wandererModel;
        public bool EliteHere => Time.time >= eliteBackAt;
        public bool LegendTaken => G.save.legendDay == Today;
        public bool MerchantHere => merchant && merchant.gameObject.activeSelf;
        public bool WandererHere => wanderer && wanderer.gameObject.activeSelf;
        public Vector3 HillTop => hillTop; public Vector3 RisePos => risePos; public Vector3 LurkAt => lurkAt; public string Fighting => fighting?.id;
        static Vector3 Ground(Vector2 p) => new(p.x, WildsMap.Height(p.x, p.y), p.y);

        void DuelsEnter(Transform zone)
        {
            hillTop = Ground(WildsMap.Hill); risePos = Ground(WildsMap.Rise + new Vector2(2, -2));
            if (!portal) portal = Portal.Make(zone, hillTop, PortalCol, 1.4f);
            fighting = null; duelDone = false; ambushAt = -1;
            if (lurkAt == default) PickLurk();
        }
        void PickLurk() { var p = LurkSpots[Random.Range(0, LurkSpots.Length)]; lurkAt = Ground(p); }
        void DuelsTurn(bool night)
        {
            if (!night) wandererGone = false;   // a new day: the wanderer is back on the rise
            else { lurkSprung = false; PickLurk(); if (!LegendTaken) lurkBannerAt = Time.time + LurkBannerDelay; }   // each night the legend hides somewhere else; a hint of where follows the dusk banner
        }

        void DuelsTick()
        {
            if (G.sky) { if (exit) exit.gameObject.SetActive(duelDone); return; }
            if (portal) portal.gameObject.SetActive(EliteHere);
            bool merchantOn = !EliteHere; if (merchantOn && !merchant) merchant = Npc("npc_merchant", "warrior_m", "show", hillTop + new Vector3(2.6f, 0, 1.4f));
            if (merchant && merchant.gameObject.activeSelf != merchantOn) merchant.gameObject.SetActive(merchantOn);
            bool wanderOn = !Clock.IsNight && !wandererGone && Time.time >= wandererBackAt; var wid = Other("ninja", "spartan");
            if (wanderOn && (!wanderer || wandererModel != wid)) { if (wanderer) Destroy(wanderer.gameObject); wandererModel = wid; wanderer = Npc(wid, Data.AnimSet(wid), "idle", risePos, true); }
            if (wanderer && wanderer.gameObject.activeSelf != wanderOn) wanderer.gameObject.SetActive(wanderOn);
            Face(merchant); Face(wanderer);
            // the lurking legend: a dusk banner names the direction, a faint violet beam shows from afar and fades as you close in
            if (lurkBannerAt > 0 && Time.time >= lurkBannerAt) { lurkBannerAt = -1; if (Clock.IsNight && !LegendTaken && !lurkSprung) G.ui.Banner(Loc.S("กลีบสีม่วงร่วงหล่นในความมืด", "Violet petals fall in the dark"), Loc.S($"มีบางอย่างซุ่มรออยู่ทางทิศ{Compass(lurkAt, true)}", $"Something waits to the {Compass(lurkAt, false)}")); }
            LurkBeam(Clock.IsNight && !LegendTaken && !lurkSprung && ambushAt < 0);
            if (ambushAt > 0) { if (Time.time >= ambushAt) { ambushAt = -1; EnterDuel(Legend, lurkAt + new Vector3(0, 0, 2)); } return; }
            if (!Clock.IsNight || LegendTaken || lurkSprung || G.hero.dead) return;
            float d = Main.Flat(G.hero.Pos - lurkAt).magnitude;
            if (d < LurkHint && Time.time > hintT)
            {   // violet petals swirl up where it waits; closer, a pair of eyes glints in the dark
                hintT = Time.time + .6f; Fx.Burst(lurkAt + new Vector3(Random.Range(-1.5f, 1.5f), .4f, Random.Range(-1.5f, 1.5f)), 10, LegendCol, 2.2f, 1.8f, -.35f, .16f);
                if (d < LurkHint * .5f) Fx.Burst(lurkAt + Vector3.up * 1.6f, 2, new Color(1, .5f, 1), .2f, .5f, 0, .07f);
            }
            if (d < LurkNear)
            {
                lurkSprung = true; ambushAt = Time.time + AmbushDelay; G.shake = .4f;
                Fx.Burst(lurkAt + Vector3.up, 60, LegendCol, 7, .9f, -.3f); Fx.Ring(lurkAt, 4, LegendCol, .8f, .25f);
                G.ui.Banner(Loc.S("มีบางอย่างซุ่มอยู่!", "Something was lying in wait!"), Loc.S("เงาดึงคุณเข้าสู่ลานฟ้า", "A shadow drags you into the Sky Court"));
            }
        }
        // north is up the screen (-Z), east is to the right (-X): the camera looks from the gate side
        static string Compass(Vector3 p, bool th)
        {
            float a = Mathf.Atan2(-p.x, -p.z) * Mathf.Rad2Deg; int k = ((Mathf.RoundToInt(a / 45f) % 8) + 8) % 8;
            return th ? new[] { "เหนือ", "ตะวันออกเฉียงเหนือ", "ตะวันออก", "ตะวันออกเฉียงใต้", "ใต้", "ตะวันตกเฉียงใต้", "ตะวันตก", "ตะวันตกเฉียงเหนือ" }[k]
                      : new[] { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" }[k];
        }
        // two crossed quads of soft violet light standing over the hiding place; brightest from afar, gone up close so the ambush still surprises
        void LurkBeam(bool on)
        {
            if (!on) { if (beam && beam.gameObject.activeSelf) beam.gameObject.SetActive(false); return; }
            if (!beam)
            {
                const int W = 32, H = 64; var t = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float u = Mathf.Abs((x + .5f) / W * 2 - 1), v = (y + .5f) / H; t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(1 - u, 2.4f) * Mathf.Pow(1 - v, 1.3f) * Mathf.Clamp01(v * 10))); }
                t.Apply(false);
                var verts = new[] { new Vector3(-BeamW / 2, 0, 0), new Vector3(BeamW / 2, 0, 0), new Vector3(BeamW / 2, BeamH, 0), new Vector3(-BeamW / 2, BeamH, 0), new Vector3(0, 0, -BeamW / 2), new Vector3(0, 0, BeamW / 2), new Vector3(0, BeamH, BeamW / 2), new Vector3(0, BeamH, -BeamW / 2) };
                var uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                var mesh = new Mesh { name = "LurkBeam", vertices = verts, uv = uv, triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 6, 5, 4, 7, 6 } }; mesh.RecalculateBounds();
                beamMat = new Material(Catalog.I.glowMat) { mainTexture = t, renderQueue = 3100 }; beamMat.SetFloat("_SrcBlend", 1); beamMat.SetFloat("_DstBlend", 1); beamMat.SetFloat("_UseVC", 0);
                var go = new GameObject("LurkBeam"); go.transform.SetParent(G.Stage("Stage_wilds"), false); go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = beamMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; beam = go.transform;
            }
            if (!beam.gameObject.activeSelf) beam.gameObject.SetActive(true); beam.position = lurkAt;
            float d = Main.Flat(G.hero.Pos - lurkAt).magnitude, k = Mathf.InverseLerp(BeamGone, BeamFull, d) * (.8f + .2f * Mathf.Sin(Time.time * 1.7f));
            beamMat.SetColor("_Color", LegendCol * .45f * k);
        }
        // a person standing in the zone: the hero's own clips, a weapon if the model has a socket
        Transform Npc(string id, string controller, string state, Vector3 at, bool armed = false)
        {
            var pf = Resources.Load<GameObject>("Heroes/Hero_" + id); if (!pf) { Debug.LogError("[Duels] no model " + id); return null; }
            var go = Instantiate(pf, at, Quaternion.Euler(0, 180, 0), G.Stage("Stage_wilds")); go.name = "Npc_" + id;
            var a = go.GetComponentInChildren<Animator>();
            if (a && controller != null && Resources.Load<RuntimeAnimatorController>("Anim/" + controller) is RuntimeAnimatorController c) { a.runtimeAnimatorController = c; a.SetFloat("Speed", 1); a.Play(state, 0, Random.value); }
            if (armed) foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t.name == "SwordSocket") { Instantiate(Catalog.I.Model(Data.Swords[0].id), t); break; }
            return go.transform;
        }
        void Face(Transform npc)
        {
            if (!npc || !npc.gameObject.activeSelf) return; var to = Main.Flat(G.hero.Pos - npc.position);
            if (to.sqrMagnitude < 100 && to.sqrMagnitude > .01f) npc.rotation = Quaternion.Slerp(npc.rotation, Quaternion.LookRotation(to), Time.deltaTime * NpcTurn);
        }

        void DuelGlow()
        {
            if (G.sky) { if (duelDone) Glow(SkyExit, ExitCol); return; }
            if (EliteHere) Glow(hillTop, PortalCol); else if (MerchantHere) Glow(merchant.position, new Color(1, .85f, .4f));
            if (WandererHere) Glow(risePos, new Color(.6f, .8f, 1));
        }
        string DuelKey(System.Func<Vector3, bool> near, out string text)
        {
            text = null;
            if (G.sky) { if (duelDone && near(SkyExit)) { text = Loc.S("กลับสู่ป่า", "BACK TO THE WILDS"); return "sky_exit"; } return null; }
            if (EliteHere && near(hillTop)) { text = Loc.S("เข้าประตูมิติ · ลานฟ้า", "ENTER THE PORTAL · SKY COURT"); return "portal"; }
            if (MerchantHere && near(merchant.position)) { text = Loc.S("ซื้อของ · พ่อค้าเร่", "SHOP · TRAVELLING MERCHANT"); return "merchant"; }
            if (WandererHere && near(risePos)) { text = Loc.S("ท้าดวล · ", "CHALLENGE · ") + HeroName(wandererModel); return "wanderer"; }
            return null;
        }
        void UseDuel(string key)
        {
            if (key == "portal") EnterDuel(Elite, hillTop + new Vector3(0, 0, 3));
            else if (key == "wanderer") EnterDuel(Wanderer, risePos + new Vector3(0, 0, 2.5f));
            else if (key == "merchant") G.ui.OpenCityPanel("shop");
            else if (key == "sky_exit") { if (fromCity) { LeftSky(); G.CityAtGate(); } else G.BackToZone(returnTo); }
        }
        public void EnterDuel(Duelist d, Vector3 back, bool city = false)
        {
            fighting = d; duelDone = false; returnTo = back; nearKey = null; fromCity = city;
            G.EnterSky(d, Clock.IsNight);
            if (!exit) exit = Portal.Make(G.Stage("Stage_sky"), SkyExit, ExitCol, 1.3f);
            exit.gameObject.SetActive(false);
            if (!runes) runes = RuneCircle.Make(G.Stage("Stage_sky"), SkyHalf - .2f, 4.8f);
            runes.Set(d.boss.color); runes.Flare();   // the floor's letters wake in the duellist's colour
            if (!heroLight) { heroLight = new GameObject("DuelLight").AddComponent<Light>(); heroLight.transform.SetParent(G.hero.transform, false); heroLight.transform.localPosition = new Vector3(0, 2.6f, .8f); heroLight.type = LightType.Point; heroLight.color = new Color(.8f, .88f, 1f); heroLight.intensity = 1.9f; heroLight.range = 7; heroLight.shadows = LightShadows.None; }
            heroLight.enabled = Clock.IsNight;   // by night the hero carries a soft light into the duel
        }
        // the duellist has fallen (Foe.Kill, after its death): rewards, the way home opens
        public void DuelWon(Foe f)
        {
            if (fighting == null || duelDone) return; duelDone = true; G.boss = null; G.SetSoul(false); if (runes) { runes.Set(ExitCol); runes.Flare(); }
            switch (fighting.id)
            {
                case "elite":
                    eliteBackAt = Time.time + EliteBack;
                    G.ui.Banner(Loc.S("ปราบ Elite!", "Elite defeated!"), fromCity ? Loc.S("ท้าใหม่ได้ในอีก 5 นาที", "Back for another duel in 5 minutes") : Loc.S("เนินปลอดภัย 5 นาที · พ่อค้าเร่มารออยู่บนยอดเนิน", "The hill is quiet for 5 minutes · a travelling merchant waits on top")); break;
                case "wanderer":
                    if (fromCity) wandererBackAt = Time.time + EliteBack; else wandererGone = true; G.save.mats[2] += 2;
                    G.ui.Banner(Loc.S("ชนะการดวล!", "Duel won!"), fromCity ? Loc.S("ได้หินตีบวกหายาก ×2 · ท้าใหม่ได้ในอีก 5 นาที", "Rare enhance stones ×2 · back for another duel in 5 minutes") : Loc.S("ได้หินตีบวกหายาก ×2 · เขาจะกลับมาท้าใหม่พรุ่งนี้", "Rare enhance stones ×2 · he will be back tomorrow")); break;
                case "legend":
                    G.save.legendDay = Today; var w = LegendWeapon();
                    if (w != null) { G.save.owned.Add(Shop.Key(ItemKind.Weapon, w.id)); G.ui.Banner(Loc.S("ปราบตำนาน!", "The legend falls!"), Loc.S("ได้อาวุธ ", "Weapon: ") + w.name); }
                    else { G.gold += 600; G.ui.Banner(Loc.S("ปราบตำนาน!", "The legend falls!"), Loc.S("ทอง +600", "+600 gold")); }
                    break;
            }
            G.Persist();
        }
        // the dearest weapon not yet owned
        static SwordDef LegendWeapon()
        {
            SwordDef best = null; foreach (var w in Data.Swords) if (!Shop.Owns(ItemKind.Weapon, w.id) && (best == null || w.price > best.price)) best = w; return best;
        }
        // the Sky Court straight from the city gate (the phone build has no zone yet): the Elite and the wanderer, each back 5 minutes after a loss; home is the city
        public bool FromCityDuel => fromCity && G.sky;
        public void FromCity(string id)
        {
            bool elite = id == "elite"; float back = elite ? eliteBackAt : wandererBackAt;
            if (Time.time < back) { int min = Mathf.CeilToInt((back - Time.time) / 60); G.ui.Banner(elite ? Elite.boss.name : Wanderer.boss.name, Loc.S($"ยังพักอยู่ · ท้าใหม่ได้ในอีก {min} นาที", $"Resting · back in {min} min")); return; }
            EnterDuel(elite ? Elite : Wanderer, Vector3.zero, true);
        }
        public void LeftSky() { fromCity = false; fighting = null; duelDone = false; if (exit) exit.gameObject.SetActive(false); if (heroLight) heroLight.enabled = false; }
    }
}
