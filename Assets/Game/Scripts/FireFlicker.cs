using UnityEngine;

namespace NovaBlade
{
    // a fire's point light that breathes like a flame (braziers placed by the stage builder)
    public class FireFlicker : MonoBehaviour
    {
        Light l; float baseI, seed;
        void Awake() { l = GetComponent<Light>(); baseI = l.intensity; seed = Random.value * 10; }
        void Update() { float t = Time.time * 7 + seed; l.intensity = baseI * (.8f + .2f * Mathf.PerlinNoise(t, seed)); }
    }
}
