using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // The VS screen's art, drawn in code: a menacing night (a huge moon in a halo, storm clouds lit at their edges, mountains and a dark
    // citadel on the horizon, a crimson glow behind them), a jagged fire bolt between the sides, a burning VS; and the rank badges.
    public partial class Ui
    {
        static Texture2D vsNight;
        static float Fbm(float x, float y, int oct = 5) { float v = 0, a = .5f, f = 1; for (int i = 0; i < oct; i++) { v += a * Mathf.PerlinNoise(x * f, y * f); a *= .5f; f *= 2.03f; } return v; }
        static float Smooth(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3 - 2 * t); }
        static Texture2D VsNight()
        {
            if (vsNight) return vsNight; const int W = 640, H = 360; vsNight = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * H]; const float aspect = W / (float)H; var moon = new Vector2(.38f * aspect, .19f); const float mr = .12f;
            for (int y = 0; y < H; y++)
            {
                float v = 1 - (y + .5f) / H;   // 0 at the top
                for (int x = 0; x < W; x++)
                {
                    float u = (x + .5f) / W; var q = new Vector2(u * aspect, v);
                    // sky: near-black indigo up high, deep blue, a smouldering crimson low down
                    var c = Color.Lerp(new Color(.01f, .015f, .05f), new Color(.05f, .08f, .2f), Smooth(0, .55f, v));
                    c = Color.Lerp(c, new Color(.32f, .06f, .05f), Smooth(.5f, .82f, v) * .85f);
                    float dm = Vector2.Distance(q, moon);
                    c += new Color(.35f, .42f, .6f) * Mathf.Pow(Mathf.Clamp01(1 - dm / .6f), 2.6f) * .6f;   // wide halo, soft
                    c += new Color(.6f, .66f, .8f) * Mathf.Pow(Mathf.Clamp01(1 - dm / .32f), 3f);             // close halo
                    if (dm < mr)
                    {   // the moon: pale, a little warm at the rim, dark seas and craters
                        float k = dm / mr; var p = (q - moon) / mr; float seas = Fbm(p.x * 2.2f + 7, p.y * 2.2f + 3, 4), crat = Fbm(p.x * 9 + 1, p.y * 9 + 5, 3);
                        var mc = new Color(.92f, .93f, .98f) * (1 - .16f * Smooth(.42f, .7f, seas) - .07f * Smooth(.5f, .72f, crat));   // soft seas, faint craters
                        mc *= 1 - .35f * Mathf.Pow(k, 3); mc = Color.Lerp(mc, new Color(1f, .82f, .7f), Mathf.Pow(k, 6) * .4f);
                        c = Color.Lerp(c, mc, Smooth(1, .97f, k));
                    }
                    // storm clouds: dark masses, their edges lit by the moon, thicker low down and across the moon's lower half
                    float cl = Fbm(u * 3.2f + 11, v * 6.5f + 2) * .7f + Fbm(u * 9 + 3, v * 14) * .3f; float band = Smooth(.18f, .5f, v) * .55f + .25f;
                    float wisp = Smooth(.12f, .02f, Mathf.Abs(v - moon.y - .06f - .04f * Mathf.Sin(u * 9))) * Smooth(.45f, .7f, Fbm(u * 6 + 4, v * 3)) * Smooth(.5f, .2f, Mathf.Abs(q.x - moon.x));   // a thin cloud across the moon
                    float cov = Mathf.Max(Smooth(.44f, .64f, cl + band - .4f), wisp * .8f);   // heavier storm clouds
                    var lit = new Color(.55f, .6f, .75f) * Mathf.Clamp01(1 - dm / .6f) * Smooth(.62f, .52f, cl + band - .45f);
                    c = Color.Lerp(c, new Color(.04f, .05f, .09f) + lit * .6f, cov * .85f);
                    // horizon: two mountain ridges and a dark citadel, mist between them
                    float far = .70f - .09f * Fbm(u * 5 + 1, 3, 4) - .1f * Mathf.Pow(Mathf.Abs(Mathf.Sin(u * 6.1f + .4f)), 3);   // jagged peaks
                    float near = .80f - .09f * Fbm(u * 3.5f + 9, 7, 4) - .04f * Mathf.Abs(Mathf.Sin(u * 13 + 2));
                    float tower = 0; float keepTop = .58f; if (u > .62f && u < .8f && v > keepTop + .03f * Mathf.Floor(Mathf.Abs(Mathf.Sin(u * 90)) * 2) * 0) tower = 1;   // the citadel's wall
                    foreach (var (tx, tw, th) in new[] { (.7f, .016f, .3f), (.735f, .011f, .24f), (.665f, .012f, .2f), (.765f, .009f, .17f), (.635f, .008f, .12f), (.785f, .007f, .1f) }) if (Mathf.Abs(u - tx) < tw && v > keepTop - th + Mathf.Max(0, Mathf.Abs(u - tx) - tw * .4f) * 6) tower = 1;
                    if (u > .62f && u < .8f && v < keepTop + .02f && Mathf.Abs(Mathf.Repeat(u * 140, 2) - 1) < .5f && v > keepTop - .012f) tower = 1;   // battlements
                    if (v > far || tower > 0) c = Color.Lerp(c, new Color(.05f, .03f, .06f), .9f);
                    if (tower > 0 && v > keepTop - .2f && Mathf.PerlinNoise(x * .55f, y * .55f) > .82f && (x + y) % 3 == 0) c = new Color(1f, .55f, .15f);   // lit windows
                    c = Color.Lerp(c, new Color(.35f, .12f, .1f), Smooth(.05f, 0, Mathf.Abs(v - far)) * .25f);   // a red rim on the far ridge
                    if (v > near) c = Color.Lerp(c, new Color(.015f, .012f, .02f), .95f);
                    c += new Color(.3f, .1f, .08f) * Smooth(.06f, 0, Mathf.Abs(v - (far + near) / 2)) * .35f;   // mist
                    if (cov < .35f && v < .55f && dm > .22f) { float st = Mathf.PerlinNoise(x * 1.7f + 3, y * 1.7f); if (st > .8f) c += Color.white * (st - .8f) * 4.5f; }   // more stars, some brighter   // a few stars
                    c.a = 1; px[y * W + x] = c;
                }
            }
            vsNight.SetPixels(px); vsNight.Apply(); return vsNight;
        }
        // a jagged bolt of fire from the top to the bottom, leaning, drawn as three strokes: a wide glow, the flame, a white-hot core
        // ---------- the VS screen's lightning and cliffs ----------
        // A bolt splits the sky between the two teams: it strikes down from the top (drawn growing over a few frames) with a white flash,
        // stays lit with a flicker, and strikes again every second or two with a new shape and a smaller flash.
        float boltAge = 99, boltNext; int boltSeed = 1;
        readonly List<Vector2> boltPts = new(); readonly List<List<Vector2>> boltForks = new();
        void NewBolt()
        {
            var rnd = new System.Random(boltSeed++); boltPts.Clear(); boltForks.Clear();
            float R() => (float)rnd.NextDouble();
            // a jagged line from the sky (a little right of centre) down into the chasm, by midpoint displacement
            var line = new List<Vector2> { new(.53f + (R() - .5f) * .04f, -.04f), new(.495f + (R() - .5f) * .02f, .9f) };
            for (int pass = 0; pass < 5; pass++)
            {
                var nl = new List<Vector2>(); float amp = .07f / (1 << pass);
                for (int i = 0; i < line.Count - 1; i++) { nl.Add(line[i]); var m = (line[i] + line[i + 1]) * .5f; m.x += (R() - .5f) * amp * 2; m.y += (R() - .5f) * amp * .4f; nl.Add(m); }
                nl.Add(line[^1]); line = nl;
            }
            boltPts.AddRange(line);
            for (int f = 0; f < 4; f++)
            {   // forks branching off and dying out
                int at = 3 + rnd.Next(line.Count - 10); var p = line[at]; var fork = new List<Vector2> { p }; float dir = R() < .5f ? -1 : 1;
                for (int k = 0; k < 6; k++) { p += new Vector2(dir * (.008f + R() * .02f), .02f + R() * .03f); fork.Add(p); }
                boltForks.Add(fork);
            }
        }
        VisualElement Lightning()
        {
            NewBolt(); boltAge = 99; boltNext = 0;
            var b = Box(); b.style.position = Position.Absolute; b.style.left = b.style.right = b.style.top = b.style.bottom = 0; b.pickingMode = PickingMode.Ignore;
            b.generateVisualContent += ctx =>
            {
                if (boltAge > 50) return;
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; p.lineJoin = LineJoin.Round; p.lineCap = LineCap.Round;
                float grow = Mathf.Clamp01(boltAge / .1f), tm = Time.unscaledTime;
                float lit = boltAge < .1f ? 1 : Mathf.Max(.72f, 1 - (boltAge - .1f) * 2.2f);   // full at the strike, settling to a steady glow
                lit *= .82f + .18f * Mathf.PerlinNoise(tm * 18, 3);                           // and a crackling flicker
                void Path(List<Vector2> pts, float upTo)
                {
                    int n = Mathf.Max(2, Mathf.CeilToInt(pts.Count * upTo)); p.BeginPath();
                    for (int i = 0; i < n && i < pts.Count; i++) { var q = new Vector2(pts[i].x * r.width, pts[i].y * r.height); if (i == 0) p.MoveTo(q); else p.LineTo(q); }
                }
                void Draw(List<Vector2> pts, float upTo, float k)
                {
                    p.strokeColor = new Color(.35f, .55f, 1f, .1f * k); p.lineWidth = 80; Path(pts, upTo); p.Stroke();
                    p.strokeColor = new Color(.45f, .7f, 1f, .22f * k); p.lineWidth = 34; Path(pts, upTo); p.Stroke();
                    p.strokeColor = new Color(.6f, .82f, 1f, .7f * k); p.lineWidth = 13; Path(pts, upTo); p.Stroke();
                    p.strokeColor = new Color(.97f, .99f, 1f, k); p.lineWidth = 5; Path(pts, upTo); p.Stroke();
                }
                Draw(boltPts, grow, lit);
                if (grow >= 1) { float fk = lit * Mathf.Clamp01(1 - (boltAge - .1f) * 1.5f); if (fk > .02f) foreach (var f in boltForks) Draw(f, 1, fk * .8f); }
                if (grow >= 1)
                {   // where it lands: a burst of light in the chasm
                    var end = new Vector2(boltPts[^1].x * r.width, boltPts[^1].y * r.height);
                    p.fillColor = new Color(.6f, .8f, 1f, .25f * lit); p.BeginPath(); p.Arc(end, 46, 0, 360); p.Fill();
                    p.fillColor = new Color(.9f, .97f, 1f, .5f * lit); p.BeginPath(); p.Arc(end, 14, 0, 360); p.Fill();
                }
            };
            return b;
        }
        // returns the strength of a new strike this frame (0 if none), for the screen flash
        float UpdLightning(float dt, float vsT)
        {
            if (vsT < .5f) return 0;
            boltAge += dt; float hit = 0;
            if (boltAge > 50) { boltAge = 0; boltNext = 1.4f + Random.value * 1.2f; hit = 1; }        // the first strike
            else if (boltAge > boltNext) { NewBolt(); boltAge = 0; boltNext = 1.2f + Random.value * 1.4f; hit = .45f; }   // strikes again
            return hit;
        }
        // two cliffs the teams stand on, a chasm between them: dark rock, its top edge lit in the side's colour, cracks, mist in the gap
        VisualElement Cliffs()
        {
            var c = Box(); c.style.position = Position.Absolute; c.style.left = c.style.right = c.style.top = c.style.bottom = 0; c.pickingMode = PickingMode.Ignore;
            var rnd = new System.Random(21); float R() => (float)rnd.NextDouble();
            List<Vector2> Shape()
            {   // the left cliff, from the far edge to the chasm: a broken top, then a jagged drop
                var pts = new List<Vector2> { new(-.02f, 1.02f), new(-.02f, .72f) };
                for (float x = .03f; x < .44f; x += .02f + R() * .03f) pts.Add(new Vector2(x, .715f - x * .05f + (R() - .5f) * .03f - (R() < .2f ? .02f : 0)));
                pts.Add(new Vector2(.455f, .675f)); pts.Add(new Vector2(.468f, .69f)); pts.Add(new Vector2(.472f, .73f));
                for (float y = .76f; y < 1; y += .04f + R() * .05f) pts.Add(new Vector2(.465f - (y - .73f) * .25f + (R() - .5f) * .02f, y));
                pts.Add(new Vector2(.39f, 1.02f)); return pts;
            }
            var left = Shape(); var right = Shape(); for (int i = 0; i < right.Count; i++) right[i] = new Vector2(1 - right[i].x, right[i].y);
            var cracks = new List<(Vector2 a, Vector2 b)>(); for (int i = 0; i < 26; i++) { var a = new Vector2(.02f + R() * .42f, .76f + R() * .22f); cracks.Add((a, a + new Vector2((R() - .5f) * .03f, .03f + R() * .06f))); }
            c.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; Vector2 S(Vector2 v) => new(v.x * r.width, v.y * r.height);
                void Cliff(List<Vector2> pts, Color rim, bool mirror)
                {
                    p.fillGradient = FillGradient.MakeLinearGradient(new Color(.15f, .14f, .16f), new Color(.02f, .02f, .03f), S(new Vector2(.25f, .7f)), S(new Vector2(.25f, 1)), AddressMode.Clamp);
                    p.BeginPath(); p.MoveTo(S(pts[0])); for (int i = 1; i < pts.Count; i++) p.LineTo(S(pts[i])); p.ClosePath(); p.Fill();
                    // the flat rock the heroes stand on: a lighter band along the top, lit from above
                    int top = 1; while (top + 1 < pts.Count && pts[top + 1].y < .74f) top++;
                    p.fillGradient = FillGradient.MakeLinearGradient(new Color(.42f, .4f, .42f), new Color(.17f, .16f, .18f), S(new Vector2(.25f, .67f)), S(new Vector2(.25f, .76f)), AddressMode.Clamp);
                    p.BeginPath(); p.MoveTo(S(pts[1])); for (int i = 2; i <= top; i++) p.LineTo(S(pts[i])); for (int i = top; i >= 1; i--) p.LineTo(S(pts[i] + new Vector2(0, .045f))); p.ClosePath(); p.Fill();
                    // the chasm-side face catches the lightning
                    p.strokeColor = new Color(.7f, .85f, 1f, .55f); p.lineWidth = 3; p.BeginPath(); p.MoveTo(S(pts[top])); for (int i = top + 1; i < pts.Count - 1; i++) p.LineTo(S(pts[i])); p.Stroke();
                    p.lineCap = LineCap.Round; p.strokeColor = new Color(0, 0, 0, .45f); p.lineWidth = 2;
                    foreach (var (a, b) in cracks) { var A = mirror ? new Vector2(1 - a.x, a.y) : a; var B = mirror ? new Vector2(1 - b.x, b.y) : b; p.BeginPath(); p.MoveTo(S(A)); p.LineTo(S(B)); p.Stroke(); }
                    // the lit top edge: a soft glow and a thin bright line along the rim
                    p.lineJoin = LineJoin.Round;
                    for (int pass = 0; pass < 2; pass++)
                    {
                        p.strokeColor = pass == 0 ? new Color(rim.r, rim.g, rim.b, .25f) : new Color(Mathf.Min(1, rim.r + .3f), Mathf.Min(1, rim.g + .3f), Mathf.Min(1, rim.b + .3f), .9f); p.lineWidth = pass == 0 ? 10 : 2.5f;
                        p.BeginPath(); p.MoveTo(S(pts[1])); for (int i = 2; i < pts.Count; i++) { if (pts[i].y > .74f) break; p.LineTo(S(pts[i])); } p.Stroke();
                    }
                }
                Cliff(left, new Color(.35f, .65f, 1f), false); Cliff(right, new Color(1f, .4f, .2f), true);
            };
            return c;
        }
        float boltGlow = 1;   // the slash's outer glow, pulsed by VsIntro
        VisualElement FireBolt()
        {
            var b = Box(); b.style.position = Position.Absolute; b.style.left = b.style.right = b.style.top = b.style.bottom = 0;
            var rnd = new System.Random(5); var pts = new List<Vector2>();
            for (int i = 0; i <= 18; i++) { float t = i / 18f; pts.Add(new Vector2(.58f - .16f * t + (float)(rnd.NextDouble() - .5) * .035f, -.05f + 1.1f * t)); }
            var sparks = new List<(Vector2 p, float r)>(); for (int i = 0; i < 40; i++) { float t = (float)rnd.NextDouble(); sparks.Add((new Vector2(.58f - .16f * t + (float)(rnd.NextDouble() - .5) * .14f, -.05f + 1.1f * t), 1 + (float)rnd.NextDouble() * 2.5f)); }
            b.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; p.lineJoin = LineJoin.Miter; p.lineCap = LineCap.Round;
                void Path() { p.BeginPath(); for (int i = 0; i < pts.Count; i++) { var q = new Vector2(pts[i].x * r.width, pts[i].y * r.height); if (i == 0) p.MoveTo(q); else p.LineTo(q); } }
                float gk = boltGlow;
                p.strokeColor = new Color(1, .3f, .05f, .07f * gk); p.lineWidth = 70 * (.7f + .3f * gk); Path(); p.Stroke();
                p.strokeColor = new Color(1, .35f, .05f, .14f * gk); p.lineWidth = 34; Path(); p.Stroke();
                p.strokeColor = new Color(1, .5f, .1f, .3f * Mathf.Min(gk, 1.4f)); p.lineWidth = 16; Path(); p.Stroke();
                p.strokeColor = new Color(1, .65f, .2f, .95f); p.lineWidth = 6; Path(); p.Stroke();
                p.strokeColor = new Color(1, .97f, .85f, 1); p.lineWidth = 2.2f; Path(); p.Stroke();
                foreach (var (sp, sr) in sparks) { p.fillColor = new Color(1, .7f + sr * .08f, .3f, .85f); p.BeginPath(); p.Arc(new Vector2(sp.x * r.width, sp.y * r.height), sr, 0, 360); p.Fill(); }
            };
            return b;
        }
        // the VS: a burst of flame behind, a dark shadow, the letters in gold with a deep outline and an orange glow
        VisualElement BurningVs(bool phone)
        {
            var box = Box(); box.style.position = Position.Absolute; box.style.left = 0; box.style.right = 0; box.style.top = Length.Percent(31); box.style.height = Length.Percent(30); box.style.alignItems = Align.Center; box.style.justifyContent = Justify.Center;
            var burst = Box(); burst.style.position = Position.Absolute; burst.style.width = phone ? 300 : 380; burst.style.height = phone ? 300 : 380;
            var rnd = new System.Random(3); var spikes = new List<(float a, float l, float w)>(); for (int i = 0; i < 22; i++) spikes.Add(((float)rnd.NextDouble() * Mathf.PI * 2, .55f + (float)rnd.NextDouble() * .45f, .08f + (float)rnd.NextDouble() * .1f));
            burst.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; var c = r.center; float R = r.width * .5f;
                foreach (var (a, l, w) in spikes)
                    foreach (var (k, col) in new[] { (1f, new Color(1, .35f, .05f, .35f)), (.7f, new Color(1, .6f, .15f, .55f)) })
                    {
                        p.fillColor = col; p.BeginPath(); p.MoveTo(c + new Vector2(Mathf.Cos(a - w), Mathf.Sin(a - w)) * R * .18f); p.LineTo(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * R * l * k); p.LineTo(c + new Vector2(Mathf.Cos(a + w), Mathf.Sin(a + w)) * R * .18f); p.ClosePath(); p.Fill();
                    }
            };
            box.Add(burst);
            var glow = Box(); glow.style.position = Position.Absolute; glow.style.width = phone ? 280 : 360; glow.style.height = phone ? 200 : 250; glow.style.backgroundImage = new StyleBackground(SoftGlow()); glow.style.unityBackgroundImageTintColor = new Color(1, .5f, .1f, .9f); glow.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; box.Add(glow);
            float size = phone ? 120 : 165;
            var shadow = L("VS", size, new Color(.35f, .03f, 0, .9f), true); shadow.style.position = Position.Absolute; shadow.style.translate = new Translate(6, 8); shadow.style.rotate = new Rotate(-8); box.Add(shadow);
            var vs = L("VS", size, new Color(1, .86f, .42f), true); vs.style.unityTextOutlineWidth = .28f; vs.style.unityTextOutlineColor = new Color(.42f, .1f, 0);
            vs.style.textShadow = new TextShadow { offset = Vector2.zero, blurRadius = 30, color = new Color(1, .4f, 0) }; vs.style.rotate = new Rotate(-8); box.Add(vs);
            return box;
        }

        // ---------- ranks ----------
        public static readonly (int min, string th, string en, Color c)[] Ranks =
        {
            (0, "สัมฤทธิ์", "Bronze", new Color(.8f, .5f, .3f)), (100, "เงิน", "Silver", new Color(.78f, .82f, .9f)), (250, "ทอง", "Gold", new Color(1f, .8f, .3f)),
            (450, "แพลทินัม", "Platinum", new Color(.45f, .9f, .85f)), (700, "เพชร", "Diamond", new Color(.5f, .7f, 1f)), (1000, "ปรมาจารย์", "Master", new Color(.8f, .45f, 1f)), (1400, "ตำนาน", "Legend", new Color(1f, .4f, .3f)),
        };
        public static (string name, Color c, int tier) RankOf(int pts)
        {
            int t = 0; for (int i = 0; i < Ranks.Length; i++) if (pts >= Ranks[i].min) t = i;
            var r = Ranks[t]; string n = Loc.S(r.th, r.en);
            if (t < Ranks.Length - 1) { int span = Ranks[t + 1].min - r.min, div = 3 - Mathf.Min(2, (pts - r.min) * 3 / span); n += " " + new[] { "", "I", "II", "III" }[div]; }
            else n += $" {pts - r.min}";
            return (n, r.c, t);
        }
        // a small shield badge in the rank's colour, with as many pips as its tier step
        VisualElement RankBadge(int pts, float s)
        {
            var (_, c, tier) = RankOf(pts); var b = Box(); b.style.width = s; b.style.height = s * 1.15f; b.style.flexShrink = 0;
            b.generateVisualContent += ctx =>
            {
                var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height;
                void Shield(float i) { p.BeginPath(); p.MoveTo(new Vector2(w * .5f, i)); p.LineTo(new Vector2(w - i, h * .2f)); p.LineTo(new Vector2(w - i, h * .55f)); p.LineTo(new Vector2(w * .5f, h - i)); p.LineTo(new Vector2(i, h * .55f)); p.LineTo(new Vector2(i, h * .2f)); p.ClosePath(); }
                p.fillColor = c * .45f + new Color(0, 0, 0, 1); Shield(1); p.Fill(); p.strokeColor = c; p.lineWidth = 2; Shield(1.5f); p.Stroke();
                p.fillColor = c; int pips = Mathf.Clamp(tier + 1, 1, 7);
                for (int i = 0; i < Mathf.Min(pips, 3); i++) { float x = w * (.5f + (i - (Mathf.Min(pips, 3) - 1) / 2f) * .24f); p.BeginPath(); p.MoveTo(new Vector2(x, h * .3f)); p.LineTo(new Vector2(x + w * .09f, h * .45f)); p.LineTo(new Vector2(x, h * .6f)); p.LineTo(new Vector2(x - w * .09f, h * .45f)); p.ClosePath(); p.Fill(); }
            };
            return b;
        }
    }
}
