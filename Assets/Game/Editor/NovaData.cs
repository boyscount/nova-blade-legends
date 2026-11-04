// Writes the game's current numbers to Assets/Game/Resources/Data/balance.json (the file the game reads at start-up).
//   Unity.exe -batchmode -quit -projectPath . -executeMethod NovaData.Export
using System.IO;
using NovaBlade;
using UnityEditor;
using UnityEngine;

public static class NovaData
{
    const string Path = "Assets/Game/Resources/Data/balance.json";
    [MenuItem("Nova Blade/Export balance.json")]
    public static void Export()
    {
        Balance.Load();   // start from what the file already says, so an export never drops edits
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        // floats come out as 1.0499999523: round to 4 places so the file reads like the numbers that were typed
        var json = System.Text.RegularExpressions.Regex.Replace(JsonUtility.ToJson(Balance.Current(), true), @"-?\d+\.\d{5,}",
            m => float.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
        File.WriteAllText(Path, json);
        AssetDatabase.ImportAsset(Path); Debug.Log("[NovaData] wrote " + Path);
    }
}
