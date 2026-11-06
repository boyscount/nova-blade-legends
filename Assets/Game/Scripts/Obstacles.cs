using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // Solid scenery: rocks, tree trunks, houses, walls, wells, lanterns and props block movement.
    // Built once per stage from the stage root's objects (by name), as circles on the ground; Main.Clamp pushes units out.
    public static class Obstacles
    {
        static readonly Dictionary<Transform, List<Vector3>> cache = new();   // x, z, radius
        public static List<Vector3> Current = new();
        static readonly string[] Solid = { "rock", "boulder", "menhir", "stone", "tree", "pine", "trunk", "stump", "house", "preset_", "tavern", "church",
            "tower", "windmill", "stable", "blacksmith", "wall", "well", "fence", "gate", "pillar", "column", "crate", "barrel", "lantern", "lamp", "pole", "planter", "dais", "logs", "statue", "chest" };
        static readonly string[] Trunk = { "tree", "pine" };   // trees block only at the trunk, not the whole canopy

        public static void Use(Transform stage)
        {
            if (!stage) { Current = new(); return; }
            if (!cache.TryGetValue(stage, out var list)) cache[stage] = list = Build(stage);
            Current = list;
        }
        public static void Add(Transform extra)
        {   // scenery shown on top of the current stage
            if (!cache.TryGetValue(extra, out var list)) cache[extra] = list = Build(extra);
            Current = new List<Vector3>(Current); Current.AddRange(list);
        }
        static List<Vector3> Build(Transform stage)
        {
            var list = new List<Vector3>();
            foreach (Transform t in stage)
            {
                string n = t.name.ToLowerInvariant(); bool solid = false, trunk = false;
                foreach (var k in Solid) if (n.Contains(k)) { solid = true; break; }
                foreach (var k in Trunk) if (n.Contains(k)) trunk = true;
                if (!solid || n.Contains("grass") || n.Contains("bush") || n.Contains("light") || n.Contains("ground") || n.Contains("bridge") || n.Contains("water")) continue;
                var rs = t.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.size.y < .45f) continue;   // flat things (rugs, low stones, floor tiles) stay walkable
                float rad = trunk ? Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * .18f, .35f, 1.2f) : Mathf.Min(b.extents.x, b.extents.z) * .85f;
                if (rad < .2f) continue;
                list.Add(new Vector3(b.center.x, b.center.z, Mathf.Min(rad, 9)));
            }
            return list;
        }
        // push a point out of every obstacle circle (unit radius 'pad')
        public static Vector3 Push(Vector3 p, float pad = .45f)
        {
            foreach (var o in Current)
            {
                float dx = p.x - o.x, dz = p.z - o.y, r = o.z + pad, d2 = dx * dx + dz * dz;
                if (d2 >= r * r) continue; float d = Mathf.Sqrt(d2);
                if (d < 1e-3f) { dx = 0; dz = 1; d = 1; }
                p.x = o.x + dx / d * r; p.z = o.y + dz / d * r;
            }
            return p;
        }
    }
}
