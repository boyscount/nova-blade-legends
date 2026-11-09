using UnityEngine;

namespace NovaBlade
{
    // In battle the stage's sun often comes from in front of the hero, so the camera (behind the hero) sees the side lit only by the
    // cool sky ambient: the heroes look grey and pale next to the lobby, where a warm key light shapes them. This adds a warm key light
    // that follows the camera from above and to one side, and lights only the heroes (the player's hero, its pet and the champion bots),
    // through a rendering layer of their own: the stage itself does not change.
    public class HeroLight : MonoBehaviour
    {
        public const uint Bit = 1u << 1;   // rendering layer 1: "lit by the hero key"
        const float Intensity = .75f, Pitch = 32, Yaw = 38, Retag = .3f;
        static readonly Color Warm = new(1f, .9f, .78f);
        Light key; float next;

        void Start()
        {
            key = new GameObject("HeroKey").AddComponent<Light>(); key.transform.SetParent(transform, false);
            key.type = LightType.Directional; key.color = Warm; key.intensity = Intensity; key.shadows = LightShadows.None;
            key.renderingLayerMask = (int)Bit; key.enabled = false;
        }

        void LateUpdate()
        {
            var g = Main.G; if (!g || !g.cam || !key) return;
            bool on = g.mode is Main.Mode.Game or Main.Mode.Result; key.enabled = on; if (!on) return;
            var f = g.cam.transform.forward; f.y = 0; if (f.sqrMagnitude < 1e-4f) f = Vector3.forward;
            key.transform.rotation = Quaternion.LookRotation(f) * Quaternion.Euler(Pitch, Yaw, 0);   // from behind the camera, high and to the left
            if (Time.unscaledTime < next) return; next = Time.unscaledTime + Retag;   // weapons, wings and bots change: tag again now and then
            if (g.hero) Tag(g.hero.transform); if (g.pet) Tag(g.pet.transform);
            foreach (var foe in g.foes) if (foe && foe.d != null && foe.d.kind == "champ") Tag(foe.transform);
        }
        static void Tag(Transform t)
        {
            foreach (var r in t.GetComponentsInChildren<Renderer>()) if ((r.renderingLayerMask & Bit) == 0 && r is MeshRenderer or SkinnedMeshRenderer) r.renderingLayerMask |= Bit;
        }
    }
}
