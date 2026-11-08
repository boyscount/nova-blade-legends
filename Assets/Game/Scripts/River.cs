using UnityEngine;

namespace NovaBlade
{
    // The river valley stage: a winding river across the arena with two wooden bridges.
    // Ground() is shared by the terrain builder and by every character, so wading heroes sink into the water.
    public static class River
    {
        public static bool Active;
        public const float HalfW = 2.6f, Depth = .75f;
        // the green valley: a shallow river (the ground dips .45 under the water) crossed by two bridges (BridgeDeck)
        public static bool Valley;
        public static bool Sky;     // Sky Lanes: raised bases, a sunken jungle (SkyLanes.Height)
        public static bool Wilds;   // the Open World zone: its own land (WildsMap), with the same shallow water as the valley
        public static bool Wet => Active || Valley || Wilds;
        public static float WaterY => Valley || Wilds ? -.22f : -.32f;
        static float VZ(float x) => -3 + Mathf.Sin(x * .09f) * 4.5f;   // same curve as NovaBuild.ValleyRiver
        public static readonly float[] BridgeX = { -9f, 8.5f };
        public static float Z(float x) => -4 + Mathf.Sin(x * .12f) * 2.5f;   // river centre line

        public static bool OnBridge(float x, float z)
        {
            foreach (var bx in BridgeX) if (Mathf.Abs(x - bx) < 1.7f && Mathf.Abs(z - Z(bx)) < HalfW + 1.6f) return true;
            return false;
        }
        // terrain height (0 = normal ground); bridges hold characters just above the water
        public static float Bed(float x, float z)
        {
            float d = Mathf.Abs(z - Z(x));
            return -Depth * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(HalfW + 1.4f, HalfW - .6f, d));
        }
        public static float Ground(float x, float z)
        {
            if (BridgeDeck.Height(x, z, out var deck)) return deck;
            if (Wilds) return WildsMap.Height(x, z);
            if (Sky) return SkyLanes.Height(x, z);
            if (Valley) return -.45f * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.4f - .6f, 2.4f + 1.2f, Mathf.Abs(z - VZ(x)))));
            if (!Active) return 0;
            if (OnBridge(x, z)) return .2f;
            return Bed(x, z);
        }
        public static bool InWater(float x, float z) => Valley || Wilds ? !BridgeDeck.Height(x, z, out _) && Ground(x, z) < WaterY - .1f
            : Active && !OnBridge(x, z) && Bed(x, z) < WaterY + .05f;
    }
}
