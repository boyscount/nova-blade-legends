using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Wings worn on the back: heroes carry them on the chest bone, pets and the toy robot on the top of their back.
    // The two halves (WingL/WingR, origins at the root) flap about their roots, slow at rest and harder on the move;
    // the material glows (emissive, picked up by the bloom) and a stream of soft motes drifts off them in the wings' colour.
    public class WingRig : MonoBehaviour
    {
        Transform anchor, pivot, left, right; Quaternion l0, r0; WingDef def; bool pet;
        Vector3 petOffset; float span, t, speed; Vector3 lastPos; ParticleSystem motes;

        // replace whatever this object wears with wings <id> (null or unknown: none)
        public static void Wear(GameObject host, Transform anchor, string id, bool pet)
        {
            foreach (var old in host.GetComponents<WingRig>()) { if (old.pivot) Destroy(old.pivot.gameObject); Destroy(old); }
            var def = Data.Wing(id); if (def == null || !anchor || !Catalog.I.Model("wing_" + id)) return;
            host.AddComponent<WingRig>().Build(anchor, def, pet);
        }

        void Build(Transform a, WingDef d, bool isPet)
        {
            anchor = a; def = d; pet = isPet;
            pivot = new GameObject("Wings").transform; pivot.SetParent(transform, false);
            var w = Instantiate(Catalog.I.Model("wing_" + d.id), pivot); w.transform.localPosition = Vector3.zero; w.transform.localRotation = Quaternion.identity;
            var parts = w.GetComponentsInChildren<Transform>(true);
            left = parts.FirstOrDefault(x => x.name == "WingL"); right = parts.FirstOrDefault(x => x.name == "WingR");
            if (left) l0 = left.localRotation; if (right) r0 = right.localRotation;
            foreach (var r in w.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (pet)
            {   // pets: size and seat the wings from the model's bounds (top of the back)
                var rs = anchor.GetComponentsInChildren<Renderer>(); var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                span = Mathf.Max(b.size.x, b.size.y) * 1.25f;
                var c = anchor.InverseTransformPoint(b.center); petOffset = new Vector3(0, c.y + b.extents.y * .25f, c.z - b.extents.z * .55f);
            }
            else span = 1.75f;   // full spread, a little wider than the hero is tall
            // tall pairs (the angel wings are as tall as they are wide) are held to ~60% of the spread in height,
            // so the tips stay below the head-room of the lobby frame
            pivot.rotation = Quaternion.identity;   // measure upright (LateUpdate turns it to the wearer)
            var wr = w.GetComponentsInChildren<Renderer>(); var wb = wr[0].bounds; foreach (var r in wr) wb.Encapsulate(r.bounds);
            float aspect = wb.size.y / Mathf.Max(.01f, wb.size.x); if (aspect > .62f) span *= .62f / aspect;
            pivot.localScale = Vector3.one * span;
            Motes(); lastPos = transform.position;
        }

        void Motes()
        {
            var go = new GameObject("WingMotes"); go.transform.SetParent(pivot, false); go.transform.localPosition = new Vector3(0, .1f, 0);
            motes = go.AddComponent<ParticleSystem>(); motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = motes.main; m.loop = true; m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = 160;
            m.startLifetime = new ParticleSystem.MinMaxCurve(.9f, 2f); m.startSpeed = 0; m.startSize = new ParticleSystem.MinMaxCurve(.03f * span, .075f * span);
            var c = def.glow; m.startColor = new ParticleSystem.MinMaxGradient(c * 2.2f, Color.Lerp(c, Color.white, .55f) * 2.2f);
            var sh = motes.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(.95f, .55f, .05f);
            var em = motes.emission; em.rateOverTime = pet ? 10 : 22;
            var vel = motes.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            var (vy0, vy1, side) = def.id switch { "fire" => (.5f, 1.1f, .15f), "storm" => (-.2f, .4f, .6f), "death" => (.1f, .4f, .25f), _ => (-.35f, .05f, .2f) };
            vel.x = new ParticleSystem.MinMaxCurve(-side, side); vel.y = new ParticleSystem.MinMaxCurve(vy0, vy1); vel.z = new ParticleSystem.MinMaxCurve(-side, side);
            var col = motes.colorOverLifetime; col.enabled = true; var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); col.color = g;
            var sz = motes.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, 1, 1, def.id == "fire" ? .2f : .6f));
            if (def.id == "storm") { var noise = motes.noise; noise.enabled = true; noise.strength = .8f; noise.frequency = 3; }   // crackling sparks
            var mat = new Material(Catalog.I.glowMat) { mainTexture = Dot() }; mat.SetFloat("_SrcBlend", 1); mat.SetFloat("_DstBlend", 1);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = mat; motes.Play();
        }
        static Texture2D dot;
        static Texture2D Dot()
        {
            if (dot) return dot; const int N = 32; dot = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++) for (int x = 0; x < N; x++) { float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(N / 2f, N / 2f)) / (N / 2f); float a = Mathf.Clamp01(1 - r); dot.SetPixel(x, y, new Color(1, 1, 1, a * a)); }
            dot.Apply(); return dot;
        }

        void LateUpdate()
        {
            if (!pivot) return;
            if (!anchor) { pivot.gameObject.SetActive(false); return; }
            float dt = Time.deltaTime; t += dt;
            var mv = transform.position - lastPos; mv.y = 0; lastPos = transform.position;
            speed = Mathf.Lerp(speed, dt > 0 ? mv.magnitude / dt : 0, Mathf.Min(1, dt * 6));
            var fwd = transform.forward; var up = Vector3.up;
            pivot.position = pet ? anchor.TransformPoint(petOffset) : anchor.position + up * .1f - fwd * .14f;
            pivot.rotation = Quaternion.LookRotation(fwd, up);
            // flap: a slow breath at rest, full beats when moving fast; the wings also sweep back a little as speed builds
            float k = Mathf.Clamp01(speed / 4f), rate = Mathf.Lerp(2.2f, 7.5f, k), amp = Mathf.Lerp(9, 24, k);
            float beat = Mathf.Sin(t * rate) * amp, sweep = 10 + 18 * k;
            if (left) left.localRotation = l0 * Quaternion.Euler(0, sweep, beat);
            if (right) right.localRotation = r0 * Quaternion.Euler(0, -sweep, -beat);
        }

        void OnDestroy() { if (pivot) Destroy(pivot.gameObject); }
    }
}
