using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // Recall and tower warp on the HUD (HeroTravel): two small buttons under the minimap (B and T on a keyboard), a bar under the middle
    // of the screen while one is under way, and for the warp a short list of your standing towers to pick from.
    public partial class Ui
    {
        VisualElement travelRow, recallBtn, warpBtn, chanBox, chanFill, warpPick; Label chanL, warpCdL;
        void BuildTravel()
        {
            float big = touchUi ? 1.15f : 1, mapSize = touchUi ? 210 : 190, top = (touchUi ? 130 : 122) + mapSize + 10;
            travelRow = Box(); Abs(travelRow, null, top, 34); travelRow.style.width = mapSize; travelRow.style.flexDirection = FlexDirection.Row; travelRow.style.justifyContent = Justify.SpaceBetween; hud.Add(travelRow);
            VisualElement TBtn(string text, string key, Color edge, System.Action<MeshGenerationContext> icon, System.Action act, out Label sub)
            {
                var b = Box(new Color(.08f, .1f, .18f, .9f), 12); Border(b, edge, 2); b.style.width = mapSize / 2 - 4; b.style.alignItems = Align.Center; b.style.paddingTop = b.style.paddingBottom = 5; b.pickingMode = PickingMode.Position;
                var r = Box(); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center; r.pickingMode = PickingMode.Ignore; b.Add(r);
                var ic = Box(); ic.style.width = ic.style.height = 24 * big; ic.style.marginRight = 5; ic.generateVisualContent += icon; ic.pickingMode = PickingMode.Ignore; r.Add(ic);
                var l = L(text, 16 * big, Color.white, true); l.pickingMode = PickingMode.Ignore; r.Add(l);
                sub = L(touchUi ? "" : key, 12, Gold, true); sub.pickingMode = PickingMode.Ignore; b.Add(sub);
                b.RegisterCallback<PointerDownEvent>(e => { act(); e.StopPropagation(); }); travelRow.Add(b); return b;
            }
            recallBtn = TBtn(Loc.S("กลับบ้าน", "Recall"), "B", new Color(.5f, .8f, 1f), ctx => HomeIcon(ctx, new Color(.6f, .85f, 1f)), () => G.hero?.StartRecall(), out _);
            warpBtn = TBtn(Loc.S("วาปป้อม", "Warp"), "T", Gold, ctx => DrawCastle(ctx, new Color(1f, .85f, .5f), new Color(.08f, .1f, .18f)), ToggleWarpPick, out warpCdL);
            chanBox = Box(new Color(.05f, .07f, .14f, .85f), 10); chanBox.style.position = Position.Absolute; chanBox.style.left = Length.Percent(50); chanBox.style.marginLeft = -160; chanBox.style.width = 320; chanBox.style.bottom = Length.Percent(30); Pad(chanBox, 6); chanBox.pickingMode = PickingMode.Ignore; hud.Add(chanBox);
            chanL = L("", 18 * big, Color.white, true); chanL.style.unityTextAlign = TextAnchor.MiddleCenter; chanBox.Add(chanL);
            var tr = Box(new Color(.2f, .2f, .3f, 1), 4); tr.style.height = 8; tr.style.marginTop = 4; chanBox.Add(tr); chanFill = Box(new Color(.5f, .8f, 1f), 4); chanFill.style.height = 8; tr.Add(chanFill);
            chanBox.style.display = DisplayStyle.None;
            warpPick = Box(new Color(.06f, .08f, .16f, .95f), 12); Border(warpPick, Gold, 2); Abs(warpPick, null, top + 70 * big, 34); warpPick.style.width = mapSize; Pad(warpPick, 8); warpPick.style.display = DisplayStyle.None; hud.Add(warpPick);
        }
        static void HomeIcon(MeshGenerationContext ctx, Color c)
        {   // a little house
            var r = ctx.visualElement.contentRect; var p = ctx.painter2D; float w = r.width, h = r.height; Vector2 P(float x, float y) => new(w * x, h * y); p.fillColor = c;
            p.BeginPath(); p.MoveTo(P(.5f, .08f)); p.LineTo(P(.95f, .48f)); p.LineTo(P(.82f, .48f)); p.LineTo(P(.82f, .92f)); p.LineTo(P(.18f, .92f)); p.LineTo(P(.18f, .48f)); p.LineTo(P(.05f, .48f)); p.ClosePath(); p.Fill();
            p.fillColor = new Color(.08f, .1f, .18f); p.BeginPath(); p.MoveTo(P(.42f, .92f)); p.LineTo(P(.42f, .66f)); p.LineTo(P(.58f, .66f)); p.LineTo(P(.58f, .92f)); p.ClosePath(); p.Fill();
        }
        public void TestWarpPick() => ToggleWarpPick();
        void ToggleWarpPick()
        {
            var h = G.hero; if (h == null || warpPick == null) return;
            if (warpPick.style.display == DisplayStyle.Flex) { warpPick.style.display = DisplayStyle.None; return; }
            if (!h.InBase) { Banner(Loc.S("วาปป้อมได้จากฐานเท่านั้น", "Warp only from your base"), Loc.S("กด \"กลับบ้าน\" ก่อน", "Recall first")); return; }
            if (h.warpCd > 0) { Banner(Loc.S($"วาปได้อีกใน {Mathf.CeilToInt(h.warpCd)} วิ", $"Warp ready in {Mathf.CeilToInt(h.warpCd)} s")); return; }
            warpPick.Clear(); warpPick.Add(L(Loc.S("เลือกป้อมที่จะวาปไป", "Warp to which tower?"), 15, Gold, true)); float big = touchUi ? 1.15f : 1;
            foreach (var t in h.WarpTowers)
            {
                var tt = t; string lane = Lane.Lanes == 2 ? (t.laneIdx == 0 ? Loc.S("เลนตรง", "Straight") : Loc.S("เลนโค้ง", "Curve")) : Loc.S($"เลน {t.laneIdx + 1}", $"Lane {t.laneIdx + 1}");
                var b = Box(new Color(.15f, .14f, .26f, 1), 8); b.style.flexDirection = FlexDirection.Row; b.style.justifyContent = Justify.SpaceBetween; b.style.marginTop = 4; b.style.paddingLeft = b.style.paddingRight = 8; b.style.paddingTop = b.style.paddingBottom = 4; b.pickingMode = PickingMode.Position;
                var nl = L(Loc.S($"{lane} · ป้อม {t.towerTier + 1}", $"{lane} · tower {t.towerTier + 1}"), 15 * big, Color.white, true); nl.pickingMode = PickingMode.Ignore; b.Add(nl);
                var hl = L($"{t.hp / t.maxHp * 100:0}%", 15 * big, t.hp < t.maxHp * .4f ? new Color(1f, .5f, .45f) : new Color(.6f, 1f, .6f), true); hl.pickingMode = PickingMode.Ignore; b.Add(hl);
                b.RegisterCallback<PointerDownEvent>(e => { warpPick.style.display = DisplayStyle.None; G.hero?.StartWarp(tt); e.StopPropagation(); }); warpPick.Add(b);
            }
            warpPick.style.display = DisplayStyle.Flex;
        }
        void UpdTravel()
        {
            if (travelRow == null) { if (hud != null) BuildTravel(); else return; }
            var h = G ? G.hero : null; bool on = G && G.lane && G.mode == Main.Mode.Game && h != null;
            var d = on ? DisplayStyle.Flex : DisplayStyle.None; if (travelRow.style.display != d) travelRow.style.display = d;
            if (!on) { chanBox.style.display = DisplayStyle.None; warpPick.style.display = DisplayStyle.None; return; }
            var kb = Keyboard.current;
            if (kb != null && !PerksOpen) { if (kb.bKey.wasPressedThisFrame) h.StartRecall(); if (kb.tKey.wasPressedThisFrame) ToggleWarpPick(); }
            warpBtn.style.opacity = h.InBase && h.warpCd <= 0 ? 1 : .5f; recallBtn.style.opacity = h.InBase ? .5f : 1;
            warpCdL.text = h.warpCd > 0 ? $"{Mathf.CeilToInt(h.warpCd)}s" : touchUi ? "" : "T";
            if (h.chanKind != null)
            {
                chanBox.style.display = DisplayStyle.Flex; bool rc = h.chanKind == "recall";
                chanL.text = rc ? Loc.S("กำลังกลับบ้าน… (ขยับ = ยกเลิก)", "Recalling… (move to cancel)") : Loc.S("กำลังวาปไปป้อม… (ขยับ = ยกเลิก)", "Warping… (move to cancel)");
                chanFill.style.width = Length.Percent(h.ChanShare * 100); chanFill.style.backgroundColor = rc ? new Color(.5f, .8f, 1f) : Gold;
            }
            else chanBox.style.display = DisplayStyle.None;
            if (warpPick.style.display == DisplayStyle.Flex && !h.InBase) warpPick.style.display = DisplayStyle.None;
        }
    }
}
