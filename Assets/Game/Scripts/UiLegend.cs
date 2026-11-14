using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NovaBlade
{
    // Hero legends on screen (Legends.cs): the story dialogue (the speaker's card, name and line; tap or Space for the next line), the
    // legend picker opened from the lobby (chapters, normal / legend difficulty, the reward) and the result after each chapter.
    public partial class Ui
    {
        // ---------- dialogue ----------
        VisualElement dlgOv, dlgCard; Label dlgName, dlgText, dlgHint; Legends.Line[] dlgLines; int dlgAt; System.Action dlgDone; float dlgShown;
        public bool DialogueOpen => dlgOv != null && dlgOv.style.display == DisplayStyle.Flex;
        static readonly Color DlgInk = new(1f, .86f, .55f);
        void BuildDialogue()
        {
            dlgOv = Box(new Color(0, 0, 0, .35f)); Abs(dlgOv, 0, 0, 0, 0); dlgOv.pickingMode = PickingMode.Position; root.Add(dlgOv);
            var box = Box(new Color(.05f, .05f, .1f, .94f), 18); Border(box, new Color(1f, .74f, .35f, .85f), 3); Abs(box, null, null, null, touchUi ? 26 : 34);
            box.style.left = Length.Percent(touchUi ? 4 : 12); box.style.right = Length.Percent(touchUi ? 4 : 12); box.style.minHeight = touchUi ? 230 : 190;
            box.style.flexDirection = FlexDirection.Row; box.style.alignItems = Align.Center; Pad(box, touchUi ? 18 : 16); box.style.paddingLeft = touchUi ? 210 : 190; dlgOv.Add(box);
            dlgCard = Box(new Color(.1f, .08f, .14f), 14); Border(dlgCard, new Color(1f, .8f, .45f), 3); Abs(dlgCard, touchUi ? 18 : 18, null, null, touchUi ? 18 : 16);
            dlgCard.style.width = touchUi ? 172 : 156; dlgCard.style.height = touchUi ? 250 : 226; dlgCard.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; box.Add(dlgCard);
            var col = Box(); col.style.flexGrow = 1; col.style.flexShrink = 1; box.Add(col);
            dlgName = L("", touchUi ? 28 : 24, DlgInk, true); Shadow(dlgName, 4); col.Add(dlgName);
            dlgText = L("", touchUi ? 27 : 23, Color.white); dlgText.style.whiteSpace = WhiteSpace.Normal; dlgText.style.marginTop = 8; col.Add(dlgText);
            dlgHint = L(touchUi ? Loc.S("แตะเพื่อไปต่อ ▶", "Tap to go on ▶") : Loc.S("คลิก หรือ Space เพื่อไปต่อ ▶", "Click or Space to go on ▶"), touchUi ? 18 : 15, new Color(1, 1, 1, .6f)); dlgHint.style.alignSelf = Align.FlexEnd; dlgHint.style.marginTop = 10; col.Add(dlgHint);
            var skip = L(Loc.S("ข้าม ▶▶", "Skip ▶▶"), touchUi ? 22 : 18, new Color(1, 1, 1, .75f), true); Abs(skip, null, touchUi ? 24 : 20, touchUi ? 30 : 34); skip.pickingMode = PickingMode.Position;
            skip.RegisterCallback<PointerUpEvent>(e => { e.StopPropagation(); CloseDialogue(); }); dlgOv.Add(skip);
            dlgOv.RegisterCallback<PointerUpEvent>(_ => NextLine());
            dlgOv.schedule.Execute(() =>
            {
                if (!DialogueOpen) return; var kb = Keyboard.current;
                if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) NextLine();
                if (kb != null && kb.escapeKey.wasPressedThisFrame) CloseDialogue();
            }).Every(16);
            dlgOv.style.display = DisplayStyle.None;
        }
        public void Dialogue(Legends.Line[] lines, System.Action done)
        {
            if (dlgOv == null) BuildDialogue();
            if (lines == null || lines.Length == 0) { done?.Invoke(); return; }
            dlgLines = lines; dlgAt = -1; dlgDone = done; dlgOv.style.display = DisplayStyle.Flex; dlgOv.BringToFront(); NextLine(true);
        }
        void NextLine(bool first = false)
        {
            if (!first && Time.unscaledTime - dlgShown < .25f) return;   // the tap that opened it, or a double tap, does not skip a line
            dlgAt++; if (dlgLines == null || dlgAt >= dlgLines.Length) { CloseDialogue(); return; }
            if (!first) Sfx.Play("ui_dialogue", .7f);
            var ln = dlgLines[dlgAt]; var h = System.Array.Find(Data.Heroes, x => x.id == ln.who);
            dlgName.text = h != null ? h.name : ln.who; dlgText.text = ln.Text; dlgShown = Time.unscaledTime;
            var tex = Resources.Load<Texture2D>("UI/Cards/hero_" + ln.who);
            dlgCard.style.backgroundImage = tex ? new StyleBackground(tex) : new StyleBackground((Texture2D)null);
            Border(dlgCard, ln.who == G.hero?.def.id ? new Color(1f, .8f, .45f) : new Color(.6f, .75f, 1f), 3);
        }
        void CloseDialogue()
        {
            if (dlgOv == null || !DialogueOpen) return; dlgOv.style.display = DisplayStyle.None; var d = dlgDone; dlgDone = null; dlgLines = null; d?.Invoke();
        }
        public void TestDialogueNext() { dlgShown = -1; NextLine(); }
        public string TestDialogueText => DialogueOpen ? $"{dlgName.text}: {dlgText.text}" : null;

        // ---------- the legend picker (from the lobby, beside the story banner) ----------
        VisualElement legOv, legList, legCard, legHeroes; Label legDiffN, legDiffH, legReward, legName; bool legHard; int legSel;
        Legends.Legend LegSel => Legends.All[Mathf.Clamp(legSel, 0, Legends.All.Length - 1)];
        public void OpenLegends()
        {
            if (legOv == null) BuildLegends();
            int mine = System.Array.FindIndex(Legends.All, x => x.hero == G.save.hero); if (mine >= 0) legSel = mine;   // the hero in play first
            var l = LegSel; if (legHard && Legends.Cleared(G.save, l, false) < l.steps.Length) legHard = false;
            RenderLegends(); legOv.style.display = DisplayStyle.Flex; legOv.BringToFront(); Sfx.Play("ui_open");
        }
        public void CloseLegends() { if (legOv != null) legOv.style.display = DisplayStyle.None; }
        public bool LegendsOpen => legOv != null && legOv.style.display == DisplayStyle.Flex;
        void BuildLegends()
        {
            legOv = Box(new Color(.01f, .01f, .03f, .78f)); Abs(legOv, 0, 0, 0, 0); legOv.pickingMode = PickingMode.Position; root.Add(legOv);
            legOv.style.alignItems = Align.Center; legOv.style.justifyContent = Justify.Center;
            var p = Box(new Color(.08f, .06f, .14f, .97f), 22); Border(p, new Color(1f, .74f, .35f, .9f), 3); p.style.width = Length.Percent(touchUi ? 94 : 72); p.style.maxWidth = 1200;
            p.style.maxHeight = Length.Percent(94); Pad(p, touchUi ? 22 : 24); legOv.Add(p);
            p.style.backgroundImage = new StyleBackground(Grad(new Color(.2f, .1f, .08f, .97f), new Color(.06f, .05f, .12f, .97f), true));
            var head = Box(); head.style.flexDirection = FlexDirection.Row; head.style.alignItems = Align.Center; p.Add(head);
            var t = L(Loc.S("ตำนานฮีโร่", "HERO LEGENDS"), touchUi ? 40 : 36, DlgInk, true); Shadow(t, 6); t.style.flexGrow = 1; head.Add(t);
            var x = L("X", touchUi ? 34 : 28, Color.white, true); x.pickingMode = PickingMode.Position; x.style.paddingLeft = x.style.paddingRight = 12; x.RegisterCallback<PointerUpEvent>(_ => CloseLegends()); head.Add(x);
            var sub = L(Loc.S("เล่นเรื่องของฮีโร่ทีละบท ปลดสกิลทีละตัว จบด้วยบอส", "Play a hero's story chapter by chapter: one skill a chapter, a boss at the end"), touchUi ? 19 : 16, Muted); sub.style.whiteSpace = WhiteSpace.Normal; sub.style.marginTop = 4; p.Add(sub);

            legHeroes = Box(); legHeroes.style.flexDirection = FlexDirection.Row; legHeroes.style.marginTop = 12; legHeroes.style.flexWrap = Wrap.Wrap; p.Add(legHeroes);
            for (int i = 0; i < Legends.All.Length; i++)
            {   // one tab a legend: the hero's name
                int k = i; var lg = Legends.All[i]; var h = System.Array.Find(Data.Heroes, x => x.id == lg.hero);
                var b = L(h != null ? h.name : lg.hero, touchUi ? 22 : 19, Color.white, true); b.pickingMode = PickingMode.Position; Pad(b, 8); b.style.paddingLeft = b.style.paddingRight = 18; Round(b, 10); b.style.marginRight = 8;
                b.RegisterCallback<PointerUpEvent>(_ => { legSel = k; if (legHard && Legends.Cleared(G.save, LegSel, false) < LegSel.steps.Length) legHard = false; RenderLegends(); }); legHeroes.Add(b);
            }
            var body = Box(); body.style.flexDirection = FlexDirection.Row; body.style.marginTop = 14; body.style.flexShrink = 1; p.Add(body);
            var card = Box(new Color(.1f, .08f, .14f), 16); Border(card, new Color(1f, .8f, .45f), 3); card.style.width = touchUi ? 210 : 230; card.style.height = touchUi ? 300 : 330; card.style.flexShrink = 0;
            card.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop; body.Add(card); legCard = card;
            var right = Box(); right.style.flexGrow = 1; right.style.flexShrink = 1; right.style.marginLeft = 18; body.Add(right);
            var nm = L("", touchUi ? 30 : 28, Color.white, true); nm.style.whiteSpace = WhiteSpace.Normal; right.Add(nm); legName = nm;
            var diff = Box(); diff.style.flexDirection = FlexDirection.Row; diff.style.marginTop = 10; right.Add(diff);
            Label Tab(string text, bool hard)
            {
                var b = L(text, touchUi ? 21 : 18, Color.white, true); b.pickingMode = PickingMode.Position; Pad(b, 8); b.style.paddingLeft = b.style.paddingRight = 16; Round(b, 10); b.style.marginRight = 8;
                b.RegisterCallback<PointerUpEvent>(_ => { if (hard && Legends.Cleared(G.save, LegSel, false) < LegSel.steps.Length) { Banner(Loc.S("ระดับตำนานยังล็อก", "Legend difficulty is locked"), Loc.S("จบระดับปกติให้ครบ 4 บทก่อน", "Finish all 4 chapters on normal first")); return; } legHard = hard; RenderLegends(); });
                diff.Add(b); return b;
            }
            legDiffN = Tab(Loc.S("ปกติ", "Normal"), false); legDiffH = Tab(Loc.S("ตำนาน (ยาก)", "Legend (hard)"), true);
            legList = Box(); legList.style.marginTop = 10; right.Add(legList);
            legReward = L("", touchUi ? 18 : 16, DlgInk); legReward.style.whiteSpace = WhiteSpace.Normal; legReward.style.marginTop = 10; right.Add(legReward);
            var soon = L(Loc.S("ตำนานของฮีโร่ตัวอื่นจะตามมา", "More heroes' legends are on the way"), touchUi ? 17 : 15, Muted); soon.style.marginTop = 12; soon.style.whiteSpace = WhiteSpace.Normal; p.Add(soon);
            legOv.style.display = DisplayStyle.None;
        }
        void RenderLegends()
        {
            var l = LegSel; int done = Legends.Cleared(G.save, l, legHard);
            var hd = System.Array.Find(Data.Heroes, x => x.id == l.hero); legName.text = (hd != null ? hd.name + " · " : "") + l.Name;
            var ct = Resources.Load<Texture2D>("UI/Cards/herofull_" + l.hero) ?? Resources.Load<Texture2D>("UI/Cards/hero_" + l.hero); legCard.style.backgroundImage = ct ? new StyleBackground(ct) : new StyleBackground((Texture2D)null);
            for (int i = 0; i < legHeroes.childCount; i++) { var b = legHeroes[i]; bool on = i == legSel; b.style.backgroundColor = on ? new Color(.45f, .22f, .08f) : new Color(1, 1, 1, .06f); Border(b, on ? DlgInk : new Color(1, 1, 1, .2f), 2); }
            foreach (var (b, on) in new[] { (legDiffN, !legHard), (legDiffH, legHard) })
            { b.style.backgroundColor = on ? (legHard ? new Color(.55f, .12f, .12f) : new Color(.7f, .42f, .1f)) : new Color(1, 1, 1, .08f); Border(b, on ? DlgInk : new Color(1, 1, 1, .2f), 2); }
            bool hardLocked = Legends.Cleared(G.save, l, false) < l.steps.Length; legDiffH.style.opacity = hardLocked ? .5f : 1; if (hardLocked) legDiffH.text = Loc.S("ตำนาน (ยาก) · ล็อก", "Legend (hard) · locked"); else legDiffH.text = Loc.S("ตำนาน (ยาก)", "Legend (hard)");
            legList.Clear();
            for (int i = 0; i < l.steps.Length; i++)
            {
                var s = l.steps[i]; bool clear = i < done, open = Legends.Open(G.save, l, legHard, i); int k = i;
                var r = Box(new Color(1, 1, 1, open ? .07f : .03f), 12); Border(r, clear ? new Color(.45f, .9f, .5f, .7f) : open ? DlgInk : new Color(1, 1, 1, .12f), 2); r.style.flexDirection = FlexDirection.Row; r.style.alignItems = Align.Center;
                Pad(r, touchUi ? 12 : 10); r.style.marginBottom = 8; r.pickingMode = PickingMode.Position; legList.Add(r);
                var n = L($"{i + 1}", touchUi ? 28 : 24, open ? DlgInk : Muted, true); n.style.width = 36; n.style.unityTextAlign = TextAnchor.MiddleCenter; r.Add(n);
                var tx = Box(); tx.style.flexGrow = 1; tx.style.flexShrink = 1; r.Add(tx);
                var tt = L(s.Title, touchUi ? 23 : 20, open ? Color.white : Muted, true); tx.Add(tt);
                string sk = s.skills == 0 ? Loc.S("ตีปกติเท่านั้น", "plain attacks only") : Loc.S($"สกิล {s.skills} ตัว", $"{s.skills} skill{(s.skills > 1 ? "s" : "")}");
                var info = L((s.boss != null ? Loc.S("บอส: ", "Boss: ") + s.boss.name + " · " : "") + sk, touchUi ? 17 : 15, Muted); info.style.whiteSpace = WhiteSpace.Normal; tx.Add(info);
                var st = L(clear ? Loc.S("ผ่าน", "Clear") : open ? "▶" : Loc.S("ล็อก", "Locked"), touchUi ? 28 : 24, clear ? new Color(.5f, 1f, .55f) : open ? DlgInk : Muted, true); st.style.marginLeft = 8; r.Add(st);
                if (open) r.RegisterCallback<PointerUpEvent>(_ => { CloseLegends(); G.StartLegend(l, k, legHard); });
            }
            legReward.text = (legHard ? Loc.S("รางวัลเมื่อจบระดับตำนาน: ", "Legend reward: ") + Loc.S(l.rewardTh, l.rewardEn) : Loc.S($"ทองบทละ {Legends.StepGold} · จบครบ 4 บทเพื่อเปิดระดับตำนาน", $"{Legends.StepGold} gold a chapter · finish all 4 to open Legend difficulty"));
        }
        public void TestOpenLegends(bool hard, int pick = -1) { OpenLegends(); if (pick >= 0) legSel = pick; legHard = hard; RenderLegends(); }

        // ---------- the result of a chapter ----------
        void ShowLegendResult(bool win)
        {
            resultOv.style.display = DisplayStyle.Flex; resX.text = "+" + G.runXp;
            var l = G.legend; int st = G.legendStep; bool last = st + 1 >= l.steps.Length, hard = G.legendHardOn;
            resH.text = win ? (last ? Loc.S("จบตำนาน!", "Legend Complete!") : Loc.S($"ผ่านบทที่ {st + 1}!", $"Chapter {st + 1} Clear!")) : Loc.S("พ่ายแพ้…", "Defeated…"); resH.style.color = win ? Gold : Red;
            if (!win) resS.text = Loc.S("ลองใหม่อีกครั้ง: ดูท่าบอสแล้วหลบก่อนตี", "Try again: watch the boss's moves and dodge before you strike");
            else if (!last) resS.text = Loc.S($"ได้ทอง {(hard ? Legends.HardStepGold : Legends.StepGold)} · ต่อไป: บทที่ {st + 2} · {l.steps[st + 1].th}", $"+{(hard ? Legends.HardStepGold : Legends.StepGold)} gold · Next: chapter {st + 2} · {l.steps[st + 1].en}");
            else resS.text = hard ? Loc.S("ได้รับ: ", "Earned: ") + Loc.S(l.rewardTh, l.rewardEn) : Loc.S("ระดับตำนานเปิดแล้ว! ชนะครบเพื่อรับ ", "Legend difficulty is open! Clear it for ") + Loc.S(l.rewardTh, l.rewardEn);
            resK.text = G.kills.ToString(); resG.text = G.gold.ToString(); int s = Mathf.RoundToInt(G.time); resT.text = $"{s / 60}:{s % 60:00}";
            again.text = win ? (last ? Loc.S("ตำนานฮีโร่", "Hero Legends") : Loc.S("บทต่อไป ▶", "Next Chapter ▶")) : Loc.S("ลองอีกครั้ง", "Try Again");
            if (againAct != null) again.clicked -= againAct;
            againAct = win ? (last ? () => { G.ShowLobby(); OpenLegends(); } : () => G.StartLegend(l, st + 1, hard)) : () => G.StartLegend(l, st, hard);
            again.clicked += againAct;
        }

        // the lobby button beside the story banner
        void LegendButton(VisualElement row)
        {
            var b = Box(new Color(.24f, .1f, .06f, .95f), 14); Border(b, DlgInk, 2); b.style.flexShrink = 0; b.style.marginLeft = 10; b.style.minHeight = touchUi ? 100 : 74; b.style.width = touchUi ? 150 : 124;
            b.style.alignItems = Align.Center; b.style.justifyContent = Justify.Center; b.pickingMode = PickingMode.Position; Pad(b, 6);
            b.style.backgroundImage = new StyleBackground(Grad(new Color(.5f, .2f, .06f, .96f), new Color(.2f, .07f, .04f, .96f), true));
            var ic = L("◆", touchUi ? 30 : 24, DlgInk, true); b.Add(ic);
            var t = L(Loc.S("ตำนาน", "LEGENDS"), touchUi ? 22 : 17, Color.white, true); Shadow(t, 4); b.Add(t);
            var s = L(Loc.S("ฮีโร่", "of heroes"), touchUi ? 15 : 12, DlgInk); b.Add(s);
            b.RegisterCallback<PointerDownEvent>(e => b.style.scale = new Scale(Vector3.one * .95f));
            b.RegisterCallback<PointerLeaveEvent>(e => b.style.scale = new Scale(Vector3.one));
            b.RegisterCallback<PointerUpEvent>(e => { b.style.scale = new Scale(Vector3.one); OpenLegends(); });
            row.Add(b);
        }
    }
}
