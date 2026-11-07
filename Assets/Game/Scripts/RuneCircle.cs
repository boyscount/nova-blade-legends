using UnityEngine;

namespace NovaBlade
{
    // Rings of glowing ancient letters on the Sky Court floor: an outer band just inside the railing and a smaller inner one,
    // turning against each other and breathing slowly. The letters are drawn here (straight carved strokes on a grid, like old runes),
    // so no picture file is needed. Their colour follows the duellist; Flare() brightens them for a moment.
    public class RuneCircle : MonoBehaviour
    {
        const int CellPx = 64, PerStrip = 8, Seg = 160; const float Bright = .95f, Breath = 1.4f, OuterSpin = 2.2f, InnerSpin = -3.4f, FlareFade = 1.2f;
        static Texture2D strip;
        Transform outer, inner; Material mo, mi; Color col = new(1, .8f, .45f), target; float t, flare;

        public static RuneCircle Make(Transform parent, float outerR, float innerR)
        {
            var go = new GameObject("RuneCircle"); go.transform.SetParent(parent, false); go.transform.position = new Vector3(0, .03f, 0);
            var rc = go.AddComponent<RuneCircle>(); rc.target = rc.col;
            rc.outer = rc.Band("Outer", outerR - 1, outerR, out rc.mo); rc.inner = rc.Band("Inner", innerR - .8f, innerR, out rc.mi);
            return rc;
        }
        public void Set(Color c) { target = c; }
        public void Flare() { flare = 1; }
        // a flat ring on the ground; the letter strip repeats round it with letters about as wide as the band
        Transform Band(string name, float r0, float r1, out Material m)
        {
            float w = r1 - r0, around = 2 * Mathf.PI * (r0 + r1) / 2; int repeats = Mathf.Max(1, Mathf.RoundToInt(around / w / PerStrip));
            var v = new Vector3[(Seg + 1) * 2]; var uv = new Vector2[v.Length]; var tri = new int[Seg * 6];
            for (int i = 0; i <= Seg; i++)
            {
                float a = i / (float)Seg * Mathf.PI * 2, c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[i * 2] = new Vector3(c * r0, 0, s * r0); v[i * 2 + 1] = new Vector3(c * r1, 0, s * r1);
                uv[i * 2] = new Vector2(i / (float)Seg * repeats, 0); uv[i * 2 + 1] = new Vector2(i / (float)Seg * repeats, 1);
                if (i < Seg) { int k = i * 6, b = i * 2; tri[k] = b; tri[k + 1] = b + 2; tri[k + 2] = b + 1; tri[k + 3] = b + 1; tri[k + 4] = b + 2; tri[k + 5] = b + 3; }   // clockwise seen from above
            }
            var mesh = new Mesh { name = "Runes" + name, vertices = v, uv = uv, triangles = tri }; mesh.RecalculateBounds();
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            m = new Material(Catalog.I.glowMat) { mainTexture = Strip(), renderQueue = 2995 }; m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 1); m.SetFloat("_UseVC", 0); m.SetColor("_Color", col * Bright);
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            return go.transform;
        }
        // the letter strip: two thin border lines and a row of runes, each a few straight strokes between points of a 3 x 4 grid, sometimes a ring
        static Texture2D Strip()
        {
            if (strip) return strip;
            int W = CellPx * PerStrip, H = CellPx; var a = new float[W * H]; var rng = new System.Random(7);
            void Line(Vector2 p, Vector2 q, float wid)
            {
                int x0 = Mathf.Max(0, (int)(Mathf.Min(p.x, q.x) - wid - 1)), x1 = Mathf.Min(W - 1, (int)(Mathf.Max(p.x, q.x) + wid + 1)), y0 = Mathf.Max(0, (int)(Mathf.Min(p.y, q.y) - wid - 1)), y1 = Mathf.Min(H - 1, (int)(Mathf.Max(p.y, q.y) + wid + 1));
                var d = q - p; float len2 = Mathf.Max(1e-4f, d.sqrMagnitude);
                for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                {
                    var o = new Vector2(x + .5f, y + .5f); float u = Mathf.Clamp01(Vector2.Dot(o - p, d) / len2); float dist = Vector2.Distance(o, p + d * u);
                    a[y * W + x] = Mathf.Max(a[y * W + x], Mathf.Clamp01(wid - dist + .5f));
                }
            }
            void Ring(Vector2 c, float r, float wid) { for (int i = 0; i < 16; i++) { float a0 = i / 16f * Mathf.PI * 2, a1 = (i + 1) / 16f * Mathf.PI * 2; Line(c + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, wid); } }
            Line(new Vector2(0, H * .08f), new Vector2(W, H * .08f), 2.6f); Line(new Vector2(0, H * .92f), new Vector2(W, H * .92f), 2.6f);   // strokes are thick: seen from ten metres a thin line fades out in the smaller mip levels
            for (int g = 0; g < PerStrip; g++)
            {
                float cx = g * CellPx; Vector2 P(int i, int j) => new(cx + CellPx * (.3f + .2f * i), H * (.22f + .187f * j));   // grid 3 wide, 4 tall inside the band
                Line(P(1, 0), P(1, 3), 5f);   // most runes stand on one upright stave
                int strokes = 2 + rng.Next(3);
                for (int s = 0; s < strokes; s++) { int i0 = rng.Next(3), j0 = rng.Next(4), i1 = rng.Next(3), j1 = rng.Next(4); if (i0 == i1 && j0 == j1) continue; Line(P(i0, j0), P(i1, j1), 4.6f); }
                if (rng.Next(4) == 0) Ring(P(1, rng.Next(1, 3)), CellPx * .13f, 3.4f);
                if (g % 2 == 1) Line(new Vector2(cx + 2, H * .3f), new Vector2(cx + 2, H * .7f), 2.4f);   // a small divider between words
            }
            strip = new Texture2D(W, H, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var px = new Color32[W * H]; for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, (byte)(a[i] * 255));
            strip.SetPixels32(px); strip.Apply(true); strip.wrapModeV = TextureWrapMode.Clamp; return strip;
        }
        void Update()
        {
            t += Time.deltaTime; flare = Mathf.Max(0, flare - Time.deltaTime / FlareFade); col = Color.Lerp(col, target, 1 - Mathf.Exp(-Time.deltaTime * 3));
            if (outer) outer.localRotation = Quaternion.Euler(0, t * OuterSpin, 0); if (inner) inner.localRotation = Quaternion.Euler(0, t * InnerSpin, 0);
            float k = Bright * (.75f + .25f * Mathf.Sin(t * Breath)) * (1 + flare * 1.5f);
            if (mo) mo.SetColor("_Color", col * k); if (mi) mi.SetColor("_Color", col * k * (.85f + .15f * Mathf.Sin(t * Breath + 1.6f)));
        }
    }
}
