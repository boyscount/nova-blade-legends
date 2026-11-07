using UnityEngine;

namespace NovaBlade
{
    // Wide sleeves modelled hanging from a T-posed arm: their own bones (arena_autorig "sleeves") keep the hanging direction, whatever the arm does.
    public class SleeveHang : MonoBehaviour
    {
        Transform[] bones; Quaternion[] rest;
        // call straight after creating the model, while it is still in its T-pose
        public static SleeveHang Attach(GameObject model)
        {
            var found = System.Array.FindAll(model.GetComponentsInChildren<Transform>(true), t => t.name.StartsWith("sleeve."));
            if (found.Length == 0) return null;
            var h = model.AddComponent<SleeveHang>(); h.bones = found; h.rest = new Quaternion[found.Length];
            for (int i = 0; i < found.Length; i++) h.rest[i] = Quaternion.Inverse(model.transform.rotation) * found[i].rotation;
            return h;
        }
        public void Apply() { for (int i = 0; i < bones.Length; i++) if (bones[i]) bones[i].rotation = transform.rotation * rest[i]; }
        void LateUpdate() => Apply();
    }
}
