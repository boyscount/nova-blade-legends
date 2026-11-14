using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // The end of a lane battle: "Battle Chronicle" (owner's reference and brief, 2026-11-12). Page 1:
    // left, the player's hero posed with its weapon in its class's aura, the MVP crest when earned, its name, title and a closing line;
    // right, a big VICTORY (or DEFEAT) with the match time, then Battle Record (K/D/A, damage dealt, damage taken, healing, towers),
    // Battle Rewards (gold, EXP, the rank bar moving), Legendary Moments (only the medals earned, a tip on each) and the Battle Chronicle
    // (first blood, your multi-kill, the first tower, the Guardian, the end, with their times); buttons All Stats, Play Again, Lobby.
    // Everything counts up / fades in (UpdEnd). Page 2 is every hero of both sides. The numbers come from Main (stats, chron, side counts).
    public partial class Ui
    {
        VisualElement endOv, endTip; Label endTipL;
        static readonly Color EndGold = new(1f, .82f, .42f), EndBlue = new(.5f, .78f, 1f), EndRed = new(1f, .5f, .45f), EndNavy = new(.05f, .06f, .14f, .88f);
        Main.HeroStat Mvp(bool win)
        {
            var side = G.stats.Where(s => s.team == (win ? 0 : 1)).ToList(); if (side.Count == 0) side = G.stats; return side.OrderByDescending(s => s.Score).FirstOrDefault();
        }
        Texture2D HeroArt(Main.HeroStat s)
        {
            if (s.me && G.hero) return Studio.HeroFull(G.hero.modelKey) ?? Studio.HeroFull(G.hero.def.id);
            var h = Data.Heroes.FirstOrDefault(x => x.name == s.name); return h != null ? Studio.HeroFull(h.id) : null;
        }
        static string Dmg(float v) => v >= 1000 ? $"{v / 1000:0.0}K" : $"{v:0}";
        static string Clock(float t) { int s = Mathf.RoundToInt(t); return $"{s / 60:00}:{s % 60:00}"; }
        // the medals earned (kind, name, what it was for): only what happened this match
        List<(string kind, string name, string what)> Feats(Main.HeroStat me, Main.HeroStat mvp)
        {
            var f = new List<(string, string, string)>(); if (me == null) return f;
            if (mvp == me) f.Add(("mvp", Loc.S("ผู้เล่นยอดเยี่ยม", "MVP"), Loc.S("ผลงานดีที่สุดในทีม", "The best in your team")));
            if (G.myFirstBlood) f.Add(("firstblood", Loc.S("เลือดแรก", "First Blood"), Loc.S("ฆ่าคนแรกของเกม", "The match's first kill")));
            if (G.bestMulti >= 3) f.Add(("triple", Loc.S("ทริปเปิลคิล", "Triple Kill"), Loc.S("ฆ่า 3 ตัวติดกัน", "Three kills in a row"))); else if (G.bestMulti == 2) f.Add(("double", Loc.S("ดับเบิลคิล", "Double Kill"), Loc.S("ฆ่า 2 ตัวติดกัน", "Two kills in a row")));
            if (G.bestSpree >= 3) f.Add(("spree", Loc.S("กำลังเดือด", "Killing Spree"), Loc.S($"ฆ่าต่อเนื่อง {G.bestSpree} ไม่ตาย", $"{G.bestSpree} kills without dying")));
            if (me.towers > 0) f.Add(("tower", Loc.S("ทลายป้อม", "Tower Breaker"), Loc.S($"ทำลายป้อม {me.towers}", $"{me.towers} towers brought down")));
            if (me.bosses > 0) f.Add(("guardian", Loc.S("ผู้ล้มผู้พิทักษ์", "Guardian Slayer"), Loc.S("ตีบอสกลางป่าตัวสุดท้าย", "Felled the Sky Guardian")));
            if (me.d == 0) f.Add(("undying", Loc.S("ไม่ตายสักครั้ง", "Undying"), Loc.S("ทั้งเกมไม่ตาย", "Never fell all match")));
            if (me.camps >= 3) f.Add(("jungle", Loc.S("เจ้าป่า", "Jungle Lord"), Loc.S($"เคลียร์ค่ายป่า {me.camps}", $"{me.camps} camps cleared")));
            if (G.stats.Count > 1 && G.stats.Max(s => s.dmg) <= me.dmg && me.dmg > 0) f.Add(("topdmg", Loc.S("ดาเมจสูงสุด", "Top Damage"), Loc.S("ดาเมจมากที่สุดในเกม", "The most damage of all")));
            return f;
        }
        static Color MomentCol(string k) => k switch { "mvp" => new Color(1f, .78f, .3f), "firstblood" => new Color(.85f, .2f, .22f), "double" or "triple" => new Color(.6f, .35f, .95f), "spree" => new Color(.95f, .62f, .2f), "tower" => new Color(.9f, .7f, .35f), "guardian" or "jungle" => new Color(.3f, .8f, .45f), "undying" => new Color(.3f, .85f, .6f), "topdmg" => new Color(.95f, .35f, .3f), "victory" => new Color(1f, .8f, .3f), "defeat" => new Color(.55f, .6f, .75f), _ => new Color(.8f, .8f, .9f) };
        // ---- drawn icons for medals and the chronicle ----
        static void Crown(MeshGenerationContext ctx, Color c)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y); p.fillColor = c;
            p.BeginPath(); p.MoveTo(P(.1f, .8f)); p.LineTo(P(.06f, .3f)); p.LineTo(P(.3f, .52f)); p.LineTo(P(.5f, .18f)); p.LineTo(P(.7f, .52f)); p.LineTo(P(.94f, .3f)); p.LineTo(P(.9f, .8f)); p.ClosePath(); p.Fill();
            p.BeginPath(); p.MoveTo(P(.1f, .84f)); p.LineTo(P(.9f, .84f)); p.LineTo(P(.9f, .94f)); p.LineTo(P(.1f, .94f)); p.ClosePath(); p.Fill();
            foreach (var x in new[] { .06f, .5f, .94f }) { p.BeginPath(); p.Arc(P(x, x == .5f ? .14f : .26f), w * .06f, 0, 360); p.Fill(); }
        }
        static void Skull(MeshGenerationContext ctx, Color c)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y); p.fillColor = c;
            p.BeginPath(); p.Arc(P(.5f, .42f), w * .34f, 0, 360); p.Fill();
            p.BeginPath(); p.MoveTo(P(.32f, .62f)); p.LineTo(P(.68f, .62f)); p.LineTo(P(.64f, .92f)); p.LineTo(P(.36f, .92f)); p.ClosePath(); p.Fill();
            p.fillColor = new Color(.12f, .06f, .05f); p.BeginPath(); p.Arc(P(.37f, .44f), w * .09f, 0, 360); p.Fill(); p.BeginPath(); p.Arc(P(.63f, .44f), w * .09f, 0, 360); p.Fill();
            p.BeginPath(); p.MoveTo(P(.5f, .56f)); p.LineTo(P(.45f, .66f)); p.LineTo(P(.55f, .66f)); p.ClosePath(); p.Fill();
        }
        static void Gem2(MeshGenerationContext ctx, Color c)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y);
            p.fillColor = c; p.BeginPath(); p.MoveTo(P(.5f, .04f)); p.LineTo(P(.9f, .42f)); p.LineTo(P(.5f, .98f)); p.LineTo(P(.1f, .42f)); p.ClosePath(); p.Fill();
            p.fillColor = Color.Lerp(c, Color.white, .5f); p.BeginPath(); p.MoveTo(P(.5f, .04f)); p.LineTo(P(.66f, .42f)); p.LineTo(P(.34f, .42f)); p.ClosePath(); p.Fill();
        }
        static void MomentIcon(MeshGenerationContext ctx, string kind, Color c)
        {
            switch (kind)
            {
                case "mvp": case "victory": Crown(ctx, c); break;
                case "firstblood": case "double": case "triple": case "topdmg": Swords(ctx, c); break;
                case "spree": case "defeat": Skull(ctx, c); break;
                case "tower": DrawCastle(ctx, c, new Color(.1f, .08f, .2f)); break;
                case "undying": PerkIcon(ctx, "vigor", c); break;
                default: Sparkle(ctx, c); break;
            }
        }
        VisualElement Icon(System.Action<MeshGenerationContext> draw, float s) { var e = Box(); e.style.width = e.style.height = s; e.style.flexShrink = 0; e.pickingMode = PickingMode.Ignore; e.generateVisualContent += draw; return e; }
        VisualElement EndPanel(float w)
        {   // page 2's panels: the painted violet card with gold corners
            var p = Box(new Color(.1f, .07f, .22f, .92f), 18); Border(p, new Color(EndGold.r, EndGold.g, EndGold.b, .55f), 2); p.style.width = w; Pad(p, 14);
            if (Resources.Load<Texture2D>("UI/Battle/card") is Texture2D card)
            {
                p.style.backgroundImage = new StyleBackground(card); p.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; p.style.backgroundColor = Color.clear;
                p.style.unitySliceLeft = p.style.unitySliceRight = p.style.unitySliceTop = p.style.unitySliceBottom = 40; p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 0; Pad(p, 34); p.style.paddingLeft = p.style.paddingRight = 48; p.style.paddingTop = p.style.paddingBottom = 30;
            }
            return p;
        }
        Button EndBtn(string text, System.Action act, bool primary)
        {
            var b = Btn(text, act, primary); StyleGold(b, primary); b.style.minWidth = 200; b.style.marginLeft = b.style.marginRight = 8; return b;
        }
        public void ShowLaneResult(bool win)
        {
            resultOv.style.display = DisplayStyle.None; bannerT = 0; bannerA.text = bannerB.text = "";   // the old "Victory! Rank points" banner showed through
            if (endOv == null) { endOv = Box(new Color(.02f, .02f, .06f, .9f)); Abs(endOv, 0, 0, 0, 0); endOv.pickingMode = PickingMode.Position; root.Add(endOv); }
            endPose = null; EndPage1(win); endOv.style.display = DisplayStyle.Flex;
        }
        public void TestEndPage2(bool win) => EndPage2(win);
        public void HideLaneResult() { if (endOv != null) endOv.style.display = DisplayStyle.None; endAnimOn = false; }
        // ---- the entrance: what fades in when, what counts up ----
        float endAnim; bool endAnimOn; Texture2D endPose;
        readonly List<(VisualElement e, float at, float dy)> endIn = new();
        readonly List<(Label l, float at, float to, System.Func<float, string> fmt)> endCount = new();
        readonly List<(VisualElement e, float at)> endPop = new();
        (VisualElement fill, float from, float to, float at) endRank;
        void In(VisualElement e, float at, float dy = 24) { e.style.opacity = 0; endIn.Add((e, at, dy)); }
        void Count(Label l, float at, float to, System.Func<float, string> fmt) { l.text = fmt(0); endCount.Add((l, at, to, fmt)); }
        void UpdEnd(float dt)
        {
            if (!endAnimOn || endOv == null || endOv.style.display != DisplayStyle.Flex) return;
            endAnim += dt; float t = endAnim; bool done = true;
            foreach (var (e, at, dy) in endIn) { float k = Mathf.Clamp01((t - at) / .4f), ease = 1 - (1 - k) * (1 - k); e.style.opacity = ease; e.style.translate = new Translate(0, dy * (1 - ease), 0); if (k < 1) done = false; }
            foreach (var (l, at, to, fmt) in endCount) { float k = Mathf.Clamp01((t - at) / .9f), ease = 1 - (1 - k) * (1 - k) * (1 - k); l.text = fmt(to * ease); if (k < 1) done = false; }
            foreach (var (e, at) in endPop) { float k = Mathf.Clamp01((t - at) / .3f), s = k < .7f ? Mathf.Lerp(.3f, 1.15f, k / .7f) : Mathf.Lerp(1.15f, 1, (k - .7f) / .3f); e.style.opacity = Mathf.Clamp01(k * 2); e.style.scale = new Scale(new Vector3(s, s, 1)); if (k < 1) done = false; }
            if (endRank.fill != null) { float k = Mathf.Clamp01((t - endRank.at) / 1f), ease = 1 - (1 - k) * (1 - k); endRank.fill.style.width = Length.Percent(Mathf.Lerp(endRank.from, endRank.to, ease) * 100); if (k < 1) done = false; }
            if (done) endAnimOn = false;
        }
        // the screen takes the colours of the hero's order (STORY-LORE.md): top and bottom of the backdrop, the glow behind the hero, its aura;
        // a painted backdrop per class (Resources/UI/EndBg/<class>.png, END-BG-PROMPTS.md) is used instead when there is one
        static (Color top, Color bottom, Color glow, Color aura) EndTheme(string cls) => cls switch
        {
            "warrior" => (new Color(.34f, .06f, .07f), new Color(.04f, .02f, .03f), new Color(.95f, .25f, .2f), new Color(.85f, .88f, .95f)),   // Iron Vale: crimson and steel
            "assassin" => (new Color(.3f, .08f, .2f), new Color(.03f, .01f, .03f), new Color(1f, .45f, .72f), new Color(1f, .6f, .82f)),     // Shadow Blossom: black and sakura pink
            "archer" => (new Color(.08f, .22f, .1f), new Color(.02f, .04f, .02f), new Color(.4f, .85f, .35f), new Color(1f, .82f, .4f)),     // Greenwood: moss and leaf gold
            "cleric" => (new Color(.07f, .11f, .32f), new Color(.02f, .03f, .08f), new Color(1f, .86f, .5f), new Color(1f, .95f, .8f)),     // Sun and Moon: deep blue and gold
            "monk" => (new Color(.34f, .15f, .04f), new Color(.05f, .02f, .01f), new Color(1f, .6f, .2f), new Color(1f, .75f, .35f)),       // Golden Flame: ember and bronze
            "mage" => (new Color(.08f, .1f, .36f), new Color(.04f, .02f, .1f), new Color(.5f, .42f, 1f), new Color(.55f, .75f, 1f)),       // Azure Spire: azure and violet
            _ => (new Color(.12f, .06f, .24f), new Color(.02f, .02f, .07f), new Color(.55f, .3f, .95f), new Color(.6f, .5f, 1f)),
        };
        void EndPage1(bool win)
        {
            endOv.Clear(); endIn.Clear(); endCount.Clear(); endPop.Clear(); endRank = default; endAnim = 0; endAnimOn = true;
            float big = touchUi ? 1.1f : 1; var me = G.stats.FirstOrDefault(s => s.me); var mvp = Mvp(win); var hd = G.hero ? G.hero.def : null;
            var head = win ? EndGold : new Color(.72f, .78f, .95f); var cls = hd != null ? Data.Class(hd.cls).c : EndBlue;
            var th = EndTheme(hd != null ? hd.cls : ""); var painted = hd != null ? Resources.Load<Texture2D>("UI/EndBg/" + hd.cls) : null;
            endOv.style.backgroundColor = Color.clear;
            if (painted)
            {   // the order's painted scene, darkened on the right where the record sits
                endOv.style.backgroundImage = new StyleBackground(painted); endOv.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                var shade = Box(); Abs(shade, 0, 0, 0, 0); shade.style.backgroundImage = new StyleBackground(Grad(new Color(0, 0, 0, .05f), new Color(.02f, .02f, .05f, .82f), false)); shade.pickingMode = PickingMode.Ignore; endOv.Add(shade);
            }
            else endOv.style.backgroundImage = new StyleBackground(Grad(new Color(th.top.r, th.top.g, th.top.b, 1), new Color(th.bottom.r, th.bottom.g, th.bottom.b, 1), true));
            var glowL = Box(); Abs(glowL, -60, 0, null, 0); glowL.style.width = Length.Percent(55); glowL.style.backgroundImage = new StyleBackground(SoftGlow()); glowL.style.unityBackgroundImageTintColor = new Color(th.glow.r, th.glow.g, th.glow.b, painted ? .2f : .38f); glowL.pickingMode = PickingMode.Ignore; endOv.Add(glowL);
            endTip = null;
            var cols = Box(); Abs(cols, 0, 0, 0, 0); cols.style.flexDirection = FlexDirection.Row; endOv.Add(cols);
            // ---- left: the hero showcase ----
            var left = Box(); left.style.width = Length.Percent(38); left.style.alignItems = Align.Center; left.style.justifyContent = Justify.FlexEnd; left.style.paddingBottom = 28; cols.Add(left);
            var aura = Box(); Abs(aura, 0, 4, 0, 6); aura.style.backgroundImage = new StyleBackground(SoftGlow()); aura.style.unityBackgroundImageTintColor = new Color(th.aura.r, th.aura.g, th.aura.b, .45f); aura.pickingMode = PickingMode.Ignore; left.Add(aura);   // the class's aura
            if (endPose == null && hd != null && hd.rig) { var poses = Studio.VsPoses(new List<(HeroDef, float, string)> { (hd, -.01f, G.hero.modelKey) }); endPose = poses.Count > 0 ? poses[0] : null; }
            var pose = Box(); Abs(pose, 0, 2, 0, 18); pose.style.backgroundImage = new StyleBackground(endPose ? endPose : (me != null ? HeroArt(me) : null)); pose.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit; pose.pickingMode = PickingMode.Ignore; left.Add(pose); In(pose, .15f, 0);
            if (mvp != null && mvp == me)
            {   // the MVP emblem (UiMvp): winged, over the hero's head
                var crest = MvpEmblem(big * (touchUi ? .85f : .9f)); crest.style.position = Position.Absolute; crest.style.left = Length.Percent(50); crest.style.top = 6;
                crest.style.translate = new Translate(Length.Percent(-50), 0); left.Add(crest);
                pose.style.top = 150 * big;   // the hero stands under the emblem, not behind it
                endPop.Add((crest, .5f)); crest.style.opacity = 0; crest.schedule.Execute(() => Sfx.Play("ui_mvp")).StartingIn(500);
            }
            if (hd != null)
            {
                var nb = Box(); nb.style.alignItems = Align.Center; left.Add(nb); In(nb, .35f);
                var nm = L(hd.name, 40 * big, new Color(1f, .94f, .82f), true); nm.style.unityTextOutlineWidth = .2f; nm.style.unityTextOutlineColor = new Color(.1f, .05f, .15f); nb.Add(nm);
                nb.Add(L(hd.title, 18 * big, new Color(.85f, .8f, .95f)));
                var q = L(win ? Loc.S("“อีกหนึ่งชัยชนะ… สู่ตำนานที่ยิ่งใหญ่กว่า”", "“Another victory… for a greater legend.”") : Loc.S("“พ่ายวันนี้… เพื่อแกร่งกว่าในวันหน้า”", "“Fallen today… stronger tomorrow.”"), 17 * big, new Color(.9f, .86f, .78f)); q.style.unityFontStyleAndWeight = FontStyle.Italic; q.style.marginTop = 10; nb.Add(q);
            }
            // ---- right: the record ----
            var right = Box(); right.style.width = Length.Percent(60); right.style.paddingTop = 12; right.style.paddingRight = 24; cols.Add(right);
            var hdr = Box(); hdr.style.alignItems = Align.Center; right.Add(hdr);
            var halo = Box(); halo.style.position = Position.Absolute; halo.style.left = Length.Percent(15); halo.style.right = Length.Percent(15); halo.style.top = -20; halo.style.height = 150 * big; halo.style.backgroundImage = new StyleBackground(SoftGlow()); halo.style.unityBackgroundImageTintColor = win ? new Color(1f, .7f, .25f, .5f) : new Color(.4f, .5f, .9f, .35f); halo.pickingMode = PickingMode.Ignore; hdr.Add(halo);
            var title = L(win ? Loc.S("ชัยชนะ", "VICTORY") : Loc.S("พ่ายแพ้", "DEFEAT"), 84 * big, head, true); title.style.letterSpacing = 6; title.style.unityTextOutlineWidth = .22f; title.style.unityTextOutlineColor = new Color(.3f, .12f, .02f);
            title.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 16, color = win ? new Color(1f, .65f, .15f, .9f) : new Color(.3f, .4f, .8f, .8f) }; hdr.Add(title); endPop.Add((hdr, 0)); hdr.style.opacity = 0;
            hdr.Add(L(win ? Loc.S("·  ชนะการต่อสู้แล้ว  ·", "·  THE BATTLE HAS BEEN WON  ·") : Loc.S("·  ยังไม่ใช่วันของเรา  ·", "·  NOT OUR DAY  ·"), 18 * big, new Color(.95f, .88f, .7f), true));
            var dur = Box(); dur.style.position = Position.Absolute; dur.style.right = 24; dur.style.top = 12; dur.style.alignItems = Align.FlexEnd; right.Add(dur);
            dur.Add(L(Clock(G.time), 30 * big, Color.white, true)); dur.Add(L(Loc.S("เวลาแมตช์", "MATCH DURATION"), 13 * big, new Color(.8f, .78f, .9f), true));
            VisualElement Section(string name, string kind, float at)
            {
                var s = Box(); s.style.marginTop = 10; right.Add(s); In(s, at);
                var h = Box(); h.style.flexDirection = FlexDirection.Row; h.style.alignItems = Align.Center; s.Add(h);
                var hi = Box(new Color(.25f, .14f, .05f, 1), 14); Border(hi, EndGold, 1.5f); hi.style.width = hi.style.height = 26 * big; hi.style.alignItems = Align.Center; hi.style.justifyContent = Justify.Center; hi.Add(Icon(ctx => MomentIcon(ctx, kind, EndGold), 16 * big)); h.Add(hi);
                var hl = L(name, 18 * big, EndGold, true); hl.style.marginLeft = 8; hl.style.letterSpacing = 1; h.Add(hl);
                var body = Box(EndNavy, 10); Border(body, new Color(EndGold.r, EndGold.g, EndGold.b, .45f), 1.5f); body.style.marginTop = 4; body.style.flexDirection = FlexDirection.Row; body.style.alignItems = Align.Center; body.style.paddingLeft = body.style.paddingRight = 16; body.style.paddingTop = body.style.paddingBottom = 8; s.Add(body);
                return body;
            }
            VisualElement Divider() { var d = Box(new Color(EndGold.r, EndGold.g, EndGold.b, .3f)); d.style.width = 1; d.style.alignSelf = Align.Stretch; d.style.marginLeft = d.style.marginRight = 10; return d; }
            VisualElement Cell(VisualElement parent, System.Action<MeshGenerationContext> icon, float grow)
            {
                var c = Box(); c.style.flexGrow = grow; c.style.alignItems = Align.Center; parent.Add(c); if (icon != null) c.Add(Icon(icon, 26 * big)); return c;
            }
            // Battle Record
            var rec = Section(Loc.S("บันทึกการรบ", "BATTLE RECORD"), "firstblood", .3f);
            {
                var kd = Cell(rec, null, 1.6f); var kl = L("", 44 * big, Color.white, true); kd.Add(kl); kd.Add(L(Loc.S("ฆ่า / ตาย / ช่วย", "KILLS / DEATHS / ASSISTS"), 12 * big, new Color(.8f, .8f, .92f), true));
                if (me != null) { var m = me; Count(kl, .45f, 1, k => $"{Mathf.RoundToInt(m.k * k)} / {Mathf.RoundToInt(m.d * k)} / {Mathf.RoundToInt(m.a * k)}"); }
                void Stat(System.Action<MeshGenerationContext> ic, float v, string cap)
                {
                    rec.Add(Divider()); var c = Cell(rec, ic, 1); var l = L("", 26 * big, Color.white, true); c.Add(l); c.Add(L(cap, 11 * big, new Color(.8f, .8f, .92f), true)); Count(l, .5f, v, x => Dmg(x));
                }
                Stat(ctx => Swords(ctx, new Color(.9f, .88f, .95f)), me?.dmg ?? 0, Loc.S("ดาเมจที่ทำ", "DAMAGE DEALT"));
                Stat(ctx => PerkIcon(ctx, "armor", new Color(.85f, .88f, .95f)), me?.taken ?? 0, Loc.S("ดาเมจที่รับ", "DAMAGE TAKEN"));
                Stat(ctx => PerkIcon(ctx, "vigor", new Color(.95f, .8f, .82f)), me?.heal ?? 0, Loc.S("ฟื้นเลือด", "HEALING"));
                rec.Add(Divider()); var tc = Cell(rec, ctx => DrawCastle(ctx, new Color(.9f, .88f, .95f), EndNavy), 1); var tl = L("", 26 * big, Color.white, true); tc.Add(tl); tc.Add(L(Loc.S("ป้อม", "TOWERS"), 11 * big, new Color(.8f, .8f, .92f), true)); Count(tl, .5f, me?.towers ?? 0, x => $"{Mathf.RoundToInt(x)}");
            }
            // Battle Rewards
            var rw = Section(Loc.S("รางวัลจากการรบ", "BATTLE REWARDS"), "mvp", .45f);
            {
                rw.Add(Coin(34 * big)); var gc = Box(); gc.style.marginLeft = 8; gc.style.marginRight = 22; rw.Add(gc); var gl = L("", 26 * big, new Color(1f, .88f, .5f), true); gc.Add(gl); gc.Add(L(Loc.S("ทอง", "GOLD"), 11 * big, new Color(.8f, .8f, .92f), true)); Count(gl, .65f, G.gold, x => $"+{Mathf.RoundToInt(x)}");
                rw.Add(Icon(ctx => Gem2(ctx, new Color(.35f, .65f, 1f)), 32 * big)); var xc = Box(); xc.style.marginLeft = 8; rw.Add(xc); var xl = L("", 26 * big, new Color(.65f, .85f, 1f), true); xc.Add(xl); xc.Add(L("EXP", 11 * big, new Color(.8f, .8f, .92f), true)); Count(xl, .7f, G.runXp, x => $"+{Mathf.RoundToInt(x)}");
                rw.Add(Divider());
                int now = G.save.rankPts, was = G.rankWas; var (rn, rc, tier) = RankOf(now); int lo = Ranks[tier].min, hi = tier + 1 < Ranks.Length ? Ranks[tier + 1].min : lo + 400;
                rw.Add(RankBadge(now, 42 * big)); var rcol = Box(); rcol.style.flexGrow = 1; rcol.style.marginLeft = 10; rw.Add(rcol);
                var rt = Box(); rt.style.flexDirection = FlexDirection.Row; rcol.Add(rt); rt.Add(L(rn, 20 * big, rc, true)); int delta = now - was; var dl = L((delta >= 0 ? "+" : "") + delta, 20 * big, delta >= 0 ? new Color(.5f, 1f, .6f) : EndRed, true); dl.style.marginLeft = 10; rt.Add(dl);
                var tr = Box(new Color(.18f, .14f, .32f, 1), 5); tr.style.height = 10; tr.style.marginTop = 4; rcol.Add(tr); var fill = Box(rc, 5); fill.style.height = 10; tr.Add(fill);
                float f0 = Mathf.Clamp01((Mathf.Clamp(was, lo, hi) - lo) / (float)(hi - lo)), f1 = Mathf.Clamp01((now - lo) / (float)(hi - lo)); fill.style.width = Length.Percent(f0 * 100); endRank = (fill, f0, f1, .9f);
            }
            // Legendary Moments: only the medals earned, popping in one by one, a tip on hover / tap
            var feats = Feats(me, mvp);
            if (feats.Count > 0)
            {
                var lm = Section(Loc.S("ช่วงเวลาแห่งตำนาน", "LEGENDARY MOMENTS"), "mvp", .6f); lm.style.justifyContent = Justify.SpaceAround;
                int i = 0;
                foreach (var (kind, name, what) in feats.Take(5))
                {
                    var c = MomentCol(kind); var m = Box(); m.style.alignItems = Align.Center; m.style.width = 120 * big; m.pickingMode = PickingMode.Position; lm.Add(m);
                    var disc = Box(new Color(c.r * .35f, c.g * .35f, c.b * .35f, 1), 40); Border(disc, EndGold, 2.5f); disc.style.width = disc.style.height = 58 * big; disc.style.alignItems = Align.Center; disc.style.justifyContent = Justify.Center; disc.pickingMode = PickingMode.Ignore; m.Add(disc);
                    string k = kind; disc.Add(Icon(ctx => MomentIcon(ctx, k, Color.Lerp(c, Color.white, .35f)), 34 * big));
                    var n = L(Loc.En ? name.ToUpperInvariant() : name, 14 * big, EndGold, true); n.style.marginTop = 4; n.style.unityTextAlign = TextAnchor.MiddleCenter; n.pickingMode = PickingMode.Ignore; m.Add(n);
                    string tipText = what; m.RegisterCallback<PointerEnterEvent>(e => ShowTip(m, tipText)); m.RegisterCallback<PointerLeaveEvent>(e => HideTip()); m.RegisterCallback<PointerDownEvent>(e => ShowTip(m, tipText));
                    m.style.opacity = 0; endPop.Add((m, 1.0f + i * .15f)); i++;
                }
            }
            // Battle Chronicle: the big moments of this match on a line
            var events = Chronicle(win);
            if (events.Count > 0)
            {
                var ch = Section(Loc.S("บันทึกตำนาน", "BATTLE CHRONICLE"), "guardian", .75f); ch.style.paddingTop = ch.style.paddingBottom = 10;
                for (int i = 0; i < events.Count; i++)
                {
                    var (t, kind, label) = events[i]; var c = MomentCol(kind);
                    if (i > 0) { var line = Box(new Color(c.r, c.g, c.b, .7f)); line.style.height = 2; line.style.flexGrow = 1; line.style.marginBottom = 30 * big; ch.Add(line); }
                    var node = Box(); node.style.alignItems = Align.Center; node.style.width = 120 * big; ch.Add(node);
                    string k = kind; node.Add(Icon(ctx => MomentIcon(ctx, k, c), 30 * big));
                    var nl = L(label, 14 * big, Color.white, true); nl.style.unityTextAlign = TextAnchor.MiddleCenter; node.Add(nl); node.Add(L(Clock(t), 12 * big, new Color(.8f, .8f, .92f)));
                }
            }
            // buttons
            var bt = Box(); bt.style.flexDirection = FlexDirection.Row; bt.style.justifyContent = Justify.Center; bt.style.marginTop = 14; right.Add(bt); In(bt, 1.1f, 12);
            bt.Add(EndBtn(Loc.S("สถิติทั้งหมด", "All Stats"), () => EndPage2(win), false));
            bt.Add(EndBtn(Loc.S("เล่นอีกครั้ง", "Play Again"), () => { HideLaneResult(); G.StartLane(G.laneSize, G.laneCount); }, true));
            bt.Add(EndBtn(Loc.S("กลับล็อบบี้", "Lobby"), () => { HideLaneResult(); G.LeaveStage(); }, false));
            endTip = Box(new Color(.08f, .06f, .16f, .96f), 8); Border(endTip, EndGold, 1.5f); endTip.style.position = Position.Absolute; endTip.style.paddingLeft = endTip.style.paddingRight = 10; endTip.style.paddingTop = endTip.style.paddingBottom = 4; endTip.pickingMode = PickingMode.Ignore;
            endTipL = L("", 15 * big, Color.white, true); endTip.Add(endTipL); endTip.style.display = DisplayStyle.None; endOv.Add(endTip);
            UpdEnd(0);
        }
        void ShowTip(VisualElement over, string text)
        {
            if (endTip == null) return; endTipL.text = text; var b = over.worldBound; var o = endOv.worldBound;
            endTip.style.left = b.x - o.x; endTip.style.top = b.y - o.y - 34; endTip.style.display = DisplayStyle.Flex;
        }
        void HideTip() { if (endTip != null) endTip.style.display = DisplayStyle.None; }
        // the chronicle's nodes: first blood, the player's multi-kill, our first tower, the Guardian falls, and the end (at most 5, in order)
        List<(float t, string kind, string label)> Chronicle(bool win)
        {
            var o = new List<(float, string, string)>(); var ev = G.chron;
            var fb = ev.FirstOrDefault(e => e.kind == "firstblood"); if (fb.kind != null) o.Add((fb.t, "firstblood", Loc.S("เลือดแรก", "First Blood")));
            var mk = ev.Where(e => e.kind is "double" or "triple").OrderByDescending(e => e.kind == "triple").FirstOrDefault(); if (mk.kind != null) o.Add((mk.t, mk.kind, mk.kind == "triple" ? Loc.S("ทริปเปิลคิล", "Triple Kill") : Loc.S("ดับเบิลคิล", "Double Kill")));
            var tw = ev.FirstOrDefault(e => e.kind == "tower" && e.side == 0); if (tw.kind != null) o.Add((tw.t, "tower", Loc.S("ทำลายป้อม", "Tower Destroyed")));
            var gd = ev.FirstOrDefault(e => e.kind == "guardian" && e.side == 0); if (gd.kind != null) o.Add((gd.t, "guardian", Loc.S("ล้มผู้พิทักษ์", "Guardian Slain")));
            o = o.OrderBy(x => x.Item1).Take(4).ToList();
            o.Add((G.time, win ? "victory" : "defeat", win ? Loc.S("ชัยชนะ", "Victory") : Loc.S("พ่ายแพ้", "Defeat")));
            return o;
        }
        void EndPage2(bool win)
        {
            endOv.Clear(); endAnimOn = false; endOv.style.backgroundImage = StyleKeyword.None; endOv.style.backgroundColor = new Color(.02f, .02f, .06f, .9f); float big = touchUi ? 1.1f : 1; var mvp = Mvp(win);
            var col = Box(); Abs(col, 0, 0, 0, 0); col.style.alignItems = Align.Center; col.style.justifyContent = Justify.Center; endOv.Add(col);
            col.Add(L(Loc.S("สถิติทั้งหมด", "Match Stats"), 40 * big, EndGold, true));
            col.Add(L(Loc.S($"เวลา {Clock(G.time)} · ป้อม {G.sideTowers[0]} : {G.sideTowers[1]} · ค่ายป่า {G.sideCamps[0]} : {G.sideCamps[1]} · บอส {G.sideBosses[0]} : {G.sideBosses[1]} · ฆ่า {G.teamKills[0]} : {G.teamKills[1]}",
                $"Time {Clock(G.time)} · towers {G.sideTowers[0]} : {G.sideTowers[1]} · camps {G.sideCamps[0]} : {G.sideCamps[1]} · Guardian {G.sideBosses[0]} : {G.sideBosses[1]} · kills {G.teamKills[0]} : {G.teamKills[1]}"), 18 * big, Color.white, true));
            var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 12; col.Add(row);
            float top = G.stats.Count > 0 ? Mathf.Max(1, G.stats.Max(s => s.dmg)) : 1;
            foreach (int t in new[] { 0, 1 })
            {
                var p = EndPanel(540 * big); p.style.marginLeft = p.style.marginRight = 8; row.Add(p);
                bool won = (t == 0) == win; p.Add(L((t == 0 ? Loc.S("ทีมฟ้า (เรา)", "Blue (us)") : Loc.S("ทีมแดง", "Red")) + "  " + (won ? Loc.S("· ชนะ", "· won") : Loc.S("· แพ้", "· lost")), 22 * big, t == 0 ? EndBlue : EndRed, true));
                var hd = Box(); hd.style.flexDirection = FlexDirection.Row; hd.style.marginTop = 6; p.Add(hd);
                void Cell(VisualElement r, string s, float w, Color c, bool b = false, int size = 16) { var l = L(s, size * big, c, b); l.style.width = Length.Percent(w); r.Add(l); }
                Cell(hd, Loc.S("ฮีโร่", "Hero"), 40, new Color(.75f, .75f, .9f)); Cell(hd, "Lv", 10, new Color(.75f, .75f, .9f)); Cell(hd, "K/D/A", 22, new Color(.75f, .75f, .9f)); Cell(hd, Loc.S("ดาเมจ", "Damage"), 28, new Color(.75f, .75f, .9f));
                foreach (var s in G.stats.Where(x => x.team == t).OrderByDescending(x => x.Score))
                {
                    var r = Box(s.me ? new Color(1, .84f, .45f, .14f) : Color.clear, 8); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.style.marginTop = 4; r.style.paddingTop = r.style.paddingBottom = 3; p.Add(r);
                    Cell(r, (s == mvp ? "◆ " : "") + s.name + (s.me ? Loc.S(" (คุณ)", " (you)") : ""), 40, s == mvp ? EndGold : Color.white, true, 18);
                    Cell(r, G.LevelOf(s).ToString(), 10, Color.white); Cell(r, $"{s.k}/{s.d}/{s.a}", 22, Color.white, true);
                    var dc = Box(); dc.style.width = Length.Percent(28); r.Add(dc); dc.Add(L(Dmg(s.dmg), 15 * big, Color.white));
                    var bar = Box(t == 0 ? EndBlue : EndRed, 3); bar.style.height = 6; bar.style.width = Length.Percent(Mathf.Clamp01(s.dmg / top) * 100); dc.Add(bar);
                }
            }
            var bt = Box(); bt.style.flexDirection = FlexDirection.Row; bt.style.marginTop = 14; col.Add(bt);
            bt.Add(EndBtn(Loc.S("◀ กลับ", "◀ Back"), () => EndPage1(win), false));
            bt.Add(EndBtn(Loc.S("เล่นอีกครั้ง", "Play Again"), () => { HideLaneResult(); G.StartLane(G.laneSize, G.laneCount); }, true));
            bt.Add(EndBtn(Loc.S("กลับล็อบบี้", "Lobby"), () => { HideLaneResult(); G.LeaveStage(); }, false));
        }
    }
}
