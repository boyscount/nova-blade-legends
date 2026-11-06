using System.Collections.Generic;
using System.IO;
using System.Linq;
using NovaBlade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

// How much each stage keeps in memory while the whole game sits in one scene. Editor figures, so read them as proportions, not phone megabytes.
// Run: -executeMethod NovaMemoryReport.Run   Writes Builds/memory-report.txt
public static class NovaMemoryReport
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity");
        var main = Object.FindAnyObjectByType<Main>();
        var groups = new List<(string name, Object[] roots)>();
        foreach (var r in main.stageRoots) if (r) groups.Add((r.name, new Object[] { r.gameObject }));
        if (main.cityRoot) groups.Add((main.cityRoot.name, new Object[] { main.cityRoot.gameObject }));
        var cat = Catalog.I ? Catalog.I : AssetDatabase.LoadAssetAtPath<Catalog>("Assets/Game/Resources/Catalog.asset");
        if (cat) groups.Add(("Catalog (models and effects, always loaded)", new Object[] { cat }));
        var rest = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(g => g.transform.parent == null && !main.stageRoots.Contains(g.transform) && g.transform != main.cityRoot).Cast<Object>().ToArray();
        groups.Add(("Rest of the scene (lobby set, camera, UI)", rest));

        var owners = new Dictionary<Object, int>(); var deps = new List<(string name, Object[] objs)>();
        foreach (var (name, roots) in groups)
        {
            var objs = EditorUtility.CollectDependencies(roots).Where(Counted).Distinct().ToArray();
            deps.Add((name, objs)); foreach (var o in objs) { owners.TryGetValue(o, out var n); owners[o] = n + 1; }
        }
        static long Mb(long b) => b / 1048576;
        var lines = new List<string> { "group | total MB | only this group MB | textures MB | meshes MB | terrain MB | objects" };
        long all = owners.Keys.Sum(Profiler.GetRuntimeMemorySizeLong);
        foreach (var (name, objs) in deps.OrderByDescending(d => d.objs.Where(o => owners[o] == 1).Sum(Profiler.GetRuntimeMemorySizeLong)))
        {
            long Sum(IEnumerable<Object> s) => s.Sum(Profiler.GetRuntimeMemorySizeLong);
            lines.Add($"{name} | {Mb(Sum(objs))} | {Mb(Sum(objs.Where(o => owners[o] == 1)))} | {Mb(Sum(objs.Where(o => o is Texture)))} | {Mb(Sum(objs.Where(o => o is Mesh)))} | {Mb(Sum(objs.Where(o => o is TerrainData)))} | {objs.Length}");
        }
        lines.Add($"everything counted once | {Mb(all)}");
        lines.Add("biggest single assets:");
        foreach (var o in owners.Keys.OrderByDescending(Profiler.GetRuntimeMemorySizeLong).Take(15)) lines.Add($"{Mb(Profiler.GetRuntimeMemorySizeLong(o))} MB  {o.GetType().Name}  {AssetDatabase.GetAssetPath(o)}  used by {owners[o]}");
        File.WriteAllLines("Builds/memory-report.txt", lines);
        Debug.Log("[NovaMemoryReport] done");
    }
    static bool Counted(Object o) => o is Texture || o is Mesh || o is TerrainData || o is AudioClip || o is AnimationClip;
}
