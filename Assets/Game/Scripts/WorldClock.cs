using UnityEngine;

namespace NovaBlade
{
    // The zone's time of day: part of the world, not of the player, so it could later live on a server. It only runs while someone is in the zone.
    public class WorldClock
    {
        public const float DayLen = 240, NightLen = 120, Cycle = DayLen + NightLen, Fade = 20;   // the light turns in the last Fade seconds of day and of night
        public float t;
        public bool IsNight => t >= DayLen;
        public float SecondsLeft => IsNight ? Cycle - t : DayLen - t;
        public void Tick(float dt) => t = Mathf.Repeat(t + dt, Cycle);
        public float Darkness => t < DayLen ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(DayLen - Fade, DayLen, t)) : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(Cycle - Fade, Cycle, t));
    }
}
