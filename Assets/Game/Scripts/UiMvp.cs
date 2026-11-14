using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // The MVP emblem of the end screen (owner, 2026-11-14: "make the MVP stand out, with wings"): two golden wings that beat slowly,
    // a violet shield with a gold rim and "MVP" on it, a crown over it, gold rays turning behind, a soft glow and twinkling sparks,
    // and a ribbon "MOST VALUABLE PLAYER" under it. All drawn (no art needed).
    public partial class Ui
    {
        static readonly Color MvpGold = new(1f, .8f, .32f), MvpGoldHi = new(1f, .95f, .7f), MvpGoldLo = new(.62f, .36f, .08f), MvpViolet = new(.3f, .1f, .45f), MvpVioletHi = new(.55f, .25f, .8f);
        VisualElement MvpEmblem(float big)
        {
            float W = 340 * big, H = 250 * big;
            var em = Box(); em.style.width = W; em.style.height = H; em.pickingMode = PickingMode.Ignore;
            // light behind: a warm glow and turning rays
            var glow = Box(); Abs(glow, -W * .1f, -H * .12f); glow.style.width = W * 1.2f; glow.style.height = H * 1.15f; glow.style.backgroundImage = new StyleBackground(SoftGlow());
            glow.style.unityBackgroundImageTintColor = new Color(1f, .7f, .25f, .55f); glow.pickingMode = PickingMode.Ignore; em.Add(glow);
            float rs = H * 1.05f; var rays = Box(); Abs(rays, (W - rs) / 2, (H - rs) / 2 - H * .06f); rays.style.width = rays.style.height = rs; rays.pickingMode = PickingMode.Ignore;
            rays.generateVisualContent += Rays; em.Add(rays);
            // the wings, from the shield's sides; each beats round its root
            float ww = W * .5f, wh = H * .7f, wy = H * .1f;
            var wl = Box(); Abs(wl, W * .5f - ww * .96f - W * .04f, wy); wl.style.width = ww; wl.style.height = wh; wl.pickingMode = PickingMode.Ignore; wl.generateVisualContent += Wing;
            wl.style.transformOrigin = new TransformOrigin(Length.Percent(96), Length.Percent(62)); em.Add(wl);
            // the right wing: a holder that beats round the root (now at its left), with the drawn wing inside flipped about its own middle
            var wr = Box(); Abs(wr, W * .5f - ww * .04f + W * .04f, wy); wr.style.width = ww; wr.style.height = wh; wr.pickingMode = PickingMode.Ignore;
            wr.style.transformOrigin = new TransformOrigin(Length.Percent(4), Length.Percent(62)); em.Add(wr);
            var wrIn = Box(); Abs(wrIn, 0, 0, 0, 0); wrIn.pickingMode = PickingMode.Ignore; wrIn.generateVisualContent += Wing; wrIn.style.scale = new Scale(new Vector3(-1, 1, 1)); wr.Add(wrIn);
            // the shield with MVP, the crown over it
            float sw = W * .36f, sh = H * .5f;
            var sd = Box(); Abs(sd, (W - sw) / 2, H * .22f); sd.style.width = sw; sd.style.height = sh; sd.pickingMode = PickingMode.Ignore; sd.generateVisualContent += Shield;
            sd.style.alignItems = Align.Center; sd.style.paddingTop = sh * .2f; em.Add(sd);
            var t = L("MVP", 44 * big, MvpGoldHi, true); t.style.unityTextOutlineWidth = .25f; t.style.unityTextOutlineColor = new Color(.35f, .15f, .02f); t.style.letterSpacing = 2;
            t.style.textShadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 8, color = new Color(0, 0, 0, .8f) }; sd.Add(t);
            float cs = W * .24f; var crown = Box(); Abs(crown, (W - cs) / 2, H * .02f); crown.style.width = cs; crown.style.height = cs * .8f; crown.pickingMode = PickingMode.Ignore;
            crown.generateVisualContent += ctx => { Crown(ctx, MvpGoldLo); }; em.Add(crown);
            var crownHi = Box(); Abs(crownHi, (W - cs) / 2 + 2, H * .02f - 3); crownHi.style.width = cs - 4; crownHi.style.height = cs * .8f - 3; crownHi.pickingMode = PickingMode.Ignore;
            crownHi.generateVisualContent += ctx => Crown(ctx, MvpGold); em.Add(crownHi);
            // the ribbon under it
            var rib = Box(); Abs(rib, W * .14f, H * .74f); rib.style.width = W * .72f; rib.style.height = H * .16f; rib.pickingMode = PickingMode.Ignore; rib.generateVisualContent += Ribbon;
            rib.style.alignItems = Align.Center; rib.style.justifyContent = Justify.Center; em.Add(rib);
            var rt = L(Loc.S("ผู้เล่นยอดเยี่ยม", "MOST VALUABLE PLAYER"), 15 * big, MvpGoldHi, true); rt.style.letterSpacing = 1; rt.style.marginTop = -2; Shadow(rt, 3); rib.Add(rt);
            // sparks
            var sparks = new VisualElement[6];
            var spots = new[] { (.12f, .1f), (.86f, .12f), (.04f, .62f), (.95f, .58f), (.3f, .02f), (.7f, .04f) };
            for (int i = 0; i < sparks.Length; i++)
            {
                float ss = (i < 4 ? 22 : 15) * big; var sp = Box(); Abs(sp, W * spots[i].Item1 - ss / 2, H * spots[i].Item2); sp.style.width = sp.style.height = ss; sp.pickingMode = PickingMode.Ignore;
                sp.generateVisualContent += Spark; em.Add(sp); sparks[i] = sp;
            }
            // the motion: rays turn, wings beat, the glow breathes, sparks twinkle
            float t0 = Time.unscaledTime;
            em.schedule.Execute(() =>
            {
                float tt = Time.unscaledTime - t0;
                rays.style.rotate = new Rotate(tt * 14);
                float flap = Mathf.Sin(tt * 2.2f) * 5;
                wl.style.rotate = new Rotate(flap); wr.style.rotate = new Rotate(-flap);
                glow.style.opacity = .75f + .25f * Mathf.Sin(tt * 1.7f);
                for (int i = 0; i < sparks.Length; i++) { float k = Mathf.Sin(tt * 2.6f + i * 1.9f); sparks[i].style.opacity = Mathf.Clamp01(k); sparks[i].style.scale = new Scale(Vector3.one * (.6f + .5f * Mathf.Clamp01(k))); }
            }).Every(33);
            return em;
        }
        static void Rays(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; var c = r.center; float R = r.width * .5f;
            for (int i = 0; i < 16; i++)
            {
                float a = i / 16f * Mathf.PI * 2, d = Mathf.PI / 16 * .45f; bool longR = i % 2 == 0; float len = R * (longR ? 1 : .8f);
                p.fillColor = new Color(1f, .82f, .4f, longR ? .3f : .18f);
                p.BeginPath(); p.MoveTo(c); p.LineTo(c + new Vector2(Mathf.Cos(a - d), Mathf.Sin(a - d)) * len); p.LineTo(c + new Vector2(Mathf.Cos(a + d), Mathf.Sin(a + d)) * len); p.ClosePath(); p.Fill();
            }
        }
        // a left wing (the right one is the same, mirrored): the root at the inner side, feathers fanning out and down
        static void Wing(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height;
            Vector2 root = new(w * .96f, h * .62f), elbow = new(w * .5f, h * .18f);
            const int N = 8;
            void Feather(Vector2 b, float ang, float len, float wid, Color fill, Color edge, bool line)
            {
                var d = new Vector2(Mathf.Cos(ang), -Mathf.Sin(ang)); var n = new Vector2(-d.y, d.x); var tip = b + d * len; var m = b + d * len * .45f;
                p.fillColor = fill; p.BeginPath(); p.MoveTo(b); p.QuadraticCurveTo(m + n * wid, tip); p.QuadraticCurveTo(m - n * wid * .7f, b); p.ClosePath(); p.Fill();
                p.strokeColor = edge; p.lineWidth = 1.2f; p.Stroke();
                if (line) { p.strokeColor = new Color(1f, .97f, .8f, .7f); p.lineWidth = 1.4f; p.BeginPath(); p.MoveTo(b + d * len * .1f); p.LineTo(b + d * len * .8f); p.Stroke(); }
            }
            // the long flight feathers behind, darker
            for (int i = 0; i < N; i++)
            {
                float k = i / (float)(N - 1); var b = Vector2.Lerp(elbow, root, k * .8f);
                float ang = Mathf.Lerp(150, 222, k) * Mathf.Deg2Rad, len = Mathf.Lerp(w * .62f, w * .4f, k);
                Feather(b, ang, len, w * .11f, MvpGoldLo, new Color(.35f, .18f, .02f, .9f), false);
            }
            // the gold feathers over them, a little shorter
            for (int i = 0; i < N; i++)
            {
                float k = (i + .5f) / N; var b = Vector2.Lerp(elbow, root, k * .85f);
                float ang = Mathf.Lerp(146, 218, k) * Mathf.Deg2Rad, len = Mathf.Lerp(w * .5f, w * .3f, k);
                Feather(b, ang, len, w * .09f, Color.Lerp(MvpGold, MvpGoldHi, .25f * (1 - k)), new Color(.55f, .3f, .05f, .9f), true);
            }
            // the wing's arm: a bright curved band from the root to the elbow, then small covert feathers along it
            p.fillColor = MvpGold; p.strokeColor = MvpGoldLo; p.lineWidth = 1.5f;
            p.BeginPath(); p.MoveTo(root + new Vector2(0, h * .06f)); p.QuadraticCurveTo(new Vector2(w * .7f, h * .22f), elbow + new Vector2(-w * .04f, h * .02f));
            p.QuadraticCurveTo(new Vector2(w * .72f, h * .08f), root + new Vector2(0, -h * .1f)); p.ClosePath(); p.Fill(); p.Stroke();
            for (int i = 0; i < 4; i++)
            {
                float k = (i + .5f) / 4; var b = Vector2.Lerp(elbow, root, k) + new Vector2(0, h * .03f);
                Feather(b, Mathf.Lerp(200, 240, k) * Mathf.Deg2Rad, w * .16f, w * .05f, MvpGoldHi, new Color(.6f, .35f, .05f, .8f), false);
            }
        }
        static void Shield(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y);
            void Path(float inset)
            {
                p.BeginPath(); p.MoveTo(P(.5f, inset * .7f)); p.LineTo(P(1 - inset, .12f + inset * .5f)); p.LineTo(P(1 - inset, .5f));
                p.QuadraticCurveTo(P(1 - inset, .82f - inset * .3f), P(.5f, 1 - inset)); p.QuadraticCurveTo(P(inset, .82f - inset * .3f), P(inset, .5f)); p.LineTo(P(inset, .12f + inset * .5f)); p.ClosePath();
            }
            p.fillColor = MvpGoldLo; Path(0); p.Fill();
            p.fillColor = MvpGold; Path(.04f); p.Fill();
            p.fillColor = MvpViolet; Path(.1f); p.Fill();
            p.fillColor = new Color(MvpVioletHi.r, MvpVioletHi.g, MvpVioletHi.b, .6f);   // a lighter top half
            p.BeginPath(); p.MoveTo(P(.5f, .09f)); p.LineTo(P(.9f, .19f)); p.LineTo(P(.9f, .5f)); p.LineTo(P(.1f, .5f)); p.LineTo(P(.1f, .19f)); p.ClosePath(); p.Fill();
            p.strokeColor = MvpGoldHi; p.lineWidth = 1.5f; Path(.1f); p.Stroke();
            p.fillColor = new Color(.7f, .95f, 1f); p.BeginPath(); p.MoveTo(P(.5f, .74f)); p.LineTo(P(.56f, .82f)); p.LineTo(P(.5f, .9f)); p.LineTo(P(.44f, .82f)); p.ClosePath(); p.Fill();   // a gem at the point
        }
        static void Ribbon(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y);
            p.fillColor = new Color(.45f, .12f, .14f);   // the folded tails
            foreach (int s in new[] { 0, 1 })
            {
                float x0 = s == 0 ? 0 : 1, x1 = s == 0 ? .12f : .88f;
                p.BeginPath(); p.MoveTo(P(x0, .25f)); p.LineTo(P(x1, .25f)); p.LineTo(P(x1, 1)); p.LineTo(P(x0, 1)); p.LineTo(P(s == 0 ? .05f : .95f, .62f)); p.ClosePath(); p.Fill();
            }
            p.fillColor = new Color(.7f, .16f, .18f); p.strokeColor = MvpGold; p.lineWidth = 1.5f;
            p.BeginPath(); p.MoveTo(P(.08f, .05f)); p.LineTo(P(.92f, .05f)); p.LineTo(P(.92f, .85f)); p.LineTo(P(.08f, .85f)); p.ClosePath(); p.Fill(); p.Stroke();
        }
        static void Spark(MeshGenerationContext ctx)
        {
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; var c = r.center; float R = r.width * .5f, q = R * .22f;
            p.fillColor = MvpGoldHi;
            p.BeginPath(); p.MoveTo(c + new Vector2(0, -R)); p.LineTo(c + new Vector2(q, -q)); p.LineTo(c + new Vector2(R, 0)); p.LineTo(c + new Vector2(q, q)); p.LineTo(c + new Vector2(0, R)); p.LineTo(c + new Vector2(-q, q)); p.LineTo(c + new Vector2(-R, 0)); p.LineTo(c + new Vector2(-q, -q)); p.ClosePath(); p.Fill();
        }
    }
}
