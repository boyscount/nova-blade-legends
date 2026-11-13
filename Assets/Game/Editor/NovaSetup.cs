// One-shot project setup for Nova Blade Legends (run in batchmode):
//   Unity.exe -batchmode -quit -projectPath . -executeMethod NovaSetup.All
// - URP pipeline assets
// - Mixamo FBX clips -> Humanoid
// - Humanoid avatars for the rigged heroes (glTF rigs from Blender, A-pose forced to a T-pose reference)
// - one shared Animator controller and a prefab per hero
// - check renders of every hero in a few poses (Builds/peek/*.png)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public static class NovaSetup
{
    const string Anim = "Assets/Game/Animations";
    const string Heroes = "Assets/Game/Models/Heroes";
    const string Prefabs = "Assets/Game/Resources/Heroes";
    // the classic skins live outside Resources, so they are not in the game's main data: each goes out as an AssetBundle (NovaBuild.SkinBundles, SkinPacks)
    public const string SkinPackDir = "Assets/Game/SkinPacks";
    public static bool IsPack(string id) => id.EndsWith("_classic") || id.EndsWith("_legend");   // model skins (classic, legend): built into asset bundles, fetched when worn
    static string PrefabPath(string id) => IsPack(id) ? $"{SkinPackDir}/Hero_{id}.prefab" : $"{Prefabs}/Hero_{id}.prefab";
    const string ControllerPath = "Assets/Game/Animations/Hero.controller";

    // state name -> (Mixamo file, loops)
    public static readonly Dictionary<string, (string file, bool loop)> Clips = new()
    {
        ["idle"] = ("Great Sword Idle", true), ["show"] = ("Great Sword Idle", true), ["run"] = ("Great Sword Run", true),
        ["atk0"] = ("Sword And Shield Attack", false), ["atk1"] = ("Great Sword Slash", false), ["atk2"] = ("Great Sword Slide Attack", false),
        ["spin"] = ("Great Sword High Spin Attack", false), ["cast"] = ("Standing 2H Magic Attack 01", false), ["guard"] = ("Great Sword Crouching", true),
        ["roll"] = ("Stand To Roll", false), ["death"] = ("Sword And Shield Death", false), ["hurt"] = ("Big Kidney Hit", false),
        ["leap"] = ("Big Jump", false), ["jump"] = ("Sword And Shield Jump", false), ["crouch"] = ("Standing To Crouch", false),
        // Soulslike set
        ["walk"] = ("Great Sword Walk", true), ["strafeL"] = ("Great Sword Strafe Left Walk", true), ["strafeR"] = ("Great Sword Strafe Right Walk", true),
        ["blockidle"] = ("Great Sword Blocking Idle", true), ["blockhit"] = ("Great Sword Blocked Impact", false), ["heavy"] = ("Great Sword Power Slash", false),
        ["jumpatk"] = ("Great Sword Jump Attack From Run", false), ["kick"] = ("Great Sword Side Kick", false), ["counter"] = ("Sword And Shield Downward Slash", false),
        ["backstep"] = ("Dodge Backward With Bow", false),
        // ranged / caster classes (Phase 2): bow draw + release, one-hand spell, two-hand cast
        ["bowdraw"] = ("Standing Draw Arrow", false), ["bowshot"] = ("Standing Aim Recoil", false),
        // character-select stances (lobby only): women and men each have their own (added once the files are in Animations/)
        ["show_f"] = ("Catwalk Idle To Twist R", true), ["show_m"] = ("Kneeling Down", false),
        ["cast1"] = ("Standing 1H Magic Attack 01", false), ["cast2"] = ("Standing 2H Cast Spell 01", false), ["sblock"] = ("Sword And Shield Block Idle", true), ["sblockhit"] = ("Sword And Shield Blocked Impact", false),
    };
    // per-class clips from Animations/new (Mixamo "Ch02_nonPBR@<name>.fbx"): set -> state -> (clip name, loops)
    public static readonly Dictionary<string, Dictionary<string, (string file, bool loop)>> Sets = new()
    {
        ["mage"] = new() { ["idle"] = ("mage_idle", true), ["show"] = ("show_mage|mage_idle", true), ["walk"] = ("mage_walk", true), ["run"] = ("mage_run", true), ["cast2"] = ("mage_cast_big", false) },
        ["bow"] = new() { ["show"] = ("bow_idle", true), ["idle"] = ("bow_idle", true), ["walk"] = ("bow_walk", true), ["run"] = ("bow_run", true) },
        ["ninja"] = new() { ["show"] = ("show_ninja", true), ["idle"] = ("ninja_idle", true), ["walk"] = ("ninja_walk", true), ["run"] = ("ninja_run", true), ["atk0"] = ("ninja_slash", false) },
        ["monk"] = new() { ["show"] = ("monk_idle", true), ["idle"] = ("monk_idle", true), ["walk"] = ("monk_walk", true), ["run"] = ("monk_run", true), ["atk0"] = ("monk_punch1", false), ["atk1"] = ("monk_punch2", false), ["atk2"] = ("monk_kick", false) },
        ["warrior"] = new() { ["show"] = ("show_warrior", true), ["atk0"] = ("sws_atk0", false), ["atk1"] = ("sws_atk1", false), ["atk2"] = ("sws_atk2", false) },
        ["shield"] = new() { ["show"] = ("show_warrior", true), ["idle"] = ("sws_idle", true), ["walk"] = ("sws_walk", true), ["run"] = ("sws_run", true), ["atk0"] = ("sws_atk0", false), ["atk1"] = ("sws_atk1", false), ["atk2"] = ("sws_atk2", false) },
        ["samurai"] = new() { ["show"] = ("show_samurai", true) },
    };
    // every class set also comes in a women's and a men's version: same attacks, their own walk, run and lobby stance
    static NovaSetup()
    {
        foreach (var set in Sets.Keys.ToList())
            foreach (var (sfx, run, walk, stance) in new[] { ("_f", "f_run", "f_walk", "f_idle"), ("_m", "m_run", "m_walk", "m_idle") })
                Sets[set + sfx] = set == "shield" ? new(Sets[set]) { ["show"] = (stance, true) } : new(Sets[set]) { ["run"] = (run, true), ["walk"] = (walk, true), ["show"] = (stance, true) };   // the shield set keeps its own walk and run: the shield arm stays up
    }
    public static string SetPath(string clip)
    {
        foreach (var c in clip.Split('|')) foreach (var p in new[] { $"{Anim}/new/Ch02_nonPBR@{c}.fbx", $"{Anim}/new/{c}.fbx" }) if (File.Exists(p)) return p;
        return null;
    }
    public static AnimationClip SetClip(string clip) => SetPath(clip) is string p ? AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__")) : null;
    public static void AnimSets()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        Directory.CreateDirectory("Assets/Game/Resources/Anim");
        // overrides swap clips, not states: "show" needs a base clip of its own or it always plays whatever replaces "idle"
        var states = ctrl.layers[0].stateMachine.states.Select(s => s.state).ToArray();
        var show = states.First(s => s.name == "show"); var idle = states.First(s => s.name == "idle");
        // one showcase idle for every hero in the lobby (the same lively motion for all): the mage's
        var shared = SetClip("show_mage"); if (shared && show.motion != shared) { show.motion = shared; EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets(); }
        if (!shared && show.motion == idle.motion)
        {
            const string ShowBase = Anim + "/show_base.anim";
            var copy = Object.Instantiate((AnimationClip)idle.motion); copy.name = "show_base";
            if (File.Exists(ShowBase)) AssetDatabase.DeleteAsset(ShowBase);
            AssetDatabase.CreateAsset(copy, ShowBase); show.motion = copy; EditorUtility.SetDirty(ctrl); AssetDatabase.SaveAssets();
            Debug.Log("[NovaSetup] show state has its own base clip");
        }
        foreach (var (set, clips) in Sets)
        {
            foreach (var (st, (file, loop)) in clips) if (SetPath(file) is string sp) Humanoid(sp, st, loop); else Debug.Log($"[NovaSetup] {set}:{st} not downloaded yet ({file})");
            var oc = new AnimatorOverrideController(ctrl) { name = set };
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); oc.GetOverrides(pairs);
            for (int i = 0; i < pairs.Count; i++)
            {
                var key = pairs[i].Key; var st = ctrl.layers[0].stateMachine.states.FirstOrDefault(s => s.state.motion == key).state;
                if (st != null && clips.TryGetValue(st.name, out var f) && SetPath(f.file) != null) pairs[i] = new(key, SetClip(f.file));
            }
            oc.ApplyOverrides(pairs);
            var path = $"Assets/Game/Resources/Anim/{set}.overrideController"; if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(oc, path); Debug.Log($"[NovaSetup] anim set {set}: {clips.Count} clips");
        }
        AssetDatabase.SaveAssets();
    }
    // Mixamo clip -> Humanoid, played in place (same settings as the shared clips)
    static void Humanoid(string path, string name, bool loop)
    {
        var mi = (ModelImporter)AssetImporter.GetAtPath(path);
        if (mi == null) { Debug.LogError("[NovaSetup] missing " + path); return; }
        mi.animationType = ModelImporterAnimationType.Human; mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        mi.importCameras = false; mi.importLights = false; mi.materialImportMode = ModelImporterMaterialImportMode.None;
        mi.SaveAndReimport();
        var clips = mi.defaultClipAnimations;
        foreach (var c in clips)
        {
            c.name = name; c.loopTime = loop;
            c.lockRootRotation = true; c.keepOriginalOrientation = true;
            c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = false;
            c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false;
        }
        mi.clipAnimations = clips; mi.SaveAndReimport();
    }
    public static readonly string[] RiggedHeroes = { "warrior", "samurai", "harpy", "spartan", "elf", "ninja", "hood", "sunpriest", "monk", "mage", "darkmage", "sorceress", "lyra", "rowan", "tenzin", "knight", "flamepriest", "tiger", "knight_classic", "warrior_classic", "samurai_classic", "harpy_classic", "spartan_classic", "elf_classic", "ninja_classic", "hood_classic", "sunpriest_classic", "monk_classic", "mage_classic", "darkmage_classic", "sorceress_classic", "lyra_classic", "rowan_classic", "tenzin_classic", "flamepriest_classic", "tiger_classic", "ninja_legend", "darkmage_legend", "flamepriest_legend", "tiger_legend", "monk_legend", "spartan_legend" };   // knight_classic: Garland's old look, a model skin
    // class heroes (auto-rigged by arena_autorig.py): build their avatars and prefabs against the existing controller
    public static readonly string[] ClassHeroes = { "warrior", "samurai", "harpy", "spartan", "elf", "ninja", "hood", "sunpriest", "monk", "mage", "darkmage", "sorceress", "lyra", "rowan", "tenzin", "knight", "flamepriest", "tiger", "knight_classic", "warrior_classic", "samurai_classic", "harpy_classic", "spartan_classic", "elf_classic", "ninja_classic", "hood_classic", "sunpriest_classic", "monk_classic", "mage_classic", "darkmage_classic", "sorceress_classic", "lyra_classic", "rowan_classic", "tenzin_classic", "flamepriest_classic", "tiger_classic", "ninja_legend", "darkmage_legend", "flamepriest_legend", "tiger_legend", "monk_legend", "spartan_legend" };   // knight_classic: Garland's old look, a model skin
    public static void NewHeroes()
    {
        AssetDatabase.ImportAsset(Heroes, ImportAssetOptions.ImportRecursive);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        // clips added to the table after the controller was built: import them and add their states in place (prefabs keep the controller)
        var sm = ctrl.layers[0].stateMachine; var have = sm.states.Select(s => s.state.name).ToHashSet();
        var missing = Clips.Where(kv => !have.Contains(kv.Key) && File.Exists($"{Anim}/{kv.Value.file}.fbx")).ToDictionary(kv => kv.Key, kv => kv.Value);
        if (missing.Count > 0)
        {
            var all = new Dictionary<string, (string, bool)>(Clips); Clips.Clear(); foreach (var kv in missing) Clips[kv.Key] = kv.Value;
            ImportClips(); Clips.Clear(); foreach (var kv in all) Clips[kv.Key] = kv.Value;
            foreach (var k in missing.Keys) { var st = sm.AddState(k); st.motion = Clip(k); st.speedParameterActive = true; st.speedParameter = "Speed"; }
            EditorUtility.SetDirty(ctrl); Debug.Log("[NovaSetup] added states " + string.Join(", ", missing.Keys));
        }
        foreach (var h in ClassHeroes) BuildHero(h, ctrl);
        AssetDatabase.SaveAssets(); Peek();
        Debug.Log("[NovaSetup] new heroes done");
    }

    public static readonly string[] Npcs = { "npc_quest", "npc_merchant", "npc_smith" };
    public static void BuildNpcs()
    {
        AssetDatabase.ImportAsset(Heroes, ImportAssetOptions.ImportRecursive);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath); foreach (var h in Npcs) BuildHero(h, ctrl); AssetDatabase.SaveAssets();
    }
    public static readonly string[] Monsters = { "e_skeleton", "e_goblin", "e_orc", "e_zombie", "e_cultist", "e_ghoul", "m_blue", "m_red", "b_lich", "b_treant" };
    public static void Enemies()
    {
        AssetDatabase.ImportAsset(Heroes, ImportAssetOptions.ImportRecursive);
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        foreach (var m in Monsters) BuildHero(m, ctrl);
        AssetDatabase.SaveAssets(); Debug.Log("[NovaSetup] enemies done");
    }

    public static void All()
    {
        Pipeline();
        // glTFast picks shaders for the active pipeline at import time, so re-import the models under URP
        AssetDatabase.ImportAsset("Assets/Game/Models", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
        ImportClips();
        var ctrl = BuildController();
        foreach (var h in RiggedHeroes) BuildHero(h, ctrl);
        AssetDatabase.SaveAssets();
        Peek();
        Debug.Log("[NovaSetup] done");
    }

    static void Pipeline()
    {
        var pc = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var mobile = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
        GraphicsSettings.defaultRenderPipeline = pc;
        var cur = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = i < QualitySettings.names.Length / 2 ? mobile : pc;
        }
        QualitySettings.SetQualityLevel(cur, false);
        PlayerSettings.companyName = "home-redar"; PlayerSettings.productName = "Nova Blade Legends";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false; PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true; PlayerSettings.allowedAutorotateToLandscapeRight = true;
        Debug.Log("[NovaSetup] pipeline " + pc + " / " + mobile);
    }

    static void ImportClips()
    {
        foreach (var kv in Clips)
        {
            var path = $"{Anim}/{kv.Value.file}.fbx";
            var mi = (ModelImporter)AssetImporter.GetAtPath(path);
            if (mi == null) { Debug.LogError("[NovaSetup] missing " + path); continue; }
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importCameras = false; mi.importLights = false; mi.materialImportMode = ModelImporterMaterialImportMode.None;
            mi.SaveAndReimport();
            var clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.name = kv.Key; c.loopTime = kv.Value.loop;
                // the game moves the character itself, so bake root motion into the pose
                c.lockRootRotation = true; c.keepOriginalOrientation = true;
                // height and turning stay in the pose; travel is extracted and dropped (applyRootMotion off) so moves play in place
                c.lockRootHeightY = true; c.keepOriginalPositionY = true; c.heightFromFeet = false;
                c.lockRootPositionXZ = false; c.keepOriginalPositionXZ = false;
            }
            mi.clipAnimations = clips;
            mi.SaveAndReimport();
        }
    }

    public static AnimationClip Clip(string state)
    {
        var f = Clips[state].file;
        return AssetDatabase.LoadAllAssetsAtPath($"{Anim}/{f}.fbx").OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));   // null: not downloaded yet
    }

    static AnimatorController BuildController()
    {
        if (File.Exists(ControllerPath)) AssetDatabase.DeleteAsset(ControllerPath);
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ac.layers[0].stateMachine;
        foreach (var k in Clips.Keys)
        {
            if (!File.Exists($"{Anim}/{Clips[k].file}.fbx")) continue;
            var st = sm.AddState(k);
            st.motion = Clip(k);
            st.speedParameterActive = true; st.speedParameter = "Speed";
            if (k == "idle") sm.defaultState = st;
        }
        ac.AddParameter(new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 1 });
        return ac;
    }

    // glTF bone name -> Unity human bone
    static readonly (string human, string bone)[] Map =
    {
        ("Hips", "hips"), ("Spine", "spine"), ("Chest", "chest"), ("Neck", "neck"), ("Head", "head"),
        ("LeftShoulder", "shoulder.L"), ("RightShoulder", "shoulder.R"),
        ("LeftUpperArm", "upperarm.L"), ("LeftLowerArm", "forearm.L"), ("LeftHand", "hand.L"),
        ("RightUpperArm", "upperarm.R"), ("RightLowerArm", "forearm.R"), ("RightHand", "hand.R"),
        ("LeftUpperLeg", "thigh.L"), ("LeftLowerLeg", "shin.L"), ("LeftFoot", "foot.L"),
        ("RightUpperLeg", "thigh.R"), ("RightLowerLeg", "shin.R"), ("RightFoot", "foot.R"),
    };

    static Transform Find(Transform root, string n) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == n);

    static void Aim(Transform root, string bone, string child, Vector3 dir)
    {
        var b = Find(root, bone); var c = Find(root, child);
        if (!b || !c) return;
        var cur = (c.position - b.position).normalized;
        b.rotation = Quaternion.FromToRotation(cur, dir) * b.rotation;
    }

    // the skin with its leg and shoe weights mended (NovaBlade.FootWeights), saved beside the generated assets and used by the prefab instead of the model's own
    const string MendedMeshes = "Assets/Game/Generated/HeroMesh";
    static int MendMesh(string id, GameObject inst)
    {
        var body = NovaBlade.FootWeights.Body(inst); if (!body) return 0;
        var src = body.sharedMesh; var own = AssetDatabase.GetAssetPath(src); if (own.StartsWith(MendedMeshes)) { var model = NovaBuild.Model($"{Heroes}/{id}.glb") ?? NovaBuild.Model($"{Heroes}/{id}.gltf"); var orig = model ? NovaBlade.FootWeights.Body(model) : null; if (!orig) return 0; src = orig.sharedMesh; }
        var w = NovaBlade.FootWeights.Mended(src, body.bones, out int changed); w ??= src.boneWeights;
        Directory.CreateDirectory(MendedMeshes); var path = $"{MendedMeshes}/{id}.asset"; if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
        var copy = Object.Instantiate(src); copy.boneWeights = w; int cut = NovaBlade.FootWeights.CutBridges(copy); if (changed == 0 && cut == 0) { Object.DestroyImmediate(copy); return 0; } AssetDatabase.CreateAsset(copy, path); body.sharedMesh = copy;
        Debug.Log($"[NovaSetup] mended {id}: {changed} skin points of {src.vertexCount}, {cut} triangles between the legs cut"); return changed + cut;
    }
    // mend every hero, NPC and monster prefab already built, without rebuilding avatars
    public static void MendHeroMeshes()
    {
        int n = 0;
        foreach (var id in ClassHeroes.Concat(Npcs).Concat(Monsters).Distinct())
        {
            var path = PrefabPath(id); if (!File.Exists(path)) continue;
            var root = PrefabUtility.LoadPrefabContents(path); if (MendMesh(id, root) > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); n++; } PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets(); Debug.Log($"[NovaSetup] mended prefabs: {n}");
    }
    // which leg points still follow a bone of the other side, by distance from the middle (cm), and which bones
    public static void LegReport()
    {
        foreach (var id in new[] { "warrior", "spartan", "knight", "elf" })
        {
            var pf = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/Hero_{id}.prefab"); var sk = NovaBlade.FootWeights.Body(pf); var m = sk.sharedMesh; var v = m.vertices; var w = m.boneWeights; var bind = m.bindposes; var bones = sk.bones;
            float X(int b) => bind[b].inverse.GetColumn(3).x; float Y(int b) => bind[b].inverse.GetColumn(3).y;
            var hist = new int[12]; var who = new Dictionary<string, int>(); int legPts = 0; float lo = v.Min(p => p.y), hi = v.Max(p => p.y);
            int thigh = System.Array.FindIndex(bones, b => b && b.name.StartsWith("thigh")); float hip = thigh >= 0 ? Y(thigh) : lo + (hi - lo) * .5f;
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i].y > hip) continue; legPts++; var b = w[i]; float x = v[i].x; float opp = 0;
                void T(int bi, float ww) { if (ww <= 0) return; float bx = X(bi); if (Mathf.Abs(bx) > .02f && Mathf.Sign(bx) != Mathf.Sign(x)) { opp += ww; var n = bones[bi] ? bones[bi].name : "?"; who.TryGetValue(n, out var c); who[n] = c + 1; } }
                T(b.boneIndex0, b.weight0); T(b.boneIndex1, b.weight1); T(b.boneIndex2, b.weight2); T(b.boneIndex3, b.weight3);
                if (opp > .05f) hist[Mathf.Min(11, (int)(Mathf.Abs(x) * 100))]++;
            }
            Debug.Log($"[LegReport] {id} mesh {m.name} hip {hip:0.00} legPts {legPts} opposite by cm: {string.Join(",", hist)} bones: {string.Join(" ", who.OrderByDescending(k => k.Value).Take(6).Select(k => k.Key + ":" + k.Value))} thighs {string.Join(" ", bones.Where(b => b && (b.name.StartsWith("thigh") || b.name.StartsWith("shin"))).Select(b => b.name + "@" + X(System.Array.IndexOf(bones, b)).ToString("0.00")))}");
        }
    }
    // once: the classic skin prefabs already built under Resources move out to SkinPackDir (same GUIDs), without their controller
    public static void MoveSkinPacks()
    {
        Directory.CreateDirectory(SkinPackDir); AssetDatabase.Refresh(); int n = 0;
        foreach (var id in ClassHeroes.Where(i => i.EndsWith("_classic")))
        {
            string from = $"{Prefabs}/Hero_{id}.prefab", to = PrefabPath(id);
            if (File.Exists(from)) { var err = AssetDatabase.MoveAsset(from, to); if (!string.IsNullOrEmpty(err)) { Debug.LogError($"[NovaSetup] move {id}: {err}"); continue; } }
            if (!File.Exists(to)) continue;
            var root = PrefabUtility.LoadPrefabContents(to); var an = root.GetComponentInChildren<Animator>(); if (an) an.runtimeAnimatorController = null; PrefabUtility.SaveAsPrefabAsset(root, to); PrefabUtility.UnloadPrefabContents(root); n++;
        }
        AssetDatabase.SaveAssets(); Debug.Log($"[NovaSetup] skin packs: {n}");
    }
    public static void MoveSkinPacksAndWin() { MoveSkinPacks(); NovaBuild.BuildWin(); }
    public static void NewHeroesAndWin() { NewHeroes(); NovaBuild.BuildWin(); }
    public static void MendAndWin() { MendHeroMeshes(); NovaBuild.BuildWin(); }
    static void BuildHero(string id, AnimatorController ctrl)
    {
        var src = NovaBuild.Model($"{Heroes}/{id}.glb");
        if (!src) { Debug.LogError("[NovaSetup] no model " + id); return; }
        var go = (GameObject)Object.Instantiate(src);
        go.name = id;
        var root = go.transform;
        // the character faces +Z; its left side is -X after glTFast's handedness flip
        bool leftIsNegX = Find(root, "upperarm.L").position.x < Find(root, "upperarm.R").position.x;
        var left = leftIsNegX ? Vector3.left : Vector3.right;
        // T-pose reference for the avatar (the mesh itself keeps its A-pose bind)
        if (Find(root, "chest")) { Aim(root, "spine", "chest", Vector3.up); Aim(root, "chest", "neck", Vector3.up); Aim(root, "neck", "head", Vector3.up); }
        else Aim(root, "spine", "head", Vector3.up);
        foreach (var (s, side) in new[] { ("L", left), ("R", -left) })
        {
            Aim(root, $"shoulder.{s}", $"upperarm.{s}", side);
            Aim(root, $"upperarm.{s}", $"forearm.{s}", side);
            Aim(root, $"forearm.{s}", $"hand.{s}", side);
            Aim(root, $"thigh.{s}", $"shin.{s}", Vector3.down);
            Aim(root, $"shin.{s}", $"foot.{s}", Vector3.down);
        }
        var desc = new HumanDescription
        {
            // older hand-made rigs have no chest, neck or collarbones: map only the bones the model has
            human = Map.Where(m => Find(root, m.bone)).Select(m => new HumanBone { humanName = m.human, boneName = m.bone, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
            skeleton = root.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
            upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f, armStretch = .05f, legStretch = .05f, feetSpacing = 0, hasTranslationDoF = false,
        };
        var avatar = AvatarBuilder.BuildHumanAvatar(go, desc);
        avatar.name = id + "Avatar";
        if (!avatar.isValid || !avatar.isHuman) { Debug.LogError($"[NovaSetup] avatar invalid for {id}"); Object.DestroyImmediate(go); return; }
        var avPath = $"{Heroes}/{id}_Avatar.asset";
        if (File.Exists(avPath)) AssetDatabase.DeleteAsset(avPath);
        AssetDatabase.CreateAsset(avatar, avPath);
        Object.DestroyImmediate(go);
        // prefab: fresh instance in its own bind pose + Animator
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(src);
        var holder = new GameObject("Hero_" + id);
        inst.transform.SetParent(holder.transform, false);
        var anim = inst.AddComponent<Animator>();
        anim.avatar = avatar; anim.runtimeAnimatorController = IsPack(id) ? null : ctrl; anim.applyRootMotion = false;   // a skin bundle would carry the controller and every clip: the game gives it the hero's (SkinPacks.Animate)
        anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(SkinPackDir); MendMesh(id, inst);
        PrefabUtility.SaveAsPrefabAsset(holder, PrefabPath(id));
        Object.DestroyImmediate(holder);
        Debug.Log($"[NovaSetup] hero {id} avatar ok, left side {(leftIsNegX ? "-X" : "+X")}");
    }

    // Render each hero at the key frame of a few clips (no play mode needed).
    public static void Peek()
    {
        Directory.CreateDirectory("Builds/peek");
        var states = new[] { ("idle", .3f), ("run", .3f), ("atk1", .45f), ("spin", .45f), ("roll", .5f), ("death", .95f) };
        var lightGo = new GameObject("PeekLight"); var l = lightGo.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 2; lightGo.transform.rotation = Quaternion.Euler(45, 150, 0);
        RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
        var camGo = new GameObject("PeekCam"); var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.3f, .3f, .32f); cam.fieldOfView = 30;
        camGo.transform.position = new Vector3(2.6f, 2.0f, 5.6f); camGo.transform.LookAt(new Vector3(0, .9f, 0));
        const int W = 360, H = 460;
        foreach (var id in RiggedHeroes)
        {
            var pf = Resources.Load<GameObject>("Heroes/Hero_" + id) ?? AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(id)); if (!pf) continue;
            var hero = (GameObject)Object.Instantiate(pf);
            var animr = hero.GetComponentInChildren<Animator>();
            var sheet = new Texture2D(W * states.Length, H, TextureFormat.RGB24, false);
            for (int i = 0; i < states.Length; i++)
            {
                var (st, t) = states[i];
                var clip = Clip(st);
                var graph = PlayableGraph.Create(); var output = AnimationPlayableOutput.Create(graph, "o", animr);
                var cp = AnimationClipPlayable.Create(graph, clip); output.SetSourcePlayable(cp);
                cp.SetTime(clip.length * t); graph.Evaluate(); graph.Destroy();
                var rt = new RenderTexture(W, H, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; sheet.ReadPixels(new Rect(0, 0, W, H), W * i, 0); RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            }
            sheet.Apply(); File.WriteAllBytes($"Builds/peek/hero_{id}.png", sheet.EncodeToPNG());
            Object.DestroyImmediate(hero);
        }
        Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo);
        Debug.Log("[NovaSetup] peek written");
    }
}
