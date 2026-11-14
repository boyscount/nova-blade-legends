using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // The level-up pick (Perks), in two steps (owner, 2026-11-12: "it did not say what to do"):
    // 1) a pulsing gold "Pick a power!" button above the skill buttons, out of the fight's way; P on a keyboard
    // 2) tapped: a glowing LEVEL UP!, three cards over a darkened screen, each with its category (crimson offense, gold utility, emerald
    //    defense), a picture, the rarity, the name, what it does, what it changes for this hero ("Health 1404 -> 1572") and a gold Select;
    //    hover lifts and lights a card, a legendary one has light running round its frame; picking flies the card up in sparks.
    //    One reroll per pick; "Later" goes back to the button; a random pick when the time runs out (or the button is left alone).
    // Presentation only: the powers themselves are Perks (gameplay), taken through Perks.Take.
    public partial class Ui
    {
        VisualElement perkOv, perkBtn, perkBar; Label perkBtnL; string[] perkIds; int perkLevel; float perkT, perkPend; bool perkOpen, perkRerolled;
        const float PickTime = 15, PendTime = 30, ChooseTime = .45f;
        public bool PerksOpen => perkOpen;
        public bool PerkPending => perkIds != null;
        public void HidePerks() { perkIds = null; perkOpen = false; perkChosen = -1; if (perkOv != null) perkOv.style.display = DisplayStyle.None; if (perkBtn != null) perkBtn.style.display = DisplayStyle.None; }
        // a pick is due: show the button (the cards come when it is tapped)
        public void ShowPerks(string[] ids, int level)
        {
            perkIds = ids; perkLevel = level; perkPend = PendTime; perkOpen = false; perkRerolled = false; perkChosen = -1; float big = touchUi ? 1.25f : 1;
            if (perkBtn == null)
            {
                perkBtn = Box(new Color(.42f, .26f, .04f, .95f), 26); Border(perkBtn, new Color(1f, .84f, .45f), 3); perkBtn.style.flexDirection = FlexDirection.Row; perkBtn.style.alignItems = Align.Center;
                perkBtn.style.paddingLeft = perkBtn.style.paddingRight = 16 * big; perkBtn.style.paddingTop = perkBtn.style.paddingBottom = 8 * big; perkBtn.pickingMode = PickingMode.Position;
                Abs(perkBtn, null, null, touchUi ? 60 : 40, touchUi ? 640 : 380);
                var up = Box(); up.style.width = up.style.height = 30 * big; up.style.marginRight = 8; up.pickingMode = PickingMode.Ignore;
                up.generateVisualContent += ctx =>
                {   // a bold up arrow: level up
                    var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; p.fillColor = new Color(1f, .9f, .55f);
                    p.BeginPath(); p.MoveTo(new Vector2(w * .5f, 0)); p.LineTo(new Vector2(w, h * .5f)); p.LineTo(new Vector2(w * .68f, h * .5f)); p.LineTo(new Vector2(w * .68f, h)); p.LineTo(new Vector2(w * .32f, h)); p.LineTo(new Vector2(w * .32f, h * .5f)); p.LineTo(new Vector2(0, h * .5f)); p.ClosePath(); p.Fill();
                };
                perkBtn.Add(up); perkBtnL = L("", 22 * big, new Color(1f, .95f, .8f), true); perkBtnL.pickingMode = PickingMode.Ignore; perkBtn.Add(perkBtnL);
                perkBtn.RegisterCallback<PointerDownEvent>(e => { OpenPerks(); e.StopPropagation(); }); hud.Add(perkBtn);
            }
            perkBtnL.text = Loc.S("เลือกพลัง!", "Pick a power!") + (touchUi ? "" : "  [P]"); perkBtn.style.display = DisplayStyle.Flex;
            if (perkOv != null) perkOv.style.display = DisplayStyle.None;
            Banner(Loc.S($"เลเวล {level}!", $"Level {level}!"), Loc.S("แตะปุ่ม \"เลือกพลัง!\" เพื่อรับพลังใหม่", "Tap \"Pick a power!\" to take a new power"));
        }
        // ---- the cards ----
        VisualElement perkTitle; readonly List<VisualElement> perkCards = new(), perkShimmer = new(); VisualElement perkSpark; float perkAnim, perkChooseT; int perkChosen = -1;
        static string CatName(string c) => c == "offense" ? Loc.S("โจมตี", "OFFENSE") : c == "defense" ? Loc.S("ป้องกัน", "DEFENSE") : Loc.S("สนับสนุน", "UTILITY");
        static Color CatCol(string c) => c == "offense" ? new Color(.92f, .24f, .3f) : c == "defense" ? new Color(.22f, .86f, .56f) : new Color(1f, .8f, .32f);   // crimson, emerald, gold
        static string RareName(int r) => r >= 3 ? Loc.S("ตำนาน", "LEGENDARY") : r == 2 ? Loc.S("หายากมาก", "EPIC") : Loc.S("หายาก", "RARE");
        static Color RareCol(int r) => r >= 3 ? new Color(1f, .78f, .3f) : r == 2 ? new Color(.78f, .5f, 1f) : new Color(.45f, .7f, 1f);
        // what the power changes for this hero, before it is taken
        string PerkPreview(string id)
        {
            var h = G.hero; if (!h) return "";
            switch (id)
            {
                case "might": return Loc.S($"ดาเมจ {h.AtkPow:0} → {h.AtkPow * Perks.MightK:0}", $"Damage {h.AtkPow:0} → {h.AtkPow * Perks.MightK:0}");
                case "vigor": return Loc.S($"เลือด {h.max:0} → {h.max * Perks.VigorK:0}", $"Health {h.max:0} → {h.max * Perks.VigorK:0}");
                case "haste": { float cd = h.SkillCdMax(0); return Loc.S($"คูลดาวน์สกิล {cd:0.0} → {cd / Perks.HasteK:0.0} วิ", $"Skill cooldown {cd:0.0} → {cd / Perks.HasteK:0.0} s"); }
                case "swift": return Loc.S("ความเร็ว 100% → 112%", "Speed 100% → 112%");
                case "armor": return Loc.S("ดาเมจที่รับ 100% → 88%", "Damage taken 100% → 88%");
                case "siege": return Loc.S("ดาเมจใส่ป้อม 100% → 130%", "Tower damage 100% → 130%");
                case "leech": return Loc.S("ฟื้นเลือดจากการตี 0% → 8%", "Healing from hits 0% → 8%");
                case "ward": return Loc.S("โล่ตอนเลือดต่ำ: ไม่มี → 3 วิ", "Low-health shield: none → 3 s");
            }
            return "";
        }
        void OpenPerks()
        {
            if (perkIds == null) return; perkOpen = true; perkT = PickTime; perkAnim = 0; perkChosen = -1; float big = touchUi ? 1.2f : 1; perkBtn.style.display = DisplayStyle.None;
            if (perkOv == null) { perkOv = Box(new Color(.01f, .01f, .04f, .8f)); Abs(perkOv, 0, 0, 0, 0); perkOv.style.alignItems = Align.Center; perkOv.style.justifyContent = Justify.Center; perkOv.pickingMode = PickingMode.Position; hud.Add(perkOv); }
            perkOv.Clear(); perkCards.Clear(); perkShimmer.Clear(); bannerT = 0;
            var gold = new Color(1f, .82f, .42f);
            // 1) LEVEL UP! with a golden glow behind it, CHOOSE YOUR POWER under it, the time bar
            perkTitle = Box(); perkTitle.style.alignItems = Align.Center; perkOv.Add(perkTitle);
            var halo = Box(); halo.style.position = Position.Absolute; halo.style.width = 520 * big; halo.style.height = 150 * big; halo.style.top = -36 * big; halo.style.left = Length.Percent(50); halo.style.marginLeft = -260 * big;
            halo.style.backgroundImage = new StyleBackground(SoftGlow()); halo.style.unityBackgroundImageTintColor = new Color(1f, .7f, .25f, .45f); halo.pickingMode = PickingMode.Ignore; perkTitle.Add(halo);
            var title = L(Loc.S("เลเวลอัป!", "LEVEL UP!"), 56 * big, gold, true); title.style.unityTextOutlineWidth = .22f; title.style.unityTextOutlineColor = new Color(.3f, .13f, .02f); title.style.letterSpacing = 4;
            title.style.textShadow = new TextShadow { offset = new Vector2(0, 0), blurRadius = 14, color = new Color(1f, .7f, .2f, .9f) }; perkTitle.Add(title);
            perkTitle.Add(L(Loc.S($"เลือกพลังของคุณ · เลเวล {perkLevel}", $"CHOOSE YOUR POWER · LEVEL {perkLevel}"), 20 * big, new Color(.9f, .86f, .98f), true));
            var track = Box(new Color(.2f, .16f, .38f, .9f), 4); track.style.width = 380 * big; track.style.height = 6; track.style.marginTop = 8; perkTitle.Add(track);
            perkBar = Box(gold, 4); perkBar.style.height = 6; perkBar.style.width = Length.Percent(100); track.Add(perkBar);
            // 2) the cards
            var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 16; row.style.overflow = Overflow.Visible; perkOv.Add(row);
            var frame = Resources.Load<Texture2D>("UI/Battle/card"); var goldBtn = Resources.Load<Texture2D>("UI/Battle/btn_gold");
            for (int i = 0; i < perkIds.Length; i++)
            {
                int k = i; var pk = Perks.Get(perkIds[i]); if (pk == null) continue; var cc = CatCol(pk.cat);
                var c = Box(new Color(.1f, .07f, .2f, .98f), 18); c.style.width = 268 * big; c.style.marginLeft = c.style.marginRight = 10 * big; c.style.alignItems = Align.Center; c.style.overflow = Overflow.Visible; c.pickingMode = PickingMode.Position;
                c.style.paddingLeft = c.style.paddingRight = 22 * big; c.style.paddingTop = 22 * big; c.style.paddingBottom = 20 * big;
                var glow = Box(); glow.name = "glow"; Abs(glow, -36, -36, -36, -36); glow.style.backgroundImage = new StyleBackground(SoftGlow()); glow.style.unityBackgroundImageTintColor = new Color(cc.r, cc.g, cc.b, .7f); glow.style.opacity = 0; glow.pickingMode = PickingMode.Ignore; c.Add(glow);   // lights up on hover
                if (frame)
                {   // the painted violet card with gold corners (the game's own frame)
                    c.style.backgroundImage = new StyleBackground(frame); c.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; c.style.backgroundColor = Color.clear;
                    c.style.unitySliceLeft = c.style.unitySliceRight = c.style.unitySliceTop = c.style.unitySliceBottom = 40;
                }
                else Border(c, new Color(gold.r, gold.g, gold.b, .6f), 2);
                var tag = Box(new Color(cc.r * .22f, cc.g * .22f, cc.b * .22f, .95f), 10); Border(tag, new Color(cc.r, cc.g, cc.b, .6f), 1); tag.style.paddingLeft = tag.style.paddingRight = 12; tag.style.paddingTop = 1; tag.style.paddingBottom = 2; tag.pickingMode = PickingMode.Ignore;
                tag.Add(L(CatName(pk.cat), 14 * big, cc, true)); c.Add(tag);
                var tile = Box(new Color(.05f, .04f, .1f, 1), 14); tile.style.width = tile.style.height = 180 * big; tile.style.marginTop = 8; tile.style.overflow = Overflow.Hidden; tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center; tile.pickingMode = PickingMode.Ignore;
                Border(tile, new Color(gold.r, gold.g, gold.b, .35f), 1.5f);
                var art = Resources.Load<Texture2D>("UI/Perks/" + pk.id);
                if (art) { tile.style.backgroundImage = new StyleBackground(art); tile.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; }
                else
                {   // no painting yet: a magic circle in the category's colour round the drawn icon
                    var gl = Box(); Abs(gl, 0, 0, 0, 0); gl.style.backgroundImage = new StyleBackground(SoftGlow()); gl.style.unityBackgroundImageTintColor = new Color(cc.r, cc.g, cc.b, .55f); gl.pickingMode = PickingMode.Ignore; tile.Add(gl);
                    var ring = Box(); ring.style.width = ring.style.height = 134 * big; ring.style.alignItems = Align.Center; ring.style.justifyContent = Justify.Center; ring.pickingMode = PickingMode.Ignore;
                    Color rc = cc; ring.generateVisualContent += ctx => MagicCircle(ctx, rc, gold); tile.Add(ring);
                    var ic = Box(); ic.style.width = ic.style.height = 70 * big; ic.pickingMode = PickingMode.Ignore; string pid = pk.id; Color pc = Color.Lerp(pk.col, Color.white, .25f); ic.generateVisualContent += ctx => PerkIcon(ctx, pid, pc); ring.Add(ic);
                }
                c.Add(tile);
                var rr = L("◆ " + RareName(pk.rare), 14 * big, RareCol(pk.rare), true); rr.style.marginTop = 8; rr.pickingMode = PickingMode.Ignore; c.Add(rr);
                var n = L(Loc.En ? pk.name.ToUpperInvariant() : pk.name, 28 * big, Color.white, true); n.style.letterSpacing = 1; n.pickingMode = PickingMode.Ignore; c.Add(n);
                var t = L(pk.desc, 16 * big, new Color(.86f, .86f, .96f)); t.style.whiteSpace = WhiteSpace.Normal; t.style.unityTextAlign = TextAnchor.UpperCenter; t.style.marginTop = 2; t.style.minHeight = 40 * big; t.pickingMode = PickingMode.Ignore; c.Add(t);
                var pv = Box(new Color(.1f, .3f, .16f, .7f), 8); pv.style.alignSelf = Align.Stretch; pv.style.marginTop = 4; pv.style.paddingTop = pv.style.paddingBottom = 3; pv.pickingMode = PickingMode.Ignore;   // what changes for this hero
                var pvl = L(PerkPreview(pk.id), 14 * big, new Color(.6f, 1f, .7f), true); pvl.style.unityTextAlign = TextAnchor.MiddleCenter; pvl.style.whiteSpace = WhiteSpace.Normal; pvl.pickingMode = PickingMode.Ignore; pv.Add(pvl); c.Add(pv);
                var b = Box(new Color(.78f, .48f, .12f), 10); b.style.alignSelf = Align.Stretch; b.style.marginTop = 10; b.style.height = 50 * big; b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center; b.pickingMode = PickingMode.Ignore;
                if (goldBtn)
                {
                    b.style.backgroundImage = new StyleBackground(goldBtn); b.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; b.style.backgroundColor = Color.clear;
                    b.style.unitySliceLeft = b.style.unitySliceRight = 60; b.style.unitySliceTop = b.style.unitySliceBottom = 30;
                }
                b.Add(L(Loc.S("เลือก", "SELECT") + (touchUi ? "" : $"  [{i + 1}]"), 22 * big, new Color(.28f, .13f, .01f), true)); c.Add(b);
                if (pk.rare >= 3)
                {   // legendary: a spark of light running round the frame
                    var sh = Box(); Abs(sh, 0, 0, 0, 0); sh.pickingMode = PickingMode.Ignore; sh.generateVisualContent += ctx => Shimmer(ctx); c.Add(sh); perkShimmer.Add(sh);
                }
                c.RegisterCallback<PointerDownEvent>(e => { ChoosePerk(k); e.StopPropagation(); });
                c.RegisterCallback<PointerEnterEvent>(e => { if (perkChosen < 0) { c.style.scale = new Scale(Vector3.one * 1.05f); c.Q("glow").style.opacity = 1; } });
                c.RegisterCallback<PointerLeaveEvent>(e => { if (perkChosen < 0) { c.style.scale = new Scale(Vector3.one); c.Q("glow").style.opacity = 0; } });
                c.style.opacity = 0; row.Add(c); perkCards.Add(c);
            }
            // 3) reroll (once a pick) and later
            var bottom = Box(); bottom.style.flexDirection = FlexDirection.Row; bottom.style.marginTop = 14; perkOv.Add(bottom);
            VisualElement Small(string text, bool on, System.Action act)
            {
                var l = L(text, 18 * big, on ? new Color(.95f, .9f, 1f) : new Color(.6f, .6f, .7f), true); l.style.marginLeft = l.style.marginRight = 8; l.style.paddingLeft = l.style.paddingRight = 18; l.style.paddingTop = l.style.paddingBottom = 5;
                l.style.backgroundColor = on ? new Color(.26f, .2f, .46f, .9f) : new Color(.18f, .18f, .24f, .8f); Round(l, 14); if (on) Border(l, new Color(gold.r, gold.g, gold.b, .5f), 1.5f); l.pickingMode = PickingMode.Position;
                l.RegisterCallback<PointerDownEvent>(e => { act(); e.StopPropagation(); }); bottom.Add(l); return l;
            }
            Small(perkRerolled ? Loc.S("สุ่มใหม่ (ใช้แล้ว)", "Reroll (used)") : Loc.S("สุ่มใหม่ (1 ครั้ง)", "Reroll (1 left)"), !perkRerolled, RerollPerks);
            Small(Loc.S("ไว้ทีหลัง", "Later"), true, () => { perkOpen = false; perkOv.style.display = DisplayStyle.None; perkBtn.style.display = DisplayStyle.Flex; });
            // sparks over the chosen card (drawn while it flies)
            perkSpark = Box(); Abs(perkSpark, 0, 0, 0, 0); perkSpark.pickingMode = PickingMode.Ignore; perkSpark.generateVisualContent += ctx => Sparks(ctx); perkOv.Add(perkSpark);
            perkOv.style.display = DisplayStyle.Flex; AnimPerks(0);
        }
        void RerollPerks()
        {
            if (perkRerolled || perkChosen >= 0 || perkIds == null) return;
            string[] o = perkIds; for (int tries = 0; tries < 8; tries++) { o = Perks.Offer(Perks.Me); if (o.Length > 0 && string.Join(",", o) != string.Join(",", perkIds)) break; }
            float keep = perkT; perkIds = o; perkRerolled = true; OpenPerks(); perkT = keep;
        }
        // a card is picked: it lifts and grows in a burst of sparks, the others fade; the power is taken when that is done
        void ChoosePerk(int k)
        {
            if (perkChosen >= 0 || perkIds == null || k < 0 || k >= perkCards.Count) return;
            perkChosen = k; perkChooseT = 0;
        }
        void AnimChoose(float dt)
        {
            perkChooseT += dt; float e = Mathf.Clamp01(perkChooseT / ChooseTime), ease = 1 - (1 - e) * (1 - e);
            for (int i = 0; i < perkCards.Count; i++)
            {
                var c = perkCards[i];
                if (i == perkChosen) { float s = 1.05f + .12f * ease; c.style.scale = new Scale(new Vector3(s, s, 1)); c.style.translate = new Translate(0, -40 * ease, 0); c.Q("glow").style.opacity = 1; }
                else { c.style.opacity = 1 - ease; c.style.scale = new Scale(Vector3.one * (1 - .1f * ease)); }
            }
            perkSpark?.MarkDirtyRepaint();
            if (e >= 1) PickPerk(perkChosen);
        }
        // the title pops in (big to normal), then each card rises and fades in, a little after the one before
        void AnimPerks(float dt)
        {
            perkAnim += dt; float tk = Mathf.Clamp01(perkAnim / .35f), ts = 1 + .35f * (1 - tk) * (1 - tk);
            if (perkTitle != null) { perkTitle.style.scale = new Scale(new Vector3(ts, ts, 1)); perkTitle.style.opacity = tk; }
            for (int i = 0; i < perkCards.Count; i++)
            {
                float ck = Mathf.Clamp01((perkAnim - .15f - i * .1f) / .3f), e = 1 - (1 - ck) * (1 - ck);
                perkCards[i].style.opacity = e; perkCards[i].style.translate = new Translate(0, (1 - e) * 60, 0);
            }
        }
        public void PickPerk(int i)
        {
            if (perkIds == null || perkIds.Length == 0) return; i = Mathf.Clamp(i, 0, perkIds.Length - 1); var p = Perks.Get(perkIds[i]);
            Perks.Take(Perks.Me, perkIds[i]); HidePerks();
            if (G.hero)
            {   // the power's colour rises round the hero
                G.hero.RefreshGear(); var c = p != null ? CatCol(p.cat) : Color.white;
                Fx.Ring(G.hero.Pos, 2.6f, c, .7f, .25f); Fx.Burst(G.hero.Pos + Vector3.up, 50, c, 6, .9f, -.7f, .15f, true); Fx.Burst(G.hero.Pos + Vector3.up * .3f, 30, new Color(1f, .85f, .45f), 4, .8f, -1.2f, .1f, true);
            }
            if (p != null) Banner(Loc.S($"ได้พลัง: {p.name}", $"New power: {p.name}"), p.desc);
            G.PerkCheck();   // another one due (two levels at once)
        }
        // test hooks
        public void TestOpenPerks() => OpenPerks();
        public void TestHoverPerk(int i) { if (i >= 0 && i < perkCards.Count) { perkCards[i].style.scale = new Scale(Vector3.one * 1.05f); perkCards[i].Q("glow").style.opacity = 1; } }
        public void TestReroll() => RerollPerks();
        public void TestChoosePerk(int i) => ChoosePerk(i);
        void UpdPerks(float dt)
        {
            if (perkIds == null) return;
            if (!G || !G.SkyMatch || G.mode != Main.Mode.Game) { HidePerks(); return; }
            if (G.paused) return;
            var kb = Keyboard.current;
            if (!perkOpen)
            {   // the button pulses; P opens; left alone, a random pick
                perkPend -= dt; float s = 1 + .06f * Mathf.Sin(Time.time * 6); perkBtn.style.scale = new Scale(new Vector3(s, s, 1));
                if (kb != null && kb.pKey.wasPressedThisFrame) { OpenPerks(); return; }
                if (perkPend <= 0) PickPerk(Random.Range(0, perkIds.Length));
                return;
            }
            if (perkAnim < 1) AnimPerks(dt);
            foreach (var sh in perkShimmer) sh.MarkDirtyRepaint();
            if (perkChosen >= 0) { AnimChoose(dt); return; }
            perkT -= dt; if (perkBar != null) perkBar.style.width = Length.Percent(Mathf.Clamp01(perkT / PickTime) * 100);
            if (kb != null) { if (kb.digit1Key.wasPressedThisFrame) { ChoosePerk(0); return; } if (kb.digit2Key.wasPressedThisFrame) { ChoosePerk(1); return; } if (kb.digit3Key.wasPressedThisFrame) { ChoosePerk(2); return; } }
            if (perkT <= 0) ChoosePerk(Random.Range(0, perkCards.Count));
        }
        // ---- drawn effects ----
        static void MagicCircle(MeshGenerationContext ctx, Color c, Color gold)
        {   // two rings with runes ticks between them, in the category's colour, turning slowly
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; var m = new Vector2(r.width / 2, r.height / 2); float R = r.width * .47f, a0 = Time.time * 20;
            p.lineWidth = 2; p.strokeColor = new Color(c.r, c.g, c.b, .9f); p.BeginPath(); p.Arc(m, R, 0, 360); p.Stroke();
            p.strokeColor = new Color(gold.r, gold.g, gold.b, .6f); p.lineWidth = 1.2f; p.BeginPath(); p.Arc(m, R * .82f, 0, 360); p.Stroke();
            p.strokeColor = new Color(c.r, c.g, c.b, .7f); p.lineWidth = 2;
            for (int i = 0; i < 12; i++) { float a = (a0 + i * 30) * Mathf.Deg2Rad; var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); p.BeginPath(); p.MoveTo(m + d * R * .84f); p.LineTo(m + d * R * .96f); p.Stroke(); }
        }
        static void Shimmer(MeshGenerationContext ctx)
        {   // a bright point with a short tail running round the card's edge
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height, per = 2 * (w + h), inset = 6;
            Vector2 At(float s) { s = Mathf.Repeat(s, per); if (s < w) return new Vector2(s, inset); s -= w; if (s < h) return new Vector2(w - inset, s); s -= h; if (s < w) return new Vector2(w - s, h - inset); s -= w; return new Vector2(inset, h - s); }
            float head = Time.time * per * .35f;
            for (int i = 0; i < 14; i++) { float k = 1 - i / 14f; p.fillColor = new Color(1f, .88f, .5f, .9f * k); p.BeginPath(); p.Arc(At(head - i * 7), 2 + 3 * k, 0, 360); p.Fill(); }
            float pulse = .35f + .25f * Mathf.Sin(Time.time * 4); p.strokeColor = new Color(1f, .8f, .35f, pulse); p.lineWidth = 2.5f;
            p.BeginPath(); p.MoveTo(new Vector2(inset, inset)); p.LineTo(new Vector2(w - inset, inset)); p.LineTo(new Vector2(w - inset, h - inset)); p.LineTo(new Vector2(inset, h - inset)); p.ClosePath(); p.Stroke();
        }
        void Sparks(MeshGenerationContext ctx)
        {   // sparks flying out of the chosen card
            if (perkChosen < 0 || perkChosen >= perkCards.Count) return;
            var card = perkCards[perkChosen].worldBound; var me = ((VisualElement)ctx.visualElement).worldBound; var m = card.center - me.position;
            var p = ctx.painter2D; float e = Mathf.Clamp01(perkChooseT / ChooseTime); var pk = Perks.Get(perkIds[perkChosen]); var c = pk != null ? CatCol(pk.cat) : Color.white;
            var rnd = new System.Random(perkChosen * 31 + 7);
            for (int i = 0; i < 26; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2, sp = 120 + (float)rnd.NextDouble() * 220; var pos = m + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * sp * e;
                p.fillColor = new Color(Mathf.Lerp(c.r, 1, .4f), Mathf.Lerp(c.g, 1, .4f), Mathf.Lerp(c.b, 1, .4f), 1 - e); p.BeginPath(); p.Arc(pos, 2 + 4 * (1 - e), 0, 360); p.Fill();
            }
        }
        // ---- the powers' icons ----
        static void PerkIcon(MeshGenerationContext ctx, string id, Color c)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y);
            p.fillColor = c; p.strokeColor = c; p.lineWidth = w * .09f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
            switch (id)
            {
                case "might": Swords(ctx, c); break;
                case "siege": DrawCastle(ctx, c, new Color(.1f, .08f, .2f)); break;
                case "haste":   // a lightning bolt
                    p.BeginPath(); p.MoveTo(P(.58f, .02f)); p.LineTo(P(.2f, .55f)); p.LineTo(P(.46f, .55f)); p.LineTo(P(.38f, .98f)); p.LineTo(P(.8f, .4f)); p.LineTo(P(.54f, .4f)); p.ClosePath(); p.Fill(); break;
                case "armor":   // a shield
                    p.BeginPath(); p.MoveTo(P(.5f, .04f)); p.LineTo(P(.9f, .18f)); p.LineTo(P(.86f, .58f)); p.BezierCurveTo(P(.8f, .8f), P(.6f, .92f), P(.5f, .98f)); p.BezierCurveTo(P(.4f, .92f), P(.2f, .8f), P(.14f, .58f)); p.LineTo(P(.1f, .18f)); p.ClosePath(); p.Fill(); break;
                case "ward":    // a ring with a spark
                    p.BeginPath(); p.Arc(P(.5f, .5f), w * .38f, 0, 360); p.Stroke(); p.BeginPath(); p.Arc(P(.5f, .5f), w * .14f, 0, 360); p.Fill(); break;
                case "vigor":   // a heart
                    p.BeginPath(); p.MoveTo(P(.5f, .92f)); p.BezierCurveTo(P(.05f, .6f), P(.05f, .1f), P(.5f, .32f)); p.BezierCurveTo(P(.95f, .1f), P(.95f, .6f), P(.5f, .92f)); p.Fill(); break;
                case "leech":   // a drop
                    p.BeginPath(); p.MoveTo(P(.5f, .04f)); p.BezierCurveTo(P(.85f, .45f), P(.85f, .95f), P(.5f, .95f)); p.BezierCurveTo(P(.15f, .95f), P(.15f, .45f), P(.5f, .04f)); p.Fill(); break;
                case "swift":   // two chevrons
                    p.BeginPath(); p.MoveTo(P(.18f, .2f)); p.LineTo(P(.48f, .5f)); p.LineTo(P(.18f, .8f)); p.MoveTo(P(.52f, .2f)); p.LineTo(P(.82f, .5f)); p.LineTo(P(.52f, .8f)); p.Stroke(); break;
            }
        }
    }
}
