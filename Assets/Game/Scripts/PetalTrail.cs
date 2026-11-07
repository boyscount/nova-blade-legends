using UnityEngine;

namespace NovaBlade
{
    // The way to the secret cave, shown with no arrow: while the hero is near, a ribbon of brighter petals keeps flying from the old stone yard to the cave door, across the wind.
    public class PetalTrail : MonoBehaviour
    {
        static readonly Vector2[] Path = { new(-6, -22), new(-3, -30), new(-9, -37), new(-3, -44), new(4, -47), WildsMap.Cave + new Vector2(0, 3) };
        const float NearPath = 22, Speed = 4.5f, Rest = 1.2f, Above = 1.6f, Drift = 2.4f, Rate = 34;
        ParticleSystem ps; float along, pause; int leg;
        public void Set(Transform ribbon)
        {
            ps = ribbon.GetComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var m = ps.main; m.prewarm = false; m.maxParticles = 130; m.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 3.8f);
            m.startSizeX = new ParticleSystem.MinMaxCurve(.3f, .44f); m.startSizeY = new ParticleSystem.MinMaxCurve(.3f, .44f); m.startColor = new ParticleSystem.MinMaxGradient(new Color(1.6f, 1.45f, .85f), new Color(1.5f, 1.15f, 1.2f));
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Sphere; sh.scale = Vector3.one; sh.radius = .5f;
            ps.Play();
        }
        void Update()
        {
            var G = Main.G; if (!ps || !G || !G.wilds || !G.hero) return;
            var h = new Vector2(G.hero.Pos.x, G.hero.Pos.z); float best = 1e9f; foreach (var p in Path) best = Mathf.Min(best, Vector2.Distance(h, p));
            var em = ps.emission; bool flying = best < NearPath && pause <= 0; em.rateOverTime = flying ? Rate : 0;
            if (pause > 0) { pause -= Time.deltaTime; return; }
            if (best >= NearPath) return;
            var a = Path[leg]; var b = Path[leg + 1]; float len = Vector2.Distance(a, b); along += Speed * Time.deltaTime;
            if (along >= len) { along -= len; leg++; if (leg + 1 >= Path.Length) { leg = 0; along = 0; pause = Rest; return; } a = Path[leg]; b = Path[leg + 1]; len = Vector2.Distance(a, b); }
            var at = Vector2.Lerp(a, b, along / len); ps.transform.position = new Vector3(at.x, WildsMap.Height(at.x, at.y) + Above, at.y);
            var dir = (b - a).normalized; var v = ps.velocityOverLifetime; v.x = dir.x * Drift; v.z = dir.y * Drift; v.y = new ParticleSystem.MinMaxCurve(-.1f, .05f);
        }
    }
}
