using UnityEngine;

namespace NovaBlade
{
    // A soft, volumetric-looking fire for braziers and torches (replaces the cartoon flame prefab): a white-yellow core,
    // an orange body that licks upward and fades to red, and a few embers drifting off. Additive particles, sized by the
    // object's scale (1 = a brazier about 40 cm across).
    public class Flame : MonoBehaviour
    {
        static Texture2D soft;
        static Texture2D Soft()
        {
            if (soft) return soft; const int N = 64; soft = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {   // a teardrop: rounder at the bottom, thinner at the top
                float u = (x + .5f) / N * 2 - 1, v = (y + .5f) / N * 2 - 1, w = Mathf.Lerp(1.1f, .55f, (v + 1) / 2);
                float d = Mathf.Sqrt(u * u / (w * w) + v * v); float a = Mathf.Clamp01(1 - d); soft.SetPixel(x, y, new Color(1, 1, 1, a * a));
            }
            soft.Apply(true); return soft;
        }
        void Start()
        {
            float s = transform.lossyScale.y;
            Layer("core", s, .14f, .32f, 1.2f, 2.2f, new Color(1, .95f, .75f) * 2.6f, new Color(1, .7f, .3f) * 1.4f, 26, .12f);
            Layer("body", s, .28f, .7f, .9f, 1.6f, new Color(1, .55f, .18f) * 2.0f, new Color(.7f, .12f, .04f) * .9f, 30, .2f);
            Layer("embers", s, .03f, 1.6f, 1.2f, 2.6f, new Color(1, .65f, .25f) * 3f, new Color(1, .3f, .05f) * 2f, 7, .25f, true);
        }
        void Layer(string n, float s, float size, float life, float sp0, float sp1, Color c0, Color c1, float rate, float radius, bool embers = false)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 120; m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            m.startLifetime = new ParticleSystem.MinMaxCurve(life * .6f, life); m.startSpeed = new ParticleSystem.MinMaxCurve(sp0 * .3f, sp1 * .3f);
            m.startSize = new ParticleSystem.MinMaxCurve(size * .7f, size * 1.3f); m.startRotation = new ParticleSystem.MinMaxCurve(-.3f, .3f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = radius; sh.rotation = new Vector3(-90, 0, 0);
            var em = ps.emission; em.rateOverTime = rate;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.Local; vel.y = new ParticleSystem.MinMaxCurve(sp0, sp1);
            vel.x = vel.z = new ParticleSystem.MinMaxCurve(0, 0);
            var noise = ps.noise; noise.enabled = true; noise.strength = embers ? .6f : .35f; noise.frequency = 2.2f; noise.scrollSpeed = 1.2f;
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(c1 / Mathf.Max(.01f, c0.maxColorComponent), 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(embers ? 1 : .6f, .55f), new GradientAlphaKey(0, 1) }); col.color = g;
            m.startColor = c0;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, embers ? AnimationCurve.Linear(0, 1, 1, .3f) : AnimationCurve.EaseInOut(0, .7f, 1, .15f));
            var r = go.GetComponent<ParticleSystemRenderer>(); var mat = new Material(Catalog.I.glowMat) { mainTexture = Soft() }; mat.SetFloat("_SrcBlend", 1); mat.SetFloat("_DstBlend", 1);
            r.sharedMaterial = mat; r.renderMode = embers ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.VerticalBillboard;
            ps.Play();
        }
    }
}
