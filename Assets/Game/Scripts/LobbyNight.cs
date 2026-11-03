using UnityEngine;

namespace NovaBlade
{
    // Lobby backdrop: the forest at night. Moonlight and dark blue fog, fireflies drifting in the grass,
    // warm lantern glows with flickering light, and low mist. Built once at runtime, switched on only in the lobby.
    public static class LobbyNight
    {
        static GameObject root, set; static Light sun; static Color sunCol; static float sunInt; static bool on;

        public static void Set(bool want)
        {
            if (want == on) return; on = want;
            if (!want)
            {   // the stage's own StageLook.Apply() restores fog, ambient and sky; the sun is restored here
                if (root) root.SetActive(false); if (set) set.SetActive(false);
                if (sun) { sun.color = sunCol; sun.intensity = sunInt; }
                return;
            }
            if (!root) Build();
            root.SetActive(true); if (set) set.SetActive(true);
            sun = RenderSettings.sun; if (sun) { sunCol = sun.color; sunInt = sun.intensity; sun.color = new Color(.6f, .7f, 1); sun.intensity = .6f; }
            Color sky = new(.24f, .29f, .5f), eq = new(.13f, .15f, .24f), ground = new(.05f, .06f, .07f), fog = new(.04f, .065f, .1f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = fog; RenderSettings.fogDensity = .032f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(eq);
            sh.AddDirectionalLight(Vector3.up, (sky - eq) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (ground - eq) * 1.6f, 1);
            RenderSettings.ambientProbe = sh; Shader.SetGlobalVector("_NovaAmbient", (sky + eq) * .5f);
            if (Camera.main) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(.02f, .03f, .06f); }
        }

        static Texture2D Dot()
        {
            var t = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f; float a = Mathf.Clamp01(1 - d); t.SetPixel(x, y, new Color(1, 1, 1, a * a * a)); }
            t.Apply(); return t;
        }
        static Material Mat(bool additive, Texture tex)
        {
            var m = new Material(Catalog.I.glowMat) { mainTexture = tex }; m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", additive ? 1 : 10); return m;
        }

