using UnityEngine;

namespace NovaBlade
{
    // A standing gateway of light, made here instead of from an effects pack (their portals render dark in URP):
    // two swirling discs turning against each other inside a bright rim, motes drifting in, a soft light on the ground.
    public class Portal : MonoBehaviour
    {
        const int TexSize = 128; const float Spin = 70, Pulse = 2.2f, Bright = .75f;   // additive under bloom: kept low or the disc burns out white
        static Texture2D swirl;
        Transform a, b; Material ma, mb; Color col; float t;

        public static Portal Make(Transform parent, Vector3 ground, Color c, float r)
        {
            var go = new GameObject("Portal"); go.transform.SetParent(parent, false); go.transform.position = ground + Vector3.up * (r * 1.15f);
            var p = go.AddComponent<Portal>(); p.col = c;
            p.a = p.Disc(r * 2, out p.ma, c * Bright); p.b = p.Disc(r * 1.7f, out p.mb, Color.Lerp(c, Color.white, .3f) * Bright);
            p.Motes(r, c);
            var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(go.transform, false); l.type = LightType.Point; l.color = c; l.intensity = 1.6f; l.range = r * 5; l.shadows = LightShadows.None;
            return p;
        }
        Transform Disc(float size, out Material m, Color c)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.transform.SetParent(transform, false); q.transform.localScale = new Vector3(size, size * 1.25f, 1);
            m = new Material(Catalog.I.glowMat) { mainTexture = Swirl(), renderQueue = 3050 }; m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 1); m.SetFloat("_UseVC", 0); m.SetColor("_Color", c);
            var r = q.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var back = Instantiate(q, transform); back.transform.localRotation = Quaternion.Euler(0, 180, 0); back.transform.SetParent(q.transform, true);   // seen from behind too
            return q.transform;
        }
        void Motes(float r, Color c)
        {
            var go = new GameObject("Motes"); go.transform.SetParent(transform, false); var ps = go.AddComponent<ParticleSystem>(); ps.Stop();
            var m = ps.main; m.loop = true; m.startLifetime = 1.4f; m.startSpeed = -r * .9f; m.startSize = new ParticleSystem.MinMaxCurve(.06f, .14f); m.startColor = Color.Lerp(c, Color.white, .4f); m.maxParticles = 40; m.simulationSpace = ParticleSystemSimulationSpace.Local;
            var e = ps.emission; e.rateOverTime = 22; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = r * 1.3f; sh.radiusThickness = 0; sh.rotation = new Vector3(0, 0, 0);
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(0, 1) }); col.color = g;
            var mat = new Material(Catalog.I.glowMat) { mainTexture = Atmos.Dot() }; mat.SetFloat("_SrcBlend", 1); mat.SetFloat("_DstBlend", 1);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; ps.Play();
        }
        // a spiral of light fading to the middle, inside a bright ring
        static Texture2D Swirl()
        {
            if (swirl) return swirl;
            swirl = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < TexSize; y++) for (int x = 0; x < TexSize; x++)
            {
                float u = (x + .5f) / TexSize * 2 - 1, v = (y + .5f) / TexSize * 2 - 1, r = Mathf.Sqrt(u * u + v * v), ang = Mathf.Atan2(v, u);
                float arms = .5f + .5f * Mathf.Sin(ang * 3 + r * 11), body = (1 - Mathf.SmoothStep(.15f, .95f, r)) * (.1f + .6f * arms) * Mathf.SmoothStep(.05f, .45f, r);   // dark in the very middle: a hole, not a lamp
                float rim = Mathf.Exp(-Mathf.Pow((r - .9f) / .045f, 2)), a = Mathf.Clamp01(Mathf.Max(body, rim)) * (r < 1 ? 1 : 0);
                swirl.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            swirl.Apply(false); return swirl;
        }
        void Update()
        {
            t += Time.deltaTime; if (a) a.localRotation = Quaternion.Euler(0, 0, t * Spin); if (b) b.localRotation = Quaternion.Euler(0, 0, -t * Spin * 1.6f);
            float k = .85f + .15f * Mathf.Sin(t * Pulse); if (ma) ma.SetColor("_Color", col * Bright * k); if (mb) mb.SetColor("_Color", Color.Lerp(col, Color.white, .3f) * Bright * (1.6f - k * .6f));
        }
    }
}
