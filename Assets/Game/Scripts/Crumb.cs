using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace NovaBlade
{
    // Crash trail for phones: the last step reached is written straight to the browser's localStorage, which survives the tab being killed;
    // ?fps shows the previous run's last step, so a crash can be placed without any debugger on the device.
    public static class Crumb
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void NovaCrumbSet(string s);
        [DllImport("__Internal")] static extern string NovaCrumbGet();
#else
        static void NovaCrumbSet(string s) { }
        static string NovaCrumbGet() => "";
#endif
        public static string Previous { get; private set; } = "";
        public static void Begin() => Previous = NovaCrumbGet();
        public static int RamMb => (int)(Profiler.GetTotalReservedMemoryLong() / 1048576);
        public static void Mark(string step) { var s = $"{step} RAM {RamMb}"; Debug.Log("[Crumb] " + s); NovaCrumbSet(s); }
        public static bool Has(string urlFlag) => Application.absoluteURL.Contains(urlFlag);   // test switches in the page address
    }
}
