using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // On the game camera: trees, bushes, walls and bridges standing between the camera and the hero or a monster turn
    // see-through (and solid again once the view is clear), so nobody fights hidden under a canopy.
    // The faded copy of each material is URP Lit, transparent (Resources/FadeLit keeps that shader variant in the build).
    public class OcclusionFader : MonoBehaviour
    {
        const float Alpha = .22f, Speed = 6, WildsFoeNear = 9;
        class Faded { public Renderer r; public Material[] orig; public UnityEngine.Rendering.ShadowCastingMode shadow; public float a = 1; public bool hit, near; public bool recv; }
        readonly Dictionary<Renderer, Faded> faded = new Dictionary<Renderer, Faded>();
        readonly Dictionary<Material, Material> fadeMat = new Dictionary<Material, Material>();
        readonly List<Renderer> scenery = new List<Renderer>();
        Transform stage; Material template; MaterialPropertyBlock mpb;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), BaseMap = Shader.PropertyToID("_BaseMap");

        public int FadedCount => faded.Count;
        public string Report => (template ? $"template q{template.renderQueue} transp={template.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")} | " : "no template | ")
            + string.Join("; ", faded.Values.Where(f => f.r).Select(f => $"{f.r.name} [{string.Join(",", f.orig.Select(o => o ? o.shader.name + ":" + (o.HasProperty(BaseMap) && o.GetTexture(BaseMap) ? o.GetTexture(BaseMap).name : "notex") : "null"))}] b{f.r.bounds.size}"));
        void Start() { template = Resources.Load<Material>("FadeLit"); mpb = new MaterialPropertyBlock(); }

        void LateUpdate()
        {
            var G = Main.G; var cam = Camera.main;
            if (!template || !G || !cam || G.mode != Main.Mode.Game) { if (faded.Count > 0) ClearAll(); return; }
            var root = G.stageRoots?.FirstOrDefault(t => t && t.gameObject.activeInHierarchy);
            if (root != stage) { ClearAll(); stage = root; Collect(); }
            foreach (var f in faded.Values) f.hit = f.near = false;
            // what must stay visible: the hero and the nearest monsters
            var c = cam.transform.position;
            if (G.hero) Check(c, G.hero.Pos + Vector3.up * 1.1f);
            foreach (var e in G.Live.Where(e => e && e.alive && (!G.wilds || (e.Pos - G.hero.Pos).sqrMagnitude < WildsFoeNear * WildsFoeNear)).OrderBy(e => (e.Pos - G.hero.Pos).sqrMagnitude).Take(G.wilds ? 4 : 10)) Check(c, e.Pos + Vector3.up * 1f);
            var done = new List<Renderer>();
            foreach (var f in faded.Values)
            {
                f.a = Mathf.MoveTowards(f.a, f.hit ? (f.near ? .04f : Alpha) : 1, Time.deltaTime * Speed);   // round the camera: almost gone, or its layers stack up dark
                if (!f.r) { done.Add(f.r); continue; }
                if (f.a >= 1 && !f.hit) { f.r.sharedMaterials = f.orig; f.r.SetPropertyBlock(null); f.r.shadowCastingMode = f.shadow; f.r.receiveShadows = f.recv; done.Add(f.r); continue; }
                for (int i = 0; i < f.orig.Length; i++)
                {
                    var o = f.orig[i]; var col = o && o.HasProperty(BaseColor) ? o.GetColor(BaseColor) : o && o.HasProperty("_Color") ? o.color : Color.white;
                    col.a *= f.a; f.r.GetPropertyBlock(mpb, i); mpb.SetColor(BaseColor, col); f.r.SetPropertyBlock(mpb, i);
                }
            }
            foreach (var r in done) faded.Remove(r);
        }

        // every scenery renderer of the stage that could stand in the way (not the ground, grass, water or effects)
        void Collect()
        {
            scenery.Clear(); if (!stage) return;
            foreach (var r in stage.GetComponentsInChildren<Renderer>(true))
            {
                if (!(r is MeshRenderer) || r.GetComponentInParent<Hero>() || r.GetComponentInParent<Foe>()) continue;
                var top = r.transform; while (top.parent && top.parent != stage) top = top.parent; if (top.name.Contains("CaveRock")) continue;   // the rocks of the cave door are big and stand behind whoever is at the door
                var n = r.gameObject.name.ToLower(); var b = r.bounds;
                if (n.Contains("ground") || n.Contains("grass") || n.Contains("water") || n.Contains("terrain") || n.Contains("sky") || n.Contains("mist")) continue;
                if (b.size.y < .6f || b.size.x > 40 || b.size.z > 40) continue;   // flat pieces and the huge backdrops
                scenery.Add(r);
            }
        }

        void Check(Vector3 from, Vector3 to)
        {
            var d = to - from; float len = d.magnitude; if (len < .1f) return; var ray = new Ray(from, d / len);
            foreach (var r in scenery)
            {
                if (!r || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                var b = r.bounds; b.Expand(.3f);
                if (!b.IntersectRay(ray, out float t) || t > len - .2f) continue;
                if (!faded.TryGetValue(r, out var f))
                {   // see-through, and no shadow either: under a canopy the hero would still stand in the dark
                    f = new Faded { r = r, orig = r.sharedMaterials, shadow = r.shadowCastingMode, recv = r.receiveShadows }; faded[r] = f; r.sharedMaterials = f.orig.Select(Fade).ToArray(); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
                }
                f.hit = true; var nb = r.bounds; nb.Expand(2.5f); if (nb.Contains(from)) f.near = true;
            }
        }

        Material Fade(Material o)
        {
            if (!o) return template;
            if (fadeMat.TryGetValue(o, out var m)) return m;
            m = new Material(template) { name = o.name + " (fade)" };
            var tex = o.HasProperty(BaseMap) ? o.GetTexture(BaseMap) : o.mainTexture;
            if (tex) { m.SetTexture(BaseMap, tex); if (o.HasProperty(BaseMap)) { m.SetTextureScale(BaseMap, o.GetTextureScale(BaseMap)); m.SetTextureOffset(BaseMap, o.GetTextureOffset(BaseMap)); } }
            fadeMat[o] = m; return m;
        }

        void ClearAll()
        {
            foreach (var f in faded.Values) if (f.r) { f.r.sharedMaterials = f.orig; f.r.SetPropertyBlock(null); f.r.shadowCastingMode = f.shadow; f.r.receiveShadows = f.recv; }
            faded.Clear();
        }
    }
}
