using UnityEngine;

namespace NovaBlade
{
    // Lobby backdrop: the forest at night. Moonlight and dark blue fog, fireflies drifting in the grass,
    // warm lantern glows with flickering light, and low mist. Built once at runtime, switched on only in the lobby.
    public static class LobbyNight
    {
        // background blur in the lobby only: the scene's "LobbyDOF" volume (built by NovaBuild) is switched on here
        static void Dof(float w) { var v = GameObject.Find("LobbyDOF"); if (v && v.GetComponent<UnityEngine.Rendering.Volume>() is var vol && vol) vol.weight = w; }
        static GameObject root, set; static Light sun; static Color sunCol; static float sunInt; static bool on;

        public static void Set(bool want)
        {
            if (want == on) return; on = want;
            if (!want)
            {   // the stage's own StageLook.Apply() restores fog, ambient and sky; the sun is restored here
                if (root) root.SetActive(false); if (set) set.SetActive(false); Dof(0); Cull(false);
                if (sun) { sun.color = sunCol; sun.intensity = sunInt; }
                return;
            }
            if (!root) Build();
            root.SetActive(true); if (set) set.SetActive(true);
            Dof(1); Cull(true);
            sun = RenderSettings.sun; if (sun) { sunCol = sun.color; sunInt = sun.intensity; sun.color = new Color(.78f, .82f, .98f); sun.intensity = .75f; }
            Color sky = new(.3f, .34f, .5f), eq = new(.18f, .19f, .26f), ground = new(.05f, .06f, .07f), fog = new(.04f, .065f, .1f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = fog; RenderSettings.fogDensity = .022f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = sky; RenderSettings.ambientEquatorColor = eq; RenderSettings.ambientGroundColor = ground;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(eq);
            sh.AddDirectionalLight(Vector3.up, (sky - eq) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (ground - eq) * 1.6f, 1);
            RenderSettings.ambientProbe = sh; Shader.SetGlobalVector("_NovaAmbient", (sky + eq) * .5f);
            if (Camera.main) { Camera.main.clearFlags = CameraClearFlags.SolidColor; Camera.main.backgroundColor = new Color(.02f, .03f, .06f); }
        }

        // the lobby shows the hero and the dark treeline: the stage's far scenery is lost in fog but cost 40% of the frame.
        // The night sky and set sit on their own layer, so only stage scenery is cut.
        const int NightLayer = 1; const float FarScenery = 24;
        static void Cull(bool lobby)
        {
            var c = Camera.main; if (!c) return; var d = new float[32]; if (lobby) d[0] = FarScenery; c.layerCullDistances = d;
            if (!lobby) return;
            foreach (var g in new[] { root, set }) if (g) foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = NightLayer;
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

        static void Spot(Vector3 p, Color c, float intensity, float range, Vector3? at = null)
        {
            var l = new GameObject("Rim").AddComponent<Light>(); l.transform.SetParent(root.transform, false); l.type = LightType.Spot; l.color = c; l.intensity = intensity; l.range = range;
            l.spotAngle = 55; l.innerSpotAngle = 25; l.shadows = LightShadows.None; l.transform.position = p; l.transform.LookAt(at ?? new Vector3(0, 1.1f, 0));
        }
        // glowing cherry petals drifting down through the light, turning as they fall
        public static Transform PetalsAt(Transform parent)
        {   // the same falling petals for any other place
            var keep = root; var holder = new GameObject("PetalsHolder"); holder.transform.SetParent(parent, false); root = holder; Petals(); root = keep;
            var ps = holder.GetComponentInChildren<ParticleSystem>(); var sh = ps.shape; sh.scale = new Vector3(30, .5f, 24);
            var m = ps.main; m.maxParticles = 420; m.startSizeX = new ParticleSystem.MinMaxCurve(.24f, .38f); m.startSizeY = new ParticleSystem.MinMaxCurve(.24f, .38f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .88f, .93f, 1), Color.white);
            var em = ps.emission; em.rateOverTime = 52;
            var sheet = Resources.Load<Texture2D>("UI/City/petal_sheet");
            if (sheet) { var tsa = ps.textureSheetAnimation; tsa.enabled = true; tsa.numTilesX = 2; tsa.numTilesY = 2; tsa.animation = ParticleSystemAnimationType.WholeSheet; tsa.frameOverTime = new ParticleSystem.MinMaxCurve(0, 0); tsa.startFrame = new ParticleSystem.MinMaxCurve(0, 1); ps.GetComponent<ParticleSystemRenderer>().sharedMaterial.mainTexture = sheet; }
            ps.Clear(); ps.Play(); return ps.transform;
        }
        static void Petals()
        {
            var go = new GameObject("Petals"); go.transform.SetParent(root.transform, false); go.transform.position = new Vector3(0, 3.6f, .2f);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.prewarm = true; m.maxParticles = 140; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(6, 10); m.startSpeed = 0; m.startSize3D = true;
            m.startSizeX = new ParticleSystem.MinMaxCurve(.13f, .2f); m.startSizeY = new ParticleSystem.MinMaxCurve(.09f, .14f); m.startSizeZ = 1;
            m.startRotation3D = true; m.startRotationX = m.startRotationY = m.startRotationZ = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
            m.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, .42f, .62f, .95f), new Color(1f, .6f, .76f, .95f)); m.gravityModifier = .025f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(9, .5f, 6);
            var em = ps.emission; em.rateOverTime = 24;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(.15f, .45f); vel.y = new ParticleSystem.MinMaxCurve(-.25f, -.1f); vel.z = new ParticleSystem.MinMaxCurve(-.1f, .1f);
            var noise = ps.noise; noise.enabled = true; noise.strength = .35f; noise.frequency = .6f; noise.scrollSpeed = .3f;
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = rot.y = new ParticleSystem.MinMaxCurve(-2, 2); rot.z = new ParticleSystem.MinMaxCurve(-1, 1);
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(1, .85f), new GradientAlphaKey(0, 1) }); col.color = g;
            const int N = 32; var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
            {   // a petal: an ellipse with a small notch at the tip
                float u = (x + .5f) / N * 2 - 1, v = (y + .5f) / N * 2 - 1, e = u * u + v * v * 1.6f, notch = v > .55f && Mathf.Abs(u) < .12f ? 0 : 1;
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((1 - e) * 4) * notch));
            }
            t.Apply(true);
            var mat = new Material(Catalog.I.glowMat) { mainTexture = t }; mat.SetFloat("_SrcBlend", 5); mat.SetFloat("_DstBlend", 10); mat.SetColor("_Color", new Color(1.35f, 1.2f, 1.25f));   // alpha-blended so they stay pink, a touch over 1 so they glow faintly
            var r = go.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = mat; r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx"); r.alignment = ParticleSystemRenderSpace.World;
            ps.Play();
        }
        // low mist drifting behind the hero: big, faint, slow puffs
        static void GroundMist()
        {
            var go = new GameObject("Mist"); go.transform.SetParent(root.transform, false); go.transform.position = new Vector3(0, .5f, -4.5f);
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.loop = true; m.prewarm = true; m.maxParticles = 40; m.simulationSpace = ParticleSystemSimulationSpace.World;
            m.startLifetime = new ParticleSystem.MinMaxCurve(10, 16); m.startSpeed = 0; m.startSize = new ParticleSystem.MinMaxCurve(3.5f, 6f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2); m.startColor = new Color(.55f, .65f, .9f, .07f);
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(14, .6f, 5);
            var em = ps.emission; em.rateOverTime = 3;
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.x = new ParticleSystem.MinMaxCurve(.08f, .2f); vel.y = new ParticleSystem.MinMaxCurve(0, .03f); vel.z = new ParticleSystem.MinMaxCurve(-.02f, .02f);
            var col = ps.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .3f), new GradientAlphaKey(1, .7f), new GradientAlphaKey(0, 1) }); col.color = g;
            var mat = new Material(Catalog.I.glowMat) { mainTexture = Dot() }; mat.SetFloat("_SrcBlend", 5); mat.SetFloat("_DstBlend", 10);   // alpha blended
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; ps.Play();
        }
        static void Build()
        {
            root = new GameObject("LobbyNight"); var dot = Dot();
            // key light from the camera side so the hero still reads in the dark, cool moon rim from behind
            Point(new Vector3(-1.6f, 2.2f, 3f), new Color(1, .94f, .88f), 14f, 10);
            Spot(new Vector3(.7f, 2f, 3.4f), new Color(1, .95f, .9f), 15, 8);   // a warm light on the hero alone, so skin and cloth keep their colours at night
            Point(new Vector3(.5f, 2.3f, -1.6f), new Color(.5f, .65f, 1), 6f, 6);
            // rim lights from behind both shoulders, cool moonlight and warm firelight, so the hero's edges glow off the dark
            Spot(new Vector3(-1.5f, 2.5f, -1.9f), new Color(.55f, .72f, 1), 16, 7);
            Spot(new Vector3(1.7f, 2.1f, -1.7f), new Color(1, .6f, .32f), 11, 7);
            Spot(new Vector3(-1.9f, 1.6f, -.6f), new Color(.6f, .75f, 1), 9, 5, new Vector3(-.8f, .45f, .8f));   // and one on the pet, beside the hero's feet
            Petals(); GroundMist();
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
        // lobby showcase: a slowly turning magic circle under the hero's feet and soft motes of light rising around them,
        // both in the hero's class colour
        class Showcase : MonoBehaviour
        {
            Transform circle; ParticleSystem motes; string cur; Material cm;
            static Texture2D ring;
            static Texture2D Ring()
            {   // two thin rings, rune ticks between them and a soft inner glow
                if (ring) return ring; const int N = 256; ring = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
                for (int y = 0; y < N; y++) for (int x = 0; x < N; x++)
                {
                    float dx = (x - N / 2f + .5f) / (N / 2f), dy = (y - N / 2f + .5f) / (N / 2f), r = Mathf.Sqrt(dx * dx + dy * dy), ang = Mathf.Atan2(dy, dx);
                    float a = Mathf.Exp(-Mathf.Pow((r - .95f) / .018f, 2)) + .8f * Mathf.Exp(-Mathf.Pow((r - .78f) / .012f, 2));
                    float tick = Mathf.Abs(Mathf.Sin(ang * 18)) > .93f && r > .8f && r < .93f ? .9f : 0; a += tick;
                    a += .55f * Mathf.Exp(-Mathf.Pow((r - .55f) / .01f, 2)) * (Mathf.Abs(Mathf.Sin(ang * 3)) > .5f ? 1 : .2f);
                    a += .12f * Mathf.Clamp01(1 - r) * Mathf.Clamp01(1 - r);
                    ring.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(a)));
                }
                ring.Apply(true); return ring;
            }
            static Color For(string cls) => cls switch { "assassin" => new Color(.75f, .45f, 1), "mage" => new Color(.35f, .7f, 1), "archer" => new Color(.55f, 1, .45f), "cleric" => new Color(1, .85f, .4f), "monk" => new Color(1, .45f, .3f), _ => new Color(1, .62f, .25f) };
            void Start()
            {
                circle = GameObject.CreatePrimitive(PrimitiveType.Quad).transform; Destroy(circle.GetComponent<Collider>()); circle.SetParent(transform, false); circle.name = "ShowcaseCircle";
                circle.localRotation = Quaternion.Euler(90, 0, 0); circle.localScale = Vector3.one * 2.6f;
                cm = new Material(Catalog.I.glowMat) { mainTexture = Ring() }; cm.SetFloat("_SrcBlend", 1); cm.SetFloat("_DstBlend", 1); cm.SetFloat("_UseVC", 0); circle.GetComponent<Renderer>().sharedMaterial = cm;
                var go = new GameObject("ShowcaseMotes"); go.transform.SetParent(transform, false); motes = go.AddComponent<ParticleSystem>(); motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var m = motes.main; m.loop = true; m.startLifetime = new ParticleSystem.MinMaxCurve(1.8f, 3); m.startSpeed = new ParticleSystem.MinMaxCurve(.2f, .5f); m.startSize = new ParticleSystem.MinMaxCurve(.03f, .07f); m.maxParticles = 120; m.simulationSpace = ParticleSystemSimulationSpace.World; m.gravityModifier = -.05f;
                var sh = motes.shape; sh.shapeType = ParticleSystemShapeType.Circle; sh.radius = .85f; sh.rotation = new Vector3(-90, 0, 0);
                var em = motes.emission; em.rateOverTime = 26; var vel = motes.velocityOverLifetime; vel.enabled = true; vel.y = new ParticleSystem.MinMaxCurve(.3f, .7f); vel.x = vel.z = new ParticleSystem.MinMaxCurve(0, 0);
                var col = motes.colorOverLifetime; col.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) }); col.color = g;
                var dot = new Material(Catalog.I.glowMat) { mainTexture = Ring() }; dot.SetFloat("_SrcBlend", 1); dot.SetFloat("_DstBlend", 1);
                var tex = new Texture2D(16, 16); for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(7.5f, 7.5f)) / 8; tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d))); } tex.Apply(); dot.mainTexture = tex;
                go.GetComponent<ParticleSystemRenderer>().sharedMaterial = dot; motes.Play();
            }
            void Update()
            {
                var h = Main.G ? Main.G.hero : null; if (!h || h.def == null || !circle) return;
                if (h.def.cls != cur) { cur = h.def.cls; var c = For(cur); cm.SetColor("_Color", c * 1.6f); var m = motes.main; m.startColor = new ParticleSystem.MinMaxGradient(c * 2.4f, Color.Lerp(c, Color.white, .5f) * 2.4f); }
                var p = h.transform.position; circle.position = p + Vector3.up * .04f; circle.rotation = Quaternion.Euler(90, Time.time * 14, 0);
                motes.transform.position = p + Vector3.up * .05f;
                float pulse = .85f + .15f * Mathf.Sin(Time.time * 2.2f); circle.localScale = Vector3.one * 2.6f * (.97f + .03f * pulse);
            }
        }
        class Face : MonoBehaviour { void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; } }
        class Billboard : MonoBehaviour
        {
            void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; float s = .5f + .04f * Mathf.Sin(Time.time * 9 + transform.position.x); transform.localScale = Vector3.one * s; }
        }
    }
}
