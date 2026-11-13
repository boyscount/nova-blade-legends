using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // Getting about a lane battle quickly (owner, 2026-11-12):
    // Recall (B): stand still for 6 s on a turning blue rune with light rising, then a flash and the hero is at its fountain.
    // Tower warp (T): from the base only; pick one of your standing towers; 4 s on a gold rune while a second gold rune marks the tower
    // (the enemy sees where you are coming), then a shock ring there. 90 s between warps. Moving or being hit stops either.
    public partial class Hero
    {
        public const float RecallTime = 6, WarpTime = 4, WarpEvery = 90;
        public string chanKind; public float chanT, warpCd; Foe chanTower; float chanFx;
        public float ChanShare => chanKind == null ? 0 : Mathf.Clamp01(chanT / (chanKind == "recall" ? RecallTime : WarpTime));
        public bool InBase => G.lane && Main.Flat(Pos - Lane.HealAt(0)).magnitude < (Lane.Lanes == 2 ? SkyLanes.BaseR + 1.5f : 12);
        public Foe[] WarpTowers => G.foes.Where(f => f && f.alive && f.team == 0 && f.d.id == "tower").OrderBy(f => f.laneIdx).ThenBy(f => f.towerTier).ToArray();
        public bool StartRecall()
        {
            if (!G.lane || dead || chanKind != null || InBase) return false;
            chanKind = "recall"; chanT = 0; chanFx = 0; G.Number(Pos + Vector3.up * 2.4f, Loc.S("กำลังกลับบ้าน…", "Recalling…"), "crit"); return true;
        }
        public bool StartWarp(Foe tower)
        {
            if (!G.lane || dead || chanKind != null || !InBase || warpCd > 0 || !tower || !tower.alive || tower.team != 0) return false;
            chanKind = "warp"; chanTower = tower; chanT = 0; chanFx = 0; G.Number(Pos + Vector3.up * 2.4f, Loc.S("กำลังวาป…", "Warping…"), "crit"); return true;
        }
        public void CancelChannel(bool quiet = false)
        {
            if (chanKind == null) return; chanKind = null; chanTower = null;
            if (!quiet) G.Number(Pos + Vector3.up * 2.4f, Loc.S("ยกเลิก", "Cancelled"), "hurt");
        }
        void TickChannel(float dt)
        {
            if (warpCd > 0) warpCd -= dt;
            if (chanKind == null) return;
            if (dead || !G.lane || G.mode != Main.Mode.Game || Move().magnitude > .2f || state == St.Skill || state == St.Dodge || chanKind == "warp" && (!chanTower || !chanTower.alive)) { CancelChannel(); return; }
            chanT += dt; chanFx -= dt; bool recall = chanKind == "recall"; var col = recall ? new Color(.35f, .75f, 1f) : new Color(1f, .8f, .35f);
            if (chanFx <= 0)
            {   // the rune turns under the hero and light climbs it; the warp's second rune glows at the tower
                chanFx = .3f; float k = ChanShare;
                Fx.Ring(Pos + Vector3.up * .05f, 1.4f + .3f * Mathf.Sin(chanT * 4), col, .35f, .08f);
                Fx.Burst(Pos + Vector3.up * (.2f + k * 1.6f), 6 + (int)(k * 10), col, 1.5f + k * 2, .5f, -1.2f, .1f, true);
                if (!recall && chanTower) Fx.Ring(WarpSpot(chanTower) + Vector3.up * .05f, 1.8f, col, .35f, .1f);
            }
            float need = recall ? RecallTime : WarpTime; if (chanT < need) return;
            var from = Pos; var to = recall ? Lane.HealAt(0) : WarpSpot(chanTower);
            Fx.Burst(from + Vector3.up, 40, col, 6, .6f, -.8f, .14f, true);
            transform.position = G.Clamp(to); StopWalk(); G.pet.transform.position = transform.position + new Vector3(1, 0, 1);
            Fx.Ring(Pos, recall ? 3 : 4, col, .6f, .25f); Fx.Burst(Pos + Vector3.up, 60, col, 7, .7f, -.6f, .16f, true); G.shake = Mathf.Max(G.shake, recall ? .1f : .25f);
            if (!recall) warpCd = WarpEvery;
            chanKind = null; chanTower = null;
        }
        // beside the tower on our own side of it
        public static Vector3 WarpSpot(Foe tower) { var p = tower.Pos; return p + new Vector3(0, 0, Lane.Side(0) * (tower.rad + 1.8f)); }
    }
}
