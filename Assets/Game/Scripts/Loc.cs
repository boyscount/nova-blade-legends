using UnityEngine;

namespace NovaBlade
{
    // Language: English by default, Thai as an option. Text in the code is written as Loc.S(thai, english).
    // Data (hero, skill, stage names) is built once at start, so switching language reloads the game.
    public static class Loc
    {
        const string Key = "novaLang";
        public static readonly bool En = PlayerPrefs.GetString(Key, "en") != "th" && System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-thai") < 0;   // (-thai: test runs)
        public static string S(string th, string en) => En ? en : th;
        public static void Switch()
        {
            PlayerPrefs.SetString(Key, En ? "th" : "en"); PlayerPrefs.Save();
#if UNITY_WEBGL && !UNITY_EDITOR
            Application.ExternalEval("location.reload()");
#elif UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            System.Diagnostics.Process.Start(System.Environment.GetCommandLineArgs()[0]); Application.Quit();
#endif
        }
    }
}
