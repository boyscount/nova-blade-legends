using System.Collections.Generic;
using UnityEngine;

namespace NovaBlade
{
    // A walkable bridge: the deck's height profile, measured from the bridge model when the stage is built (NovaBuild.Deck),
    // so heroes, pets and monsters walk up onto it instead of through it. Only the decks of the active stage count.
    public class BridgeDeck : MonoBehaviour
    {
        public Vector3 center, axis, side;   // world space: middle of the deck, along the bridge, across it
        public float halfLen, halfWid;
        public float[] h;                     // deck top heights, evenly spaced from one end to the other

        static readonly List<BridgeDeck> all = new List<BridgeDeck>();
        void OnEnable() { if (!all.Contains(this)) all.Add(this); }
        void OnDisable() => all.Remove(this);

        public static bool Height(float x, float z, out float y)
        {
            y = 0;
            foreach (var d in all)
            {
                if (d.h == null || d.h.Length < 2) continue;
                var p = new Vector3(x - d.center.x, 0, z - d.center.z);
                float a = Vector3.Dot(p, d.axis), s = Vector3.Dot(p, d.side);
                if (Mathf.Abs(a) > d.halfLen || Mathf.Abs(s) > d.halfWid) continue;
                float u = (a + d.halfLen) / (2 * d.halfLen) * (d.h.Length - 1); int i = Mathf.Clamp(Mathf.FloorToInt(u), 0, d.h.Length - 2);
                y = Mathf.Lerp(d.h[i], d.h[i + 1], u - i); return true;
            }
            return false;
        }
    }
}