        static void Build()
        {
            root = new GameObject("LobbyNight"); var dot = Dot();
            // key light from the camera side so the hero still reads in the dark, cool moon rim from behind
            Point(new Vector3(-1.6f, 2.2f, 3f), new Color(1, .93f, .85f), 12f, 10);
            Point(new Vector3(.5f, 2.3f, -1.6f), new Color(.5f, .65f, 1), 6f, 6);
            // soft contact shadows so the hero and pet stand on the ground (the night lights cast none)
            var blobT = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 16f; float a = Mathf.Clamp01(1 - d); blobT.SetPixel(x, y, new Color(0, 0, 0, a * a * .8f)); }
            blobT.Apply(); var blobM = Mat(false, blobT); blobM.SetFloat("_UseVC", 0);
            foreach (var (pos, size) in new[] { (new Vector3(0, .03f, 0), 1.5f), (new Vector3(-.95f, .03f, .55f), .9f) })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>()); q.name = "ContactShadow"; q.transform.SetParent(root.transform, false);
                q.transform.position = pos; q.transform.rotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = new Vector3(size, size * .7f, 1); q.GetComponent<Renderer>().sharedMaterial = blobM;
            }
            // the scene's lobby set: fires, rocks, lantern; add flickering firelight and soft rising smoke at its markers
            var setT = Main.G ? Main.G.transform.Find("LobbySet") : null; set = setT ? setT.gameObject : null;
            if (setT) foreach (Transform mk in setT)
            {
                if (mk.name.StartsWith("Glow_")) { var l = Point(mk.position, new Color(1, .58f, .24f), mk.name == "Glow_pit" ? 5.5f : 3.2f, mk.name == "Glow_pit" ? 7 : 5); l.gameObject.AddComponent<Flicker>(); }
                if (mk.name.StartsWith("Smoke_")) Smoke(mk.position, mk.name == "Smoke_pit" ? 1 : .6f, dot);
            }
            Sky(dot);
            root.AddComponent<Showcase>();
            // fireflies: slow wandering glow points that pulse on and off
            var ff = Ps("Fireflies", new Vector3(.6f, 1.1f, -1.5f), new Vector3(8, 2.6f, 7), Mat(true, dot));
            var m = ff.main; m.startLifetime = new ParticleSystem.MinMaxCurve(5, 9); m.startSpeed = .05f; m.startSize = new ParticleSystem.MinMaxCurve(.09f, .17f); m.maxParticles = 200;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(6, 5.2f, 1.8f), new Color(3.8f, 6, 2));
            var em = ff.emission; em.rateOverTime = 28;
            var nz = ff.noise; nz.enabled = true; nz.strength = .35f; nz.frequency = .35f; nz.scrollSpeed = .2f;
            var col = ff.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(.15f, .3f), new GradientAlphaKey(1, .45f), new GradientAlphaKey(.2f, .65f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            col.color = g; ff.Simulate(8, true, true); ff.Play();
            // mist: big faint puffs drifting low over the grass
            var mist = Ps("Mist", new Vector3(.5f, .45f, -3), new Vector3(14, .5f, 9), Mat(false, dot));
            var mm = mist.main; mm.startLifetime = 16; mm.startSpeed = .12f; mm.startSize = new ParticleSystem.MinMaxCurve(4, 7); mm.maxParticles = 60;
            mm.startColor = new Color(.55f, .65f, .8f, .13f); mm.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
            var me = mist.emission; me.rateOverTime = 4;
            var mc = mist.colorOverLifetime; mc.enabled = true; var mg = new Gradient();
            mg.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) });
            mc.color = mg; mist.Simulate(16, true, true); mist.Play();
            root.SetActive(false);
        }
        // soft grey-blue smoke rising from a fire, lit warm at the base and fading as it spreads
        static void Smoke(Vector3 p, float k, Texture dot)
        {
            var sm = Ps("Smoke", p, new Vector3(.25f * k, .05f, .25f * k), Mat(false, dot)); var sh = sm.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8; sh.radius = .12f * k; sh.rotation = new Vector3(-90, 0, 0);
            var m = sm.main; m.startLifetime = new ParticleSystem.MinMaxCurve(3.5f * k + 1, 5 * k + 1.5f); m.startSpeed = new ParticleSystem.MinMaxCurve(.35f, .6f); m.startSize = new ParticleSystem.MinMaxCurve(.35f * k, .6f * k); m.maxParticles = 80;
            m.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f); m.startColor = new Color(.5f, .52f, .58f, .4f);
            var em = sm.emission; em.rateOverTime = 9 * k;
            var vel = sm.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World; vel.x = new ParticleSystem.MinMaxCurve(.05f, .22f); vel.y = new ParticleSystem.MinMaxCurve(0, 0); vel.z = new ParticleSystem.MinMaxCurve(-.05f, .05f);
            var sz = sm.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, .6f, 1, 2.6f));
            var rot = sm.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.4f, .4f);
            var col = sm.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(1, .7f, .45f), 0), new GradientColorKey(new Color(.75f, .78f, .85f), .25f), new GradientColorKey(new Color(.55f, .6f, .7f), 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.8f, .12f), new GradientAlphaKey(.45f, .55f), new GradientAlphaKey(0, 1) });
            col.color = g; sm.Simulate(6, true, true); sm.Play();
        }
        // night sky: a dome fading from the fog colour at the horizon to deep blue overhead, stars, and a soft moon
        static void Sky(Texture dot)
        {
            var mesh = new Mesh(); var v = new System.Collections.Generic.List<Vector3>(); var c = new System.Collections.Generic.List<Color>(); var tri = new System.Collections.Generic.List<int>();
            const int SEG = 48, RING = 12; Color horizon = new(.06f, .09f, .15f), glow = new(.13f, .13f, .25f), zenith = new(.012f, .02f, .055f);
            for (int r = 0; r <= RING; r++) for (int s = 0; s <= SEG; s++)
            {
                float el = Mathf.Lerp(-.15f, Mathf.PI / 2, r / (float)RING), az = s / (float)SEG * Mathf.PI * 2, t = Mathf.Clamp01(el / (Mathf.PI / 2));
                v.Add(new Vector3(Mathf.Cos(el) * Mathf.Cos(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Sin(az)) * 80);
                var k = t < .18f ? Color.Lerp(horizon, glow, t / .18f) : Color.Lerp(glow, zenith, Mathf.Pow((t - .18f) / .82f, .6f)); k.a = 1; c.Add(k);
                if (r < RING && s < SEG) { int a = r * (SEG + 1) + s, b = a + SEG + 1; tri.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
            }
            mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(tri, 0); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200);
            var dome = new GameObject("SkyDome"); dome.transform.SetParent(root.transform, false); dome.AddComponent<MeshFilter>().sharedMesh = mesh;
            var dm = new Material(Catalog.I.glowMat); dm.SetFloat("_SrcBlend", 1); dm.SetFloat("_DstBlend", 0); dm.renderQueue = 2900; dome.AddComponent<MeshRenderer>().sharedMaterial = dm;
            // stars: tiny twinkling points scattered over the upper sky
            var st = Ps("Stars", new Vector3(0, 0, 0), Vector3.one, Mat(true, dot)); var sh = st.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 70; sh.radiusThickness = 0; sh.scale = Vector3.one;
            var m = st.main; m.startSpeed = 0; m.startLifetime = new ParticleSystem.MinMaxCurve(4, 9); m.startSize = new ParticleSystem.MinMaxCurve(.12f, .32f); m.maxParticles = 500;
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1.6f, 1.7f, 2.2f), new Color(2.4f, 2.2f, 1.8f)); var em = st.emission; em.rateOverTime = 70;
            var col = st.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(.5f, .5f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
            col.color = g; st.Simulate(9, true, true); st.Play();
            // moon with a wide halo, up and to the left of the hero
            foreach (var (size, colr) in new[] { (16f, new Color(.25f, .3f, .45f)), (5.5f, new Color(.9f, .95f, 1.1f)), (3.2f, new Color(2.2f, 2.3f, 2.5f)) })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(q.GetComponent<Collider>()); q.name = "Moon"; q.transform.SetParent(root.transform, false);
                q.transform.position = new Vector3(16, 11, -48); q.transform.localScale = Vector3.one * size;
                var qm = Mat(true, dot); qm.SetColor("_Color", colr); qm.SetFloat("_UseVC", 0); qm.renderQueue = 2950; q.GetComponent<Renderer>().sharedMaterial = qm; q.AddComponent<Face>();
            }
        }
        static Light Point(Vector3 p, Color c, float intensity, float range)
        {
            var go = new GameObject("Light"); go.transform.SetParent(root.transform, false); go.transform.position = p;
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None; return l;
        }
        static ParticleSystem Ps(string name, Vector3 at, Vector3 box, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.playOnAwake = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = box;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; return ps;
        }

        class Flicker : MonoBehaviour
        {
            Light l; float baseI, seed;
            void Awake() { l = GetComponent<Light>(); baseI = l.intensity; seed = Random.value * 10; }
            void Update() { float t = Time.time * 7 + seed; l.intensity = baseI * (.85f + .15f * Mathf.PerlinNoise(t, seed)); }
        }
        // lobby showcase: two glowing trails orbit the hero in the class's element (fire, void, cosmos, nature...)
        class Showcase : MonoBehaviour
        {
            Transform a, b; string cur;
            static string For(string cls) => cls switch { "assassin" => "vx_void", "mage" => "vx_cosmos", "archer" => "vx_nature", "cleric" => "vx_ice", "monk" => "vx_electric", _ => "vx_fire" };
            void Update()
            {
                var h = Main.G ? Main.G.hero : null; if (!h || h.def == null) return;
                string want = For(h.def.cls);
                if (want != cur)
                {
                    cur = want; if (a) Destroy(a.gameObject); if (b) Destroy(b.gameObject); a = Orb(want); b = Orb(want);
                }
                float t = Time.time * 1.8f, r = .62f; var c = h.transform.position;
                a.position = c + new Vector3(Mathf.Cos(t) * r, 1.05f + Mathf.Sin(t * .5f) * .55f, Mathf.Sin(t) * r);
                b.position = c + new Vector3(Mathf.Cos(t + Mathf.PI) * r, 1.05f - Mathf.Sin(t * .5f) * .55f, Mathf.Sin(t + Mathf.PI) * r);
            }
            Transform Orb(string fx)
            {
                var o = new GameObject("ShowcaseTrail").transform; o.SetParent(transform, false);
                var pf = Catalog.I.Fx(fx); if (pf) { var g = Instantiate(pf, o); g.transform.localPosition = Vector3.zero; g.transform.localScale = Vector3.one * .3f; foreach (var ps in g.GetComponentsInChildren<ParticleSystem>(true)) { var mm = ps.main; mm.scalingMode = ParticleSystemScalingMode.Hierarchy; } foreach (var tr in g.GetComponentsInChildren<TrailRenderer>(true)) { tr.widthMultiplier *= .3f; tr.time *= .6f; } foreach (var au in g.GetComponentsInChildren<AudioSource>(true)) au.enabled = false; }
                return o;
            }
        }
        class Face : MonoBehaviour { void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; } }
        class Billboard : MonoBehaviour
        {
            void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; float s = .5f + .04f * Mathf.Sin(Time.time * 9 + transform.position.x); transform.localScale = Vector3.one * s; }
        }
    }
}
