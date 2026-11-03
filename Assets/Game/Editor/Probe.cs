using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
// Logs the size of every prefab/model in a folder (used to lay out the castle kit): -executeMethod Probe.Run
public static class Probe
{
    public static void Run()
    {
        foreach (var dir in new[] { "Assets/ThirdParty/Dungeon/Prefabs", "Assets/ThirdParty/PrisonCell/Prefabs", "Assets/Game/Models/Props", "Assets/Game/Models/Nature", "Assets/Game/Models/Foes" })
            foreach (var guid in AssetDatabase.FindAssets("t:GameObject", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); var go = AssetDatabase.LoadAssetAtPath<GameObject>(path); if (!go) continue;
                var inst = (GameObject)Object.Instantiate(go); var rs = inst.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { Object.DestroyImmediate(inst); continue; }
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                int tris = inst.GetComponentsInChildren<MeshFilter>().Where(f => f.sharedMesh).Sum(f => f.sharedMesh.triangles.Length / 3) + inst.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s => s.sharedMesh).Sum(s => s.sharedMesh.triangles.Length / 3);
                var anim = inst.GetComponentInChildren<Animation>();
                Debug.Log($"[Probe] {path.Replace("Assets/", "")} size {b.size.x:0.00}x{b.size.y:0.00}x{b.size.z:0.00} min {b.min.x:0.0},{b.min.y:0.0},{b.min.z:0.0} tris {tris} anim {(anim ? anim.GetClipCount() : -1)}");
                Object.DestroyImmediate(inst);
            }
    }
}
