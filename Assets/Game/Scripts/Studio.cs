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

        // saved pictures (Resources/UI/Cards, made with -bakecards) load when a card shows them; pictures rendered this run are used when there is no file
        static Texture2D Card(string key) => tex.TryGetValue(key, out var t) ? t : Resources.Load<Texture2D>("UI/Cards/" + key);
        public static IEnumerable<KeyValuePair<string, Texture2D>> Rendered => tex.Where(kv => !kv.Key.StartsWith("stage_") && kv.Value);
        public static Texture2D Hero(string id) => Card("hero_" + id);
        public static Texture2D HeroFull(string id) => Card("herofull_" + id);   // full-body card art
        public static Texture2D Wing(string id) => Card("wing_" + id);
        public static Texture2D Pet(string id) => Card("pet_" + id);
        public static Texture2D Weapon(string id) => Card("w_" + id);
        public static Texture2D Stage(int i) => tex.TryGetValue("stage_" + i, out var t) ? t : null;

        public static string Timing = "";   // how long the start-up pictures took, by kind
        public static void Build()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew(); float Lap() { float ms = (float)sw.Elapsed.TotalMilliseconds; sw.Restart(); return ms; }
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
            var cmd = System.Environment.GetCommandLineArgs();
            bool baked = System.Array.IndexOf(cmd, "-bakecards") < 0 && Data.Heroes.All(h => Resources.Load<Texture2D>("UI/Cards/herofull_" + h.id)) && Data.AllSkinModels.All(m => Resources.Load<Texture2D>("UI/Cards/herofull_" + m));   // nothing to render: start-up skips loading every model
            if (!baked) foreach (var h in Data.Heroes)
            {
                var pf = h.rig ? Resources.Load<GameObject>("Heroes/Hero_" + h.id) : Catalog.I.Model(h.id);
                tex["hero_" + h.id] = h.rig ? Portrait(pf) : Shoot(pf, .5f, 1.1f, .35f, true);
                tex["herofull_" + h.id] = FullBody(pf, h.rig, h.female);
                foreach (var (_, m) in Data.ModelSkins(h.id)) if (SkinPacks.Get(m) is GameObject spf) { tex["hero_" + m] = Portrait(spf); tex["herofull_" + m] = FullBody(spf, true, h.female); }   // model skins' cards
            }
            float tHeroes = Lap();
            if (!baked) foreach (var p in Data.Pets) tex["pet_" + p.id] = Shoot(Catalog.I.Model(p.id), .48f, 1.15f, .45f);
            if (!baked) foreach (var w in Data.Wings) tex["wing_" + w.id] = Shoot(Catalog.I.Model("wing_" + w.id), .45f, 1.1f, Mathf.PI);   // seen from behind
            if (!baked) foreach (var w in Data.Swords) tex["w_" + w.id] = Shoot(Catalog.I.Model(w.id), .5f, 1.0f, 0, weapon: true);
            if (!baked) foreach (var w in Data.Shields) tex["w_" + w.id] = Shoot(Catalog.I.Model(w.id), .5f, 1.0f, 0, weapon: true);
            Object.Destroy(key.gameObject); Object.Destroy(rim.gameObject); Object.Destroy(fill.gameObject);
            (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientProbe) = amb;
            float tItems = Lap();
            // stage cards are saved pictures (Resources/UI/Stages, made with -stageshots): rendering them takes 14 s in a browser. A stage with no file yet is rendered.
            bool fresh = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-stageshots") >= 0;
            for (int i = 0; i < Data.Themes.Length; i++) { var saved = fresh ? null : Resources.Load<Texture2D>("UI/Stages/stage_" + i); tex["stage_" + i] = saved ? saved : StageShot(i); }
            Object.Destroy(go); rt.Release();
            Resources.UnloadUnusedAssets();   // every hero's model and skins were only needed for the pictures: give that memory back (phones crash without it)
            Timing = $"studio heroes {tHeroes:0} ms, pets wings weapons {tItems:0} ms, stages {Lap():0} ms"; Debug.Log("[Hitch] " + Timing);
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
        static Texture2D FullBody(GameObject pf, bool rig, bool female)
        {
            if (!pf) return null; int W = Main.IsPhone ? 320 : 512, H = Main.IsPhone ? 480 : 768;   // sharp enough for the big cards of the hero pick
            var inst = Object.Instantiate(pf, Spot, Quaternion.identity);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var an = inst.GetComponentInChildren<Animator>(); if (an) an.enabled = false; var hang = SleeveHang.Attach(inst); var grip = Grip.Attach(inst);
            var rs = inst.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            // a model built in T-pose (much wider than a standing figure) takes its lobby stance for the card; the framing still comes from its height
            bool tPose = an && b.size.x > b.size.y * .45f;   // T-pose or the A-pose of the 2026-11 heroes (arms out at an angle)
            if (tPose) { an.enabled = true; if (Resources.Load<RuntimeAnimatorController>("Anim/warrior" + (female ? "_f" : "_m")) is RuntimeAnimatorController stance) an.runtimeAnimatorController = stance; an.Rebind(); an.Play("show", 0, 0); an.Update(0); an.enabled = false; if (hang) hang.Apply(); if (grip) grip.Apply(); }   // the plain standing pose, not a posed one with crossed legs
            if (an && an.isHuman && an.GetBoneTransform(HumanBodyBones.LeftUpperArm) is Transform la && an.GetBoneTransform(HumanBodyBones.RightUpperArm) is Transform ra)
            {   // every card faces straight ahead: undo whatever turn the stance has, going by the shoulders
                var fwd = Vector3.Cross(ra.position - la.position, Vector3.up); if (fwd.sqrMagnitude > .001f) inst.transform.Rotate(0, -Vector3.SignedAngle(Vector3.forward, fwd, Vector3.up), 0, Space.World);
            }
            float hgt = tPose ? b.size.y : Mathf.Max(b.size.y, b.size.x * H / W * .8f), fov = cam.fieldOfView;
            float dist = hgt * 1.1f * .5f / Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            var look = new Vector3(b.center.x, b.min.y + b.size.y * .5f, b.center.z);
            cam.transform.position = look + new Vector3(0, -b.size.y * .12f, dist); cam.transform.LookAt(look);
            var big = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; var prev = cam.targetTexture; cam.targetTexture = big; cam.Render();
            var pa = RenderTexture.active; RenderTexture.active = big; var t2 = new Texture2D(W, H, TextureFormat.RGBA32, true); t2.ReadPixels(new Rect(0, 0, W, H), 0, 0); t2.Apply(true);
            RenderTexture.active = pa; cam.targetTexture = prev; big.Release(); Object.DestroyImmediate(inst); return t2;
        }
        // the VS screen: a hero in a fighting stance (a wind-up from its own class moves), turned toward the other side by 'yaw' degrees
        // the weapon a hero shows on the VS screen: the player's own blade when it suits, else one of its class's
        static string VsWeapon(HeroDef h)
        {
            var mine = Main.G && Main.G.hero && Main.G.hero.def == h ? Main.G.hero.ModelId : null;
            if (mine != null && Data.Swords.FirstOrDefault(w => w.id == mine) is SwordDef md && System.Array.IndexOf(Data.Affinity(h.cls), md.type) >= 0) return mine;
            return h.id switch
            {
                "samurai" => "w_katana", "ninja" => "w_kunai", "hood" => "w_daggers", "knight" => "w_greataxe", "tiger" => "w_claws", "darkmage" => "w_orb",
                _ => h.cls switch { "warrior" => "w_lava", "assassin" => "w_katana", "archer" => "w_longbow", "mage" => "w_arcanestaff", "cleric" => "w_holystaff", "monk" => "w_gauntlet", _ => null },
            };
        }
        static Quaternion Basis(Vector3 x, Vector3 y) { x.Normalize(); y = (y - x * Vector3.Dot(x, y)).normalized; return Quaternion.LookRotation(Vector3.Cross(x, y), y); }
        static void WeaponFor(GameObject inst, Animator an, HeroDef h)
        {
            string id = VsWeapon(h); if (id == null || h.ownWeapon) return; var md = Data.Swords.FirstOrDefault(w => w.id == id); var pf = Catalog.I.Model(id); if (md == null || !pf) return;
            Transform rh = an ? an.GetBoneTransform(HumanBodyBones.RightHand) : null, lh = an ? an.GetBoneTransform(HumanBodyBones.LeftHand) : null, rf = an ? an.GetBoneTransform(HumanBodyBones.RightLowerArm) : null, lf = an ? an.GetBoneTransform(HumanBodyBones.LeftLowerArm) : null;
            GameObject Put(Transform parent, Vector3 p, Quaternion r)
            {
                var g = Object.Instantiate(pf, parent, false); g.transform.SetPositionAndRotation(p, r);
                foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer; return g;
            }
            if (md.type is "bow" or "crossbow" or "fist" or "claw")
            {
                if (!rh || !lh || !rf || !lf) return;
                Vector3 right = (rh.position - lh.position).normalized, F = Vector3.Cross(right, Vector3.up).normalized, aR = (rh.position - rf.position).normalized, aL = (lh.position - lf.position).normalized;
                if (md.type == "bow") { if (!h.ownBow) Put(lh, lh.position + aL * .07f, Basis(F * .5f + aL * .87f, -aL)); }
                else if (md.type == "crossbow") Put(rh, rh.position + aR * .07f, Basis(aR, F));
                else { var g = Put(rh, rh.position + aR * .03f, Basis(aR, F)); var g2 = Put(lh, lh.position + aL * .03f, Basis(aL, F)); g2.transform.localScale = new Vector3(1, 1, -1); }
                return;
            }
            var socket = inst.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "SwordSocket"); if (!socket) return;
            var sw = Object.Instantiate(pf, socket, false); sw.transform.localPosition = Vector3.zero; sw.transform.localRotation = Quaternion.identity; sw.transform.localScale = Vector3.one * (id == "w_daggers" ? 1.15f : VsWeaponScale);   // a touch smaller so staffs and blades don't jut out of the picture
            foreach (var t in sw.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
        }
        public static List<Texture2D> VsPoses(List<(HeroDef h, float yaw, string model)> who)
        {   // its own little studio (the start-up one is gone by now): a cool key, a hot fire light from below and behind, a red rim
            var go = new GameObject("VsCam"); var c = go.AddComponent<Camera>(); c.enabled = false; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0, 0, 0, 0);
            c.cullingMask = 1 << Layer; c.fieldOfView = 30; c.nearClipPlane = .05f; c.farClipPlane = 50;
            Light Lt(Color col, float i, Vector3 e) { var l = new GameObject("VsLight").AddComponent<Light>(); l.transform.SetParent(go.transform); l.type = LightType.Directional; l.color = col; l.intensity = i; l.cullingMask = 1 << Layer; l.transform.rotation = Quaternion.Euler(e); return l; }
            var key = Lt(new Color(1f, 1f, .98f), 1.55f, new Vector3(30, 200, 0)); var under = Lt(new Color(.85f, .9f, 1f), .2f, new Vector3(-25, 160, 0)); var rimL = Lt(new Color(1f, .3f, .2f), 2.2f, new Vector3(15, 20, 0));   // daylight: a bright key, sky bounce from below, a strong rim in the side's colour
            var amb = (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientProbe);
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(new Color(.1f, .1f, .13f)); RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.3f, .3f, .31f);   // low ambient: the pale, washed-out look came from too much fill RenderSettings.ambientProbe = sh;
            // the scene's sun would be the "main" light and the key only an extra one; the phones' render settings drop much of the extra
            // lights, and the heroes came out dark there: the scene's suns off for the shot, the key made the main light
            var suns = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l => l.enabled && l.type == LightType.Directional && l.transform.parent != go.transform).ToList();
            var sun0 = RenderSettings.sun; foreach (var l in suns) l.enabled = false; RenderSettings.sun = key;
            if (Main.IsPhone || PhoneLook) { RenderSettings.ambientLight = new Color(.34f, .35f, .38f); key.intensity = 1.7f; }   // in case the extra lights are lost all the same
            float baseKey = -1; var outl = new List<Texture2D>(); foreach (var (h, yaw, mdl) in who)
            {   // each side in its own colour: ours edged in icy blue, theirs in fire and violet
                bool ours = yaw < 0; if (baseKey < 0) baseKey = key.intensity; key.intensity = baseKey * (ours ? 1.25f : 1); under.intensity = ours ? .35f : .2f;   // our side brighter and fairer skinned (owner)
                rimL.color = ours ? new Color(.62f, .82f, 1f) : new Color(1f, .74f, .58f); rimL.intensity = 1.1f;   // a thin edge of the side's colour from behind; the faces and clothes keep their own colours (owner's reference: natural, balanced)
                under.color = new Color(.85f, .9f, 1f);   // the same sky bounce for both sides: the warm one turned theirs too red
                var pic = Pose(c, h, yaw, null, 0, mdl); if (!pic) Debug.Log($"[Vs] no picture for {h.id} ({mdl})"); outl.Add(pic);
            }
            foreach (var l in suns) if (l) l.enabled = true; RenderSettings.sun = sun0;
            (RenderSettings.ambientMode, RenderSettings.ambientLight, RenderSettings.ambientProbe) = amb; Object.Destroy(go); return outl;
        }
        // test (-posesheet <dir>): every candidate stance for one hero of each class, to pick the VS poses by eye
        public static void PoseSheet(string dir)
        {
            System.IO.Directory.CreateDirectory(dir);
            var go = new GameObject("VsCam"); var c = go.AddComponent<Camera>(); c.enabled = false; c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(.15f, .15f, .2f, 1);
            c.cullingMask = 1 << Layer; c.fieldOfView = 30; c.nearClipPlane = .05f; c.farClipPlane = 50;
            var l = new GameObject("VsLight").AddComponent<Light>(); l.transform.SetParent(go.transform); l.type = LightType.Directional; l.intensity = 1.4f; l.cullingMask = 1 << Layer; l.transform.rotation = Quaternion.Euler(30, 200, 0);
            var states = new (string, float)[] { ("idle", .2f), ("guard", .3f), ("blockidle", .3f), ("heavy", .25f), ("heavy", .45f), ("atk0", .3f), ("atk1", .3f), ("counter", .3f), ("spin", .2f), ("show", .5f), ("cast", .3f), ("cast2", .35f), ("bowdraw", .6f), ("jumpatk", .3f), ("kick", .3f) };
            foreach (var id in PoseOnly != null ? new[] { PoseOnly } : new[] { "knight", "warrior", "ninja", "hood", "lyra", "elf", "mage", "darkmage", "sunpriest", "flamepriest", "tiger", "spartan", "samurai", "monk" })
            {
                var h = Data.Heroes.FirstOrDefault(x => x.id == id); if (h == null) continue;
                foreach (var (st, at) in states) { var t = Pose(c, h, -30, st, at); if (!t) continue; System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"{id}_{st}_{at * 100:0}_s{Grip.Sign:0}.png"), t.EncodeToPNG()); Object.Destroy(t); }
            }
            Object.Destroy(go);
        }
        // a hero in a fighting stance (the heavy swing's wind-up, or a guard / a cast), turned toward the other side by 'yaw' degrees
        static Texture2D Pose(Camera c, HeroDef h, float yaw, string forceSt = null, float forceAt = 0, string model = null)
        {
            if (h == null || !h.rig) return null; var pf = SkinPacks.Get(model ?? h.id) ?? Resources.Load<GameObject>("Heroes/Hero_" + h.id); if (!pf) return null;
            int W = Main.IsPhone || PhoneLook ? 540 : 720, H = Main.IsPhone || PhoneLook ? 720 : 960;   // the VS shows them up to ~750 px tall: 432 x 576 looked soft when stretched
            var inst = Object.Instantiate(pf, Spot, Quaternion.identity); SkinPacks.Animate(inst, h.id);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
            var an = inst.GetComponentInChildren<Animator>(); var hang = SleeveHang.Attach(inst); var grip = Grip.Attach(inst);
            string set = Data.AnimSet(h.id); if (set != null) set += h.female ? "_f" : "_m";
            if (an && set != null && Resources.Load<RuntimeAnimatorController>("Anim/" + set) is RuntimeAnimatorController oc) an.runtimeAnimatorController = oc;
            WeaponFor(inst, an, h);   // the class's weapon in hand (attached in the rest pose, like Hero.EquipSword)
            if (an)
            {   // every hero stands with their weapon
                an.enabled = true; an.Rebind();
                var (st, at) = set == null ? ("idle", .2f) : ("show", .5f);   // heroes on the base clips (Garland) reach a hand out in "show": they stand at ease instead
                  // standing tall with the class weapon in hand (picked from -posesheet; the owner wanted a calm stand, not a swing)
                if (forceSt != null) (st, at) = (forceSt, forceAt);
                if (!an.HasState(0, Animator.StringToHash(st))) (st, at) = ("idle", 0f);
                an.Play(st, 0, at); an.Update(0); an.enabled = false; if (hang) hang.Apply(); if (grip) grip.Apply();   // the hands closed round the weapon
            }
            if (an && an.isHuman && an.GetBoneTransform(HumanBodyBones.LeftUpperArm) is Transform la && an.GetBoneTransform(HumanBodyBones.RightUpperArm) is Transform ra)
            {   // face the camera first (going by the shoulders), then turn toward the other side
                var fwd = Vector3.Cross(ra.position - la.position, Vector3.up); if (fwd.sqrMagnitude > .001f) inst.transform.Rotate(0, -Vector3.SignedAngle(Vector3.forward, fwd, Vector3.up), 0, Space.World);
            }
            inst.transform.Rotate(0, yaw, 0, Space.World);
            var rs = inst.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float hgt = Mathf.Max(b.size.y, b.size.x * H / W * .8f); float dist = hgt * 1.08f * .5f / Mathf.Tan(c.fieldOfView * .5f * Mathf.Deg2Rad);
            var look = new Vector3(b.center.x, b.min.y + b.size.y * .5f, b.center.z);
            c.transform.position = look + new Vector3(0, -b.size.y * .1f, dist); c.transform.LookAt(look);
            var big = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 }; c.targetTexture = big; c.Render();
            var pa = RenderTexture.active; RenderTexture.active = big; var t2 = new Texture2D(W, H, TextureFormat.RGBA32, false); t2.ReadPixels(new Rect(0, 0, W, H), 0, 0); Grade(t2); t2.Apply();
            RenderTexture.active = pa; c.targetTexture = null; big.Release(); Object.DestroyImmediate(inst); return t2;
        }
        public static float VsWeaponScale = .82f; public static bool PhoneLook; public static string PoseOnly;   // tests: -posehero <id>   // PhoneLook: tests on Windows with the phones' render settings
        static void Grade(Texture2D t)
        {   // a little more saturation and contrast, so the heroes don't come out pale against the night
            var px = t.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i]; if (p.a == 0) continue; float a = p.a / 255f, r = p.r / 255f / a, g = p.g / 255f / a, b = p.b / 255f / a;
                float l = r * .3f + g * .59f + b * .11f; r = l + (r - l) * 1.12f; g = l + (g - l) * 1.12f; b = l + (b - l) * 1.12f;
                r = (r - .42f) * 1.15f + .4f; g = (g - .42f) * 1.15f + .4f; b = (b - .42f) * 1.15f + .4f;
                px[i] = new Color32((byte)(Mathf.Clamp01(r) * a * 255), (byte)(Mathf.Clamp01(g) * a * 255), (byte)(Mathf.Clamp01(b) * a * 255), p.a);
            }
            t.SetPixels32(px);
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
