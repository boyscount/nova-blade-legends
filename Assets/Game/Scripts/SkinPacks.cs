using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace NovaBlade
{
    // The classic skins (an older model of a hero) are not in the game's main data: each is an AssetBundle beside the page
    // (skins/hero_<model> on the web, StreamingAssets/skins in the Windows build), fetched the first time it is worn and kept
    // for the session. Until it arrives the hero shows their own model. The bundles carry no animator controller (it would
    // bring every clip with it): the hero's own controller is put on when the model is made.
    public static class SkinPacks
    {
        public const uint Version = 1;   // raise when the bundles are rebuilt with new content, so browsers fetch them again
        static readonly Dictionary<string, GameObject> ready = new();
        static readonly Dictionary<string, List<System.Action>> waiting = new();
        public static bool IsPack(string model) => model != null && (model.EndsWith("_classic") || model.EndsWith("_legend"));
        // the prefab for a hero model, if it is here now
        public static GameObject Get(string model)
        {
            var r = Resources.Load<GameObject>("Heroes/Hero_" + model); if (r) return r;
            return ready.TryGetValue(model, out var g) ? g : null;
        }
        public static bool Loading(string model) => waiting.ContainsKey(model);
        // start fetching a classic skin; done runs once it is ready (not when it fails)
        public static void Fetch(string model, System.Action done = null)
        {
            if (!IsPack(model) || ready.ContainsKey(model) || !Main.G) return;
            if (waiting.TryGetValue(model, out var list)) { if (done != null) list.Add(done); return; }
            waiting[model] = new List<System.Action>(); if (done != null) waiting[model].Add(done);
            Main.G.StartCoroutine(Load(model));
        }
        static string Base()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                var u = Application.absoluteURL; int q = u.IndexOfAny(new[] { '?', '#' }); if (q >= 0) u = u.Substring(0, q);
                return u.Substring(0, u.LastIndexOf('/') + 1) + "skins/";
            }
            return "file:///" + Application.streamingAssetsPath.Replace('\\', '/') + "/skins/";
        }
        static IEnumerator Load(string model)
        {
            var url = Base() + "hero_" + model.ToLowerInvariant(); float t0 = Time.realtimeSinceStartup;
            using (var req = UnityWebRequestAssetBundle.GetAssetBundle(url, Version, 0))
            {
                yield return req.SendWebRequest();
                if (req.result != UnityWebRequest.Result.Success) { Debug.LogWarning($"[Skins] {model}: {req.error} ({url})"); waiting.Remove(model); yield break; }
                var bundle = DownloadHandlerAssetBundle.GetContent(req); var go = bundle ? bundle.LoadAsset<GameObject>("Hero_" + model) : null;
                if (!go) { Debug.LogWarning($"[Skins] {model}: no prefab in the bundle"); waiting.Remove(model); yield break; }
                ready[model] = go; Debug.Log($"[Skins] {model} ready in {(Time.realtimeSinceStartup - t0) * 1000:0} ms");
            }
            var cbs = waiting[model]; waiting.Remove(model); foreach (var c in cbs) c?.Invoke();
        }
        // a model from a bundle has no controller of its own: give it the hero's
        public static void Animate(GameObject model, string heroId)
        {
            var an = model ? model.GetComponentInChildren<Animator>() : null; if (!an || an.runtimeAnimatorController) return;
            var main = Resources.Load<GameObject>("Heroes/Hero_" + heroId); var ma = main ? main.GetComponentInChildren<Animator>() : null;
            if (ma) an.runtimeAnimatorController = ma.runtimeAnimatorController;
        }
    }
}
