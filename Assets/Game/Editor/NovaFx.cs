// Third-party effect packs made for the built-in pipeline: switch their materials to URP shaders so they render
// (instead of pink). Hovl "Magic Effects FREE" uses the built-in Particles/Standard shaders.
//   Unity.exe -batchmode -quit -projectPath . -executeMethod NovaFx.ConvertHovl
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class NovaFx
{
    public static void ConvertHovl() => ConvertParticles("Assets/Hovl Studio");
    // Matthew Guz "Slash Effects FREE": Particles/Standard Unlit too
    public static void ConvertSlash() => ConvertParticles("Assets/Matthew Guz");
    // "Altar Ruins Free" (built-in Standard Specular materials) -> URP Lit, metallic workflow; also reports each ruin mesh's triangles.
    // Its textures were shrunk to 1024 px and its EXR displacement maps, old meshes and demo scenes left out when it was copied in.
    public static void ConvertRuins()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Altar_Ruins_FREE" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m || m.shader == lit || m.shader.name.StartsWith("Skybox")) continue;
            Texture Tex(string p) => m.HasProperty(p) ? m.GetTexture(p) : null;
            var albedo = Tex("_MainTex"); var normal = Tex("_BumpMap"); var ao = Tex("_OcclusionMap"); var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white; var was = m.shader.name;
            m.shader = lit; m.SetTexture("_BaseMap", albedo); m.SetColor("_BaseColor", col); m.SetFloat("_Metallic", 0); m.SetFloat("_Smoothness", .15f);
            if (normal) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
            if (ao) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
            m.enableInstancing = true; EditorUtility.SetDirty(m); Debug.Log($"[NovaFx] ruins material {path}: {was} -> URP Lit (albedo {(albedo ? albedo.name : "none")}, normal {(normal ? normal.name : "none")})");
        }
        foreach (var path in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Altar_Ruins_FREE", "Assets/Symphonie" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); int tris = go.GetComponentsInChildren<MeshFilter>(true).Sum(f => f.sharedMesh ? f.sharedMesh.triangles.Length / 3 : 0);
            var b = new Bounds(); bool any = false; foreach (var r in go.GetComponentsInChildren<MeshFilter>(true)) if (r.sharedMesh) { var mb = r.sharedMesh.bounds; if (!any) { b = mb; any = true; } else b.Encapsulate(mb); }
            Debug.Log($"[NovaFx] ruins mesh {System.IO.Path.GetFileNameWithoutExtension(path)}: {tris} tris, size {b.size.x:0.0} x {b.size.y:0.0} x {b.size.z:0.0}");
        }
        AssetDatabase.SaveAssets();
    }
    static void ConvertParticles(string folder)
    {
        var unlit = Shader.Find("Universal Render Pipeline/Particles/Unlit"); var lit = Shader.Find("Universal Render Pipeline/Lit");
        int n = 0;
        foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m || m.shader.name.StartsWith("Universal Render Pipeline")) continue;
            var tex = m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null; var col = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            if (m.shader.name == "Standard")
            {
                var bump = m.GetTexture("_BumpMap"); m.shader = lit; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", col);
                if (bump) { m.SetTexture("_BumpMap", bump); m.EnableKeyword("_NORMALMAP"); }
                n++; EditorUtility.SetDirty(m); continue;
            }
            float dst = m.HasProperty("_DstBlend") ? m.GetFloat("_DstBlend") : 10; bool add = dst == 1;   // One = additive, OneMinusSrcAlpha = alpha blend
            m.shader = unlit;
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", col);
            m.SetFloat("_Surface", 1); m.SetFloat("_Blend", add ? 2 : 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetFloat("_DstBlend", add ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", 1); m.SetFloat("_DstBlendAlpha", add ? 1 : 10); m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            n++; EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets(); Debug.Log($"[NovaFx] converted {n} materials in {folder} to URP");
    }

    // contact sheet of every prefab under the given folders (Builds/peek/props_<n>.png) with its size in metres, for planning stages
    public static void PeekProps()
    {
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Polytope Studio" }).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p).ToList();
        System.IO.Directory.CreateDirectory("Builds/peek");
        var lightGo = new GameObject("L"); var l = lightGo.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.6f; lightGo.transform.rotation = Quaternion.Euler(40, 140, 0);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.6f, .6f, .65f);
        var camGo = new GameObject("C"); var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.25f, .27f, .3f); cam.fieldOfView = 30;
        const int W = 220, COLS = 8, ROWS = 6; var log = new System.Text.StringBuilder();
        for (int sheet = 0; sheet * COLS * ROWS < paths.Count; sheet++)
        {
            var tex = new Texture2D(W * COLS, W * ROWS, TextureFormat.RGB24, false);
            for (int i = 0; i < COLS * ROWS; i++)
            {
                int idx = sheet * COLS * ROWS + i; if (idx >= paths.Count) break;
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[idx])); var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Object.DestroyImmediate(go); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float s = Mathf.Max(b.size.x, b.size.y, b.size.z); cam.transform.position = b.center + new Vector3(.8f, .55f, 1) .normalized * s * 1.9f; cam.transform.LookAt(b.center);
                var rt = new RenderTexture(W, W, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, W), (i % COLS) * W, (ROWS - 1 - i / COLS) * W); RenderTexture.active = null; cam.targetTexture = null; rt.Release(); Object.DestroyImmediate(go);
                log.AppendLine($"{sheet}:{i}\t{b.size.x:0.0}x{b.size.y:0.0}x{b.size.z:0.0}\t{paths[idx]}");
            }
            tex.Apply(); System.IO.File.WriteAllBytes($"Builds/peek/props_{sheet}.png", tex.EncodeToPNG());
        }
        System.IO.File.WriteAllText("Builds/peek/props.txt", log.ToString()); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo);
        Debug.Log("[NovaFx] props sheets written");
    }

    // Polytope low-poly nature: its custom wind shaders render pink in the web build. Switch every material to URP Lit,
    // keeping the first texture it uses and its tint; foliage / flowers / grass get alpha clipping and both faces.
    public static void ConvertPolytope()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit"); int n = 0;
        foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Polytope Studio" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m || m.shader == lit || path.Contains("Skybox")) continue;
            var so = new SerializedObject(m); var envs = so.FindProperty("m_SavedProperties.m_TexEnvs"); Texture tex = null;
            foreach (var name in new[] { "_BaseTexture", "_BaseMap", "_MainTex", "_Albedo", "_Texture", "_Leaves", "_TrunkTexture" })
                for (int i = 0; i < envs.arraySize && !tex; i++) { var e = envs.GetArrayElementAtIndex(i); if (e.FindPropertyRelative("first").stringValue == name) tex = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture; }
            for (int i = 0; i < envs.arraySize && !tex; i++) tex = envs.GetArrayElementAtIndex(i).FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
            var cols = so.FindProperty("m_SavedProperties.m_Colors"); Color tint = Color.white;
            for (int i = 0; i < cols.arraySize; i++) { var e = cols.GetArrayElementAtIndex(i); var nm = e.FindPropertyRelative("first").stringValue; if (nm == "_BaseColor" || nm == "_Color") { tint = e.FindPropertyRelative("second").colorValue; break; } }
            bool cut = m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || path.Contains("Foliage") || path.Contains("Leaf") || path.Contains("Leaves") || path.Contains("Grass") || path.Contains("Poppy") || path.Contains("Plants") || path.Contains("Pine");
            bool water = path.Contains("Water");
            m.shader = lit; m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", water ? new Color(.25f, .55f, .6f, .75f) : new Color(tint.r, tint.g, tint.b, 1)); m.SetFloat("_Smoothness", water ? .8f : .15f); m.SetFloat("_Metallic", 0);
            if (cut) { m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", .5f); m.EnableKeyword("_ALPHATEST_ON"); m.SetFloat("_Cull", 0); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; }
            else { m.SetFloat("_AlphaClip", 0); m.SetFloat("_Cull", 2); m.renderQueue = -1; }
            n++; EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets(); Debug.Log($"[NovaFx] converted {n} Polytope materials to URP Lit");
    }

    // HDRP Lit materials (Chinese Modular House) -> URP Lit. The HDRP shader is not in this project, so the values are read
    // from the material's saved properties. HDRP's mask map (R metallic, G occlusion, A smoothness) lines up with URP's
    // metallic-gloss map (R metallic, A smoothness) and occlusion map (G), so it is used for both.
    public static void ConvertHdrp()
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit"); int n = 0;
        foreach (var path in AssetDatabase.FindAssets("t:Material", new[] { "Assets/Erbeilo3D" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path); if (!m || m.shader == lit) continue;
            var so = new SerializedObject(m); var props = so.FindProperty("m_SavedProperties");
            Texture Tex(string name) { var a = props.FindPropertyRelative("m_TexEnvs"); for (int i = 0; i < a.arraySize; i++) { var e = a.GetArrayElementAtIndex(i); if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture; } return null; }
            float F(string name, float def) { var a = props.FindPropertyRelative("m_Floats"); for (int i = 0; i < a.arraySize; i++) { var e = a.GetArrayElementAtIndex(i); if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second").floatValue; } return def; }
            Color C(string name, Color def) { var a = props.FindPropertyRelative("m_Colors"); for (int i = 0; i < a.arraySize; i++) { var e = a.GetArrayElementAtIndex(i); if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second").colorValue; } return def; }
            var baseMap = Tex("_BaseColorMap") ?? Tex("_MainTex"); var normal = Tex("_NormalMap") ?? Tex("_BumpMap"); var mask = Tex("_MaskMap");
            var col = C("_BaseColor", C("_Color", Color.white)); float cut = F("_AlphaCutoffEnable", 0), cutoff = F("_AlphaCutoff", .5f), two = F("_DoubleSidedEnable", 0);
            float smoothMax = F("_SmoothnessRemapMax", F("_Smoothness", .5f)), metal = F("_Metallic", 0), nScale = F("_NormalScale", 1);
            m.shader = lit;
            m.SetTexture("_BaseMap", baseMap); m.SetColor("_BaseColor", col);
            if (normal) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", nScale); m.EnableKeyword("_NORMALMAP"); }
            if (mask) { m.SetTexture("_MetallicGlossMap", mask); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetTexture("_OcclusionMap", mask); m.EnableKeyword("_OCCLUSIONMAP"); m.SetFloat("_Smoothness", smoothMax); m.SetFloat("_GlossMapScale", smoothMax); }
            else { m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", Mathf.Min(smoothMax, .6f)); }
            if (cut > .5f) { m.SetFloat("_AlphaClip", 1); m.SetFloat("_Cutoff", cutoff); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest; }
            m.SetFloat("_Cull", two > .5f ? 0 : 2);
            n++; EditorUtility.SetDirty(m);
        }
        // textures: cap at 1024 for the web build
        foreach (var path in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Erbeilo3D", "Assets/Art" }).Select(AssetDatabase.GUIDToAssetPath))
            if (AssetImporter.GetAtPath(path) is TextureImporter ti && ti.maxTextureSize > 1024) { ti.maxTextureSize = 1024; ti.SaveAndReimport(); }
        AssetDatabase.SaveAssets(); Debug.Log($"[NovaFx] converted {n} HDRP materials to URP Lit");
    }
}

// overhead pictures of every stage root in the main scene (Builds/peek/stage_<name>.png), to check layouts from above
public static class NovaStagePeek
{
    public static void Run()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity");
        var main = Object.FindFirstObjectByType<NovaBlade.Main>(); System.IO.Directory.CreateDirectory("Builds/peek");
        var camGo = new GameObject("PeekCam"); var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = 50; cam.farClipPlane = 300;
        foreach (var r in main.stageRoots)
        {
            foreach (var o in main.stageRoots) o.gameObject.SetActive(o == r); var look = r.GetComponent<NovaBlade.StageLook>(); if (look) look.Apply();
            foreach (var (tag, pos) in new[] { ("top", new Vector3(0, 62, 18)), ("view", new Vector3(0, 8.6f, 7.4f)) })
            {
                cam.transform.position = pos; cam.transform.LookAt(tag == "top" ? new Vector3(0, 0, -4) : new Vector3(0, .9f, 0));
                var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply(); RenderTexture.active = null; cam.targetTexture = null; rt.Release();
                System.IO.File.WriteAllBytes($"Builds/peek/stage_{r.name}_{tag}.png", t.EncodeToPNG());
            }
        }
        Object.DestroyImmediate(camGo); Debug.Log("[NovaStagePeek] done");
    }
}
