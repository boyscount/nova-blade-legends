using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // The VS screen's fire: "VS" drawn as burning letters (white-hot at the top, orange, deep red below, a dark scorched edge,
    // flames licking up from them), embers rising over the whole screen, and the posed heroes facing off.
    public partial class Ui
    {
        VisualElement fireVs, embersEl; readonly List<Vector4> embers = new();   // x, y (0..1), speed, size
        readonly List<Texture2D> vsPoses = new();

        VisualElement FireVs(bool phone)
        {
            var box = Box(); box.style.position = Position.Absolute; box.style.left = 0; box.style.right = 0; box.style.top = Length.Percent(29); box.style.height = Length.Percent(34); box.style.alignItems = Align.Center; box.style.justifyContent = Justify.Center;
            var glow = Box(); glow.style.position = Position.Absolute; glow.style.width = phone ? 320 : 420; glow.style.height = phone ? 230 : 300; glow.style.backgroundImage = new StyleBackground(SoftGlow()); glow.style.unityBackgroundImageTintColor = new Color(1, .45f, .08f, .95f); glow.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; box.Add(glow);
            fireVs = Box(); fireVs.style.width = phone ? 230 : 300; fireVs.style.height = phone ? 150 : 195; fireVs.style.rotate = new Rotate(-7); box.Add(fireVs);
            var rnd = new System.Random(9); var tongues = new List<(float x, float h, float w, float ph)>();
            for (int i = 0; i < 16; i++) tongues.Add(((float)rnd.NextDouble(), .25f + (float)rnd.NextDouble() * .35f, .045f + (float)rnd.NextDouble() * .05f, (float)rnd.NextDouble() * 6));
            fireVs.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height * .78f, top = r.height * .22f, tm = Time.unscaledTime;
                Vector2 P(float x, float y) => new(x * w, top + y * h);
                // the letters' shapes (V filled, S as a thick stroke)
                void V() { p.BeginPath(); p.MoveTo(P(0, 0)); p.LineTo(P(.17f, 0)); p.LineTo(P(.245f, .6f)); p.LineTo(P(.32f, 0)); p.LineTo(P(.49f, 0)); p.LineTo(P(.33f, 1)); p.LineTo(P(.16f, 1)); p.ClosePath(); }
                void S() { p.BeginPath(); p.MoveTo(P(.97f, .14f)); p.BezierCurveTo(P(.9f, -.02f), P(.6f, -.05f), P(.58f, .2f)); p.BezierCurveTo(P(.56f, .42f), P(.95f, .42f), P(.95f, .7f)); p.BezierCurveTo(P(.95f, .98f), P(.66f, 1.04f), P(.56f, .86f)); }
                var fire = FillGradient.MakeLinearGradient(new Color(1, .98f, .75f), new Color(.85f, .12f, .02f), P(.5f, 0), P(.5f, 1), AddressMode.Clamp);
                // flames rising from the letters' tops (behind the letters)
                foreach (var (x, fh, fw, ph) in tongues)
                {
                    float f = fh * (.8f + .25f * Mathf.Sin(tm * 9 + ph) + .1f * Mathf.Sin(tm * 23 + ph * 2)); float sway = .02f * Mathf.Sin(tm * 6 + ph);
                    float baseY = x < .5f ? 0.02f : .05f; var b0 = P(x - fw, baseY + .08f); var b1 = P(x + fw, baseY + .08f); var tip = P(x + sway, baseY - f);
                    p.fillGradient = FillGradient.MakeLinearGradient(new Color(1, .9f, .4f, .95f), new Color(1, .25f, 0, 0), b0, tip, AddressMode.Clamp);
                    p.BeginPath(); p.MoveTo(b0); p.BezierCurveTo(P(x - fw * 1.3f, baseY - f * .3f), P(x + sway - fw * .3f, baseY - f * .7f), tip); p.BezierCurveTo(P(x + sway + fw * .3f, baseY - f * .7f), P(x + fw * 1.3f, baseY - f * .3f), b1); p.ClosePath(); p.Fill();
                }
                // scorched outline, then the fire inside, then a bright inner edge
                p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
                p.strokeColor = new Color(.25f, .02f, 0, .95f); p.lineWidth = w * .045f; V(); p.Stroke(); p.lineWidth = w * .19f; S(); p.Stroke();
                p.fillGradient = fire; V(); p.Fill(); p.strokeFillGradient = fire; p.lineWidth = w * .135f; S(); p.Stroke();
                p.strokeColor = new Color(1, 1, .85f, .55f); p.lineWidth = w * .012f; V(); p.Stroke();
            };
            return box;
        }
        VisualElement Embers()
        {
            embers.Clear(); var rnd = new System.Random(4);
            for (int i = 0; i < 70; i++) embers.Add(new Vector4((float)rnd.NextDouble(), (float)rnd.NextDouble(), .05f + (float)rnd.NextDouble() * .12f, 1 + (float)rnd.NextDouble() * 2.6f));
            embersEl = Box(); embersEl.style.position = Position.Absolute; embersEl.style.left = embersEl.style.right = embersEl.style.top = embersEl.style.bottom = 0;
            embersEl.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D;
                foreach (var e in embers) { float a = Mathf.Clamp01(e.y * 1.4f); p.fillColor = new Color(1, .55f + .3f * (e.w / 3.6f), .15f, a * .9f); p.BeginPath(); p.Arc(new Vector2(e.x * r.width, e.y * r.height), e.w, 0, 360); p.Fill(); }
            };
            return embersEl;
        }
        void UpdFire(float dt)
        {
            fireVs?.MarkDirtyRepaint();
            if (embersEl == null) return;
            for (int i = 0; i < embers.Count; i++) { var e = embers[i]; e.y -= e.z * dt; e.x += Mathf.Sin(Time.unscaledTime * 2 + i) * .004f * dt * 10; if (e.y < -.02f) { e.y = 1.02f; e.x = Random.value; } embers[i] = e; }
            embersEl.MarkDirtyRepaint();
        }
        // the six heroes rendered in a fighting stance, ours turned to face right, theirs to face left
        const float VsYaw = -0.01f;   // facing the camera (the owner preferred it to the sides turned toward each other); the sign still tells the sides apart for the lighting
        void MakePoses(List<HeroDef> us, List<HeroDef> them)
        {
            foreach (var t in vsPoses) if (t) Destroy(t); vsPoses.Clear();
            var who = new List<(HeroDef, float, string)>(); for (int i = 0; i < us.Count; i++) who.Add((us[i], VsYaw, i == 0 ? G.ModelKey(us[i]) : us[i].id)); foreach (var h in them) who.Add((h, -VsYaw, h.id));   // the player in their skin
            vsPoses.AddRange(Studio.VsPoses(who)); Debug.Log($"[Vs] hero pictures {vsPoses.Count(t => t)} of {who.Count}");
        }
        Texture2D PoseOf(int t, int i, int ourCount) { int k = t == 0 ? i : ourCount + i; return k < vsPoses.Count ? vsPoses[k] : null; }
    }
}
