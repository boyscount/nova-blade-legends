using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Everything the game spawns, collected by the editor (NovaSetup/SceneBuild) into Resources/Catalog.asset.
    public class Catalog : ScriptableObject
    {
        [System.Serializable] public class Entry { public string id; public GameObject prefab; }
        [System.Serializable] public class ClipInfo { public string id; public float length, hit, speed; }   // speed: ground speed of a walk/run cycle at 1.75 m (m/s), 0 = not a gait
        public List<Entry> models = new();      // hero_*, pet/foe models by id, bosses, weapons (w_*)
        public List<Entry> fx = new();          // CartoonFX prefabs by short name
        public List<ClipInfo> clips = new();    // Mixamo clip lengths and strike times (seconds)
        public RuntimeAnimatorController heroController;
        public Material grassMat, telegraphMat, glowMat, trailMat;
        public Mesh[] grassMeshes;

        static Catalog inst;
        public static Catalog I => inst ? inst : inst = Resources.Load<Catalog>("Catalog");
        // the catalog holds names, not the things themselves: a direct reference would keep every weapon, wing and effect in memory for the whole game.
        // The prefabs sit in Resources/Cat (NovaBuild.LazyCatalog) and load the first time they are needed.
        public GameObject Model(string id) => Find(models, id, "m_");
        public GameObject Fx(string id) => Find(fx, id, "fx_");
        static GameObject Find(List<Entry> list, string id, string prefix)
        {
            foreach (var e in list) if (e.id == id) return e.prefab ? e.prefab : Resources.Load<GameObject>("Cat/" + prefix + id);
            return null;
        }
        public ClipInfo Clip(string set, string id) { if (set != null) foreach (var c in clips) if (c.id == set + ":" + id) return c; return Clip(id); }
        public ClipInfo Clip(string id) { foreach (var c in clips) if (c.id == id) return c; return new ClipInfo { id = id, length = .7f, hit = .35f }; }
    }

    // Tint / glow / hit flash on glTFast materials through property blocks (no material copies).
    public class Look
    {
        static readonly int BaseColor = Shader.PropertyToID("baseColorFactor"), Emissive = Shader.PropertyToID("emissiveFactor"),
            UrpBase = Shader.PropertyToID("_BaseColor"), UrpEmis = Shader.PropertyToID("_EmissionColor");
        readonly Renderer[] rends; readonly MaterialPropertyBlock mpb = new();
        Color tint = Color.white, glow = Color.black; float flash;
        readonly System.Collections.Generic.Dictionary<Renderer, Color[]> baseCols = new();   // each material's own colour, so a tint multiplies it
        public Look(GameObject go)
        {
            rends = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
                if (r is MeshRenderer || r is SkinnedMeshRenderer)
                {
                    var ms = r.sharedMaterials; var cols = new Color[ms.Length];
                    for (int i = 0; i < ms.Length; i++)
                    {
                        var m = ms[i]; cols[i] = Color.white; if (!m) continue; m.EnableKeyword("_EMISSION");
                        if (m.HasProperty(UrpBase)) cols[i] = m.GetColor(UrpBase); else if (m.HasProperty(BaseColor)) cols[i] = m.GetColor(BaseColor);
                    }
                    baseCols[r] = cols;
                }
            Apply();
        }
        public void Set(Color t, Color g) { tint = t; glow = g; Apply(); }
        public void Flash(float v) { if (Mathf.Approximately(v, flash)) return; flash = v; Apply(); }
        void Apply()
        {
            var e = glow + Color.white * flash;
            foreach (var r in rends)
            {
                if (!r || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                if (!baseCols.TryGetValue(r, out var cols)) continue;
                for (int i = 0; i < cols.Length; i++)
                {
                    var c = cols[i] * tint; c.a = cols[i].a;
                    r.GetPropertyBlock(mpb, i); mpb.SetColor(BaseColor, c); mpb.SetColor(Emissive, e); mpb.SetColor(UrpBase, c); mpb.SetColor(UrpEmis, e); r.SetPropertyBlock(mpb, i);
                }
            }
        }
    }
}
