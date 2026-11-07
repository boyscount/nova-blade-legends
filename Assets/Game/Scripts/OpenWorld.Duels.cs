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
        const float EliteBack = 300, LurkNear = 6, LurkHint = 24, AmbushDelay = 1.2f, NpcTurn = 4;
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

        float eliteBackAt; bool wandererGone, lurkSprung, duelDone; float ambushAt = -1, hintT; Duelist fighting; Vector3 returnTo;
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
            else { lurkSprung = false; PickLurk(); }   // each night the legend hides somewhere else
        }

        void DuelsTick()
        {
            if (G.sky) { if (exit) exit.gameObject.SetActive(duelDone); return; }
            if (portal) portal.gameObject.SetActive(EliteHere);
            bool merchantOn = !EliteHere; if (merchantOn && !merchant) merchant = Npc("npc_merchant", "warrior_m", "show", hillTop + new Vector3(2.6f, 0, 1.4f));
            if (merchant && merchant.gameObject.activeSelf != merchantOn) merchant.gameObject.SetActive(merchantOn);
            bool wanderOn = !Clock.IsNight && !wandererGone; var wid = Other("ninja", "spartan");
            if (wanderOn && (!wanderer || wandererModel != wid)) { if (wanderer) Destroy(wanderer.gameObject); wandererModel = wid; wanderer = Npc(wid, Data.AnimSet(wid), "idle", risePos, true); }
            if (wanderer && wanderer.gameObject.activeSelf != wanderOn) wanderer.gameObject.SetActive(wanderOn);
            Face(merchant); Face(wanderer);
            // the lurking legend
            if (ambushAt > 0) { if (Time.time >= ambushAt) { ambushAt = -1; EnterDuel(Legend, lurkAt + new Vector3(0, 0, 2)); } return; }
            if (!Clock.IsNight || LegendTaken || lurkSprung || G.hero.dead) return;
            float d = Main.Flat(G.hero.Pos - lurkAt).magnitude;
            if (d < LurkHint && Time.time > hintT)
            {   // violet petals swirl up where it waits; closer, a pair of eyes glints in the dark
                hintT = Time.time + .7f; Fx.Burst(lurkAt + new Vector3(Random.Range(-1f, 1f), .4f, Random.Range(-1f, 1f)), 6, LegendCol, 1.6f, 1.4f, -.25f, .12f);
                if (d < LurkHint * .5f) Fx.Burst(lurkAt + Vector3.up * 1.6f, 2, new Color(1, .5f, 1), .2f, .5f, 0, .07f);
            }
            if (d < LurkNear)
            {
                lurkSprung = true; ambushAt = Time.time + AmbushDelay; G.shake = .4f;
                Fx.Burst(lurkAt + Vector3.up, 60, LegendCol, 7, .9f, -.3f); Fx.Ring(lurkAt, 4, LegendCol, .8f, .25f);
                G.ui.Banner(Loc.S("มีบางอย่างซุ่มอยู่!", "Something was lying in wait!"), Loc.S("เงาดึงคุณเข้าสู่ลานฟ้า", "A shadow drags you into the Sky Court"));
            }
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
            else if (key == "sky_exit") G.BackToZone(returnTo);
        }
        public void EnterDuel(Duelist d, Vector3 back)
        {
            fighting = d; duelDone = false; returnTo = back; nearKey = null;
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
                    G.ui.Banner(Loc.S("ปราบ Elite!", "Elite defeated!"), Loc.S("เนินปลอดภัย 5 นาที · พ่อค้าเร่มารออยู่บนยอดเนิน", "The hill is quiet for 5 minutes · a travelling merchant waits on top")); break;
                case "wanderer":
                    wandererGone = true; G.save.mats[2] += 2;
                    G.ui.Banner(Loc.S("ชนะการดวล!", "Duel won!"), Loc.S("ได้หินตีบวกหายาก ×2 · เขาจะกลับมาท้าใหม่พรุ่งนี้", "Rare enhance stones ×2 · he will be back tomorrow")); break;
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
        public void LeftSky() { fighting = null; duelDone = false; if (exit) exit.gameObject.SetActive(false); if (heroLight) heroLight.enabled = false; }
    }
}
