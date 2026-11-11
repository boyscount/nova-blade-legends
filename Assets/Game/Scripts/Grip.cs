using UnityEngine;

namespace NovaBlade
{
    // Closed hands for heroes rigged with mitten fingers (arena_fingers.py): each frame, after the animation, the four-finger bone and
    // the thumb turn from their rest about the hinge their "_curl" marker gives (its local +Y), so the hand closes round the weapon.
    // The animation clips do not move these bones (they are not part of the humanoid avatar), so they would stay flat without this.
    public class Grip : MonoBehaviour
    {
        public static float FingerDeg = 100, ThumbDeg = 55;   // how far they close
        public static float Sign = -1;                        // Blender's right-handed hinge turned the other way in Unity's left-handed space
        Transform[] bones; Quaternion[] rest; Vector3[] hinge;   // hinge: in the bone's own space at rest
        float k = 1;   // 0 open .. 1 closed

        // call straight after creating the model, while it is still in its bind pose
        public static Grip Attach(GameObject model)
        {
            var all = model.GetComponentsInChildren<Transform>(true);
            var marks = System.Array.FindAll(all, t => t.name.EndsWith("_curl") && t.parent && (t.parent.name.StartsWith("fingers.") || t.parent.name.StartsWith("thumb.")));
            if (marks.Length == 0) return null;
            bool turn = TurnHinge(model.name);
            var g = model.AddComponent<Grip>(); int n = marks.Length; g.bones = new Transform[n]; g.rest = new Quaternion[n]; g.hinge = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                var b = marks[i].parent; g.bones[i] = b; g.rest[i] = b.localRotation;
                g.hinge[i] = b.InverseTransformDirection(marks[i].up).normalized;   // the marker's +Y, kept in the bone's space
                if (turn && b.parent) { var f = b.InverseTransformDirection(b.position - b.parent.position).normalized; var h = Vector3.Cross(f, g.hinge[i]); if (h.sqrMagnitude > 1e-4f) g.hinge[i] = h.normalized; }
            }
            return g;
        }
        // models whose "_curl" marker came out pointing out of the palm instead of across the knuckles (the fingers swung sideways and the
        // hand stayed open on the weapon): the hinge is turned a quarter round the finger, so it lies across the knuckles
        static bool TurnHinge(string model) => model.StartsWith("Hero_spartan") && !model.Contains("_classic");
        public void Set(float closed) => k = Mathf.Clamp01(closed);
        public void Apply()
        {
            for (int i = 0; i < bones.Length; i++)
            {
                var b = bones[i]; if (!b) continue; bool thumb = b.name.StartsWith("thumb.");
                b.localRotation = rest[i] * Quaternion.AngleAxis(Sign * k * (thumb ? ThumbDeg : FingerDeg), hinge[i]);
            }
        }
        void LateUpdate() => Apply();
    }
}
