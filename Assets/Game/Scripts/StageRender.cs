using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NovaBlade
{
    // Render settings one stage wants and the others must not get: Sky Lanes on a PC asks for deeper ambient occlusion and longer
    // shadows (depth on its stone, walls and cliffs). Set on entering a stage (Main.SetTheme), put back on leaving it.
    public static class StageRender
    {
        const float SkyShadow = 55, SkyAoIntensity = .9f, SkyAoRadius = .55f;
        static bool kept, on; static float shadow, aoIntensity, aoRadius;
        static object aoSettings; static System.Reflection.FieldInfo fIntensity, fRadius;

        public static void For(string stage)
        {
            bool want = stage == "Stage_skylanes" && !Main.IsPhone;
            if (want == on) return; var asset = UniversalRenderPipeline.asset; if (!asset) return;
            if (!kept) { kept = true; shadow = asset.shadowDistance; FindAo(asset); if (aoSettings != null) { aoIntensity = (float)fIntensity.GetValue(aoSettings); aoRadius = (float)fRadius.GetValue(aoSettings); } }
            on = want;
            if (shadow > 0) asset.shadowDistance = on ? SkyShadow : shadow;   // ?noshadow (0) stays off
            if (aoSettings != null) { fIntensity.SetValue(aoSettings, on ? SkyAoIntensity : aoIntensity); fRadius.SetValue(aoSettings, on ? SkyAoRadius : aoRadius); }
        }
        // the PC renderer's SSAO settings: URP keeps them private, so they are reached by name (nothing changes if a URP update renames them)
        static void FindAo(UniversalRenderPipelineAsset asset)
        {
            try
            {
                const System.Reflection.BindingFlags Any = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
                var list = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", Any)?.GetValue(asset) as ScriptableRendererData[];
                if (list == null) return;
                foreach (var data in list)
                {
                    if (!data) continue;
                    foreach (var f in data.rendererFeatures)
                    {
                        if (!f || f.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                        var s = f.GetType().GetField("m_Settings", Any)?.GetValue(f); if (s == null) continue;
                        fIntensity = s.GetType().GetField("Intensity", Any); fRadius = s.GetType().GetField("Radius", Any);
                        if (fIntensity != null && fRadius != null) { aoSettings = s; return; }
                    }
                }
            }
            catch (System.Exception e) { Debug.Log("[StageRender] SSAO not reachable: " + e.Message); }
        }
    }
}
