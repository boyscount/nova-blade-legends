using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // The auto-rigged models let parts of one leg follow the other leg too, or the hips: while walking, the skin then stretches
    // across (a long dark sheet under Tenzin's feet, a spike off the elf's heel, white slabs between the new heroes' knees).
    // The weights are mended once in the editor (NovaSetup.MendHeroMeshes) and the hero prefabs use the mended mesh, so the game,
    // the cards and the AI heroes all get it with no copies at run time.
    public static class FootWeights
    {
        // the Tripo heroes' armour comes out like chrome: their metal is taken down by a third (per renderer, no material copies)
        const float MetalKeep = .65f;
        static readonly int Metallic = Shader.PropertyToID("metallicFactor");
        public static void Matte(GameObject model)
        {
            if (!model) return; var mpb = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) { if (!ms[i] || !ms[i].HasProperty(Metallic)) continue; r.GetPropertyBlock(mpb, i); mpb.SetFloat(Metallic, ms[i].GetFloat(Metallic) * MetalKeep); r.SetPropertyBlock(mpb, i); }
            }
        }
        const float SoleExtra = .005f, SideShare = .012f, LegNear = .09f, LegSide = .03f, HipMargin = .03f, BodyDrop = .08f;   // shares of the model's height
        public static bool Off = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nomend") >= 0;   // test: compare with the models as rigged

        // test: the mesh's extent on each axis, and which bones the lowest skin points follow (with their bind positions)
        public static string Report(SkinnedMeshRenderer sk)
        {
            var m = sk.sharedMesh; if (!m || !m.isReadable) return "unreadable"; var v = m.vertices; var w = m.boneWeights; var bind = m.bindposes; var b = m.bounds;
            var sb = new System.Text.StringBuilder($"mesh {m.name} bounds min {b.min} max {b.max} | ");
            int axis = b.size.y >= b.size.z ? 1 : 2; float lo = b.min[axis], h = b.size[axis];
            var count = new Dictionary<int, float>();
            for (int i = 0; i < v.Length; i++) { if (v[i][axis] > lo + h * .03f) continue; void Add(int bi, float ww) { if (ww <= 0) return; count.TryGetValue(bi, out var c); count[bi] = c + ww; } Add(w[i].boneIndex0, w[i].weight0); Add(w[i].boneIndex1, w[i].weight1); Add(w[i].boneIndex2, w[i].weight2); Add(w[i].boneIndex3, w[i].weight3); }
            foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(count, k => k.Value), 8)) sb.Append($"{(kv.Key < sk.bones.Length && sk.bones[kv.Key] ? sk.bones[kv.Key].name : "?")}@{(Vector3)bind[kv.Key].inverse.GetColumn(3)} w{kv.Value:0} ; ");
            return sb.ToString();
        }

        // the body: the skin with the most points
        public static SkinnedMeshRenderer Body(GameObject model)
        {
            SkinnedMeshRenderer body = null;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh && (!body || r.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = r;
            return body;
        }

        // mended weights for a skin, or null when nothing needs changing
        public static BoneWeight[] Mended(Mesh src, Transform[] bones, out int changed)
        {
            changed = 0; if (!src || !src.isReadable) return null;
            var bind = src.bindposes; var w = src.boneWeights; var v = src.vertices; if (w.Length != v.Length || bind.Length == 0) return null;
            float lo = float.MaxValue, hi = float.MinValue; foreach (var p in v) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
            float h = hi - lo, side = h * SideShare;
            Vector3 P(int k) => bind[k].inverse.GetColumn(3);
            int footL = -1, footR = -1, shinL = -1, shinR = -1, thighL = -1, thighR = -1;
            var byName = new Dictionary<string, int>();
            for (int k = 0; k < bind.Length && k < bones.Length; k++)
            {
                if (!bones[k]) continue; byName[bones[k].name] = k; var n = bones[k].name.ToLower(); float bx = P(k).x;
                if (n.Contains("foot")) { if (bx < 0) footL = k; else footR = k; }
                else if (n.Contains("shin") || n.Contains("calf") || n.Contains("lowerleg")) { if (bx < 0) shinL = k; else shinR = k; }
                else if (n.Contains("thigh") || n.Contains("upperleg")) { if (bx < 0) thighL = k; else thighR = k; }
            }
            if (footL < 0 || footR < 0) return null;

            // 1. the legs: below the hip joints, a skin point clearly to one side never follows a bone of the other side; that share goes to the mirror bone
            if (thighL >= 0 && thighR >= 0)
            {
                var mirror = new int[bind.Length];
                for (int k = 0; k < mirror.Length; k++)
                {
                    mirror[k] = -1; if (k >= bones.Length || !bones[k]) continue; var n = bones[k].name;
                    string m = n.EndsWith(".L") ? n[..^2] + ".R" : n.EndsWith(".R") ? n[..^2] + ".L" : null;
                    if (m != null && byName.TryGetValue(m, out var mi)) mirror[k] = mi;
                }
                float hipY = Mathf.Min(P(thighL).y, P(thighR).y) - h * HipMargin, legSide = h * LegSide;
                float bodyLow = hipY - h * BodyDrop; float kneeY = shinL >= 0 && shinR >= 0 ? (P(shinL).y + P(shinR).y) / 2 : lo;
                int Fix(int bone, float x, float y, ref bool any)
                {
                    if (bone < 0 || bone >= bind.Length) return bone; float bx = P(bone).x;
                    // well below the hips, a leg point that follows the body (hips, spine) follows its own thigh instead: else it stays behind as the leg swings
                    if (Mathf.Abs(bx) < side) { if (y > bodyLow) return bone; any = true; return x < 0 ? thighL : thighR; }
                    if (Mathf.Sign(bx) == Mathf.Sign(x) || mirror[bone] < 0) return bone; any = true; return mirror[bone];
                }
                for (int i = 0; i < v.Length; i++)
                {
                    if (v[i].y > hipY || Mathf.Abs(v[i].x) < (v[i].y < kneeY ? side : legSide)) continue;   // below the knees the legs stand apart: only the very middle is left alone
                    var b = w[i]; bool any = false; float x = v[i].x;
                    float y = v[i].y;
                    if (b.weight0 > 0) b.boneIndex0 = Fix(b.boneIndex0, x, y, ref any); if (b.weight1 > 0) b.boneIndex1 = Fix(b.boneIndex1, x, y, ref any);
                    if (b.weight2 > 0) b.boneIndex2 = Fix(b.boneIndex2, x, y, ref any); if (b.weight3 > 0) b.boneIndex3 = Fix(b.boneIndex3, x, y, ref any);
                    if (!any) continue; w[i] = Merge(b); changed++;
                }
            }

            // 2. under the ankle joint, a skin point to one side is part of that shoe: it moves with that foot only
            float soleTop = Mathf.Max(P(footL).y, P(footR).y) + h * SoleExtra;
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i].y > soleTop || Mathf.Abs(v[i].x) < side) continue;
                int f = v[i].x < 0 ? footL : footR; var b = w[i]; if (b.boneIndex0 == f && b.weight0 > .99f) continue;
                w[i] = new BoneWeight { boneIndex0 = f, weight0 = 1 }; changed++;
            }

            // 3. between the ankle and the knee, close round the leg (cuffs, ankle ties, trouser hems): what follows the body moves over to
            // this leg's shin; a wide robe further out keeps its weights
            if (shinL >= 0 && shinR >= 0)
            {
                float near = h * LegNear;
                for (int i = 0; i < v.Length; i++)
                {
                    if (v[i].y <= soleTop || Mathf.Abs(v[i].x) < side) continue;
                    bool left = v[i].x < 0; int shin = left ? shinL : shinR, foot = left ? footL : footR; Vector3 knee = P(shin), ankle = P(foot);
                    if (v[i].y > knee.y) continue;
                    var seg = ankle - knee; float u = Mathf.Clamp01(Vector3.Dot(v[i] - knee, seg) / seg.sqrMagnitude); if (Vector3.Distance(v[i], knee + seg * u) > near) continue;
                    var b = w[i]; float moved = 0; bool Leg(int bone) => bone == shin || bone == foot;
                    if (b.weight0 > 0 && !Leg(b.boneIndex0)) { moved += b.weight0; b.weight0 = 0; }
                    if (b.weight1 > 0 && !Leg(b.boneIndex1)) { moved += b.weight1; b.weight1 = 0; }
                    if (b.weight2 > 0 && !Leg(b.boneIndex2)) { moved += b.weight2; b.weight2 = 0; }
                    if (b.weight3 > 0 && !Leg(b.boneIndex3)) { moved += b.weight3; b.weight3 = 0; }
                    if (moved < .01f) continue;
                    float onFoot = (b.boneIndex0 == foot ? b.weight0 : 0) + (b.boneIndex1 == foot ? b.weight1 : 0) + (b.boneIndex2 == foot ? b.weight2 : 0) + (b.boneIndex3 == foot ? b.weight3 : 0);
                    w[i] = new BoneWeight { boneIndex0 = shin, weight0 = 1 - onFoot, boneIndex1 = foot, weight1 = onFoot }; changed++;
                }
            }
            return changed > 0 ? w : null;
        }

        // Some models have the legs joined by a skin of triangles between the knees (the source mesh was one piece there). Once the legs move apart
        // these triangles stretch into white slabs. Below the hips, a triangle whose corners mostly follow different legs is removed.
        public static int CutBridges(Mesh m)
        {
            var v = m.vertices; var w = m.boneWeights; var bind = m.bindposes; if (w.Length != v.Length) return 0;
            float lo = float.MaxValue, hi = float.MinValue; foreach (var p in v) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
            float h = hi - lo, side = h * SideShare, hip = lo + h * .55f;
            var bx = new float[bind.Length]; for (int k = 0; k < bind.Length; k++) bx[k] = bind[k].inverse.GetColumn(3).x;
            var leg = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                if (v[i].y > hip) continue; var b = w[i]; float l = 0, r = 0;
                void S(int bi, float x) { if (x <= 0 || bi < 0 || bi >= bx.Length) return; if (bx[bi] < -side) l += x; else if (bx[bi] > side) r += x; }
                S(b.boneIndex0, b.weight0); S(b.boneIndex1, b.weight1); S(b.boneIndex2, b.weight2); S(b.boneIndex3, b.weight3);
                leg[i] = l > .6f ? -1 : r > .6f ? 1 : 0;
            }
            int cut = 0;
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var t = m.GetTriangles(s); var keep = new List<int>(t.Length);
                for (int i = 0; i < t.Length; i += 3)
                {
                    int a = leg[t[i]], b = leg[t[i + 1]], c = leg[t[i + 2]];
                    if ((a == -1 || b == -1 || c == -1) && (a == 1 || b == 1 || c == 1)) { cut++; continue; }
                    keep.Add(t[i]); keep.Add(t[i + 1]); keep.Add(t[i + 2]);
                }
                m.SetTriangles(keep, s);
            }
            return cut;
        }

        // the same bone may now appear twice: add those shares together and sort by weight, as Unity expects
        static BoneWeight Merge(BoneWeight b)
        {
            var d = new Dictionary<int, float>();
            void Add(int i, float x) { if (x <= 0) return; d.TryGetValue(i, out var c); d[i] = c + x; }
            Add(b.boneIndex0, b.weight0); Add(b.boneIndex1, b.weight1); Add(b.boneIndex2, b.weight2); Add(b.boneIndex3, b.weight3);
            var l = new List<KeyValuePair<int, float>>(d); l.Sort((p, q) => q.Value.CompareTo(p.Value)); float sum = 0; foreach (var p in l) sum += p.Value;
            var r = new BoneWeight();
            if (l.Count > 0) { r.boneIndex0 = l[0].Key; r.weight0 = l[0].Value / sum; }
            if (l.Count > 1) { r.boneIndex1 = l[1].Key; r.weight1 = l[1].Value / sum; }
            if (l.Count > 2) { r.boneIndex2 = l[2].Key; r.weight2 = l[2].Value / sum; }
            if (l.Count > 3) { r.boneIndex3 = l[3].Key; r.weight3 = l[3].Value / sum; }
            return r;
        }
    }
}
