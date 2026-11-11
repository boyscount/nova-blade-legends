using System.Collections;
using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // -modethumbs <dir>: a picture of each lane battle map for the Battle Settings cards. Starts each mode in turn, frames the towers
    // and cores from high above at a slant, renders 512 x 300 off screen (no HUD) and writes mode_<n>.jpg into <dir>
    // (pointed at Assets/Game/Resources/UI/Modes, then the game is built again so the cards carry them).
    public static class ModeThumbs
    {
        public const int W = 512, H = 300;
        public static IEnumerator Run(Main g, string dir)
        {
            System.IO.Directory.CreateDirectory(dir); yield return new WaitForSecondsRealtime(3);
            var log = new System.Text.StringBuilder();
            for (int m = 0; m < 4; m++)
            {
                if (m == 3 && !g.Stage("Stage_skylanes")) continue;
                g.save.laneMode = m; g.StartBattle(); yield return new WaitForSecondsRealtime(2.5f);
                var keep = g.foes.Where(f => f && f.d != null && (f.d.id == "tower" || f.d.id == "core")).Select(f => f.Pos).ToList();
                if (keep.Count == 0) { log.AppendLine($"mode {m}: no towers"); continue; }
                var b = new Bounds(keep[0], Vector3.zero); foreach (var p in keep) b.Encapsulate(p); b.Expand(new Vector3(6, 0, 6));
                var go = new GameObject("ThumbCam"); var cam = go.AddComponent<Camera>(); cam.fieldOfView = 34; cam.nearClipPlane = .5f; cam.farClipPlane = 600;
                var main = Camera.main; if (main) { cam.clearFlags = main.clearFlags; cam.backgroundColor = main.backgroundColor; cullFrom(cam, main); }
                // look along the long side of the map, tipped 58 degrees down, far enough back that the whole length fits
                bool alongX = b.size.x > b.size.z; float len = Mathf.Max(b.size.x, b.size.z), wid = Mathf.Min(b.size.x, b.size.z);
                var fwd = alongX ? Vector3.forward : Vector3.right;   // the camera faces across the map, so the long side runs left to right
                float need = Mathf.Max(len / (2 * Mathf.Tan(cam.fieldOfView * .5f * Mathf.Deg2Rad) * (W / (float)H)), wid * .9f) * 1.08f;
                var dirv = Quaternion.AngleAxis(58, Vector3.Cross(Vector3.up, fwd)) * fwd;   // tipped down
                if (Vector3.Dot(dirv, Vector3.down) < 0) dirv = Quaternion.AngleAxis(-58, Vector3.Cross(Vector3.up, fwd)) * fwd;
                go.transform.position = b.center - dirv.normalized * need; go.transform.rotation = Quaternion.LookRotation(dirv, Vector3.up);
                var rt = new RenderTexture(W * 2, H * 2, 24) { antiAliasing = 4 }; cam.targetTexture = rt; cam.Render();
                var prev = RenderTexture.active; RenderTexture.active = rt; var big = new Texture2D(W * 2, H * 2, TextureFormat.RGB24, false); big.ReadPixels(new Rect(0, 0, W * 2, H * 2), 0, 0); big.Apply(); RenderTexture.active = prev;
                var small = new Texture2D(W, H, TextureFormat.RGB24, false);   // 2x2 box filter down to the card size
                for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    var c = big.GetPixel(x * 2, y * 2) + big.GetPixel(x * 2 + 1, y * 2) + big.GetPixel(x * 2, y * 2 + 1) + big.GetPixel(x * 2 + 1, y * 2 + 1);
                    small.SetPixel(x, y, c * .25f);
                }
                small.Apply(); System.IO.File.WriteAllBytes(System.IO.Path.Combine(dir, $"mode_{m}.jpg"), small.EncodeToJPG(88));
                log.AppendLine($"mode {m}: {keep.Count} towers, bounds {b.size}, cam at {go.transform.position}");
                cam.targetTexture = null; rt.Release(); Object.Destroy(rt); Object.Destroy(go); Object.Destroy(big); Object.Destroy(small);
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "modethumbs.txt"), log.ToString()); Application.Quit();
        }
        static void cullFrom(Camera cam, Camera main) { cam.cullingMask = main.cullingMask; }
    }
}
