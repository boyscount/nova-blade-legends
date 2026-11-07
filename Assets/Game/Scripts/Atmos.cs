using UnityEngine;

namespace NovaBlade
{
    // Stage atmosphere, built at runtime on a stage root: low drifting mist, fireflies, and soft glowing halos around
    // every "Glow" marker (lamp posts, braziers) so lights bloom through the fog like lanterns at dusk.
    public class Atmos : MonoBehaviour
    {
        public Color mist = new(.45f, .5f, .62f, .1f); public float mistArea = 44; public int fireflies = 60;
        public Color haloColor = new(1, .62f, .3f); public float haloSize = 2.6f;

        static Texture2D dot;
        public static Texture2D Dot()
        {
            if (dot) return dot; dot = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f; float a = Mathf.Clamp01(1 - d); dot.SetPixel(x, y, new Color(1, 1, 1, a * a * a)); }
            dot.Apply(); return dot;
        }
        Material Mat(bool additive) { var m = new Material(Catalog.I.glowMat) { mainTexture = Dot() }; m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", additive ? 1 : 10); return m; }

        void Start()
        {
            // halos: a camera-facing soft glow at each marker, breathing slightly
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Glow")) continue;
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Destroy(q.GetComponent<Collider>()); q.name = "Halo"; q.transform.SetParent(t, false);
                q.transform.localScale = Vector3.one * haloSize; var m = Mat(true); m.SetFloat("_UseVC", 0); m.SetColor("_Color", haloColor * .55f);
                q.GetComponent<Renderer>().sharedMaterial = m; q.AddComponent<Halo>();
            }
            // mist: big faint puffs drifting low over the ground
            var mistPs = Ps("Mist", new Vector3(0, .5f, -2), new Vector3(mistArea, .6f, mistArea * .8f), Mat(false));
            var mm = mistPs.main; mm.startLifetime = 18; mm.startSpeed = .15f; mm.startSize = new ParticleSystem.MinMaxCurve(5, 9); mm.maxParticles = 90; mm.startColor = mist; mm.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var me = mistPs.emission; me.rateOverTime = 5;
            Fade(mistPs, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            mistPs.Simulate(18, true, true); mistPs.Play();
            if (fireflies <= 0) return;
            var ff = Ps("Fireflies", new Vector3(0, 1.2f, -2), new Vector3(mistArea, 2.4f, mistArea * .8f), Mat(true));
            var fm = ff.main; fm.startLifetime = new ParticleSystem.MinMaxCurve(5, 9); fm.startSpeed = .05f; fm.startSize = new ParticleSystem.MinMaxCurve(.08f, .15f); fm.maxParticles = fireflies * 3;
            fm.startColor = new ParticleSystem.MinMaxGradient(new Color(5, 4.2f, 1.6f), new Color(3.4f, 5, 1.8f));
            var fe = ff.emission; fe.rateOverTime = fireflies / 7f; var nz = ff.noise; nz.enabled = true; nz.strength = .35f; nz.frequency = .35f;
            Fade(ff, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(.2f, .35f), new GradientAlphaKey(1, .55f), new GradientAlphaKey(.2f, .75f), new GradientAlphaKey(0, 1) });
            ff.Simulate(8, true, true); ff.Play();
        }
        ParticleSystem Ps(string n, Vector3 at, Vector3 box, Material mat)
        {
            var go = new GameObject(n); go.transform.SetParent(transform, false); go.transform.localPosition = at;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; return ps;
        }
        static void Fade(ParticleSystem ps, GradientAlphaKey[] a)
        {
            var c = ps.colorOverLifetime; c.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, a); c.color = g;
        }
        class Halo : MonoBehaviour
        {
            float s0, seed; void Awake() { s0 = transform.localScale.x; seed = Random.value * 9; }
            void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; transform.localScale = Vector3.one * s0 * (.92f + .08f * Mathf.PerlinNoise(Time.time * 3, seed)); }
        }
    }
}
