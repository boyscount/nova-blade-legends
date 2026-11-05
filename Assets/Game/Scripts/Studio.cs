using System.Linq;
using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Card and portrait pictures, rendered once at start-up from the real models (heroes: head portrait, pets/weapons: whole model,
    // stages: an overhead view of each arena).
    public static class Studio
    {
        const int Layer = 31, Size = 256, HeroSize = 512;
        static readonly Dictionary<string, Texture2D> tex = new();
        static Camera cam; static RenderTexture rt; static Light key, rim;
        static readonly Vector3 Spot = new(0, -300, 0);

        public static Texture2D Hero(string id) => tex.TryGetValue("hero_" + id, out var t) ? t : null;
        public static Texture2D HeroFull(string id) => tex.TryGetValue("herofull_" + id, out var t) ? t : null;   // full-body card art
        public static Texture2D Wing(string id) => tex.TryGetValue("wing_" + id, out var t) ? t : null;
        public static Texture2D Pet(string id) => tex.TryGetValue("pet_" + id, out var t) ? t : null;
        public static Texture2D Weapon(string id) => tex.TryGetValue("w_" + id, out var t) ? t : null;
        public static Texture2D Stage(int i) => tex.TryGetValue("stage_" + i, out var t) ? t : null;

        public static void Build()
        {
            var go = new GameObject("StudioCam"); cam = go.AddComponent<Camera>(); cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = 1 << Layer; cam.fieldOfView = 30; cam.nearClipPlane = .05f; cam.farClipPlane = 50;
            rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32); cam.targetTexture = rt;
            // studio light of its own: bright warm key, soft front fill, cool rim, and a neutral ambient (the scene's own ambient is dark at start-up)
            key = new GameObject("StudioKey").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.35f; key.color = new Color(1, .95f, .88f); key.cullingMask = 1 << Layer; key.transform.rotation = Quaternion.Euler(28, 205, 0);
            rim = new GameObject("StudioRim").AddComponent<Light>(); rim.type = LightType.Directional; rim.intensity = 2.2f; rim.color = new Color(.6f, .8f, 1); rim.cullingMask = 1 << Layer; rim.transform.rotation = Quaternion.Euler(20, 20, 0);
            var fill = new GameObject("StudioFill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .45f; fill.color = new Color(1, .9f, .8f); fill.cullingMask = 1 << Layer; fill.transform.rotation = Quaternion.Euler(-5, 160, 0);
            var amb = (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientProbe);
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(new Color(.32f, .32f, .36f)); sh.AddDirectionalLight(Vector3.up, new Color(.2f, .2f, .24f), 1);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.34f, .34f, .38f); RenderSettings.ambientProbe = sh;
            foreach (var h in Data.Heroes)
            {
                var pf = h.rig ? Resources.Load<GameObject>("Heroes/Hero_" + h.id) : Catalog.I.Model(h.id);
                tex["hero_" + h.id] = h.rig ? Portrait(pf) : Shoot(pf, .5f, 1.1f, .35f, true);
                tex["herofull_" + h.id] = FullBody(pf, h.rig);
            }
            foreach (var p in Data.Pets) tex["pet_" + p.id] = Shoot(Catalog.I.Model(p.id), .48f, 1.15f, .45f);
            foreach (var w in Data.Wings) tex["wing_" + w.id] = Shoot(Catalog.I.Model("wing_" + w.id), .45f, 1.1f, Mathf.PI);   // seen from behind
            foreach (var w in Data.Swords) tex["w_" + w.id] = Shoot(Catalog.I.Model(w.id), .5f, 1.0f, 0, weapon: true);
            foreach (var w in Data.Shields) tex["w_" + w.id] = Shoot(Catalog.I.Model(w.id), .5f, 1.0f, 0, weapon: true);
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
        // hero card: the model's own standing pose (no animation frame, so the head is up), head and shoulders, seen from
        // slightly below at a three-quarter angle, rendered at 512 px
        static Texture2D Portrait(GameObject pf)
        {
            var inst = Object.Instantiate(pf, Spot, Quaternion.Euler(0, .4f * Mathf.Rad2Deg, 0));
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var an = inst.GetComponentInChildren<Animator>(); if (an) an.enabled = false;
            var rs = inst.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            var head = inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "head");
            float hgt = b.size.y; var focus = head ? head.position + Vector3.up * hgt * .055f : new Vector3(b.center.x, b.min.y + hgt * .88f, b.center.z);   // face: a little above the head bone (it starts at the neck)
            float frame = hgt * .36f, dist = frame * .5f / Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad);
            cam.transform.position = focus + new Vector3(0, -hgt * .02f, dist); cam.transform.LookAt(focus + Vector3.down * hgt * .07f);
            var big = new RenderTexture(HeroSize, HeroSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; var prev = cam.targetTexture; cam.targetTexture = big; cam.Render();
            var pa = RenderTexture.active; RenderTexture.active = big; var t2 = new Texture2D(HeroSize, HeroSize, TextureFormat.RGBA32, true); t2.ReadPixels(new Rect(0, 0, HeroSize, HeroSize), 0, 0); t2.Apply(true);
            RenderTexture.active = pa; cam.targetTexture = prev; big.Release(); Object.DestroyImmediate(inst); return t2;
        }
        // select-screen card: the whole hero standing tall, seen from a little below (heroic), 384 x 576, 4x anti-aliased
        static Texture2D FullBody(GameObject pf, bool rig)
        {
            if (!pf) return null; const int W = 384, H = 576;
            var inst = Object.Instantiate(pf, Spot, Quaternion.Euler(0, .45f * Mathf.Rad2Deg, 0));
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var an = inst.GetComponentInChildren<Animator>(); if (an) an.enabled = false;
            var rs = inst.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            // a model built in T-pose (as wide as it is tall, wings aside) takes its lobby stance for the card; the framing still comes from its height
            bool tPose = an && b.size.x > b.size.y * .9f;
            if (tPose) { an.enabled = true; an.Rebind(); foreach (var st in new[] { "show_f", "show_m", "show" }) if (an.HasState(0, Animator.StringToHash(st))) { an.Play(st, 0, .15f); break; } an.Update(0); an.enabled = false; }
            float hgt = tPose ? b.size.y : Mathf.Max(b.size.y, b.size.x * H / W * .8f), fov = cam.fieldOfView;
            float dist = hgt * 1.1f * .5f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            var look = new Vector3(b.center.x, b.min.y + b.size.y * .5f, b.center.z);
            cam.transform.position = look + new Vector3(0, -b.size.y * .12f, dist); cam.transform.LookAt(look);
            var big = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; var prev = cam.targetTexture; cam.targetTexture = big; cam.Render();
            var pa = RenderTexture.active; RenderTexture.active = big; var t2 = new Texture2D(W, H, TextureFormat.RGBA32, true); t2.ReadPixels(new Rect(0, 0, W, H), 0, 0); t2.Apply(true);
            RenderTexture.active = pa; cam.targetTexture = prev; big.Release(); Object.DestroyImmediate(inst); return t2;
        }
        static Texture2D StageShot(int i)
        {
            var G = Main.G; G.SetTheme(i);
            cam.cullingMask = ~(1 << Layer); cam.clearFlags = CameraClearFlags.Skybox; cam.fieldOfView = 42;
            // a low, wide establishing shot: the arena in front, the stage's skyline and sky behind (the 360° stages show their photo)
            cam.fieldOfView = 50; cam.transform.position = new Vector3(0, 4.2f, 18); cam.transform.LookAt(new Vector3(0, 2f, -6)); cam.farClipPlane = 220;
            const int W = 768, H = 384;
            var big = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; cam.targetTexture = big; cam.Render();
            var pa = RenderTexture.active; RenderTexture.active = big; var t = new Texture2D(W, H, TextureFormat.RGBA32, true); t.ReadPixels(new Rect(0, 0, W, H), 0, 0); t.Apply(true);
            RenderTexture.active = pa; cam.targetTexture = rt; big.Release(); cam.fieldOfView = 30; return t;
        }
        static Texture2D Grab()
        {
            cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(Size, Size, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); t.Apply(); RenderTexture.active = prev; return t;
        }
    }
}
