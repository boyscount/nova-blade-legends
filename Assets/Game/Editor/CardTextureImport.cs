using UnityEditor;

// Saved card pictures keep their own shape: by default Unity stretches a 384 x 576 picture to a power-of-two size, which makes the heroes on the cards look wide.
public class CardTextureImport : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.Contains("/Resources/UI/Cards/")) return;
        var ti = (TextureImporter)assetImporter; ti.npotScale = TextureImporterNPOTScale.None; ti.alphaIsTransparency = true;
    }
}
