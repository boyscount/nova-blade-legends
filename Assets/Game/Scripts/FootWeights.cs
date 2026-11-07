using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // The auto-rigged models let part of each shoe sole follow the other leg too: while walking, the sole then stretches back
    // towards the other foot (a long dark sheet under Tenzin, a spike off the elf's heel). Below the ankle joint, a skin point follows only
    // its own foot. The mended mesh is a copy; only the hero being shown keeps one, so phones do not hold a copy of every hero.
    public static class FootWeights
    {
        const float SoleExtra = .005f, SideShare = .012f, LegNear = .09f;   // of the model's height: a little above the ankle joint still counts as shoe; how far from the middle counts as left or right
        static Mesh lastSrc, lastCopy;
        public static int LastChanged;
        public static bool Off = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-nomend") >= 0;   // test: compare with the models as rigged
        // test: the mesh's extent on each axis, and which bones the lowest skin points follow (with their bind positions)
        public static string Report(SkinnedMeshRenderer sk)
        {
            var m = sk.sharedMesh; if (!m || !m.isReadable) return "unreadable"; var v = m.vertices; var w = m.boneWeights; var bind = m.bindposes; var b = m.bounds;
            var sb = new System.Text.StringBuilder($"bounds min {b.min} max {b.max} root {(sk.rootBone ? sk.rootBone.name : "-")} meshT rot {sk.transform.localEulerAngles} | ");
            int axis = b.size.y >= b.size.z ? 1 : 2; float lo = b.min[axis], h = b.size[axis];
            var count = new Dictionary<int, float>();
            for (int i = 0; i < v.Length; i++) { if (v[i][axis] > lo + h * .03f) continue; void Add(int bi, float ww) { if (ww <= 0) return; count.TryGetValue(bi, out var c); count[bi] = c + ww; } Add(w[i].boneIndex0, w[i].weight0); Add(w[i].boneIndex1, w[i].weight1); Add(w[i].boneIndex2, w[i].weight2); Add(w[i].boneIndex3, w[i].weight3); }
            foreach (var kv in System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(count, k => k.Value), 8)) sb.Append($"{(kv.Key < sk.bones.Length && sk.bones[kv.Key] ? sk.bones[kv.Key].name : "?")}@{(Vector3)bind[kv.Key].inverse.GetColumn(3)} w{kv.Value:0} ; ");
            return sb.ToString();
        }
        public static void Mend(GameObject model)
        {
            if (!model || Off) return;
            SkinnedMeshRenderer body = null;   // the body: the skin with the most points (only one mended copy is kept at a time)
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true)) if (r.sharedMesh && (!body || r.sharedMesh.vertexCount > body.sharedMesh.vertexCount)) body = r;
            if (!body) return;
            foreach (var sk in new[] { body })
            {
                var src = sk.sharedMesh;
                if (src == lastSrc) { if (lastCopy) sk.sharedMesh = lastCopy; continue; }
                if (lastCopy) Object.Destroy(lastCopy); lastSrc = src; lastCopy = null; if (!src.isReadable) continue;
                var bind = src.bindposes; var w = src.boneWeights; var v = src.vertices; if (w.Length != v.Length || bind.Length == 0) continue;
                float lo = float.MaxValue, hi = float.MinValue; foreach (var p in v) { lo = Mathf.Min(lo, p.y); hi = Mathf.Max(hi, p.y); }
                float h = hi - lo, side = h * SideShare;
                // the two foot bones, told apart by which side their bind position lies on
                int footL = -1, footR = -1, shinL = -1, shinR = -1; var bones = sk.bones;
                for (int k = 0; k < bind.Length && k < bones.Length; k++)
                {
                    if (!bones[k]) continue; var n = bones[k].name.ToLower(); float bx = bind[k].inverse.GetColumn(3).x;
                    if (n.Contains("foot")) { if (bx < 0) footL = k; else footR = k; } else if (n.Contains("shin") || n.Contains("calf") || n.Contains("lowerleg")) { if (bx < 0) shinL = k; else shinR = k; }
                }
                if (footL < 0 || footR < 0) continue;
                float soleTop = Mathf.Max(bind[footL].inverse.GetColumn(3).y, bind[footR].inverse.GetColumn(3).y) + h * SoleExtra;
                int changed = 0;
                for (int i = 0; i < v.Length; i++)
                {   // under the ankle joint, a skin point to one side is part of that shoe: it moves with that foot only
                    if (v[i].y > soleTop || Mathf.Abs(v[i].x) < side) continue;
                    int f = v[i].x < 0 ? footL : footR; var b = w[i]; if (b.boneIndex0 == f && b.weight0 > .99f) continue;
                    w[i] = new BoneWeight { boneIndex0 = f, weight0 = 1 }; changed++;
                }
                // between the ankle and the knee, close round the leg (cuffs, ankle ties, trouser hems): what follows the body or the
                // other leg moves over to this leg's shin; a wide robe further out keeps its weights
                if (shinL >= 0 && shinR >= 0)
                {
                    Vector3 P(int k) => bind[k].inverse.GetColumn(3); float near = h * LegNear;
                    bool Leg(int bone, int shin, int foot) => bone == shin || bone == foot;
                    for (int i = 0; i < v.Length; i++)
                    {
                        if (v[i].y <= soleTop || Mathf.Abs(v[i].x) < side) continue;
                        bool left = v[i].x < 0; int shin = left ? shinL : shinR, foot = left ? footL : footR; Vector3 knee = P(shin), ankle = P(foot);
                        if (v[i].y > knee.y) continue;
                        var seg = ankle - knee; float u = Mathf.Clamp01(Vector3.Dot(v[i] - knee, seg) / seg.sqrMagnitude); if (Vector3.Distance(v[i], knee + seg * u) > near) continue;
                        var b = w[i]; float moved = 0;
                        if (b.weight0 > 0 && !Leg(b.boneIndex0, shin, foot)) { moved += b.weight0; b.weight0 = 0; }
                        if (b.weight1 > 0 && !Leg(b.boneIndex1, shin, foot)) { moved += b.weight1; b.weight1 = 0; }
                        if (b.weight2 > 0 && !Leg(b.boneIndex2, shin, foot)) { moved += b.weight2; b.weight2 = 0; }
                        if (b.weight3 > 0 && !Leg(b.boneIndex3, shin, foot)) { moved += b.weight3; b.weight3 = 0; }
                        if (moved < .01f) continue;
                        // gather what is left plus the moved share into at most two bones: this shin and this foot
                        float onFoot = (b.boneIndex0 == foot ? b.weight0 : 0) + (b.boneIndex1 == foot ? b.weight1 : 0) + (b.boneIndex2 == foot ? b.weight2 : 0) + (b.boneIndex3 == foot ? b.weight3 : 0);
                        w[i] = new BoneWeight { boneIndex0 = shin, weight0 = 1 - onFoot, boneIndex1 = foot, weight1 = onFoot }; changed++;
                    }
                }
                LastChanged = changed; if (changed == 0) continue;
                var copy = Object.Instantiate(src); copy.name = src.name; copy.boneWeights = w; lastCopy = copy; sk.sharedMesh = copy;
            }
        }
    }
}
