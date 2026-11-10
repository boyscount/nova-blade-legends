using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // The VS screen in daylight (bright fantasy, owner's reference 2026-11-10): a blue-violet sky with sunlit clouds, a floating island in the
    // middle with a castle and waterfalls, rock pillars floating at the sides; a bright stone arena both teams share, a gold rune circle,
    // a blue banner for our side and a red one for theirs, blossom in the corners. The heroes are lit brightly (Studio.VsPoses); the menace
    // stays in the ice wolf and the fire skull.
    public partial class Ui
    {
        static Texture2D vsDay; const bool DrawIsland = false;
        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
        // the sky picture, painted once per session (or loaded from Resources/UI/Vs/vs_day.png when that has been saved)
        public static Texture2D VsDay()
        {
            if (vsDay) return vsDay; var saved = Resources.Load<Texture2D>("UI/Vs/vs_day"); if (saved) return vsDay = saved;
            const int W = 960, H = 540; const float aspect = W / (float)H; vsDay = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H]; var sun = new Vector2(.5f * aspect, .1f);
            // the floating island in the middle: its top edge, its underside tapering to a point, the castle on it
            float IslandTop(float u) => .40f + .012f * Mathf.Sin(u * 60) - .03f * Smooth(.12f, 0, Mathf.Abs(u - .5f));
            float IslandHalf = .13f;
            float IslandBottom(float u) { float d = Mathf.Abs(u - .5f) / IslandHalf; return .41f + .27f * Mathf.Pow(Mathf.Clamp01(1 - d), 1.4f) + .03f * (Fbm(u * 14, 3, 3) - .5f); }
            bool Castle(float u, float v, out float shade, out bool roof, out bool window)
            {
                shade = 0; roof = false; window = false; float top = IslandTop(u);
                if (Mathf.Abs(u - .5f) < .085f && v < top && v > top - .07f) { shade = .5f + .5f * Mathf.Sin(u * 40); window = Mathf.Repeat(u * 120, 1) < .3f && Mathf.Repeat(v * 90, 1) < .35f && v < top - .02f; if (Mathf.Abs(Mathf.Repeat(u * 160, 2) - 1) < .5f && v < top - .066f) return false; return true; }   // the wall and its battlements
                foreach (var (tx, tw, th) in new[] { (.5f, .014f, .27f), (.465f, .011f, .19f), (.535f, .011f, .2f), (.435f, .009f, .14f), (.565f, .009f, .15f), (.41f, .008f, .1f), (.59f, .008f, .11f), (.485f, .007f, .23f), (.517f, .007f, .22f) })
                {
                    float roofH = tw * 3.2f; float body = top - th + roofH;
                    if (Mathf.Abs(u - tx) < tw && v > body && v < top) { shade = (u - tx) / tw; window = Mathf.Abs(u - tx) < tw * .3f && Mathf.Repeat(v * 70, 1) < .3f && v < top - .03f; return true; }
                    float k = (v - (top - th)) / roofH; if (k > 0 && k < 1 && Mathf.Abs(u - tx) < tw * 1.15f * k) { shade = (u - tx) / (tw * 1.15f * k); roof = true; return true; }   // conical roofs
                }
                return false;
            }
            // floating rock pillars at the sides, with a little green on top
            (float l, float r, float t, float b)[] pillars = { (.03f, .14f, .30f, .78f), (.16f, .23f, .44f, .72f), (.26f, .31f, .52f, .74f), (.86f, .97f, .3f, .78f), (.77f, .84f, .44f, .72f), (.69f, .74f, .52f, .74f), (.62f, .645f, .26f, .33f) };
            for (int y = 0; y < H; y++)
            {
                float v = (y + .5f) / H, vv = 1 - v;   // v: 0 at the top
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W; var q = new Vector2(u * aspect, v);
                    // sky: deep azure with violet up top, paling to a soft white-blue at the horizon; a cool cast on our side, a warm one on theirs
                    var c = Mix(new Color(.22f, .32f, .78f), new Color(.42f, .62f, .97f), Smooth(0, .45f, v));
                    c = Mix(c, new Color(.82f, .88f, 1f), Smooth(.4f, .75f, v));
                    c = Mix(c, new Color(.45f, .3f, .8f), Smooth(.25f, 0, v) * (Smooth(.35f, 0, u) + Smooth(.65f, 1, u)) * .5f);
                    c += new Color(-.05f, .02f, .08f) * Smooth(.4f, 0, u) + new Color(.12f, -.02f, -.06f) * Smooth(.6f, 1, u);
                    // the sun behind the clouds, high and centred
                    float ds = Vector2.Distance(q, sun); c += new Color(1f, .95f, .8f) * (Mathf.Pow(Mathf.Clamp01(1 - ds / .7f), 3) * .45f + Mathf.Pow(Mathf.Clamp01(1 - ds / .18f), 2) * .5f);
                    // cumulus: heaps of cloud, white where the sun lights them, blue-grey underneath
                    float cl = Fbm(u * 4 + 3, v * 7 + 1) * .65f + Fbm(u * 11 + 7, v * 16 + 2) * .35f;
                    float band = Smooth(.1f, .3f, v) * Smooth(.78f, .5f, v) + Smooth(.08f, 0, v) * .4f;
                    float cov = Smooth(.6f, .72f, cl + band * .3f - .1f);
                    float under = Smooth(.45f, .62f, Fbm(u * 4 + 3, (v + .03f) * 7 + 1) * .65f + Fbm(u * 11 + 7, (v + .03f) * 16 + 2) * .35f + band * .35f - .1f);
                    var cloud = Mix(new Color(1f, .99f, .96f), new Color(.66f, .72f, .9f), Mathf.Clamp01(under - cov + .35f));
                    c = Mix(c, cloud, cov * .92f);
                    // distant mountains and floating isles in haze
                    float far = .58f + .05f * Fbm(u * 6 + 2, 9, 3) - .06f * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * 9 + 1)), 4);
                    if (v > far) c = Mix(c, new Color(.55f, .68f, .9f), .55f + .2f * Smooth(far, far + .1f, v));
                    // a sea of cloud below the islands
                    float sea = .7f + .03f * Fbm(u * 8, 4, 3);
                    if (v > sea) c = Mix(c, Mix(new Color(.97f, .97f, 1f), new Color(.75f, .8f, .95f), Fbm(u * 12, v * 20, 3)), .9f);
                    // rock pillars
                    foreach (var (l, r, t, b) in pillars)
                    {
                        if (u < l - .01f || u > r + .01f || v < t - .02f) continue; float mid = (l + r) / 2, half = (r - l) / 2;
                        float edge = half * (1 - .5f * Smooth(t + (b - t) * .5f, b, v)) * (1 + .12f * (Fbm(v * 20 + l * 9, 1, 3) - .5f));
                        float top = t + .015f * Mathf.Sin(u * 80 + l * 7);
                        if (Mathf.Abs(u - mid) < edge && v > top && v < b)
                        {
                            float lit = .5f + .5f * (mid - u) / edge;   // the sun-side face brighter
                            var rock = Mix(new Color(.36f, .38f, .5f), new Color(.78f, .76f, .78f), lit * .8f + .1f) * (.85f + .3f * Fbm(u * 60, v * 60, 2));
                            if (v < top + .02f) rock = Mix(rock, new Color(.38f, .62f, .32f), .8f);   // grass
                            float haze = Mathf.Clamp01((.25f - Mathf.Min(u, 1 - u)) * 2) * 0 + (half < .03f ? .35f : .1f);
                            c = Mix(rock, new Color(.7f, .8f, .97f), haze);
                        }
                    }
                    // the floating island and its castle
                    float itop = IslandTop(u), ibot = IslandBottom(u);
                    if (DrawIsland && Mathf.Abs(u - .5f) < IslandHalf && v > itop && v < ibot)
                    {
                        float lit = .55f + .45f * (.5f - u) / IslandHalf; float depth = Smooth(itop, ibot, v);
                        var rock = Mix(new Color(.8f, .74f, .66f), new Color(.38f, .36f, .46f), depth * .8f + (1 - lit) * .35f) * (.86f + .28f * Fbm(u * 50, v * 50, 3));
                        if (v < itop + .025f) rock = Mix(new Color(.34f, .62f, .3f), new Color(.5f, .78f, .38f), Fbm(u * 40, v * 40, 2));   // grass on top
                        c = rock;
                    }
                    if (DrawIsland && Castle(u, v, out float sh, out bool roof, out bool window))
                    {
                        var wall = Mix(new Color(.96f, .93f, .88f), new Color(.66f, .66f, .78f), (sh + 1) * .5f);
                        c = roof ? Mix(new Color(.35f, .55f, .95f), new Color(.18f, .26f, .6f), (sh + 1) * .5f) : wall;
                        if (window) c = new Color(1f, .85f, .5f);
                        c = Mix(c, new Color(.8f, .87f, 1f), .12f);   // a little air between us and it
                    }
                    // waterfalls off the island's edge, foaming white, mist where they vanish into the cloud sea
                    foreach (var (wx, ww) in new[] { (.458f, .008f), (.538f, .011f), (.505f, .006f) })
                    {
                        if (!DrawIsland) break; float dx = Mathf.Abs(u - wx); float start = IslandBottom(wx) - .12f; if (dx > ww * 2.4f || v < start) continue;
                        float fall = Smooth(ww * 1.2f, ww * .4f, dx) * Smooth(start, start + .02f, v);
                        float streak = .75f + .25f * Mathf.PerlinNoise(u * 900, v * 18);
                        c = Mix(c, new Color(.92f, .97f, 1f) * streak, fall * .95f);
                        c += new Color(.9f, .95f, 1f) * Smooth(ww * 2.4f, 0, dx) * Smooth(sea - .06f, sea + .02f, v) * .45f;   // mist
                    }
                    c.a = 1; px[y * W + x] = c;
                }
            }
            vsDay.SetPixels(px); vsDay.Apply(); return vsDay;
        }

        // the arena both teams stand on: a wide disc of pale stone in perspective, rings and seams, a gold rune circle at the centre, light reflected off it
        VisualElement ArenaFloor()
        {
            var a = Box(); a.style.position = Position.Absolute; a.style.left = a.style.right = a.style.top = a.style.bottom = 0; a.pickingMode = PickingMode.Ignore;
            a.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float W = r.width, H = r.height;
                Vector2 E(float cx, float cy, float rx, float ry, float ang) => new(W * (cx + rx * Mathf.Cos(ang)), H * (cy + ry * Mathf.Sin(ang)));
                void Ellipse(float cx, float cy, float rx, float ry) { p.BeginPath(); for (int i = 0; i <= 64; i++) { var e = E(cx, cy, rx, ry, i / 64f * Mathf.PI * 2); if (i == 0) p.MoveTo(e); else p.LineTo(e); } p.ClosePath(); }
                const float cx = .5f, cy = .9f;
                // the stone: pale grey, a lighter middle, a cool tint on our side and a warm one on theirs
                p.fillColor = new Color(.62f, .65f, .74f); Ellipse(cx, cy, .78f, .24f); p.Fill();
                p.fillColor = new Color(.8f, .82f, .88f, .6f); Ellipse(cx, cy - .01f, .55f, .16f); p.Fill();
                p.fillColor = new Color(.95f, .94f, .92f, .45f); Ellipse(cx, cy - .015f, .32f, .09f); p.Fill();
                p.fillColor = new Color(.45f, .7f, 1f, .18f); Ellipse(cx - .36f, cy, .38f, .2f); p.Fill();
                p.fillColor = new Color(1f, .5f, .3f, .16f); Ellipse(cx + .36f, cy, .38f, .2f); p.Fill();
                // rings and radial seams of the paving
                p.lineCap = LineCap.Round;
                foreach (var k in new[] { .28f, .46f, .64f })
                {
                    p.strokeColor = new Color(.32f, .34f, .44f, .5f); p.lineWidth = 2; Ellipse(cx, cy, .78f * k + .06f, .24f * k + .02f); p.Stroke();
                    p.strokeColor = new Color(1, 1, 1, .25f); p.lineWidth = 1; Ellipse(cx, cy - .004f, .78f * k + .06f, .24f * k + .02f); p.Stroke();
                }
                p.strokeColor = new Color(.32f, .34f, .44f, .45f); p.lineWidth = 1.5f;
                for (int i = 0; i < 24; i++) { float ang = Mathf.PI + i / 24f * Mathf.PI; p.BeginPath(); p.MoveTo(E(cx, cy, .28f * .78f + .06f, .28f * .24f + .02f, ang)); p.LineTo(E(cx, cy, .84f, .26f, ang)); p.Stroke(); }
                // the rune circle: a soft gold glow, two gold rings, little glyph ticks between them
                p.fillColor = new Color(1f, .85f, .45f, .3f); Ellipse(cx, cy - .01f, .22f, .065f); p.Fill();
                foreach (var (k, wdt, al) in new[] { (1f, 3f, .9f), (.82f, 2f, .8f), (.45f, 2f, .7f) }) { p.strokeColor = new Color(1f, .85f, .45f, al); p.lineWidth = wdt; Ellipse(cx, cy - .01f, .2f * k, .058f * k); p.Stroke(); }
                p.strokeColor = new Color(1f, .9f, .6f, .85f); p.lineWidth = 1.6f;
                for (int i = 0; i < 28; i++) { float ang = i / 28f * Mathf.PI * 2; var a0 = E(cx, cy - .01f, .2f * .86f, .058f * .86f, ang); var a1 = E(cx, cy - .01f, .2f * .96f, .058f * .96f, ang + .05f); p.BeginPath(); p.MoveTo(a0); p.LineTo(a1); p.Stroke(); }
            };
            return a;
        }

        // banners on tall poles at the edges: ours blue with a gold wolf mark, theirs red with a gold flame
        VisualElement Banners()
        {
            var b = Box(); b.style.position = Position.Absolute; b.style.left = b.style.right = b.style.top = b.style.bottom = 0; b.pickingMode = PickingMode.Ignore;
            b.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float W = r.width, H = r.height;
                void Flag(float x, Color cloth, Color dark, bool right)
                {
                    float s = right ? -1 : 1; p.strokeColor = new Color(.25f, .2f, .18f); p.lineWidth = Mathf.Max(3, W * .004f);
                    p.BeginPath(); p.MoveTo(new Vector2(W * x, H * .2f)); p.LineTo(new Vector2(W * x, H * .8f)); p.Stroke();
                    p.BeginPath(); p.MoveTo(new Vector2(W * (x - s * .012f), H * .26f)); p.LineTo(new Vector2(W * (x + s * .07f), H * .26f)); p.Stroke();   // the cross bar
                    p.fillGradient = FillGradient.MakeLinearGradient(cloth, dark, new Vector2(W * x, 0), new Vector2(W * (x + s * .07f), 0), AddressMode.Clamp);
                    p.BeginPath(); p.MoveTo(new Vector2(W * (x + s * .004f), H * .265f)); p.LineTo(new Vector2(W * (x + s * .066f), H * .265f));
                    p.LineTo(new Vector2(W * (x + s * .07f), H * .6f)); p.LineTo(new Vector2(W * (x + s * .036f), H * .55f)); p.LineTo(new Vector2(W * (x + s * .008f), H * .6f)); p.ClosePath(); p.Fill(); p.fillGradient = default;
                    p.strokeColor = new Color(1f, .82f, .4f, .9f); p.lineWidth = 2; p.BeginPath(); p.MoveTo(new Vector2(W * (x + s * .012f), H * .29f)); p.LineTo(new Vector2(W * (x + s * .058f), H * .29f)); p.Stroke();
                    var c = new Vector2(W * (x + s * .036f), H * .41f); float rr = W * .016f;
                    p.BeginPath(); p.Arc(c, rr, 0, 360); p.Stroke(); p.BeginPath(); p.MoveTo(c + new Vector2(0, -rr * 1.5f)); p.LineTo(c + new Vector2(rr * .7f, 0)); p.LineTo(c + new Vector2(0, rr * 1.5f)); p.LineTo(c + new Vector2(-rr * .7f, 0)); p.ClosePath(); p.Stroke();
                }
                Flag(.018f, new Color(.2f, .38f, .9f), new Color(.08f, .14f, .45f), false); Flag(.982f, new Color(.85f, .16f, .14f), new Color(.42f, .05f, .06f), true);
            };
            return b;
        }

        // cherry blossom heaped in the bottom corners and hanging into the top ones
        VisualElement Blossoms()
        {
            var b = Box(); b.style.position = Position.Absolute; b.style.left = b.style.right = b.style.top = b.style.bottom = 0; b.pickingMode = PickingMode.Ignore;
            var rnd = new System.Random(8); var dots = new List<Vector4>();
            void Heap(float x, float y, float rx, float ry, int n) { for (int i = 0; i < n; i++) { float a = (float)rnd.NextDouble() * Mathf.PI * 2, d = Mathf.Sqrt((float)rnd.NextDouble()); dots.Add(new Vector4(x + Mathf.Cos(a) * d * rx, y + Mathf.Sin(a) * d * ry, .006f + (float)rnd.NextDouble() * .01f, (float)rnd.NextDouble())); } }
            Heap(.02f, .98f, .12f, .12f, 70); Heap(.98f, .98f, .12f, .12f, 70); Heap(.02f, .02f, .07f, .1f, 40); Heap(.98f, .02f, .07f, .1f, 40);
            b.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float W = r.width, H = r.height;
                foreach (var d in dots)
                {
                    var c = new Vector2(d.x * W, d.y * H); float s = d.z * W;
                    p.fillColor = Color.Lerp(new Color(1f, .62f, .78f, .95f), new Color(1f, .86f, .92f, .95f), d.w);
                    for (int k = 0; k < 5; k++) { float a = k / 5f * Mathf.PI * 2 + d.w * 3; p.BeginPath(); p.Arc(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * s * .55f, s * .5f, 0, 360); p.Fill(); }
                    p.fillColor = new Color(1f, .85f, .4f, .9f); p.BeginPath(); p.Arc(c, s * .22f, 0, 360); p.Fill();
                }
            };
            return b;
        }
    }
}
