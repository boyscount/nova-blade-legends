using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // All screens in UI Toolkit (Thai text through the Advanced Text Generator): lobby with hero/skin/weapon/pet/stage tabs,
    // in-game HUD (HP, wave, boss bar, minimap, joystick, skill buttons), damage numbers, pause and result.
    public class Ui : MonoBehaviour
    {
        // input read by Hero
        public Vector2 Joy; public bool atkP, atkHold, dodgeP, swapP; bool pickOff; VisualElement swapBtn; Button viewBtn;
        string ViewLabel() => Loc.S("มุม ", "View ") + (G.ViewStyle + 1); public readonly bool[] skP = new bool[3];
        public bool heavyP, lockP, blockHold;   // Soulslike buttons
        Vector2 lookAcc; public Vector2 TakeLook() { var d = lookAcc; lookAcc = Vector2.zero; return d; }
        VisualElement stamFill, stamBar, poiseFill, reticle, lookZone; Label hint;
        public void ConsumeInputs() { atkP = dodgeP = false; for (int i = 0; i < 3; i++) skP[i] = false; }

        static readonly Color Gold = new(1, .78f, .35f), Ink = new(.95f, .94f, .9f), Muted = new(.72f, .76f, .74f), Panel = new(.04f, .06f, .08f, .86f),
            Line = new(1, .84f, .55f, .28f), Red = new(1, .3f, .37f), Green = new(.42f, 1, .56f), Cyan = new(.37f, .96f, 1);
        VisualElement mpFill; Label mpT;
        public Vector3? Pan;   // set while the player holds the minimap: where the camera should look
        UIDocument doc; VisualElement root, lobby, hud, pauseOv, resultOv, cards, nums, bars, joyBase, joyKnob, joyZone, bossW, bossFill, hpFill, mini, banner, buffs;
        Label heroT, heroN, heroD, detail, goldL, goldH, waveT, hpT, hudName, bossN, bannerA, bannerB, resH, resS, resK, resG, resT, petN;
        VisualElement[] stats; readonly VisualElement[] sk = new VisualElement[5]; VisualElement porImg, petImg; Button again;
        FontDefinition font, fontBold; string tab = "hero";
        Main G => Main.G;
        public bool touchUi; float J = 1;
        // control schemes: phones = joystick + big buttons; web on a PC = right-click to move, Q W E skills, R / Space dodge (MOBA style);
        // the Windows build keeps WASD + J / U I O
        public bool WebPc { get; private set; }   // J: joystick scale (phones get a much bigger stick)

        // ---------- helpers ----------
        Label L(string t, float size, Color c, bool bold = false)
        {
            var l = new Label(t); l.style.unityFontDefinition = bold ? fontBold : font; l.style.unityTextGenerator = TextGeneratorType.Advanced;
            l.style.fontSize = size; l.style.color = c; l.pickingMode = PickingMode.Ignore; l.style.marginLeft = l.style.marginRight = l.style.marginTop = l.style.marginBottom = 0;
            l.style.paddingLeft = l.style.paddingRight = l.style.paddingTop = l.style.paddingBottom = 0; return l;
        }
        static VisualElement Box(Color? bg = null, float radius = 0)
        {
            var v = new VisualElement(); v.pickingMode = PickingMode.Ignore; if (bg.HasValue) v.style.backgroundColor = bg.Value;
            if (radius > 0) v.style.borderTopLeftRadius = v.style.borderTopRightRadius = v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = radius; return v;
        }
        static void Border(VisualElement v, Color c, float w = 1) { v.style.borderLeftWidth = v.style.borderRightWidth = v.style.borderTopWidth = v.style.borderBottomWidth = w; v.style.borderLeftColor = v.style.borderRightColor = v.style.borderTopColor = v.style.borderBottomColor = c; }
        static void Abs(VisualElement v, float? l = null, float? t = null, float? r = null, float? b = null)
        { v.style.position = Position.Absolute; if (l.HasValue) v.style.left = l.Value; if (t.HasValue) v.style.top = t.Value; if (r.HasValue) v.style.right = r.Value; if (b.HasValue) v.style.bottom = b.Value; }
        static void Pad(VisualElement v, float p) { v.style.paddingLeft = v.style.paddingRight = v.style.paddingTop = v.style.paddingBottom = p; }
        // readable on any background: soft dark shadow under text
        static void Shadow(VisualElement v, float blur = 6, float a = .85f) => v.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = blur, color = new Color(0, 0, 0, a) };
        static void Round(VisualElement v, float r) => v.style.borderTopLeftRadius = v.style.borderTopRightRadius = v.style.borderBottomLeftRadius = v.style.borderBottomRightRadius = r;
        // small gradient textures (UI Toolkit has no gradient fill): a -> b along x (horizontal) or y (vertical, a at the top)
        static readonly Dictionary<string, Texture2D> grads = new();
        static Texture2D Grad(Color a, Color b, bool vertical)
        {
            string key = $"{a}{b}{vertical}"; if (grads.TryGetValue(key, out var t)) return t;
            t = vertical ? new Texture2D(1, 64, TextureFormat.RGBA32, false) : new Texture2D(64, 1, TextureFormat.RGBA32, false); t.wrapMode = TextureWrapMode.Clamp;
            for (int i = 0; i < 64; i++) { var c = Color.Lerp(a, b, i / 63f); if (vertical) t.SetPixel(0, 63 - i, c); else t.SetPixel(i, 0, c); }
            t.Apply(); return grads[key] = t;
        }
        // dark glow in the bottom-left corner: fades out to the right and upward, no hard edges (y = 0 is the top row)
        static Texture2D corner;
        static Texture2D Corner()
        {
            if (corner) return corner; corner = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float fx = Mathf.Clamp01((1 - x / 63f) * 1.35f), fy = Mathf.SmoothStep(0, 1, Mathf.Clamp01(y / 63f * 1.6f)); corner.SetPixel(x, 63 - y, new Color(0, 0, 0, .82f * fx * fx * (3 - 2 * fx) * fy)); }
            corner.Apply(); return corner;
        }
        // painted 9-slice button frame from Resources/UI/Lobby: sx / sy = untouched corner size in texture pixels, scale = corner size on screen
        static void Frame(VisualElement b, string art, int sx, int sy, float scale)
        {
            var t = Resources.Load<Texture2D>("UI/Lobby/" + art); if (!t) return;
            b.style.backgroundImage = new StyleBackground(t); b.style.backgroundColor = Color.clear; Border(b, Color.clear, 0);
            b.style.unitySliceLeft = b.style.unitySliceRight = sx; b.style.unitySliceTop = b.style.unitySliceBottom = sy; b.style.unitySliceScale = scale;
            b.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 5, color = new Color(0, 0, 0, .85f) };
        }
        Button Btn(string text, System.Action onClick, bool primary = true)
        {
            var b = new Button(onClick) { text = text }; b.style.unityFontDefinition = fontBold; b.style.unityTextGenerator = TextGeneratorType.Advanced;
            b.style.fontSize = primary ? 30 : 24; b.style.color = Color.white; b.style.backgroundColor = primary ? new Color(.78f, .38f, .1f) : new Color(.06f, .14f, .17f, .92f);
            if (primary) { b.style.backgroundImage = new StyleBackground(Grad(new Color(1, .62f, .2f), new Color(.72f, .2f, .06f), true)); Shadow(b, 4, .6f); }
            Border(b, primary ? new Color(1, .84f, .45f) : new Color(.37f, .96f, 1, .55f), 2); b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 12;
            b.style.paddingLeft = b.style.paddingRight = 26; b.style.paddingTop = b.style.paddingBottom = 12; return b;
        }

        void Awake()
        {
            font = FontDefinition.FromFont(Resources.Load<Font>("Fonts/Mali-SemiBold"));
            fontBold = FontDefinition.FromFont(Resources.Load<Font>("Fonts/Mali-Bold"));
            doc = gameObject.AddComponent<UIDocument>(); doc.panelSettings = Resources.Load<PanelSettings>("UI/Panel");
            root = doc.rootVisualElement; root.pickingMode = PickingMode.Ignore; Abs(root, 0, 0, 0, 0);
            var args = System.Environment.GetCommandLineArgs();
            WebPc = (Application.platform == RuntimePlatform.WebGLPlayer && !Application.isMobilePlatform) || args.Contains("-webctl");
            touchUi = !WebPc && (Application.isMobilePlatform || Touchscreen.current != null || args.Contains("-touchui"));
            J = touchUi ? 1.7f : 1;
            Studio.Build();
            BuildLobby(); BuildHud(); BuildOverlays();
        }

        // ---------- lobby ----------
        // lobby view: drag to turn the hero (or the pet on the pet tab), wheel / pinch / +- to zoom
        public void SetTab(string id) { tab = id; RenderCards(); }
        public void TestFilter(string cls) { if (cls != "all") cardScroll.scrollOffset = new Vector2(0, 400); clsFilter = cls; RenderCards(); }   // autopilot: scrolled list, then filter
        VisualElement classRow; string clsFilter = "all";
        public float heroYaw, petYaw, lobbyZoom; float yawVel; public bool FocusPet => tab == "pet";
        static readonly Color GoldHi = new(1, .94f, .62f), GoldLo = new(.82f, .46f, .1f);   // stat bars and icons: one gold, shaded light to deep
        // heart (HP), sword (attack), bolt (speed), target (range): drawn on a 24 grid, dark outline so they read on any background
        static void StatIcon(MeshGenerationContext ctx, int i, float w)
        {
            var p = ctx.painter2D; float k = w / 24; Vector2 P(float x, float y) => new(x * k, y * k);
            var gold = FillGradient.MakeLinearGradient(GoldHi, GoldLo, P(12, 2), P(12, 22), AddressMode.Clamp);   // bright at the top, deep amber below
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            void Shape(System.Action path, bool fill)
            {
                p.strokeColor = new Color(0, 0, 0, .75f); p.lineWidth = 4.5f * k; p.BeginPath(); path(); if (fill) p.ClosePath(); p.Stroke();
                if (fill) { p.fillColor = Color.white; p.fillGradient = gold; p.BeginPath(); path(); p.ClosePath(); p.Fill(); } else { p.strokeColor = Color.white; p.strokeFillGradient = gold; p.lineWidth = 2.6f * k; p.BeginPath(); path(); p.Stroke(); }
            }
            if (i == 0) Shape(() => { p.MoveTo(P(12, 21)); p.BezierCurveTo(P(5, 15.5f), P(2, 12), P(2.5f, 8.5f)); p.BezierCurveTo(P(3, 4), P(9, 2.5f), P(12, 7)); p.BezierCurveTo(P(15, 2.5f), P(21, 4), P(21.5f, 8.5f)); p.BezierCurveTo(P(22, 12), P(19, 15.5f), P(12, 21)); }, true);
            else if (i == 1) Shape(() => { p.MoveTo(P(5, 19)); p.LineTo(P(19, 5)); p.MoveTo(P(5, 13)); p.LineTo(P(11, 19)); p.MoveTo(P(6, 18)); p.LineTo(P(3, 21)); p.MoveTo(P(15, 5)); p.LineTo(P(19, 5)); p.LineTo(P(19, 9)); }, false);
            else if (i == 2) Shape(() => { p.MoveTo(P(14, 2)); p.LineTo(P(5, 13.5f)); p.LineTo(P(11, 13.5f)); p.LineTo(P(9.5f, 22)); p.LineTo(P(19, 10)); p.LineTo(P(13, 10)); }, true);
            else { Shape(() => { p.MoveTo(P(21, 12)); p.Arc(P(12, 12), 9 * k, 0, 360); p.MoveTo(P(16.5f, 12)); p.Arc(P(12, 12), 4.5f * k, 0, 360); }, false); p.fillColor = Color.white; p.fillGradient = gold; p.BeginPath(); p.Arc(P(12, 12), 1.8f * k, 0, 360); p.Fill(); }
        }
        // class icons, styled like engraved silver: metal glyphs with a dark edge; only magic carries colour (mage orb blue, cleric green).
        // The picked class sits in a warm gold glow. A painted Resources/UI/Classes/class_<id>.png replaces the drawing when present.
        static void ClassIcon(MeshGenerationContext ctx, string id, float w, bool on)
        {
            if (float.IsNaN(w) || w <= 0) return; var p = ctx.painter2D; float k = w / 24; Vector2 P(float x, float y) => new(x * k, y * k);
            float pulse = on ? .8f + .2f * Mathf.Sin(Time.unscaledTime * 3) : 1;
            void Glow(Color c, float a, float r0, float step)
            { for (int i = 7; i >= 1; i--) { var g = c; g.a = a * pulse; p.fillColor = g; p.BeginPath(); p.Arc(P(12, 12), (r0 + i * step) * k, 0, 360); p.Fill(); } }
            if (on) Glow(new Color(1, .55f, .18f), .09f, 3, 1.35f);
            bool magic = id == "mage" || id == "cleric"; var acc = id == "mage" ? new Color(.35f, .72f, 1) : new Color(.3f, .95f, .55f);
            var parts = new List<System.Action>(); var lit = new List<System.Action>();   // metal parts / glowing magic parts
            void Line(params float[] pts) => parts.Add(() => { p.MoveTo(P(pts[0], pts[1])); for (int i = 2; i < pts.Length; i += 2) p.LineTo(P(pts[i], pts[i + 1])); });
            void Ring(List<System.Action> to, float cx, float cy, float r, float a0 = 0, float a1 = 360) => to.Add(() => { p.MoveTo(P(cx + r * Mathf.Cos(a0 * Mathf.Deg2Rad), cy + r * Mathf.Sin(a0 * Mathf.Deg2Rad))); p.Arc(P(cx, cy), r * k, a0, a1); });
            switch (id)
            {
                case "all": Line(5, 5, 10, 5, 10, 10, 5, 10, 5, 5); Line(14, 5, 19, 5, 19, 10, 14, 10, 14, 5); Line(5, 14, 10, 14, 10, 19, 5, 19, 5, 14); Line(14, 14, 19, 14, 19, 19, 14, 19, 14, 14); break;
                case "warrior": Line(6, 18, 18.5f, 5.5f, 19.5f, 4.5f, 18.5f, 5.5f); Line(16, 4.5f, 19.5f, 4.5f, 19.5f, 8); Line(4.5f, 13.5f, 10.5f, 19.5f); Line(6.5f, 17.5f, 3, 21); break;
                case "assassin": Line(4.5f, 19.5f, 15.5f, 8.5f, 18, 4); Line(19.5f, 19.5f, 8.5f, 8.5f, 6, 4); Line(3.5f, 15, 9, 20.5f); Line(20.5f, 15, 15, 20.5f); break;
                case "mage": Line(5, 21, 14, 10.5f); Line(12.5f, 8.5f, 15.5f, 11.5f); Ring(lit, 16.8f, 7.2f, 3); lit.Add(() => { p.MoveTo(P(21, 1.5f)); p.LineTo(P(21, 4.5f)); p.MoveTo(P(19.5f, 3)); p.LineTo(P(22.5f, 3)); p.MoveTo(P(11, 3)); p.LineTo(P(11, 5)); p.MoveTo(P(10, 4)); p.LineTo(P(12, 4)); }); break;
                case "archer": Ring(parts, 8, 12, 9.2f, -68, 68); Line(11.4f, 3.5f, 11.4f, 20.5f); Line(3.5f, 12, 21, 12); Line(18, 9, 21, 12, 18, 15); Line(4, 10, 6, 12, 4, 14); break;
                case "cleric": lit.Add(() => { p.MoveTo(P(8, 21)); p.LineTo(P(8, 12)); p.LineTo(P(8, 7.5f)); p.MoveTo(P(10.8f, 12)); p.LineTo(P(10.8f, 5.5f)); p.MoveTo(P(13.6f, 12)); p.LineTo(P(13.6f, 6)); p.MoveTo(P(16.4f, 13)); p.LineTo(P(16.4f, 8)); p.MoveTo(P(8, 21)); p.LineTo(P(15, 21)); p.LineTo(P(17.5f, 15)); p.LineTo(P(16.4f, 13)); p.MoveTo(P(8, 14)); p.LineTo(P(5, 11)); }); Ring(lit, 12, 13, 2.2f); break;
                default: Line(7, 10, 7, 18, 16, 18, 19, 14, 19, 10, 7, 10); Line(10, 10, 10, 7); Line(13, 10, 13, 6); Line(16, 10, 16, 7); Line(2, 12, 4, 12); Line(2, 16, 4, 16); break;
            }
            if (magic) Glow(acc, on ? .085f : .06f, 2, 1.3f);
            p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
            void Stroke(List<System.Action> path, Color c, float lw) { if (path.Count == 0) return; p.strokeColor = c; p.lineWidth = lw * k; p.BeginPath(); foreach (var a in path) a(); p.Stroke(); }
            // dark edge under everything, then brushed silver (warmer when picked), then the glowing magic parts on top
            Stroke(parts, new Color(0, 0, 0, .75f), 4.4f); Stroke(lit, new Color(0, 0, 0, .75f), 4.4f);
            var metal = on ? FillGradient.MakeLinearGradient(new Color(1, .98f, .9f), new Color(.72f, .62f, .48f), P(12, 3), P(12, 21), AddressMode.Clamp)
                           : FillGradient.MakeLinearGradient(new Color(.97f, .98f, 1), new Color(.5f, .54f, .6f), P(12, 3), P(12, 21), AddressMode.Clamp);
            if (parts.Count > 0) { p.strokeColor = Color.white; p.strokeFillGradient = metal; p.lineWidth = 2.5f * k; p.BeginPath(); foreach (var a in parts) a(); p.Stroke(); }
            if (lit.Count > 0)
            {
                var halo = acc; halo.a = on ? .55f : .4f; Stroke(lit, halo, 6.5f);
                p.strokeColor = Color.white; p.strokeFillGradient = FillGradient.MakeLinearGradient(Color.Lerp(acc, Color.white, .75f), acc, P(12, 2), P(12, 22), AddressMode.Clamp);
                p.lineWidth = 2.5f * k; p.BeginPath(); foreach (var a in lit) a(); p.Stroke();
            }
        }
        void BuildLobby()
        {
            lobby = Box(); Abs(lobby, 0, 0, 0, 0); root.Add(lobby);
            // the open area left of the panel turns and zooms the model
            var view = Box(); Abs(view, 0, 0, null, 0); view.style.width = Length.Percent(55); view.pickingMode = PickingMode.Position; lobby.Add(view);
            var touches = new Dictionary<int, Vector2>(); float pinch0 = 0, zoom0 = 0;
            view.RegisterCallback<PointerDownEvent>(e => { touches[e.pointerId] = e.position; view.CapturePointer(e.pointerId); if (touches.Count == 2) { var p = touches.Values.ToArray(); pinch0 = Vector2.Distance(p[0], p[1]); zoom0 = lobbyZoom; } });
            view.RegisterCallback<PointerMoveEvent>(e =>
            {
                if (!touches.ContainsKey(e.pointerId)) return;
                var prev = touches[e.pointerId]; touches[e.pointerId] = e.position;
                if (touches.Count >= 2) { var p = touches.Values.ToArray(); if (pinch0 > 1) lobbyZoom = Mathf.Clamp01(zoom0 + (Vector2.Distance(p[0], p[1]) - pinch0) / 600f); }
                else { float d = (e.position.x - prev.x) * .45f; yawVel = d / Mathf.Max(Time.unscaledDeltaTime, .008f); if (FocusPet) petYaw += d; else heroYaw += d; }
            });
            System.Action<IPointerEvent> lift = e => { touches.Remove(e.pointerId); if (view.HasPointerCapture(e.pointerId)) view.ReleasePointer(e.pointerId); if (touches.Count < 2) pinch0 = 0; };
            view.RegisterCallback<PointerUpEvent>(e => lift(e)); view.RegisterCallback<PointerCancelEvent>(e => lift(e));
            view.RegisterCallback<WheelEvent>(e => lobbyZoom = Mathf.Clamp01(lobbyZoom - e.delta.y * .035f));
            // logo
            var logo = Box(); Abs(logo, 44, 28); lobby.Add(logo);
            var t1 = L("NOVA BLADE", 64, new Color(1, .95f, .82f), true); t1.style.letterSpacing = 4; t1.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 14, color = new Color(1, .6f, .2f, .75f) }; logo.Add(t1);
            var t2 = L("LEGENDS", 24, Gold, true); t2.style.letterSpacing = 14; t2.style.marginLeft = 4; Shadow(t2); logo.Add(t2);
            // language: English / ไทย (switching reloads the game)
            var lang = Btn(Loc.En ? "EN  ·  ไทย" : "ไทย  ·  EN", () => Loc.Switch(), false); lang.style.alignSelf = Align.FlexStart; lang.style.marginTop = 12; lang.style.fontSize = touchUi ? 24 : 18;
            lang.style.paddingTop = lang.style.paddingBottom = touchUi ? 10 : 6; lang.style.paddingLeft = lang.style.paddingRight = 16; Round(lang, 18); lang.pickingMode = PickingMode.Position; logo.Add(lang); logo.pickingMode = PickingMode.Position;
            // hero info on a dark fade so it reads over any stage
            var info = Box(); Abs(info, 0, null, null, 0); info.style.width = Length.Percent(42); info.style.paddingLeft = 44; info.style.paddingRight = 60; info.style.paddingTop = 140; info.style.paddingBottom = 34;
            info.style.backgroundImage = new StyleBackground(Corner()); lobby.Add(info);
            heroT = L("", touchUi ? 29 : 24, Gold, true); Shadow(heroT); info.Add(heroT);
            heroN = L("", 72, Color.white, true); heroN.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 10, color = new Color(0, 0, 0, .9f) }; heroN.style.marginTop = -4; info.Add(heroN);
            heroD = L("", touchUi ? 27 : 22, new Color(.9f, .92f, .9f)); heroD.style.whiteSpace = WhiteSpace.Normal; Shadow(heroD); info.Add(heroD);
            var sk = Btn(Loc.S("ⓘ ดูรายละเอียดสกิล", "ⓘ Skill details"), ShowSkills, false); sk.style.alignSelf = Align.FlexStart; sk.style.marginTop = 10; sk.style.fontSize = touchUi ? 24 : 18;
            sk.style.paddingTop = sk.style.paddingBottom = touchUi ? 10 : 6; sk.style.paddingLeft = sk.style.paddingRight = 16; Round(sk, 18); sk.pickingMode = PickingMode.Position; info.Add(sk);
            var st = Box(); st.style.marginTop = 12; info.Add(st); stats = new VisualElement[4];
            string[] names = { Loc.S("พลังชีวิต", "Health"), Loc.S("โจมตี", "Attack"), Loc.S("ความเร็ว", "Speed"), Loc.S("ระยะ", "Range") };
            for (int i = 0; i < 4; i++)
            {
                var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Center; row.style.marginTop = 6;
                int si = i; var ic = Box(); ic.style.width = ic.style.height = 28; ic.style.marginRight = 10; ic.generateVisualContent += ctx => StatIcon(ctx, si, 28); row.Add(ic);
                var n = L(names[i], touchUi ? 25 : 21, Color.white, true); n.style.width = touchUi ? 130 : 112; Shadow(n); row.Add(n);
                var track = Box(new Color(0, 0, 0, .55f), 6); track.style.width = 260; track.style.height = 12; Border(track, new Color(1, 1, 1, .14f));
                stats[i] = Box(GoldLo, 6); stats[i].style.height = Length.Percent(100); stats[i].style.backgroundImage = new StyleBackground(Grad(GoldLo, GoldHi, false));   // deep amber at the start, bright gold at the tip
                track.Add(stats[i]); row.Add(track); st.Add(row);
            }
            // view controls: zoom in / out / reset + hint
            // left edge, mid-height: clear of the pet, the wings and the hero's name
            var vc = Box(); Abs(vc, 36, 330); vc.style.alignItems = Align.Center; lobby.Add(vc);
            foreach (var (txt, act) in new (string, System.Action)[] { ("+", () => lobbyZoom = Mathf.Clamp01(lobbyZoom + .25f)), ("−", () => lobbyZoom = Mathf.Clamp01(lobbyZoom - .25f)), (Loc.S("คืน", "Reset"), () => { lobbyZoom = 0; heroYaw = petYaw = 0; yawVel = 0; }) })
            {
                var b = new Button(act) { text = txt }; b.style.unityFontDefinition = fontBold; b.style.unityTextGenerator = TextGeneratorType.Advanced; b.style.fontSize = txt.Length > 1 ? (touchUi ? 22 : 18) : (touchUi ? 34 : 28); b.style.color = Color.white;
                b.style.width = b.style.height = touchUi ? 76 : 58; Round(b, 40); b.style.backgroundColor = new Color(.04f, .07f, .09f, .78f); Border(b, new Color(1, .84f, .55f, .5f), 2);
                b.style.marginBottom = 10; b.style.paddingLeft = b.style.paddingRight = b.style.paddingTop = b.style.paddingBottom = 0; vc.Add(b);
            }
            var hintV = L(touchUi ? Loc.S("ลากเพื่อหมุน · ถ่างนิ้วเพื่อซูม", "Drag to rotate · Pinch to zoom") : Loc.S("ลากเพื่อหมุน · ล้อเมาส์เพื่อซูม", "Drag to rotate · Mouse wheel to zoom"), 17, new Color(1, 1, 1, .8f)); Shadow(hintV); hintV.style.unityTextAlign = TextAnchor.MiddleCenter; vc.Add(hintV);
            // right panel: dark glass with a thin gold rim
            var panel = Box(new Color(.03f, .045f, .06f, .62f), 18); Border(panel, new Color(1, .82f, .5f, .45f), 2); Abs(panel, null, 70, 30, 30); panel.style.width = Length.Percent(44); Pad(panel, 18); panel.pickingMode = PickingMode.Position; lobby.Add(panel);
            var tabs = Box(new Color(0, 0, 0, .3f), 12); Border(tabs, new Color(1, .84f, .55f, .25f)); tabs.style.flexDirection = FlexDirection.Row; Pad(tabs, 4); panel.Add(tabs);
            int ti = 0;
            foreach (var (id, label) in new[] { ("hero", Loc.S("ฮีโร่", "Heroes")), ("skin", Loc.S("สกิน", "Skins")), ("sword", Loc.S("อาวุธ", "Weapons")), ("wskin", Loc.S("สกินอาวุธ", "Weapon Skins")), ("wings", Loc.S("ปีก", "Wings")), ("pet", Loc.S("สัตว์เลี้ยง", "Pets")), ("stage", Loc.S("ด่าน", "Stages")) })
            {
                if (ti++ > 0) { var sep = Box(new Color(1, .84f, .55f, .22f)); sep.style.width = 1; sep.style.marginTop = sep.style.marginBottom = 10; tabs.Add(sep); }
                var b = new Button(() => { if ((tab == "pet") != (id == "pet")) lobbyZoom = 0; tab = id; RenderCards(); }) { text = label, name = "tab_" + id };
                b.style.unityFontDefinition = fontBold; b.style.unityTextGenerator = TextGeneratorType.Advanced; b.style.fontSize = touchUi ? 27 : 21; b.style.flexGrow = 1; b.style.color = Ink;
                b.style.backgroundColor = Color.clear; Border(b, Color.clear, 2); Round(b, 9); b.style.marginLeft = b.style.marginRight = 2;
                b.style.paddingTop = b.style.paddingBottom = touchUi ? 16 : 9; tabs.Add(b);
            }
            classRow = Box(); classRow.style.flexDirection = FlexDirection.Row; classRow.style.marginTop = 12; classRow.style.flexShrink = 0; classRow.style.justifyContent = Justify.SpaceBetween; panel.Add(classRow);
            foreach (var k in Data.Classes)
            {
                var kk = k; var b = Box(new Color(0, 0, 0, .35f), 12); Border(b, new Color(1, 1, 1, .1f), 1.5f); b.name = "cls_" + k.id; b.pickingMode = PickingMode.Position;
                b.style.width = Length.Percent(13.6f); b.style.alignItems = Align.Center; b.style.paddingTop = 6; b.style.paddingBottom = 6;
                var ic = Box(); ic.style.width = ic.style.height = touchUi ? 64 : 56; ic.style.flexShrink = 0; ic.style.marginBottom = 2; b.Add(ic);
                var png = Resources.Load<Texture2D>("UI/Classes/class_" + k.id);   // painted icon if one exists, otherwise the drawn one
                if (png)
                {   // painted icon over a warm glow that shows only on the picked class
                    ic.generateVisualContent += ctx =>
                    {
                        if (clsFilter != kk.id) return; var p = ctx.painter2D; float w = ic.resolvedStyle.width; if (float.IsNaN(w)) return; float pulse = .8f + .2f * Mathf.Sin(Time.unscaledTime * 3);
                        for (int i = 7; i >= 1; i--) { p.fillColor = new Color(1, .55f, .18f, .1f * pulse); p.BeginPath(); p.Arc(new Vector2(w / 2, w / 2), w * (.12f + i * .055f), 0, 360); p.Fill(); }
                    };
                    var img = Box(); Abs(img, 0, 0, 0, 0); img.style.backgroundImage = new StyleBackground(png); ic.Add(img);
                }
                else ic.generateVisualContent += ctx => ClassIcon(ctx, kk.id, ic.resolvedStyle.width, clsFilter == kk.id);
                var th = L(k.th, touchUi ? 23 : 18, Ink, true); th.name = "th"; Shadow(th, 3, .6f); b.Add(th);
                var en = L(k.en, touchUi ? 16 : 13, Muted); en.name = "en"; b.Add(en); if (Loc.En) en.style.display = DisplayStyle.None;
                b.RegisterCallback<ClickEvent>(_ => { clsFilter = kk.id; RenderCards(); });
                b.schedule.Execute(() => { if (clsFilter == kk.id && tab == "hero") ic.MarkDirtyRepaint(); }).Every(50);   // selected icon glow breathes
                classRow.Add(b);
            }
            var sv = new ScrollView(ScrollViewMode.Vertical); sv.style.flexGrow = 1; sv.style.marginTop = 12; panel.Add(sv); cardScroll = sv;
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden; sv.touchScrollBehavior = ScrollView.TouchScrollBehavior.Elastic;
            var vs = sv.verticalScroller; vs.style.width = 14; vs.style.marginLeft = 6; vs.style.borderLeftWidth = 0; vs.style.backgroundColor = Color.clear;
            vs.lowButton.style.display = vs.highButton.style.display = DisplayStyle.None;
            vs.slider.style.marginTop = vs.slider.style.marginBottom = 0; vs.slider.style.marginLeft = vs.slider.style.marginRight = 0;
            var trk = vs.slider.Q("unity-tracker"); trk.style.backgroundColor = new Color(0, 0, 0, .45f); Border(trk, new Color(1, .84f, .55f, .18f)); Round(trk, 7); trk.style.left = 3; trk.style.right = 3;
            var drg = vs.slider.Q("unity-dragger"); drg.style.backgroundColor = new Color(1, .74f, .3f, .85f); Border(drg, new Color(1, .9f, .6f, .9f)); Round(drg, 7); drg.style.left = 1; drg.style.right = 1;
            drg.style.backgroundImage = new StyleBackground(Grad(new Color(1, .86f, .5f), new Color(.85f, .52f, .15f), false));
            cards = sv.contentContainer; cards.style.flexDirection = FlexDirection.Row; cards.style.flexWrap = Wrap.Wrap;
            detail = L("", touchUi ? 25 : 21, new Color(.86f, .88f, .86f)); detail.style.whiteSpace = WhiteSpace.Normal; detail.style.minHeight = touchUi ? 40 : 60; detail.style.flexShrink = 0; detail.style.marginTop = 8; panel.Add(detail);
            buyBtn = Btn("", () => AskBuy()); buyBtn.style.fontSize = 22; buyBtn.style.paddingTop = buyBtn.style.paddingBottom = 8; buyBtn.style.marginTop = 4; buyBtn.style.display = DisplayStyle.None; panel.Add(buyBtn);
            VisualElement Pill(string text) { var p = Box(new Color(1, .78f, .35f, .16f), 20); Border(p, new Color(1, .8f, .45f, .7f), 2); p.style.paddingLeft = p.style.paddingRight = 14; p.style.paddingTop = p.style.paddingBottom = touchUi ? 12 : 6; p.style.marginRight = 10; p.Add(L(text, touchUi ? 26 : 21, Gold, true)); return p; }
            Button Sec(string text, System.Action act) { var b = Btn(text, act, false); Frame(b, "btn_mode_frame", 70, 50, touchUi ? .4f : .3f); b.style.fontSize = touchUi ? 30 : 22; b.style.paddingTop = b.style.paddingBottom = touchUi ? 18 : 9; Round(b, touchUi ? 28 : 22); return b; }
            var soulRow = Box(); soulRow.style.flexShrink = 0; soulRow.style.flexDirection = FlexDirection.Row; soulRow.style.alignItems = Align.Center; soulRow.style.marginTop = 10; panel.Add(soulRow);
            soulRow.Add(Pill(Loc.S("โหมด Soulslike", "Soulslike Mode"))); var sb = Sec(Loc.S("ดวลบอส (มุมกล้องบุคคลที่สาม) ▶", "Boss Duel (third-person) ▶"), () => G.StartSoul()); sb.style.flexGrow = 1; soulRow.Add(sb);
            var laneRow = Box(); laneRow.style.flexShrink = 0; laneRow.style.flexDirection = FlexDirection.Row; laneRow.style.alignItems = Align.Center; laneRow.style.marginTop = 8; panel.Add(laneRow);
            laneRow.Add(Pill(touchUi ? Loc.S("สนามรบ", "Battle") : Loc.S("สนามรบเลนเดียว", "Lane Battle")));
            foreach (var (txt, size, lanes) in new[] { (Loc.S("1 เลน 1v1", "1 Lane 1v1"), 1, 1), (Loc.S("1 เลน 3v3", "1 Lane 3v3"), 3, 1), (Loc.S("3 เลน 3v3", "3 Lanes 3v3"), 3, 3) })
            { var b = Sec(txt, () => G.StartLane(size, lanes)); b.style.flexGrow = 1; b.style.marginLeft = 6; laneRow.Add(b); }
            var bottom = Box(); bottom.style.flexShrink = 0; bottom.style.flexDirection = FlexDirection.Row; bottom.style.alignItems = Align.Center; bottom.style.marginTop = 12; panel.Add(bottom);
            var cur = Box(new Color(0, 0, 0, .5f), 24); Border(cur, new Color(1, .82f, .5f, .5f), 2); cur.style.paddingLeft = cur.style.paddingRight = 16; cur.style.paddingTop = cur.style.paddingBottom = 10; cur.style.flexDirection = FlexDirection.Row; cur.style.alignItems = Align.Center;
            cur.Add(Coin(24)); goldL = L("0", 24, Ink, true); goldL.style.marginLeft = 8; cur.Add(goldL);
            var gem = Box(new Color(.62f, .4f, 1), 4); gem.style.width = gem.style.height = 20; gem.style.rotate = new Rotate(45); gem.style.marginLeft = 16; Border(gem, new Color(.85f, .75f, 1), 2); cur.Add(gem);
            gemsL = L("0", 24, Ink, true); gemsL.style.marginLeft = 8; cur.Add(gemsL); bottom.Add(cur);
            var play = Btn(Loc.S("สู้เลย", "BATTLE"), () => G.StartGame(Mathf.Clamp(G.save.stage, 1, G.MaxStage))); play.style.flexGrow = 1; play.style.marginLeft = 12; play.style.fontSize = touchUi ? 42 : 32; Round(play, 16);
            if (Resources.Load<Texture2D>("UI/Lobby/btn_play_frame_new") is Texture2D pf)
            {   // painted frame at its own proportions (1200 x 360): the diamonds top and bottom must not stretch
                float pw = touchUi ? 600 : 470; play.style.flexGrow = 0; play.style.width = pw; play.style.height = pw * .3f; play.style.marginLeft = Length.Auto(); play.style.marginRight = Length.Auto();
                play.style.backgroundImage = new StyleBackground(pf); play.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; play.style.backgroundColor = Color.clear; Border(play, Color.clear, 0);
                play.style.paddingLeft = play.style.paddingRight = play.style.paddingTop = play.style.paddingBottom = 0; play.style.fontSize = touchUi ? 50 : 40;
                play.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 6, color = new Color(.35f, .08f, 0, .95f) }; play.style.color = new Color(1, .96f, .82f);
            }
            else { play.style.paddingTop = play.style.paddingBottom = touchUi ? 30 : 20; Frame(play, "btn_play_frame", 230, 90, touchUi ? .36f : .28f); play.style.paddingLeft = play.style.paddingRight = touchUi ? 90 : 70; }
            // a warm halo breathes behind the button and the word glows, so the way into battle always draws the eye
            var playBox = new VisualElement(); playBox.style.marginLeft = Length.Auto(); playBox.style.marginRight = Length.Auto(); playBox.style.justifyContent = Justify.Center; playBox.style.alignItems = Align.Center;
            var halo = new VisualElement { pickingMode = PickingMode.Ignore }; halo.style.position = Position.Absolute;
            halo.style.left = Length.Percent(-14); halo.style.right = Length.Percent(-14); halo.style.top = Length.Percent(-55); halo.style.bottom = Length.Percent(-55);
            halo.style.backgroundImage = new StyleBackground(SoftGlow()); halo.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; halo.style.unityBackgroundImageTintColor = new Color(1, .55f, .18f);
            play.style.marginLeft = play.style.marginRight = 0;
            playBox.Add(halo); playBox.Add(play); bottom.Add(playBox);
            playBox.schedule.Execute(() =>
            {
                float k = .5f + .5f * Mathf.Sin(Time.unscaledTime * 2.6f);
                halo.style.opacity = .45f + .4f * k; halo.style.scale = new Scale(Vector3.one * (.94f + .1f * k));
                play.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 10 + 10 * k, color = new Color(1, .62f + .2f * k, .25f, .9f) };
            }).Every(33);
            lobby.schedule.Execute(() =>
            {   // turntable inertia after a flick
                if (G == null || G.mode != Main.Mode.Lobby || touches.Count > 0 || Mathf.Abs(yawVel) < 2) { if (touches.Count == 0) yawVel = 0; return; }
                float d = yawVel * .016f; if (FocusPet) petYaw += d; else heroYaw += d; yawVel *= .9f;
            }).Every(16);
        }
        static VisualElement Coin(float s) { var c = Box(new Color(1, .78f, .35f), s / 2); c.style.width = c.style.height = s; Border(c, new Color(.72f, .46f, .1f), 2); return c; }

        public void ShowLobby()
        {
            lobby.style.display = DisplayStyle.Flex; hud.style.display = DisplayStyle.None; pauseOv.style.display = resultOv.style.display = DisplayStyle.None;
            RenderInfo(); RenderCards();
        }
        void RenderInfo()
        {
            var h = G.hero.def; heroT.text = h.title; heroN.text = h.name; heroD.text = Loc.S("สกิล: ", "Skills: ") + string.Join(" · ", h.skills.Select(s => Data.SkillLook(h.id, s).n));
            for (int i = 0; i < 4; i++) stats[i].style.width = Length.Percent(h.stats[i] * 100);
            goldL.text = G.save.gold.ToString("N0"); gemsL.text = G.save.gems.ToString("N0") + Loc.S(" (ทดสอบ)", " (test)");
        }
        ScrollView cardScroll; string cardsKey;
        void RenderCards()
        {
            cards.Clear();
            // a different tab or class filter starts at the top (the old offset could leave a shorter list scrolled out of view)
            string key = tab + "/" + clsFilter;
            if (key != cardsKey) { cardsKey = key; cardScroll.scrollOffset = Vector2.zero; cardScroll.schedule.Execute(() => cardScroll.scrollOffset = Vector2.zero); }
            foreach (var b in lobby.Query<Button>().Where(b => b.name != null && b.name.StartsWith("tab_")).ToList())
            { bool on = b.name == "tab_" + tab; b.style.backgroundColor = on ? new Color(1, .7f, .25f, .22f) : Color.clear; Border(b, on ? Gold : Color.clear, 2); b.style.color = on ? new Color(1, .88f, .6f) : Ink; }
            var s = G.save; var h = G.hero.def;
            classRow.style.display = tab == "hero" ? DisplayStyle.Flex : DisplayStyle.None;
            foreach (var b in classRow.Children())
            {
                bool on = b.name == "cls_" + clsFilter; var acc = Data.Class(b.name.Substring(4)).c;
                b.style.backgroundColor = on ? new Color(1, .62f, .2f, .16f) : new Color(0, 0, 0, .35f); Border(b, on ? new Color(1, .84f, .45f) : new Color(1, 1, 1, .1f), on ? 2 : 1.5f);
                b.Q<Label>("th").style.color = on ? new Color(1, .9f, .62f) : Ink; b.Q<Label>("en").style.color = on ? new Color(1, .85f, .6f, .9f) : Muted;
                foreach (var c in b.Children()) c.MarkDirtyRepaint();
            }
            if (tab == "hero")
            {
                foreach (var d in Data.Heroes.Where(d => clsFilter == "all" || d.cls == clsFilter)) { var dd = d; Card(d.name, Studio.HeroFull(d.id) ?? Studio.Hero(d.id), null, s.hero == d.id, () => { s.hero = dd.id; Pick(); }, false, null, (Loc.En ? Data.Class(d.cls).en : $"{Data.Class(d.cls).th} ({Data.Class(d.cls).en})"), Data.Class(d.cls).c); }
                var k = Data.Class(h.cls); detail.text = Loc.S($"{h.name} · คลาส{k.th} ({k.en}) · {h.desc}", $"{h.name} · {k.en} · {h.desc}");
            }
            else if (tab == "skin")
            {
                int cur = G.Skin(h.id); if (!Shop.Owns(ItemKind.HeroSkin, $"{h.id}:{cur}")) cur = 0; int shown = G.pvSkin >= 0 ? G.pvSkin : cur;
                for (int i = 0; i < h.skins.Length; i++)
                {
                    int k = i; string id = $"{h.id}:{i}";
                    Card(h.skins[i].n, null, h.skins[i], shown == i, () => { if (Shop.Owns(ItemKind.HeroSkin, id)) { G.ClearPreview(); G.SetSkin(h.id, k); } else G.pvSkin = k; Pick(); }, false, Badge(ItemKind.HeroSkin, id, i == cur));
                }
                Offer(ItemKind.HeroSkin, $"{h.id}:{shown}", h.skins[shown].n, Loc.S($"สกินของ {h.name} · {h.skins[shown].n} · เปลี่ยนแค่รูปลักษณ์ ไม่เพิ่มพลัง", $"{h.name} skin · {h.skins[shown].n} · Looks only, no stat change"));
            }
            else if (tab == "sword")
            {
                // off-hand: tap this, then an owned weapon; tapping the main weapon clears it
                var off = Data.Swords.FirstOrDefault(w => w.id == s.sword2 && w.id != s.sword);
                Card(pickOff ? Loc.S("เลือกอาวุธรอง: แตะอาวุธที่มีแล้ว", "Choose the off-hand: tap an owned weapon") : off != null ? Loc.S($"อาวุธรอง: {off.name} · กดสลับ X / ⇄", $"Off-hand: {off.name} · swap with X / ⇄") : Loc.S("ตั้งอาวุธรอง (สลับระหว่างสู้)", "Set an off-hand weapon (swap mid-fight)"),
                    off != null ? Studio.Weapon(off.id) : null, null, pickOff, () => { pickOff = !pickOff; RenderCards(); }, true);
                foreach (var w in Data.Swords)
                {
                    var ww = w; string badge = s.sword2 == w.id && w.id != s.sword ? Loc.S("อาวุธรอง", "Off-hand") : Badge(ItemKind.Weapon, w.id, s.sword == w.id);
                    Card(w.name, Studio.Weapon(w.id), null, G.hero.sword.id == w.id, () =>
                    {
                        if (pickOff && Shop.Owns(ItemKind.Weapon, ww.id)) { s.sword2 = ww.id == s.sword ? "" : ww.id; pickOff = false; Pick(); return; }
                        shieldFocus = false;
                        if (Shop.Owns(ItemKind.Weapon, ww.id)) { G.ClearPreview(); s.sword = ww.id; if (s.sword2 == ww.id) s.sword2 = ""; } else G.pvSword = ww.id; Pick();
                    }, false, badge, MasteryLine(w, true));
                }
                // shields: worn on the left arm with the weapon (warriors and clerics)
                bool canShield = Data.ShieldFits(G.hero.def); string shs = G.pvShield ?? s.shield;
                Card(Loc.S("ไม่ใช้โล่", "No shield"), null, null, shs == "", () => { G.ClearPreview(); s.shield = ""; shieldFocus = true; Pick(); }, false, s.shield == "" ? Loc.S("ใส่อยู่", "Equipped") : null);
                foreach (var sh in Data.Shields)
                { var hh = sh; Card(sh.name, Studio.Weapon(sh.id), null, shs == sh.id, () => { shieldFocus = true; if (Shop.Owns(ItemKind.Shield, hh.id)) { G.ClearPreview(); s.shield = hh.id; } else G.pvShield = hh.id; Pick(); }, false, Badge(ItemKind.Shield, sh.id, s.shield == sh.id)); }
                if (shieldFocus)
                {
                    var sd = Data.Shields.FirstOrDefault(x => x.id == shs);
                    Offer(ItemKind.Shield, sd?.id, sd?.name, sd == null ? Loc.S("ไม่ใช้โล่", "No shield") : Loc.S($"{sd.name} · {sd.desc}\nโดนดาเมจลดลง {sd.block * 100:0}%", $"{sd.name} · {sd.desc}\nDamage taken -{sd.block * 100:0}%")
                        + (canShield ? "" : Loc.S("\nใช้ได้เฉพาะสายนักรบและนักบวช", "\nOnly warriors and clerics can use shields")));
                }
                else
                {
                var sw = G.hero.sword; Offer(ItemKind.Weapon, sw.id, sw.name, Loc.S($"{sw.name} · {sw.desc}\nดาเมจ ×{sw.dmg} · ความเร็ว ×{sw.spd} · ระยะ ×{sw.len} · อาวุธได้จาก Gold ที่หาในเกมเท่านั้น", $"{sw.name} · {sw.desc}\nDamage ×{sw.dmg} · Speed ×{sw.spd} · Range ×{sw.len} · Weapons are bought only with Gold earned in play"));
                detail.text += "\n" + MasteryLine(sw, false);
                }
            }
            else if (tab == "wskin")
            {   // weapon skins: the model changes, the equipped weapon's stats do not
                var weapon = Data.Swords.FirstOrDefault(w => w.id == s.sword && Shop.Owns(ItemKind.Weapon, w.id)) ?? Data.Swords[0];
                string worn = Shop.ModelFor(weapon), shown = G.pvWeaponSkin ?? worn;
                Card(Loc.S($"{weapon.name} (ปกติ)", $"{weapon.name} (default)"), Studio.Weapon(weapon.id), null, shown == weapon.id, () => { G.ClearPreview(); s.SetWeaponSkin(weapon.id, weapon.id); Pick(); }, false, worn == weapon.id ? Loc.S("ใส่อยู่", "Equipped") : Loc.S("มีแล้ว", "Owned"));
                foreach (var w in Data.Swords.Where(x => Shop.Fits(weapon, x)))
                { var ww = w; Card(w.name, Studio.Weapon(w.id), null, shown == w.id, () => { if (Shop.Owns(ItemKind.WeaponSkin, ww.id)) { G.ClearPreview(); s.SetWeaponSkin(weapon.id, ww.id); } else G.pvWeaponSkin = ww.id; Pick(); }, false, Badge(ItemKind.WeaponSkin, w.id, worn == w.id)); }
                var sd = Data.Swords.First(x => x.id == shown);
                Offer(ItemKind.WeaponSkin, shown == weapon.id ? null : shown, sd.name + Loc.S(" (สกิน)", " (skin)"), Loc.S($"สกินอาวุธของ {weapon.name}: เปลี่ยนแค่รูปลักษณ์ ค่าพลังยังเป็นของ {weapon.name} (ดาเมจ ×{weapon.dmg} · ระยะ ×{weapon.len})", $"{weapon.name} weapon skin: looks only, stats stay {weapon.name}'s (Damage ×{weapon.dmg} · Range ×{weapon.len})"));
            }
            else if (tab == "wings")
            {   // wings: try any pair on in the lobby, buy with Gems; the pet can wear a matching pair
                string shown = G.WingsShown ?? "";
                Card(Loc.S("ไม่ใส่ปีก", "No wings"), null, null, shown == "", () => { G.ClearPreview(); s.wings = ""; Pick(); }, false, s.wings == "" ? Loc.S("ใส่อยู่", "Equipped") : null);
                foreach (var w in Data.Wings)
                { var ww = w; Card(w.name, Studio.Wing(w.id), null, shown == w.id, () => { if (Shop.Owns(ItemKind.Wings, ww.id)) { G.ClearPreview(); s.wings = ww.id; } else G.pvWings = ww.id; Pick(); }, false, Badge(ItemKind.Wings, w.id, s.wings == w.id), null, w.glow * .55f); }
                Card(s.petWings ? Loc.S("สัตว์เลี้ยงใส่ปีกด้วย: เปิด", "Pet wears wings: ON") : Loc.S("สัตว์เลี้ยงใส่ปีกด้วย: ปิด", "Pet wears wings: OFF"), Studio.Pet(s.pet), null, s.petWings, () => { s.petWings = !s.petWings; Pick(); }, true);
                var wd = Data.Wing(shown);
                Offer(ItemKind.Wings, wd?.id, wd?.name, wd == null ? Loc.S("ไม่ใส่ปีก", "No wings") : Loc.S($"{wd.name} · {wd.desc} · เป็นของแต่งอย่างเดียว ไม่เปลี่ยนค่าพลัง", $"{wd.name} · {wd.desc} · Looks only, no stat change"));
                if (wd != null && G.hero.def.id == "harpy") detail.text += Loc.S("\nแอริสมีปีกของตัวเองอยู่แล้ว ปีกนี้จะใส่ให้สัตว์เลี้ยง", "\nAeris has wings of her own: this pair is worn by the pet");
            }
            else if (tab == "pet") { foreach (var p in Data.Pets) Card(p.name, Studio.Pet(p.id), null, s.pet == p.id, () => { s.pet = p.id; Pick(); }); detail.text = $"{G.pet.def.name} · {G.pet.def.desc}"; }
            else
            {
                int mx = G.MaxStage;
                for (int n = 1; n <= Data.Themes.Length; n++) { int k = n; var T = Data.Themes[Data.ThemeOf(n)]; Card(Loc.S($"ด่าน {n} · {T.name}", $"Stage {n} · {T.name}") + (n > mx ? Loc.S(" (ล็อก)", " (locked)") : ""), Studio.Stage(Data.ThemeOf(n)), null, s.stage == n, n > mx ? null : () => { s.stage = k; Pick(); }, true); }
                var th = Data.Themes[Data.ThemeOf(s.stage)]; detail.text = Loc.S($"ด่าน {s.stage} · {th.name} · {th.desc} · บอส: {th.boss.name}", $"Stage {s.stage} · {th.name} · {th.desc} · Boss: {th.boss.name}");
            }
        }
        // "Expert 140/300 · +6% dmg · +2% crit · affinity +6%" (short: tier and progress only)
        string MasteryLine(SwordDef w, bool brief)
        {
            int xp = G.save.Mastery(w.id), t = Data.Tier(xp); var names = Data.MasteryName;
            string prog = t + 1 < Data.MasteryXp.Length ? $"{xp}/{Data.MasteryXp[t + 1]}" : Loc.S("สูงสุด", "max");
            if (brief) return $"{names[t]} · {prog}";
            float aff = Data.AffinityBonus(G.hero.def.cls, w.type);
            string bonus = Loc.S($"ความชำนาญ {names[t]} ({prog}) · ดาเมจ +{Data.MasteryDmg[t] * 100:0}% · คริ +{Data.MasteryCrit[t] * 100:0}%", $"Mastery {names[t]} ({prog}) · damage +{Data.MasteryDmg[t] * 100:0}% · crit +{Data.MasteryCrit[t] * 100:0}%");
            if (t >= 4) bonus += Loc.S(" · ท่าปิดคอมโบแรงขึ้น", " · stronger combo finisher");
            bonus += aff > 0 ? Loc.S($" · เข้ากับ{G.hero.def.name} +{aff * 100:0.#}%", $" · suits {G.hero.def.name} +{aff * 100:0.#}%") : "";
            return bonus;
        }
        string Badge(ItemKind k, string id, bool worn) => Shop.Owns(k, id) ? (worn ? Loc.S("ใส่อยู่", "Equipped") : Loc.S("มีแล้ว", "Owned")) : Shop.PriceText(k, id);
        // detail text, plus the buy button when the item being tried on is not owned yet
        VisualElement skillOv, skillBox;
        string pickSkill;
        // Hero details (character select): name and class, a 2x2 stat box with bars, the three skills as round pictures;
        // tapping a skill shows what it does underneath. Closes with the X or a tap outside.
        void ShowSkills()
        {
            var h = G.hero.def; skillBox.Clear(); float big = touchUi ? 1.25f : 1;
            if (pickSkill == null || System.Array.IndexOf(h.skills, pickSkill) < 0) pickSkill = h.skills[0];
            skillBox.style.width = 640 * big;
            var cl = Data.Classes.FirstOrDefault(c => c.id == h.cls);
            // header
            var top = Box(); top.style.flexDirection = FlexDirection.Row; top.style.alignItems = Align.Center; skillBox.Add(top);
            var tt = Box(); tt.style.flexGrow = 1; tt.style.flexShrink = 1; top.Add(tt);
            var title = L(Loc.S($"รายละเอียดฮีโร่: {h.name}", $"HERO DETAILS: {h.name.ToUpper()}"), 36 * big, new Color(1, .93f, .8f), true); title.style.textShadow = new TextShadow { offset = new Vector2(0, 2), blurRadius = 6, color = new Color(0, 0, 0, .8f) }; tt.Add(title);
            var cr = Box(); cr.style.flexDirection = FlexDirection.Row; cr.style.alignItems = Align.Center; cr.style.marginTop = 4; tt.Add(cr);
            var ci = Box(); ci.style.width = ci.style.height = 30 * big; ci.style.marginRight = 8; string cid = h.cls; ci.generateVisualContent += ctx => ClassIcon(ctx, cid, ci.resolvedStyle.width, true); cr.Add(ci);
            cr.Add(L(cl.th, 24 * big, Gold, true)); var ttl = L("  ·  " + h.title, 19 * big, Muted); cr.Add(ttl);
            var x = new Button(() => skillOv.style.display = DisplayStyle.None) { text = "✕" }; x.style.fontSize = 30 * big; x.style.color = Ink; x.style.width = x.style.height = 52 * big;
            x.style.backgroundColor = new Color(1, 1, 1, .08f); Border(x, Line, 1.5f); Round(x, 26 * big); x.style.unityFontDefinition = fontBold; x.style.flexShrink = 0; top.Add(x);
            // stats: 2 x 2, each with its picture, value and a bar against the strongest hero
            var sb = Box(new Color(1, 1, 1, .035f), 14); Border(sb, new Color(1, .84f, .55f, .22f), 1.5f); Pad(sb, 16); sb.style.marginTop = 16; sb.style.flexDirection = FlexDirection.Row; sb.style.flexWrap = Wrap.Wrap; skillBox.Add(sb);
            var heroes = Data.Heroes; float crit = (G.hero.sword.crit + G.hero.CritBonus) * 100;
            var cells = new (int icon, string name, string val, float frac, Color c)[]
            {
                (0, Loc.S("พลังชีวิต", "HP"), $"{h.hp:N0}", h.hp / heroes.Max(x2 => x2.hp), new Color(.35f, .9f, .42f)),
                (1, Loc.S("โจมตี", "ATK"), $"{G.hero.AtkPow:N0}", h.atk / heroes.Max(x2 => x2.atk), new Color(1, .5f, .18f)),
                (2, Loc.S("ความเร็ว", "SPD"), $"{h.spd:0.0}", h.spd / heroes.Max(x2 => x2.spd), new Color(.62f, .48f, 1)),
                (3, Loc.S("คริติคอล", "CRIT"), $"{crit:0.#}%", Mathf.Clamp01(crit / 40), new Color(1, .84f, .3f)),
            };
            foreach (var (icon, name, val, frac, c) in cells)
            {
                var cell = Box(); cell.style.width = Length.Percent(50); cell.style.flexDirection = FlexDirection.Row; cell.style.alignItems = Align.Center; cell.style.paddingTop = cell.style.paddingBottom = 6; cell.style.paddingRight = 14; sb.Add(cell);
                var ring = Box(new Color(c.r * .22f, c.g * .22f, c.b * .22f, 1), 24 * big); ring.style.width = ring.style.height = 48 * big; Border(ring, c, 2); ring.style.alignItems = Align.Center; ring.style.justifyContent = Justify.Center; ring.style.flexShrink = 0; cell.Add(ring);
                var ii = Box(); ii.style.width = ii.style.height = 26 * big; int ix = icon; ii.generateVisualContent += ctx => StatIcon(ctx, ix, ii.resolvedStyle.width); ring.Add(ii);
                var col = Box(); col.style.flexGrow = 1; col.style.marginLeft = 12; cell.Add(col);
                var lr = Box(); lr.style.flexDirection = FlexDirection.Row; lr.style.justifyContent = Justify.SpaceBetween; col.Add(lr);
                lr.Add(L(name + ":", 22 * big, Ink, true)); lr.Add(L(val, 22 * big, Ink, true));
                var bar = Box(new Color(0, 0, 0, .55f), 4); bar.style.height = 8 * big; bar.style.marginTop = 5; col.Add(bar);
                var fill = Box(c, 4); fill.style.height = Length.Percent(100); fill.style.width = Length.Percent(Mathf.Clamp(frac, .05f, 1) * 100); bar.Add(fill);
            }
            // skills: round pictures in a row, the chosen one glows; its description below
            var kb = Box(new Color(1, 1, 1, .035f), 14); Border(kb, new Color(1, .84f, .55f, .22f), 1.5f); Pad(kb, 16); kb.style.marginTop = 14; skillBox.Add(kb);
            var kr = Box(); kr.style.flexDirection = FlexDirection.Row; kr.style.justifyContent = Justify.SpaceAround; kb.Add(kr);
            foreach (var id in h.skills)
            {
                var d = Data.Skill(id); var look = Data.SkillLook(h.id, id); bool on = id == pickSkill; string sid = id;
                var it = Box(); it.style.alignItems = Align.Center; it.style.width = 160 * big; it.pickingMode = PickingMode.Position; kr.Add(it);
                it.RegisterCallback<PointerDownEvent>(e => { pickSkill = sid; ShowSkills(); });
                var ic = Box(new Color(look.c.r * .25f, look.c.g * .25f, look.c.b * .25f, .95f), 46 * big); ic.style.width = ic.style.height = 92 * big; ic.style.overflow = Overflow.Hidden;
                Border(ic, on ? new Color(1, .85f, .45f) : new Color(.75f, .55f, .3f, .8f), on ? 4 : 2.5f); ic.style.alignItems = Align.Center; ic.style.justifyContent = Justify.Center; ic.pickingMode = PickingMode.Ignore;
                if (on) Shadow(ic, 14, .9f);
                var tex = Resources.Load<Texture2D>($"UI/Skills/skill_{h.id}_{id}");   // the same picture as the skill button in battle
                if (tex) ic.style.backgroundImage = new StyleBackground(tex); else ic.Add(L(d.icon, 42 * big, look.c, true));
                it.Add(ic);
                var nm = L(look.n, 21 * big, on ? Gold : Ink, true); nm.style.marginTop = 8; nm.style.unityTextAlign = TextAnchor.MiddleCenter; nm.style.whiteSpace = WhiteSpace.Normal; it.Add(nm);
                it.Add(L(Loc.S($"คูลดาวน์ {d.cd:0} วิ", $"CD {d.cd:0}s"), 17 * big, Muted));
            }
            var sel = Data.SkillLook(h.id, pickSkill);
            var dsBox = Box(new Color(0, 0, 0, .3f), 10); Pad(dsBox, 12); dsBox.style.marginTop = 14; kb.Add(dsBox);
            dsBox.Add(L(sel.n, 22 * big, Gold, true));
            var ds = L(Data.SkillDesc(h, pickSkill), 20 * big, Ink); ds.style.whiteSpace = WhiteSpace.Normal; ds.style.marginTop = 4; dsBox.Add(ds);
            skillOv.style.display = DisplayStyle.Flex;
        }
        public void TestSkills() => ShowSkills();
        bool shieldFocus;   // weapons tab: the detail line shows the shield picked last
        void Offer(ItemKind k, string id, string name, string text)
        {
            detail.text = text;
            bool show = id != null && !Shop.Owns(k, id); pending = show ? (k, id, name) : default;
            buyBtn.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show) { buyBtn.text = Loc.S($"ลองใส่อยู่ · ซื้อ {name} ({Shop.PriceText(k, id)})", $"Trying on · Buy {name} ({Shop.PriceText(k, id)})"); detail.text += Loc.S("\nกำลังลองใส่: เอาเข้าไปเล่นได้หลังซื้อ", "\nTrying on: buy it to take it into battle"); }
        }
        void Card(string label, Texture2D img, (string n, Color tint, Color glow)? swatch, bool on, System.Action click, bool wide = false, string badge = null, string sub = null, Color? glowBg = null)
        {
            var c = Box(on ? new Color(.22f, .16f, .06f, .78f) : new Color(.07f, .09f, .12f, .66f), 14); Border(c, on ? new Color(1, .84f, .42f) : new Color(1, 1, 1, .12f), on ? 2.5f : 1); Pad(c, 5);
            c.style.width = Length.Percent(wide ? 48 : 31.3f); c.style.marginRight = Length.Percent(1.6f); c.style.marginBottom = 12; c.pickingMode = PickingMode.Position;
            var pic = Box(new Color(.12f, .14f, .17f), 10); pic.style.width = Length.Percent(100); pic.style.height = wide ? 150 : glowBg.HasValue ? (touchUi ? 340 : 300) : 176;   // hero cards are tall (full body)
            if (glowBg.HasValue)
            {   // portrait over a soft glow in the class colour, fading to dark at the bottom
                var g = glowBg.Value; pic.style.backgroundImage = new StyleBackground(Grad(new Color(g.r * .55f, g.g * .55f, g.b * .55f), new Color(.05f, .06f, .08f), true));
                if (img) { var face = Box(); Abs(face, 0, 0, 0, 0); face.style.backgroundImage = new StyleBackground(img); face.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain); pic.Add(face); }
            }
            else if (img) { pic.style.backgroundImage = new StyleBackground(img); pic.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover); }
            if (swatch.HasValue) { var sw = Box(swatch.Value.tint, 60); sw.style.width = sw.style.height = 100; sw.style.alignSelf = Align.Center; sw.style.marginTop = 18; Border(sw, swatch.Value.glow.maxColorComponent > 0 ? swatch.Value.glow * 4 : new Color(.4f, .4f, .4f), 10); pic.Add(sw); }
            if (badge != null)
            {
                bool owned = badge == Loc.S("ใส่อยู่", "Equipped") || badge == Loc.S("มีแล้ว", "Owned"); var bg = Box(owned ? new Color(.1f, .35f, .2f, .9f) : new Color(.35f, .2f, .05f, .92f), 8); Abs(bg, 4, 4); Pad(bg, 3);
                bg.Add(L(badge, 16, owned ? Green : Gold, true)); pic.Add(bg);
            }
            c.Add(pic); var l = L(label, touchUi ? 27 : 21, on ? new Color(1, .88f, .58f) : Ink, true); l.style.unityTextAlign = TextAnchor.MiddleCenter; l.style.whiteSpace = WhiteSpace.Normal; l.style.marginTop = 5; l.style.marginBottom = 2; Shadow(l, 4, .7f); c.Add(l);
            if (sub != null) { var sl = L(sub, touchUi ? 19 : 15, on ? new Color(1, .85f, .6f, .9f) : Muted); sl.style.unityTextAlign = TextAnchor.MiddleCenter; sl.style.marginTop = -2; sl.style.marginBottom = 3; c.Add(sl); }
            if (on) Sparkle(c);
            if (click == null) c.style.opacity = .45f; else c.RegisterCallback<ClickEvent>(_ => click());
            cards.Add(c);
        }
        // selected card: the gold rim breathes and little four-point stars twinkle as they travel around it
        void Sparkle(VisualElement c)
        {
            var fx = Box(); Abs(fx, -8, -8, -8, -8); fx.pickingMode = PickingMode.Ignore; c.Add(fx); float t0 = Time.unscaledTime;
            fx.generateVisualContent += ctx =>
            {
                var p = ctx.painter2D; float W = fx.resolvedStyle.width, H = fx.resolvedStyle.height; if (float.IsNaN(W) || W < 20) return; float t = Time.unscaledTime - t0;
                Vector2 Along(float u)
                {   // point on the card's rim, u in [0,1) clockwise from the top-left
                    float w = W - 16, h = H - 16, per = 2 * (w + h), d = Mathf.Repeat(u, 1) * per;
                    if (d < w) return new(8 + d, 8); d -= w; if (d < h) return new(8 + w, 8 + d); d -= h; if (d < w) return new(8 + w - d, 8 + h); d -= w; return new(8, 8 + h - d);
                }
                for (int i = 0; i < 6; i++)
                {
                    float u = i / 6f + t * .06f, tw = Mathf.Pow(Mathf.Max(0, Mathf.Sin(t * 3.1f + i * 2.1f)), 3); if (tw < .03f) continue;
                    var s = Along(u); float r = 4 + 9 * tw;
                    p.fillColor = new Color(1, .82f, .4f, .35f * tw); p.BeginPath(); p.Arc(s, r * .9f, 0, 360); p.Fill();
                    p.fillColor = new Color(1, .97f, .82f, tw); p.BeginPath();
                    p.MoveTo(s + new Vector2(0, -r)); p.LineTo(s + new Vector2(r * .2f, -r * .2f)); p.LineTo(s + new Vector2(r, 0)); p.LineTo(s + new Vector2(r * .2f, r * .2f));
                    p.LineTo(s + new Vector2(0, r)); p.LineTo(s + new Vector2(-r * .2f, r * .2f)); p.LineTo(s + new Vector2(-r, 0)); p.LineTo(s + new Vector2(-r * .2f, -r * .2f)); p.ClosePath(); p.Fill();
                }
            };
            fx.schedule.Execute(() =>
            {
                fx.MarkDirtyRepaint(); float b = .5f + .5f * Mathf.Sin((Time.unscaledTime - t0) * 3.4f);
                Border(c, Color.Lerp(new Color(1, .72f, .25f), new Color(1, .97f, .78f), b), 3);
            }).Every(33);
        }
        void Pick()
        {
            G.Persist(); G.stage = G.save.stage;
            G.hero.Equip(); G.pet.Equip(); G.hero.PlaceLobby(); G.pet.PlaceLobby();
            Fx.Burst(Vector3.up, 24, tab == "sword" ? G.hero.sword.color : Gold, 3, 1.2f, .1f);
            RenderInfo(); RenderCards();
        }

        // ---------- HUD ----------
        void BuildHud()
        {
            hud = Box(); Abs(hud, 0, 0, 0, 0); root.Add(hud);
            bars = Box(); Abs(bars, 0, 0, 0, 0); hud.Add(bars); nums = Box(); Abs(nums, 0, 0, 0, 0); hud.Add(nums);
            // top-left: portrait, name, HP, pet
            var me = Box(); Abs(me, 30, 22); me.style.flexDirection = FlexDirection.Row; me.style.alignItems = Align.Center; hud.Add(me);
            porImg = Box(new Color(.1f, .16f, .17f), 50); porImg.style.width = porImg.style.height = 100; Border(porImg, Gold, 3); porImg.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover); me.Add(porImg);
            var col = Box(); col.style.marginLeft = 14; me.Add(col);
            hudName = L("", 24, Ink, true); col.Add(hudName);
            var hpBar = Box(new Color(0, 0, 0, .6f), 12); hpBar.style.width = 380; hpBar.style.height = 26; Border(hpBar, new Color(1, 1, 1, .25f)); col.Add(hpBar);
            hpFill = Box(new Color(.2f, .78f, .38f), 12); hpFill.style.height = Length.Percent(100); hpBar.Add(hpFill);
            hpT = L("", 18, Ink, true); Abs(hpT, 0, 0, 0, 0); hpT.style.unityTextAlign = TextAnchor.MiddleCenter; hpBar.Add(hpT);
            var mpBar = Box(new Color(0, 0, 0, .6f), 7); mpBar.style.width = 380; mpBar.style.height = 14; mpBar.style.marginTop = 4; Border(mpBar, new Color(1, 1, 1, .2f)); col.Add(mpBar);
            mpFill = Box(new Color(.25f, .55f, 1), 7); mpFill.style.height = Length.Percent(100); mpFill.style.backgroundImage = new StyleBackground(Grad(new Color(.45f, .78f, 1), new Color(.2f, .4f, 1), true)); mpBar.Add(mpFill);
            mpT = L("", 12, Ink, true); Abs(mpT, 0, 0, 0, 0); mpT.style.unityTextAlign = TextAnchor.MiddleCenter; mpBar.Add(mpT);
            stamBar = Box(new Color(0, 0, 0, .6f), 6); stamBar.style.width = 300; stamBar.style.height = 11; stamBar.style.marginTop = 4; Border(stamBar, new Color(1, 1, 1, .2f)); col.Add(stamBar);
            stamFill = Box(new Color(1, .82f, .3f), 6); stamFill.style.height = Length.Percent(100); stamBar.Add(stamFill);
            var petRow = Box(); petRow.style.flexDirection = FlexDirection.Row; petRow.style.alignItems = Align.Center; petRow.style.marginTop = 6; col.Add(petRow);
            petImg = Box(new Color(.1f, .16f, .17f), 18); petImg.style.width = petImg.style.height = 36; Border(petImg, Line); petImg.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover); petRow.Add(petImg);
            petN = L("", 19, Ink); petN.style.unityTextOutlineWidth = .2f; petN.style.unityTextOutlineColor = new Color(0, 0, 0, .7f); petN.style.marginLeft = 8; petRow.Add(petN);
            buffs = Box(); buffs.style.flexDirection = FlexDirection.Row; buffs.style.marginTop = 6; col.Add(buffs);
            // top-centre: wave + boss
            var mid = Box(); Abs(mid, 0, 20, 0); mid.style.alignItems = Align.Center; hud.Add(mid);
            waveT = L("", 24, Ink, true); mid.Add(waveT);
            bossW = Box(); bossW.style.width = 700; bossW.style.marginTop = 8; mid.Add(bossW);
            bossN = L("", 22, new Color(.91f, .71f, 1), true); bossN.style.unityTextAlign = TextAnchor.MiddleCenter; bossW.Add(bossN);
            var bb = Box(new Color(0, 0, 0, .6f), 10); bb.style.height = 20; Border(bb, new Color(.9f, .7f, 1, .45f)); bossW.Add(bb); bossFill = Box(new Color(.6f, .2f, 1), 10); bossFill.style.height = Length.Percent(100); bb.Add(bossFill);
            var pb2 = Box(new Color(0, 0, 0, .5f), 4); pb2.style.height = 7; pb2.style.marginTop = 3; bossW.Add(pb2); poiseFill = Box(new Color(1, .75f, .25f), 4); poiseFill.style.height = Length.Percent(100); pb2.Add(poiseFill);
            // top-right: gold, minimap, pause
            var tr = Box(); Abs(tr, null, 22, 30); tr.style.flexDirection = FlexDirection.Row; tr.style.alignItems = Align.FlexStart; hud.Add(tr);
            var cur = Box(new Color(0, 0, 0, .45f), 20); Border(cur, Line); Pad(cur, 8); cur.style.flexDirection = FlexDirection.Row; cur.style.alignItems = Align.Center; cur.style.marginRight = 12;
            cur.Add(Coin(22)); goldH = L("0", 22, Ink, true); goldH.style.marginLeft = 8; cur.Add(goldH); tr.Add(cur);
            mini = Box(new Color(.04f, .08f, .06f, .65f), 95); mini.style.width = mini.style.height = touchUi ? 230 : 190; Border(mini, Line, 3); mini.generateVisualContent += DrawMini;
            if (touchUi) { Abs(mini, 24, 22); hud.Add(mini); me.style.left = 24 + 230 + 18; }
            else if (PcArt("minimap_frame") is Texture2D mf)
            {   // compass frame (600 px, round hole of 404 px) around the paper map; the minimap is drawn on the paper
                const float P = 230, F = P * 600f / 404f; var wrap = Box(); wrap.style.width = wrap.style.height = F; wrap.style.marginTop = -(F - P) / 2 + 40; wrap.style.marginRight = -(F - P) / 2 + 10; tr.Add(wrap);
                mini.style.width = mini.style.height = P; Abs(mini, (F - P) / 2, (F - P) / 2); Round(mini, P / 2); mini.style.overflow = Overflow.Hidden; Border(mini, Color.clear, 0);
                mini.style.backgroundColor = new Color(.84f, .75f, .54f); if (PcArt("minimap_paper") is Texture2D pp) mini.style.backgroundImage = new StyleBackground(pp);
                wrap.Add(mini); var fr = Box(); Abs(fr, 0, 0, 0, 0); fr.style.backgroundImage = new StyleBackground(mf); fr.pickingMode = PickingMode.Ignore; wrap.Add(fr);
            }
            else tr.Add(mini);
            // hold / drag on the minimap: the camera looks at that spot (scout the lanes); let go and it returns to the hero
            mini.pickingMode = PickingMode.Position; int mpid = -1;
            void PanTo(Vector2 lp) { float W = mini.resolvedStyle.width; if (float.IsNaN(W) || G == null) return; float S = W / 2 / ((G.lane ? Lane.HalfL : Data.ArenaR) + 3); Pan = new Vector3(-(lp.x - W / 2) / S, 0, (lp.y - W / 2) / S); }
            mini.RegisterCallback<PointerDownEvent>(e => { mpid = e.pointerId; mini.CapturePointer(mpid); PanTo(e.localPosition); e.StopPropagation(); });
            mini.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId == mpid) PanTo(e.localPosition); });
            System.Action<IPointerEvent> mup = e => { if (e.pointerId != mpid) return; if (mini.HasPointerCapture(mpid)) mini.ReleasePointer(mpid); mpid = -1; Pan = null; };
            mini.RegisterCallback<PointerUpEvent>(e => mup(e)); mini.RegisterCallback<PointerCancelEvent>(e => mup(e));
            var pb = Btn("II", () => TogglePause(), false); pb.style.marginLeft = 12; pb.style.paddingLeft = pb.style.paddingRight = 18; if (touchUi) { pb.style.width = pb.style.height = 92; pb.style.fontSize = 34; pb.style.paddingLeft = pb.style.paddingRight = 0; }
            else if (PcArt("btn_pause_round") is Texture2D pt) { pb.text = ""; pb.style.width = pb.style.height = 84; pb.style.backgroundColor = Color.clear; Border(pb, Color.clear, 0); pb.style.backgroundImage = new StyleBackground(pt); pb.style.paddingLeft = pb.style.paddingRight = 0; }
            // camera: View cycles high / mid / low, then zoom out and in (also: C, mouse wheel)
            Button Small(string t, System.Action a) { var b = Btn(t, a, false); b.style.marginLeft = 10; b.style.fontSize = touchUi ? 30 : 20; if (touchUi) { b.style.height = 92; b.style.minWidth = 92; } b.style.paddingLeft = b.style.paddingRight = touchUi ? 18 : 14; return b; }
            viewBtn = Small("", () => { G.CycleView(); viewBtn.text = ViewLabel(); }); viewBtn.text = ViewLabel(); tr.Add(viewBtn);
            tr.Add(Small("−", () => G.ZoomBy(.15f))); tr.Add(Small("+", () => G.ZoomBy(-.15f)));
            tr.Add(pb);
            // joystick (left 45% of the screen)
            // Soulslike: drag on the right side of the screen turns the camera (buttons sit on top and still win)
            lookZone = Box(); Abs(lookZone, null, 0, 0, 0); lookZone.style.width = Length.Percent(55); lookZone.pickingMode = PickingMode.Position; hud.Add(lookZone);
            int lid = -1; Vector2 lp = default;
            lookZone.RegisterCallback<PointerDownEvent>(e => { if (!G.soul || lid >= 0) return; lid = e.pointerId; lookZone.CapturePointer(lid); lp = e.localPosition; });
            lookZone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId != lid) return; var d = (Vector2)e.localPosition - lp; lp = e.localPosition; lookAcc += new Vector2(d.x * .9f, d.y * .9f); });
            lookZone.RegisterCallback<PointerUpEvent>(e => { if (e.pointerId != lid) return; lookZone.ReleasePointer(lid); lid = -1; });
            reticle = Box(); reticle.style.position = Position.Absolute; reticle.style.width = reticle.style.height = 46; Border(reticle, new Color(1, .35f, .3f), 3);
            reticle.style.borderTopLeftRadius = reticle.style.borderTopRightRadius = reticle.style.borderBottomLeftRadius = reticle.style.borderBottomRightRadius = 23; hud.Add(reticle);
            joyZone = Box(); Abs(joyZone, 0, null, null, 0); joyZone.style.width = Length.Percent(45); joyZone.style.height = Length.Percent(62); joyZone.pickingMode = PickingMode.Position; hud.Add(joyZone);
            joyBase = Box(new Color(1, 1, 1, .08f), 100 * J); joyBase.style.width = joyBase.style.height = 200 * J; Border(joyBase, new Color(1, 1, 1, .25f), 3); joyBase.style.position = Position.Absolute; joyZone.Add(joyBase);
            joyKnob = Box(new Color(.85f, .9f, .92f, .9f), 45 * J); joyKnob.style.width = joyKnob.style.height = 90 * J; Abs(joyKnob, 55 * J, 55 * J); joyBase.Add(joyKnob);
            JoyHome(); joyZone.RegisterCallback<GeometryChangedEvent>(_ => JoyHome());
            if (WebPc) joyZone.style.display = DisplayStyle.None;
            int jid = -1; Vector2 c0 = default;
            joyZone.RegisterCallback<PointerDownEvent>(e => { if (jid >= 0) return; jid = e.pointerId; joyZone.CapturePointer(jid); c0 = e.localPosition; joyBase.style.left = c0.x - 100 * J; joyBase.style.top = c0.y - 100 * J; });
            joyZone.RegisterCallback<PointerMoveEvent>(e => { if (e.pointerId != jid) return; var d = (Vector2)e.localPosition - c0; float R = 80 * J; if (d.magnitude > R) d = d.normalized * R; joyKnob.style.left = 55 * J + d.x; joyKnob.style.top = 55 * J + d.y; Joy = d.magnitude > 8 * J ? d / R : Vector2.zero; });
            System.Action<IPointerEvent> up = e => { if (e.pointerId != jid) return; joyZone.ReleasePointer(jid); jid = -1; Joy = Vector2.zero; joyKnob.style.left = joyKnob.style.top = 55 * J; JoyHome(); };
            joyZone.RegisterCallback<PointerUpEvent>(e => up(e)); joyZone.RegisterCallback<PointerCancelEvent>(e => up(e));
            // skill buttons (bottom-right)
            // thumb cluster: big attack button in the corner, the three skills on an arc around it, dodge further left
            // (panel units are 1/1080 of the screen height: on a 6.5" phone held sideways 330 is about 2.1 cm and 215 about 1.4 cm)
            var btns = Box(); Abs(btns, null, null, 0, 0); btns.style.width = 980; btns.style.height = 820; hud.Add(btns);
            float mainS = touchUi ? 330 : 220, skS = touchUi ? 215 : 140, dgS = touchUi ? 185 : 124, arc = mainS / 2 + skS / 2 + (touchUi ? 30 : 26);
            var mc = new Vector2(44 + mainS / 2, 40 + mainS / 2);   // attack centre, measured from the bottom-right corner
            var at = new (Vector2 c, float s)[5];
            at[0] = (mc, mainS);
            for (int i = 0; i < 3; i++) { float a = (8 + i * 44) * Mathf.Deg2Rad; at[i + 1] = (mc + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * arc, skS); }
            at[4] = (new Vector2(at[1].c.x + skS / 2 + dgS / 2 + 34, 40 + dgS / 2), dgS);
            for (int i = 0; i < 5; i++)
            {
                int k = i; float sz = at[i].s; var b = Box(); Abs(b, null, null, at[i].c.x - sz / 2, at[i].c.y - sz / 2); b.style.width = b.style.height = sz; b.pickingMode = PickingMode.Position;
                b.userData = new SkBtn(); b.generateVisualContent += ctx => DrawSkill(ctx, b, k); b.style.alignItems = Align.Center;
                var lbRow = Box(); Abs(lbRow, 0, null, 0, -16); lbRow.style.flexDirection = FlexDirection.Row; lbRow.style.justifyContent = Justify.Center; b.Add(lbRow);
                var lb = L("", touchUi ? 25 : 18, Ink, true); lb.style.unityTextAlign = TextAnchor.MiddleCenter;
                lb.style.backgroundColor = new Color(.05f, .08f, .1f, .82f); lb.style.paddingLeft = lb.style.paddingRight = 10; lb.style.paddingTop = 1; lb.style.paddingBottom = 3;
                lb.style.borderTopLeftRadius = lb.style.borderTopRightRadius = lb.style.borderBottomLeftRadius = lb.style.borderBottomRightRadius = 12; lb.name = "lb"; lbRow.Add(lb);
                b.Add(Layer(Gloss(true), sz * .06f));
                var cd = L("", sz * .3f, Color.white, true); Abs(cd, 0, 0, 0, 0); cd.style.unityTextAlign = TextAnchor.MiddleCenter; cd.style.unityTextOutlineWidth = .2f; cd.style.unityTextOutlineColor = new Color(0, 0, 0, .8f); cd.name = "cd"; b.Add(cd);
                var key = L((WebPc ? new[] { "A", "Q", "W", "E", "R" } : new[] { "J", "U", "I", "O", "␣" })[k], WebPc ? 19 : 15, WebPc ? Gold : Muted, WebPc); Abs(key, null, -6, -4); key.style.display = touchUi ? DisplayStyle.None : DisplayStyle.Flex; b.Add(key);
                b.RegisterCallback<PointerDownEvent>(e => { b.CapturePointer(e.pointerId); b.style.scale = new Scale(Vector3.one * .9f); ((SkBtn)b.userData).down = true; b.MarkDirtyRepaint(); Press(k, true); });
                System.Action<IPointerEvent> release = e => { if (b.HasPointerCapture(e.pointerId)) b.ReleasePointer(e.pointerId); b.style.scale = new Scale(Vector3.one); ((SkBtn)b.userData).down = false; b.MarkDirtyRepaint(); Press(k, false); };
                b.RegisterCallback<PointerUpEvent>(e => release(e)); b.RegisterCallback<PointerCancelEvent>(e => release(e));
                btns.Add(b); sk[k] = b;
            }
            {   // weapon swap (shown when an off-hand weapon is set): small, above the dodge button
                float ss = touchUi ? 110 : 76; var c = at[4].c + new Vector2(0, at[4].s / 2 + ss / 2 + 18);
                swapBtn = Box(new Color(.05f, .08f, .1f, .82f)); Abs(swapBtn, null, null, c.x - ss / 2, c.y - ss / 2); swapBtn.style.width = swapBtn.style.height = ss; Round(swapBtn, ss / 2);
                Border(swapBtn, new Color(1, .84f, .55f, .8f), 3); swapBtn.pickingMode = PickingMode.Position; swapBtn.style.justifyContent = Justify.Center; swapBtn.style.alignItems = Align.Center;
                var sl = L("⇄", touchUi ? 46 : 32, Gold, true); sl.pickingMode = PickingMode.Ignore; swapBtn.Add(sl);
                swapBtn.RegisterCallback<PointerDownEvent>(e => { swapP = true; e.StopPropagation(); }); btns.Add(swapBtn);
            }
            sk[0].Q<Label>("lb").style.display = DisplayStyle.None;   // the sword icon is enough for the big button
            bool pcBarOn = !touchUi && PcArt("hud_bar_frame"); if (pcBarOn) { BuildPcBar(); btns.style.display = DisplayStyle.None; }
            hint = L(WebPc ? Loc.S("คลิกขวา เดิน/โจมตีเป้า · คลิกซ้าย/A ฟัน · Q W E สกิล · R/Space หลบ", "Right-click move/attack · Left-click/A attack · Q W E skills · R/Space dodge") : Loc.S("WASD เดิน · J/คลิก ฟัน · U I O สกิล · Space หลบ", "WASD move · J/click attack · U I O skills · Space dodge"), 18, Muted); Abs(hint, 30, null, null, 14); hint.style.display = touchUi ? DisplayStyle.None : DisplayStyle.Flex; hud.Add(hint); if (pcBarOn) hint.style.display = DisplayStyle.None;
            banner = Box(); Abs(banner, 0, null, 0); banner.style.top = Length.Percent(17); banner.style.alignItems = Align.Center; hud.Add(banner);
            bannerA = L("", 72, Ink, true); bannerB = L("", 26, Muted); banner.Add(bannerA); banner.Add(bannerB); banner.style.opacity = 0;
        }
        class SkBtn { public float cd; public string id; public Color c = Gold; public bool down; public Texture2D tex; }
        static readonly Dictionary<string, Texture2D> iconCache = new();
        static Texture2D Icon(string file) { if (!iconCache.TryGetValue(file, out var t)) iconCache[file] = t = Resources.Load<Texture2D>("UI/Skills/" + file); return t; }
        void SetIcon(int k, string file) { var info = (SkBtn)sk[k].userData; info.tex = Icon(file); sk[k].style.backgroundImage = info.tex ? new StyleBackground(info.tex) : new StyleBackground(StyleKeyword.None); sk[k].MarkDirtyRepaint(); }
        // ---------- PC action bar (art in Resources/UI/PC, positions from hud_layout.json: 1600x260 frame) ----------
        static Texture2D PcArt(string n) => Resources.Load<Texture2D>("UI/PC/" + n);
        VisualElement pcBar, pcHp, pcMp, pcXp; readonly VisualElement[] pcSlot = new VisualElement[6], pcIcon = new VisualElement[6], pcShade = new VisualElement[6], pcRim = new VisualElement[6]; readonly Label[] pcCd = new Label[6];
        readonly float[] pcFlash = new float[6]; readonly VisualElement[] pcGlow = new VisualElement[6];
        // depth for skill buttons: a glassy highlight across the top, a darker rim, and a soft halo behind a ready skill
        static readonly Dictionary<bool, Texture2D> glossTex = new(); static Texture2D haloTex;
        static float Ss(float a, float b, float x) { x = Mathf.Clamp01((x - a) / (b - a)); return x * x * (3 - 2 * x); }   // shader-style smoothstep
        static Texture2D Gloss(bool round)
        {
            if (glossTex.TryGetValue(round, out var t)) return t; const int N = 128; t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {
                float u = (x + .5f) / N * 2 - 1, v = (y + .5f) / N * 2 - 1;   // v = 1 at the top
                float edge = round ? Mathf.Sqrt(u * u + v * v) : Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                if (round && edge > 1) { t.SetPixel(x, y, Color.clear); continue; }
                float rim = Ss(.55f, 1, edge) * .6f + Mathf.Clamp01(-v) * .25f;   // darker toward the rim and the bottom
                float hl = Ss(.05f, 1, v) * (1 - Ss(.5f, 1, Mathf.Abs(u))) * .3f;   // soft light from above, fading toward the middle
                float a = Mathf.Clamp01(rim + hl), w = a > 0 ? hl / (rim + hl) : 0; t.SetPixel(x, y, new Color(w, w, w, a));
            }
            t.Apply(); glossTex[round] = t; return t;
        }
        static Texture2D Halo()
        {
            if (haloTex) return haloTex; const int N = 128; haloTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float e = Mathf.Max(Mathf.Abs((x + .5f) / N * 2 - 1), Mathf.Abs((y + .5f) / N * 2 - 1)); float a = Ss(.72f, 1, e); haloTex.SetPixel(x, y, new Color(1, 1, 1, a * a)); }
            haloTex.Apply(); return haloTex;
        }
        static Texture2D softGlow;
        static Texture2D SoftGlow()
        {   // soft elliptical glow, brightest in the middle
            if (softGlow) return softGlow; const int W = 128, H = 64; softGlow = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++) { float u = (x + .5f) / W * 2 - 1, v = (y + .5f) / H * 2 - 1, d = Mathf.Sqrt(u * u + v * v); float a = Mathf.Clamp01(1 - d); softGlow.SetPixel(x, y, new Color(1, 1, 1, a * a * a)); }
            softGlow.Apply(); return softGlow;
        }
        static VisualElement Layer(Texture2D t, float inset) { var e = new VisualElement(); e.pickingMode = PickingMode.Ignore; e.style.position = Position.Absolute; e.style.left = e.style.top = e.style.right = e.style.bottom = inset; e.style.backgroundImage = new StyleBackground(t); e.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; return e; }
        void BuildPcBar()
        {
            const float K = .62f;   // 1600 px art -> 992 panel units wide
            VisualElement At(float x, float y, float w, float h, VisualElement parent) { var e = Box(); Abs(e, x * K, y * K); e.style.width = w * K; e.style.height = h * K; parent.Add(e); return e; }
            void Img(VisualElement e, string n) { var t = PcArt(n); if (t) { e.style.backgroundImage = new StyleBackground(t); e.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; } }
            pcBar = Box(); Abs(pcBar, null, null, null, 6); pcBar.style.width = 1600 * K; pcBar.style.height = 260 * K; pcBar.style.left = Length.Percent(50); pcBar.style.translate = new Translate(Length.Percent(-50), 0); hud.Add(pcBar);
            // orbs: liquid clipped from the top as it drains, glass on top
            foreach (var (cx, hpOrb) in new[] { (175f, true), (1425f, false) })
            {
                var orb = At(cx - 102, 136 - 102, 204, 204, pcBar); Round(orb, 102 * K); orb.style.overflow = Overflow.Hidden; orb.style.backgroundColor = new Color(.04f, .04f, .05f, .9f);
                var clip = Box(); Abs(clip, 0, null, 0, 0); clip.style.height = Length.Percent(100); clip.style.overflow = Overflow.Hidden; orb.Add(clip);
                var liq = Box(); Abs(liq, 0, null, 0, 0); liq.style.height = 204 * K; Img(liq, hpOrb ? "hud_orb_hp" : "hud_orb_mp"); clip.Add(liq);
                var glass = Box(); Abs(glass, 0, 0, 0, 0); Img(glass, "hud_orb_glass"); orb.Add(glass);
                if (hpOrb) pcHp = clip; else pcMp = clip;
            }
            var xp = At(300, 34, 1000, 40, pcBar); xp.style.overflow = Overflow.Hidden; pcXp = Box(); Abs(pcXp, 0, 0, null, 0); Img(pcXp, "hud_xp_bar"); pcXp.style.width = 1000 * K; xp.Add(pcXp);
            var xpClip = xp; xpClip.style.width = 1000 * K;
            string[] keys = WebPc ? new[] { "A", "Q", "W", "E", "R", "" } : new[] { "J", "U", "I", "O", "␣", "" };
            for (int i = 0; i < 6; i++)
            {
                int k = i; float sx = 321 + i * 164;                 var slot = At(sx, 100, 138, 138, pcBar); slot.pickingMode = PickingMode.Position; pcSlot[i] = slot;
                pcIcon[i] = Box(); Abs(pcIcon[i], 8 * K, 8 * K, 8 * K, 8 * K); pcIcon[i].style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; slot.Add(pcIcon[i]);
                slot.Add(Layer(Gloss(false), 8 * K)); pcGlow[i] = Layer(Halo(), 8 * K); pcGlow[i].style.unityBackgroundImageTintColor = new Color(1, .85f, .5f); pcGlow[i].style.opacity = 0; slot.Add(pcGlow[i]);
                pcShade[i] = Box(new Color(0, 0, 0, .62f)); Abs(pcShade[i], 8 * K, 8 * K, 8 * K, null); pcShade[i].style.height = 0; slot.Add(pcShade[i]);
                pcRim[i] = Box(); Abs(pcRim[i], 0, 0, 0, 0); Img(pcRim[i], "hud_slot"); slot.Add(pcRim[i]);
                pcCd[i] = L("", 40, Color.white, true); Abs(pcCd[i], 0, 0, 0, 0); pcCd[i].style.unityTextAlign = TextAnchor.MiddleCenter; Shadow(pcCd[i], 6, .9f); slot.Add(pcCd[i]);
                if (keys[i] != "") { var kl = L(keys[i], 14, new Color(1, .9f, .62f), true); Abs(kl, null, null, null, 4); kl.style.left = 0; kl.style.right = 0; kl.style.unityTextAlign = TextAnchor.MiddleCenter; Shadow(kl, 3, .9f); slot.Add(kl); }
                if (i < 5) { int b = new[] { 0, 1, 2, 3, 4 }[i]; slot.RegisterCallback<PointerDownEvent>(e => { pcFlash[k] = .25f; Press(b, true); e.StopPropagation(); }); slot.RegisterCallback<PointerUpEvent>(e => Press(b, false)); }
            }
            var frame = Box(); Abs(frame, 0, 0, 0, 0); Img(frame, "hud_bar_frame"); frame.pickingMode = PickingMode.Ignore; pcBar.Add(frame);
            // the slots sit under the frame art; picking ignores the frame so clicks reach them
        }
        void UpdPcBar(Hero h)
        {
            if (viewBtn != null) { var vt = ViewLabel(); if (viewBtn.text != vt) viewBtn.text = vt; }   // the phone default is only known once the HUD runs
            if (swapBtn != null) swapBtn.style.display = h.OffHand != null && !G.soul ? DisplayStyle.Flex : DisplayStyle.None;
            if (pcBar == null) return;
            pcHp.style.height = Length.Percent(Mathf.Clamp01(h.hp / h.max) * 100); pcMp.style.height = Length.Percent(Mathf.Clamp01(h.mp / h.mpMax) * 100);
            int nw = G.Theme.waves.Length; float prog = G.lane ? 1 - G.TowersLeft(1) / 7f : Mathf.Clamp01((G.wave + (G.wave < nw ? 0 : 1)) / (float)(nw + 1));
            pcXp.style.width = Length.Percent(prog * 100); pcXp.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            for (int i = 0; i < 6; i++)
            {
                Texture2D tex = i < 5 ? ((SkBtn)sk[i].userData).tex : G.pet && G.pet.def != null ? Studio.Pet(G.pet.def.id) : null;
                if (tex != null && pcIcon[i].style.backgroundImage.value.texture != tex) pcIcon[i].style.backgroundImage = new StyleBackground(tex);
                float cdK = 0, left = 0;
                if (i >= 1 && i <= 3 && !G.soul && Data.Skill(((SkBtn)sk[i].userData).id) != null) { var info = (SkBtn)sk[i].userData; float cdMax = Data.Skill(info.id).cd; left = Mathf.Max(0, h.cds[i - 1]); cdK = left / cdMax; if (!G.soul && h.mp < Hero.SkillCost(cdMax)) cdK = Mathf.Max(cdK, .001f); }
                else if (i == 4) { left = Mathf.Max(0, h.dodgeCD); cdK = left / 1.1f; }
                pcShade[i].style.height = Length.Percent(cdK * 100); pcCd[i].text = left > .05f ? Mathf.CeilToInt(left).ToString() : "";
                pcIcon[i].style.unityBackgroundImageTintColor = cdK > 0 && left <= .05f ? new Color(.55f, .6f, 1) : Color.white;   // not enough power: bluish
                float ready = i < 5 && cdK <= 0 ? .45f + .25f * Mathf.Sin(Time.unscaledTime * 2.4f + i) : 0; pcGlow[i].style.opacity = Mathf.Max(ready, pcFlash[i] > 0 ? 1 : 0);
                pcFlash[i] -= Time.unscaledDeltaTime; var sel = PcArt(pcFlash[i] > 0 || (i == 0 && h.state == Hero.St.Atk) ? "hud_slot_selected" : "hud_slot");
                if (pcRim[i].style.backgroundImage.value.texture != sel) pcRim[i].style.backgroundImage = new StyleBackground(sel);
            }
        }
        void Press(int k, bool down)
        {
            if (G.soul)
            {   // light, heavy, hold-to-block, lock-on, dodge
                if (k == 0) { if (down) atkP = true; } else if (k == 1) { if (down) heavyP = true; } else if (k == 2) blockHold = down; else if (k == 3) { if (down) lockP = true; } else if (down) dodgeP = true;
                return;
            }
            if (k == 0) { if (down) atkP = true; atkHold = down; }
            else if (down) { if (k == 4) dodgeP = true; else skP[k - 1] = true; }
        }
        void JoyHome() { var w = joyZone.resolvedStyle.width; var h = joyZone.resolvedStyle.height; if (float.IsNaN(w) || w <= 0) return; joyBase.style.left = Mathf.Max(60 + 100 * J, w * .32f) - 100 * J; joyBase.style.top = h - 200 * J - 50; }

        // vector skill icons (24x24 grid polylines) + cooldown sweep
        static readonly Dictionary<string, float[][]> Icons = new()
        {
            ["atk"] = new[] { new float[] { 5, 19, 19, 5 }, new float[] { 15, 5, 19, 5, 19, 9 }, new float[] { 7, 13, 11, 17 } },
            ["dodge"] = new[] { new float[] { 4, 7, 9, 12, 4, 17 }, new float[] { 11, 7, 16, 12, 11, 17 }, new float[] { 18, 6, 18, 18 } },
            ["whirl"] = new[] { Spiral() }, ["slam"] = new[] { new float[] { 12, 3, 12, 14 }, new float[] { 8, 10, 12, 14, 16, 10 }, new float[] { 4, 20, 20, 20 } },
            ["cry"] = new[] { new float[] { 9, 9, 6, 12, 9, 15 }, Arc(10, 12, 5, -1, 1), Arc(10, 12, 9, -1.1f, 1.1f) },
            ["iaido"] = new[] { new float[] { 3, 17, 21, 7 }, new float[] { 4, 11, 10, 11 }, new float[] { 6, 20, 12, 20 } },
            ["petal"] = new[] { Arc(12, 7, 4, 0, 6.3f), Arc(17, 12, 4, 0, 6.3f), Arc(12, 17, 4, 0, 6.3f), Arc(7, 12, 4, 0, 6.3f) },
            ["dance"] = new[] { new float[] { 4, 20, 20, 4 }, new float[] { 4, 4, 20, 20 }, new float[] { 12, 2, 12, 6 }, new float[] { 12, 18, 12, 22 } },
            ["feather"] = new[] { new float[] { 4, 20, 12, 12 }, new float[] { 12, 12, 13, 6, 20, 3, 19, 8, 12, 12 } },
            ["dive"] = new[] { new float[] { 5, 4, 12, 17, 19, 4 }, new float[] { 4, 20, 20, 20 } },
            ["gale"] = new[] { new float[] { 4, 6, 18, 6 }, new float[] { 6, 10, 16, 10 }, new float[] { 8, 14, 15, 14 }, new float[] { 10, 18, 14, 18 } },
            ["laser"] = new[] { new float[] { 3, 12, 7, 12 }, new float[] { 9, 12, 21, 12 }, new float[] { 7, 9, 7, 15 } },
            ["missile"] = new[] { new float[] { 5, 19, 10, 14 }, new float[] { 14, 4, 20, 4, 20, 10, 12, 18, 6, 12, 14, 4 } },
            ["heal"] = new[] { new float[] { 12, 4, 12, 20 }, new float[] { 4, 12, 20, 12 }, Arc(12, 12, 9, 0, 6.3f) },
            ["shield"] = new[] { new float[] { 12, 3, 20, 6, 20, 12, 12, 21, 4, 12, 4, 6, 12, 3 } },
            ["lock"] = new[] { Arc(12, 12, 7, 0, 6.3f), new float[] { 12, 2, 12, 7 }, new float[] { 12, 17, 12, 22 }, new float[] { 2, 12, 7, 12 }, new float[] { 17, 12, 22, 12 } },
        };
        static float[] Arc(float cx, float cy, float r, float a0, float a1) { var l = new List<float>(); for (int i = 0; i <= 16; i++) { float a = Mathf.Lerp(a0, a1, i / 16f); l.Add(cx + Mathf.Cos(a) * r); l.Add(cy + Mathf.Sin(a) * r); } return l.ToArray(); }
        static float[] Spiral() { var l = new List<float>(); for (int i = 0; i <= 40; i++) { float a = i / 40f * 9, r = 1 + i / 40f * 8; l.Add(12 + Mathf.Cos(a) * r); l.Add(12 + Mathf.Sin(a) * r); } return l.ToArray(); }
        // glossy disc: soft drop shadow, dark body tinted by the skill colour, top highlight, coloured rim; cooldown dims it with a sweep
        void DrawSkill(MeshGenerationContext ctx, VisualElement b, int k)
        {
            var info = (SkBtn)b.userData; var p = ctx.painter2D; float w = b.resolvedStyle.width; if (float.IsNaN(w)) return;
            string id = k == 0 ? "atk" : k == 4 ? "dodge" : info.id; var c = new Vector2(w / 2, w / 2); float R = w / 2 - 6;
            void Disc(Vector2 at, float r, Color col) { p.fillColor = col; p.BeginPath(); p.Arc(at, r, 0, 360); p.Fill(); }
            bool ready = info.cd <= 0;
            if (info.tex)
            {   // painted icon is the background: add press flash, cooldown sweep and a thin rim on top
                if (info.down) Disc(c, R, new Color(1, 1, 1, .18f));
                if (!ready)
                {
                    p.fillColor = new Color(0, 0, 0, .6f); p.BeginPath(); p.MoveTo(c); p.Arc(c, R, -90, -90 + 360 * info.cd); p.ClosePath(); p.Fill();
                    var pc = Color.Lerp(info.c, Color.white, .3f); p.strokeColor = pc; p.lineWidth = 6; p.BeginPath(); p.Arc(c, R - 1, -90, -90 + 360 * (1 - info.cd)); p.Stroke();
                }
                else { p.strokeColor = new Color(1, 1, 1, .45f); p.lineWidth = 2.5f; p.BeginPath(); p.Arc(c, R - 1, 0, 360); p.Stroke(); }
                return;
            }
            for (int i = 3; i >= 1; i--) Disc(c + new Vector2(0, 3), R + i * 2f, new Color(0, 0, 0, .1f));
            var body = Color.Lerp(new Color(.07f, .1f, .13f), info.c, ready ? .28f : .1f); if (info.down) body = Color.Lerp(body, info.c, .35f); body.a = .9f;
            Disc(c, R, body);
            var inner = Color.Lerp(body, Color.black, .25f); inner.a = .9f; Disc(c, R * .84f, inner);
            var hi = Color.Lerp(body, Color.white, .16f); hi.a = .45f; Disc(c + new Vector2(0, -R * .3f), R * .62f, hi);
            if (id != null && Icons.ContainsKey(id))
            {
                float s = w * .5f / 24, o = (w - 24 * s) / 2;
                p.strokeColor = ready ? Color.white : new Color(1, 1, 1, .55f); p.lineWidth = Mathf.Max(3.5f, w * .05f); p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round;
                foreach (var line in Icons[id]) { p.BeginPath(); p.MoveTo(new Vector2(o + line[0] * s, o + line[1] * s)); for (int i = 2; i < line.Length; i += 2) p.LineTo(new Vector2(o + line[i] * s, o + line[i + 1] * s)); p.Stroke(); }
            }
            if (!ready)
            {
                p.fillColor = new Color(0, 0, 0, .55f); p.BeginPath(); p.MoveTo(c);
                p.Arc(c, R, -90, -90 + 360 * info.cd); p.ClosePath(); p.Fill();
            }
            var rim = info.c; rim.a = ready ? 1 : .45f; var glow = rim; glow.a *= .25f;
            p.strokeColor = glow; p.lineWidth = 9; p.BeginPath(); p.Arc(c, R - 1, 0, 360); p.Stroke();
            p.strokeColor = rim; p.lineWidth = k == 0 ? 5 : 4; p.BeginPath(); p.Arc(c, R - 1, 0, 360); p.Stroke();
            if (!ready) { var prog = info.c; prog.a = 1; p.strokeColor = prog; p.lineWidth = 5; p.BeginPath(); p.Arc(c, R - 1, -90, -90 + 360 * (1 - info.cd)); p.Stroke(); }
        }
        void DrawMini(MeshGenerationContext ctx)
        {
            if (G == null || G.hero == null) return; var p = ctx.painter2D; float W = mini.resolvedStyle.width; if (float.IsNaN(W)) return; float S = W / 2 / ((G.lane ? Lane.HalfL : Data.ArenaR) + 3); var c = new Vector2(W / 2, W / 2);
            Vector2 M(Vector3 w) => c + new Vector2(-w.x, w.z) * S;   // screen-right is world -X
            if (G.lane && Lane.Lanes == 3)
            {   // three lane strips that meet at both bases, a river across the middle
                p.strokeColor = new Color(.7f, .55f, .32f, .55f); p.lineWidth = 5.5f * S; p.lineJoin = LineJoin.Round;
                foreach (float x in Lane.LaneX3) { p.BeginPath(); p.MoveTo(M(new Vector3(0, 0, -Lane.CoreZ))); p.LineTo(M(new Vector3(x, 0, -30))); p.LineTo(M(new Vector3(x, 0, 30))); p.LineTo(M(new Vector3(0, 0, Lane.CoreZ))); p.Stroke(); }
                p.strokeColor = new Color(.3f, .6f, .9f, .5f); p.lineWidth = 3 * S; p.BeginPath(); p.MoveTo(M(new Vector3(-Lane.HalfW, 0, 0))); p.LineTo(M(new Vector3(Lane.HalfW, 0, 0))); p.Stroke();
            }
            else if (G.lane) { p.fillColor = new Color(.6f, .45f, .28f, .35f); p.BeginPath(); var a0 = M(new Vector3(Lane.HalfW, 0, -Lane.HalfL)); var a1 = M(new Vector3(-Lane.HalfW, 0, Lane.HalfL)); p.MoveTo(a0); p.LineTo(new Vector2(a1.x, a0.y)); p.LineTo(a1); p.LineTo(new Vector2(a0.x, a1.y)); p.ClosePath(); p.Fill(); }
            else { p.strokeColor = new Color(.6f, .45f, .28f, .55f); p.lineWidth = S * 1.4f; p.BeginPath(); p.Arc(c, 11 * S, 0, 360); p.Stroke(); }
            foreach (var f in G.foes) if (f && f.alive) { bool big = f.isBoss || f.d.kind == "tower"; p.fillColor = f.isBoss ? new Color(.82f, .44f, 1) : f.team == 0 ? new Color(.35f, .7f, 1) : Red; p.BeginPath(); p.Arc(M(f.Pos), big ? 10 : f.d.kind == "champ" ? 8 : 5, 0, 360); p.Fill(); }
            p.fillColor = new Color(1, .88f, .54f); p.BeginPath(); p.Arc(M(G.pet.Pos), 5, 0, 360); p.Fill();
            var hp = M(G.hero.Pos); float a = G.hero.face; Vector2 R(float x, float y) => hp + new Vector2(-(x * Mathf.Cos(a) + y * Mathf.Sin(a)), -x * Mathf.Sin(a) + y * Mathf.Cos(a));
            p.fillColor = Cyan; p.BeginPath(); p.MoveTo(R(0, 13)); p.LineTo(R(-9, -8)); p.LineTo(R(9, -8)); p.ClosePath(); p.Fill();
        }

        public void ShowHud()
        {
            lobby.style.display = DisplayStyle.None; hud.style.display = DisplayStyle.Flex; resultOv.style.display = pauseOv.style.display = DisplayStyle.None; bossW.style.display = DisplayStyle.None;
            var h = G.hero.def; hudName.text = h.name; petN.text = G.pet.def.name;
            porImg.style.backgroundImage = new StyleBackground(Studio.Hero(h.id)); petImg.style.backgroundImage = new StyleBackground(Studio.Pet(G.pet.def.id));
            for (int i = 0; i < 3; i++) { var d = Data.SkillLook(h.id, h.skills[i]); var info = (SkBtn)sk[i + 1].userData; info.id = h.skills[i]; info.c = d.c; sk[i + 1].Q<Label>("lb").text = d.n; SetIcon(i + 1, $"skill_{h.id}_{h.skills[i]}"); }
            SetIcon(0, G.soul ? "btn_souls_light" : "btn_attack_" + Data.AttackIcon(h)); SetIcon(4, "btn_dodge");
            if (G.soul)
            {
                var ids = new[] { "slam", "shield", "lock" }; var names = new[] { Loc.S("หนัก", "Heavy"), Loc.S("ป้องกัน", "Block"), Loc.S("ล็อกเป้า", "Lock-on") };
                for (int i = 0; i < 3; i++) { var info = (SkBtn)sk[i + 1].userData; info.id = ids[i]; info.cd = 0; info.c = i == 1 ? Cyan : Gold; sk[i + 1].Q<Label>("lb").text = names[i]; sk[i + 1].Q<Label>("cd").text = ""; SetIcon(i + 1, new[] { "btn_souls_heavy", "btn_souls_guard", "btn_souls_lock" }[i]); }
            }
            stamBar.style.display = G.soul ? DisplayStyle.Flex : DisplayStyle.None; lookZone.style.display = G.soul ? DisplayStyle.Flex : DisplayStyle.None;
            if (hint != null) hint.text = G.soul ? Loc.S("WASD เดิน · เมาส์หมุนกล้อง · คลิกซ้าย เบา · คลิกขวา หนัก · Q ป้องกัน/ปัดป้อง · Space หลบ · Tab ล็อกเป้า", "WASD move · Mouse turns camera · Left-click light · Right-click heavy · Q block/parry · Space dodge · Tab lock-on") : WebPc ? Loc.S("คลิกขวา เดิน/โจมตีเป้า · คลิกซ้าย/A ฟัน · Q W E สกิล · R/Space หลบ", "Right-click move/attack · Left-click/A attack · Q W E skills · R/Space dodge") : Loc.S("WASD เดิน · J/คลิก ฟัน · U I O สกิล · Space หลบ", "WASD move · J/click attack · U I O skills · Space dodge");
            ((SkBtn)sk[0].userData).c = G.hero.sword.color; ((SkBtn)sk[4].userData).c = new Color(.62f, .9f, 1);
            Joy = Vector2.zero; ConsumeInputs(); JoyHome();
        }
        public void ShowBoss(string name) { bossW.style.display = DisplayStyle.Flex; bossN.text = name; }
        public void HideBoss() { bossW.style.display = DisplayStyle.None; }
        float bannerT;
        public void Banner(string a, string b = "") { bannerA.text = a; bannerB.text = b; bannerT = 2.4f; }

        // ---------- numbers & enemy bars ----------
        class Num { public Label l; public Vector3 p; public float t; }
        readonly List<Num> numList = new(); readonly Stack<Label> numPool = new(); readonly List<VisualElement> barPool = new();
        public void Number(Vector3 p, string txt, string cls)
        {
            var l = numPool.Count > 0 ? numPool.Pop() : null;
            if (l == null) { l = L("", 30, Color.white, true); l.style.position = Position.Absolute; l.style.unityTextOutlineWidth = .25f; l.style.unityTextOutlineColor = Color.black; nums.Add(l); }
            l.text = txt; l.style.display = DisplayStyle.Flex;
            l.style.color = cls switch { "crit" => new Color(1, .82f, .23f), "hurt" => new Color(1, .35f, .41f), "heal" => Green, "gold" => Gold, _ => Color.white };
            l.style.fontSize = cls == "crit" ? 42 : cls == "gold" ? 24 : 30;
            numList.Add(new Num { l = l, p = p + new Vector3(Random.Range(-.3f, .3f), 0, Random.Range(-.2f, .2f)) });
        }
        public void ClearNums() { foreach (var n in numList) { n.l.style.display = DisplayStyle.None; numPool.Push(n.l); } numList.Clear(); }
        Vector2 ToPanel(Vector3 w) => RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, w, G.cam);

        // ---------- overlays ----------
        void BuildOverlays()
        {
            pauseOv = Box(new Color(0, 0, 0, .7f)); Abs(pauseOv, 0, 0, 0, 0); pauseOv.style.alignItems = Align.Center; pauseOv.style.justifyContent = Justify.Center; pauseOv.pickingMode = PickingMode.Position; root.Add(pauseOv);
            var pb = Box(Panel, 18); Border(pb, Line); Pad(pb, 30); pb.style.alignItems = Align.Center; pauseOv.Add(pb);
            pb.Add(L(Loc.S("หยุดเกม", "Paused"), 56, Ink, true)); var row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.marginTop = 20; pb.Add(row);
            row.Add(Btn(Loc.S("เล่นต่อ", "Resume"), TogglePause)); var q = Btn(Loc.S("กลับล็อบบี้", "Back to Lobby"), () => { TogglePause(); G.ShowLobby(); }, false); q.style.marginLeft = 14; row.Add(q);
            confirmOv = Box(new Color(0, 0, 0, .7f)); Abs(confirmOv, 0, 0, 0, 0); confirmOv.style.alignItems = Align.Center; confirmOv.style.justifyContent = Justify.Center; confirmOv.pickingMode = PickingMode.Position; root.Add(confirmOv);
            var cb = Box(Panel, 18); Border(cb, Line); Pad(cb, 28); cb.style.alignItems = Align.Center; cb.style.width = 700; confirmOv.Add(cb);
            cb.Add(L(Loc.S("ยืนยันการซื้อ", "Confirm Purchase"), 44, Ink, true)); confirmT = L("", 24, Muted); confirmT.style.whiteSpace = WhiteSpace.Normal; confirmT.style.unityTextAlign = TextAnchor.MiddleCenter; confirmT.style.marginTop = 10; cb.Add(confirmT);
            var crow = Box(); crow.style.flexDirection = FlexDirection.Row; crow.style.marginTop = 20; cb.Add(crow);
            crow.Add(Btn(Loc.S("ยืนยัน", "Confirm"), () => DoBuy())); var cn = Btn(Loc.S("ยกเลิก", "Cancel"), () => confirmOv.style.display = DisplayStyle.None, false); cn.style.marginLeft = 14; crow.Add(cn);
            confirmOv.style.display = DisplayStyle.None;
            // hero details: the hero's three skills with what each does; closes with the X or a tap outside
            skillOv = Box(new Color(0, 0, 0, .55f)); Abs(skillOv, 0, 0, 0, 0); skillOv.style.alignItems = Align.Center; skillOv.style.justifyContent = Justify.Center; skillOv.pickingMode = PickingMode.Position; root.Add(skillOv);
            skillOv.RegisterCallback<PointerDownEvent>(e => { if (e.target == skillOv) skillOv.style.display = DisplayStyle.None; });
            skillBox = Box(Panel, 20); Border(skillBox, new Color(1, .84f, .45f, .6f), 2); Pad(skillBox, 28); skillBox.style.width = touchUi ? 1100 : 940; skillBox.pickingMode = PickingMode.Position; skillOv.Add(skillBox);
            skillOv.style.display = DisplayStyle.None;
            resultOv = Box(new Color(0, 0, 0, .7f)); Abs(resultOv, 0, 0, 0, 0); resultOv.style.alignItems = Align.Center; resultOv.style.justifyContent = Justify.Center; resultOv.pickingMode = PickingMode.Position; root.Add(resultOv);
            var rb = Box(Panel, 18); Border(rb, Line); Pad(rb, 30); rb.style.alignItems = Align.Center; rb.style.width = 760; resultOv.Add(rb);
            resH = L("", 64, Gold, true); rb.Add(resH); resS = L("", 22, Muted); resS.style.whiteSpace = WhiteSpace.Normal; resS.style.unityTextAlign = TextAnchor.MiddleCenter; resS.style.marginTop = 8; rb.Add(resS);
            var grid = Box(); grid.style.flexDirection = FlexDirection.Row; grid.style.marginTop = 18; rb.Add(grid);
            foreach (var (lbl, set) in new (string, System.Action<Label>)[] { (Loc.S("กำจัด", "Kills"), l => resK = l), (Loc.S("ทอง", "Gold"), l => resG = l), (Loc.S("เวลา", "Time"), l => resT = l) })
            { var cell = Box(new Color(1, 1, 1, .05f), 10); Pad(cell, 12); cell.style.width = 200; cell.style.marginLeft = cell.style.marginRight = 6; cell.style.alignItems = Align.Center; var v = L("0", 40, Ink, true); set(v); cell.Add(v); cell.Add(L(lbl, 20, Muted)); grid.Add(cell); }
            var r2 = Box(); r2.style.flexDirection = FlexDirection.Row; r2.style.marginTop = 22; rb.Add(r2);
            again = Btn("", () => { }); r2.Add(again); var tl = Btn(Loc.S("ล็อบบี้", "Lobby"), () => G.ShowLobby(), false); tl.style.marginLeft = 14; r2.Add(tl);
            pauseOv.style.display = resultOv.style.display = DisplayStyle.None;
        }
        System.Action againAct;
        (ItemKind kind, string id, string name) pending; Button buyBtn; VisualElement confirmOv; Label confirmT, gemsL;
        void AskBuy()
        {
            if (pending.id == null) return; var (p, c) = Shop.Price(pending.kind, pending.id);
            confirmT.text = (Loc.En ? $"{pending.name}\nPrice {Shop.PriceText(pending.kind, pending.id)} · You have {Shop.Balance(c):N0} {(c == Currency.Gold ? "Gold" : "Gem")}" : $"{pending.name}\nราคา {Shop.PriceText(pending.kind, pending.id)} · มีอยู่ {Shop.Balance(c):N0} {(c == Currency.Gold ? "Gold" : "Gem")}") + (c == Currency.Gem ? Loc.S("\n(Gem สำหรับทดสอบ ยังไม่มีการจ่ายเงินจริง)", "\n(Test Gems, no real payment yet)") : "");
            confirmOv.style.display = DisplayStyle.Flex;
        }
        void DoBuy()
        {
            var (k, id, _) = pending;
            if (!Shop.Buy(k, id, out var msg)) { confirmT.text = msg; return; }
            var s = G.save;
            if (k == ItemKind.HeroSkin) G.SetSkin(G.hero.def.id, int.Parse(id.Split(':')[1]));
            else if (k == ItemKind.Weapon) s.sword = id;
            else if (k == ItemKind.Wings) s.wings = id;
            else if (k == ItemKind.Shield) s.shield = id;
            else { var weapon = Data.Swords.FirstOrDefault(w => w.id == s.sword) ?? Data.Swords[0]; s.SetWeaponSkin(weapon.id, id); }
            confirmOv.style.display = DisplayStyle.None; G.ClearPreview(); Fx.Burst(Vector3.up, 40, Gold, 4, 1.4f, .1f); Pick();
        }
        public void ShowResult(bool win)
        {
            resultOv.style.display = DisplayStyle.Flex; resH.text = win ? Loc.S("ชนะแล้ว!", "Victory!") : Loc.S("พ่ายแพ้…", "Defeated…"); resH.style.color = win ? Gold : Red;
            var T = G.Theme;
            if (G.lane)
            {
                resH.text = win ? Loc.S("ชนะสนามรบ!", "Battle Won!") : Loc.S("พ่ายแพ้…", "Defeated…"); resS.text = win ? Loc.S("ทำลายคริสตัลหลักของฝ่ายตรงข้ามได้สำเร็จ", "You destroyed the enemy Core Crystal") : Loc.S("คริสตัลหลักของเราถูกทำลาย ลองตั้งรับใกล้ป้อมแล้วบุกพร้อมทหาร", "Our Core Crystal fell. Hold near your towers and push with your soldiers");
                resK.text = G.kills.ToString(); resG.text = G.gold.ToString(); int ls = Mathf.RoundToInt(G.time); resT.text = $"{ls / 60}:{ls % 60:00}";
                again.text = Loc.S("เล่นอีกครั้ง", "Play Again"); if (againAct != null) again.clicked -= againAct; int size = G.laneSize, lanes = G.laneCount; againAct = () => G.StartLane(size, lanes); again.clicked += againAct; return;
            }
            resS.text = win ? (Loc.En ? $"Defeated {T.boss.name} on stage {G.stage} · Next: {Data.Themes[Data.ThemeOf(G.stage + 1)].name}" : $"ปราบ{T.boss.name}แห่งด่าน {G.stage} ได้สำเร็จ · ด่านต่อไป: {Data.Themes[Data.ThemeOf(G.stage + 1)].name}") : (Loc.En ? $"Fell in wave {Mathf.Min(G.wave + 1, 4)} of stage {G.stage} · Roll away when you see a red circle" : $"ล้มลงในคลื่นที่ {Mathf.Min(G.wave + 1, 4)} ของด่าน {G.stage} · ลองกลิ้งหลบตอนเห็นวงแดง");
            resK.text = G.kills.ToString(); resG.text = G.gold.ToString(); int s = Mathf.RoundToInt(G.time); resT.text = $"{s / 60}:{s % 60:00}";
            again.text = win ? (Loc.En ? $"Stage {G.stage + 1} ▶" : $"ด่าน {G.stage + 1} ▶") : Loc.S("ลองอีกครั้ง", "Try Again");
            if (againAct != null) again.clicked -= againAct; int st = G.stage; againAct = () => G.StartGame(win ? st + 1 : st, win); again.clicked += againAct;
        }
        void TogglePause() { if (G.mode != Main.Mode.Game) return; G.paused = !G.paused; UnityEngine.Cursor.lockState = G.soul && !G.paused && !Application.isMobilePlatform ? UnityEngine.CursorLockMode.Locked : UnityEngine.CursorLockMode.None; Time.timeScale = G.paused ? 0 : 1; pauseOv.style.display = G.paused ? DisplayStyle.Flex : DisplayStyle.None; }
        void OnApplicationPause(bool p) { if (p && G && G.mode == Main.Mode.Game && !G.paused) TogglePause(); }

        public string PickName() { var pt = Pointer.current; if (pt == null || root?.panel == null) return "-"; var sp = pt.position.ReadValue(); sp.y = Screen.height - sp.y; var e = root.panel.Pick(RuntimePanelUtils.ScreenToPanel(root.panel, sp)); return e == null ? "none" : $"{e.GetType().Name}:{e.name}:{e.parent?.name}"; }
        public bool PointerOverUi
        {
            get { var pt = Pointer.current; if (pt == null || root?.panel == null) return false; var sp = pt.position.ReadValue(); sp.y = Screen.height - sp.y; var pp = RuntimePanelUtils.ScreenToPanel(root.panel, sp); var e = root.panel.Pick(pp); return e != null; }
        }

        // ---------- per frame ----------
        float dragX; bool dragging;
        void Update()
        {
            if (skillOv != null && G && G.mode != Main.Mode.Lobby && skillOv.style.display == DisplayStyle.Flex) skillOv.style.display = DisplayStyle.None;   // never over a fight
            if (G == null || G.hero == null) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) TogglePause();
            float dt = Time.unscaledDeltaTime;
            if (G.mode == Main.Mode.Lobby)
            {   // drag on empty space rotates the hero
                var pt = Pointer.current;
                if (pt != null)
                {
                    if (pt.press.wasPressedThisFrame && !PointerOverUi) { dragging = true; dragX = pt.position.ReadValue().x; }
                    if (!pt.press.isPressed) dragging = false;
                    if (dragging) { float x = pt.position.ReadValue().x; G.hero.transform.Rotate(0, (x - dragX) * .4f, 0); dragX = x; }
                }
                return;
            }
            if (G.mode != Main.Mode.Game && G.mode != Main.Mode.Result) return;
            var h = G.hero;
            hpFill.style.width = Length.Percent(Mathf.Clamp01(h.hp / h.max) * 100); hpT.text = $"{Mathf.CeilToInt(h.hp)} / {h.max:0}";
            mpFill.style.width = Length.Percent(Mathf.Clamp01(h.mp / h.mpMax) * 100); mpT.text = $"{Mathf.FloorToInt(h.mp)} / {h.mpMax:0}"; goldH.text = G.gold.ToString();
            int left = G.foes.Count(f => f && f.alive);
            int nw = G.Theme.waves.Length;
            if (G.lane) waveT.text = (Loc.En ? $"Battle {G.laneSize} vs {G.laneSize} · Our towers {G.TowersLeft(0)} · Enemy towers {G.TowersLeft(1)} · Wave {G.wave}" : $"สนามรบ {G.laneSize} vs {G.laneSize} · ป้อมเรา {G.TowersLeft(0)} · ป้อมศัตรู {G.TowersLeft(1)} · ระลอก {G.wave}");
            else waveT.text = G.wave < nw ? (Loc.En ? $"Stage {G.stage} · {G.Theme.name} · Wave {G.wave + 1}/{nw} · Enemies {left}" : $"ด่าน {G.stage} · {G.Theme.name} · คลื่น {G.wave + 1}/{nw} · ศัตรู {left}") : (Loc.En ? $"Stage {G.stage} · {G.Theme.name} · Boss Fight" : $"ด่าน {G.stage} · {G.Theme.name} · ศึกบอส");
            if (G.boss) bossFill.style.width = Length.Percent(Mathf.Clamp01(G.boss.hp / G.boss.maxHp) * 100);
            buffs.Clear(); if (h.buffT > 0) buffs.Add(Tag((Loc.En ? $"Battle Fury {Mathf.CeilToInt(h.buffT)}" : $"คลั่งศึก {Mathf.CeilToInt(h.buffT)}"), Red)); if (h.shieldT > 0) buffs.Add(Tag((Loc.En ? $"Barrier {Mathf.CeilToInt(h.shieldT)}" : $"บาเรีย {Mathf.CeilToInt(h.shieldT)}"), Cyan));
            if (G.soul)
            {
                var sc = h.soul; if (sc) stamFill.style.width = Length.Percent(Mathf.Clamp01(sc.stamina / sc.staminaMax) * 100);
                if (G.boss) poiseFill.style.width = Length.Percent(Mathf.Clamp01(G.boss.Broken ? 1 : G.boss.poise / G.boss.poiseMax) * 100);
                var lt = sc ? sc.lockTarget : null; reticle.style.display = lt ? DisplayStyle.Flex : DisplayStyle.None;
                if (lt) { var rp = ToPanel(lt.Pos + Vector3.up * lt.h * .55f); reticle.style.left = rp.x - 23; reticle.style.top = rp.y - 23; Border(reticle, lt.Broken ? Gold : new Color(1, .35f, .3f), 3); }
            }
            else reticle.style.display = DisplayStyle.None;
            if (!G.soul) for (int i = 0; i < 3; i++)
            {
                var info = (SkBtn)sk[i + 1].userData; float cdMax = Data.Skill(info.id).cd, v = Mathf.Max(0, h.cds[i]); float n = v / cdMax;
                if (Mathf.Abs(n - info.cd) > .002f) { info.cd = n; sk[i + 1].MarkDirtyRepaint(); }
                sk[i + 1].Q<Label>("cd").text = v > 0 ? Mathf.CeilToInt(v).ToString() : "";
            }
            var dinfo = (SkBtn)sk[4].userData; float dn = Mathf.Max(0, h.dodgeCD) / 1.1f; if (Mathf.Abs(dn - dinfo.cd) > .002f) { dinfo.cd = dn; sk[4].MarkDirtyRepaint(); }
            mini.MarkDirtyRepaint(); UpdPcBar(h);
            if (bannerT > 0) { bannerT -= dt; float k = 1 - bannerT / 2.4f; banner.style.opacity = k < .12f ? k / .12f : k > .78f ? (1 - k) / .22f : 1; banner.style.scale = new Scale(Vector3.one * (k < .12f ? 1.25f - k / .12f * .25f : 1)); }
            // damage numbers
            for (int i = numList.Count - 1; i >= 0; i--)
            {
                var n = numList[i]; n.t += dt; var pp = ToPanel(n.p + Vector3.up * n.t * 1.6f);
                float s = n.t < .12f ? 1.4f - n.t * 3 : 1; n.l.style.left = pp.x - 40; n.l.style.top = pp.y - 20; n.l.style.scale = new Scale(Vector3.one * s); n.l.style.opacity = Mathf.Min(1, (1 - n.t) * 2.5f);
                if (n.t > 1) { n.l.style.display = DisplayStyle.None; numPool.Push(n.l); numList.RemoveAt(i); }
            }
            // overhead bars: HP (+ power for heroes); towers and heroes always, other units once hurt
            int k2 = 0;
            void Bar(Vector3 at, float hpK, float mpK, int team, bool big, bool me)
            {
                if (k2 >= barPool.Count)
                {
                    var b = Box(new Color(0, 0, 0, .7f), 4); b.style.position = Position.Absolute; Border(b, new Color(0, 0, 0, .8f), 1.5f);
                    var fi = Box(Red, 3); fi.style.height = 9; b.Add(fi); var mpb = Box(new Color(.3f, .6f, 1), 2); mpb.style.height = 4; mpb.style.marginTop = 1; b.Add(mpb); bars.Add(b); barPool.Add(b);
                }
                var bar = barPool[k2++]; var bp = ToPanel(at); float w = big ? 120 : 76;
                bar.style.display = DisplayStyle.Flex; bar.style.width = w; bar.style.left = bp.x - w / 2; bar.style.top = bp.y; bar[0].style.height = big ? 13 : 9;
                bar[0].style.width = Length.Percent(Mathf.Clamp01(hpK) * 100);
                bar[0].style.backgroundColor = me ? new Color(.3f, .9f, .4f) : team == 0 ? new Color(.35f, .7f, 1) : Red;
                bar[1].style.display = mpK >= 0 ? DisplayStyle.Flex : DisplayStyle.None; if (mpK >= 0) bar[1].style.width = Length.Percent(Mathf.Clamp01(mpK) * 100);
            }
            foreach (var f in G.foes)
            {
                if (!f || !f.alive || f.isBoss) continue; bool tower = f.d.kind == "tower", champ = f.d.kind == "champ";
                if (!tower && !champ && f.hp >= f.maxHp) continue;
                Bar(f.Pos + Vector3.up * (tower ? Lane.TopY + 1.2f : f.h + .45f), f.hp / f.maxHp, champ ? f.PowerK : -1, f.team, tower, false);
            }
            var me = G.hero; if (me && G.mode == Main.Mode.Game && !G.soul && !me.dead) Bar(me.Pos + Vector3.up * 2.35f, me.hp / me.max, me.mp / me.mpMax, 0, false, true);
            for (int i = k2; i < barPool.Count; i++) barPool[i].style.display = DisplayStyle.None;
        }
        VisualElement Tag(string t, Color c) { var b = Box(c * new Color(1, 1, 1, .35f), 10); Border(b, c); b.style.paddingLeft = b.style.paddingRight = 10; b.style.marginRight = 6; b.Add(L(t, 17, Ink)); return b; }
    }
}
