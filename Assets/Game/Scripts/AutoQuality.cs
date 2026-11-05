using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace NovaBlade
{
    // Web only: many laptops run the browser on their built-in graphics, where the full picture holds about 16 fps.
    // Watches the frame time and steps the picture down until the game runs smoothly; the level is kept for the next visit.
    public class AutoQuality : MonoBehaviour
    {
        const string Key = "novaQuality"; const float SlowMs = 28, HiddenMs = 300; const int Window = 60, MaxLevel = 3;
        public static int Level { get; private set; }
        public static bool Paused;
        UniversalRenderPipelineAsset rp; float settle = 6; int slowWindows; readonly float[] ms = new float[Window]; int n;

        void Start()
        {
            if (Application.platform != RuntimePlatform.WebGLPlayer) { enabled = false; return; }
            rp = UniversalRenderPipeline.asset; Level = Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, MaxLevel); Apply();
        }
        void Apply()
        {
            if (Level >= 1) rp.msaaSampleCount = 1;
            if (Level >= 2) rp.renderScale = Level >= 3 ? .65f : .8f;
            if (Level >= 3) rp.shadowDistance = Mathf.Min(rp.shadowDistance, 15);
            Debug.Log($"[Quality] level {Level}: msaa {rp.msaaSampleCount} scale {rp.renderScale} shadows {rp.shadowDistance}");
        }
        void Update()
        {
            if (Paused || Level >= MaxLevel) return;
            float dt = Time.unscaledDeltaTime * 1000;
            if (settle > 0) { settle -= Time.unscaledDeltaTime; return; }   // the first seconds after loading are not typical
            ms[n++] = dt; if (n < Window) return; n = 0;
            System.Array.Sort(ms); float median = ms[Window / 2];
            if (median > HiddenMs) { slowWindows = 0; return; }   // a hidden tab runs at about one frame a second
            slowWindows = median > SlowMs ? slowWindows + 1 : 0; if (slowWindows < 2) return;
            slowWindows = 0; settle = 2; Level++; PlayerPrefs.SetInt(Key, Level); PlayerPrefs.Save(); Apply();
        }
    }
}
