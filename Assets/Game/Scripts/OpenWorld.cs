using UnityEngine;
using UnityEngine.InputSystem;

namespace NovaBlade
{
    // The zone's land, shared by the terrain builder, every character and the grass: a shallow stream to wade or bridge, a hill to climb, a low rise.
    public static class WildsMap
    {
        public const float StreamHalf = 2.4f, StreamDepth = .45f, HillH = 8, RiseH = 3.5f;
        public static readonly Vector2 Hill = new(30, -34), Rise = new(-32, 30), House = new(-20, 16);
        const float HillR = 22, HillTop = 6, RiseR = 18;
        static readonly Vector2[] Trail = { new(2, -12), new(16, -20), new(24, -44), new(38, -40), new(36, -30), new(30, -34) };   // the worn way up the hill
        public static float StreamZ(float x) => -6 + Mathf.Sin(x * .07f) * 7;
        public static float StreamSlope(float x) => .49f * Mathf.Cos(x * .07f);
        public static bool Dry(float x, float z, float pad = 0) => Mathf.Abs(z - StreamZ(x)) > StreamHalf + 1.5f + pad;
        static float Mound(float x, float z, Vector2 c, float r, float top, float h) => h * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(top, r, Vector2.Distance(new Vector2(x, z), c))));
        public static float Height(float x, float z)
        {
            float bed = -StreamDepth * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(StreamHalf - .6f, StreamHalf + 1.2f, Mathf.Abs(z - StreamZ(x)))));
            return bed + Mound(x, z, Hill, HillR, HillTop, HillH) + Mound(x, z, Rise, RiseR, 4, RiseH);
        }
        public static float TrailDistance(float x, float z)
        {
            var p = new Vector2(x, z); float best = 1e9f;
            for (int i = 0; i + 1 < Trail.Length; i++) { var a = Trail[i]; var d = Trail[i + 1] - a; float u = Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude); best = Mathf.Min(best, Vector2.Distance(p, a + d * u)); }
            return best;
        }
    }
    // Open World zone 1, Driftpetal Wilds (design: OPEN-WORLD-ZONE1.md): a walkable area reached from the city gate, a house to enter and fight in, and the way back.
    public class OpenWorld : MonoBehaviour
    {
        public const float Radius = 60, RoomHalf = 8;
        public static readonly Vector3 Start = new(0, 0, 54), Gate = new(0, 0, 57.5f), RoomStart = new(0, 0, 3.6f), RoomExit = new(0, 0, 7.6f), Chest = new(0, 0, -6.2f);
        const float TalkRange = 3.4f, GlowEvery = 1.1f;
        const int ChestGold = 60;
        public bool HouseCleared { get; private set; }   // the chest has been opened on this visit to the zone
        Main G => Main.G; Transform petals; string nearKey; float glowT; Vector3 door;
        public Vector3 Door => door;

        public void Enter()
        {
            HouseCleared = false; nearKey = null;
            var zone = G.Stage("Stage_wilds"); if (!petals) petals = LobbyNight.PetalsAt(zone);
            var d = zone.Find("HouseDoor"); door = d ? d.position : Start;
        }
        void Glow(Vector3 at, Color c) => Fx.Ring(at + Vector3.up * .06f, 2.4f, c, 1.1f, .22f);
        void Use(string key)
        {
            if (key == "gate") G.StartCity();
            else if (key == "door") G.EnterHouse();
            else if (key == "exit") G.LeaveHouse();
            else if (key == "chest") OpenChest();
        }
        public void OpenChest()
        {
            if (HouseCleared) return; HouseCleared = true; nearKey = null; G.ui.SetCityPrompt(null, null);
            Fx.Burst(Chest + Vector3.up, 60, new Color(1, .85f, .4f), 7); G.DropLoot(Chest + new Vector3(0, 0, 2), true, ChestGold); G.ui.Banner(Loc.S("เปิดหีบสมบัติ!", "Treasure!"), "");
        }
        void Update()
        {
            string key = null; string text = null;
            if (G && G.hero && !G.hero.dead && G.mode == Main.Mode.Game && (G.wilds || G.house))
            {
                bool Near(Vector3 at) => Main.Flat(at - G.hero.Pos).magnitude < TalkRange;
                if (Time.time > glowT)
                {
                    glowT = Time.time + GlowEvery;
                    if (G.wilds) { Glow(Gate, new Color(1, .72f, .86f)); Glow(door, new Color(1, .8f, .45f)); }
                    else { Glow(RoomExit, new Color(1, .8f, .45f)); if (G.HouseFought && !HouseCleared) Glow(Chest, new Color(1, .85f, .4f)); }
                }
                if (G.wilds) { if (Near(Gate)) { key = "gate"; text = Loc.S("กลับเมือง", "BACK TO CITY"); } else if (Near(door)) { key = "door"; text = Loc.S("เข้าบ้าน", "ENTER THE HOUSE"); } }
                else if (Near(RoomExit)) { key = "exit"; text = Loc.S("ออกจากบ้าน", "LEAVE THE HOUSE"); }
                else if (G.HouseFought && !HouseCleared && Near(Chest)) { key = "chest"; text = Loc.S("เปิดหีบ", "OPEN THE CHEST"); }
            }
            if (key != nearKey) { nearKey = key; if (G && G.ui) { if (key != null) { var k = key; G.ui.SetCityPrompt(text, () => Use(k)); } else G.ui.SetCityPrompt(null, null); } }
            var kb = Keyboard.current; if (nearKey != null && kb != null && (kb.eKey.wasPressedThisFrame || kb.fKey.wasPressedThisFrame)) Use(nearKey);
        }
        void LateUpdate() { if (G && G.wilds && petals) petals.position = G.hero.Pos + new Vector3(0, 6, -2); }
    }
}
