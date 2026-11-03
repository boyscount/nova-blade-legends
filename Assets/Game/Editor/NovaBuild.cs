// Builds everything the game needs from the imported assets (run after NovaSetup.All):
//   Unity.exe -batchmode -quit -projectPath . -executeMethod NovaBuild.All
// Assets(): panel settings, materials, Catalog (models, effects, clip timings), enemy import settings
// Scene(): Assets/Game/Scenes/Main.unity with camera + post-processing and the three stage arenas
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaBlade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

public static class NovaBuild
{
    const string Gen = "Assets/Game/Generated";
    static System.Random rnd = new(11);
    static float R(float a, float b) => a + (float)rnd.NextDouble() * (b - a);

    [MenuItem("Nova Blade/Rebuild Everything (assets + scene)")] public static void All() { Assets(); Scene(); Debug.Log("[NovaBuild] done"); }
    [MenuItem("Nova Blade/Rebuild Scene Only")] static void SceneMenu() { Scene(); Debug.Log("[NovaBuild] scene rebuilt"); }
    // scene + both players in one batch run: -executeMethod NovaBuild.SceneAndPlayers
    public static void SceneAndPlayers() { Scene(); BuildWin(); BuildWebGL(); }

    // Windows player for local testing: Builds/Win/NovaBlade.exe (run with -novashot <dir> for automatic screenshots)
    // Web build for phones and desktop browsers: Builds/WebGL (ASTC textures for phones, full-screen template)
    [MenuItem("Nova Blade/Build WebGL (phones)")]
    // web build only: scenery textures at 512 px and character/other textures at most 1024 (WebGL platform override),
    // compressed meshes for the Fantasy Kingdom set; keeps each file of the web build under GitHub Pages' 100 MB limit
    static void WebSlim()
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); if (path.Contains("/Resources/UI/") || path.Contains("NovaBlade_SkillIcons")) continue;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            bool scenery = path.Contains("/Models/Nature/") || path.StartsWith("Assets/Art/") || path.Contains("Polytope") || path.Contains("/ThirdParty/") || path.Contains("/Textures/PBR/") || path.Contains("/Models/Props/");
            int want = scenery ? 512 : 1024; var ps = ti.GetPlatformTextureSettings("WebGL");
            if (ps.overridden && ps.maxTextureSize <= want) continue;
            ps.overridden = true; ps.maxTextureSize = want; ps.format = TextureImporterFormat.Automatic; ps.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(ps); ti.SaveAndReimport(); n++;
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art" }))
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is ModelImporter mi && mi.meshCompression != ModelImporterMeshCompression.High) { mi.meshCompression = ModelImporterMeshCompression.High; mi.SaveAndReimport(); n++; }
        Debug.Log($"[NovaBuild] web slim: {n} assets adjusted");
    }

    public static void BuildWebGL()
    {
        WebSlim(); Towers(Cat(), false);
        PlayerSettings.WebGL.template = "PROJECT:NovaBlade";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli; PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true; PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric; PlayerSettings.WebGL.maximumMemorySize = 2048;
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        EditorUserBuildSettings.webGLBuildSubtarget = WebGLTextureSubtarget.ASTC;
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Game/Scenes/Main.unity" }, "Builds/WebGL", BuildTarget.WebGL, BuildOptions.None);
        Debug.Log($"[NovaBuild] webgl build {r.summary.result} {r.summary.totalSize / 1e6:0} MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        SplitData("Builds/WebGL");
    }

    // GitHub Pages refuses files over 100 MB: cut the data file into 90 MB parts (<name>.part0, .part1, ...);
    // index.html fetches the parts, joins them into one blob and hands that to the Unity loader
    static void SplitData(string dir)
    {
        const int Part = 90 * 1024 * 1024;
        var data = Directory.GetFiles(dir + "/Build", "*.data*").FirstOrDefault(f => !f.Contains(".part"));
        if (data == null || new FileInfo(data).Length <= Part) return;
        var bytes = File.ReadAllBytes(data); int n = (bytes.Length + Part - 1) / Part;
        for (int i = 0; i < n; i++) using (var fs = File.Create($"{data}.part{i}")) fs.Write(bytes, i * Part, Mathf.Min(Part, bytes.Length - i * Part));
        File.Delete(data);
        var html = File.ReadAllText(dir + "/index.html"); File.WriteAllText(dir + "/index.html", html.Replace("var dataParts = 0;", $"var dataParts = {n};"));
        Debug.Log($"[NovaBuild] data split into {n} parts ({bytes.Length / 1e6:0} MB)");
    }

    // lane towers: present set (team 0) and future set (team 1), mobile low-poly or the reduced PC models
    static readonly (string id, string file)[] TowerFiles = { ("tower_p1", "P1_WaterTowerCannon"), ("tower_p2", "P2_LionFountainFortress"), ("tower_p3", "P3_HydroDamFortress"),
        ("tower_f1", "F1_PlasmaSpringPylon"), ("tower_f2", "F2_AntiGravWaterfallSpire"), ("tower_f3", "F3_QuantumGeyserCitadel") };
    static void Towers(Catalog cat, bool pc)
    {
        cat.models.RemoveAll(e => e.id.StartsWith("tower_"));
        foreach (var (id, f) in TowerFiles)
        {
            var path = pc ? $"Assets/Game/Tower/PC/{f}_PC_LOD.glb" : $"Assets/Game/Tower/Mobile/{f}.glb";
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Game/Tower/Mobile/{f}.glb");
            if (!go) Debug.LogError("[NovaBuild] missing tower " + path); cat.models.Add(new Catalog.Entry { id = id, prefab = go });
        }
        EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets();
    }
    static Catalog Cat() => AssetDatabase.LoadAssetAtPath<Catalog>("Assets/Game/Resources/Catalog.asset");

    [MenuItem("Nova Blade/Build Windows exe")]
    public static void BuildWin()
    {
        Towers(Cat(), false);   // the decimated PC towers lose their UVs (shattered look), so every build uses the hand-made mobile set
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Game/Scenes/Main.unity" }, "Builds/Win/NovaBlade.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[NovaBuild] win build {r.summary.result} {r.summary.totalSize / 1e6:0} MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    // ======================= assets =======================
    public static void Assets()
    {
        Directory.CreateDirectory(Gen);
        // animated Quaternius enemies: legacy Animation component so clips can be played by name
        foreach (var guid in AssetDatabase.FindAssets("", new[] { "Assets/Game/Models/Foes" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); if (!path.EndsWith(".glb") && !path.EndsWith(".gltf")) continue;
            var imp = AssetImporter.GetAtPath(path); var so = new SerializedObject(imp); bool changed = false;
            var it = so.GetIterator();
            while (it.Next(true)) if (it.propertyType == SerializedPropertyType.Enum && it.name.ToLower().Contains("animationmethod")) { var names = it.enumNames; int li = System.Array.FindIndex(names, n => n.ToLower().Contains("legacy")); if (li >= 0 && it.enumValueIndex != li) { it.enumValueIndex = li; changed = true; } }
            if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); imp.SaveAndReimport(); }
        }
        // textures split out of the models: at most 1024 px (they compress to ASTC on phones / DXT on PC)
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Models" }))
            if (AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) is TextureImporter mt && (mt.maxTextureSize > 1024 || mt.textureCompression == TextureImporterCompression.Uncompressed))
            { mt.maxTextureSize = 1024; mt.textureCompression = TextureImporterCompression.Compressed; mt.SaveAndReimport(); }
        // normal maps
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Textures/PBR" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool nrm = path.Contains("_nor_"); if (nrm && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            if (path.Contains("_Rough") && ti.sRGBTexture) { ti.sRGBTexture = false; ti.SaveAndReimport(); }
        }
        // water (CozyFishing/Water) refracts the opaque texture and fades with the depth texture
        foreach (var rp in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
        { var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(rp); if (a) { a.supportsCameraDepthTexture = true; a.supportsCameraOpaqueTexture = true; EditorUtility.SetDirty(a); } }
        // UI panel
        Directory.CreateDirectory("Assets/Game/Resources/UI");
        const string tss = "Assets/Game/Resources/UI/Theme.tss";
        if (!File.Exists(tss)) { File.WriteAllText(tss, "@import url(\"unity-theme://default\");\n"); AssetDatabase.ImportAsset(tss); }
        var ps = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Game/Resources/UI/Panel.asset");
        if (!ps) { ps = ScriptableObject.CreateInstance<PanelSettings>(); AssetDatabase.CreateAsset(ps, "Assets/Game/Resources/UI/Panel.asset"); }
        ps.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tss); ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080); ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight; ps.match = 1f; EditorUtility.SetDirty(ps);
        // materials
        var glow = Mat($"{Gen}/Glow.mat", "NovaBlade/Glow", m => { m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 1); });
        var grass = Mat($"{Gen}/Grass.mat", "NovaBlade/Grass", m => { m.shader = Shader.Find("NovaBlade/Grass"); } ); Mat($"{Gen}/Grass.mat", "NovaBlade/Grass", m => { m.enableInstancing = true; m.SetFloat("_Bend", .14f); m.SetFloat("_Variation", .3f); });
        // catalog
        var cat = AssetDatabase.LoadAssetAtPath<Catalog>("Assets/Game/Resources/Catalog.asset");
        if (!cat) { cat = ScriptableObject.CreateInstance<Catalog>(); AssetDatabase.CreateAsset(cat, "Assets/Game/Resources/Catalog.asset"); }
        cat.models.Clear();
        void M(string id, string path) { var go = Model(path); if (!go) Debug.LogError("[NovaBuild] missing " + path); cat.models.Add(new Catalog.Entry { id = id, prefab = go }); }
        M("toybot", "Assets/Game/Models/Heroes/toybot.glb");
        foreach (var p in Data.Pets) M(p.id, $"Assets/Game/Models/Pets/{p.id}.glb");
        foreach (var b in new[] { "reaper", "captain" }) M(b, $"Assets/Game/Models/Bosses/{b}.glb");
        foreach (var w in Data.Swords) M(w.id, $"Assets/Game/Models/Weapons/{w.id}.glb");
        foreach (var f in Data.Foes.Where(f => f.model.StartsWith("q_"))) M(f.model, $"Assets/Game/Models/Foes/{f.model}.glb");
        M("q_dragon", "Assets/Game/Models/Foes/q_dragon.glb");
        M("column", "Assets/ThirdParty/PrisonCell/Prefabs/Walls/Column.prefab"); Towers(cat, false); M("fire_pit", "Assets/Game/Models/Props/stone_fire_pit/stone_fire_pit_1k.gltf");
        cat.fx.Clear();
        const string F = "Assets/ThirdParty/CartoonFX/CFXR Prefabs/";
        foreach (var (id, p) in new[] {
            ("hit_red", "Impacts/CFXR Hit A (Red)"), ("hit_yellow", "Impacts/CFXR Hit D 3D (Yellow)"), ("ground_hit", "Impacts/CFXR2 Ground Hit"),
            ("death", "Eerie/CFXR2 WW Enemy Explosion"), ("boss_death", "Explosions/CFXR2 WW Explosion"), ("poof", "Misc/CFXR Magic Poof"),
            ("aura", "Magic Misc/CFXR3 Magic Aura A (Runic)"), ("electric", "Electric/CFXR Electrified 3"), ("explosion", "Explosions/CFXR3 Fire Explosion B"),
            ("fire_small", "Fire/CFXR Fire"), ("souls", "Eerie/CFXR2 Souls Escape"), ("wind", "Nature/CFXR4 Wind Trails"), ("glows", "Misc/CFXR3 Ambient Glows"),
            ("trail_fire", "Sword Trails/Fire/CFXR4 Sword Trail FIRE (360 Spiral)"), ("trail_ice", "Sword Trails/Ice/CFXR4 Sword Trail ICE (360 Spiral)"),
            ("trail_plain", "Sword Trails/Plain/CFXR4 Sword Trail PLAIN (360 Spiral)") })
        { var go = AssetDatabase.LoadAssetAtPath<GameObject>(F + p + ".prefab"); if (!go) Debug.LogError("[NovaBuild] missing fx " + p); cat.fx.Add(new Catalog.Entry { id = id, prefab = go }); }
        // Vefects trails (lobby showcase rings) and Hovl magic effects (skills), by full path
        const string V = "Assets/Vefects/Trails VFX URP/VFX/Particles/", HV = "Assets/Hovl Studio/Magic effects pack/Prefabs/";
        foreach (var (id, p) in new[] {
            ("vx_fire", V + "VFX_Trail_Fire"), ("vx_void", V + "VFX_Trail_Void"), ("vx_cosmos", V + "VFX_Trail_Cosmos"), ("vx_nature", V + "VFX_Trail_Nature"),
            ("vx_ice", V + "VFX_Trail_Ice"), ("vx_electric", V + "VFX_Trail_Electric"), ("vx_water", V + "VFX_Trail_Water"), ("vx_dark", V + "VFX_Trail_Dark"),
            ("mx_heal_circle", HV + "Magic circles/Healing circle"), ("mx_healing", HV + "Character auras/Healing"), ("mx_buff", HV + "Character auras/Buff"),
            ("mx_shield", HV + "Magic shields/Magic shield blue"), ("mx_shield_gold", HV + "Magic shields/Magic shield yellow"), ("mx_ground", HV + "AoE effects/Ground AOE explosion"),
            ("mx_holy_hit", HV + "Hits and explosions/Holy hit"), ("mx_electro_hit", HV + "Hits and explosions/Electro hit"), ("mx_magic_circle", HV + "Magic circles/Magic circle") })
        { var go = AssetDatabase.LoadAssetAtPath<GameObject>(p + ".prefab"); if (!go) Debug.LogError("[NovaBuild] missing fx " + p); else cat.fx.Add(new Catalog.Entry { id = id, prefab = go }); }
        cat.heroController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Game/Animations/Hero.controller");
        cat.glowMat = glow; cat.grassMat = grass;
        cat.clips = ClipTimings();
        EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets();
        Debug.Log($"[NovaBuild] catalog {cat.models.Count} models, {cat.fx.Count} fx, {cat.clips.Count} clips");
    }

    static Material Mat(string path, string shader, System.Action<Material> init)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
        init(m); EditorUtility.SetDirty(m); return m;
    }

    // clip lengths, and the strike moment = fastest right-hand frame (sampled on the warrior through the Humanoid avatar)
    static List<Catalog.ClipInfo> ClipTimings()
    {
        var list = new List<Catalog.ClipInfo>();
        var pf = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/Heroes/Hero_warrior.prefab");
        var inst = (GameObject)Object.Instantiate(pf); var an = inst.GetComponentInChildren<Animator>(); var hand = an.GetBoneTransform(HumanBodyBones.RightHand);
        foreach (var k in NovaSetup.Clips.Keys)
        {
            var clip = NovaSetup.Clip(k); float best = 0, hit = clip.length * .5f; Vector3 prev = default;
            var g = PlayableGraph.Create(); var o = AnimationPlayableOutput.Create(g, "o", an); var cp = AnimationClipPlayable.Create(g, clip); o.SetSourcePlayable(cp);
            for (float t = 0; t <= clip.length; t += 1 / 30f)
            {
                cp.SetTime(t); g.Evaluate(); var p = hand.position;
                if (t > 0) { float sp = (p - prev).magnitude; if (sp > best) { best = sp; hit = t; } }
                prev = p;
            }
            g.Destroy();
            list.Add(new Catalog.ClipInfo { id = k, length = clip.length, hit = hit });
            Debug.Log($"[NovaBuild] clip {k} len {clip.length:0.00} hit {hit:0.00}");
        }
        var limbs = new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot }.Select(an.GetBoneTransform).ToArray();
        foreach (var (set, states) in NovaSetup.Sets) foreach (var (k, (file, _)) in states)
        {
            var clip = NovaSetup.SetClip(file); if (!clip) { Debug.LogError("[NovaBuild] missing set clip " + file); continue; }
            float best = 0, hit = clip.length * .5f; var prev = new Vector3[limbs.Length];
            var g = PlayableGraph.Create(); var o = AnimationPlayableOutput.Create(g, "o", an); var cp = AnimationClipPlayable.Create(g, clip); o.SetSourcePlayable(cp);
            for (float t = 0; t <= clip.length; t += 1 / 30f)
            {
                cp.SetTime(t); g.Evaluate();
                for (int i = 0; i < limbs.Length; i++) { var p = limbs[i].position; if (t > 0) { float sp = (p - prev[i]).magnitude; if (sp > best) { best = sp; hit = t; } } prev[i] = p; }
            }
            g.Destroy();
            list.Add(new Catalog.ClipInfo { id = set + ":" + k, length = clip.length, hit = hit });
            Debug.Log($"[NovaBuild] clip {set}:{k} len {clip.length:0.00} hit {hit:0.00}");
        }
        Object.DestroyImmediate(inst);
        return list;
    }

    // ======================= scene =======================
    static Transform root;
    public static void Scene()
    {
        rnd = new System.Random(11);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // camera + post
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" }; var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 40; cam.nearClipPlane = .3f; cam.farClipPlane = 160;
        var cd = camGo.AddComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        camGo.transform.position = new Vector3(0, 9, 14); camGo.transform.LookAt(Vector3.zero);
        var volGo = new GameObject("Post"); var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(.6f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.65f);
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(.15f); ca.contrast.Override(14); ca.saturation.Override(9);   // richer, demo-like colour
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(6); wb.tint.Override(2);
        var smh = prof.Add<ShadowsMidtonesHighlights>(true); smh.shadows.Override(new Vector4(.92f, .96f, 1.08f, 0)); smh.highlights.Override(new Vector4(1.05f, 1.02f, .95f, 0));
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(.22f); vg.smoothness.Override(.5f);
        var lgg = prof.Add<LiftGammaGain>(true); lgg.lift.Override(new Vector4(.98f, .99f, 1.04f, 0));
        var pp = $"{Gen}/Post.asset"; if (File.Exists(pp)) AssetDatabase.DeleteAsset(pp); AssetDatabase.CreateAsset(prof, pp);
        foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
        vol.sharedProfile = prof;
        // game
        var mainGo = new GameObject("Game"); var main = mainGo.AddComponent<Main>(); main.cam = cam;
        main.stageRoots = new[] { Forest(), RiverStage(), KingdomSquare(), ValleyStage(), Castle(), LaneStage(), Lane3Stage() };   // Data.Themes order, then the 1-lane and 3-lane maps   // Data.Themes order, lane last   // story themes in Data.Themes order, lane last
        for (int i = 1; i < main.stageRoots.Length; i++) main.stageRoots[i].gameObject.SetActive(false);
        LobbySet(mainGo.transform);
        main.stageRoots[0].GetComponent<StageLook>().Apply();
        Directory.CreateDirectory("Assets/Game/Scenes");
        EditorSceneManager.SaveScene(scene, "Assets/Game/Scenes/Main.unity");
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Game/Scenes/Main.unity", true) };
        AssetDatabase.SaveAssets();
    }

    // Lobby set pieces over the forest stage: a stone fire pit with logs, big rocks (one with a small fire on top, one with a
    // brass lantern). Empty "Glow_" / "Smoke_" markers tell LobbyNight where to add flickering light and rising smoke.
    static void LobbySet(Transform parent)
    {
        var prev = root; root = new GameObject("LobbySet").transform; root.SetParent(parent, false);
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        GameObject Sized(string model, Vector3 at, float height, float yaw)
        {   // place a model scaled to a given height, sitting on the ground at 'at'
            var go = Place(Load(N(model)), at, yaw, 1); if (!go) return null; float h = Bounds(go).size.y;
            go.transform.localScale *= height / Mathf.Max(h, .01f); var b = Bounds(go);
            go.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z); return go;
        }
        void Mark(string name, Vector3 p) { var m = new GameObject(name).transform; m.SetParent(root); m.position = p; }
        var fireFx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        void Fire(Vector3 p, float s)
        {
            var f = (GameObject)PrefabUtility.InstantiatePrefab(fireFx, root); f.transform.position = p; f.transform.localScale *= s;
            foreach (var ps in f.GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "Smoke") ps.gameObject.SetActive(false);   // our own softer smoke instead
        }
        // fire pit left of the hero (screen-left is world +X)
        var pit = new Vector3(3.1f, 0, -4.2f);
        for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2; Sized(i % 2 == 0 ? "boulder_01" : "namaqualand_boulder_03", pit + new Vector3(Mathf.Cos(a) * .55f, 0, Mathf.Sin(a) * .45f), R(.16f, .24f), R(0, 360)); }
        var bark = Lit("LobbyBark", "bark_brown_02", 1, .05f, new Color(.32f, .26f, .22f));
        var logs = Combine("LobbyLogs", (PrimitiveType.Cylinder, T(0, .07f, 0, .09f, .3f, .09f, 0, 30, 80)), (PrimitiveType.Cylinder, T(0, .09f, 0, .08f, .28f, .08f, 0, -50, 80)), (PrimitiveType.Cylinder, T(0, .11f, 0, .08f, .26f, .08f, 0, 100, 75)));
        MeshObj("logs", logs, bark, pit, 0, 1);
        Fire(pit + Vector3.up * .1f, .5f); Mark("Glow_pit", pit + Vector3.up * .5f); Mark("Smoke_pit", pit + Vector3.up * .55f);
        // big rocks framing the clearing; a small fire burns on top of one, a lantern sits on another
        var r1 = Sized("namaqualand_boulder_02", new Vector3(-2.4f, 0, -4.4f), 1.05f, 30); var top1 = r1 ? Bounds(r1).max.y : 1;
        Fire(new Vector3(-2.4f, top1 - .05f, -4.4f), .32f); Mark("Glow_rock", new Vector3(-2.4f, top1 + .3f, -4.4f)); Mark("Smoke_rock", new Vector3(-2.4f, top1 + .35f, -4.4f));
        var r2 = Sized("boulder_01", new Vector3(4.9f, 0, -6.2f), .95f, 110); var top2 = r2 ? Bounds(r2).max.y : 1;
        Sized("brass_diya_lantern", new Vector3(4.9f, top2 - .04f, -6.2f), .42f, 0); Mark("Glow_lantern", new Vector3(4.9f, top2 + .25f, -6.2f));
        Sized("rock_moss_set_01", new Vector3(-4.6f, 0, -8.5f), 1.4f, 200);
        Sized("namaqualand_boulder_03", new Vector3(1.4f, 0, -7.6f), .7f, 75);
        Sized("rock_moss_set_02", new Vector3(6.2f, 0, -2.2f), .8f, 10);
        root.gameObject.SetActive(false); root = prev;
    }

    // ---------- helpers ----------
    // stone ground under tiled floors, so any seam or missing tile shows stone instead of the void
    static void Underlay()
    {
        var plane = GameObject.CreatePrimitive(PrimitiveType.Plane); Object.DestroyImmediate(plane.GetComponent<Collider>());
        plane.name = "Underlay"; plane.transform.SetParent(root); plane.transform.position = new Vector3(0, -.04f, 0); plane.transform.localScale = new Vector3(10, 1, 12);
        plane.GetComponent<Renderer>().sharedMaterial = Lit("FloorUnder", "rock_face_04", 30, .1f, new Color(.42f, .42f, .46f));
    }
    // (the gameplay camera looks toward -Z, so suns shine toward -Z too: the camera sees lit faces, shadows fall away from it)
    // models were split from .glb into .gltf + image files (glb_split.py) so their textures compress; accept either
    public static GameObject Model(string p) => AssetDatabase.LoadAssetAtPath<GameObject>(p) ?? (p.EndsWith(".glb") ? AssetDatabase.LoadAssetAtPath<GameObject>(p.Substring(0, p.Length - 4) + ".gltf") : null);
    static GameObject Load(string p) { var g = Model(p); if (!g) Debug.LogError("[NovaBuild] missing " + p); return g; }
    static Bounds Bounds(GameObject go) { var rs = go.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b; }
    // place a prefab so its bounds sit on y (bottom) or hang from y (top), centred on (x,z)
    static GameObject Place(GameObject pf, Vector3 at, float yaw = 0, float scale = 1, bool top = false, Transform parent = null)
    {
        if (!pf) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent ? parent : root);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0) * go.transform.rotation; go.transform.localScale *= scale;
        var b = Bounds(go);
        go.transform.position += new Vector3(at.x - b.center.x, (top ? at.y - b.max.y : at.y - b.min.y), at.z - b.center.z);
        return go;
    }
    static Light PointLight(Vector3 p, Color c, float intensity, float range)
    {
        var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(root); l.transform.position = p; l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None; return l;
    }
    static Light Sun(Color c, float intensity, Vector3 euler)
    {
        var l = new GameObject("Sun").AddComponent<Light>(); l.transform.SetParent(root); l.type = LightType.Directional; l.color = c; l.intensity = intensity; l.transform.rotation = Quaternion.Euler(euler);
        l.shadows = LightShadows.Soft; l.shadowStrength = .85f; return l;
    }
    static Transform NewRoot(string name, Color fog, float dens, Color sky, Color eq, Color gnd)
    {
        root = new GameObject(name).transform; var look = root.gameObject.AddComponent<StageLook>();
        look.fog = fog; look.fogDensity = dens; look.skyAmb = sky; look.eqAmb = eq; look.groundAmb = gnd; return root;
    }
    static Material Lit(string name, string tex, float tile, float smooth = .15f, Color? tint = null)
    {
        var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        if (tex != null)
        {
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Textures/PBR/{tex}_Diffuse_1k.jpg"));
            var n = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Textures/PBR/{tex}_nor_gl_1k.jpg"); if (n) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
            m.SetTextureScale("_BaseMap", Vector2.one * tile);
        }
        m.SetColor("_BaseColor", tint ?? Color.white); m.SetFloat("_Smoothness", smooth); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }
    static Material GlowMat(string name, Color c)
    {
        var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("NovaBlade/Glow")); AssetDatabase.CreateAsset(m, path); }
        m.SetFloat("_SrcBlend", 1); m.SetFloat("_DstBlend", 0); m.SetFloat("_UseVC", 0); m.SetColor("_Color", c); m.renderQueue = 2000; EditorUtility.SetDirty(m); return m;
    }
    // combine primitive pieces into one saved mesh
    static Mesh Combine(string name, params (PrimitiveType p, Matrix4x4 m)[] parts)
    {
        var cis = new List<CombineInstance>();
        foreach (var (p, m) in parts) { var g = GameObject.CreatePrimitive(p); cis.Add(new CombineInstance { mesh = g.GetComponent<MeshFilter>().sharedMesh, transform = m }); Object.DestroyImmediate(g); }
        var mesh = new Mesh { name = name }; mesh.CombineMeshes(cis.ToArray(), true, true); mesh.RecalculateBounds();
        var path = $"{Gen}/{name}.asset"; if (File.Exists(path)) AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path); return mesh;
    }
    static GameObject MeshObj(string name, Mesh mesh, Material mat, Vector3 p, float yaw, float s, Transform parent = null)
    {
        var go = new GameObject(name); go.transform.SetParent(parent ? parent : root); go.transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0)); go.transform.localScale = Vector3.one * s;
        go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go;
    }
    static Matrix4x4 T(float x, float y, float z, float sx, float sy, float sz, float rx = 0, float ry = 0, float rz = 0) => Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(rx, ry, rz), new Vector3(sx, sy, sz));
    static List<Vector2> Ring(int n, float r0, float r1, float avoidPath = 0, float jitter = .03f)
    {
        var o = new List<Vector2>();
        for (int i = 0; i < n * 4 && o.Count < n; i++) { float a = (o.Count / (float)n + R(-jitter, jitter)) * Mathf.PI * 2, r = R(r0, r1); var p = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); if (avoidPath > 0 && GrassField.Path(p.x, p.y) < avoidPath) continue; o.Add(p); }
        return o;
    }
    static void Static(Transform t) { foreach (var x in t.GetComponentsInChildren<Transform>(true)) if (!x.GetComponent<Light>() && !x.GetComponent<ParticleSystem>() && !x.GetComponent<GrassField>()) GameObjectUtility.SetStaticEditorFlags(x.gameObject, StaticEditorFlags.BatchingStatic); }

    // terrain: flat arena, gentle hills outside; layers painted from noise and the shared path layout
    static Terrain Ground(string name, (string tex, float tile)[] layers, System.Func<float, float, int, float> weight, float hills, System.Func<float, float, float> extra = null, float baseY = 0)
    {
        const float S = 220; int hr = 257, ar = 512;   // big enough that the camera never reaches the edge
        // save the TerrainData asset first, so the splat (paint) textures made by SetAlphamaps become sub-assets and survive into the build
        var tdPath = $"{Gen}/{name}.asset"; if (File.Exists(tdPath)) AssetDatabase.DeleteAsset(tdPath);
        var td = new TerrainData { heightmapResolution = hr, alphamapResolution = ar, size = new Vector3(S, 10, S) }; AssetDatabase.CreateAsset(td, tdPath);
        var h = new float[hr, hr];
        for (int z = 0; z < hr; z++) for (int x = 0; x < hr; x++)
        {
            float wx = x / (hr - 1f) * S - S / 2, wz = z / (hr - 1f) * S - S / 2, r = Mathf.Sqrt(wx * wx + wz * wz);
            float k = Mathf.SmoothStep(0, 1, (r - 25) / 18); h[z, x] = (baseY + (extra != null ? extra(wx, wz) : 0) + k * hills * (.5f + Mathf.PerlinNoise(wx * .05f + 3, wz * .05f + 7)) + Mathf.PerlinNoise(wx * .3f, wz * .3f) * .04f) / 10f;
        }
        td.SetHeights(0, 0, h);
        td.terrainLayers = layers.Select((l, i) =>
        {
            var path = $"{Gen}/{name}_L{i}.terrainlayer"; var tl = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path); if (!tl) { tl = new TerrainLayer(); AssetDatabase.CreateAsset(tl, path); }
            if (l.tex.StartsWith("#")) { tl.diffuseTexture = Solid(l.tex); tl.normalMapTexture = null; }
            else
            {
                tl.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Textures/PBR/{l.tex}_Diffuse_1k.jpg");
                tl.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets/Game/Textures/PBR/{l.tex}_nor_gl_1k.jpg");
            } tl.tileSize = Vector2.one * l.tile; tl.smoothness = .05f; tl.normalScale = 1;
            tl.diffuseRemapMax = l.tex == "leafy_grass" ? new Vector4(.78f, .9f, .68f, 1) : Vector4.one;   // greener ground under the meadow
            EditorUtility.SetDirty(tl); return tl;
        }).ToArray();
        var a = new float[ar, ar, layers.Length];
        for (int z = 0; z < ar; z++) for (int x = 0; x < ar; x++)
        {
            float wx = x / (ar - 1f) * S - S / 2, wz = z / (ar - 1f) * S - S / 2, sum = 0;
            for (int i = 0; i < layers.Length; i++) { a[z, x, i] = Mathf.Max(0, weight(wx, wz, i)); sum += a[z, x, i]; }
            for (int i = 0; i < layers.Length; i++) a[z, x, i] = sum > 0 ? a[z, x, i] / sum : (i == 0 ? 1 : 0);
        }
        td.SetAlphamaps(0, 0, a);
        EditorUtility.SetDirty(td); AssetDatabase.SaveAssets();
        var go = Terrain.CreateTerrainGameObject(td); go.name = name; go.transform.SetParent(root); go.transform.position = new Vector3(-S / 2, -.02f - baseY, -S / 2);
        var t = go.GetComponent<Terrain>(); t.materialTemplate = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
        AssetDatabase.CreateAsset(t.materialTemplate, $"{Gen}/{name}_TerrainMat.mat");
        t.drawInstanced = true; t.basemapDistance = 80; t.heightmapPixelError = 8;
        // beyond the terrain: a dark skirt so nothing ever looks like a floating square
        var skirt = GameObject.CreatePrimitive(PrimitiveType.Plane); Object.DestroyImmediate(skirt.GetComponent<Collider>()); skirt.name = "GroundSkirt"; skirt.transform.SetParent(root);
        skirt.transform.position = new Vector3(0, -.6f, 0); skirt.transform.localScale = Vector3.one * 80; skirt.GetComponent<Renderer>().sharedMaterial = Lit("GroundSkirt", null, 1, 0, new Color(.08f, .1f, .07f));
        return t;
    }
    // a small flat-colour texture asset (low-poly ground), with a faint speckle so it doesn't look like plastic
    static Texture2D Solid(string hex)
    {
        var path = $"{Gen}/Solid_{hex.Substring(1)}.png"; var t = AssetDatabase.LoadAssetAtPath<Texture2D>(path); if (t) return t;
        ColorUtility.TryParseHtmlString(hex, out var c); var tex = new Texture2D(32, 32, TextureFormat.RGB24, false);
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++) tex.SetPixel(x, y, c * (.94f + .06f * Mathf.PerlinNoise(x * .3f, y * .3f)));
        File.WriteAllBytes(path, tex.EncodeToPNG()); AssetDatabase.ImportAsset(path); return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static float PathMask(float x, float z, float w = 1.2f) => 1 - Mathf.SmoothStep(0, 1, (GrassField.Path(x, z) + (Mathf.PerlinNoise(x * .4f, z * .4f) - .5f) - w) / .9f);

    static GrassField Grass(Color b, Color m, Color t, float height, float spacing, float radius)
    {
        var g = new GameObject("Grass").AddComponent<GrassField>(); g.transform.SetParent(root);
        g.material = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Grass.mat"); g.baseCol = b; g.midCol = m; g.tipCol = t; g.height = height; g.spacing = spacing; g.radius = radius; return g;
    }

    // ======================= stage 1: moonlit forest =======================
    static Transform Forest()
    {
        NewRoot("Stage_forest", new Color(.13f, .22f, .24f), .017f, new Color(.55f, .65f, .85f), new Color(.32f, .42f, .4f), new Color(.12f, .14f, .09f));
        var sun = Sun(new Color(.85f, .9f, 1f), 1.8f, new Vector3(50, 150, 0)); root.GetComponent<StageLook>().sun = sun;
        Ground("ForestGround", new[] { ("leafy_grass", 3.5f), ("forest_ground_05", 4f), ("grass_path_3", 3f) }, (x, z, i) =>
        {
            float path = PathMask(x, z), r = Mathf.Sqrt(x * x + z * z), patch = Mathf.PerlinNoise(x * .08f + 5, z * .08f + 2);
            float edge = Mathf.SmoothStep(0, 1, (r - 23) / 6);
            return i == 2 ? path * (r < 27 ? 1 : 0) : i == 1 ? (1 - path) * Mathf.Max(edge, Mathf.SmoothStep(.55f, .75f, patch)) : (1 - path) * (1 - Mathf.Max(edge, Mathf.SmoothStep(.55f, .75f, patch)));
        }, 4);
        Grass(new Color(.05f, .12f, .05f), new Color(.15f, .32f, .12f), new Color(.4f, .56f, .3f), .45f, .32f, 28);   // soft sage meadow green
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        var trees = new[] { (N("island_tree_01"), 1.4f, 2.0f), (N("island_tree_02"), 1.6f, 2.3f), (N("searsia_lucida"), 1.6f, 2.1f), (N("jacaranda_tree"), .32f, .42f) };
        foreach (var p in Ring(46, 25.5f, 46)) { var t = trees[rnd.Next(trees.Length)]; Place(Load(t.Item1), new Vector3(p.x, 0, p.y), R(0, 360), R(t.Item2, t.Item3)); }
        foreach (var p in Ring(40, 22.5f, 27, 0, .02f)) Place(Load(N(rnd.Next(3) == 0 ? "shrub_02" : "fern_02")), new Vector3(p.x, 0, p.y), R(0, 360), rnd.Next(3) == 0 ? R(.35f, .5f) : R(1, 1.5f));
        foreach (var p in Ring(14, 13, 21.5f, 2.2f, .15f)) Place(Load(N(rnd.Next(2) == 0 ? "fern_02" : "weed_plant_02")), new Vector3(p.x, 0, p.y), R(0, 360), R(.8f, 1.2f));
        foreach (var p in Ring(10, 22, 25.5f)) Place(Load(N(rnd.Next(2) == 0 ? "rock_moss_set_01" : "rock_moss_set_02")), new Vector3(p.x, -.2f, p.y), R(0, 360), R(.35f, .55f));
        foreach (var p in Ring(6, 15, 21, 2.5f, .2f)) Place(Load(N(rnd.Next(2) == 0 ? "boulder_01" : "namaqualand_boulder_03")), new Vector3(p.x, -.1f, p.y), R(0, 360), R(.45f, .7f));
        foreach (var p in Ring(5, 21.5f, 24)) Place(Load(N("dead_tree_trunk")), new Vector3(p.x, 0, p.y), R(0, 360), R(1.3f, 1.8f));
        foreach (var p in Ring(4, 20, 24)) Place(Load("Assets/Game/Models/Props/tree_stump_01/tree_stump_01_1k.gltf"), new Vector3(p.x, -.05f, p.y), R(0, 360), R(.9f, 1.3f));
        foreach (var p in Ring(4, 22, 25)) Place(Load(N("root_cluster_01")), new Vector3(p.x, 0, p.y), R(0, 360), R(.5f, .8f));
        // glowing mushroom clusters with soft coloured light
        var stem = Lit("MushStem", null, 1, .2f, new Color(.85f, .82f, .74f));
        var cols = new[] { new Color(.4f, 2.4f, 2.6f), new Color(2.6f, .5f, 2f), new Color(2.4f, 1.7f, .4f) };
        var capMesh = Combine("MushCap", (PrimitiveType.Sphere, T(0, 0, 0, 1, .45f, 1))); var stemMesh = Combine("MushStem", (PrimitiveType.Cylinder, T(0, .5f, 0, .3f, .5f, .3f)));
        int li = 0;
        foreach (var p in Ring(16, 14, 24, 1.6f, .2f))
        {
            var c = cols[rnd.Next(3)]; var cap = GlowMat("MushCap" + System.Array.IndexOf(cols, c), c);
            for (int k = 0; k < 4; k++) { float s = R(.12f, .3f); var q = new Vector3(p.x + R(-.6f, .6f), 0, p.y + R(-.6f, .6f)); MeshObj("stem", stemMesh, stem, q, 0, s); MeshObj("cap", capMesh, cap, q + Vector3.up * s, R(0, 360), s * 1.2f); }
            if (li++ % 3 == 0) PointLight(new Vector3(p.x, .8f, p.y), c / 2.6f, 2.2f, 5);
        }
        foreach (var p in Ring(4, 8, 20, 2, .2f)) { var gl = Place(Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Misc/CFXR3 Ambient Glows.prefab"), new Vector3(p.x, .5f, p.y)); }
        Static(root); return root;
    }

    // ======================= stage 2: abandoned castle hall (Dungeon kit) =======================
    static Transform Castle()
    {
        NewRoot("Stage_castle", new Color(.07f, .07f, .1f), .012f, new Color(.6f, .62f, .78f), new Color(.38f, .33f, .32f), new Color(.14f, .11f, .1f));
        var sun = Sun(new Color(.75f, .8f, 1f), 1.9f, new Vector3(62, 165, 0)); root.GetComponent<StageLook>().sun = sun;
        const string D = "Assets/ThirdParty/Dungeon/Prefabs/";
        var floors = new[] { Load(D + "Floors/Floor_A.prefab") };   // Floor_B renders dark from above
        Underlay();
        var fl = new GameObject("Floor").transform; fl.SetParent(root);
        for (int ix = -6; ix <= 6; ix++) for (int iz = -6; iz <= 6; iz++) Place(floors[0], new Vector3(ix * 5.8f, 0, iz * 5.8f), 90 * rnd.Next(4), 1, true, fl);
        // walls on a square (the near/south side is low so it never hides the hero)
        var walls = new[] { Load(D + "Walls/Wall_A.prefab"), Load(D + "Walls/Wall_B.prefab"), Load(D + "Walls/Wall_C.prefab"), Load(D + "Walls/Wall_E.prefab") };
        const float W = 26.4f;
        for (int side = 0; side < 4; side++)
            for (int i = -4; i <= 4; i++)
            {
                float along = i * 5.86f; Vector3 p; float yaw;
                if (side == 0) { p = new Vector3(-W, 0, along); yaw = 0; } else if (side == 1) { p = new Vector3(W, 0, along); yaw = 180; } else if (side == 2) { p = new Vector3(along, 0, -W); yaw = 90; } else { p = new Vector3(along, 0, W); yaw = -90; }
                var w = Place(walls[rnd.Next(walls.Length)], p, yaw); if (side == 3) { w.transform.localScale = new Vector3(w.transform.localScale.x, w.transform.localScale.y * .3f, w.transform.localScale.z); var b = Bounds(w); w.transform.position += Vector3.up * -b.min.y; }
            }
        var pillar = Load(D + "Pillars/Pillar_A.prefab"); var torch = Load("Assets/ThirdParty/PrisonCell/Prefabs/Props/Torch.prefab");
        var fire = Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        int n = 0;
        for (int side = 0; side < 3; side++)
            for (int i = -2; i <= 2; i++)
            {
                float along = i * 9.5f, inset = W - 1.4f; Vector3 p = side == 0 ? new Vector3(-inset, 0, along) : side == 1 ? new Vector3(inset, 0, along) : new Vector3(along, 0, -inset);
                Place(pillar, p, 0, 1);
                var inward = -new Vector3(p.x, 0, p.z).normalized; if (side == 2) inward = Vector3.forward; if (side == 0) inward = Vector3.right; if (side == 1) inward = Vector3.left;
                var tp = p + inward * .75f + Vector3.up * 2.6f;
                var t = Place(torch, tp, Quaternion.LookRotation(inward).eulerAngles.y, 1.6f);
                var f = Place(fire, tp + Vector3.up * .55f, 0, .35f); if (f) f.transform.position = tp + Vector3.up * .6f;
                if (n++ % 2 == 0) PointLight(tp + inward * .6f + Vector3.up * .3f, new Color(1, .55f, .22f), 12, 15);
            }
        // braziers and the runic circle in the middle
        var pit = Load("Assets/Game/Models/Props/stone_fire_pit/stone_fire_pit_1k.gltf");
        foreach (var a in new[] { 45, 135, 225, 315 }) { var p = Quaternion.Euler(0, a, 0) * Vector3.forward * 16; Place(pit, p, R(0, 360), 1.2f); Place(fire, p + Vector3.up * .3f, 0, .9f); PointLight(p + Vector3.up * 1.2f, new Color(1, .5f, .2f), 12, 13); }
        PointLight(new Vector3(0, 1.5f, 0), new Color(.9f, .2f, .15f), 3, 9);
        // props along the walls
        var props = new[] { (D + "Props/Crate_A.prefab", 1f), (D + "Props/Crate_C.prefab", 1f), (D + "Props/Barrel.prefab", .55f), (D + "Props/Table_A.prefab", 1f), (D + "Props/Stool_A_Broken.prefab", 1f),
            ("Assets/Game/Models/Props/Barrel_01/Barrel_01_1k.gltf", 1.3f), ("Assets/Game/Models/Props/wooden_crate_01/wooden_crate_01_1k.gltf", 1.5f), (D + "Props/Floor_Cage.prefab", 1f), (D + "Props/Hanging_Cage.prefab", .8f) };
        for (int i = 0; i < 26; i++)
        {
            var (pp, sc) = props[rnd.Next(props.Length)]; float along = R(-22, 22), inset = R(22, 24.2f); int side = rnd.Next(3);
            var p = side == 0 ? new Vector3(-inset, 0, along) : side == 1 ? new Vector3(inset, 0, along) : new Vector3(along, 0, -inset);
            Place(Load(pp), p, R(0, 360), sc);
        }
        Place(Load("Assets/Game/Models/Props/treasure_chest/treasure_chest_1k.gltf"), new Vector3(0, 0, -23.5f), 0, 1.6f);
        Place(Load("Assets/Game/Models/Props/large_iron_gate/large_iron_gate_1k.gltf"), new Vector3(0, 0, -25.6f), 0, 1.8f);
        foreach (var p in new[] { new Vector3(-6, 0, -25), new Vector3(6, 0, -25) }) Place(Load(D + "Props/Chain_B.prefab"), p + Vector3.up * 5.5f, 0, 1, true);
        var decals = new[] { Load(D + "Decals/Grunge_A.prefab"), Load(D + "Decals/Grunge_C.prefab"), Load(D + "Decals/Grunge_E.prefab"), Load(D + "Decals/Hay_B.prefab") };
        Static(root); return root;
    }

    // ======================= stage 3: cursed graveyard =======================
    static Transform Grave()
    {
        NewRoot("Stage_grave", new Color(.17f, .13f, .24f), .014f, new Color(.7f, .64f, .92f), new Color(.42f, .36f, .46f), new Color(.14f, .11f, .1f));
        var sun = Sun(new Color(.85f, .8f, 1f), 2.4f, new Vector3(52, 205, 0)); root.GetComponent<StageLook>().sun = sun;
        Ground("GraveGround", new[] { ("mud_forest", 4f), ("brown_mud_02", 4f), ("rocky_gravel", 2.5f) }, (x, z, i) =>
        {
            float path = PathMask(x, z, 1.1f), patch = Mathf.PerlinNoise(x * .1f + 9, z * .1f + 4);
            return i == 2 ? path : i == 1 ? (1 - path) * Mathf.SmoothStep(.45f, .7f, patch) : (1 - path) * (1 - Mathf.SmoothStep(.45f, .7f, patch));
        }, 3);
        Grass(new Color(.13f, .11f, .06f), new Color(.3f, .27f, .14f), new Color(.6f, .55f, .33f), .3f, .75f, 28);
        var stone = Lit("GraveStone", "rock_face_04", .6f, .1f, new Color(.62f, .62f, .66f)); var bark = Lit("DeadBark", "bark_brown_02", 1, .05f, new Color(.45f, .42f, .42f));
        var iron = Lit("Iron", null, 1, .55f, new Color(.08f, .08f, .1f)); iron.SetFloat("_Metallic", .85f);
        var gs = Combine("Gravestone", (PrimitiveType.Cube, T(0, .4f, 0, .75f, .8f, .16f)), (PrimitiveType.Cylinder, T(0, .8f, 0, .75f, .08f, .75f, 90)), (PrimitiveType.Cube, T(0, .06f, 0, .95f, .12f, .4f)));
        var cross = Combine("Cross", (PrimitiveType.Cube, T(0, .65f, 0, .14f, 1.3f, .14f)), (PrimitiveType.Cube, T(0, .95f, 0, .7f, .14f, .14f)), (PrimitiveType.Cube, T(0, .08f, 0, .5f, .16f, .3f)));
        // rows of graves north and south of the centre, plus scattered ones at the edge
        for (int row = 0; row < 4; row++)
            for (int i = 0; i < 9; i++)
            {
                float z = (row < 2 ? -1 : 1) * (12.5f + (row % 2) * 3.2f), x = -13 + i * 3.25f + R(-.4f, .4f);
                if (GrassField.Path(x, z) < 1.6f || Mathf.Sqrt(x * x + z * z) > 21.5f) continue;
                bool c = rnd.Next(4) == 0; var g = MeshObj(c ? "cross" : "grave", c ? cross : gs, stone, new Vector3(x, 0, z), (z < 0 ? 0 : 180) + R(-12, 12), R(.9f, 1.25f));
                g.transform.rotation *= Quaternion.Euler(R(-6, 6), 0, R(-6, 6));
            }
        foreach (var p in Ring(24, 24, 36)) MeshObj("grave", rnd.Next(3) == 0 ? cross : gs, stone, new Vector3(p.x, 0, p.y), R(0, 360), R(.9f, 1.3f));
        // dead trees from bark cylinders
        for (int v = 0; v < 3; v++)
        {
            var parts = new List<(PrimitiveType, Matrix4x4)> { (PrimitiveType.Cylinder, T(0, 2.5f, 0, .5f, 2.5f, .5f, 0, 0, R(-4, 4))) };
            for (int b = 0; b < 7; b++) { float y = 2.2f + b * .45f, a = b * 137 + v * 40, l = R(1.1f, 2.2f); parts.Add((PrimitiveType.Cylinder, Matrix4x4.TRS(new Vector3(0, y, 0), Quaternion.Euler(0, a, R(35, 65)), Vector3.one) * T(0, l / 2, 0, .16f, l / 2, .16f))); }
            var mesh = Combine("DeadTree" + v, parts.ToArray());
            foreach (var p in Ring(10, 24.5f, 42).Where((_, i) => true)) MeshObj("deadtree", mesh, bark, new Vector3(p.x, 0, p.y), R(0, 360), R(.9f, 1.5f));
        }
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        foreach (var p in Ring(6, 22, 26)) Place(Load(N("root_cluster_01")), new Vector3(p.x, 0, p.y), R(0, 360), R(.6f, .9f));
        foreach (var p in Ring(5, 21, 24)) Place(Load(N("dead_tree_trunk")), new Vector3(p.x, 0, p.y), R(0, 360), R(1.3f, 1.8f));
        foreach (var p in Ring(8, 22, 27)) Place(Load(N(rnd.Next(2) == 0 ? "namaqualand_boulder_02" : "boulder_01")), new Vector3(p.x, -.15f, p.y), R(0, 360), R(.5f, .8f));
        foreach (var p in Ring(4, 18, 21, 2, .2f)) Place(Load("Assets/Game/Models/Props/tree_stump_01/tree_stump_01_1k.gltf"), new Vector3(p.x, -.05f, p.y), R(0, 360), R(.9f, 1.2f));
        // iron fence ring with gates where the path crosses
        var postParts = new List<(PrimitiveType, Matrix4x4)>();
        for (int i = 0; i < 160; i++)
        {
            float a = i / 160f * Mathf.PI * 2, r = 23.6f; float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r; if (Mathf.Abs(x) < 2.2f) continue;
            postParts.Add((PrimitiveType.Cylinder, T(x, .8f, z, .05f, .8f, .05f)));
            float a2 = (i + .5f) / 160f * Mathf.PI * 2; float seg = r * Mathf.PI * 2 / 160;
            foreach (float y in new[] { .35f, 1.3f }) postParts.Add((PrimitiveType.Cube, T(Mathf.Cos(a2) * r, y, Mathf.Sin(a2) * r, seg, .04f, .04f, 0, -a2 * Mathf.Rad2Deg + 90)));
        }
        MeshObj("fence", Combine("Fence", postParts.ToArray()), iron, Vector3.zero, 0, 1);
        var gate = Load("Assets/Game/Models/Props/large_iron_gate/large_iron_gate_1k.gltf");
        Place(gate, new Vector3(0, 0, -23.6f), 0, 1.4f); Place(gate, new Vector3(0, 0, 23.6f), 180, 1.4f);
        // lanterns on poles with green ghost-light
        var lantern = Load(N("brass_diya_lantern")); var pole = Combine("Pole", (PrimitiveType.Cylinder, T(0, 1, 0, .07f, 1, .07f)));
        for (int i = 0; i < 6; i++)
        {
            float a = (i / 6f + .08f) * Mathf.PI * 2; var p = new Vector3(Mathf.Cos(a) * 16.5f, 0, Mathf.Sin(a) * 16.5f);
            MeshObj("pole", pole, iron, p, 0, 1); Place(lantern, p + Vector3.up * 2f, 0, 1.8f);
            PointLight(p + Vector3.up * 2.2f, i % 2 == 0 ? new Color(.4f, 1, .6f) : new Color(.6f, .45f, 1), 9, 11);
        }
        foreach (var p in Ring(5, 6, 21, 2, .2f)) Place(Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Misc/CFXR3 Ambient Glows.prefab"), new Vector3(p.x, .6f, p.y));
        Static(root); return root;
    }

    // ======================= lane battle: one long stone lane =======================
    // Both new stages keep the fighting circle (radius ~19) open, flat and mirror-symmetric left/right so PvP duels are fair;
    // tall buildings stand only behind (-Z) and to the sides, the camera side (+Z) gets low pieces so the hero is never hidden.
    static GameObject Fit(GameObject pf, Vector3 at, float yaw, float height, Transform parent = null)
    {   // place a prefab scaled to a target height (metres), sitting on the ground at 'at'
        var go = Place(pf, at, yaw, 1, false, parent); if (!go) return null; float h = Bounds(go).size.y;
        go.transform.localScale *= height / Mathf.Max(h, .01f); var b = Bounds(go);
        go.transform.position += new Vector3(at.x - b.center.x, at.y - b.min.y, at.z - b.center.z); return go;
    }
    static void Mirror(System.Action<float> place) { place(1); place(-1); }

    // ======================= stage 2: fantasy kingdom town square (Fantasy Kingdom) =======================
    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));   // smoothstep between two edges
    static Transform KingdomSquare()
    {
        // dusk: low orange sun, blue-violet fog and ambient, so every lantern glows through the mist
        NewRoot("Stage_kingdom", new Color(.22f, .24f, .36f), .011f, new Color(.62f, .64f, .9f), new Color(.46f, .42f, .52f), new Color(.17f, .14f, .13f));
        var sun = Sun(new Color(1f, .72f, .55f), 2f, new Vector3(34, 150, 0)); root.GetComponent<StageLook>().sun = sun;
        var at = root.gameObject.AddComponent<Atmos>(); at.mist = new Color(.5f, .55f, .72f, .12f); at.fireflies = 50; at.haloColor = new Color(1, .64f, .32f); at.haloSize = 2.8f;
        // cobbled plaza in the middle, a dirt ring road, grass beyond
        Ground("KingdomGround", new[] { ("leafy_grass", 3.5f), ("grass_path_3", 3f), ("forest_ground_05", 4f) }, (x, z, i) =>
        {
            float r = Mathf.Sqrt(x * x + z * z) + (Mathf.PerlinNoise(x * .2f, z * .2f) - .5f) * 2;
            float plaza = 1 - Smooth(15, 16.5f, r), road = (1 - plaza) * (1 - Smooth(19.5f, 21.5f, r));
            return i == 2 ? plaza : i == 1 ? road : (1 - plaza) * (1 - road);
        }, 5);
        var kg = Grass(new Color(.04f, .08f, .05f), new Color(.12f, .22f, .12f), new Color(.3f, .4f, .26f), .36f, .38f, 30); kg.clearRadius = 21;
        const string H = "Assets/Art/Prefabs/Houses/", K = "Assets/Art/Prefabs/Foliage/Trees and Bushes/";
        GameObject House(string n, float x, float z, float h) => Fit(Load(H + n + ".prefab"), new Vector3(x, 0, z), Mathf.Atan2(-x, -z) * Mathf.Rad2Deg, h);
        void Mark(string n, Vector3 p) { var m = new GameObject(n).transform; m.SetParent(root); m.position = p; }
        var fireFx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        void Fire(Vector3 p, float sc) { var f = (GameObject)PrefabUtility.InstantiatePrefab(fireFx, root); f.transform.position = p; f.transform.localScale *= sc; foreach (var ps in f.GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "Smoke") ps.gameObject.SetActive(false); }
        // houses close around the square, facing it, windows lit warm (mirrored left/right; the camera side +Z stays open)
        foreach (var (n, x, z, h) in new[] { ("Preset_Tavern_01", 0f, -28f, 12f), ("Preset_Church_01_A", 15f, -26f, 12f), ("Preset_Church_01_A", -15f, -26f, 12f), ("Preset_House_05", 26f, -15f, 11f), ("Preset_House_01_A", -26f, -15f, 11f),
            ("Preset_Blacksmith_01", 28f, 0f, 9f), ("Preset_Stables_01", -28f, 0f, 9f), ("Preset_Tower_01", 26f, 15f, 13f), ("Preset_House_Windmill_01", -26f, 15f, 13f), ("Preset_House_03", 30f, -29f, 10f), ("Preset_House_03", -30f, -29f, 10f) })
        {
            House(n, x, z, h); var toC = new Vector3(-x, 0, -z).normalized; var w = new Vector3(x, 2.2f, z) + toC * 4.5f;
            PointLight(w, new Color(1, .62f, .3f), 4, 7); Mark("Glow_window", w);
        }
        // centre: a raised stone dais with a burning brazier (the duel ring, readable from above)
        var stone = Lit("KingdomStone", "rock_face_04", 3, .1f, new Color(.9f, .86f, .8f));
        var dais = Combine("KingdomDais", (PrimitiveType.Cylinder, T(0, .09f, 0, 9, .09f, 9)), (PrimitiveType.Cylinder, T(0, .22f, 0, 2.2f, .22f, 2.2f)));
        MeshObj("dais", dais, stone, Vector3.zero, 0, 1);
        var pit = Load("Assets/Game/Models/Props/stone_fire_pit/stone_fire_pit_1k.gltf"); if (pit) Fit(pit, new Vector3(0, .44f, 0), 0, .35f);
        Fire(new Vector3(0, .6f, 0), .4f); PointLight(new Vector3(0, 1.6f, 0), new Color(1, .55f, .25f), 8, 12); Mark("Glow_brazier", new Vector3(0, 1.3f, 0));
        // lamp posts round the plaza with strings of small festival lanterns hung between them
        var lantern = Load("Assets/Game/Models/Nature/brass_diya_lantern.glb");
        var pole = Combine("KingdomPole", (PrimitiveType.Cylinder, T(0, 1.4f, 0, .1f, 1.4f, .1f)), (PrimitiveType.Cube, T(0, 2.75f, .3f, .08f, .08f, .7f)));
        var iron = Lit("KingdomIron", null, 1, .4f, new Color(.14f, .13f, .12f)); const int N = 10; var tops = new Vector3[N];
        for (int i = 0; i < N; i++)
        {
            float a = (i + .5f) / N * Mathf.PI * 2; var p = new Vector3(Mathf.Cos(a) * 15.6f, 0, Mathf.Sin(a) * 15.6f); tops[i] = p + Vector3.up * 2.75f;
            MeshObj("pole", pole, iron, p, -a * Mathf.Rad2Deg - 90, 1); var lp = p + (-p.normalized) * .6f; Fit(lantern, lp + Vector3.up * 2.1f, 0, .5f);
            PointLight(lp + Vector3.up * 2.3f, new Color(1, .72f, .4f), 5, 8); Mark("Glow_lamp", lp + Vector3.up * 2.35f);
        }
        var bulbMat = GlowMat("KingdomBulb", new Color(2.4f, 1.5f, .7f)); var bulbRed = GlowMat("KingdomBulbRed", new Color(2.4f, .6f, .4f));
        var bulb = Combine("KingdomBulbMesh", (PrimitiveType.Sphere, T(0, 0, 0, .16f, .2f, .16f))); var wire = Lit("KingdomWire", null, 1, .2f, new Color(.1f, .09f, .08f));
        for (int i = 0; i < N; i++)
        {
            var a = tops[i]; var b = tops[(i + 1) % N];
            for (int k = 1; k < 9; k++) { float t = k / 9f; var p = Vector3.Lerp(a, b, t) + Vector3.down * Mathf.Sin(t * Mathf.PI) * .8f; MeshObj("bulb", bulb, k % 2 == 0 ? bulbRed : bulbMat, p, 0, 1); }
            var cable = Combine("KingdomCable" + i, (PrimitiveType.Cube, Matrix4x4.TRS(Vector3.zero, Quaternion.LookRotation(b - a), new Vector3(.02f, .02f, (b - a).magnitude))));
            MeshObj("cable", cable, wire, Vector3.Lerp(a, b, .5f) + Vector3.down * .5f, 0, 1);
        }
        // low stone walls with gaps on the camera side, small trees in planters on the plaza, bushes on its edge
        Mirror(s => Fit(Load("Assets/Art/Prefabs/Town Wall Border/SM_Town_Wall_Border_04.prefab"), new Vector3(s * 11, 0, 25), 0, 1.1f));
        Mirror(s => Fit(Load(K + "SM_Env_Tree_Large_01.prefab"), new Vector3(s * 19, 0, -19), s * 30, 10));
        Mirror(s => Fit(Load(K + "SM_Env_Tree_Round_03.prefab"), new Vector3(s * 19, 0, 19), s * 60, 8));
        var planter = Combine("KingdomPlanter", (PrimitiveType.Cylinder, T(0, .25f, 0, 1.6f, .25f, 1.6f)));
        foreach (var (x, z) in new[] { (11f, -8f), (-11f, -8f), (11f, 8f), (-11f, 8f) }) { MeshObj("planter", planter, stone, new Vector3(x, 0, z), 0, 1); Fit(Load(K + "SmallTreeRound_01.prefab"), new Vector3(x, .5f, z), R(0, 360), 4); }
        foreach (var x in new[] { -36f, -12f, 12f, 36f }) Fit(Load(K + "TreePine_VarB.prefab"), new Vector3(x, 0, -42), R(0, 360), R(14, 18));
        foreach (var p in Ring(14, 17.5f, 19.5f, 0, .02f)) Fit(Load(K + (rnd.Next(2) == 0 ? "SM_Env_Bush_Cluster_03.prefab" : "Bush_01.prefab")), new Vector3(p.x, 0, p.y), R(0, 360), R(1.1f, 1.6f));
        Static(root); return root;
    }

    // ======================= stage 5: green valley (Polytope low-poly nature) =======================
    // bright cartoon-green meadow, a winding river with two wooden bridges, pines and fruit trees round the edge;
    // the middle stays open for fights (the river crosses it, shallow and walkable)
    static float ValleyRiver(float x) => -3 + Mathf.Sin(x * .09f) * 4.5f;
    static Transform ValleyStage()
    {
        NewRoot("Stage_valley", new Color(.34f, .38f, .34f), .009f, new Color(.5f, .58f, .66f), new Color(.3f, .36f, .24f), new Color(.1f, .12f, .06f));
        var sun = Sun(new Color(1f, .74f, .46f), 2.2f, new Vector3(24, 125, 0));   // low warm evening sun, long shadows root.GetComponent<StageLook>().sun = sun;
        var at = root.gameObject.AddComponent<Atmos>(); at.mist = new Color(.9f, .75f, .55f, .06f); at.fireflies = 25;
        const float RH = 2.4f;
        // flat bright greens painted as solid colours, a lighter grass path, sandy river banks
        Ground("ValleyGround", new[] { ("leafy_grass", 3.5f), ("forest_ground_05", 4f), ("brown_mud_02", 3f), ("grass_path_3", 3f) }, (x, z, i) =>
        {
            float d = Mathf.Abs(z - ValleyRiver(x)), bank = 1 - Smooth(RH, RH + 1.6f, d), patch = Mathf.PerlinNoise(x * .07f + 3, z * .07f + 5), path = PathMask(x, z) * (1 - bank);
            return i == 2 ? bank : i == 3 ? path : i == 1 ? (1 - bank) * (1 - path) * Smooth(.5f, .7f, patch) : (1 - bank) * (1 - path) * (1 - Smooth(.5f, .7f, patch));
        }, 7, (x, z) => -.45f * (1 - Smooth(RH - .6f, RH + 1.2f, Mathf.Abs(z - ValleyRiver(x)))), .45f);
        var vg = Grass(new Color(.02f, .08f, .03f), new Color(.08f, .26f, .08f), new Color(.34f, .55f, .16f), .5f, .3f, 30); vg.channel = new Vector4(-3, 4.5f, .09f, 2.4f + 1.4f);   // keep the river (ValleyRiver) clear
        const string P = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/", V = "Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/";
        // river: strips of the pack's water following the curve
        var wmat = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Water.mat");
        for (float x = -60; x < 60; x += 4)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(w.GetComponent<Collider>()); w.name = "Water"; w.transform.SetParent(root);
            float z0 = ValleyRiver(x), z1 = ValleyRiver(x + 4); w.transform.position = new Vector3(x + 2, -.22f, (z0 + z1) / 2);
            w.transform.rotation = Quaternion.Euler(90, -Mathf.Atan2(z1 - z0, 4) * Mathf.Rad2Deg, 0); w.transform.localScale = new Vector3(4.6f, RH * 2 + 1.2f, 1);
            if (wmat) w.GetComponent<Renderer>().sharedMaterial = wmat;
        }
        // two wooden bridges across the river
        var bridge = Load(V + "Bridge/PT_Wooden_Bridge_02.prefab");
        foreach (float bx in new[] { -11f, 12f })
        {
            float slope = Mathf.Atan(.09f * 4.5f * Mathf.Cos(bx * .09f)) * Mathf.Rad2Deg;
            Fit(bridge, new Vector3(bx, -.1f, ValleyRiver(bx)), 90 - slope, 2.2f);
        }
        // trees: pines and fruit trees in a ring round the meadow, a few inside for cover
        var pines = new[] { P + "Trees/PT_Pine_Tree_03_green.prefab", P + "Trees/PT_Pine_Tree_03_green_cut.prefab" };
        var fruit = new[] { P + "Trees/PT_Fruit_Tree_01_green.prefab", P + "Trees/PT_Fruit_Tree_01_apples.prefab", P + "Trees/PT_Fruit_Tree_01_pears.prefab", P + "Trees/PT_Fruit_Tree_01_plums.prefab" };
        bool Dry(float x, float z, float pad) => Mathf.Abs(z - ValleyRiver(x)) > RH + 1.5f + pad;
        foreach (var p in Ring(26, 25, 36)) if (Dry(p.x, p.y, 1)) Fit(Load(pines[rnd.Next(2)]), new Vector3(p.x, 0, p.y), R(0, 360), R(9, 14));
        foreach (var p in Ring(14, 19, 24, 2.5f, .04f)) if (Dry(p.x, p.y, 1.5f)) Fit(Load(fruit[rnd.Next(fruit.Length)]), new Vector3(p.x, 0, p.y), R(0, 360), R(5.5f, 7.5f));
        // shrubs, menhir stones, rocks by the water, logs and stumps, mushrooms
        foreach (var p in Ring(18, 15, 23, 2, .05f)) if (Dry(p.x, p.y, .5f)) Fit(Load(P + "Shrubs/PT_Generic_Shrub_01_green.prefab"), new Vector3(p.x, 0, p.y), R(0, 360), R(1.4f, 2.2f));
        foreach (var (x, z) in new[] { (-17f, -15f), (18f, -14f), (-20f, 9f), (21f, 10f) }) Fit(Load(P + "Rocks/PT_Menhir_Rock_02.prefab"), new Vector3(x, 0, z), R(0, 360), R(2.4f, 3.2f));
        for (float x = -40; x < 40; x += R(3, 6)) { float z = ValleyRiver(x) + (rnd.Next(2) * 2 - 1) * (RH + R(.3f, 1.2f)); Fit(Load(P + "Rocks/PT_River_Rock_Pile_02.prefab"), new Vector3(x, -.15f, z), R(0, 360), R(.25f, .45f)); }
        foreach (var p in Ring(6, 17, 22, 2, .1f)) if (Dry(p.x, p.y, 1)) Fit(Load(P + (rnd.Next(2) == 0 ? "Trees/PT_Pine_Tree_03_logs.prefab" : "Trees/PT_Fruit_Tree_01_stump.prefab")), new Vector3(p.x, 0, p.y), R(0, 360), R(.6f, 1));
        // flowers and grass tufts scattered over the meadow (low, the fight area stays readable)
        for (int i = 0; i < 260; i++)
        {
            float x = R(-30, 30), z = R(-30, 30); if (!Dry(x, z, .3f) || GrassField.Path(x, z) < 1.4f) continue;
            int k = 6 + rnd.Next(4); if (rnd.Next(3) > 0) continue; string pf = k < 9 ? "Flowers/PT_Poppy_02.prefab" : "Mushrooms/PT_Caesars_Mushroom_01.prefab";
            Fit(Load(P + pf), new Vector3(x, 0, z), R(0, 360), k < 6 ? R(.5f, .8f) : k < 9 ? R(.6f, .9f) : R(.25f, .4f));
        }
        // a little fenced paddock and a gate at the back
        var fence = Load(V + "Fence/PT_Modular_Fence_Wood_01.prefab");
        for (int i = -3; i <= 3; i++) Fit(fence, new Vector3(i * 2.2f, 0, -26), 0, 1.5f);
        Fit(Load(V + "Fence/PT_Modular_Gate_Wood_01.prefab"), new Vector3(0, 0, -26.3f), 0, 1.7f);
        Static(root); return root;
    }

    // ======================= three-lane map: forest, three dirt lanes, a river across the middle =======================
    // distance from (x,z) to the centre line of the nearest lane (lanes run along Z at x = -22/0/22 and bend in to the cores)
    static float LaneDist(float x, float z)
    {
        float best = 999;
        foreach (float lx in new[] { -22f, 0f, 22f })
        {
            float az = Mathf.Abs(z), cx = az > 30 ? Mathf.Lerp(lx, 0, Mathf.InverseLerp(30, 41, az)) : lx;
            best = Mathf.Min(best, Mathf.Abs(x - cx));
        }
        float baseD = Mathf.Min(new Vector2(x, z - 40).magnitude, new Vector2(x, z + 40).magnitude) - 7;   // round clearings at both bases
        return Mathf.Min(best, baseD);
    }
    static Transform Lane3Stage()
    {
        NewRoot("Stage_lane3", new Color(.42f, .52f, .52f), .006f, new Color(.56f, .64f, .72f), new Color(.38f, .4f, .32f), new Color(.16f, .15f, .1f));
        var sun = Sun(new Color(1f, .88f, .72f), 1.6f, new Vector3(42, 150, 0)); root.GetComponent<StageLook>().sun = sun;
        const float RiverHalf = 2.6f;
        Ground("Lane3Ground", new[] { ("leafy_grass", 3.5f), ("grass_path_3", 3f), ("brown_mud_02", 3f), ("forest_ground_05", 4f) }, (x, z, i) =>
        {
            float lane = 1 - Smooth(3.2f, 4.6f, LaneDist(x, z) + (Mathf.PerlinNoise(x * .3f, z * .3f) - .5f) * 1.2f);
            float bank = 1 - Smooth(RiverHalf, RiverHalf + 2.5f, Mathf.Abs(z)), patch = Mathf.PerlinNoise(x * .08f + 4, z * .08f + 2);
            return i == 1 ? lane * (1 - bank) : i == 2 ? bank : i == 3 ? (1 - lane) * (1 - bank) * Smooth(.55f, .72f, patch) : (1 - lane) * (1 - bank) * (1 - Smooth(.55f, .72f, patch));
        }, 0, (x, z) => -.35f * (1 - Smooth(RiverHalf - .5f, RiverHalf + 1.5f, Mathf.Abs(z))), .35f);   // flat map (no edge hills inside the lanes)
        // river water across the map
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane); Object.DestroyImmediate(water.GetComponent<Collider>()); water.name = "Water"; water.transform.SetParent(root);
        water.transform.position = new Vector3(0, -.2f, 0); water.transform.localScale = new Vector3(8, 1, .9f);
        var wm = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Water.mat"); if (wm) water.GetComponent<Renderer>().sharedMaterial = wm;
        // a plank bridge where each lane crosses the river
        var plank = Lit("Planks", "weathered_brown_planks", 1, .15f); var rail = Lit("BridgeRail", "bark_brown_02", 1, .05f, new Color(.55f, .45f, .35f));
        foreach (float lx in new[] { -22f, 0f, 22f })
        {
            var br = new GameObject("Bridge").transform; br.SetParent(root); br.position = new Vector3(lx, 0, 0);
            for (float t = -RiverHalf - 1.4f; t <= RiverHalf + 1.4f; t += .42f)
            {
                var pl = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(pl.GetComponent<Collider>()); pl.transform.SetParent(br, false);
                pl.transform.localPosition = new Vector3(0, .06f + .18f * Mathf.Cos(t / (RiverHalf + 1.4f) * Mathf.PI / 2), t); pl.transform.localScale = new Vector3(5.6f, .09f, .38f); pl.GetComponent<Renderer>().sharedMaterial = plank;
            }
            foreach (int sd in new[] { -1, 1 })
            {
                var r = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(r.GetComponent<Collider>()); r.transform.SetParent(br, false);
                r.transform.localPosition = new Vector3(sd * 2.85f, .75f, 0); r.transform.localScale = new Vector3(.1f, .1f, RiverHalf * 2 + 2.8f); r.GetComponent<Renderer>().sharedMaterial = rail;
            }
        }
        // jungle between the lanes and around the edge: trees, bushes and rocks, kept off the lanes, the river and the base clearings
        const string K = "Assets/Art/Prefabs/Foliage/Trees and Bushes/"; string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        var trees = new[] { K + "SM_Env_Tree_Round_02.prefab", K + "SmallTreeRound_01.prefab", K + "SmallTreeRound_02.prefab", K + "TreePine_VarB.prefab", K + "SM_Env_Tree_Large_01.prefab" };
        var bushes = new[] { K + "SM_Env_Bush_Cluster_03.prefab", K + "Bush_01.prefab", K + "Bush_02.prefab" };
        var rocks = new[] { N("boulder_01"), N("namaqualand_boulder_02"), N("rock_moss_set_01") };
        bool Free(float x, float z, float pad) => LaneDist(x, z) > 4.5f + pad && Mathf.Abs(z) > RiverHalf + 2 + pad;
        for (int i = 0; i < 400; i++)
        {
            float x = R(-38, 38), z = R(-50, 50); if (!Free(x, z, 1.5f)) continue;
            int kind = rnd.Next(10); bool edge = Mathf.Abs(x) > 31;
            if (kind < 4 || edge) Fit(Load(trees[rnd.Next(trees.Length)]), new Vector3(x, 0, z), R(0, 360), edge ? R(9, 15) : R(5, 9));
            else if (kind < 8) Fit(Load(bushes[rnd.Next(bushes.Length)]), new Vector3(x, 0, z), R(0, 360), R(1, 1.8f));
            else Fit(Load(rocks[rnd.Next(rocks.Length)]), new Vector3(x, 0, z), R(0, 360), R(.8f, 1.8f));
        }
        Static(root); return root;
    }

    static Transform LaneStage()
    {
        NewRoot("Stage_lane", new Color(.08f, .08f, .11f), .006f, new Color(.62f, .64f, .8f), new Color(.38f, .34f, .32f), new Color(.14f, .11f, .1f));
        var sun = Sun(new Color(.85f, .85f, 1f), 2.0f, new Vector3(58, 170, 0)); root.GetComponent<StageLook>().sun = sun;
        const string D = "Assets/ThirdParty/Dungeon/Prefabs/";
        var floors = new[] { Load(D + "Floors/Floor_A.prefab") };   // Floor_B renders dark from above
        Underlay();
        var fl = new GameObject("Floor").transform; fl.SetParent(root);
        for (int ix = -1; ix <= 1; ix++) for (int iz = -8; iz <= 8; iz++) Place(floors[0], new Vector3(ix * 5.8f, 0, iz * 5.8f), 90 * rnd.Next(4), 1, true, fl);
        var walls = new[] { Load(D + "Walls/Wall_A.prefab"), Load(D + "Walls/Wall_B.prefab"), Load(D + "Walls/Wall_C.prefab") };
        for (int iz = -8; iz <= 8; iz++) { Place(walls[rnd.Next(3)], new Vector3(-9.3f, 0, iz * 5.86f), 0); Place(walls[rnd.Next(3)], new Vector3(9.3f, 0, iz * 5.86f), 180); }
        for (int ix = -1; ix <= 1; ix++) Place(walls[rnd.Next(3)], new Vector3(ix * 5.86f, 0, -49.6f), 90);
        var torch = Load("Assets/ThirdParty/PrisonCell/Prefabs/Props/Torch.prefab"); var fire = Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        int n = 0;
        for (float z = -42; z <= 42; z += 12)
            foreach (int sgn in new[] { -1, 1 })
            {
                var tp = new Vector3(sgn * 8.6f, 2.6f, z); var inward = new Vector3(-sgn, 0, 0);
                Place(torch, tp, Quaternion.LookRotation(inward).eulerAngles.y, 1.6f); var f = Place(fire, tp + Vector3.up * .55f, 0, .35f); if (f) f.transform.position = tp + Vector3.up * .6f;
                if (n++ % 2 == 0) PointLight(tp + inward * .8f, new Color(1, .55f, .22f), 10, 13);
            }
        var props = new[] { (D + "Props/Crate_A.prefab", 1f), (D + "Props/Barrel.prefab", .55f), ("Assets/Game/Models/Props/Barrel_01/Barrel_01_1k.gltf", 1.3f), (D + "Props/Crate_C.prefab", 1f) };
        for (int i = 0; i < 18; i++) { var (pp, sc) = props[rnd.Next(props.Length)]; Place(Load(pp), new Vector3((rnd.Next(2) * 2 - 1) * R(7.6f, 8.6f), 0, R(-44, 44)), R(0, 360), sc); }
        Static(root); return root;
    }

    // ======================= stage 4: river valley at golden hour =======================
    static Transform RiverStage()
    {
        NewRoot("Stage_river", new Color(.42f, .55f, .52f), .007f, new Color(.62f, .74f, .78f), new Color(.38f, .46f, .34f), new Color(.14f, .17f, .1f));
        var sun = Sun(new Color(1f, .9f, .74f), 2.1f, new Vector3(40, 160, 0)); root.GetComponent<StageLook>().sun = sun;
        Ground("RiverGround", new[] { ("leafy_grass", 3.5f), ("forest_ground_05", 4f), ("brown_mud_02", 3f), ("grass_path_3", 3f) }, (x, z, i) =>
        {
            float bed = Mathf.InverseLerp(0, -.25f, River.Bed(x, z)), path = PathMask(x, z) * (1 - bed), patch = Mathf.PerlinNoise(x * .09f + 4, z * .09f + 8);
            float bank = Mathf.InverseLerp(River.HalfW + 3, River.HalfW + .5f, Mathf.Abs(z - River.Z(x)));
            return i == 2 ? Mathf.Max(bed, bank * .7f) : i == 3 ? path : i == 1 ? (1 - bed) * (1 - path) * Mathf.SmoothStep(.55f, .75f, patch) : (1 - bed) * (1 - path) * (1 - bank * .7f) * (1 - Mathf.SmoothStep(.55f, .75f, patch));
        }, 5, River.Bed, 1.5f);
        // water: one big plane at the river level; the terrain hides it everywhere except the channel
        var water = GameObject.CreatePrimitive(PrimitiveType.Plane); Object.DestroyImmediate(water.GetComponent<Collider>()); water.name = "Water"; water.transform.SetParent(root);
        water.transform.position = new Vector3(0, River.WaterY, 0); water.transform.localScale = new Vector3(12, 1, 12);
        var wm = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Water.mat"); if (!wm) { wm = new Material(Shader.Find("CozyFishing/Water")); AssetDatabase.CreateAsset(wm, $"{Gen}/Water.mat"); }
        wm.SetColor("_Shallow", new Color(.5f, .78f, .72f)); wm.SetColor("_Deep", new Color(.12f, .34f, .38f)); wm.SetFloat("_Clarity", .6f); wm.SetFloat("_DepthScale", 1.4f);
        wm.SetVector("_Flow", new Vector4(.22f, .02f, 0, 0)); wm.SetFloat("_Glint", 2.2f); wm.SetFloat("_FoamWidth", .12f); EditorUtility.SetDirty(wm);
        water.GetComponent<Renderer>().sharedMaterial = wm;
        // grass everywhere except the water (GrassField skips the paths; the river bed hides the rest)
        Grass(new Color(.03f, .1f, .04f), new Color(.1f, .3f, .1f), new Color(.3f, .52f, .2f), .45f, .32f, 28).river = true;
        // wooden plank bridges
        var plank = Lit("Planks", "weathered_brown_planks", 1, .15f); var rope = Lit("BridgeRail", "bark_brown_02", 1, .05f, new Color(.55f, .45f, .35f));
        foreach (var bx in River.BridgeX)
        {
            float cz = River.Z(bx), len = River.HalfW * 2 + 3.4f; float slope = Mathf.Atan(.12f * 2.5f * Mathf.Cos(bx * .12f)) * Mathf.Rad2Deg;
            var br = new GameObject("Bridge").transform; br.SetParent(root); br.position = new Vector3(bx, 0, cz); br.rotation = Quaternion.Euler(0, -slope, 0);
            for (float t = -len / 2; t <= len / 2; t += .42f)
            {
                var pl = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(pl.GetComponent<Collider>()); pl.transform.SetParent(br, false);
                float arch = .22f * Mathf.Cos(t / len * Mathf.PI); pl.transform.localPosition = new Vector3(R(-.04f, .04f), arch, t); pl.transform.localScale = new Vector3(2.8f, .09f, .38f);
                pl.transform.localRotation = Quaternion.Euler(R(-2, 2), R(-3, 3), 0); pl.GetComponent<Renderer>().sharedMaterial = plank;
            }
            foreach (int sd in new[] { -1, 1 })
                for (float t = -len / 2; t <= len / 2 + .01f; t += len / 4)
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(post.GetComponent<Collider>()); post.transform.SetParent(br, false);
                    post.transform.localPosition = new Vector3(sd * 1.45f, .35f, t); post.transform.localScale = new Vector3(.12f, .55f, .12f); post.GetComponent<Renderer>().sharedMaterial = rope;
                }
            foreach (int sd in new[] { -1, 1 })
            {
                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(rail.GetComponent<Collider>()); rail.transform.SetParent(br, false);
                rail.transform.localPosition = new Vector3(sd * 1.45f, .82f, 0); rail.transform.localScale = new Vector3(.08f, .08f, len); rail.GetComponent<Renderer>().sharedMaterial = rope;
            }
        }
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        // lotus and reeds along the water, stones in the stream
        for (int i = 0; i < 46; i++)
        {
            float x = R(-26, 26); if (River.BridgeX.Any(b => Mathf.Abs(x - b) < 2.5f)) continue;
            float off = R(-1, 1) * R(.6f, River.HalfW - .3f), z = River.Z(x) + off;
            // (the lotus models render with an unsupported material here, so stones and reeds only)
            if (rnd.Next(2) == 0) Place(Load(N("weed_plant_02")), new Vector3(x, River.WaterY - .05f, z), R(0, 360), R(.6f, .9f));
            else Place(Load(N(rnd.Next(2) == 0 ? "rock_09" : "rock_07")), new Vector3(x, River.WaterY - .25f, z), R(0, 360), R(4, 9));
        }
        for (int i = 0; i < 30; i++)
        {
            float x = R(-28, 28); int sd = rnd.Next(2) * 2 - 1; float z = River.Z(x) + sd * R(River.HalfW + .3f, River.HalfW + 1.4f);
            if (River.BridgeX.Any(b => Mathf.Abs(x - b) < 2.2f)) continue;
            Place(Load(N(rnd.Next(3) == 0 ? "fern_02" : "weed_plant_02")), new Vector3(x, River.Bed(x, z) + .02f, z), R(0, 360), R(.7f, 1.1f));
        }
        Place(Load(N("wooden_sampan")), new Vector3(-17, River.WaterY - .05f, River.Z(-17) + .8f), 85, 1.0f);
        // trees, shrubs and flowers around the valley
        var trees = new[] { (N("island_tree_02"), 1.5f, 2.1f), (N("island_tree_01"), 1.4f, 2f), (N("jacaranda_tree"), .3f, .4f), (N("searsia_lucida"), 1.6f, 2.1f) };
        foreach (var q in Ring(44, 25, 44)) { var t = trees[rnd.Next(trees.Length)]; Place(Load(t.Item1), new Vector3(q.x, 0, q.y), R(0, 360), R(t.Item2, t.Item3)); }
        foreach (var q in Ring(36, 22.5f, 27, 0, .02f)) Place(Load(N(rnd.Next(3) == 0 ? "shrub_02" : "fern_02")), new Vector3(q.x, 0, q.y), R(0, 360), rnd.Next(3) == 0 ? R(.35f, .5f) : R(1, 1.5f));
        foreach (var q in Ring(30, 12, 22, 1.8f, .2f)) { if (Mathf.Abs(q.y - River.Z(q.x)) < River.HalfW + 2) continue; Place(Load(N("flower_empodium")), new Vector3(q.x, 0, q.y), R(0, 360), R(.8f, 1.3f)); }
        foreach (var q in Ring(8, 21, 25)) Place(Load(N(rnd.Next(2) == 0 ? "rock_moss_set_01" : "namaqualand_boulder_02")), new Vector3(q.x, -.15f, q.y), R(0, 360), R(.4f, .6f));
        foreach (var q in Ring(4, 9, 20, 2, .2f)) { if (Mathf.Abs(q.y - River.Z(q.x)) < River.HalfW + 3) continue; Place(Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Misc/CFXR3 Ambient Glows.prefab"), new Vector3(q.x, .6f, q.y)); }
        Static(root); return root;
    }
}
