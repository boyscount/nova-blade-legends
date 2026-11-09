using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // The time of day over Sky Lanes follows the match clock: day at the start, it fades to dusk (a low orange sun, a pink-gold sky)
    // and then to night (a cool moon, a deep blue sky, the team lights and glows carry the scene), and night stays to the end.
    // Phase 0 = day (the stage as built), 1 = dusk, 2 = night; anything between is a blend. Set after the stage's own look (StageLook.Apply).
    public static class SkyTime
    {
        // match seconds: day until DuskFrom, fades to dusk by DuskAt, holds, fades to night from NightFrom to NightAt
        public const float DuskFrom = 90, DuskAt = 150, NightFrom = 240, NightAt = 300;   // a match runs about 10 minutes: night for its second half
        public static float? Force;   // tests: a fixed phase instead of the clock
        public static float Phase { get; private set; }

        struct Look
        {
            public Color sun; public float sunI; public Quaternion sunAt; public Color sky, eq, ground, fog; public float fogD;
            public Color skyTint, skyGround; public float exposure, atmos; public Color rim; public float rimI; public Color cloud; public float grass;
            public static Look Lerp(Look a, Look b, float t) => new()
            {
                sun = Color.Lerp(a.sun, b.sun, t), sunI = Mathf.Lerp(a.sunI, b.sunI, t), sunAt = Quaternion.Slerp(a.sunAt, b.sunAt, t),
                sky = Color.Lerp(a.sky, b.sky, t), eq = Color.Lerp(a.eq, b.eq, t), ground = Color.Lerp(a.ground, b.ground, t), fog = Color.Lerp(a.fog, b.fog, t), fogD = Mathf.Lerp(a.fogD, b.fogD, t),
                skyTint = Color.Lerp(a.skyTint, b.skyTint, t), skyGround = Color.Lerp(a.skyGround, b.skyGround, t), exposure = Mathf.Lerp(a.exposure, b.exposure, t), atmos = Mathf.Lerp(a.atmos, b.atmos, t),
                rim = Color.Lerp(a.rim, b.rim, t), rimI = Mathf.Lerp(a.rimI, b.rimI, t), cloud = Color.Lerp(a.cloud, b.cloud, t), grass = Mathf.Lerp(a.grass, b.grass, t),
            };
        }
        static readonly Look Dusk = new()
        {
            sun = new Color(1f, .56f, .3f), sunI = 1.7f, sunAt = Quaternion.Euler(14, 235, 0),
            sky = new Color(.62f, .48f, .62f), eq = new Color(.6f, .42f, .38f), ground = new Color(.26f, .19f, .18f), fog = new Color(.82f, .52f, .5f), fogD = .007f,
            skyTint = new Color(.78f, .36f, .42f), skyGround = new Color(.95f, .5f, .36f), exposure = .95f, atmos = 2.4f,
            rim = new Color(.75f, .55f, 1f), rimI = .5f, cloud = new Color(1f, .68f, .62f), grass = .92f,
        };
        static readonly Look Night = new()
        {
            sun = new Color(.56f, .66f, 1f), sunI = .7f, sunAt = Quaternion.Euler(48, 200, 0),
            sky = new Color(.2f, .26f, .44f), eq = new Color(.15f, .18f, .3f), ground = new Color(.07f, .07f, .11f), fog = new Color(.05f, .08f, .17f), fogD = .0075f,
            skyTint = new Color(.1f, .14f, .32f), skyGround = new Color(.06f, .08f, .16f), exposure = .32f, atmos = .55f,
            rim = new Color(.5f, .65f, 1f), rimI = .85f, cloud = new Color(.32f, .38f, .58f), grass = .8f,
        };

        static Transform stage, dayOf; static Look day; static Light sun, rim; static Material skyInst;
        static readonly List<Renderer> clouds = new(), grass = new();
        static readonly int Tint = Shader.PropertyToID("_Color"), Base = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock pb;

        // on entering Sky Lanes: read the day look from the stage as built, then start at the clock's phase
        public static void Begin(Transform root, float matchTime)
        {
            stage = root; if (!root) return; pb ??= new MaterialPropertyBlock();
            var look = root.GetComponent<StageLook>(); sun = RenderSettings.sun; rim = root.Find("RimLight")?.GetComponent<Light>();
            clouds.Clear(); grass.Clear();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var n = r.sharedMaterial ? r.sharedMaterial.name : "";
                if (n.StartsWith("SkyCloud")) clouds.Add(r); else if (n.StartsWith("SkyLanesGrass")) grass.Add(r);
            }
            var dome = look ? look.sky : RenderSettings.skybox; skyInst = dome ? new Material(dome) : null;
            if (root != dayOf) day = new Look   // read once per stage: its sun and rim light keep what the last match left them
            {
                sun = sun ? sun.color : Color.white, sunI = sun ? sun.intensity : 1, sunAt = sun ? sun.transform.rotation : Quaternion.identity,
                sky = RenderSettings.ambientSkyColor, eq = RenderSettings.ambientEquatorColor, ground = RenderSettings.ambientGroundColor, fog = RenderSettings.fogColor, fogD = RenderSettings.fogDensity,
                skyTint = dome && dome.HasProperty("_SkyTint") ? dome.GetColor("_SkyTint") : Color.white, skyGround = dome && dome.HasProperty("_GroundColor") ? dome.GetColor("_GroundColor") : Color.white,
                exposure = dome && dome.HasProperty("_Exposure") ? dome.GetFloat("_Exposure") : 1, atmos = dome && dome.HasProperty("_AtmosphereThickness") ? dome.GetFloat("_AtmosphereThickness") : 1,
                rim = rim ? rim.color : Color.white, rimI = rim ? rim.intensity : 0, cloud = Color.white, grass = 1,
            };
            dayOf = root; if (skyInst) RenderSettings.skybox = skyInst;
            Phase = -1; Tick(matchTime);
        }
        public static float PhaseAt(float t) => Force ?? (t < NightFrom ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(DuskFrom, DuskAt, t)) : 1 + Mathf.SmoothStep(0, 1, Mathf.InverseLerp(NightFrom, NightAt, t)));

        // each frame of a Sky Lanes match; returns 1 the moment dusk is reached, 2 when night is, else 0 (for a banner)
        public static int Tick(float matchTime)
        {
            if (!stage || !stage.gameObject.activeInHierarchy) return 0;
            float p = PhaseAt(matchTime); if (Mathf.Abs(p - Phase) < .004f) return 0;
            int reached = Phase >= 0 && Phase < 1 && p >= 1 ? 1 : Phase >= 0 && Phase < 2 && p >= 2 ? 2 : 0;
            Phase = p; Set(p < 1 ? Look.Lerp(day, Dusk, p) : Look.Lerp(Dusk, Night, p - 1)); return reached;
        }

        static void Set(Look k)
        {
            if (sun) { sun.color = k.sun; sun.intensity = k.sunI; sun.transform.rotation = k.sunAt; }
            RenderSettings.ambientSkyColor = k.sky; RenderSettings.ambientEquatorColor = k.eq; RenderSettings.ambientGroundColor = k.ground;
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(k.eq);
            sh.AddDirectionalLight(Vector3.up, (k.sky - k.eq) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (k.ground - k.eq) * 1.6f, 1); RenderSettings.ambientProbe = sh;
            Shader.SetGlobalVector("_NovaAmbient", (k.sky + k.eq) * .5f * Mathf.Lerp(.7f, 1, k.grass));   // the 3D grass blades darken with the ground
            RenderSettings.fogColor = k.fog; RenderSettings.fogDensity = k.fogD; if (Camera.main) Camera.main.backgroundColor = k.fog;
            if (skyInst)
            {
                if (skyInst.HasProperty("_SkyTint")) skyInst.SetColor("_SkyTint", k.skyTint); if (skyInst.HasProperty("_GroundColor")) skyInst.SetColor("_GroundColor", k.skyGround);
                if (skyInst.HasProperty("_Exposure")) skyInst.SetFloat("_Exposure", k.exposure); if (skyInst.HasProperty("_AtmosphereThickness")) skyInst.SetFloat("_AtmosphereThickness", k.atmos);
            }
            if (rim) { rim.color = k.rim; rim.intensity = k.rimI; }
            Tinted(clouds, k.cloud); Tinted(grass, new Color(k.grass, k.grass, k.grass, 1));
        }
        static void Tinted(List<Renderer> list, Color by)
        {
            bool plain = by.r > .999f && by.g > .999f && by.b > .999f;
            foreach (var r in list)
            {
                if (!r) continue; if (plain) { r.SetPropertyBlock(null); continue; }
                var m = r.sharedMaterial; pb.Clear();
                if (m.HasProperty(Tint)) pb.SetColor(Tint, m.GetColor(Tint) * by);
                if (m.HasProperty(Base)) pb.SetColor(Base, m.GetColor(Base) * by);
                r.SetPropertyBlock(pb);
            }
        }
        public static void End() => stage = null;
    }
}
