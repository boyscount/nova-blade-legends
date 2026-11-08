using System.Linq;
using UnityEngine;

namespace NovaBlade
{
    // The companion: follows behind the hero and helps on a cooldown (bolts, bites, heals, beams, webs, fireballs, homing magic).
    public class Pet : MonoBehaviour
    {
        public PetDef def;
        GameObject model; float t, cd = 1, face, y; Foe target; bool biting;
        Main G => Main.G;
        public Vector3 Pos => transform.position;

        public void Equip()
        {
            def = Data.Pets.FirstOrDefault(p => p.id == G.save.pet) ?? Data.Pets[0];
            if (model) Destroy(model);
            model = Instantiate(Catalog.I.Model(def.id), transform);
            WingRig.Wear(gameObject, model.transform, G.save.petWings ? G.WingsShown : null, true);
        }
        // in front of the hero's right foot: the whole pet in frame, clear of the lobby zoom buttons and the hero's wings
        public void PlaceLobby() { bool w = G.WingsShown != null; transform.position = new Vector3(w ? -.75f : -.75f, def.fly ? 1 : 0, w ? .95f : .85f); transform.rotation = Quaternion.Euler(0, 20, 0); }
        public void ResetForGame() { transform.position = new Vector3(1.2f, 0, 7); cd = 1; biting = false; }

        void Update()
        {
            if (!model) return; float dt = Time.deltaTime; t += dt;
            if (G.mode == Main.Mode.Lobby)
            {
                transform.position = new Vector3(transform.position.x, def.fly ? .9f + Mathf.Sin(t * 4) * .1f : 0, transform.position.z);
                float s = 1 + Mathf.Sin(t * 3) * .03f; model.transform.localScale = new Vector3(1 / Mathf.Sqrt(s), s, 1 / Mathf.Sqrt(s)); return;
            }
            var hero = G.hero; cd -= dt;
            float behind = hero.face + Mathf.PI + .7f; var home = hero.Pos + Main.Dir(behind) * 1.5f; var tgt = home; float sp = 7;
            if (biting) { if (!target || !target.alive) biting = false; else { tgt = target.Pos; sp = 14; if (Main.Flat(target.Pos - Pos).magnitude < target.rad + .5f) { target.Damage(Power, Pos, new Main.Hit { col = def.color, kb = 1, steal = def.steal }); biting = false; } } }
            var to = Main.Flat(tgt - Pos); float dd = to.magnitude; bool moving = false;
            if (dd > .15f) { float s = Mathf.Min(dd, sp * dt * (dd > 6 ? 2 : 1)); transform.position += to / dd * s; moving = dd > .4f; face += Main.AngDiff(face, Main.Ang(to)) * Mathf.Min(1, dt * 10); }
            if (G.mode == Main.Mode.Game && cd <= 0 && !hero.dead && !G.sky) Act();   // in a duel the pet only watches
            y = def.fly ? 1.25f + Mathf.Sin(t * 4) * .12f : moving ? Mathf.Abs(Mathf.Sin(t * 14)) * .18f : 0;
            transform.position = new Vector3(transform.position.x, y + (def.fly ? 0 : Mathf.Max(River.Ground(transform.position.x, transform.position.z), River.Wet ? River.WaterY - .1f : River.Sky ? -9 : 0)), transform.position.z); transform.rotation = Quaternion.Euler(moving ? 8 : 0, face * Mathf.Rad2Deg, 0);
            float sq = def.fly ? 1 + Mathf.Sin(t * 16) * .05f : 1 + (moving ? Mathf.Sin(t * 28) * .06f : Mathf.Sin(t * 3) * .03f);
            model.transform.localScale = new Vector3(1 / Mathf.Sqrt(sq), sq, 1 / Mathf.Sqrt(sq));
        }
        float Power => def.dmg * (1 + (G.stage - 1) * .3f);

        void Act()
        {
            var hero = G.hero; var src = Pos + Vector3.up * def.h * .6f;
            if (def.kind == "heal")
            {
                cd = def.cd; if (hero.hp < hero.max) { hero.Heal(hero.max * .06f); Fx.Burst(hero.Pos + Vector3.up, 18, def.color, 3, .7f, -.4f); Fx.Ring(hero.Pos, 1.5f, def.color, .5f); }
                return;
            }
            var e = G.Nearest(Pos, 9); if (!e) return;
            cd = def.cd; face = Main.Ang(e.Pos - Pos); var dir = Main.Flat(e.Pos - Pos).normalized; float l = Main.Flat(e.Pos - Pos).magnitude;
            switch (def.kind)
            {
                case "bolt": Shot.Fire(src, dir * 14, Power, def.color, "orb", 1, trail: true); break;
                case "melee": biting = true; target = e; break;
                case "beam": e.Damage(Power, Pos, new Main.Hit { col = def.color }); Fx.Beam(src, e.Pos + Vector3.up * e.h * .5f, def.color, .18f, .05f); break;
                case "web": Shot.Fire(src, dir * 11, Power, def.color, "orb", 1.2f, aoe: 2.2f, slow: 3); break;
                case "fireball": { float tt = Mathf.Max(.5f, l / 9); Shot.Fire(src, dir * (l / tt) + Vector3.up * 6, Power, def.color, "big", 3, aoe: 2.6f, grav: 12, trail: true, r: .5f); break; }
                case "homing": for (int i = 0; i < 3; i++) { float a = face + (i - 1) * .9f; Shot.Fire(src, Main.Dir(a) * 8, Power, def.color, "orb", 2, homing: 7, trail: true); } break;
            }
        }
    }
}
