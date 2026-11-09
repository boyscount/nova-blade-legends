"""Icons for the MOBA lane kits (MOBA-SKILLS.md) that had no matching picture: same painted style as gen.py.
Writes kit_<hero>_<slot>.svg (slot 0..2 = Q W E); the game loads Resources/UI/Skills/kit_<hero>_<slot>.png in lane battles,
and falls back to the hero's old skill icon (KitIcons in HeroKit.cs) for the slots that reuse one."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from gen import *

OUT = sys.argv[1] if len(sys.argv) > 1 else "out_kits"

def chain(x0, y0, x1, y1, n=6):
    out = ''
    for i in range(n):
        t = (i + .5) / n; x = x0 + (x1 - x0) * t; y = y0 + (y1 - y0) * t
        a = math.degrees(math.atan2(y1 - y0, x1 - x0)) + (90 if i % 2 else 0)
        out += T(x, y, a, 1, '<ellipse class="ml" cx="0" cy="0" rx="12" ry="7" style="stroke-width:6"/>')
    return out

def crescent(x, y, s=1, a=0):
    return T(x, y, a, s, '<path class="m" d="M0,-60 A60,60 0 1 0 0,60 A34,60 0 1 1 0,-60 Z"/>')

def star(x, y, r, cls='m'):
    pts = []
    for i in range(10):
        rr = r if i % 2 == 0 else r * .45; ang = math.radians(-90 + 36 * i)
        pts.append(f'{x + rr*math.cos(ang):.1f},{y + rr*math.sin(ang):.1f}')
    return f'<polygon class="{cls}" points="{" ".join(pts)}"/>'

def note(x, y, s=1):
    return T(x, y, 0, s, '<ellipse class="m" cx="-14" cy="40" rx="20" ry="15" transform="rotate(-20 -14 40)"/>'
                          '<rect class="m" x="0" y="-56" width="10" height="96" rx="4"/>'
                          '<path class="m" d="M5,-56 C40,-44 44,-20 30,-4 C34,-24 22,-34 5,-36 Z"/>')

def skull(x, y, s=1):
    return T(x, y, 0, s, '<path class="m" d="M0,-52 C34,-52 50,-28 50,0 C50,18 40,28 30,32 L30,48 L-30,48 L-30,32 C-40,28 -50,18 -50,0 C-50,-28 -34,-52 0,-52 Z"/>'
                          '<circle class="d" cx="-19" cy="2" r="13"/><circle class="d" cx="19" cy="2" r="13"/>'
                          '<path class="d" d="M0,16 L-8,30 L8,30 Z"/>'
                          '<path class="dl" d="M-14,40 L-14,48 M0,40 L0,48 M14,40 L14,48" style="stroke-width:4"/>')

def trap(x, y, s=1):
    teeth = ''.join(f'<path class="m" d="M{-48 + 16*i},0 L{-40 + 16*i},-22 L{-32 + 16*i},0 Z"/>' for i in range(6))
    teeth2 = ''.join(f'<path class="m" d="M{-48 + 16*i},0 L{-40 + 16*i},22 L{-32 + 16*i},0 Z"/>' for i in range(6))
    return T(x, y, 0, s, '<path class="ml" d="M-60,-6 A60,40 0 0 1 60,-6" style="stroke-width:10"/>'
                          '<path class="ml" d="M-60,6 A60,40 0 0 0 60,6" style="stroke-width:10"/>' + teeth + teeth2 +
             '<circle class="m" cx="0" cy="0" r="10"/>')

def leg_kick(x, y, a=0, s=1):
    return T(x, y, a, s, '<path class="m" d="M-30,60 L-14,10 L30,-50 L46,-40 L6,18 L-6,64 Z"/>'
                          '<path class="m" d="M30,-50 L56,-62 L62,-48 L44,-38 Z"/>')

# --- the new icons (hero palette as in gen.py) ---
def k_spartan_0():   # Shield Bash: a shield rushing forward
    return speed_lines([(30, 100, 80, 100), (24, 128, 76, 128), (30, 156, 80, 156)], 7) + kite_shield(150, 128, 0, .95) + burst(150, 128, 92, 108, 8, 5)
def k_spartan_1():   # Spear Throw: a spear flying flat
    return speed_lines([(26, 160, 80, 140), (40, 186, 90, 166)], 6) + arrow(128, 128, 55, 1.15)
def k_spartan_2():   # Phalanx: three shields side by side
    return round_shield(70, 140, 0, .55) + round_shield(186, 140, 0, .55) + round_shield(128, 122, 0, .72)
def k_ninja_2():     # Smoke Bomb: a figure fading into smoke
    return figure(128, 112, 0, 1.0, 'g') + ''.join(f'<circle class="m" cx="{x}" cy="{y}" r="{r}"/>' for x, y, r in [(80, 176, 30), (128, 186, 36), (178, 174, 30), (104, 150, 22), (156, 150, 22)])
def k_hood_0():      # Hook Dagger: a dagger on a chain
    return chain(54, 196, 150, 98) + dagger(176, 76, 45, .8)
def k_hood_2():      # Execute: a dagger over a skull
    return skull(128, 150, .9) + dagger(128, 96, 0, .9)
def k_elf_1():       # Piercing Arrow: one arrow through rings
    return arc(118, 128, 38, -70, 70, 'sl', 6) + arc(156, 128, 30, -70, 70, 'sl', 6) + arrow(128, 128, 90, 1.15)
def k_rowan_0():     # Bear Trap
    return trap(128, 136, 1.05)
def k_darkmage_1():  # Curse: a skull in a ring of runes
    return arc(128, 128, 96, 0, 359, 'sl', 6) + burst(128, 128, 80, 96, 12, 5) + skull(128, 126, .95)
def k_sorceress_2(): # Ice Shards: three shards round a snowflake
    return snowflake(128, 128, 1.1) + ''.join(blade_small(128 + 74*math.cos(math.radians(a)), 128 + 74*math.sin(math.radians(a)), a + 90, 1.1) for a in (-90, 30, 150)) + arc(128, 128, 74, -60, 0) + arc(128, 128, 74, 60, 120) + arc(128, 128, 74, 180, 240)
def k_sunpriest_2(): # Holy Smite: a beam of light striking down
    return '<path class="m" d="M108,20 L148,20 L140,170 L116,170 Z"/>' + burst(128, 182, 26, 56, 10, 7) + sun(128, 46, .45)
def k_lyra_0():      # Moon Ward: a crescent over a shield
    return crescent(128, 112, 1.25, -20) + sparkle(176, 70, 16) + sparkle(84, 176, 12)
def k_lyra_1():      # Starfall: stars falling with trails
    return speed_lines([(60, 30, 96, 90), (130, 20, 160, 74), (176, 70, 206, 124)], 6) + star(104, 110, 30) + star(166, 92, 24) + star(150, 170, 34)
def k_lyra_2():      # Cleansing Song: a note with sparkles
    return note(118, 120, 1.05) + sparkle(186, 80, 20) + sparkle(64, 70, 14) + sparkle(182, 176, 14)
def k_tenzin_0():    # Rising Kick
    return speed_lines([(80, 210, 96, 168), (128, 222, 128, 176), (176, 210, 160, 168)], 7) + fist(128, 112, 0, .9) + burst(128, 80, 70, 92, 7, 6, -180)
def k_tenzin_1():    # Counter Stance: a palm up against a strike
    return palm(128, 136, 0, .95) + burst(128, 128, 84, 104, 10, 6)
def k_tenzin_2():    # Iron Body: a fist inside a shield
    return hex_shield(128, 128, 0, 1.05)

KITS = [
    ('kit_spartan_0', k_spartan_0, '#ffd36b', '#9a5a1a'), ('kit_spartan_1', k_spartan_1, '#ffa63d', '#fff0c0'), ('kit_spartan_2', k_spartan_2, '#ff5a45', '#ffd0a0'),
    ('kit_ninja_2', k_ninja_2, '#b36bff', '#2a1a4a'),
    ('kit_hood_0', k_hood_0, '#7a7bff', '#10101a'), ('kit_hood_2', k_hood_2, '#ff4d6d', '#1a1a2a'),
    ('kit_elf_1', k_elf_1, '#5fd8ff', '#2a6a3a'),
    ('kit_rowan_0', k_rowan_0, '#d9a45a', '#4a2a10'),
    ('kit_darkmage_1', k_darkmage_1, '#c06bff', '#2a103a'),
    ('kit_sorceress_2', k_sorceress_2, '#6bd8ff', '#ffffff'),
    ('kit_sunpriest_2', k_sunpriest_2, '#ffd75e', '#fffbe0'),
    ('kit_lyra_0', k_lyra_0, '#8fa8ff', '#ffffff'), ('kit_lyra_1', k_lyra_1, '#7a7bff', '#fff6c0'), ('kit_lyra_2', k_lyra_2, '#a98bff', '#ffffff'),
    ('kit_tenzin_0', k_tenzin_0, '#ffb13d', '#7a3a10'), ('kit_tenzin_1', k_tenzin_1, '#ff7043', '#ffe0c0'), ('kit_tenzin_2', k_tenzin_2, '#4fdcc0', '#e0fff8'),
]

def hammer(x, y, a=0, s=1):
    return T(x, y, a, s, '<rect class="m" x="-6" y="-20" width="12" height="96" rx="4"/><rect class="m" x="-40" y="-56" width="80" height="40" rx="8"/><path class="dl" d="M-30,-46 L30,-46" style="stroke-width:4;opacity:.5"/>')
def flame(x, y, s=1, cls='m'):
    return T(x, y, 0, s, f'<path class="{cls}" d="M0,-70 C24,-40 40,-20 34,10 C30,40 12,56 0,56 C-12,56 -30,40 -34,10 C-38,-14 -18,-24 -10,-44 C-6,-28 2,-22 6,-30 C10,-42 4,-56 0,-70 Z"/>')
def claws(x, y, s=1):
    return T(x, y, 0, s, ''.join(f'<path class="m" d="M{-46+i*30},-60 C{-30+i*30},-20 {-30+i*30},20 {-50+i*30},64 C{-40+i*30},20 {-40+i*30},-20 {-56+i*30},-56 Z"/>' for i in range(4)))
def k_knight_0(): return chain(40, 200, 200, 70, 8) + '<circle class="m" cx="206" cy="64" r="16"/>'
def k_knight_1(): return hammer(128, 140, -30, 1.15) + burst(150, 90, 70, 92, 7, 6, -150)
def k_knight_2(): return chain(196, 196, 70, 90, 7) + figure(64, 120, 0, .8) + speed_lines([(150, 70, 200, 60), (150, 100, 206, 100)], 6)
def k_flamepriest_0(): return speed_lines([(40, 200, 90, 160), (60, 220, 110, 180)], 6) + flame(150, 120, 1.05)
def k_flamepriest_1(): return round_shield(128, 140, 0, .8) + flame(86, 70, .45) + flame(172, 70, .45) + flame(128, 52, .5)
def k_flamepriest_2(): return arc(128, 150, 70, 0, 359, 'sl', 8) + burst(128, 150, 74, 92, 10, 6) + flame(128, 118, .8)
def k_tiger_0(): return speed_lines([(30, 210, 80, 170), (60, 226, 104, 190)], 7) + claws(150, 112, .9)
def k_tiger_1(): return claws(128, 128, 1.2)
def k_tiger_2(): return roaring_face(128, 124, 1.0) + burst(128, 124, 96, 114, 12, 6)
KITS += [
    ('kit_knight_0', k_knight_0, '#9aa8c0', '#20242c'), ('kit_knight_1', k_knight_1, '#ffc65a', '#5a3a10'), ('kit_knight_2', k_knight_2, '#7a8cff', '#ffffff'),
    ('kit_flamepriest_0', k_flamepriest_0, '#ff7a2a', '#fff0c0'), ('kit_flamepriest_1', k_flamepriest_1, '#ffb43d', '#fff6d0'), ('kit_flamepriest_2', k_flamepriest_2, '#ff4d3d', '#ffe0a0'),
    ('kit_tiger_0', k_tiger_0, '#ff9a2a', '#3a1a08'), ('kit_tiger_1', k_tiger_1, '#ff6a2a', '#fff0d0'), ('kit_tiger_2', k_tiger_2, '#ffb13d', '#7a3a10'),
]

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name, fn, p, s in KITS:
        with open(os.path.join(OUT, name + '.svg'), 'w') as f: f.write(build_svg(fn, p, s))
    print(len(KITS), 'svgs')
