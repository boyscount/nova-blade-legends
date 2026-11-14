using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // Getting from one screen to the next (premium plan, step 2, 2026-11-14): the opening splash (the Nova Blade logo rising out of the
    // dark, tap to skip), and a curtain over every change of scene: black, then it lifts; going into a fight it first shows a title
    // card (the place, the mode, a tip) for a moment.
    public partial class Ui
    {
        VisualElement flowOv, flowCard; Label flowTitle, flowSub, flowTip; float flowT = -1, flowHold, flowFade = .45f;
        VisualElement splashOv, splashLogo; float splashT = -1;
        static readonly (string th, string en)[] Tips =
        {
            ("กดหลบ (R / Space) ตอนศัตรูง้างท่า แล้วสวนกลับ", "Dodge (R / Space) as an enemy winds up, then strike back"),
            ("คอมโบที่ 3 ตีแรงที่สุด และทำให้ศัตรูเสียหลัก", "The third blow of a combo hits hardest and staggers"),
            ("สกิลที่ 3 คือท่าไม้ตาย เก็บไว้ตอนศัตรูรวมกลุ่ม", "Your third skill is the ultimate: save it for a crowd"),
            ("ในโหมด MOBA ฆ่ามอนป่าเพื่อรับบัฟ ล้มผู้พิทักษ์เพื่อพรทั้งทีม", "In MOBA, clear jungle camps for buffs; fell the Guardian to bless your side"),
            ("กด B กลับบ้านเพื่อฟื้นเลือด กด T วาปไปป้อมที่ถูกโจมตี", "Press B to recall and heal, T to warp to a tower under attack"),
            ("เลเวล 3, 6, 9 เลือกพลังเสริม เลือกให้เข้ากับฮีโร่", "At levels 3, 6 and 9 pick a power that suits your hero"),
            ("ทหารช่วยกันป้อม อย่าบุกป้อมคนเดียว", "Soldiers soak tower shots: never push a tower alone"),
            ("จบตำนานระดับยากเพื่อรับสกินตำนาน", "Finish a legend on Legend difficulty for its legend skin"),
        };

        // ---------- the curtain ----------
        void BuildFlow()
        {
            flowOv = Box(new Color(.01f, .01f, .03f, 1)); Abs(flowOv, 0, 0, 0, 0); flowOv.pickingMode = PickingMode.Ignore; flowOv.style.alignItems = Align.Center; flowOv.style.justifyContent = Justify.Center; root.Add(flowOv);
            flowCard = Box(); flowCard.style.alignItems = Align.Center; flowOv.Add(flowCard);
            flowSub = L("", touchUi ? 22 : 20, new Color(1f, .8f, .45f), true); flowSub.style.letterSpacing = 3; flowCard.Add(flowSub);
            flowTitle = L("", 64, new Color(1f, .94f, .8f), true); flowTitle.style.marginTop = 6; Shadow(flowTitle, 10); flowCard.Add(flowTitle);
            var line = Box(); line.style.width = 360; line.style.height = 2; line.style.marginTop = 14; line.style.backgroundImage = new StyleBackground(Grad(new Color(1f, .75f, .3f, 0), new Color(1f, .8f, .4f, 1), false)); flowCard.Add(line);
            flowTip = L("", touchUi ? 22 : 19, new Color(.85f, .85f, .95f)); flowTip.style.marginTop = 18; flowTip.style.maxWidth = 760; flowTip.style.whiteSpace = WhiteSpace.Normal; flowTip.style.unityTextAlign = TextAnchor.MiddleCenter; flowCard.Add(flowTip);
            flowOv.style.display = DisplayStyle.None;
        }
        // black now, lifting after a moment; with a title it is a title card (and a tip) first
        public void Curtain(string title = null, string sub = null, bool tip = false)
        {
            if (flowOv == null) BuildFlow();
            bool card = !string.IsNullOrEmpty(title);
            flowTitle.text = title ?? ""; flowSub.text = sub ?? ""; flowTip.text = "";
            if (card && tip) { var t = Tips[Random.Range(0, Tips.Length)]; flowTip.text = Loc.S("เคล็ดลับ: " + t.th, "Tip: " + t.en); }
            flowCard.style.display = card ? DisplayStyle.Flex : DisplayStyle.None;
            flowHold = card ? 1.15f : .08f; flowFade = card ? .55f : .4f; flowT = 0;
            flowOv.style.display = DisplayStyle.Flex; flowOv.style.opacity = 1; flowOv.BringToFront(); if (splashOv != null && splashT >= 0) splashOv.BringToFront();
        }
        void UpdFlow(float dt)
        {
            if (flowT >= 0 && flowOv != null)
            {
                flowT += dt; float k = Mathf.Clamp01((flowT - flowHold) / flowFade);
                flowOv.style.opacity = 1 - k * k * (3 - 2 * k);
                flowCard.style.translate = new Translate(0, -k * 18, 0);
                if (k >= 1) { flowT = -1; flowOv.style.display = DisplayStyle.None; }
            }
            if (splashT >= 0 && splashOv != null)
            {
                splashT += dt; float a = Mathf.Clamp01(splashT / 1f), outK = Mathf.Clamp01((splashT - 2.6f) / .7f);
                splashLogo.style.opacity = a * a * (3 - 2 * a); splashLogo.style.scale = new Scale(Vector3.one * (1.12f - .12f * (1 - (1 - a) * (1 - a))));
                splashOv.style.opacity = 1 - outK;
                if (outK >= 1) { splashT = -1; splashOv.style.display = DisplayStyle.None; }
            }
        }
        public bool CurtainUp => flowT >= 0;

        // ---------- the opening splash ----------
        public void Splash()
        {
            splashOv = Box(new Color(.01f, .01f, .03f, 1)); Abs(splashOv, 0, 0, 0, 0); splashOv.pickingMode = PickingMode.Position; splashOv.style.alignItems = Align.Center; splashOv.style.justifyContent = Justify.Center; root.Add(splashOv);
            var glow = Box(); Abs(glow, 0, 0, 0, 0); glow.style.backgroundImage = new StyleBackground(SoftGlow()); glow.style.unityBackgroundImageTintColor = new Color(.55f, .3f, .95f, .35f); glow.pickingMode = PickingMode.Ignore; splashOv.Add(glow);
            splashLogo = Box(); splashLogo.style.alignItems = Align.Center; splashOv.Add(splashLogo);
            var nova = new Label("NOVA BLADE"); nova.style.unityFontDefinition = fontTitleEn; nova.style.fontSize = touchUi ? 92 : 104; nova.style.color = new Color(1f, .86f, .5f); nova.style.letterSpacing = 6;
            nova.style.unityTextOutlineWidth = .12f; nova.style.unityTextOutlineColor = new Color(.35f, .15f, .02f); nova.style.textShadow = new TextShadow { offset = new Vector2(0, 4), blurRadius = 18, color = new Color(1f, .55f, .15f, .7f) }; nova.pickingMode = PickingMode.Ignore; splashLogo.Add(nova);
            var leg = new Label("L E G E N D S"); leg.style.unityFontDefinition = fontTitleEn; leg.style.fontSize = touchUi ? 34 : 38; leg.style.color = new Color(.85f, .78f, 1f); leg.style.marginTop = -4; leg.pickingMode = PickingMode.Ignore; splashLogo.Add(leg);
            var line = Box(); line.style.width = 520; line.style.height = 2; line.style.marginTop = 16; line.style.backgroundImage = new StyleBackground(Grad(new Color(1f, .75f, .3f, 0), new Color(1f, .8f, .4f, 1), false)); splashLogo.Add(line);
            var tap = L(Loc.S("แตะเพื่อเริ่ม", "Tap to begin"), 18, new Color(1, 1, 1, .55f)); tap.style.marginTop = 30; splashLogo.Add(tap);
            splashOv.RegisterCallback<PointerUpEvent>(_ => { if (splashT >= 0 && splashT < 2.6f) splashT = 2.6f; });   // tap: go straight to the fade out
            splashT = 0; splashLogo.style.opacity = 0;
        }
        public bool SplashOn => splashT >= 0;
    }
}
