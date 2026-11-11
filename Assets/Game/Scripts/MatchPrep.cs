using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // Before a lane battle: pick a hero (cards by class, the kit beside them), then the two teams face each other on a VS screen while
    // the battle loads. PC: big full-body heroes on each side. Phones: three narrow cards a side, light on memory.
    public partial class Ui
    {
        VisualElement pickOv, vsOv; string pickCls = "all", pickId; float vsT; bool vsOn; Label vsPct; VisualElement vsFill;
        static readonly string[] TeamNames = { "ทีมหมาป่า|Wolf Pack", "ทีมเหยี่ยวฟ้า|Sky Hawks", "ทีมพยัคฆ์|Tiger Clan", "ทีมเพลิงทอง|Golden Flame", "ทีมจันทรา|Moon Guard" };
        static readonly string[] FoeNames = { "ทีมผู้ท้าชิง|Challengers", "ทีมเงามรณะ|Death Shades", "ทีมโลหิต|Blood Oath", "ทีมพายุดำ|Black Storm", "ทีมเขี้ยวเหล็ก|Iron Fangs" };
        VisualElement vsSpirit0, vsSpirit1, vsSide0, vsSide1, vsBolt, vsFlash; float vsFlashK; string ourTeam, theirTeam; int vsOurCount; readonly Dictionary<string, int> vsRank = new();
        // each hero's rank points on the VS screen: the player's own; AI heroes near it (harder AI, enemies a little higher)
        int RankPts(HeroDef h, int t) { if (t == 0 && h == G.hero.def) return G.save.rankPts; string k = t + h.id; if (!vsRank.TryGetValue(k, out int v)) vsRank[k] = v = Mathf.Max(0, G.save.rankPts + Random.Range(-90, 90) + (t == 1 ? (G.save.aiLevel - 1) * 120 : 0)); return v; }
        static string Pick2(string s) => Loc.S(s.Split('|')[0], s.Split('|')[1]);
        Color ClassCol(string cls) => Data.Classes.FirstOrDefault(c => c.id == cls).c;
        string ClassName(string cls) => Data.Classes.FirstOrDefault(c => c.id == cls).th;

        // ---------- hero pick ----------
        ScrollView pickSv; string pickSvCls; float pickScrollX;
        public string PickScrollInfo => pickSv == null ? "none" : $"picked {pickId}, row at {pickSv.scrollOffset.x:0} (was 900)";   // tests
        public void TestPickScrolled() { pickCls = "all"; ShowHeroPick(); pickSv.scrollOffset = new Vector2(900, 0); pickSv.schedule.Execute(() => { pickSv.scrollOffset = new Vector2(900, 0); pickId = "monk"; ShowHeroPick(); }).ExecuteLater(200); }   // tests: a pick after scrolling keeps the row in place
        public void ShowHeroPick()
        {
            if (pickOv == null)
            {
                pickOv = Box(new Color(.015f, .025f, .045f, 1)); Abs(pickOv, 0, 0, 0, 0); pickOv.pickingMode = PickingMode.Position; pickOv.style.alignItems = Align.Center; root.Add(pickOv);
            }
            if (pickSv != null && pickSvCls == pickCls) pickScrollX = pickSv.scrollOffset.x; else pickScrollX = 0;   // picking a hero redraws the screen: keep the row where it was (it jumped back to the start, so the picked card was off-screen)
            pickId ??= G.save.hero; pickOv.Clear(); float big = touchUi ? 1.15f : 1;
            var head = L(Loc.S("เลือกฮีโร่ของคุณ", "Choose Your Hero"), 40 * big, Gold, true); head.style.marginTop = 14; Shadow(head); pickOv.Add(head);
            pickOv.Add(L(Loc.S($"{ModeName(G.save.laneMode)} · AI {LevelName(G.save.aiLevel)}", $"{ModeName(G.save.laneMode)} · AI {LevelName(G.save.aiLevel)}"), 20 * big, Muted));
            // class tabs
            var tabs = Box(); tabs.style.flexDirection = FlexDirection.Row; tabs.style.marginTop = 10; pickOv.Add(tabs);
            foreach (var c in Data.Classes)
            {
                string id = c.id; bool on = pickCls == id;
                var t = Box(on ? new Color(c.c.r, c.c.g, c.c.b, .3f) : new Color(1, 1, 1, .05f), 16); Border(t, on ? c.c : new Color(1, 1, 1, .15f), on ? 2.5f : 1.5f);
                t.style.paddingLeft = t.style.paddingRight = 14 * big; t.style.paddingTop = t.style.paddingBottom = 6 * big; t.style.marginLeft = t.style.marginRight = 4; t.pickingMode = PickingMode.Position;
                t.RegisterCallback<PointerDownEvent>(e => { pickCls = id; ShowHeroPick(); }); t.Add(L(c.th, 19 * big, on ? Color.white : Ink, on)); tabs.Add(t);
            }
            var body = Box(); body.style.flexDirection = FlexDirection.Row; body.style.flexGrow = 1; body.style.width = Length.Percent(96); body.style.marginTop = 10; pickOv.Add(body);
            // cards
            var sv = new ScrollView(ScrollViewMode.Horizontal); sv.style.flexGrow = 1; sv.style.flexShrink = 1; sv.verticalScrollerVisibility = ScrollerVisibility.Hidden; sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic; body.Add(sv);
            sv.RegisterCallback<WheelEvent>(e => { sv.scrollOffset += new Vector2(e.delta.y * 60, 0); e.StopPropagation(); });   // the mouse wheel scrolls the long row of heroes sideways
            sv.contentContainer.style.flexDirection = FlexDirection.Row; sv.contentContainer.style.alignItems = Align.Center;
            pickSv = sv; pickSvCls = pickCls; float keepX = pickScrollX; sv.schedule.Execute(() => sv.scrollOffset = new Vector2(keepX, 0)).ExecuteLater(1);
            Vector2 downAt = default; bool downOn = false;   // a card is picked by a tap: press and lift without sliding; a slide only scrolls the row
            sv.RegisterCallback<PointerDownEvent>(e => { downAt = e.position; downOn = true; }, TrickleDown.TrickleDown);
            sv.RegisterCallback<PointerUpEvent>(e =>
            {
                if (!downOn) return; downOn = false; if (((Vector2)e.position - downAt).magnitude > 18) return;
                for (var el = sv.panel?.Pick(e.position); el != null && el != sv; el = el.parent) if (el.userData is string hid) { pickId = hid; sv.schedule.Execute(() => ShowHeroPick()); return; }
            }, TrickleDown.TrickleDown);
            foreach (var h in Data.Heroes.Where(h => h.rig && Kits.For(h.id) != null && (pickCls == "all" || h.cls == pickCls)))
            {
                var hh = h; bool on = h.id == pickId; var cc = ClassCol(h.cls);
                var card = Box(new Color(.05f, .06f, .09f, .95f), 12); card.style.width = 270 * big; card.style.height = Length.Percent(92); card.style.marginRight = 12; card.style.flexShrink = 0; card.style.overflow = Overflow.Hidden;
                Border(card, on ? Gold : new Color(cc.r, cc.g, cc.b, .7f), on ? 4 : 2); card.pickingMode = PickingMode.Position;
                if (on) { card.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1)); }
                card.userData = hh.id;
                var art = Box(); art.style.flexGrow = 1; var mk = G.ModelKey(h); art.style.backgroundImage = new StyleBackground(Studio.HeroFull(mk) ?? Studio.HeroFull(h.id) ?? Studio.Hero(h.id)); art.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;   // the skin the player wears for this hero, filling the card without stretching (the scale mode used to sit inside this comment, so the art was stretched)
                art.style.backgroundColor = new Color(cc.r * .25f, cc.g * .25f, cc.b * .25f); card.Add(art);
                var plate = Box(new Color(0, 0, 0, .65f)); plate.style.paddingTop = plate.style.paddingBottom = 6; plate.style.alignItems = Align.Center; card.Add(plate);
                plate.Add(L(h.name, 22 * big, on ? Gold : Ink, true)); plate.Add(L(ClassName(h.cls), 15 * big, cc));
                sv.Add(card);
            }
            // the picked hero's kit
            var hd = Data.Heroes.FirstOrDefault(h => h.id == pickId) ?? G.hero.def; var kit = Kits.For(hd.id);
            var side = Box(new Color(.03f, .05f, .08f, .9f), 12); Border(side, new Color(1, .84f, .45f, .5f), 1.5f); Pad(side, 14); side.style.alignSelf = Align.FlexStart; side.style.width = touchUi ? 360 : 420; side.style.marginLeft = 12; side.style.flexShrink = 0; body.Add(side);
            side.Add(L(hd.name, 30 * big, Gold, true)); side.Add(L($"{ClassName(hd.cls)} · {hd.title}", 17 * big, ClassCol(hd.cls)));
            if (kit != null)
            {
                void Row(string key, string name, string text, string icon)
                {
                    var r = Box(); r.style.flexDirection = FlexDirection.Row; r.style.marginTop = 8; side.Add(r);
                    if (icon != null && Icon(icon) is Texture2D it) { var ib = Box(); ib.style.width = ib.style.height = 38 * big; ib.style.flexShrink = 0; ib.style.marginRight = 8; ib.style.backgroundImage = new StyleBackground(it); r.Add(ib); }
                    var c2 = Box(); c2.style.flexShrink = 1; r.Add(c2);
                    c2.Add(L($"{key}  {name}", 18 * big, Ink, true)); var t = L(text, 14 * big, Muted); t.style.whiteSpace = WhiteSpace.Normal; c2.Add(t);
                }
                Row(Loc.S("ติดตัว", "Passive"), kit.pn, kit.pd, null);
                for (int i = 0; i < 3; i++) Row(WebPc ? "QWE"[i].ToString() : touchUi ? (i + 1).ToString() : "UIO"[i].ToString(), kit.s[i].n, Kits.Describe(kit.s[i]), Kits.IconOf(hd.id, i));
            }
            var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.justifyContent = Justify.Center; row.style.marginTop = 10; row.style.marginBottom = 14; pickOv.Add(row);
            var go = Btn(Loc.S("เลือก ▶", "Select ▶"), () => ConfirmPick()); StyleGold(go, true); go.style.minWidth = 220; row.Add(go);
            var back = Btn(Loc.S("กลับ", "Back"), () => pickOv.style.display = DisplayStyle.None, false); StyleGold(back, false); back.style.marginLeft = 14; row.Add(back);
            pickOv.style.display = DisplayStyle.Flex;
        }
        void ConfirmPick()
        {
            pickOv.style.display = DisplayStyle.None;
            if (pickId != null && pickId != G.save.hero) { G.save.hero = pickId; G.Persist(); G.hero.Equip(); G.hero.PlaceLobby(); ShowLobby(); }
            ShowVs();
        }

        // ---------- the VS screen (the owner's cover picture: a stormy night, a moon, big heroes on each side, a fiery VS slash, gold frame) ----------
        static Texture2D vsSky;
        static Texture2D VsSky()
        {   // a stormy night sky: deep blue, cloud bands, a pale moon up the middle, darker toward the bottom
            if (vsSky) return vsSky; const int W = 256, H = 144; vsSky = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H; float n = Mathf.PerlinNoise(u * 6, v * 9) * .6f + Mathf.PerlinNoise(u * 14 + 3, v * 20) * .4f;
                var c = Color.Lerp(new Color(.02f, .03f, .06f), new Color(.1f, .16f, .27f), v * .9f) * (.75f + .5f * n);
                float m = Vector2.Distance(new Vector2(u * 1.78f, v), new Vector2(.89f, .78f)); c += new Color(.75f, .8f, .9f) * Mathf.Clamp01(1 - m / .07f) + new Color(.2f, .25f, .35f) * Mathf.Clamp01(1 - m / .3f) * .6f;
                c.a = 1; vsSky.SetPixel(x, y, c);
            }
            vsSky.Apply(); return vsSky;
        }
        public void ShowVs()
        {
            int size = G.save.laneMode == 0 ? 1 : 3; G.RollTeams(size); vsRank.Clear();
            ourTeam = Pick2(TeamNames[Random.Range(0, TeamNames.Length)]); theirTeam = Pick2(FoeNames[Random.Range(0, FoeNames.Length)]);
            if (vsOv == null) { vsOv = Box(new Color(.01f, .015f, .03f, 1)); Abs(vsOv, 0, 0, 0, 0); vsOv.pickingMode = PickingMode.Position; root.Add(vsOv); }
            vsOv.Clear(); bool phone = touchUi;
            var painted = Resources.Load<Texture2D>("UI/Vs/vs_bg"); vsOv.style.backgroundImage = new StyleBackground(painted ? painted : VsDay()); vsOv.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            void Glow(float l, float t, float w, float h, Color c) { var g = Box(); g.style.position = Position.Absolute; g.style.left = Length.Percent(l); g.style.top = Length.Percent(t); g.style.width = Length.Percent(w); g.style.height = Length.Percent(h); g.style.backgroundImage = new StyleBackground(SoftGlow()); g.style.unityBackgroundImageTintColor = c; g.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; vsOv.Add(g); }
            // our side wrapped in an icy blue spirit glow, theirs in fire and violet; smoke along the bottom
            Glow(-18, -5, 64, 90, new Color(.35f, .75f, 1f, .2f)); Glow(54, -5, 64, 90, new Color(1f, .45f, .25f, .16f));   // daylight: only a breath of each side's colour
            // optional painted art (drop-in PNGs under Resources/UI/Vs): a wolf spirit rising behind our side, a flame behind theirs
            VisualElement Art(string name, float l, float t, float w, float h, bool flip)
            {
                var tex = Resources.Load<Texture2D>("UI/Vs/" + name); if (!tex) return null;
                var a = Box(); a.style.position = Position.Absolute; a.style.left = Length.Percent(l); a.style.top = Length.Percent(t); a.style.width = Length.Percent(w); a.style.height = Length.Percent(h);
                a.style.backgroundImage = new StyleBackground(tex); a.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit; if (flip) a.style.scale = new Scale(new Vector3(-1, 1, 1)); a.style.opacity = 0; vsOv.Add(a); return a;
            }
            vsSpirit0 = Art("wolf_spirit", 3, -4, 36, 60, false); vsSpirit1 = Art("foe_spirit", 61, -4, 36, 60, false);   // high up: the wolf's and the skull's heads clear of the heroes' heads   // the ice wolf roars toward the enemy, the demon skull back at us
            // the cliffs both teams stand on, the chasm between them misty
            if (!painted) { vsOv.Add(Banners()); vsOv.Add(ArenaFloor()); }   // one bright stone arena for both teams (the dark cliffs and chasm were the night look)   // a deep dark chasm, faint mist at its bottom
            // the heroes, big and overlapping, cut off by the plates (the plates are drawn after them)
            var stage = Box(); stage.style.position = Position.Absolute; stage.style.left = 0; stage.style.right = 0; stage.style.top = Length.Percent(phone ? 14 : 13); stage.style.bottom = Length.Percent(phone ? 9 : 8); stage.style.flexDirection = FlexDirection.Row; vsOv.Add(stage);
            var us = new List<HeroDef> { G.hero.def }; us.AddRange(G.nextAllies ?? new List<HeroDef>()); var them = G.nextEnemies ?? new List<HeroDef>();
            MakePoses(us, them); vsOurCount = us.Count;
            stage.Add(vsSide0 = Heroes(us, 0)); var gap = Box(); gap.style.width = Length.Percent(6); stage.Add(gap); stage.Add(vsSide1 = Heroes(them, 1));
            vsSide0.style.opacity = vsSide1.style.opacity = 0;   // they slide in from the edges (UpdVs)
            // the fiery slash, embers, the burning VS
            vsBolt = null; vsOv.Add(Embers()); vsFlashK = 0;   // a lightning bolt splits the two sides (no VS letters: the owner liked it cleaner)
            vsFlash = Box(new Color(.85f, .92f, 1f)); vsFlash.style.position = Position.Absolute; vsFlash.style.left = vsFlash.style.right = vsFlash.style.top = vsFlash.style.bottom = 0; vsFlash.style.opacity = 0; vsFlash.pickingMode = PickingMode.Ignore; vsOv.Add(vsFlash);   // the flash as VS slams in
            if (!painted) vsOv.Add(Blossoms());
            // title: the game's name, and the mode between two gold rules
            var title = Box(); title.style.position = Position.Absolute; title.style.left = 0; title.style.right = 0; title.style.top = Length.Percent(2.5f); title.style.alignItems = Align.Center; vsOv.Add(title);
            var t1 = L("NOVA BLADE", phone ? 54 : 72, new Color(1, .95f, .82f), true); t1.style.unityTextOutlineWidth = .28f; t1.style.unityTextOutlineColor = new Color(.42f, .24f, .04f); Shadow(t1, 12); title.Add(t1);
            var leg = L("LEGENDS", phone ? 22 : 28, new Color(1f, .82f, .42f), true); leg.style.letterSpacing = 6; leg.style.unityTextOutlineWidth = .3f; leg.style.unityTextOutlineColor = new Color(.25f, .13f, .02f); Shadow(leg, 8); title.Add(leg);
            var mrow = Box(); mrow.style.flexDirection = FlexDirection.Row; mrow.style.alignItems = Align.Center; mrow.style.marginTop = 4; title.Add(mrow);
            VisualElement Rule(bool left) { var r = Box(); r.style.width = phone ? 70 : 110; r.style.height = 2; r.style.backgroundImage = new StyleBackground(Grad(left ? new Color(.85f, .66f, .32f, 0) : new Color(.85f, .66f, .32f), left ? new Color(.85f, .66f, .32f) : new Color(.85f, .66f, .32f, 0), false)); return r; }
            mrow.Add(Rule(true)); var ml = L(ModeName(G.save.laneMode), phone ? 24 : 30, Color.white, true); ml.style.marginLeft = ml.style.marginRight = 14; ml.style.unityTextOutlineWidth = .25f; ml.style.unityTextOutlineColor = new Color(.08f, .1f, .22f); Shadow(ml, 8); mrow.Add(ml); mrow.Add(Rule(false));
            // name plates over the heroes' legs
            var plates = Box(); plates.style.position = Position.Absolute; plates.style.left = Length.Percent(3); plates.style.right = Length.Percent(3); plates.style.bottom = Length.Percent(phone ? 17 : 17); plates.style.flexDirection = FlexDirection.Row; plates.style.justifyContent = Justify.SpaceBetween; vsOv.Add(plates);
            plates.Add(Plates(us, 0, ourTeam, phone)); plates.Add(Plates(them, 1, theirTeam, phone));
            // the loading bar: a gold-capped bar with the percentage inside it
            var bot = Box(); bot.style.position = Position.Absolute; bot.style.left = Length.Percent(32); bot.style.right = Length.Percent(32); bot.style.bottom = Length.Percent(4); bot.style.alignItems = Align.Center; vsOv.Add(bot);
            var cap = L(Loc.S("กำลังเข้าสู่สนามรบ...", "Entering the battlefield..."), phone ? 22 : 24, new Color(1, .95f, .85f), true); Shadow(cap); bot.Add(cap);
            var brow = Box(); brow.style.flexDirection = FlexDirection.Row; brow.style.alignItems = Align.Center; brow.style.width = Length.Percent(100); brow.style.marginTop = 6; bot.Add(brow);
            VisualElement Gem() { var g = Box(new Color(.85f, .66f, .32f)); g.style.width = g.style.height = 14; g.style.rotate = new Rotate(45); g.style.flexShrink = 0; Border(g, new Color(.3f, .2f, .05f), 1.5f); return g; }
            brow.Add(Gem());
            var bar = Box(new Color(.05f, .02f, 0, .85f), 10); bar.style.flexGrow = 1; bar.style.height = phone ? 26 : 28; bar.style.marginLeft = bar.style.marginRight = 4; Border(bar, new Color(.85f, .66f, .32f), 2.5f); brow.Add(bar);
            vsFill = Box(new Color(1, .55f, .15f), 8); vsFill.style.height = Length.Percent(100); vsFill.style.width = 0; vsFill.style.backgroundImage = new StyleBackground(Grad(new Color(1, .82f, .3f), new Color(.95f, .35f, .05f), true)); bar.Add(vsFill);
            vsPct = L("", phone ? 18 : 19, Color.white, true); vsPct.style.unityTextOutlineWidth = .25f; vsPct.style.unityTextOutlineColor = new Color(.35f, .1f, 0); vsPct.style.position = Position.Absolute; vsPct.style.left = 0; vsPct.style.right = 0; vsPct.style.top = 0; vsPct.style.bottom = 0; vsPct.style.unityTextAlign = TextAnchor.MiddleCenter; bar.Add(vsPct);
            brow.Add(Gem());
            FrameOrnaments(vsOv, phone ? 120 : 160, 0);
            var edge = Box(); edge.style.position = Position.Absolute; edge.style.left = edge.style.top = edge.style.right = edge.style.bottom = 10; Border(edge, new Color(.85f, .66f, .32f, .8f), 2); vsOv.Add(edge);
            vsT = 0; vsOn = true; vsOv.style.display = DisplayStyle.Flex;
        }
        // one side's heroes, big and overlapping: the lead (the player on our side) in front and tallest, the others behind and outside
        VisualElement Heroes(List<HeroDef> team, int t)
        {
            var side = Box(); side.style.width = Length.Percent(47); side.style.height = Length.Percent(100);
            int[] order = { 1, 2, 0 };   // drawn back to front: the side heroes first, the lead last (on top)
            foreach (int i in order)
            {
                if (i >= team.Count) continue; var h = team[i]; bool lead = i == 0;
                var art = Box(); art.style.position = Position.Absolute; art.style.bottom = 0;
                float w = lead ? 70 : 58, x = lead ? 12 : i == 1 ? -14 : 50; if (t == 1) x = 100 - x - w;
                art.style.left = Length.Percent(x); art.style.width = Length.Percent(w); art.style.height = Length.Percent(lead ? 106 : 94); art.style.bottom = Length.Percent(lead ? -12 : -8);   // a little smaller than before, to leave the spirits room above them   // feet go under the plates
                var shade = Box(); shade.style.position = Position.Absolute; shade.style.left = Length.Percent(x + w * .2f); shade.style.width = Length.Percent(w * .6f);
                shade.style.bottom = Length.Percent((lead ? -12 : -8) + (lead ? 106 : 94) * .035f - 2); shade.style.height = Length.Percent(6);
                shade.style.backgroundImage = new StyleBackground(SoftGlow()); shade.style.unityBackgroundImageTintColor = new Color(0, 0, .03f, 1); shade.pickingMode = PickingMode.Ignore; side.Add(shade);
                var core = Box(); core.style.position = Position.Absolute; core.style.left = Length.Percent(x + w * .32f); core.style.width = Length.Percent(w * .36f); core.style.bottom = Length.Percent((lead ? -12 : -8) + (lead ? 106 : 94) * .035f - .5f); core.style.height = Length.Percent(3); core.style.backgroundImage = new StyleBackground(SoftGlow()); core.style.unityBackgroundImageTintColor = new Color(0, 0, .03f, 1); core.pickingMode = PickingMode.Ignore; side.Add(core);
                var pose = PoseOf(t, i, vsOurCount); art.style.backgroundImage = new StyleBackground(pose ? pose : Studio.HeroFull(h.id) ?? Studio.Hero(h.id)); art.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                if (!lead) art.style.unityBackgroundImageTintColor = new Color(.85f, .86f, .9f);   // the ones behind a little darker
                if (t == 1 && !pose) art.style.scale = new Scale(new Vector3(-1, 1, 1));
                side.Add(art);
            }
            return side;
        }
        // a side's name plates: a team crest, level, the hero's name and team, the rank badge; framed in the hero's class colour
        VisualElement Plates(List<HeroDef> team, int t, string teamName, bool phone)
        {
            var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.width = Length.Percent(44); row.style.justifyContent = t == 0 ? Justify.FlexStart : Justify.FlexEnd;
            int[] at = t == 0 ? new[] { 1, 0, 2 } : new[] { 2, 0, 1 };   // the same left-to-right order as the heroes above them
            var tc = t == 0 ? new Color(.55f, .85f, 1) : new Color(1, .55f, .5f);
            foreach (int ix in at)
            {
                if (ix >= team.Count) continue; var h = team[ix]; bool me = t == 0 && h == G.hero.def; var cc = ClassCol(h.cls);
                var p = Box(); p.style.backgroundImage = new StyleBackground(Grad(new Color(cc.r * .72f, cc.g * .72f, cc.b * .72f, .9f), new Color(cc.r * .3f, cc.g * .3f, cc.b * .3f, .92f), true)); Round(p, 6);
                Border(p, me ? Gold : new Color(Mathf.Min(1, cc.r * .6f + .45f), Mathf.Min(1, cc.g * .6f + .4f), Mathf.Min(1, cc.b * .6f + .25f)), me ? 3 : 2);
                p.style.width = Length.Percent(32); p.style.marginLeft = p.style.marginRight = 4; p.style.paddingTop = p.style.paddingBottom = 5; p.style.paddingLeft = p.style.paddingRight = 6;
                var lv = L("Lv.1", phone ? 13 : 14, new Color(1, .9f, .7f), true); lv.style.position = Position.Absolute; lv.style.left = 6; lv.style.top = 3; p.Add(lv);
                var n = L(h.name + (me ? Loc.S(" (คุณ)", " (you)") : ""), phone ? 24 : 26, me ? Gold : Color.white, true); n.style.unityTextAlign = TextAnchor.MiddleCenter; n.style.marginTop = phone ? 8 : 10; Shadow(n, 4); p.Add(n);
                var line = Box(); line.style.height = 1; line.style.marginTop = 3; line.style.marginBottom = 3; line.style.marginLeft = line.style.marginRight = 10; line.style.backgroundImage = new StyleBackground(Grad(new Color(1, .85f, .5f, 0), new Color(1, .85f, .5f, .8f), false)); p.Add(line);
                var bottom = Box(); bottom.style.flexDirection = FlexDirection.Row; bottom.style.alignItems = Align.Center; bottom.style.justifyContent = Justify.SpaceBetween; p.Add(bottom);
                var crest = Box(new Color(0, 0, 0, .45f), 12); crest.style.width = crest.style.height = phone ? 22 : 24; crest.style.flexShrink = 0; Border(crest, tc, 1.5f); crest.style.alignItems = Align.Center; crest.style.justifyContent = Justify.Center;
                var cl = Box(tc, 4); cl.style.width = cl.style.height = phone ? 8 : 9; cl.style.rotate = new Rotate(45); crest.Add(cl); bottom.Add(crest);
                int rp = RankPts(h, t); var (rn, rc, _) = RankOf(rp);
                var mid = Box(); mid.style.alignItems = Align.Center; mid.style.flexGrow = 1; bottom.Add(mid);
                mid.Add(L(teamName, phone ? 13 : 14, tc, true)); mid.Add(L($"{ClassName(h.cls)} · {rn}", phone ? 12 : 13, rc, true));
                bottom.Add(RankBadge(rp, phone ? 20 : 22));
                row.Add(p);
            }
            return row;
        }
        void StartFromVs() { if (!vsOn) return; vsOn = false; vsOv.style.display = DisplayStyle.None; G.StartBattle(); }
        void UpdVs(float dt)
        {
            if (!vsOn) return;
            dt = Mathf.Min(dt, .05f); UpdFire(dt); vsT += dt; VsIntro();   // the first frame carries the poses' render time; don't let it skip the bar
            float k = Mathf.Clamp01((vsT - .4f) / 4f);   // the bar fills by 4.4 s
            vsFill.style.width = Length.Percent(k * 100);
            vsPct.text = $"{k * 100:0}%";
            if (vsT > 5.5f) StartFromVs();   // no start button: the match begins by itself at 5.5 s (the owner wanted time to look at it)
        }
        // the opening: both teams slide in from the screen edges, then lightning strikes down between them with a flash, and the spirits rise behind them;
        // after that the spirits breathe (a slow glow with a flicker) and the lightning strikes again now and then
        void VsIntro()
        {
            static float Ease(float x) { x = Mathf.Clamp01(x); return 1 - (1 - x) * (1 - x) * (1 - x); }
            float tm = Time.unscaledTime, sl = Ease(vsT / .5f);
            if (vsSide0 != null) { vsSide0.style.translate = new Translate(Length.Percent(-70 * (1 - sl)), 0); vsSide0.style.opacity = sl; }
            if (vsSide1 != null) { vsSide1.style.translate = new Translate(Length.Percent(70 * (1 - sl)), 0); vsSide1.style.opacity = sl; }
            float sp = Ease((vsT - .6f) / .6f);
            void Spirit(VisualElement e, float ph)
            {
                if (e == null) return; float p = .5f + .5f * Mathf.Sin(tm * 2.4f + ph);
                e.style.opacity = sp * (.68f + .18f * p + .12f * Mathf.PerlinNoise(tm * 7, ph));   // dim, glowing in and out, a flicker like flame
                float sc = .96f + .04f * sp + .02f * p; e.style.scale = new Scale(new Vector3(sc, sc, 1));
                e.style.translate = new Translate(0, Length.Percent(4 * (1 - sp) - 1.2f * p));   // rises a little as it breathes
            }
            Spirit(vsSpirit0, 0); Spirit(vsSpirit1, 1.7f);
            float hit = vsBolt != null ? UpdLightning(Mathf.Min(Time.unscaledDeltaTime, .05f), vsT) : 0; vsFlashK = Mathf.Max(vsFlashK * Mathf.Pow(.02f, Time.unscaledDeltaTime), hit * .6f);   // a white flash with each strike, gone in about a quarter second
            if (vsFlash != null) vsFlash.style.opacity = vsFlashK; vsBolt?.MarkDirtyRepaint();
        }
        public bool VsShown => vsOn;
        public void TestPick(string id, string cls) { pickId = id; pickCls = cls; ShowHeroPick(); }   // tests
        public void TestConfirm() => ConfirmPick();
    }
}
