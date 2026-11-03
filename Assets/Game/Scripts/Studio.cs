using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Card and portrait pictures, rendered once at start-up from the real models (heroes: head portrait, pets/weapons: whole model,
    // stages: an overhead view of each arena).
    public static class Studio
    {
        const int Layer = 31, Size = 256;
        static readonly Dictionary<string, Texture2D> tex = new();
        static Camera cam; static RenderTexture rt; static Light key, rim;
        static readonly Vector3 Spot = new(0, -300, 0);

        public static Texture2D Hero(string id) => tex.TryGetValue("hero_" + id, out var t) ? t : null;
        public static Texture2D Pet(string id) => tex.TryGetValue("pet_" + id, out var t) ? t : null;
        public static Texture2D Weapon(string id) => tex.TryGetValue("w_" + id, out var t) ? t : null;
        public static Texture2D Stage(int i) => tex.TryGetValue("stage_" + i, out var t) ? t : null;

        public static void Build()
        {
            var go = new GameObject("StudioCam"); cam = go.AddComponent<Camera>(); cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer; cam.fieldOfView = 30; cam.nearClipPlane = .05f; cam.farClipPlane = 50;
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            // studio light of its own: bright warm key, soft front fill, cool rim, and a neutral ambient (the scene's own ambient is dark at start-up)
            key = new GameObject("StudioKey").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.7f; key.color = new Color(1, .95f, .88f); key.cullingMask = 1 << Layer; key.transform.rotation = Quaternion.Euler(28, 205, 0);
            rim = new GameObject("StudioRim").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = 1.5f; rim.color = new Color(.6f, .8f, 1); rim.cullingMask = 1 << Layer; rim.transform.rotation = Quaternion.Euler(20, 20, 0);
            var fill = new GameObject("StudioFill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .55f; fill.color = new Color(1, .9f, .8f); fill.cullingMask = 1 << Layer; fill.transform.rotation = Quaternion.Euler(-5, 160, 0);
            var amb = (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientProbe);
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(new Color(.32f, .32f, .36f)); sh.AddDirectionalLight(Vector3.up, new Color(.2f, .2f, .24f), 1);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.34f, .34f, .38f); RenderSettings.ambientProbe = sh;
            foreach (var h in Data.Heroes)
            {
                var pf = h.rig ? Resources.Load<GameObject>("Heroes/Hero_" + h.id) : Catalog.I.Model(h.id);
                tex["hero_" + h.id] = Shoot(pf, h.rig ? .78f : .5f, h.rig ? .3f : 1.1f, .35f, true);
            }
            foreach (var p in Data.Pets) tex["pet_" + p.id] = Shoot(Catalog.I.Model(p.id), .48f, 1.15f, .45f);
            foreach (var w in Data.Swords) tex["w_" + w.id] = Shoot(Catalog.I.Model(w.id), .5f, 1.0f, 0, weapon: true);
            Object.Destroy(key.gameObject); Object.Destroy(rim.gameObject); Object.Destroy(fill.gameObject);
            (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientProbe) = amb;
            for (int i = 0; i < Data.Themes.Length; i++) tex["stage_" + i] = StageShot(i);
            Object.Destroy(go); rt.Release();
        }

        // focusY: fraction of the model height to aim at; frame: fraction of the height that fills the picture
        static Texture2D Shoot(GameObject pf, float focusY, float frame, float yaw, bool animated = false, bool weapon = false)
        {
            if (!pf) return null;
            var inst = Object.Instantiate(pf, Spot, Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0));
            if (weapon) inst.transform.rotation = Quaternion.Euler(0, 0, 45);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var an = inst.GetComponentInChildren<Animator>(); if (an && animated) { an.Rebind(); an.Update(0); }
            var rs = inst.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float hgt = weapon ? b.size.magnitude : b.size.y;
            var focus = weapon ? b.center : new Vector3(b.center.x, b.min.y + b.size.y * focusY, b.center.z);
            float dist = hgt * frame * .5f / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            cam.transform.position = focus + new Vector3(0, hgt * frame * .08f, dist); cam.transform.LookAt(focus);
            var t2 = Grab(); Object.DestroyImmediate(inst); return t2;
        }
        static Texture2D StageShot(int i)
        {
            var G = Main.G; G.SetTheme(i);
            cam.cullingMask = ~(1 << Layer); cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 42;
            cam.transform.position = new Vector3(0, 9, 15); cam.transform.LookAt(new Vector3(0, 0, 1)); cam.farClipPlane = 120;
            return Grab();
        }
        static Texture2D Grab()
        {
            cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); t.Apply(); RenderTexture.active = prev; return t;
        }
    }
}
