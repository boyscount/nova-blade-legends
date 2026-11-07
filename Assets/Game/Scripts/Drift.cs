using UnityEngine;

namespace NovaBlade
{
    // a cloud layer that slides slowly: scrolls its own copy of the material's picture
    public class Drift : MonoBehaviour
    {
        public Vector2 speed; public float tile = 1;
        Material m;
        void Start() { var r = GetComponent<Renderer>(); if (!r) return; m = r.material; m.SetTextureScale("_BaseMap", Vector2.one * tile); }
        void Update() { if (m) m.SetTextureOffset("_BaseMap", speed * Time.time); }
    }
}
