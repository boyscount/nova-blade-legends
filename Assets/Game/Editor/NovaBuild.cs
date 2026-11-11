// Builds everything the game needs from the imported assets (run after NovaSetup.All):
//   Unity.exe -batchmode -quit -projectPath . -executeMethod NovaBuild.All
// Assets(): panel settings, materials, Catalog (models, effects, clip timings), enemy import settings
// Scene(): Assets/Game/Scenes/Main.unity with camera + post-processing and the three stage arenas
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    // PC picture: a sharpening step on the PC renderer (phones keep their own renderer without it) and a lighter blur behind the lobby hero
    [MenuItem("Nova Blade/Add PC Sharpening")]
    public static void PcSharpen()
    {
        const string mp = "Assets/Game/Generated/Sharpen.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(mp); if (!mat) { mat = new Material(Shader.Find("NovaBlade/Sharpen")); AssetDatabase.CreateAsset(mat, mp); }
        mat.SetFloat("_Amount", .35f); EditorUtility.SetDirty(mat);
        var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        if (!rd.rendererFeatures.OfType<FullScreenPassRendererFeature>().Any())
        {
            var fx = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>(); fx.name = "PC Sharpen"; fx.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing; fx.fetchColorBuffer = true; fx.requirements = ScriptableRenderPassInput.Color; fx.passMaterial = mat;
            AssetDatabase.AddObjectToAsset(fx, rd); AssetDatabase.TryGetGUIDAndLocalFileIdentifier(fx, out string _, out long id);
            var so = new SerializedObject(rd); var list = so.FindProperty("m_RendererFeatures"); var map = so.FindProperty("m_RendererFeatureMap");
            list.arraySize++; list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fx; map.arraySize++; map.GetArrayElementAtIndex(map.arraySize - 1).longValue = id; so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rd);
        }
        var dp = AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Gen}/LobbyDOF.asset");
        if (dp && dp.TryGet<DepthOfField>(out var dof)) { dof.gaussianStart.Override(9); dof.gaussianMaxRadius.Override(.7f); EditorUtility.SetDirty(dof); EditorUtility.SetDirty(dp); }
        AssetDatabase.SaveAssets(); Debug.Log("[NovaBuild] PC sharpening on");
    }
    public static void PcSharpenAndWin() { PcSharpen(); BuildWin(); }
    // experiment: one hero's skins imported at full size for the Windows player (-executeMethod NovaBuild.Skin2048AndWin)
    public static void Skin2048AndWin() { HdSkins(); BuildWin(); }
    // heroes regenerated with 2048 skins keep them on PC (web PC build and Windows); the phone build caps them through WebSlim
    public static readonly string[] HdHeroes = { "warrior", "samurai", "sunpriest", "sorceress", "darkmage", "mage", "hood", "harpy", "elf", "ninja", "spartan", "lyra", "rowan", "tenzin" };
    static void HdSkins()
    {
        foreach (var id in HdHeroes) foreach (var guid in AssetDatabase.FindAssets(id + "_tex t:Texture2D", new[] { "Assets/Game/Models/Heroes" }))
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)); var ps = ti.GetPlatformTextureSettings("Standalone");
            if (ti.maxTextureSize == 2048 && ps.overridden && ps.maxTextureSize == 2048 && ti.GetDefaultPlatformTextureSettings().maxTextureSize == 2048) continue;
            var def = ti.GetDefaultPlatformTextureSettings(); def.maxTextureSize = 2048; ti.SetPlatformTextureSettings(def); ti.maxTextureSize = 2048; ps.overridden = true; ps.maxTextureSize = 2048; ti.SetPlatformTextureSettings(ps); ti.SaveAndReimport();
        }
    }
    // new clips in the class sets: rebuild the override controllers and the clip timings, then the Windows player
    public static void AnimsAndWin()
    {
        NovaSetup.AnimSets(); var cat = AssetDatabase.LoadAssetAtPath<Catalog>("Assets/Game/Resources/Catalog.asset"); cat.clips = ClipTimings(); EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets(); BuildWin();
    }
    public static void HdHeroesAndWin() { NovaSetup.NewHeroes(); HdSkins(); BuildWin(); }
    [MenuItem("Nova Blade/Rebuild Scene Only")] static void SceneMenu() { Scene(); Debug.Log("[NovaBuild] scene rebuilt"); }
    // scene + both players in one batch run: -executeMethod NovaBuild.SceneAndPlayers
    public static void SceneAndPlayers() { Scene(); BuildWin(); BuildWebGL(); }

    // Windows player for local testing: Builds/Win/NovaBlade.exe (run with -novashot <dir> for automatic screenshots)
    // Web build for phones and desktop browsers: Builds/WebGL (ASTC textures for phones, full-screen template)
    [MenuItem("Nova Blade/Build WebGL (phones)")]
    // web build only: scenery textures at 512 px and character/other textures at most 1024 (WebGL platform override),
    // compressed meshes for the Fantasy Kingdom set; keeps each file of the web build under GitHub Pages' 100 MB limit
    // slim = the phone web build: textures capped (scenery 512, the rest 1024) under the WebGL platform override;
    // !slim = the PC web build: the overrides come off, so textures go in at their own size
    static void WebSlim(bool slim)
    {
        int n = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid); if (path.Contains("/Resources/UI/") || path.Contains("NovaBlade_SkillIcons") || path.Contains("/Game/Skybox/")) continue;
            if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
            if (!slim) { var po = ti.GetPlatformTextureSettings("WebGL"); if (po.overridden) { po.overridden = false; ti.SetPlatformTextureSettings(po); ti.SaveAndReimport(); n++; } continue; }
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

    // Two web builds, picked by the page: PC browsers get Builds/WebGL_PC (DXT textures at full size, deployed under /pc/),
    // phones get Builds/WebGL (ASTC, capped textures). The PC one is built first so the project ends on the phone settings.
    public static void BuildWebGL()
    {
        BuildWeb(false); BuildWeb(true);
    }
    public static void BuildWebPhone() => BuildWeb(true);
    // a test round for phones: new heroes picked up, faster Gzip instead of Brotli (see CHARACTER-PERF-RULES.md)
    public static void BuildWebPhoneTest() { NovaSetup.NewHeroes(); HdSkins(); fastCompress = true; try { BuildWeb(true); } finally { fastCompress = false; } }
    static bool fastCompress, phoneScene;
    // phones cannot hold every stage in memory: the Open World zone and its house stay out of the phone build until the zone is lighter
    [UnityEditor.Callbacks.PostProcessScene(0)]
    static void StripForPhone()
    {
        if (!BuildPipeline.isBuildingPlayer || !phoneScene) return;
        var main = Object.FindAnyObjectByType<Main>(); if (!main) return;
        foreach (var r in main.stageRoots.Where(r => r && r.name is "Stage_wilds" or "Stage_house").ToArray())   // Sky Lanes is in the phone build too (lighter there: SkyLanesPhone)
            Object.DestroyImmediate(r.gameObject);   // the Sky Court stays: phones reach its duels from the city gate
        main.stageRoots = main.stageRoots.Where(r => r).ToArray();
        Debug.Log("[NovaBuild] phone scene: zone and house left out");
    }
    public static void BuildWebPc() => BuildWeb(false);
    public static void BuildWebPcTest() { fastCompress = true; try { BuildWeb(false); } finally { fastCompress = false; } }
    static void BuildWeb(bool phone)
    {
        WebSlim(phone); Towers(Cat(), false); LazyCatalog();
        PlayerSettings.WebGL.template = "PROJECT:NovaBlade";
        PlayerSettings.WebGL.compressionFormat = fastCompress ? WebGLCompressionFormat.Gzip : WebGLCompressionFormat.Brotli; phoneScene = phone; PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true; PlayerSettings.WebGL.memoryGrowthMode = WebGLMemoryGrowthMode.Geometric; PlayerSettings.WebGL.maximumMemorySize = 2048; PlayerSettings.WebGL.initialMemorySize = phone ? 512 : 32;   // phones: start big, so the heap does not have to be copied to a larger one while a stage loads (iOS closes the tab if that peak is too high)
        PlayerSettings.WebGL.nameFilesAsHashes = true;
        EditorUserBuildSettings.webGLBuildSubtarget = phone ? WebGLTextureSubtarget.ASTC : WebGLTextureSubtarget.DXT;
        var dir = phone ? "Builds/WebGL" : "Builds/WebGL_PC";
        // a web build removes the code files of the other web build made before it (their hashed names can differ): keep them and put them back
        var otherBuild = phone ? "Builds/WebGL_PC/Build" : "Builds/WebGL/Build"; var pcCode = Directory.Exists(otherBuild) ? Directory.GetFiles(otherBuild).Where(f => !f.Contains(".data.")).ToDictionary(f => f, File.ReadAllBytes) : null;
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Game/Scenes/Main.unity" }, dir, BuildTarget.WebGL, BuildOptions.None);
        Debug.Log($"[NovaBuild] webgl {(phone ? "phone" : "PC")} build {r.summary.result} {r.summary.totalSize / 1e6:0} MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        SplitData(dir); SkinBundles(BuildTarget.WebGL, dir + "/skins");
        if (pcCode != null)
        {
            foreach (var f in Directory.GetFiles(otherBuild).Where(f => !f.Contains(".data.") && !pcCode.ContainsKey(f))) File.Delete(f);
            foreach (var kv in pcCode) File.WriteAllBytes(kv.Key, kv.Value);
            CheckPage(phone ? "Builds/WebGL_PC" : "Builds/WebGL");
        }
        CheckPage(dir);
    }

    // every file the page asks for must be in the build folder, otherwise it hangs on the loading screen once deployed
    static void CheckPage(string dir)
    {
        var html = File.ReadAllText(dir + "/index.html"); int parts = int.Parse(Regex.Match(html, "var dataParts = ([0-9]+)").Groups[1].Value);
        var missing = new List<string>();
        foreach (Match m in Regex.Matches(html, @"""Build/([^""]+)"""))
        {
            var name = m.Groups[1].Value; bool cut = parts > 0 && name.Contains(".data");
            for (int i = 0; i < (cut ? parts : 1); i++) { var f = cut ? $"{name}.part{i}" : name; if (!File.Exists($"{dir}/Build/{f}")) missing.Add(f); }
        }
        if (missing.Count == 0) { Debug.Log($"[NovaBuild] page check {dir}: OK"); return; }
        Debug.LogError($"[NovaBuild] page check {dir}: missing {string.Join(", ", missing)}"); EditorApplication.Exit(1);
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
    // moves every catalog prefab out to Resources/Cat and clears the reference, so the catalog itself holds only names and loading it pulls in nothing
    public static void LazyCatalog()
    {
        var cat = Cat(); const string Dir = "Assets/Game/Resources/Cat"; Directory.CreateDirectory(Dir); int n = 0;
        void Move(List<Catalog.Entry> list, string prefix)
        {
            foreach (var e in list)
            {
                if (!e.prefab) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(e.prefab); if (!inst) inst = Object.Instantiate(e.prefab);
                PrefabUtility.SaveAsPrefabAsset(inst, $"{Dir}/{prefix}{e.id}.prefab"); Object.DestroyImmediate(inst); e.prefab = null; n++;
            }
        }
        Move(cat.models, "m_"); Move(cat.fx, "fx_");
        EditorUtility.SetDirty(cat); AssetDatabase.SaveAssets(); Debug.Log($"[NovaBuild] lazy catalog: {n} moved");
    }
    static Catalog Cat() => AssetDatabase.LoadAssetAtPath<Catalog>("Assets/Game/Resources/Catalog.asset");

    [MenuItem("Nova Blade/Build Windows exe")]
    public static void BuildWin()
    {
        Towers(Cat(), false); LazyCatalog();   // the decimated PC towers lose their UVs (shattered look), so every build uses the hand-made mobile set
        var r = BuildPipeline.BuildPlayer(new[] { "Assets/Game/Scenes/Main.unity" }, "Builds/Win/NovaBlade.exe", BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[NovaBuild] win build {r.summary.result} {r.summary.totalSize / 1e6:0} MB");
        if (r.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
        SkinBundles(BuildTarget.StandaloneWindows64, "Builds/Win/NovaBlade_Data/StreamingAssets/skins");
    }
    // the classic skins, one AssetBundle each (SkinPacks loads them when worn), built for the player just made
    static void SkinBundles(BuildTarget target, string outDir)
    {
        var builds = Directory.GetFiles(NovaSetup.SkinPackDir, "Hero_*.prefab").Select(p => new AssetBundleBuild { assetBundleName = "hero_" + Path.GetFileNameWithoutExtension(p).Substring(5).ToLowerInvariant(), assetNames = new[] { p.Replace(Path.DirectorySeparatorChar, '/') } }).ToArray();
        if (builds.Length == 0) { Debug.Log("[NovaBuild] no skin packs"); return; }
        if (Directory.Exists(outDir)) Directory.Delete(outDir, true); Directory.CreateDirectory(outDir);
        var m = BuildPipeline.BuildAssetBundles(outDir, builds, BuildAssetBundleOptions.ChunkBasedCompression, target);
        long size = Directory.GetFiles(outDir, "hero_*").Where(f => !f.EndsWith(".manifest")).Sum(f => new FileInfo(f).Length);
        Debug.Log($"[NovaBuild] skin packs {(m ? m.GetAllAssetBundles().Length : 0)} for {target}: {size / 1e6:0.0} MB in {outDir}");
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
        foreach (var w in Data.Wings) { var pf = WingPrefab(w); if (!pf) Debug.LogError("[NovaBuild] missing wings " + w.id); cat.models.Add(new Catalog.Entry { id = "wing_" + w.id, prefab = pf }); }
        foreach (var b in new[] { "reaper", "captain" }) M(b, $"Assets/Game/Models/Bosses/{b}.glb");
        foreach (var w in Data.Swords) M(w.id, $"Assets/Game/Models/Weapons/{w.id}.glb");
        foreach (var w in Data.Shields) M(w.id, $"Assets/Game/Models/Weapons/{w.id}.glb");
        foreach (var f in Data.Foes.Where(f => f.model.StartsWith("q_"))) M(f.model, $"Assets/Game/Models/Foes/{f.model}.glb");
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
        const string V = "Assets/Vefects/Trails VFX URP/VFX/Particles/", HV = "Assets/Hovl Studio/Magic effects pack/Prefabs/", SX = "Assets/Matthew Guz/Slash Effects FREE/Prefab/";
        foreach (var (id, p) in new[] {
            ("vx_fire", V + "VFX_Trail_Fire"), ("vx_void", V + "VFX_Trail_Void"), ("vx_cosmos", V + "VFX_Trail_Cosmos"), ("vx_nature", V + "VFX_Trail_Nature"),
            ("vx_ice", V + "VFX_Trail_Ice"), ("vx_electric", V + "VFX_Trail_Electric"), ("vx_water", V + "VFX_Trail_Water"), ("vx_dark", V + "VFX_Trail_Dark"),
            ("mx_heal_circle", HV + "Magic circles/Healing circle"), ("mx_healing", HV + "Character auras/Healing"), ("mx_buff", HV + "Character auras/Buff"),
            ("mx_shield", HV + "Magic shields/Magic shield blue"), ("mx_shield_gold", HV + "Magic shields/Magic shield yellow"), ("mx_ground", HV + "AoE effects/Ground AOE explosion"),
            ("mx_holy_hit", HV + "Hits and explosions/Holy hit"), ("mx_electro_hit", HV + "Hits and explosions/Electro hit"), ("mx_magic_circle", HV + "Magic circles/Magic circle"),
            // slash effects: light, second and heavy swings, multi-hit, critical sparkle, a single mesh arc
            ("sx_basic", SX + "Basic Slash Blue"), ("sx_double", SX + "Double Slash"), ("sx_explode", SX + "Explosion Slash "),
            ("sx_multi", SX + "Multiple Slash 2 "), ("sx_shiny", SX + "Shiny Slash "), ("sx_mesh", SX + "1Slash Mesh"),
            ("mx_meteors", HV + "AoE effects/Meteors AOE"), ("mx_snow", HV + "AoE effects/Snow AOE"), ("mx_dust", HV + "Smoke effects/Dust ground"),
            ("mx_charge", HV + "Slash effects/Charge slash red"), ("mx_spark_y", HV + "Sparks/Sparks explode yellow"), ("mx_spark_w", HV + "Sparks/Sparks explode white"),
            ("aoe_orange", HV + "AoE effects/AoE slash orange"), ("aoe_blue", HV + "AoE effects/AoE slash blue"), ("aoe_green", HV + "AoE effects/AoE slash green") })
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
            var clip = NovaSetup.Clip(k); if (!clip) continue; float best = 0, hit = clip.length * .5f; Vector3 prev = default;
            var g = PlayableGraph.Create(); var o = AnimationPlayableOutput.Create(g, "o", an); var cp = AnimationClipPlayable.Create(g, clip); o.SetSourcePlayable(cp);
            for (float t = 0; t <= clip.length; t += 1 / 30f)
            {
                cp.SetTime(t); g.Evaluate(); var p = hand.position;
                if (t > 0) { float sp = (p - prev).magnitude; if (sp > best) { best = sp; hit = t; } }
                prev = p;
            }
            g.Destroy();
            list.Add(new Catalog.ClipInfo { id = k, length = clip.length, hit = hit, speed = k is "run" or "walk" ? Gait(an, inst.transform, clip) : 0 });
            Debug.Log($"[NovaBuild] clip {k} len {clip.length:0.00} hit {hit:0.00}");
        }
        var limbs = new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand, HumanBodyBones.RightFoot, HumanBodyBones.LeftFoot }.Select(an.GetBoneTransform).ToArray();
        foreach (var (set, states) in NovaSetup.Sets) foreach (var (k, (file, _)) in states)
        {
            var clip = NovaSetup.SetClip(file); if (!clip) continue;   // not downloaded yet
            float best = 0, hit = clip.length * .5f; var prev = new Vector3[limbs.Length];
            var g = PlayableGraph.Create(); var o = AnimationPlayableOutput.Create(g, "o", an); var cp = AnimationClipPlayable.Create(g, clip); o.SetSourcePlayable(cp);
            for (float t = 0; t <= clip.length; t += 1 / 30f)
            {
                cp.SetTime(t); g.Evaluate();
                for (int i = 0; i < limbs.Length; i++) { var p = limbs[i].position; if (t > 0) { float sp = (p - prev[i]).magnitude; if (sp > best) { best = sp; hit = t; } } prev[i] = p; }
            }
            g.Destroy();
            list.Add(new Catalog.ClipInfo { id = set + ":" + k, length = clip.length, hit = hit, speed = k is "run" or "walk" ? Gait(an, inst.transform, clip) : 0 });
            Debug.Log($"[NovaBuild] clip {set}:{k} len {clip.length:0.00} hit {hit:0.00}");
        }
        Object.DestroyImmediate(inst);
        return list;
    }

    // A gait clip played in place: each foot sweeps back by one stride while planted, and a cycle has two steps,
    // so the body travels 2 x (front-to-back reach of a foot) per cycle.
    static float Gait(Animator an, Transform root, AnimationClip clip)
    {
        var hips = an.GetBoneTransform(HumanBodyBones.Hips); var feet = new[] { an.GetBoneTransform(HumanBodyBones.LeftFoot), an.GetBoneTransform(HumanBodyBones.RightFoot) };
        var g = PlayableGraph.Create(); var o = AnimationPlayableOutput.Create(g, "o", an); var cp = AnimationClipPlayable.Create(g, clip); o.SetSourcePlayable(cp);
        float reach = 0;
        foreach (var f in feet)
        {
            float lo = 1e9f, hi = -1e9f;
            for (float t = 0; t <= clip.length; t += 1 / 60f) { cp.SetTime(t); g.Evaluate(); float z = Vector3.Dot(f.position - hips.position, root.forward); lo = Mathf.Min(lo, z); hi = Mathf.Max(hi, z); }
            reach += (hi - lo) / 2;
        }
        g.Destroy();
        float sp = 2 * reach / Mathf.Max(.1f, clip.length); Debug.Log($"[NovaBuild] gait {clip.name} {sp:0.00} m/s"); return sp;
    }

    // Wings: the glTF model with its materials swapped for URP Lit that glows with its own texture in the wings' colour
    // (HDR emission, so the bloom haloes it), double-sided, saved as a prefab for the catalog.
    static GameObject WingPrefab(WingDef w)
    {
        var src = Model($"Assets/Game/Models/Wings/wing_{w.id}.glb"); if (!src) return null;
        var inst = (GameObject)Object.Instantiate(src); inst.name = "wing_" + w.id; int i = 0;
        float boost = w.id == "death" ? 3.2f : w.id == "angel" ? 1.6f : 2.4f;   // dark membranes need more to read at night
        foreach (var r in inst.GetComponentsInChildren<Renderer>())
        {
            var mats = r.sharedMaterials;
            for (int k = 0; k < mats.Length; k++)
            {
                var old = mats[k]; var tex = old && old.HasProperty("baseColorTexture") ? old.GetTexture("baseColorTexture") : old ? old.mainTexture : null;
                var nrm = old && old.HasProperty("normalTexture") ? old.GetTexture("normalTexture") : null;
                mats[k] = Mat($"{Gen}/Wing_{w.id}_{i++}.mat", "Universal Render Pipeline/Lit", m =>
                {
                    m.shader = Shader.Find("Universal Render Pipeline/Lit");
                    m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", .45f); m.SetFloat("_Cull", 0);
                    if (nrm) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
                    m.SetTexture("_EmissionMap", tex); m.SetColor("_EmissionColor", w.glow * boost); m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                });
            }
            r.sharedMaterials = mats;
        }
        var path = $"{Gen}/Wing_{w.id}.prefab"; var pf = PrefabUtility.SaveAsPrefabAsset(inst, path); Object.DestroyImmediate(inst); return pf;
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
        {   // see-through scenery (OcclusionFader): a transparent URP Lit material in Resources keeps that shader variant in the build
            var fm = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "FadeLit" };
            fm.SetFloat("_Surface", 1); fm.SetFloat("_Blend", 0); fm.SetFloat("_ZWrite", 0); fm.SetFloat("_AlphaClip", 0);
            fm.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); fm.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            fm.SetOverrideTag("RenderType", "Transparent"); fm.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); fm.renderQueue = 3000; fm.SetFloat("_Smoothness", .1f);
            fm.SetColor("_BaseColor", new Color(1, 1, 1, .3f));
            const string fp = "Assets/Game/Resources/FadeLit.mat"; if (File.Exists(fp)) AssetDatabase.DeleteAsset(fp); AssetDatabase.CreateAsset(fm, fp);
        }
        {   // lobby depth of field: the hero sharp, the forest behind softly out of focus (LobbyNight sets the weight)
            var dofGo = new GameObject("LobbyDOF"); var dv = dofGo.AddComponent<Volume>(); dv.isGlobal = true; dv.priority = 5; dv.weight = 0;
            var dp = ScriptableObject.CreateInstance<VolumeProfile>(); var dof = dp.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian); dof.gaussianStart.Override(9); dof.gaussianEnd.Override(17); dof.gaussianMaxRadius.Override(.7f); dof.highQualitySampling.Override(true);
            var dpath = $"{Gen}/LobbyDOF.asset"; if (File.Exists(dpath)) AssetDatabase.DeleteAsset(dpath); AssetDatabase.CreateAsset(dp, dpath); AssetDatabase.AddObjectToAsset(dof, dp); dv.sharedProfile = dp;
        }
        var volGo = new GameObject("Post"); var vol = volGo.AddComponent<Volume>(); vol.isGlobal = true;
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var bloom = prof.Add<Bloom>(true); bloom.intensity.Override(.4f); bloom.threshold.Override(1.05f); bloom.scatter.Override(.65f);
        var tm = prof.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.ACES);
        var ca = prof.Add<ColorAdjustments>(true); ca.postExposure.Override(0); ca.contrast.Override(6); ca.saturation.Override(3);   // gentle on the eyes over long sessions
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(6); wb.tint.Override(2);
        var smh = prof.Add<ShadowsMidtonesHighlights>(true); smh.shadows.Override(new Vector4(.92f, .96f, 1.08f, 0)); smh.highlights.Override(new Vector4(1.05f, 1.02f, .95f, 0));
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(.22f); vg.smoothness.Override(.5f);
        var lgg = prof.Add<LiftGammaGain>(true); lgg.lift.Override(new Vector4(.98f, .99f, 1.04f, 0));
        var pp = $"{Gen}/Post.asset"; if (File.Exists(pp)) AssetDatabase.DeleteAsset(pp); AssetDatabase.CreateAsset(prof, pp);
        foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof);
        vol.sharedProfile = prof;
        // game
        var mainGo = new GameObject("Game"); var main = mainGo.AddComponent<Main>(); main.cam = cam;
        main.cityRoot = null;
        main.stageRoots = new[] { Forest(), RiverStage(), KingdomSquare(), ValleyStage(), Castle(), LaneStage(), Lane3Stage(), WildsStage(), HouseStage(), SkyStage(), SkyLanesStage() };   // 360° photo stages (PlazaStage, MirrorStage, ValleyCliff) are kept but out of the game   // Data.Themes order, then the 1-lane and 3-lane maps   // Data.Themes order, lane last   // story themes in Data.Themes order, lane last
        foreach (var sr in main.stageRoots) NoCartoonFx(sr);
        main.cityRoot = CityStage(main); main.cityRoot.gameObject.SetActive(false);
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
            tl.diffuseRemapMax = l.tex == "leafy_grass" ? (name == "WildsGround" ? new Vector4(.5f, 1.05f, .62f, 1) : new Vector4(.78f, .9f, .68f, 1)) : Vector4.one;   // greener ground under the meadow
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
    // Stages made from one 360° photo (Skybox AI samples): the photo is projected back onto a bowl around the arena
    // (NovaBlade/PanoProject), so the ground under the fight and everything all round are the photo itself, keeping their
    // depth as the camera moves. The battle camera drops to a three-quarter view on these stages so the horizon shows.
    // folder: under Assets/Game/Skybox; h0: how high the photo was taken (sets the scale); ripple > 0: the ground is water.
    static void Pano(string id, string folder, float h0, float exposure, float rotate, float ripple = 0)
    {
        root.GetComponent<StageLook>().lowCam = true;
        var pano = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Skybox/" + folder }).Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();
        if (pano == null) { Debug.LogError("[NovaBuild] panorama missing: " + folder); return; }
        if (AssetImporter.GetAtPath(pano) is TextureImporter ti)
        {   // full size on PC, 4096 on the web; repeat across the seam, trilinear + anisotropic for the ground seen at a slant
            ti.maxTextureSize = 8192; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp; ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear;
            ti.npotScale = TextureImporterNPOTScale.None; ti.textureCompression = TextureImporterCompression.CompressedHQ;
            var web = ti.GetPlatformTextureSettings("WebGL"); web.overridden = true; web.maxTextureSize = 4096; web.format = TextureImporterFormat.Automatic; web.textureCompression = TextureImporterCompression.Compressed; ti.SetPlatformTextureSettings(web);
            ti.SaveAndReimport();
        }
        var mat = Mat($"{Gen}/Pano_{id}.mat", "NovaBlade/PanoProject", m =>
        {
            m.shader = Shader.Find("NovaBlade/PanoProject"); m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(pano));
            m.SetVector("_Center", new Vector4(0, h0, 0, 0)); m.SetFloat("_Rotate", rotate); m.SetFloat("_Exposure", exposure); m.SetFloat("_ShadowDark", .45f); m.SetFloat("_Ripple", ripple);
        });
        var bowl = MeshObj("PanoBowl", PanoBowl(), mat, Vector3.zero, 0, 1); var br = bowl.GetComponent<MeshRenderer>(); br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; br.receiveShadows = true;
    }
    // the bowl: rings out to the rim (denser near the middle where the fight is), a wall up the rim, a cap on top
    static Mesh panoBowl;
    static Mesh PanoBowl()
    {
        if (panoBowl) return panoBowl;
        const float Rb = 90, Top = 80; var prof = new List<Vector2>();
        for (int k = 0; k <= 40; k++) { float t = k / 40f; prof.Add(new Vector2(Rb * t * t, 0)); }
        for (int k = 1; k <= 16; k++) prof.Add(new Vector2(Rb, Top * k / 16f));
        for (int k = 1; k <= 8; k++) prof.Add(new Vector2(Rb * (1 - k / 8f), Top));
        const int SEG = 160; var verts = new List<Vector3>(); var tris = new List<int>();
        for (int s = 0; s <= SEG; s++) { float a = s / (float)SEG * Mathf.PI * 2; foreach (var p in prof) verts.Add(new Vector3(Mathf.Sin(a) * p.x, p.y, Mathf.Cos(a) * p.x)); }
        int P = prof.Count;
        for (int s = 0; s < SEG; s++) for (int k = 0; k < P - 1; k++) { int a = s * P + k, b = a + P; tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 }); }
        var mesh = new Mesh { name = "PanoBowl", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        var mp = Gen + "/PanoBowl.asset"; if (File.Exists(mp)) AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp); return panoBowl = mesh;
    }
    // straight below the photo's camera the picture smears into a swirl: a round stone dais covers it
    static void PanoDais(string id, Color top, Color edge)
    {
        var stone = Lit(id + "Stone", "rock_face_04", 2, .35f, top); var rim = Lit(id + "Rim", "rock_face_04", 2, .35f, edge);
        MeshObj("dais_rim", Combine(id + "DaisRim", (PrimitiveType.Cylinder, T(0, .05f, 0, 6.6f, .05f, 6.6f))), rim, Vector3.zero, 0, 1);
        MeshObj("dais", Combine(id + "Dais", (PrimitiveType.Cylinder, T(0, .1f, 0, 5.8f, .1f, 5.8f))), stone, Vector3.zero, 0, 1);
    }

    // City plaza at night ("City Central Square Night"): moonlight for shadows, a brazier on the dais, warm lamp light on the
    // fighters to match the street lamps in the photo.
    static Transform PlazaStage()
    {
        NewRoot("Stage_plaza", new Color(.08f, .09f, .16f), .003f, new Color(.38f, .44f, .7f), new Color(.5f, .42f, .32f), new Color(.2f, .17f, .14f));
        var sun = Sun(new Color(.7f, .78f, 1f), .9f, new Vector3(55, 200, 0)); root.GetComponent<StageLook>().sun = sun;
        Pano("plaza", "City Central Square Night", 6, 1.45f, .5f);
        PanoDais("Plaza", new Color(1.3f, 1.18f, 1.05f), new Color(.95f, .86f, .76f));
        var pit = Load("Assets/Game/Models/Props/stone_fire_pit/stone_fire_pit_1k.gltf"); if (pit) Fit(pit, new Vector3(0, .2f, 0), 0, .35f);
        var fireFx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        if (fireFx) { var f = (GameObject)PrefabUtility.InstantiatePrefab(fireFx, root); f.transform.position = new Vector3(0, .35f, 0); f.transform.localScale *= .4f; }
        PointLight(new Vector3(0, 1.4f, 0), new Color(1, .6f, .3f), 10, 9);
        for (int i = 0; i < 6; i++) { float a = (i + .5f) / 6 * Mathf.PI * 2; PointLight(new Vector3(Mathf.Cos(a) * 15, 3.2f, Mathf.Sin(a) * 15), new Color(1, .72f, .42f), 2.6f, 12); }
        return root;
    }

    // Mirror sky lake ("Partly Cloudy Open Sky"): a still, flooded plain that mirrors the clouds, a far strip of grass on
    // the horizon. The fight is on the water itself (rippling), round a pale stone dais; bright midday sun from the photo's sun.
    static Transform MirrorStage()
    {
        NewRoot("Stage_mirror", new Color(.62f, .74f, .92f), .004f, new Color(.62f, .74f, 1f), new Color(.7f, .72f, .68f), new Color(.4f, .45f, .5f));
        var sun = Sun(new Color(1f, .96f, .88f), 1.6f, new Vector3(38, 115, 0)); root.GetComponent<StageLook>().sun = sun;
        Pano("mirror", "Partly Cloudy Open Sky", 5, 1.0f, .5f, 1);
        PanoDais("Mirror", new Color(1.25f, 1.25f, 1.3f), new Color(.85f, .88f, .95f));
        return root;
    }

    // Clifftop valley ("valley.jpg"): a rock slab above a canyon at golden hour; the low sun from the photo's side.
    static Transform ValleyCliff()
    {
        NewRoot("Stage_cliff", new Color(.55f, .62f, .78f), .003f, new Color(.55f, .66f, .95f), new Color(.75f, .6f, .45f), new Color(.35f, .26f, .2f));
        var sun = Sun(new Color(1f, .82f, .6f), 1.5f, new Vector3(28, 90, 0)); root.GetComponent<StageLook>().sun = sun;
        Pano("cliff", "Valley", 4, 1.05f, .35f);
        PanoDais("Cliff", new Color(1.05f, .9f, .78f), new Color(.75f, .6f, .5f));
        return root;
    }

    // stages keep only 3D effects: every cartoon-pack flame becomes a Flame (soft particle fire) of the same size, and the
    // cartoon "ambient glow" sprites are removed
    static void NoCartoonFx(Transform stage)
    {
        foreach (var t in stage.GetComponentsInChildren<Transform>(true).ToArray())
        {
            if (!t || !PrefabUtility.IsOutermostPrefabInstanceRoot(t.gameObject)) continue;
            var src = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject); if (src == null || !src.Contains("/CartoonFX/")) continue;
            if (src.EndsWith("CFXR Fire.prefab"))
            {
                var f = new GameObject("Flame").transform; f.SetParent(t.parent, false); f.SetPositionAndRotation(t.position, Quaternion.identity); f.localScale = t.localScale * 2.5f;
                f.gameObject.AddComponent<Flame>();
            }
            Object.DestroyImmediate(t.gameObject);
        }
    }

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
    // a bridge's walkable deck: its long axis from the model's spread, then the top of the middle strip along it
    static void Deck(GameObject b)
    {
        var pts = b.GetComponentsInChildren<MeshFilter>().Where(mf => mf.sharedMesh).SelectMany(mf => mf.sharedMesh.vertices.Select(v => mf.transform.TransformPoint(v))).ToArray();
        if (pts.Length < 10) return;
        var c = pts.Aggregate(Vector3.zero, (s, p) => s + p) / pts.Length;
        float xx = 0, zz = 0, xz = 0; foreach (var p in pts) { float dx = p.x - c.x, dz = p.z - c.z; xx += dx * dx; zz += dz * dz; xz += dx * dz; }
        float ang = .5f * Mathf.Atan2(2 * xz, xx - zz); var axis = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)); var side = new Vector3(-axis.z, 0, axis.x);
        float a0 = pts.Min(p => Vector3.Dot(p - c, axis)), a1 = pts.Max(p => Vector3.Dot(p - c, axis)), s0 = pts.Min(p => Vector3.Dot(p - c, side)), s1 = pts.Max(p => Vector3.Dot(p - c, side));
        var mid = c + axis * ((a0 + a1) / 2) + side * ((s0 + s1) / 2); float hl = (a1 - a0) / 2, hw = (s1 - s0) / 2;
        // deck top: rays straight down the middle line onto temporary colliders on the bridge's own meshes
        var tmp = b.GetComponentsInChildren<MeshFilter>().Where(mf => mf.sharedMesh).Select(mf => { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; return mc; }).ToList();
        Physics.SyncTransforms();
        const int N = 16; var h = new float[N];
        for (int i = 0; i < N; i++)
        {
            var at = mid + axis * (-hl + 2 * hl * (i + .5f) / N); float best = float.NaN;
            foreach (var off in new[] { 0f, -.15f, .15f })
                foreach (var hit in Physics.RaycastAll(at + side * off * hw + Vector3.up * 20, Vector3.down, 40))
                    if (tmp.Contains(hit.collider) && (float.IsNaN(best) || hit.point.y > best) && Mathf.Abs(Vector3.Dot(hit.point - mid, side)) < hw * .5f) best = hit.point.y;
            h[i] = best;
        }
        foreach (var mc in tmp) Object.DestroyImmediate(mc);
        for (int i = 1; i < N; i++) if (float.IsNaN(h[i])) h[i] = h[i - 1];          // gaps: the nearest sample
        for (int i = N - 2; i >= 0; i--) if (float.IsNaN(h[i])) h[i] = h[i + 1];
        if (float.IsNaN(h[0])) return;
        // ramps of five more steps at both ends, from the ground up to the deck, so nobody pops up onto it
        const int K = 5; float step = 2 * hl / N; var hh = new float[N + 2 * K];
        for (int i = 0; i < K; i++) { hh[i] = h[0] * i / K; hh[N + 2 * K - 1 - i] = h[N - 1] * i / K; }
        for (int i = 0; i < N; i++) hh[K + i] = h[i];
        var d = b.AddComponent<BridgeDeck>(); d.center = mid; d.axis = axis; d.side = side; d.halfLen = hl + K * step; d.halfWid = hw * .6f; d.h = hh; h = hh;
        Debug.Log($"[NovaBuild] bridge deck len {2 * hl:0.0} width {2 * hw:0.0} heights {string.Join(",", h.Select(y => y.ToString("0.00")))}");
    }
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
    // The Sakura Garden city, added to the saved scene without rebuilding the stages
    // puts the Open World zone into the saved scene without rebuilding the rest, then the Windows player
    public static void AddWildsAndWin()
    {
        rnd = new System.Random(31);
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity"); var main = Object.FindAnyObjectByType<Main>();
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name is "Stage_wilds" or "Stage_house" or "Stage_sky")) Object.DestroyImmediate(old);
        var active = main.stageRoots.FirstOrDefault(r => r && r.gameObject.activeSelf);
        var w = WildsStage(); NoCartoonFx(w); w.gameObject.SetActive(false); var hs = HouseStage(); NoCartoonFx(hs); hs.gameObject.SetActive(false); var sk = SkyStage(); NoCartoonFx(sk); sk.gameObject.SetActive(false);
        main.stageRoots = main.stageRoots.Where(r => r && r.name is not ("Stage_wilds" or "Stage_house" or "Stage_sky")).Append(w).Append(hs).Append(sk).ToArray();
        if (active) active.GetComponent<StageLook>().Apply(); EditorUtility.SetDirty(main); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        BuildWin();
    }
    public static void AddCityAndWin()
    {
        NovaSetup.BuildNpcs(); rnd = new System.Random(23);
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity"); var main = Object.FindAnyObjectByType<Main>();
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "Stage_city")) Object.DestroyImmediate(old);
        var active = main.stageRoots.FirstOrDefault(r => r && r.gameObject.activeSelf); main.cityRoot = CityStage(main); main.cityRoot.gameObject.SetActive(false);
        if (active) active.GetComponent<StageLook>().Apply(); EditorUtility.SetDirty(main); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        BuildWin();
    }
    // a copy of the prefab's materials using the pink textures from Generated/Sakura
    static GameObject Blossom(GameObject go)
    {
        if (!go) return go;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i]; if (!m) continue; var path = $"{Gen}/Sakura/{m.name}_sakura.mat"; var copy = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!copy)
                {
                    copy = new Material(m); bool any = false;
                    foreach (var prop in m.GetTexturePropertyNames())
                    {
                        var t = m.GetTexture(prop); if (!t) continue; var pink = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Gen}/Sakura/{t.name}.png"); if (pink) { copy.SetTexture(prop, pink); any = true; }
                    }
                    if (!any) continue;
                    AssetDatabase.CreateAsset(copy, path);
                }
                mats[i] = copy; changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
        return go;
    }
    // the main city is stage 3 (Kingdom Square) in blossom: this root only holds what is added to it; Main hides the square's autumn trees while it is shown
    static Transform CityStage(Main main)
    {
        root = new GameObject("Stage_city").transform;
        const string K = "Assets/Art/Prefabs/Foliage/Trees and Bushes/";
        var trees = new[] { K + "SmallTreeRound_01.prefab", K + "SmallTreeRound_02.prefab" };
        void Tree(float x, float y, float z, float h) { Blossom(Fit(Load(trees[rnd.Next(2)]), new Vector3(x, y, z), R(0, 360), h)); }
        foreach (var (x, z) in new[] { (11f, -8f), (-11f, -8f) }) Tree(x, .5f, z, 5.5f);   // on the far planters; none on the camera side, where they would hide the player
        foreach (var (x, z, h) in new[] { (19f, -19f, 11f), (-19f, -19f, 11f), (17.5f, -9f, 8f), (-17.5f, -9f, 8f), (6f, -19f, 8f), (-6f, -19f, 8f), (10f, -21f, 9f), (-10f, -21f, 9f), (24f, -12f, 10f), (-24f, -12f, 10f), (26f, 8f, 9f), (-26f, 8f, 9f) })
            Tree(x, 0, z, h);
        // the way out: the magic pack's tech circle with its rising beams (Laser AOE), looping in blossom pink; no flash or smoke, they would strobe and turn black
        var tp = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Hovl Studio/Magic effects pack/Prefabs/AoE effects/Laser AOE.prefab");
        if (tp)
        {
            var t = (GameObject)PrefabUtility.InstantiatePrefab(tp, root); t.name = "GateCircle"; t.transform.position = new Vector3(-8.5f, .08f, -14.5f); t.transform.localScale *= .68f;
            foreach (var ps in t.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (ps.name is "Flash" or "Smoke") { ps.gameObject.SetActive(false); continue; }
                var m = ps.main; m.loop = true; m.prewarm = true; m.playOnAwake = true; m.stopAction = ParticleSystemStopAction.None; m.startDelay = 0; m.duration = Mathf.Max(.5f, m.startLifetime.constantMax * .8f);   // the next one starts before the last has gone, so the circle never disappears
                m.scalingMode = ParticleSystemScalingMode.Hierarchy; m.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .42f, .72f, 1), new Color(1, .7f, .88f, 1));
            }
            foreach (var l in t.GetComponentsInChildren<Light>(true)) l.gameObject.SetActive(false);
        }
        else Debug.LogError("[NovaBuild] missing Laser AOE effect");
        // cherry trees balance the gate across the square
        foreach (var (x, z, h) in new[] { (8.5f, -14f, 10f), (13f, -12f, 8f) }) Tree(x, 0, z, h);
        PointLight(new Vector3(-8.5f, 2.5f, -14.5f), new Color(1, .45f, .72f), 9, 11);
        // a proper fire in the middle of the square, lighting the stones round it
        var fireFx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        if (fireFx) { var f = (GameObject)PrefabUtility.InstantiatePrefab(fireFx, root); f.name = "SquareFire"; f.transform.position = new Vector3(0, .65f, 0); f.transform.localScale *= 1.1f; foreach (var ps in f.GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "Smoke") ps.gameObject.SetActive(false); else { var fm = ps.main; fm.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .5f, .16f, 1), new Color(1, .72f, .3f, 1)); } }
        PointLight(new Vector3(0, 2.2f, 0), new Color(1, .58f, .26f), 16, 17);
        return root;
    }
    // Open World zone 1 (OPEN-WORLD-ZONE1.md), step 1: flat walkable ground, radius 60, hills and thick forest around it, a dirt path through the middle
    // the low-poly fruit tree in blossom: its leaf texture repainted in pinks, the same shapes and triangles
    static GameObject PinkFoliage(GameObject go)
    {
        const string Mat = Gen + "/Sakura/PT_Fruit_Foliage_sakura.mat", Png = Gen + "/Sakura/PT_Fruit_Foliage_sakura.png";
        var pink = AssetDatabase.LoadAssetAtPath<Material>(Mat);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!mats[i] || !mats[i].name.Contains("Foliage")) continue;
                if (!pink)
                {
                    var src = mats[i].GetTexture("_BaseTexture") ?? mats[i].GetTexture("_BaseMap"); var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB); Graphics.Blit(src, rt);
                    var was = RenderTexture.active; RenderTexture.active = rt; var t = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); RenderTexture.active = was; RenderTexture.ReleaseTemporary(rt);
                    var px = t.GetPixels(); for (int k = 0; k < px.Length; k++) { float l = Mathf.Clamp01(px[k].grayscale * 1.5f); var c = Color.Lerp(new Color(.86f, .42f, .6f), new Color(1, .84f, .9f), l); c.a = px[k].a; px[k] = c; }
                    t.SetPixels(px); File.WriteAllBytes(Png, t.EncodeToPNG()); AssetDatabase.ImportAsset(Png);
                    pink = new Material(mats[i]); var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(Png); foreach (var prop in new[] { "_BaseTexture", "_BaseMap" }) if (pink.HasProperty(prop)) pink.SetTexture(prop, tex); if (pink.HasProperty("_TopColor")) pink.SetColor("_TopColor", new Color(1, .78f, .86f));
                    AssetDatabase.CreateAsset(pink, Mat);
                }
                mats[i] = pink;
            }
            r.sharedMaterials = mats;
        }
        return go;
    }
    static Transform WildsStage()
    {
        NewRoot("Stage_wilds", new Color(.34f, .38f, .34f), .007f, new Color(.5f, .58f, .66f), new Color(.3f, .36f, .24f), new Color(.1f, .12f, .06f));
        var sun = Sun(new Color(1f, .94f, .82f), 1.9f, new Vector3(30, 125, 0)); root.GetComponent<StageLook>().sun = sun; root.GetComponent<StageLook>().lowCam = true;
        const float Edge = 62, RH = WildsMap.StreamHalf;
        Ground("WildsGround", new[] { ("leafy_grass", 3.5f), ("leafy_grass", 6f), ("grass_path_3", 3f), ("brown_mud_02", 3f) }, (x, z, i) =>
        {
            float d = Mathf.Abs(z - WildsMap.StreamZ(x)), bank = 1 - Smooth(RH, RH + 1.6f, d); if (i == 3) return bank;
            float path = Mathf.Max(PathMask(x, z), 1 - Smooth(.8f, 2.2f, WildsMap.TrailDistance(x, z))) * (1 - bank), rest = 1 - bank, r = Mathf.Sqrt(x * x + z * z), patch = Mathf.PerlinNoise(x * .05f + 5, z * .05f + 2), edge = Mathf.SmoothStep(0, 1, (r - 86) / 8), wild = Mathf.Max(edge, Mathf.SmoothStep(.7f, .88f, patch));   // green under the grass, so the ground past it still reads as meadow
            return i == 2 ? path * (r < Edge ? 1 : 0) : i == 1 ? (rest - path) * wild : (rest - path) * (1 - wild);
        }, 0, (x, z) => WildsMap.Height(x, z) + Mathf.SmoothStep(0, 1, (Mathf.Sqrt(x * x + z * z) - 84) / 14) * 4 * (.5f + Mathf.PerlinNoise(x * .05f + 3, z * .05f + 7)), WildsMap.StreamDepth);
        var meadow = Grass(new Color(.03f, .2f, .1f), new Color(.1f, .46f, .2f), new Color(.32f, .74f, .32f), .24f, .3f, 62); meadow.wilds = true; meadow.meadow = true; root.GetComponentInChildren<GrassField>().maxDistance = 34;
        // the stream: strips of the pack's water following the curve, and two wooden bridges
        var wmat = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/Water.mat");
        for (float x = -92; x < 92; x += 4)
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(w.GetComponent<Collider>()); w.name = "Water"; w.transform.SetParent(root);
            float z0 = WildsMap.StreamZ(x), z1 = WildsMap.StreamZ(x + 4); w.transform.position = new Vector3(x + 2, -.22f, (z0 + z1) / 2);
            w.transform.rotation = Quaternion.Euler(90, -Mathf.Atan2(z1 - z0, 4) * Mathf.Rad2Deg, 0); w.transform.localScale = new Vector3(4.6f, RH * 2 + 1.2f, 1);
            if (wmat) w.GetComponent<Renderer>().sharedMaterial = wmat;
        }
        var bridge = Load("Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/Bridge/PT_Wooden_Bridge_02.prefab");
        foreach (float bx in new[] { 1.5f, -30f }) Deck(Fit(bridge, new Vector3(bx, -.1f, WildsMap.StreamZ(bx)), 90 - Mathf.Atan(WildsMap.StreamSlope(bx)) * Mathf.Rad2Deg, 2.2f));
        Vector3 On(Vector2 p, float sink = 0) => new(p.x, WildsMap.Height(p.x, p.y) - sink, p.y);
        bool Free(Vector2 p, float pad) => WildsMap.Dry(p.x, p.y, pad) && WildsMap.TrailDistance(p.x, p.y) > 2.5f && Vector2.Distance(p, WildsMap.House) > 11 && Vector2.Distance(p, WildsMap.Cave) > 9;
        // the house you can go into: faces the camera side; the marker is where its door is
        var hg = Fit(Load("Assets/Art/Prefabs/Houses/Preset_House_01_A.prefab"), new Vector3(WildsMap.House.x, 0, WildsMap.House.y), 0, 9); var hb = Bounds(hg);
        var doorMark = new GameObject("HouseDoor").transform; doorMark.SetParent(root); doorMark.position = new Vector3(hb.center.x, 0, hb.max.z + 1.2f);
        PointLight(doorMark.position + Vector3.up * 2.4f, new Color(1, .7f, .4f), 7, 9);
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        // the secret cave: a stone doorway in a heap of boulders at the north edge; its door is gone at night
        const string DG = "Assets/ThirdParty/Dungeon/Prefabs/"; var cave = WildsMap.Cave;
        Fit(Load(DG + "Doorways/Doorway_A.prefab"), On(cave), 90, 4.4f);
        Fit(Load(DG + "Doors/Door_A.prefab"), On(cave), 90, 3.6f).name = "CaveSeal";
        var dark = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(dark.GetComponent<Collider>()); dark.name = "CaveDark"; dark.transform.SetParent(root); dark.transform.position = On(cave + new Vector2(0, -.45f)) + Vector3.up * 1.9f; dark.transform.rotation = Quaternion.Euler(0, 180, 0); dark.transform.localScale = new Vector3(3.4f, 3.8f, 1);
        var darkMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); darkMat.SetColor("_BaseColor", new Color(.008f, .012f, .02f)); AssetDatabase.CreateAsset(darkMat, $"{Gen}/CaveDark.mat"); dark.GetComponent<Renderer>().sharedMaterial = darkMat;
        // a heap of big rocks round the door, and one laid over it (named so it does not block the way in)
        foreach (var (dx, dz, ht) in new[] { (-5.4f, -.8f, 3.8f), (5.4f, -.8f, 4f), (-3f, -4.2f, 5f), (3f, -4.2f, 5.2f), (0f, -5f, 5.6f), (-8.4f, -2f, 2.6f), (8.4f, -2f, 2.8f) }) Fit(Load(N("namaqualand_boulder_03")), On(cave + new Vector2(dx, dz), .2f), R(0, 360), ht).name = "boulder_CaveRock";
        var capRock = Fit(Load(N("namaqualand_boulder_03")), On(cave + new Vector2(0, -1.6f)) + Vector3.up * 4.3f, R(0, 360), 2f); capRock.name = "CaveRockTop";
        var doorSpot = new GameObject("CaveDoorMark").transform; doorSpot.SetParent(root); doorSpot.position = new Vector3(cave.x, 0, cave.y + 2);
        var moonLight = PointLight(On(cave + new Vector2(0, 1.5f)) + Vector3.up * 2.2f, new Color(.5f, 1, .95f), 3.5f, 9); moonLight.name = "CaveLight"; moonLight.gameObject.SetActive(false);
        // the low-poly pack: the photo-scanned trees of the small arenas run to 100,000 triangles each, far too many for a whole forest
        const string PT = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/";
        var pines = new[] { PT + "Trees/PT_Pine_Tree_03_green.prefab", PT + "Trees/PT_Pine_Tree_03_green_cut.prefab" }; var fruit = new[] { PT + "Trees/PT_Fruit_Tree_01_green.prefab", PT + "Trees/PT_Fruit_Tree_01_apples.prefab", PT + "Trees/PT_Fruit_Tree_01_plums.prefab" };
        foreach (var p in Ring(90, 63, 84)) if (Free(p, 1)) Fit(Load(pines[rnd.Next(2)]), On(p), R(0, 360), R(10, 15));   // the wall of forest round the zone
        foreach (var p in Ring(22, 16, 58, 3, .2f)) if (Free(p, 1)) Fit(Load(fruit[rnd.Next(3)]), On(p), R(0, 360), R(5.5f, 8));   // trees to walk among
        foreach (var g in WildsMap.Groves) for (int i = 0; i < 4; i++) { var p = g + new Vector2(R(-5.5f, 5.5f), R(-5.5f, 5.5f)); if (Free(p, 1) && PathMask(p.x, p.y) < .2f) PinkFoliage(Fit(Load(PT + "Trees/PT_Fruit_Tree_01_green.prefab"), On(p), R(0, 360), R(6, 8.5f))); }   // the blossom trees the petals fall from
        foreach (var p in Ring(16, 14, 58, 2.5f, .2f)) if (Free(p, 0)) Place(Load(N("namaqualand_boulder_03")), On(p, .1f), R(0, 360), R(.45f, .8f));
        foreach (var p in Ring(26, 12, 60, 2, .2f)) if (Free(p, 0)) Fit(Load(PT + "Shrubs/PT_Generic_Shrub_01_green.prefab"), On(p), R(0, 360), R(1.2f, 2));
        // rocks along the banks, and a ring of boulders on the hilltop: something to climb up to
        for (float x = -58; x < 58; x += R(4, 8)) { var p = new Vector2(x, WildsMap.StreamZ(x) + (rnd.Next(2) * 2 - 1) * (RH + R(.4f, 1.4f))); if (Mathf.Abs(x - 1.5f) > 4 && Mathf.Abs(x + 30) > 4) Place(Load(N("namaqualand_boulder_03")), On(p, .1f), R(0, 360), R(.2f, .35f)); }
        for (int i = 0; i < 7; i++) { float a = i / 7f * Mathf.PI * 2; if (i != 5) Place(Load(N("namaqualand_boulder_03")), On(WildsMap.Hill + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 5.2f, .1f), R(0, 360), R(.4f, .6f)); }
        return root;
    }
    // the room inside the zone's house: stone floor and walls, the near side low with a gap for the way out, torches, clutter along the walls, a chest at the far end
    static Transform HouseStage()
    {
        NewRoot("Stage_house", new Color(.05f, .05f, .07f), .012f, new Color(.5f, .52f, .66f), new Color(.34f, .3f, .28f), new Color(.13f, .1f, .09f));
        var sun = Sun(new Color(.75f, .8f, 1f), 1.2f, new Vector3(62, 165, 0)); root.GetComponent<StageLook>().sun = sun;
        const string D = "Assets/ThirdParty/Dungeon/Prefabs/"; const float W = 8.79f, Step = 5.86f;
        Underlay();
        var fl = new GameObject("Floor").transform; fl.SetParent(root); var floor = Load(D + "Floors/Floor_A.prefab");
        for (int ix = -2; ix <= 2; ix++) for (int iz = -2; iz <= 2; iz++) Place(floor, new Vector3(ix * 5.8f, 0, iz * 5.8f), 90 * rnd.Next(4), 1, true, fl);
        var walls = new[] { Load(D + "Walls/Wall_A.prefab"), Load(D + "Walls/Wall_B.prefab"), Load(D + "Walls/Wall_C.prefab") };
        for (int i = -1; i <= 1; i++)
        {
            Place(walls[rnd.Next(walls.Length)], new Vector3(-W, 0, i * Step), 0);
            Place(walls[rnd.Next(walls.Length)], new Vector3(W, 0, i * Step), 180);
            Place(walls[rnd.Next(walls.Length)], new Vector3(i * Step, 0, -W), 90);
            if (i != 0) { var w = Place(walls[rnd.Next(walls.Length)], new Vector3(i * Step, 0, W), -90); var sc = w.transform.localScale; w.transform.localScale = new Vector3(sc.x, sc.y * .3f, sc.z); }
        }
        var pillar = Load(D + "Pillars/Pillar_A.prefab"); var torch = Load("Assets/ThirdParty/PrisonCell/Prefabs/Props/Torch.prefab"); var fire = Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        foreach (var (x, z) in new[] { (-7.4f, -7.4f), (7.4f, -7.4f), (-7.4f, 2f), (7.4f, 2f) })
        {
            Place(pillar, new Vector3(x, 0, z), 0, 1);
            var tp = new Vector3(x + (x < 0 ? .75f : -.75f), 2.6f, z); Place(torch, tp, x < 0 ? 90 : -90, 1.6f); Place(fire, tp + Vector3.up * .6f, 0, .35f);
            PointLight(tp + Vector3.up * .3f, new Color(1, .55f, .22f), 12, 13);
        }
        foreach (var (n, x, z, yaw) in new[] { ("Barrel", -6.6f, -5f, 0f), ("Barrel", -6f, -6.2f, 40f), ("Crate_A", 6.5f, -5.4f, 20f), ("Crate_B", 6.2f, 4.8f, 70f), ("Table_A", -6.4f, 5f, 90f), ("Stool_A_Broken", -4.6f, 4f, 30f), ("Candles", -3f, -7.2f, 0f), ("Candles", 3f, -7.2f, 0f) })
            Place(Load(D + "Props/" + n + ".prefab"), new Vector3(x, 0, z), yaw, 1);
        Place(Load("Assets/Game/Models/Props/treasure_chest/treasure_chest_1k.gltf"), OpenWorld.Chest, 0, 1.6f);
        return root;
    }
    // the Sky Court (duels of the zone): a square stone court on a rock floating over a sea of cloud, a wooden fence open on the camera side
    // where a short bridge leads to a smaller rock, sakura in the corners, torches, more rocks drifting round, far peaks out of the clouds.
    // Only the court and its fence are near the fight; everything else sits in groups the obstacle map does not read.
    static Transform SkyStage()
    {
        NewRoot("Stage_sky", new Color(.7f, .8f, .93f), .0065f, new Color(.8f, .87f, .98f), new Color(.6f, .63f, .68f), new Color(.36f, .34f, .32f));
        var look = root.GetComponent<StageLook>(); look.sun = Sun(new Color(1f, .93f, .8f), 1.9f, new Vector3(38, 160, 0)); look.sky = SkyDome();
        const string D = "Assets/ThirdParty/Dungeon/Prefabs/", PT = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/", V = "Assets/Polytope Studio/Lowpoly_Village/Prefabs/Modular/";
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        const float Half = 11.7f, Tile = 5.85f, Gap = 2.4f;
        Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root); return g; }
        // the court: warm paving on a thick stone slab
        var fl = Group("Floor");
        var pave = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(pave.GetComponent<Collider>()); pave.name = "Paving"; pave.transform.SetParent(fl); pave.transform.position = new Vector3(0, .01f, 0); pave.transform.rotation = Quaternion.Euler(90, 0, 0); pave.transform.localScale = new Vector3(Half * 2, Half * 2, 1);
        pave.GetComponent<Renderer>().sharedMaterial = Lit("SkyPaving", "mossy_brick", 5, .12f, new Color(1.4f, 1.22f, 1f));
        var slab = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(slab.GetComponent<Collider>()); slab.name = "Slab"; slab.transform.SetParent(fl); slab.transform.position = new Vector3(0, -.75f, 0); slab.transform.localScale = new Vector3(Half * 2 + .3f, 1.4f, Half * 2 + .3f);
        slab.GetComponent<Renderer>().sharedMaterial = Lit("SkySlab", "rock_face_04", 8, .1f, new Color(.55f, .52f, .5f));
        // the rock it stands on, narrowing downward
        var under = Group("Underside");
        GameObject Hang(Vector2 at, float h, float top, Transform parent) { var g = Fit(Load(N("namaqualand_boulder_03")), new Vector3(at.x, 0, at.y), R(0, 360), h, parent); if (!g) return null; g.transform.position += Vector3.up * (top - Bounds(g).max.y); return g; }
        Hang(Vector2.zero, 15, -.9f, under); foreach (var (x, z) in new[] { (-7f, -7f), (7f, -7f), (-7f, 7f), (7f, 7f) }) Hang(new Vector2(x, z), 8, -.8f, under);
        foreach (var (x, z, h, t) in new[] { (-3f, 2f, 9f, -9f), (3f, -2f, 8f, -12f), (0f, 0f, 6f, -17f) }) Hang(new Vector2(x, z), h, t, under);
        // wooden railing round the edge (posts, a top rail and a middle rail), open in the middle of the camera side
        var rail = Group("Rail"); var wood = Lit("SkyWood", "weathered_brown_planks", 1, .1f, new Color(.78f, .6f, .45f));
        void Beam(Vector3 a, Vector3 b, float w, Transform parent)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(c.GetComponent<Collider>()); c.name = "Plank"; c.transform.SetParent(parent);
            var d = b - a; c.transform.position = (a + b) / 2; c.transform.rotation = Quaternion.LookRotation(d.sqrMagnitude > 1e-4f ? d : Vector3.forward); c.transform.localScale = new Vector3(w, w, Mathf.Max(w, d.magnitude));
            c.GetComponent<Renderer>().sharedMaterial = wood;
        }
        void Railing(Vector3 a, Vector3 b, Transform parent)
        {
            int posts = Mathf.Max(1, Mathf.RoundToInt((b - a).magnitude / 2.3f));
            for (int i = 0; i <= posts; i++) { var p = Vector3.Lerp(a, b, i / (float)posts); Beam(p, p + Vector3.up * 1.15f, .16f, parent); }
            Beam(a + Vector3.up * 1.05f, b + Vector3.up * 1.05f, .11f, parent); Beam(a + Vector3.up * .55f, b + Vector3.up * .55f, .08f, parent);
        }
        float e = Half - .15f;
        Railing(new Vector3(-e, 0, -e), new Vector3(e, 0, -e), rail); Railing(new Vector3(-e, 0, -e), new Vector3(-e, 0, e), rail); Railing(new Vector3(e, 0, -e), new Vector3(e, 0, e), rail);
        Railing(new Vector3(-e, 0, e), new Vector3(-Gap, 0, e), rail); Railing(new Vector3(Gap, 0, e), new Vector3(e, 0, e), rail);
        // torches at the corners and either side of the opening
        var torch = Load("Assets/ThirdParty/PrisonCell/Prefabs/Props/Torch.prefab"); var fire = Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        foreach (var (x, z) in new[] { (-Half + .4f, -Half + .4f), (Half - .4f, -Half + .4f), (-Half + .4f, Half - .4f), (Half - .4f, Half - .4f), (-Gap - .3f, Half - .3f), (Gap + .3f, Half - .3f) })
        { var tp = new Vector3(x, 1.5f, z); Place(torch, tp, 0, 1.4f, false, rail); Place(fire, tp + Vector3.up * .55f, 0, .3f, false, rail); }
        // sakura in the corners, low shrubs along the fence
        foreach (var (x, z) in new[] { (-9.6f, -9.6f), (9.6f, -9.6f), (-9.6f, 9.4f), (9.6f, 9.4f) }) PinkFoliage(Fit(Load(PT + "Trees/PT_Fruit_Tree_01_green.prefab"), new Vector3(x, 0, z), R(0, 360), R(4.6f, 5.4f)));
        var green = Group("Planting"); foreach (var (x, z) in new[] { (-5f, -10.6f), (5f, -10.6f), (-10.6f, -3f), (10.6f, 3f), (-10.6f, 4f), (10.6f, -4f) }) Fit(Load(PT + "Shrubs/PT_Generic_Shrub_01_green.prefab"), new Vector3(x, 0, z), R(0, 360), R(.8f, 1.2f), green);
        // the bridge out to a smaller rock on the camera side, and rocks drifting round the court
        var far = Group("Scenery"); const float IslandZ = 21.5f;
        { float z0 = Half, z1 = IslandZ - 2.6f; for (float z = z0 + .2f; z < z1; z += .42f) Beam(new Vector3(-1.2f, -.06f, z), new Vector3(1.2f, -.06f, z), .3f, far); Railing(new Vector3(-1.25f, 0, z0), new Vector3(-1.25f, 0, z1), far); Railing(new Vector3(1.25f, 0, z0), new Vector3(1.25f, 0, z1), far); }
        var isl = Fit(Load(N("namaqualand_boulder_03")), new Vector3(0, 0, IslandZ), R(0, 360), 6, far); if (isl) isl.transform.position += Vector3.up * (-.1f - Bounds(isl).max.y);
        Hang(new Vector2(0, IslandZ), 7, -1.5f, far);
        foreach (var (x, y, z, h) in new[] { (-24f, -3f, -8f, 6f), (22f, 2f, -16f, 5f), (-30f, 4f, 14f, 4f), (31f, -6f, 10f, 7f), (-12f, -9f, -30f, 5f), (14f, 6f, -34f, 4f), (40f, 1f, -30f, 8f), (-42f, -2f, -26f, 6f) })
        { var r = Fit(Load(N("namaqualand_boulder_03")), new Vector3(x, y, z), R(0, 360), h, far); if (r) r.transform.rotation = Quaternion.Euler(R(150, 210), R(0, 360), 0); }   // upside down: the broken face on top, the point below
        foreach (var (a, d, h) in new[] { (-150f, 120f, 70f), (-120f, 140f, 55f), (-95f, 110f, 80f), (-60f, 135f, 60f), (-30f, 120f, 75f), (0f, 150f, 65f), (35f, 125f, 85f), (70f, 140f, 60f), (110f, 120f, 70f), (150f, 135f, 50f) })
        {
            float ar = a * Mathf.Deg2Rad; var sp = Fit(Load(N("namaqualand_boulder_03")), Vector3.zero, R(0, 360), h, far); if (!sp) continue;
            sp.transform.localScale = Vector3.Scale(sp.transform.localScale, new Vector3(.4f, 1, .4f)); var bb = Bounds(sp);
            sp.transform.position += new Vector3(Mathf.Sin(ar) * d - bb.center.x, -40 - bb.min.y, -Mathf.Cos(ar) * d - bb.center.z);
        }
        CloudSea(far);
        Static(root); foreach (var t in rail.GetComponentsInChildren<Transform>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);   // the fires keep their own transforms
        foreach (var t in far.GetComponentsInChildren<Drift>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        return root;
    }
    // a sky dome without a picture: Unity's procedural sky, blue overhead and pale towards the cloud line
    static Material SkyDome()
    {
        var path = $"{Gen}/SkyCourtSky.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_SkyTint", new Color(.42f, .58f, .86f)); m.SetColor("_GroundColor", new Color(.78f, .84f, .92f)); m.SetFloat("_AtmosphereThickness", .75f); m.SetFloat("_Exposure", 1.25f); m.SetFloat("_SunSize", .035f);
        EditorUtility.SetDirty(m); return m;
    }
    // three layers of soft cloud below the court (a drawn noise picture, scrolled slowly by Drift) over a pale floor
    static void CloudSea(Transform parent)
    {
        const string png = Gen + "/SkyClouds.png"; const int S = 256;
        if (!File.Exists(png))
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            float Fbm(float x, float y) { float v = 0, amp = .5f, f = 1; for (int o = 0; o < 5; o++) { v += amp * TileNoise(x * f, y * f, 4 * f); amp *= .5f; f *= 2; } return v; }
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++) { float v = Fbm(x / (float)S * 4, y / (float)S * 4); float a = Mathf.SmoothStep(.38f, .72f, v); float l = .86f + .14f * Mathf.SmoothStep(.5f, .85f, v); t.SetPixel(x, y, new Color(l, l, Mathf.Min(1, l + .03f), a)); }
            File.WriteAllBytes(png, t.EncodeToPNG()); AssetDatabase.ImportAsset(png);
            var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.wrapMode = TextureWrapMode.Repeat; ti.alphaIsTransparency = true; ti.maxTextureSize = 256; ti.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        Material Mat(string name, Color c, bool see)
        {
            var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c);
            if (see)
            {
                m.SetTexture("_BaseMap", tex); m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0); m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 2990;
            }
            EditorUtility.SetDirty(m); return m;
        }
        void Layer(string name, float y, float size, float tile, Material m, float drift)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>()); q.name = name; q.transform.SetParent(parent);
            q.transform.position = new Vector3(0, y, 0); q.transform.rotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = new Vector3(size, size, 1);
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            if (drift != 0) { var d = q.AddComponent<Drift>(); d.speed = new Vector2(drift, drift * .4f); d.tile = tile; }
        }
        Layer("CloudFloor", -34, 700, 1, Mat("SkyCloudFloor", new Color(.6f, .74f, .95f), false), 0);
        Layer("CloudLow", -22, 420, 7, Mat("SkyCloudLow", new Color(.96f, .98f, 1f, 1), true), .004f);
        Layer("CloudHigh", -12, 300, 5, Mat("SkyCloudHigh", new Color(1, 1, 1, .95f), true), .007f);
    }
    // value noise that repeats every 'period' units, so the cloud picture tiles without a seam
    static float TileNoise(float x, float y, float period)
    {
        int p = Mathf.Max(1, Mathf.RoundToInt(period)); int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y); float fx = x - x0, fy = y - y0;
        float H(int a, int b) { a = ((a % p) + p) % p; b = ((b % p) + p) % p; uint h = (uint)(a * 374761393 + b * 668265263) ^ 0x5bd1e995u; h = (h ^ (h >> 13)) * 1274126177u; return (h & 0xffff) / 65535f; }
        float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
        return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), sx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), sx), sy);
    }
    // Sky Lanes (SKY-LANES-3V3.md): the floating 3v3 map, after the owner's painting (semi-realistic, muted, mossy stone): grass islands with stone roads on them, low walls
    // and fire posts along the lanes, two stone bridges on the curved lane, a blue ruined temple with waterfalls for our base (+Z) and a
    // dark spired castle for theirs (-Z), a lush jungle with a glowing cave, small floating islands round about, a sea of cloud below.
    // Walkable ground is SkyLanes (lanes, bridges, jungle, bases); the grass shoulders and everything else are scenery off the obstacle map.
    static Transform SkyLanesStage()
    {
        NewRoot("Stage_skylanes", new Color(.62f, .72f, .86f), .0058f, new Color(.7f, .78f, .92f), new Color(.55f, .58f, .62f), new Color(.32f, .3f, .28f));
        var look = root.GetComponent<StageLook>(); look.sun = Sun(new Color(1f, .9f, .76f), 2f, new Vector3(40, 140, 0)); look.sky = SkyDome();
        Grade();
        const string PT = "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/", AR = "Assets/Altar_Ruins_FREE/Prefabs/", D = "Assets/ThirdParty/Dungeon/Prefabs/";
        string N(string n) => $"Assets/Game/Models/Nature/{n}.glb";
        Transform Group(string n) { var g = new GameObject(n).transform; g.SetParent(root); return g; }
        const float Shoulder = 2.2f, Road = .4f, Cliff = 4.5f, Uv = 3.5f, FireSize = .16f; var Pale = new Color(2.1f, 2.2f, 1.95f);   // Pale: the ruin pieces' own textures read near black from above; a mossy grey-green lift

        // ---- painted textures (made once, kept in Generated) ----
        Texture2D Paint(string name, int w, int h, System.Func<float, float, Color> px)
        {
            var png = $"{Gen}/{name}.png";
            if (!File.Exists(png))
            {
                var t = new Texture2D(w, h, TextureFormat.RGBA32, false); var cols = new Color[w * h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) cols[y * w + x] = px((x + .5f) / w, (y + .5f) / h);
                t.SetPixels(cols); File.WriteAllBytes(png, t.EncodeToPNG()); AssetDatabase.ImportAsset(png);
                var ti = (TextureImporter)AssetImporter.GetAtPath(png); ti.wrapMode = TextureWrapMode.Repeat; ti.alphaIsTransparency = true; ti.maxTextureSize = 512; ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        }
        float Fbm(float x, float y, float p) { float v = 0, a = .5f, f = 1; for (int o = 0; o < 4; o++) { v += a * TileNoise(x * f, y * f, p * f); a *= .5f; f *= 2; } return v; }
        // flagstones: cells round jittered points (wrapping, so the picture tiles), each a warm grey of its own, dark joints, a lighter worn middle
        // old flagstones (the owner's road picture): grey-blue slabs set apart, brown earth and moss in the wide joints, now and then a slab
        // missing (earth and grass), and a few slabs carved with a rune that glows (emission). One cell layout feeds colour, glow and the normal map.
        const int SlabG = 3;   // three slabs per 3.5 m: big enough to read from the battle camera
        float SS(float e0, float e1, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(e0, e1, x));   // GLSL smoothstep (Mathf.SmoothStep blends between its first two numbers)
        float SlabCell(float u, float v, out float edge, out int cell, out Vector2 local)
        {
            float best = 9, second = 9; cell = 0; var bc = Vector2.zero;
            for (int gy = -1; gy <= SlabG; gy++) for (int gx = -1; gx <= SlabG; gx++)
            {
                int wx = (gx % SlabG + SlabG) % SlabG, wy = (gy % SlabG + SlabG) % SlabG; float j = TileNoise(wx * 7.3f, wy * 3.1f, 64), k = TileNoise(wx * 2.9f + 5, wy * 6.7f + 3, 64);
                var c = new Vector2((gx + .25f + .5f * j) / SlabG, (gy + .25f + .5f * k) / SlabG); var q = new Vector2(u, v) - c; q.x *= 1.25f;   // a little wider than tall: slabs, not tiles
                float d = q.magnitude;
                if (d < best) { second = best; best = d; cell = wx * 31 + wy * 17; bc = c; } else if (d < second) second = d;
            }
            edge = (second - best) * SlabG; local = (new Vector2(u, v) - bc) * SlabG; return best;
        }
        float Hash(int c, float salt) => Mathf.Repeat(Mathf.Sin(c * 12.9898f + salt * 78.233f) * 43758.5453f, 1);
        bool Missing(int cell) => Hash(cell, 1) < .12f;
        float Rune(int cell, Vector2 local)
        {   // a carved glyph: three or four straight strokes between points of a 3 x 3 grid, on about one slab in eight
            if (Hash(cell, 2) > .22f || Missing(cell)) return 0;
            Vector2 P(int i) => new Vector2((i % 3 - 1) * .17f, (i / 3 - 1) * .2f); float best = 9;
            for (int sIdx = 0; sIdx < 4; sIdx++)
            {
                int i0 = (int)(Hash(cell, 3 + sIdx) * 9), i1 = (int)(Hash(cell, 9 + sIdx) * 9); if (i0 == i1) continue;
                Vector2 a = P(i0), b = P(i1), d = b - a; float t = Mathf.Clamp01(Vector2.Dot(local - a, d) / d.sqrMagnitude); best = Mathf.Min(best, Vector2.Distance(local, a + d * t));
            }
            return Mathf.Clamp01(1 - Mathf.InverseLerp(.012f, .03f, best));
        }
        // centre of the road: slabs laid close and whole; the road's edges (a band of its own, alpha-cut over the grass) break up into
        // loose slabs, brown earth and the grass beneath (the owner's road picture)
        Color Flag(float u, float v, bool rough)
        {
            SlabCell(u, v, out float edge, out int cell, out var local);
            bool gone = rough ? Hash(cell, 1) < .5f : false;
            float grain = .78f + .44f * Fbm(u * 14, v * 14, 14), tone = .46f + .16f * Hash(cell, 4), blue = .02f + .05f * Hash(cell, 5);
            float slab = (rough ? SS(.12f, .22f, edge) : SS(.05f, .1f, edge)) * (gone ? 0 : 1);   // tight joints in the middle, wide and broken at the edges
            float rim = 1 - .18f * (1 - SS(.1f, .22f, edge));
            float crack = Hash(cell, 7) < .35f ? (1 - SS(0, .012f, Mathf.Abs(Fbm(u * 6 + cell, v * 6, 6) - .5f))) * .18f : 0;
            float l = tone * grain * rim * (1 - crack); var stone = new Color(l * (.95f - blue), l * .97f, l * (.95f + blue), 1);
            stone = Color.Lerp(stone, new Color(.3f, .38f, .2f), Mathf.Max(SS(.6f, .76f, Fbm(u * 6 + 3, v * 6 + 9, 6)) * (rough ? .55f : .3f), (1 - SS(.1f, .22f, edge)) * .25f));   // moss creeping over some slabs
            float earthOrMoss = Fbm(u * 7 + 11, v * 7 + 4, 7); var dirt = Color.Lerp(new Color(.36f, .25f, .15f), new Color(.46f, .34f, .2f), Fbm(u * 30, v * 30, 30));
            var moss = new Color(.26f, .34f, .15f) * (.8f + .4f * grain); moss.a = 1; var gap = rough ? dirt : Color.Lerp(dirt, moss, SS(.45f, .65f, earthOrMoss));
            var c = Color.Lerp(gap, stone, slab); c = Color.Lerp(c, new Color(.55f, .78f, 1f), Rune(cell, local) * slab * .8f);
            c.a = rough ? Mathf.Max(slab, SS(.5f, .56f, Fbm(u * 5 + 20, v * 5 + 7, 5))) : 1;   // the rough band: slabs and patches of earth kept, the rest cut away to the grass below
            return c;
        }
        var stoneTex = Paint("SkyStoneOld4", 512, 512, (u, v) => Flag(u, v, false));
        var edgeTex = Paint("SkyStoneEdge", 512, 512, (u, v) => Flag(u, v, true));
        var runeTex = Paint("SkyStoneRunes4", 512, 512, (u, v) => { SlabCell(u, v, out float edge, out int cell, out var local); float r = Rune(cell, local) * SS(.05f, .1f, edge); return new Color(r, r, r, 1); });
        float StoneHeight(float u, float v)
        {
            SlabCell(u, v, out float edge, out int cell, out var local);
            float slab = SS(.03f, .1f, edge) * (.85f + .15f * Hash(cell, 6));
            return slab - Rune(cell, local) * .12f * slab + .05f * Fbm(u * 24, v * 24, 24);
        }
        var stoneNrm = Paint("SkyStoneOld4_N", 512, 512, (u, v) =>
        {
            const float e = 1f / 512, k = 6; float dx = (StoneHeight(u + e, v) - StoneHeight(u - e, v)) * k, dy = (StoneHeight(u, v + e) - StoneHeight(u, v - e)) * k;
            var n = new Vector3(-dx, -dy, 1).normalized; return new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
        });
        { var ni = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(stoneNrm)); if (ni.textureType != TextureImporterType.NormalMap) { ni.textureType = TextureImporterType.NormalMap; ni.sRGBTexture = false; ni.SaveAndReimport(); } }
        var grassTex = Paint("SkyGrass", 512, 512, (u, v) =>
        {
            float n = Fbm(u * 6, v * 6, 6), fine = TileNoise(u * 64, v * 64, 64), dry = Mathf.SmoothStep(.62f, .8f, Fbm(u * 3 + 7, v * 3 + 2, 3));
            var g = Color.Lerp(new Color(.27f, .5f, .16f), new Color(.42f, .66f, .22f), n); g = Color.Lerp(g, new Color(.56f, .62f, .25f), dry * .5f);
            return g * (.88f + .24f * fine);
        });
        var waterTex = Paint("SkyWaterfall", 64, 256, (u, v) =>
        {   // falling streaks, soft at the sides
            float streak = TileNoise(u * 12, v * 1.5f, 12) * .6f + TileNoise(u * 24 + 3, v * 3, 24) * .4f, side = Mathf.SmoothStep(0, .18f, u) * Mathf.SmoothStep(1, .82f, u);
            float a = side * Mathf.Lerp(.35f, .95f, Mathf.SmoothStep(.35f, .75f, streak)); return new Color(.85f + .15f * streak, .93f + .07f * streak, 1, a);
        });
        Material TexMat(string name, Texture2D tex, Color tint, float smooth)
        {
            var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Cull", 0); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
        }
        var stoneMat = TexMat("SkyLanesStone", stoneTex, new Color(.74f, .75f, .74f), .04f); stoneMat.SetTexture("_BumpMap", stoneNrm); stoneMat.SetFloat("_BumpScale", 1.2f); stoneMat.EnableKeyword("_NORMALMAP"); stoneMat.SetTexture("_EmissionMap", runeTex); stoneMat.SetColor("_EmissionColor", new Color(.35f, .75f, 1.6f)); stoneMat.EnableKeyword("_EMISSION"); stoneMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; var grassMat = TexMat("SkyLanesGrassTop", grassTex, new Color(.5f, .62f, .42f), .04f); grassMat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Game/Textures/PBR/leafy_grass_nor_gl_1k.jpg")); grassMat.EnableKeyword("_NORMALMAP");
        var rockMat = Lit("SkyLanesCliff", "rock_face_04", 1, .06f, new Color(.56f, .53f, .5f)); rockMat.SetFloat("_Cull", 0);
        // a copy of a model's materials with their colour multiplied, kept in Generated/Tinted (dark castle stone, a brighter altar)
        GameObject Tint(GameObject go, string tag, Color mul)
        {
            if (!go) return go; Directory.CreateDirectory($"{Gen}/Tinted");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue; var path = $"{Gen}/Tinted/{mats[i].name}_{tag}.mat"; var c = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (!c) { c = new Material(mats[i]); foreach (var p in new[] { "_BaseColor", "_Color" }) if (c.HasProperty(p)) c.SetColor(p, c.GetColor(p) * mul); AssetDatabase.CreateAsset(c, path); }
                    mats[i] = c;
                }
                r.sharedMaterials = mats;
            }
            return go;
        }
        // a glowing crystal: a stretched eight-sided gem
        Mesh gem = null; Material GlowMat(string name, Color c) { var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); } m.SetColor("_BaseColor", c * .75f); m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * .8f); m.SetFloat("_Smoothness", .85f); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; EditorUtility.SetDirty(m); return m; }
        GameObject Crystal(Vector3 at, float h, Material m, Transform parent, float tilt = 0)
        {
            if (!gem)
            {
                var path = $"{Gen}/SkyGem.asset"; gem = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (!gem)
                {
                    var v = new List<Vector3> { new(0, 1, 0), new(0, -.35f, 0) }; for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; v.Add(new Vector3(Mathf.Cos(a) * .28f, .2f, Mathf.Sin(a) * .28f)); }
                    var t = new List<int>(); for (int i = 0; i < 6; i++) { int a = 2 + i, b = 2 + (i + 1) % 6; t.AddRange(new[] { 0, b, a, 1, a, b }); }
                    gem = new Mesh { name = "SkyGem", vertices = v.ToArray(), triangles = t.ToArray() }; gem.RecalculateNormals(); gem.RecalculateBounds(); AssetDatabase.CreateAsset(gem, path);
                }
            }
            var go = new GameObject("Crystal"); go.transform.SetParent(parent); go.transform.position = at; go.transform.rotation = Quaternion.Euler(tilt, R(0, 360), R(-tilt, tilt)); go.transform.localScale = Vector3.one * h;
            go.AddComponent<MeshFilter>().sharedMesh = gem; go.AddComponent<MeshRenderer>().sharedMaterial = m; return go;
        }

        // ---- ground: grass islands (the lanes' shoulders, the bases, the jungle), stone roads and plazas on them, rock cliffs under the rims ----
        // layers 6–14 cm apart: on the ramps the road, its curb and the grass under it are each bent their own way, closer they cut through each other
        var gv = new List<Vector3>(); var gt = new List<int>(); var rv = new List<Vector3>(); var rt = new List<int>(); var cv = new List<Vector3>(); var ct = new List<int>();
        float H(float x, float z) => SkyLanes.Height(x, z);
        void Disc(List<Vector3> v, List<int> t, Vector2 c, float r, float y, int seg = 40)
        {   // in rings about 1.5 m apart, each point at the land's height (raised bases, the sunken jungle)
            int rings = Mathf.Max(1, Mathf.CeilToInt(r / 1.5f)), c0 = v.Count; v.Add(new Vector3(c.x, y + H(c.x, c.y), c.y));
            for (int k = 1; k <= rings; k++)
            {
                float rr = r * k / rings;
                for (int i = 0; i <= seg; i++) { float a = -i / (float)seg * Mathf.PI * 2, px = c.x + Mathf.Cos(a) * rr, pz = c.y + Mathf.Sin(a) * rr; v.Add(new Vector3(px, y + H(px, pz), pz)); }
                int row = c0 + 1 + (k - 1) * (seg + 1);
                for (int i = 0; i < seg; i++) { if (k == 1) { t.Add(c0); t.Add(row + i); t.Add(row + i + 1); } else { int prev = row - (seg + 1); t.AddRange(new[] { prev + i, row + i, row + i + 1, prev + i, row + i + 1, prev + i + 1 }); } }
            }
        }
        void Wall(Vector3 a, Vector3 b, float depth) { int k = cv.Count; cv.Add(a); cv.Add(b); cv.Add(b + Vector3.down * depth); cv.Add(a + Vector3.down * depth); ct.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); }
        void Rim(Vector2 c, float r, float y, int seg = 40) { for (int i = 0; i < seg; i++) { float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2; float x0 = c.x + Mathf.Cos(a0) * r, z0 = c.y + Mathf.Sin(a0) * r, x1 = c.x + Mathf.Cos(a1) * r, z1 = c.y + Mathf.Sin(a1) * r; Wall(new Vector3(x0, y + H(x0, z0), z0), new Vector3(x1, y + H(x1, z1), z1), Cliff + R(-.5f, 1.5f)); } }
        void Strip(List<Vector3> v, List<int> t, int lane, float half, float y, bool rim)
        {   // the lane in 1 m steps (bridges left open), round joins at the bends
            float len = SkyLanes.Length(lane);
            for (float s = 0; s < len; s += 1)
            {
                float s1 = Mathf.Min(len, s + 1); if (SkyLanes.OnBridge(lane, (s + s1) / 2)) continue;
                var a = SkyLanes.PointAt(lane, s); var b = SkyLanes.PointAt(lane, s1); var d = (b - a).normalized; var n = new Vector2(-d.y, d.x) * half; int k = v.Count;
                Vector3 At(Vector2 p2) => new(p2.x, y + H(p2.x, p2.y), p2.y); v.Add(At(a + n)); v.Add(At(b + n)); v.Add(At(b - n)); v.Add(At(a - n)); t.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 });
                if (rim) { Wall(v[k], v[k + 1], Cliff); Wall(v[k + 3], v[k + 2], Cliff); if (SkyLanes.OnBridge(lane, s1 + .5f)) Wall(v[k + 1], v[k + 2], Cliff); if (SkyLanes.OnBridge(lane, s - .5f)) Wall(v[k + 3], v[k], Cliff); }
            }
            float run = 0; var p = SkyLanes.Paths[lane];
            for (int i = 0; i < p.Length; i++) { if (i > 0) run += Vector2.Distance(p[i - 1], p[i]); if (SkyLanes.OnBridge(lane, run)) continue; Disc(v, t, p[i], half, y, 24); if (rim) Rim(p[i], half, y, 24); }
        }
        var ev = new List<Vector3>(); var et = new List<int>();   // the rough edge band of the road
        for (int l = 0; l < SkyLanes.Paths.Length; l++) { Strip(gv, gt, l, SkyLanes.LaneHalf + Shoulder, -.14f, true); Strip(rv, rt, l, SkyLanes.LaneHalf - Road - .9f, .06f, false); Strip(ev, et, l, SkyLanes.LaneHalf + .8f, 0, false); }   // road 6 cm over the edge band: at 2 cm one lane's edge band flickered through the other lane's road where they meet
        foreach (var g in SkyLanes.Gates)
        {   // the stone paths into the jungle, in 0.5 m steps so they follow the slope down
            var dir = (g.b - g.a).normalized; var n = new Vector2(-dir.y, dir.x) * 2.2f; float len = Vector2.Distance(g.a, g.b) + 2;
            for (float s = -1; s < len - 1; s += .5f) { var a = g.a + dir * s; var b = g.a + dir * (s + .5f); int k = rv.Count; foreach (var p2 in new[] { a + n, b + n, b - n, a - n }) rv.Add(new Vector3(p2.x, .12f + SkyLanes.Height(p2.x, p2.y), p2.y)); rt.AddRange(new[] { k, k + 1, k + 2, k, k + 2, k + 3 }); }
        }
        foreach (var b in new[] { SkyLanes.BaseOf(0), SkyLanes.BaseOf(1) }) { Disc(gv, gt, b, SkyLanes.BaseR + 3, -.14f); Rim(b, SkyLanes.BaseR + 3, -.14f); Disc(rv, rt, b, SkyLanes.BaseR, .06f); }   // the base plaza at the road's height: at 0 it lay exactly on the lanes' edge band and flickered
        Disc(gv, gt, SkyLanes.Jungle, SkyLanes.JungleR + 1, -.04f, 56); Rim(SkyLanes.Jungle, SkyLanes.JungleR + 1, -.04f, 56);
        // the small floating islands round about: grass on a rock, a tree or a ruin on top
        var isles = new[] { (new Vector2(-16, 24), 4.5f), (new Vector2(-18, -20), 5f), (new Vector2(54, 26), 4f), (new Vector2(56, -16), 5.5f), (new Vector2(18, 58), 4f), (new Vector2(30, -56), 4.5f), (new Vector2(-14, 0), 3.5f), (new Vector2(46, 2), 3f) };
        foreach (var (c, r) in isles) { Disc(gv, gt, c, r, -.03f, 28); Rim(c, r, -.03f, 28); }
        GameObject MeshGo(string name, List<Vector3> v, List<int> t, Material mat, bool flat)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.SetVertices(v); mesh.SetTriangles(t, 0);
            mesh.SetUVs(0, v.Select(p => flat ? new Vector2(p.x / Uv, p.z / Uv) : new Vector2((p.x + p.z) / Uv, p.y / Uv)).ToList());
            mesh.RecalculateNormals(); mesh.RecalculateBounds();   // the land is no longer level: real normals
            var path = $"{Gen}/{name}.asset"; if (File.Exists(path)) AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name); go.transform.SetParent(root); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = mat; return go;
        }
        foreach (var old in new[] { "SkyLanesPaving", "SkyLanesJungleGround", "SkyLanesRim" }) if (File.Exists($"{Gen}/{old}.asset")) AssetDatabase.DeleteAsset($"{Gen}/{old}.asset");
        var kv = new List<Vector3>(); var kt = new List<int>();
        // (no curb under the lanes any more: the road's rough edge band shows the grass and earth beneath it)
        foreach (var b in new[] { SkyLanes.BaseOf(0), SkyLanes.BaseOf(1) }) Disc(kv, kt, b, SkyLanes.BaseR + .35f, -.06f);
        MeshGo("SkyLanesGrass", gv, gt, grassMat, true); MeshGo("SkyLanesRoad", rv, rt, stoneMat, true); { var em = TexMat("SkyLanesRoadEdge", edgeTex, new Color(.74f, .75f, .74f), .04f); em.SetTexture("_BumpMap", stoneNrm); em.EnableKeyword("_NORMALMAP"); em.SetFloat("_AlphaClip", 1); em.SetFloat("_Cutoff", .5f); em.EnableKeyword("_ALPHATEST_ON"); em.renderQueue = 2450; MeshGo("SkyLanesRoadEdge", ev, et, em, true); } MeshGo("SkyLanesCurb", kv, kt, TexMat("SkyLanesCurbStone", stoneTex, new Color(.5f, .5f, .47f), .1f), true);
        MeshGo("SkyLanesCliffs", cv, ct, rockMat, false).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ---- rock hanging under everything ----
        var under = Group("Underside");
        GameObject Hang(Vector2 at, float h, float top, Transform parent) { var g = Fit(Load(N("namaqualand_boulder_03")), new Vector3(at.x, 0, at.y), R(0, 360), h, parent); if (!g) return null; g.transform.position += Vector3.up * (top - Bounds(g).max.y); return g; }
        for (int l = 0; l < SkyLanes.Paths.Length; l++) { float len = SkyLanes.Length(l); for (float s = 3; s < len - 3; s += 7) if (!SkyLanes.OnBridge(l, s)) Hang(SkyLanes.PointAt(l, s), R(8, 13), -Cliff + 1, under); }
        foreach (var b in new[] { SkyLanes.BaseOf(0), SkyLanes.BaseOf(1) }) { Hang(b, 20, -Cliff + 1, under); Hang(b + new Vector2(R(-4, 4), R(-4, 4)), 11, -Cliff - 8, under); }
        Hang(SkyLanes.Jungle, 22, -Cliff + 1, under); foreach (var o in new[] { new Vector2(-7, 5), new Vector2(7, -5), new Vector2(5, 7), new Vector2(-5, -7) }) Hang(SkyLanes.Jungle + o, R(10, 14), -Cliff + 1, under);
        foreach (var (c, r) in isles) Hang(c, r * 2.2f, -Cliff + 1, under);

        {   // boulders set into the rims at a few depths, so the island edges read as broken rock and not one straight wall
            var edge = Group("CliffEdge"); var boulder = Load(N("namaqualand_boulder_03"));
            void Edge(Vector2 at, Vector2 outward) { var p = at + outward * R(.2f, .9f); var g = Fit(boulder, new Vector3(p.x, 0, p.y), R(0, 360), R(2.2f, 3.8f), edge); if (g) g.transform.position += Vector3.up * (R(-1.6f, -.25f) - Bounds(g).max.y); }
            for (int l = 0; l < SkyLanes.Paths.Length; l++)
            {
                float len = SkyLanes.Length(l);
                for (float s = 1; s < len - 1; s += R(3.5f, 5.5f))
                {
                    if (SkyLanes.OnBridge(l, s)) continue; var a = SkyLanes.PointAt(l, s); var dn = (SkyLanes.PointAt(l, s + .5f) - SkyLanes.PointAt(l, s - .5f)).normalized; var side = new Vector2(-dn.y, dn.x);
                    foreach (int sd in new[] { -1, 1 }) { var q = a + side * sd * (SkyLanes.LaneHalf + Shoulder); if (SkyLanes.Inside(q, .8f)) continue; Edge(q, side * sd); }   // not where the lane opens into a base or the jungle
                }
            }
            foreach (var (c, r, n) in new[] { (SkyLanes.BaseOf(0), SkyLanes.BaseR + 3, 22), (SkyLanes.BaseOf(1), SkyLanes.BaseR + 3, 22), (SkyLanes.Jungle, SkyLanes.JungleR + 1, 26) })
                for (int i = 0; i < n; i++) { float a = i / (float)n * Mathf.PI * 2 + R(-.1f, .1f); var o = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); var q = c + o * r; if (SkyLanes.Inside(q, 1)) continue; Edge(q, o); }
        }
        {   // braziers round each spawn pool (two beside the water, two out at the base's sides): warm firelight against the cool sky
            var bz = Group("Braziers"); var pit = Load("Assets/Game/Models/Props/stone_fire_pit/stone_fire_pit_1k.gltf"); 
            foreach (float side in new[] { 1f, -1f })
                foreach (var o in new[] { new Vector2(5.4f, -1.4f), new Vector2(-5.4f, -1.4f), new Vector2(8.2f, -7.4f), new Vector2(-8.2f, -7.4f) })
                {
                    var q = new Vector2(o.x, (SkyLanes.FountainZ + o.y) * side); var p = new Vector3(q.x, 0, q.y);   // level ground: the lift loop below raises every piece onto the base
                    var col = Tint(Fit(Load(AR + "Column_broken_small.prefab"), p, R(0, 360), 1.3f, bz), "moss", Pale); float top = col ? Bounds(col).max.y : 0;   // a stone stand, so the fire is seen from the side and not only from above
                    var g = Place(pit, new Vector3(p.x, top, p.z), R(0, 360), .7f, false, bz); var f = new GameObject("Flame").transform; f.SetParent(bz); f.position = new Vector3(p.x, g ? Bounds(g).max.y - .08f : top + .2f, p.z); f.localScale = Vector3.one * 3.4f; f.gameObject.AddComponent<Flame>();   // our own fire (1 = 40 cm brazier), sized to the 1.4 m pit
                    var l = PointLight(new Vector3(p.x, top + 1f, p.z), new Color(1, .52f, .2f), 7, 10); l.transform.SetParent(bz, true); l.gameObject.AddComponent<FireFlicker>();
                }
        }
        // ---- bridges: a stone deck with parapets over each gap of the curved lane ----
        var walls = Group("LaneWalls"); var wallSmall = Load(AR + "Wall_small.prefab"); var fire = Load("Assets/ThirdParty/CartoonFX/CFXR Prefabs/Fire/CFXR Fire.prefab");
        string[] posts = { "Stone_post_1", "Stone_post_2", "Stone_post_3", "Stone_post_5" };
        foreach (var br in SkyLanes.Bridges)
        {
            var a = SkyLanes.PointAt(1, br.x - 1.5f); var b = SkyLanes.PointAt(1, br.y + 1.5f); var d = (b - a); float len = d.magnitude; var mid = (a + b) / 2;
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(deck.GetComponent<Collider>()); deck.name = "BridgeDeck"; deck.transform.SetParent(walls);
            deck.transform.position = new Vector3(mid.x, -.4f, mid.y); deck.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y)); deck.transform.localScale = new Vector3(SkyLanes.BridgeHalf * 2 + .8f, .8f, len); deck.GetComponent<Renderer>().sharedMaterial = stoneMat;
            var keel = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(keel.GetComponent<Collider>()); keel.name = "BridgeArch"; keel.transform.SetParent(walls);
            keel.transform.position = new Vector3(mid.x, -2.2f, mid.y); keel.transform.rotation = deck.transform.rotation; keel.transform.localScale = new Vector3(SkyLanes.BridgeHalf * 2 - 1, 2.8f, len * .5f); keel.GetComponent<Renderer>().sharedMaterial = rockMat;
            var dn = d.normalized; var side = new Vector2(-dn.y, dn.x); float yaw = Mathf.Atan2(dn.x, dn.y) * Mathf.Rad2Deg;
            for (float t = 1.2f; t < len - 1; t += 2.15f) foreach (int sd in new[] { -1, 1 }) { var q = a + dn * t + side * sd * (SkyLanes.BridgeHalf + .35f); Tint(Fit(wallSmall, new Vector3(q.x, 0, q.y), yaw, 1.1f, walls), "moss", Pale); }
            foreach (var e in new[] { a, b }) foreach (int sd in new[] { -1, 1 }) { var q = e + side * sd * (SkyLanes.BridgeHalf + .6f); var pg = Tint(Fit(Load(AR + posts[rnd.Next(posts.Length)] + ".prefab"), new Vector3(q.x, 0, q.y), R(0, 360), 2.4f, walls), "moss", Pale); if (pg) Place(fire, new Vector3(q.x, Bounds(pg).max.y, q.y), 0, FireSize, false, walls); }
        }
        // ---- low walls along the lanes, a fire post every so often, kept off the walkable ground ----
        for (int l = 0; l < SkyLanes.Paths.Length; l++)
        {
            float len = SkyLanes.Length(l); int n = 0;
            for (float s = 2; s < len - 2; s += 2.3f)
            {
                if (SkyLanes.OnBridge(l, s) || SkyLanes.OnBridge(l, s + 1.5f) || SkyLanes.OnBridge(l, s - 1.5f)) continue;
                var a = SkyLanes.PointAt(l, s); var dn = (SkyLanes.PointAt(l, s + .5f) - SkyLanes.PointAt(l, s - .5f)).normalized; var side = new Vector2(-dn.y, dn.x); float yaw = Mathf.Atan2(dn.x, dn.y) * Mathf.Rad2Deg;
                foreach (int sd in new[] { -1, 1 })
                {
                    var q = a + side * sd * (SkyLanes.LaneHalf + .8f); if (SkyLanes.Inside(q, .4f)) continue;   // where the lane opens into a base or the jungle
                    if (++n % 3 == 0) { var pg = Tint(Fit(Load(AR + posts[rnd.Next(posts.Length)] + ".prefab"), new Vector3(q.x, 0, q.y), R(0, 360), 2.2f, walls), "moss", Pale); if (pg) Place(fire, new Vector3(q.x, Bounds(pg).max.y, q.y), 0, FireSize, false, walls); }   // a fire post about every 7 m, as in the painting
                    else if (rnd.Next(10) < 8) Tint(Fit(wallSmall, new Vector3(q.x, 0, q.y), yaw + R(-4, 4), R(.9f, 1.3f), walls), "moss", Pale);
                }
            }
        }
        {   // grass tufts and flowers along the shoulders, off the walls and the walkable ground
            var tufts = Group("Tufts");
            for (int l = 0; l < SkyLanes.Paths.Length; l++) { float len = SkyLanes.Length(l); for (float s = 1; s < len - 1; s += 1.4f) { if (SkyLanes.OnBridge(l, s)) continue; var a = SkyLanes.PointAt(l, s); var dn = (SkyLanes.PointAt(l, s + .5f) - SkyLanes.PointAt(l, s - .5f)).normalized; var side = new Vector2(-dn.y, dn.x);
                foreach (int sd in new[] { -1, 1 }) { if (rnd.Next(3) == 0) continue; var q = a + side * sd * (SkyLanes.LaneHalf + R(1.3f, 2.1f)) + dn * R(-.5f, .5f); if (SkyLanes.Inside(q, .3f)) continue; var at = new Vector3(q.x, 0, q.y);
                    if (rnd.Next(4) == 0) Fit(Load(PT + "Flowers/PT_Poppy_02.prefab"), at, R(0, 360), R(.3f, .5f), tufts); else Fit(Load(PT + "Plants/PT_Grass_02.prefab"), at, R(0, 360), R(.35f, .6f), tufts); } } }
        }
        // ---- team banners on tall poles near each base, both lanes ----
        var wood = Lit("SkyWood", "weathered_brown_planks", 1, .1f, new Color(.78f, .6f, .45f));
        Material Cloth(string name, Color c) { var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); } m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .2f); m.SetFloat("_Cull", 0); EditorUtility.SetDirty(m); return m; }
        var clothBlue = Cloth("SkyBannerBlue", new Color(.18f, .38f, .85f)); var clothRed = Cloth("SkyBannerRed", new Color(.75f, .14f, .12f)); var gold = Cloth("SkyBannerTrim", new Color(1f, .78f, .3f));
        for (int team = 0; team < 2; team++)
            for (int l = 0; l < SkyLanes.Paths.Length; l++)
                foreach (float k in new[] { 7f, 15f })
                {
                    float len = SkyLanes.Length(l), s = team == 0 ? k : len - k; var a = SkyLanes.PointAt(l, s); var dn = (SkyLanes.PointAt(l, s + .5f) - SkyLanes.PointAt(l, s - .5f)).normalized; var side = new Vector2(-dn.y, dn.x);
                    foreach (int sd in new[] { -1, 1 })
                    {
                        var q = a + side * sd * (SkyLanes.LaneHalf + 1.6f); if (SkyLanes.Inside(q, .2f)) continue; var at = new Vector3(q.x, 0, q.y);
                        var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(pole.GetComponent<Collider>()); pole.name = "BannerPole"; pole.transform.SetParent(walls); pole.transform.position = at + Vector3.up * 2.6f; pole.transform.localScale = new Vector3(.14f, 2.6f, .14f); pole.GetComponent<Renderer>().sharedMaterial = wood;
                        var bar = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(bar.GetComponent<Collider>()); bar.name = "BannerBar"; bar.transform.SetParent(walls); bar.transform.position = at + Vector3.up * 4.9f; bar.transform.rotation = Quaternion.LookRotation(new Vector3(dn.x, 0, dn.y)); bar.transform.localScale = new Vector3(.08f, .08f, 1.3f); bar.GetComponent<Renderer>().sharedMaterial = gold;
                        var cl = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(cl.GetComponent<Collider>()); cl.name = "BannerCloth"; cl.transform.SetParent(walls); cl.transform.position = at + Vector3.up * 3.85f; cl.transform.rotation = Quaternion.LookRotation(new Vector3(side.x, 0, side.y)); cl.transform.localScale = new Vector3(1.1f, 2.1f, 1); cl.GetComponent<Renderer>().sharedMaterial = team == 0 ? clothBlue : clothRed;
                    }
                }

        // ---- our base (+Z): a ruined temple court, blue crystals, waterfalls pouring off the back of the island ----
        var home = new Vector3(0, 0, SkyLanes.Base.y); var blue = GlowMat("SkyCrystalBlue", new Color(.35f, .75f, 1f)); var deco = Group("BaseDecor");
        var plat = Tint(Fit(Load(AR + "Platform_circle.prefab"), home, 0, .6f), "moss", new Color(2.3f, 2.35f, 2.15f)); if (plat) { plat.name = "BasePlatform"; plat.transform.localScale = new Vector3(plat.transform.localScale.x * 1.5f, plat.transform.localScale.y, plat.transform.localScale.z * 1.5f); var pb = Bounds(plat); plat.transform.position += new Vector3(home.x - pb.center.x, .03f - pb.max.y, home.z - pb.center.z); }
        string[] arches = { "Arch_1", "Arch_2", "Arch_3", "Arch_4", "Arch_5" };
        for (int i = 0; i < 5; i++) { float a = Mathf.Lerp(25, 155, i / 4f) * Mathf.Deg2Rad; var p = home + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (SkyLanes.BaseR + 1.4f); Tint(Fit(Load(AR + arches[i] + ".prefab"), p, Mathf.Atan2(home.x - p.x, home.z - p.z) * Mathf.Rad2Deg + 90, R(7.5f, 9f), deco), "moss", Pale); }
        foreach (int sd in new[] { -1, 1 }) { Tint(Fit(Load(AR + "Column.prefab"), home + new Vector3(sd * 9.2f, 0, -4f), R(0, 360), 5.2f), "moss", Pale); Tint(Fit(Load(AR + "Broken_column.prefab"), home + new Vector3(sd * 10f, 0, 2f), R(0, 360), 2.1f, deco), "moss", Pale); }
        Tint(Fit(Load(AR + "Stone_structure.prefab"), home + new Vector3(0, 0, SkyLanes.BaseR + 4f), 180, 12f, deco), "moss", Pale);
        PinkFoliage(Fit(Load(PT + "Trees/PT_Fruit_Tree_01_green.prefab"), home + new Vector3(-11.5f, 0, 5), R(0, 360), 6.5f, deco)); PinkFoliage(Fit(Load(PT + "Trees/PT_Fruit_Tree_01_green.prefab"), home + new Vector3(11.8f, 0, 6), R(0, 360), 6f, deco));
        foreach (var (x, z, h) in new[] { (-8f, 7f, 2.6f), (-9f, 5.5f, 1.6f), (8.5f, 7.5f, 2.9f), (9.5f, 6f, 1.7f), (-3.5f, 11.5f, 3.4f), (3.8f, 11.8f, 3f), (0f, 13.2f, 4.2f) }) Crystal(home + new Vector3(x, 0, z), h, blue, deco, 14);

        var waterMat = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/SkyWaterfall.mat"); if (!waterMat) { waterMat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(waterMat, $"{Gen}/SkyWaterfall.mat"); }
        waterMat.SetTexture("_BaseMap", waterTex); waterMat.SetColor("_BaseColor", new Color(.85f, .95f, 1f, .9f)); waterMat.SetFloat("_Surface", 1); waterMat.SetFloat("_Blend", 0); waterMat.SetFloat("_ZWrite", 0); waterMat.SetFloat("_Cull", 0);
        waterMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); waterMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); waterMat.SetOverrideTag("RenderType", "Transparent"); waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); waterMat.renderQueue = 3000; EditorUtility.SetDirty(waterMat);
        foreach (var (ang, w) in new[] { (60f, 3.4f), (90f, 4.2f), (120f, 3.2f), (20f, 2.4f), (160f, 2.6f) })
        {   // a sheet of water falling from the rim, a pool of mist where it leaves
            float a = ang * Mathf.Deg2Rad; var p = home + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (SkyLanes.BaseR + 3.05f);
            var fall = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(fall.GetComponent<Collider>()); fall.name = "Waterfall"; fall.transform.SetParent(deco);
            fall.transform.position = p + Vector3.down * 17; fall.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); fall.transform.localScale = new Vector3(w, 34, 1);
            var mr = fall.GetComponent<MeshRenderer>(); mr.sharedMaterial = waterMat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; var dr = fall.AddComponent<Drift>(); dr.speed = new Vector2(0, .45f); dr.tile = 1;
            var lip = GameObject.CreatePrimitive(PrimitiveType.Cube); Object.DestroyImmediate(lip.GetComponent<Collider>()); lip.name = "WaterLip"; lip.transform.SetParent(deco); lip.transform.position = p + Vector3.up * .02f - new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 1.4f; lip.transform.rotation = fall.transform.rotation; lip.transform.localScale = new Vector3(w * .9f, .04f, 2.8f); lip.GetComponent<Renderer>().sharedMaterial = waterMat;
        }
        // ---- their base (-Z): a dark castle with spires, orange crystals ----
        var foe = new Vector3(0, 0, -SkyLanes.Base.y); var orange = GlowMat("SkyCrystalOrange", new Color(1f, .5f, .15f)); var dark = new Color(.42f, .38f, .48f);
        var fortWalls = new[] { Load(D + "Walls/Wall_A.prefab"), Load(D + "Walls/Wall_B.prefab"), Load(D + "Walls/Wall_C.prefab") };
        for (int i = 0; i < 7; i++) { float a = -Mathf.Lerp(18, 162, i / 6f) * Mathf.Deg2Rad; var p = foe + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (SkyLanes.BaseR + .6f); var w = Tint(Place(fortWalls[rnd.Next(3)], p, Mathf.Atan2(foe.x - p.x, foe.z - p.z) * Mathf.Rad2Deg), "dark", dark); if (w) w.name = "FortWall"; }
        foreach (var (x, z, h) in new[] { (-11f, -6f, 17f), (11f, -6f, 17f), (-6.5f, -12.5f, 22f), (6.5f, -12.5f, 22f), (0f, -14.5f, 28f) })
        {   // spires: a dark column with a dark spike on top
            var col = Tint(Fit(Load(AR + "Column.prefab"), foe + new Vector3(x, 0, z), R(0, 360), h, deco), "dark", dark); if (!col) continue; col.transform.localScale = Vector3.Scale(col.transform.localScale, new Vector3(1.8f, 1, 1.8f));
            var spike = Crystal(foe + new Vector3(x, Bounds(col).max.y - .3f, z), h * .35f, Cloth("SkyCastleSpike", new Color(.18f, .14f, .22f)), deco); spike.transform.rotation = Quaternion.identity;
        }
        foreach (int sd in new[] { -1, 1 }) Fit(Load("Assets/Symphonie/Ruins/URP/Prefabs/archway_pillar02.prefab"), foe + new Vector3(sd * 6.2f, 0, 8.4f), sd * 90, 7.5f);
        foreach (var (x, z, h) in new[] { (-8f, -7f, 2.6f), (8.5f, -7.5f, 2.9f), (-3.5f, -11.2f, 3.2f), (3.8f, -11.5f, 3f) }) Crystal(foe + new Vector3(x, 0, z), h, orange, deco, 14);
        {   // each side's spawn pool behind its core: a stone basin of water ringed in the side's colour, a faint wall of light round it (the other side cannot enter)
            var pools = Group("Fountains"); var basin = Lit("SkyPoolStone", "mossy_brick", 2, .1f, new Color(.62f, .64f, .6f));
            Material Water(string name, Color c)
            {   // deep, glossy, a little see-through: the sky and the lights read in it
                var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
                m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .96f); m.SetFloat("_Metallic", .1f); m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 0); m.SetFloat("_ZWrite", 0);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3000;
                m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", c * .25f); EditorUtility.SetDirty(m); return m;
            }
            GameObject Tube(string name, Vector3 at, float r, float h, Material m)
            {   // an open ring wall of light: no lid, which washed the whole pool white
                const int seg = 40; var v = new Vector3[(seg + 1) * 2]; var uv = new Vector2[v.Length]; var t = new int[seg * 6];
                for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r; v[i * 2] = d; v[i * 2 + 1] = d + Vector3.up * h; uv[i * 2] = new Vector2(i / (float)seg, 0); uv[i * 2 + 1] = new Vector2(i / (float)seg, 1); if (i < seg) { int k = i * 6, b = i * 2; t[k] = b; t[k + 1] = b + 1; t[k + 2] = b + 2; t[k + 3] = b + 1; t[k + 4] = b + 3; t[k + 5] = b + 2; } }
                var mesh = new Mesh { name = "PoolVeil", vertices = v, uv = uv, triangles = t }; mesh.RecalculateBounds(); var mp = $"{Gen}/PoolVeil.asset"; if (!File.Exists(mp)) AssetDatabase.CreateAsset(mesh, mp); else mesh = AssetDatabase.LoadAssetAtPath<Mesh>(mp);
                var go = new GameObject(name); go.transform.SetParent(pools); go.transform.position = at; go.AddComponent<MeshFilter>().sharedMesh = mesh; var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = m; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return go;
            }
            Material Veil(string name, Color c)
            {
                var path = $"{Gen}/{name}.mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(m, path); }
                m.SetColor("_BaseColor", c); m.SetFloat("_Surface", 1); m.SetFloat("_Blend", 2); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
                m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One); m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One); m.SetOverrideTag("RenderType", "Transparent"); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = 3010; EditorUtility.SetDirty(m); return m;
            }
            GameObject Cyl(string name, Vector3 at, float r, float h, Material m) { var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Object.DestroyImmediate(c.GetComponent<Collider>()); c.name = name; c.transform.SetParent(pools); c.transform.position = at + Vector3.up * h / 2; c.transform.localScale = new Vector3(r * 2, h / 2, r * 2); c.GetComponent<Renderer>().sharedMaterial = m; c.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; return c; }
            for (int team = 0; team < 2; team++)
            {
                var at = SkyLanes.Fountain(team); var col = team == 0 ? new Color(.35f, .75f, 1f) : new Color(1f, .45f, .2f); float r = SkyLanes.FountainR;
                {   // a raised stone rim with real thickness (outer wall, top, inner wall) round shallow water, a lower step outside it
                    GameObject Ring(string name, float r0, float r1, float h, float y0)
                    {
                        const int seg = 48; var v = new List<Vector3>(); var tt = new List<int>();
                        for (int i = 0; i <= seg; i++) { float a = i / (float)seg * Mathf.PI * 2; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); v.Add(d * r1 + Vector3.up * y0); v.Add(d * r1 + Vector3.up * (y0 + h)); v.Add(d * r0 + Vector3.up * (y0 + h)); v.Add(d * r0 + Vector3.up * y0); }
                        for (int i = 0; i < seg; i++) { int b = i * 4, n = b + 4; tt.AddRange(new[] { b, n, b + 1, b + 1, n, n + 1, b + 1, n + 1, b + 2, b + 2, n + 1, n + 2, b + 2, n + 2, b + 3, b + 3, n + 2, n + 3 }); }
                        var mesh = new Mesh { name = name }; mesh.SetVertices(v); mesh.SetTriangles(tt, 0); mesh.SetUVs(0, v.Select(p => new Vector2((p.x + p.z) * .5f, p.y * .5f)).ToList()); mesh.RecalculateNormals(); mesh.RecalculateBounds();
                        var mp = $"{Gen}/{name}.asset"; if (File.Exists(mp)) AssetDatabase.DeleteAsset(mp); AssetDatabase.CreateAsset(mesh, mp);
                        var go = new GameObject(name); go.transform.SetParent(pools); go.transform.position = at; go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = basin; return go;
                    }
                    Ring("PoolStep" + team, r + .55f, r + 1.35f, .22f, 0);   // a broad low step all round
                    Ring("PoolRimStone" + team, r, r + .6f, .55f, 0);       // the basin's edge, knee high
                }
                Cyl("PoolWater", at + Vector3.up * .14f, r, .02f,   // shallow water inside the rim: heroes stand in it ankle deep
                    Water(team == 0 ? "SkyPoolWaterBlue" : "SkyPoolWaterRed", team == 0 ? new Color(.06f, .2f, .34f, .62f) : new Color(.3f, .11f, .07f, .62f)));
                Tube("PoolRim", at + Vector3.up * .15f, r - .02f, .08f, Veil(team == 0 ? "SkyPoolRimBlue" : "SkyPoolRimRed", col * .6f));   // a glowing lip round the water (a filled glow disc hid the water)
                Tube("PoolVeil", at, r + .45f, 2.2f, Veil(team == 0 ? "SkyPoolVeilBlue" : "SkyPoolVeilRed", col * .1f));
                for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; var p = at + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * (r + .55f); var pg = Tint(Fit(Load(AR + "Column_broken_small.prefab"), p, R(0, 360), 1.2f, pools), "moss", Pale); if (pg) Place(fire, new Vector3(p.x, Bounds(pg).max.y, p.z), 0, FireSize, false, pools); }
                var pl = PointLight(at + Vector3.up * 1.5f, col, 1.2f, 8); pl.name = "PoolLight"; pl.transform.SetParent(pools);
                {   // a jet of water rising from the middle and falling back in drops, catching the side's light
                    var jet = new GameObject("PoolJet"); jet.transform.SetParent(pools); jet.transform.position = at + Vector3.up * .15f; var ps = jet.AddComponent<ParticleSystem>(); ps.Stop();
                    var mm = ps.main; mm.loop = true; mm.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 1.3f); mm.startSpeed = new ParticleSystem.MinMaxCurve(4.2f, 5.2f); mm.startSize = new ParticleSystem.MinMaxCurve(.08f, .16f); mm.gravityModifier = .9f; mm.maxParticles = 160; mm.startColor = Color.Lerp(col, Color.white, .55f); mm.simulationSpace = ParticleSystemSimulationSpace.World;
                    var em = ps.emission; em.rateOverTime = 70; var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 9; sh.radius = .12f; sh.rotation = new Vector3(-90, 0, 0);
                    var cl = ps.colorOverLifetime; cl.enabled = true; var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(.9f, 0), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0, 1) }); cl.color = g;
                    var pr = jet.GetComponent<ParticleSystemRenderer>(); var dropTex = Paint("SkyDrop2", 32, 32, (u, v) => { float d = Vector2.Distance(new Vector2(u, v), new Vector2(.5f, .5f)) * 2, k = Mathf.Clamp01(1 - d) * Mathf.Clamp01(1 - d); return new Color(k, k, k, k); });   // a soft round drop, dark at the edge: the additive blend ignores alpha
                    var dropMat = Veil("SkyPoolDrops", Color.white * .7f); dropMat.SetTexture("_BaseMap", dropTex); pr.sharedMaterial = dropMat; pr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

            }
        }
        foreach (var (b, col) in new[] { (home, new Color(.55f, .8f, 1f)), (foe, new Color(1, .45f, .2f)) }) { var lt = PointLight(b + Vector3.up * 6 + new Vector3(0, 0, b.z > 0 ? 4 : -4), col, 7, 20); lt.name = "BaseLight"; }

        // ---- jungle: trees round the edge, flowers, mushrooms and grass tufts inside, a glowing cave, the middle kept open for camps ----
        {   // big-leaved plants along the outer edge of the lane shoulders and round the bases (the owner's road picture): outside the walkable ground,
            // so nothing is blocked; Poly Haven models already in the project (pachira, banana, fern, shrubs)
            var leafy = Group("LeafyEdge"); string[] big = { N("pachira_aquatica_01"), N("banana_tree_01"), N("banana_tree_02") }, low = { N("fern_02"), N("shrub_02"), N("shrub_03"), N("shrub_04") };
            void Plant(Vector2 q) { if (SkyLanes.Inside(q, .6f) || SkyLanes.OnGate(q, 1.5f)) return; var at = new Vector3(q.x, 0, q.y); bool tall = rnd.Next(4) == 0; Fit(Load(tall ? big[rnd.Next(big.Length)] : low[rnd.Next(low.Length)]), at, R(0, 360), tall ? R(1.6f, 2.4f) : R(.8f, 1.3f), leafy); }
            for (int l = 0; l < SkyLanes.Paths.Length; l++)
            {
                float len = SkyLanes.Length(l);
                for (float s2 = 3; s2 < len - 3; s2 += R(3.2f, 5))
                {
                    if (SkyLanes.OnBridge(l, s2)) continue; var a2 = SkyLanes.PointAt(l, s2); var dn = (SkyLanes.PointAt(l, s2 + .5f) - SkyLanes.PointAt(l, s2 - .5f)).normalized; var side = new Vector2(-dn.y, dn.x);
                    Plant(a2 + side * (SkyLanes.LaneHalf + R(1.2f, 1.9f)) * (rnd.Next(2) == 0 ? 1 : -1));
                }
            }
            foreach (var b in new[] { SkyLanes.BaseOf(0), SkyLanes.BaseOf(1) }) for (int i = 0; i < 18; i++) { float an = i / 18f * Mathf.PI * 2 + R(-.1f, .1f); Plant(b + new Vector2(Mathf.Cos(an), Mathf.Sin(an)) * (SkyLanes.BaseR + R(1.4f, 2.4f))); }
        }
        var jungle = Group("JungleDecor"); var violet = GlowMat("SkyCrystalViolet", new Color(.75f, .35f, 1f));
        var pines = new[] { PT + "Trees/PT_Pine_Tree_03_green.prefab", PT + "Trees/PT_Pine_Tree_03_green_cut.prefab" };
        bool OnLane(Vector2 q, float pad) { for (int l = 0; l < SkyLanes.Paths.Length; l++) { float dd = SkyLanes.Nearest(l, q, out _, out float s); if (dd < SkyLanes.HalfAt(l, s) + pad) return true; } return false; }
        for (int i = 0; i < 30; i++)
        {
            float a = i / 30f * Mathf.PI * 2 + R(-.08f, .08f), r = R(8, 12.5f); var q = SkyLanes.Jungle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; if (OnLane(q, 1.2f) || SkyLanes.OnGate(q, 1.2f)) continue; var at = new Vector3(q.x, 0, q.y);   // the ways in stay clear
            int kind = rnd.Next(6);
            if (kind < 2) Fit(Load(pines[rnd.Next(2)]), at, R(0, 360), R(6.5f, 9.5f)); else if (kind < 4) PinkFoliage(Fit(Load(PT + "Trees/PT_Fruit_Tree_01_green.prefab"), at, R(0, 360), R(4.5f, 6.5f)));
            else if (kind < 5) Fit(Load(PT + "Shrubs/PT_Generic_Shrub_01_green.prefab"), at, R(0, 360), R(1.2f, 1.9f), jungle); else Place(Load(N("namaqualand_boulder_03")), at, R(0, 360), R(.35f, .7f));
        }
        for (int i = 0; i < 60; i++)
        {
            float a = R(0, Mathf.PI * 2), r = Mathf.Sqrt(R(.1f, 1)) * 10.5f; var q = SkyLanes.Jungle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; if (OnLane(q, .5f)) continue; var at = new Vector3(q.x, 0, q.y); int kind = rnd.Next(10);
            if (kind < 4) Fit(Load(PT + "Flowers/PT_Poppy_02.prefab"), at, R(0, 360), R(.35f, .6f), jungle); else if (kind < 8) Fit(Load(PT + "Plants/PT_Grass_02.prefab"), at, R(0, 360), R(.4f, .7f), jungle); else Fit(Load(PT + "Mushrooms/PT_Caesars_Mushroom_01.prefab"), at, R(0, 360), R(.3f, .5f), jungle);
        }
        {   // the cave on the jungle's far side: a ruined arch with violet crystals glowing inside it
            var cave = SkyLanes.Jungle + new Vector2(6.5f, -6.5f); var at = new Vector3(cave.x, 0, cave.y); var arch = Fit(Load(AR + "Arch_5.prefab"), at, 45, 5.2f, jungle);
            foreach (var o in new[] { new Vector3(-.8f, 0, -.5f), new Vector3(.7f, 0, -.8f), new Vector3(0, 0, -1.4f) }) Crystal(at + o, R(1.2f, 2), violet, jungle, 18);
            var cl = PointLight(at + Vector3.up * 1.6f, new Color(.35f, .75f, 1f), 4, 10); cl.name = "CaveLight";
            for (int i = 0; i < 14; i++) { float a = R(0, Mathf.PI * 2), r = R(3, 10); var q = SkyLanes.Jungle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r; if (OnLane(q, .5f)) continue; Crystal(new Vector3(q.x, 0, q.y), R(.25f, .5f), violet, jungle, 25); }   // glowing violet blooms
            foreach (var o in new[] { new Vector2(-5, 3), new Vector2(4, 5) }) { var vl = PointLight(new Vector3(SkyLanes.Jungle.x + o.x, 1.2f, SkyLanes.Jungle.y + o.y), new Color(.7f, .4f, 1f), 1.6f, 6); vl.name = "BloomLight"; }
        }
        // ---- the small islands' trees and ruins ----
        var far = Group("Scenery");
        foreach (var (c, r) in isles) { var at = new Vector3(c.x, 0, c.y); int k = rnd.Next(3); if (k == 0) PinkFoliage(Fit(Load(PT + "Trees/PT_Fruit_Tree_01_green.prefab"), at, R(0, 360), r * 1.3f, far)); else if (k == 1) Fit(Load(pines[0]), at, R(0, 360), r * 1.8f, far); else Tint(Fit(Load(AR + "Broken_column.prefab"), at, R(0, 360), r * .7f, far), "moss", Pale); }
        // ---- drifting rocks, spires in the haze, the sea of cloud ----
        foreach (var (x, y, z, h) in new[] { (-30f, -4f, 36f, 6f), (-32f, 3f, -34f, 5f), (66f, -5f, 30f, 7f), (68f, 4f, -32f, 5f), (-22f, -9f, 66f, 6f), (36f, 6f, 70f, 4f), (26f, -7f, -70f, 6f), (-34f, 2f, -62f, 5f), (-6f, 8f, 52f, 2.5f), (44f, 9f, 48f, 2f), (62f, 7f, 0f, 3f), (-26f, 10f, 6f, 2.2f), (40f, 11f, -52f, 2.4f), (-12f, 7f, -50f, 2f), (14f, 12f, 64f, 1.8f), (70f, 10f, 16f, 2f) })
        { var rk = Fit(Load(N("namaqualand_boulder_03")), new Vector3(x, y, z), R(0, 360), h, far); if (rk) rk.transform.rotation = Quaternion.Euler(R(150, 210), R(0, 360), 0); }
        foreach (var (a, d, h) in new[] { (-150f, 130f, 70f), (-110f, 140f, 60f), (-70f, 125f, 85f), (-35f, 145f, 65f), (0f, 150f, 80f), (40f, 135f, 70f), (80f, 140f, 60f), (115f, 125f, 75f), (150f, 140f, 55f), (180f, 135f, 65f) })
        {
            float ar = a * Mathf.Deg2Rad; var sp = Fit(Load(N("namaqualand_boulder_03")), Vector3.zero, R(0, 360), h, far); if (!sp) continue;
            sp.transform.localScale = Vector3.Scale(sp.transform.localScale, new Vector3(.4f, 1, .4f)); var bb = Bounds(sp);
            sp.transform.position += new Vector3(18 + Mathf.Sin(ar) * d - bb.center.x, -40 - bb.min.y, -Mathf.Cos(ar) * d - bb.center.z);
        }
        CloudSea(far);
        {   // a cool light from behind the scene: edges of walls, towers and trees catch it, the shapes separate from the ground
            var rim = new GameObject("RimLight").AddComponent<Light>(); rim.transform.SetParent(root); rim.type = LightType.Directional; rim.color = new Color(.62f, .74f, 1f); rim.intensity = .55f; rim.shadows = LightShadows.None; rim.transform.rotation = Quaternion.Euler(28, -40, 0);
        }
        {   // soft clouds drifting at the map's edges, above the ground: something in front of the far scene
            var puff = Load("Assets/KayKit_MedievalHexagon/Models/decoration/nature/cloud_big.fbx"); var puffMat = AssetDatabase.LoadAssetAtPath<Material>($"{Gen}/SkyCloudHigh.mat");
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2 + R(-.15f, .15f); var at = new Vector3(18 + Mathf.Cos(a) * R(52, 66), R(4, 9), Mathf.Sin(a) * R(64, 78));
                var cl = Fit(puff, at, R(0, 360), R(5, 8), far); if (cl) foreach (var r in cl.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }
        {   // grass blades on the shoulders, round the bases and in the jungle (drawn near the camera only)
            var gf = Grass(new Color(.05f, .12f, .04f), new Color(.17f, .3f, .09f), new Color(.4f, .5f, .2f), .42f, .5f, 80); gf.skyLanes = true; gf.maxDistance = 34; gf.seed = 19;
        }
        {   // everything placed on level ground sits up on the raised bases and down in the jungle: each piece lifted by the land's height under it
            foreach (Transform g in root)
            {
                if (g.name.StartsWith("SkyLanes") || g.name is "Sun" or "RimLight" or "StageGrade" or "Scenery" || g.GetComponent<GrassField>()) continue;
                bool group = g.GetComponents<Component>().Length == 1 && g.childCount > 0;
                if (group) foreach (Transform c in g) c.position += Vector3.up * H(c.position.x, c.position.z);
                else g.position += Vector3.up * H(g.position.x, g.position.z);
            }
        }
        Static(root); foreach (var t in root.GetComponentsInChildren<Drift>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0); foreach (var t in root.GetComponentsInChildren<Spin>(true)) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        return root;
    }
    // Sky Lanes' own colour grade (a volume under the stage root, so it is on only while the map shows): richer colour, warm light, cool shadows
    static void Grade()
    {
        var go = new GameObject("StageGrade"); go.transform.SetParent(root); var vol = go.AddComponent<Volume>(); vol.isGlobal = true; vol.priority = 10;
        var path = $"{Gen}/SkyLanesGrade.asset"; if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        var prof = ScriptableObject.CreateInstance<VolumeProfile>();
        var ca = prof.Add<ColorAdjustments>(true); ca.saturation.Override(4); ca.contrast.Override(12); ca.postExposure.Override(-.1f);
        var wb = prof.Add<WhiteBalance>(true); wb.temperature.Override(-4); wb.tint.Override(2);   // cool, as in the painting
        var bl = prof.Add<Bloom>(true); bl.intensity.Override(.55f); bl.threshold.Override(1f); bl.scatter.Override(.7f);
        var smh = prof.Add<ShadowsMidtonesHighlights>(true); smh.shadows.Override(new Vector4(.88f, .95f, 1.12f, 0)); smh.highlights.Override(new Vector4(1.08f, 1.03f, .92f, 0));
        var vg = prof.Add<Vignette>(true); vg.intensity.Override(.26f); vg.smoothness.Override(.55f);
        AssetDatabase.CreateAsset(prof, path); foreach (var c in prof.components) AssetDatabase.AddObjectToAsset(c, prof); vol.sharedProfile = prof;
    }
    // puts the Sky Lanes map into the saved scene without rebuilding the rest, then the Windows player
    public static void AddSkyLanesAndWin()
    {
        rnd = new System.Random(47);
        var scene = EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity"); var main = Object.FindAnyObjectByType<Main>();
        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == "Stage_skylanes")) Object.DestroyImmediate(old);
        var active = main.stageRoots.FirstOrDefault(r => r && r.gameObject.activeSelf);
        var sl = SkyLanesStage(); NoCartoonFx(sl); sl.gameObject.SetActive(false);
        main.stageRoots = main.stageRoots.Where(r => r && r.name != "Stage_skylanes").Append(sl).ToArray();
        if (active) active.GetComponent<StageLook>().Apply(); EditorUtility.SetDirty(main); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        BuildWin();
    }
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
            Deck(Fit(bridge, new Vector3(bx, -.1f, ValleyRiver(bx)), 90 - slope, 2.2f));
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
