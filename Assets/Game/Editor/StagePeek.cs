using System.IO;
using NovaBlade;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
// Renders each stage from the gameplay camera angle (and a wider view) to Builds/peek/stage_*.png
public static class StagePeek
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Game/Scenes/Main.unity");
        var main = Object.FindFirstObjectByType<Main>(); var cam = main.cam; Directory.CreateDirectory("Builds/peek");
        var heroPf = Resources.Load<GameObject>("Heroes/Hero_warrior"); var hero = Object.Instantiate(heroPf, new Vector3(0, 0, 4), Quaternion.Euler(0, 160, 0));
        var an = hero.GetComponentInChildren<Animator>(); an.Rebind(); an.Update(0);
        for (int i = 0; i < main.stageRoots.Length; i++)
        {
            for (int k = 0; k < main.stageRoots.Length; k++) main.stageRoots[k].gameObject.SetActive(k == i);
            main.stageRoots[i].GetComponent<StageLook>().Apply();
            foreach (var (tag, pos, look) in new[] { ("game", new Vector3(0, 8.6f, 11.4f), new Vector3(0, .9f, 4)), ("wide", new Vector3(0, 17, 30), new Vector3(0, 0, 0)) })
            {
                cam.transform.position = pos; cam.transform.LookAt(look);
                foreach (var g in main.stageRoots[i].GetComponentsInChildren<GrassField>()) { g.Draw(); var mf = g.DebugMesh; Debug.Log($"[StagePeek] grass {g.material.shader.name} base {g.baseCol} tip {g.tipCol} tint {g.material.GetColor("_BaseColor")} verts {(mf ? mf.vertexCount : -1)} col {(mf && mf.colors.Length > 20 ? mf.colors[20].ToString() : "none")}"); }
                var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render();
                RenderTexture.active = rt; var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply();
                File.WriteAllBytes($"Builds/peek/stage_{i}_{tag}.png", t.EncodeToPNG()); cam.targetTexture = null; RenderTexture.active = null;
            }
        }
        Debug.Log("[StagePeek] done");
    }
}
