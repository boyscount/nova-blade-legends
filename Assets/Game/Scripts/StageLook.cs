using UnityEngine;

namespace NovaBlade
{
    // Per-stage lighting/fog/sky, applied when the stage becomes active.
    public class StageLook : MonoBehaviour
    {
        public Color fog = Color.gray; public float fogDensity = .02f; public Color skyAmb, eqAmb, groundAmb; public Material sky; public Light sun;
        public void Apply()
        {
            // the CartoonFX torch/brazier fires carry a cartoon smoke column: keep the flame, drop the smoke
            foreach (var ps in GetComponentsInChildren<ParticleSystem>(true)) if (ps.name == "Smoke") ps.gameObject.SetActive(false);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = fog; RenderSettings.fogDensity = fogDensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight; RenderSettings.ambientSkyColor = skyAmb; RenderSettings.ambientEquatorColor = eqAmb; RenderSettings.ambientGroundColor = groundAmb;
            RenderSettings.skybox = sky; if (sun) RenderSettings.sun = sun;
            // nothing is light-baked, so build the ambient probe from the three ambient colours ourselves
            var sh = new UnityEngine.Rendering.SphericalHarmonicsL2(); sh.AddAmbientLight(eqAmb);
            sh.AddDirectionalLight(Vector3.up, (skyAmb - eqAmb) * 1.6f, 1); sh.AddDirectionalLight(Vector3.down, (groundAmb - eqAmb) * 1.6f, 1);
            RenderSettings.ambientProbe = sh;
            Shader.SetGlobalVector("_NovaAmbient", (skyAmb + eqAmb) * .5f);   // NovaBlade/Grass ambient
            if (Camera.main) { Camera.main.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor; Camera.main.backgroundColor = fog; }
        }
    }
}
