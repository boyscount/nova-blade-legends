"""Nova Blade skill icon generator: 256x256 transparent PNGs, circular, painted-style SVG."""
import math, os, sys

OUT = sys.argv[1] if len(sys.argv) > 1 else "out"

def hx(c):
    c = c.lstrip('#'); return [int(c[i:i+2], 16) for i in (0, 2, 4)]
def tohex(r): return '#%02x%02x%02x' % tuple(max(0, min(255, int(round(v)))) for v in r)
def mix(a, b, t): return tohex([x + (y - x) * t for x, y in zip(hx(a), hx(b))])
def lum(c): r, g, b = hx(c); return (0.299 * r + 0.587 * g + 0.114 * b) / 255

# ---------- primitives (local coords, centred on 0,0) ----------
def T(x=0, y=0, a=0, s=1, body=''):
    return f'<g transform="translate({x},{y}) rotate({a}) scale({s})">{body}</g>'

def sword(x=128, y=128, a=0, s=1):
    b = ('<path class="m" d="M0,-92 L11,-76 L11,28 L-11,28 L-11,-76 Z"/>'
         '<path class="dl" d="M0,-80 L0,22" style="stroke-width:3;opacity:.45"/>'
         '<rect class="m" x="-32" y="26" width="64" height="12" rx="5"/>'
         '<rect class="m" x="-6" y="38" width="12" height="28" rx="3"/>'
         '<circle class="m" cx="0" cy="72" r="9"/>')
    return T(x, y, a, s, b)

def katana(x=128, y=128, a=0, s=1):
    b = ('<path class="m" d="M-6,40 Q-12,-30 14,-96 Q2,-30 6,40 Z"/>'
         '<ellipse class="m" cx="0" cy="44" rx="18" ry="6"/>'
         '<rect class="m" x="-5" y="49" width="10" height="38" rx="3"/>'
         '<path class="dl" d="M-5,58 L5,64 M-5,70 L5,76" style="stroke-width:2.5"/>')
    return T(x, y, a, s, b)

def dagger(x=128, y=128, a=0, s=1):
    b = ('<path class="m" d="M0,-70 C12,-50 12,-10 9,14 L-9,14 C-12,-10 -12,-50 0,-70 Z"/>'
         '<path class="m" d="M-26,12 Q0,24 26,12 L26,22 Q0,32 -26,22 Z"/>'
         '<rect class="m" x="-6" y="26" width="12" height="30" rx="4"/>'
         '<circle class="m" cx="0" cy="62" r="8"/>')
    return T(x, y, a, s, b)

def arrow(x=128, y=128, a=0, s=1):
    b = ('<rect class="m" x="-4" y="-60" width="8" height="128" rx="3"/>'
         '<path class="m" d="M0,-94 L17,-56 L0,-64 L-17,-56 Z"/>'
         '<path class="m" d="M-4,46 L-18,40 L-18,74 L-4,80 Z M4,46 L18,40 L18,74 L4,80 Z"/>')
    return T(x, y, a, s, b)

def bow(x=128, y=128, a=0, s=1):
    b = ('<path class="sl" d="M-10,-86 L-40,0 L-10,86" style="stroke-width:4"/>'
         '<path class="m" d="M-14,-90 C60,-60 60,60 -14,90 L-6,80 C44,50 44,-50 -6,-80 Z"/>'
         '<circle class="m" cx="30" cy="0" r="9"/>')
    return T(x, y, a, s, b)

def round_shield(x=128, y=128, a=0, s=1):
    b = ('<circle class="m" cx="0" cy="0" r="66"/>'
         '<circle class="dl" cx="0" cy="0" r="50" style="stroke-width:4;opacity:.5"/>'
         '<circle class="d" cx="0" cy="0" r="16" style="opacity:.55"/>'
         '<path class="dl" d="M0,-50 L0,-22 M0,22 L0,50 M-50,0 L-22,0 M22,0 L50,0" style="stroke-width:4;opacity:.45"/>')
    return T(x, y, a, s, b)

def kite_shield(x=128, y=128, a=0, s=1):
    b = ('<path class="m" d="M0,-80 C30,-62 52,-64 64,-60 C66,10 40,56 0,84 C-40,56 -66,10 -64,-60 C-52,-64 -30,-62 0,-80 Z"/>'
         '<path class="dl" d="M0,-60 L0,64 M-46,-30 L46,-30" style="stroke-width:5;opacity:.4"/>')
    return T(x, y, a, s, b)

def hexagon(r):
    return ' '.join(f'{r*math.cos(math.radians(60*i-90)):.1f},{r*math.sin(math.radians(60*i-90)):.1f}' for i in range(6))

def hex_shield(x=128, y=128, a=0, s=1):
    b = (f'<polygon class="m" points="{hexagon(76)}"/>'
         f'<polygon class="dl" points="{hexagon(58)}" style="stroke-width:4;opacity:.45"/>'
         '<path class="d" d="M8,-46 L-24,6 L-2,6 L-10,46 L24,-8 L2,-8 Z" style="opacity:.75"/>')
    return T(x, y, a, s, b)

def fist(x=128, y=128, a=0, s=1):
    b = ('<rect class="m" x="-44" y="-34" width="88" height="74" rx="22"/>'
         '<rect class="m" x="-44" y="-50" width="24" height="40" rx="12"/>'
         '<rect class="m" x="-22" y="-56" width="24" height="44" rx="12"/>'
         '<rect class="m" x="0" y="-54" width="24" height="42" rx="12"/>'
         '<rect class="m" x="20" y="-46" width="24" height="36" rx="12"/>'
         '<path class="m" d="M-44,0 Q-10,-8 14,6 Q20,18 6,22 Q-20,16 -44,22 Z"/>'
         '<rect class="m" x="-30" y="34" width="60" height="30" rx="8"/>')
    return T(x, y, a, s, b)

def palm(x=128, y=128, a=0, s=1):
    b = ('<rect class="m" x="-23" y="-92" width="18" height="60" rx="9"/>'
         '<rect class="m" x="-3" y="-98" width="18" height="66" rx="9"/>'
         '<rect class="m" x="17" y="-90" width="18" height="58" rx="9"/>'
         '<rect class="m" x="37" y="-74" width="16" height="46" rx="8"/>'
         '<path class="m" d="M-40,-20 C-60,-40 -64,-58 -52,-60 C-40,-62 -30,-40 -24,-34 L-26,-44 L52,-40 C58,10 40,44 6,48 C-26,50 -40,20 -40,-20 Z"/>'
         '<rect class="m" x="-18" y="46" width="48" height="26" rx="6"/>')
    return T(x, y, a, s, b)

def feather(x=128, y=128, a=0, s=1):
    b = ('<path class="m" d="M0,-92 C40,-60 34,30 4,72 L-4,72 C-34,30 -40,-60 0,-92 Z"/>'
         '<path class="dl" d="M0,-80 L0,92" style="stroke-width:4"/>'
         '<path class="dl" d="M2,-40 L26,-56 M2,-10 L30,-26 M-2,-24 L-26,-40 M-2,6 L-28,-8 M2,22 L24,8" style="stroke-width:3;opacity:.5"/>')
    return T(x, y, a, s, b)

def wing(x=128, y=128, a=0, s=1, flip=False):
    b = ('<path class="m" d="M-50,50 C-70,-20 -30,-80 60,-84 C46,-66 50,-58 38,-50 C56,-48 50,-34 32,-26 '
         'C50,-22 42,-8 24,-2 C38,6 28,18 12,20 C22,30 8,40 -6,38 C0,50 -20,58 -50,50 Z"/>'
         '<path class="dl" d="M-40,40 C-30,0 0,-40 50,-74" style="stroke-width:3;opacity:.45"/>')
    return T(x, y, a, s, f'<g transform="scale({-1 if flip else 1},1)">{b}</g>')

def petal(x=128, y=128, a=0, s=1, cls='m'):
    b = f'<path class="{cls}" d="M0,24 C-22,6 -18,-18 -6,-26 L0,-18 L6,-26 C18,-18 22,6 0,24 Z"/>'
    return T(x, y, a, s, b)

def flower(x=128, y=128, a=0, s=1):
    b = ''.join(f'<g transform="rotate({72*i}) translate(0,-28)">'
                '<path class="m" d="M0,26 C-24,8 -20,-18 -7,-28 L0,-19 L7,-28 C20,-18 24,8 0,26 Z"/></g>' for i in range(5))
    b += '<circle class="s" cx="0" cy="0" r="10"/>'
    return T(x, y, a, s, b)

def shuriken(x=128, y=128, a=0, s=1):
    b = ('<path class="m" d="M0,-60 L12,-12 L60,0 L12,12 L0,60 L-12,12 L-60,0 L-12,-12 Z"/>'
         '<circle class="d" cx="0" cy="0" r="9" style="opacity:.7"/>')
    return T(x, y, a, s, b)

def figure(x=128, y=128, a=0, s=1, cls='m'):
    b = (f'<circle class="{cls}" cx="0" cy="-34" r="14"/>'
         f'<path class="{cls}" d="M-16,-16 L16,-16 L26,30 L10,30 L4,56 L-4,56 L-10,30 L-26,30 Z"/>')
    return T(x, y, a, s, b)

def tornado(x=128, y=128, a=0, s=1, cls='ml'):
    rows = [(-64, 62), (-38, 50), (-14, 38), (8, 28), (28, 18), (46, 10)]
    b = ''.join(f'<path class="{cls}" d="M{-w+(i*4)},{y0} Q{i*4},{y0+12} {w+(i*4)},{y0}"/>' for i, (y0, w) in enumerate(rows))
    b += f'<path class="{cls}" d="M24,46 Q30,64 18,78" />'
    return T(x, y, a, s, b)

def spiral(cx, cy, r0, r1, turns, cls='sl', w=6, start=0):
    pts = []
    n = 80
    for i in range(n + 1):
        t = i / n
        ang = math.radians(start + 360 * turns * t)
        r = r0 + (r1 - r0) * t
        pts.append(f'{cx + r*math.cos(ang):.1f},{cy + r*math.sin(ang):.1f}')
    return f'<polyline class="{cls}" points="{" ".join(pts)}" style="stroke-width:{w}"/>'

def arc(cx, cy, r, a0, a1, cls='sl', w=6):
    p0 = (cx + r*math.cos(math.radians(a0)), cy + r*math.sin(math.radians(a0)))
    p1 = (cx + r*math.cos(math.radians(a1)), cy + r*math.sin(math.radians(a1)))
    large = 1 if (a1 - a0) % 360 > 180 else 0
    return f'<path class="{cls}" d="M{p0[0]:.1f},{p0[1]:.1f} A{r},{r} 0 {large} 1 {p1[0]:.1f},{p1[1]:.1f}" style="stroke-width:{w}"/>'

def cracks(cx, cy, w=6):
    return (f'<path class="sl" style="stroke-width:{w}" d="M{cx},{cy} L{cx-20},{cy+8} L{cx-34},{cy+2} L{cx-56},{cy+14} '
            f'M{cx},{cy} L{cx+18},{cy+10} L{cx+30},{cy+4} L{cx+56},{cy+16} M{cx},{cy} L{cx-6},{cy+18} L{cx+4},{cy+30} '
            f'M{cx-34},{cy+2} L{cx-40},{cy-10} M{cx+30},{cy+4} L{cx+36},{cy-8}"/>')

def speed_lines(lines, w=5):
    return ''.join(f'<path class="sl" d="M{a},{b} L{c},{d}" style="stroke-width:{w}"/>' for a, b, c, d in lines)

def burst(cx, cy, r0, r1, n=10, w=6, off=0):
    out = ''
    for i in range(n):
        ang = math.radians(off + 360 * i / n)
        out += f'<path class="sl" d="M{cx+r0*math.cos(ang):.1f},{cy+r0*math.sin(ang):.1f} L{cx+r1*math.cos(ang):.1f},{cy+r1*math.sin(ang):.1f}" style="stroke-width:{w}"/>'
    return out

def sparkle(cx, cy, r, cls='s'):
    k = r * 0.22
    return f'<path class="{cls}" d="M{cx},{cy-r} Q{cx+k},{cy-k} {cx+r},{cy} Q{cx+k},{cy+k} {cx},{cy+r} Q{cx-k},{cy+k} {cx-r},{cy} Q{cx-k},{cy-k} {cx},{cy-r} Z"/>'

def drop(x, y, a=0, s=1, cls='m'):
    return T(x, y, a, s, f'<path class="{cls}" d="M0,-30 C14,-10 20,2 20,10 C20,22 11,30 0,30 C-11,30 -20,22 -20,10 C-20,2 -14,-10 0,-30 Z"/>')

def rocket(x, y, a=0, s=1):
    b = ('<path class="s" d="M-8,30 Q0,58 8,30 Z"/>'
         '<path class="m" d="M0,-40 C14,-26 14,0 11,28 L-11,28 C-14,0 -14,-26 0,-40 Z"/>'
         '<path class="m" d="M-11,8 L-22,30 L-11,26 Z M11,8 L22,30 L11,26 Z"/>'
         '<circle class="d" cx="0" cy="-12" r="5" style="opacity:.6"/>')
    return T(x, y, a, s, b)

def leaf(x, y, a=0, s=1, cls='m'):
    return T(x, y, a, s, f'<path class="{cls}" d="M0,-22 C16,-12 16,12 0,22 C-16,12 -16,-12 0,-22 Z"/><path class="dl" d="M0,-18 L0,26" style="stroke-width:2.5;opacity:.5"/>')

def blade_small(x, y, a=0, s=1):
    return T(x, y, a, s, '<path class="m" d="M0,-30 C12,-14 10,10 0,30 C-4,10 -4,-14 0,-30 Z"/>')

def ghost(x, y, a=0, s=1):
    b = ('<path class="s" d="M-20,10 C-30,40 -60,56 -80,60 C-50,44 -38,30 -34,14 Z" style="opacity:.8"/>'
         '<path class="m" d="M0,-28 C22,-28 30,-10 30,6 C30,26 14,34 -4,34 C-26,34 -40,22 -40,6 C-40,-14 -22,-28 0,-28 Z"/>'
         '<ellipse class="d" cx="-10" cy="0" rx="5" ry="8"/><ellipse class="d" cx="10" cy="0" rx="5" ry="8"/>')
    return T(x, y, a, s, b)

def sun(x=128, y=128, s=1, r=36, rays=12, cls='m'):
    b = ''
    for i in range(rays):
        a = 360 * i / rays
        b += f'<g transform="rotate({a})"><path class="{cls}" d="M-9,-{r+6} L0,-{r+30} L9,-{r+6} Z"/></g>'
    b += f'<circle class="{cls}" cx="0" cy="0" r="{r}"/>'
    return T(x, y, 0, s, b)

def yinyang(x=128, y=128, a=0, s=1):
    b = ('<circle class="m" cx="0" cy="0" r="58"/>'
         '<path class="d" d="M0,-58 A58,58 0 0 1 0,58 A29,29 0 0 1 0,0 A29,29 0 0 0 0,-58 Z" style="opacity:.75"/>'
         '<circle class="d" cx="0" cy="-29" r="9" style="opacity:.75"/>'
         '<circle class="m" cx="0" cy="29" r="9" style="stroke-width:0"/>')
    return T(x, y, a, s, b)

def snowflake(x, y, s=1):
    b = ''
    for i in range(6):
        b += (f'<g transform="rotate({60*i})"><path class="ml" d="M0,0 L0,-34 M0,-20 L-10,-30 M0,-20 L10,-30" '
              'style="stroke-width:7"/></g>')
    return T(x, y, 0, s, b)

def reticle(x=128, y=128, s=1):
    b = ('<circle class="ml" cx="0" cy="0" r="56" style="stroke-width:12"/>'
         '<path class="ml" d="M0,-86 L0,-36 M0,36 L0,86 M-86,0 L-36,0 M36,0 L86,0" style="stroke-width:12"/>'
         '<circle class="m" cx="0" cy="0" r="12"/>')
    return T(x, y, 0, s, b)


# ---------- icon definitions ----------
def ic_attack_sword():
    return (burst(128, 128, 70, 96, 8, 5, 22.5) + sword(128, 132, -45, .95) + sword(128, 132, 45, .95))

def ic_attack_bow():
    return (speed_lines([(150, 128, 214, 128), (160, 108, 206, 108), (160, 148, 206, 148)]) +
            bow(108, 128, 0, .95) + arrow(132, 128, 90, .82))

def ic_attack_magic():
    return (sparkle(70, 70, 18) + sparkle(190, 78, 14) + sparkle(182, 186, 18) + sparkle(74, 182, 12) +
            '<circle class="m" cx="128" cy="128" r="54"/>'
            '<circle class="s" cx="128" cy="128" r="30" style="opacity:.7"/>'
            '<ellipse class="w" cx="112" cy="108" rx="16" ry="10" transform="rotate(-30 112 108)" style="opacity:.9"/>')

def ic_attack_holy():
    return ('<path class="s" d="M150,104 L232,40 L240,64 L160,120 Z" style="opacity:.9"/>'
            + burst(150, 108, 22, 38, 8, 5) +
            T(110, 142, 40, 1, '<rect class="m" x="-6" y="-30" width="12" height="110" rx="5"/>'
              '<circle class="m" cx="0" cy="-48" r="22"/><circle class="d" cx="0" cy="-48" r="9" style="opacity:.5"/>'
              '<path class="m" d="M-26,-30 L26,-30 L18,-20 L-18,-20 Z"/>'))

def ic_attack_fist():
    return (speed_lines([(30, 100, 70, 100), (24, 128, 64, 128), (30, 156, 70, 156), (196, 70, 222, 52), (196, 186, 222, 204)], 6)
            + arc(128, 128, 98, -40, 40, 'sl', 6) + fist(140, 132, 90, .95))

def ic_dodge():
    return (speed_lines([(36, 96, 96, 96), (24, 128, 92, 128), (36, 160, 96, 160)], 7) +
            '<path class="m" d="M96,84 L170,84 L170,58 L226,128 L170,198 L170,172 L96,172 Q118,128 96,84 Z"/>')

def ic_souls_light():
    return speed_lines([(60, 210, 200, 50)], 10).replace('sl"', 'sl" style="opacity:.6"') + sword(128, 128, 40, 1.05)

def ic_souls_heavy():
    return (cracks(128, 196, 7) + burst(128, 196, 30, 52, 7, 6, 190) + sword(128, 110, 180, 1.0))

def ic_souls_guard():
    return kite_shield(124, 132, 0, 1.0) + sparkle(190, 62, 30) + sparkle(64, 70, 14)

def ic_souls_lock():
    return reticle(128, 128, 1.05)

# warrior
def ic_warrior_whirl():
    return (spiral(128, 128, 30, 104, 1.6, 'sl', 9) + spiral(128, 128, 30, 104, 1.6, 'sl', 9, 180) + sword(128, 128, 30, .9))
def ic_warrior_slam():
    return (cracks(128, 186, 8) + burst(128, 186, 26, 44, 6, 6, 200) + sword(128, 96, 180, .95))
def roaring_face(x=128, y=124, s=1):
    b = ('<path class="m" d="M-58,-30 C-60,-80 60,-80 58,-30 C64,10 50,60 0,70 C-50,60 -64,10 -58,-30 Z"/>'
         '<path class="d" d="M-44,-34 L-12,-20 L-14,-12 L-42,-22 Z M44,-34 L12,-20 L14,-12 L42,-22 Z" style="opacity:.85"/>'
         '<ellipse class="d" cx="0" cy="26" rx="26" ry="30" style="opacity:.85"/>'
         '<path class="w" d="M-18,6 L-12,16 L-6,6 L0,16 L6,6 L12,16 L18,6 Z"/>')
    return T(x, y, 0, s, b)
def ic_warrior_cry():
    return (arc(128, 124, 92, -40, 40, 'sl', 8) + arc(128, 124, 108, -32, 32, 'sl', 6) +
            arc(128, 124, 92, 140, 220, 'sl', 8) + arc(128, 124, 108, 148, 212, 'sl', 6) + roaring_face(128, 128, .95))

# samurai
def ic_samurai_iaido():
    return ('<path class="s" d="M30,214 Q120,120 230,40 Q130,134 30,214 Z"/>'
            + T(140, 150, -40, 1, '<rect class="m" x="-9" y="10" width="18" height="90" rx="6"/>') + katana(118, 116, -40, 1.0))
def ic_samurai_petal():
    out = spiral(128, 128, 20, 100, 1.3, 'sl', 6)
    for i, (r, a) in enumerate([(78, 20), (90, 110), (70, 200), (92, 290), (50, 70), (50, 250)]):
        x = 128 + r * math.cos(math.radians(a)); y = 128 + r * math.sin(math.radians(a))
        out += petal(x, y, a + 90, .9)
    return out + flower(128, 128, 0, .62)
def ic_samurai_dance():
    ghosts = (figure(56, 150, -10, .9, 'g') + figure(200, 150, 10, .9, 'g') + figure(128, 70, 0, .7, 'g'))
    return ghosts + katana(128, 138, -38, .9) + katana(128, 138, 38, .9)

# harpy
def ic_harpy_feather():
    return (speed_lines([(128,236,128,206),(70,226,84,200),(186,226,172,200)],5) +
            ''.join(feather(128 + 62 * math.sin(math.radians(a)), 168 - 62 * math.cos(math.radians(a)), a, .55) for a in (-60, -30, 0, 30, 60)))
def ic_harpy_dive():
    bird = ('<path class="m" d="M0,70 L-14,20 C-44,0 -50,-50 -40,-80 C-24,-50 -16,-30 -8,-20 L0,-40 L8,-20 C16,-30 24,-50 40,-80 C50,-50 44,0 14,20 Z"/>')
    return (speed_lines([(80, 30, 80, 90), (176, 30, 176, 90), (128, 18, 128, 50)], 6) + cracks(128, 210, 5) + T(128, 132, 0, 1, bird))
def ic_harpy_gale():
    return (tornado(128, 132, 0, .9, 'sl') + wing(64, 120, 0, .7) + wing(192, 120, 0, .7, flip=True))

# toybot
def ic_toybot_laser():
    head = ('<rect class="m" x="-50" y="-40" width="100" height="84" rx="20"/>'
            '<path class="ml" d="M0,-40 L0,-62" style="stroke-width:7"/><circle class="m" cx="0" cy="-66" r="9"/>'
            '<rect class="d" x="-38" y="-24" width="76" height="40" rx="12" style="opacity:.8"/>'
            '<circle class="s" cx="-16" cy="-4" r="9"/><circle class="s" cx="16" cy="-4" r="9"/>'
            '<rect class="d" x="-20" y="26" width="40" height="7" rx="3" style="opacity:.6"/>')
    return ('<path class="s" d="M104,128 L240,96 L240,160 Z" style="opacity:.55"/>'
            '<path class="w" d="M112,124 L240,112 L240,144 L112,132 Z"/>' + T(90, 130, 0, .9, head))
def ic_toybot_missile():
    return rocket(76, 150, -25, .95) + rocket(176, 150, 25, .95) + rocket(128, 120, 0, 1.15)
def ic_toybot_shield():
    return burst(128, 128, 84, 100, 6, 6, 0) + hex_shield(128, 128, 0, 1.0)

# spartan
def ic_spartan_whirl():
    return (arc(128, 128, 92, 200, 320, 'sl', 9) + arc(128, 128, 92, 20, 140, 'sl', 9) +
            '<path class="s" d="M210,104 L228,128 L200,124 Z M46,152 L28,128 L56,132 Z"/>' + round_shield(128, 128, 15, .95))
def ic_spartan_slam():
    return cracks(128, 200, 8) + burst(128, 200, 24, 42, 6, 6, 200) + round_shield(128, 118, 0, .95).replace('scale(0.95)', 'scale(0.95,0.8)')
def ic_spartan_cry():
    helm = ('<path class="s" d="M-62,-58 C-40,-110 40,-110 62,-58 L48,-52 C30,-86 -30,-86 -48,-52 Z"/>'
            '<path class="m" d="M-50,-30 C-50,-80 50,-80 50,-30 L52,40 C40,64 20,70 14,70 L14,4 L34,-6 L34,-20 L-34,-20 L-34,-6 L-14,4 L-14,70 C-20,70 -40,64 -52,40 Z"/>'
            '<path class="dl" d="M-40,-40 C-20,-56 20,-56 40,-40" style="stroke-width:4;opacity:.5"/>')
    return burst(128, 132, 84, 104, 10, 6, 0) + T(128, 140, 0, .95, helm)

# elf
def ic_elf_feather():
    return speed_lines([(70, 218, 90, 190), (128, 226, 128, 196), (186, 218, 166, 190)], 6) + ''.join(arrow(128 + 70*math.sin(math.radians(a)), 150 - 50*math.cos(math.radians(a)), a, .62) for a in (-36, 0, 36))
def ic_elf_dive():
    hawk = ('<path class="m" d="M0,-30 C10,-30 14,-20 12,-10 L90,-60 C80,-30 60,-10 24,10 L14,50 L0,64 L-14,50 L-24,10 C-60,-10 -80,-30 -90,-60 L-12,-10 C-14,-20 -10,-30 0,-30 Z"/>'
            '<path class="d" d="M-4,-30 L4,-30 L0,-20 Z" style="opacity:.5"/>')
    return speed_lines([(128, 20, 128, 70), (60, 40, 70, 76), (196, 40, 186, 76)], 6) + cracks(128, 214, 5) + T(128, 136, 0, 1.0, hawk)
def ic_elf_gale():
    out = tornado(128, 128, 0, 1.0, 'ml')
    for x, y, a in [(64, 76, -30), (196, 92, 40), (60, 170, 20), (196, 176, -50), (168, 40, 70)]:
        out += leaf(x, y, a, 1.0, 's')
    return out

# ninja
def ic_ninja_iaido():
    return (figure(78, 146, -15, 1.4, 'g') + figure(118, 140, -15, 1.4, 'g') +
            '<path class="w" d="M30,200 Q130,130 232,46 Q140,140 30,200 Z"/>' + speed_lines([(150, 160, 214, 110), (120, 196, 180, 156)], 5))
def ic_ninja_petal():
    return (speed_lines([(40, 196, 80, 160), (100, 226, 130, 190), (24, 140, 60, 120)], 5) +
            shuriken(80, 164, 10, .55) + shuriken(176, 168, 30, .55) + shuriken(130, 90, 0, .75))
def ic_ninja_dance():
    out = ''
    for a in (0, 72, 144, 216, 288):
        x = 128 + 82 * math.sin(math.radians(a)); y = 132 - 82 * math.cos(math.radians(a))
        out += figure(x, y, a, .6, 'm')
    return out + '<circle class="s" cx="128" cy="132" r="18"/>' + '<circle class="sl" cx="128" cy="132" r="32" style="stroke-width:5"/>'

# hood
def ic_hood_dance():
    hood = ('<path class="m" d="M0,-80 C50,-80 70,-30 66,20 C64,50 76,66 84,80 L-84,80 C-76,66 -64,50 -66,20 C-70,-30 -50,-80 0,-80 Z"/>'
            '<path class="dk" d="M0,-50 C34,-50 44,-14 40,20 C26,36 -26,36 -40,20 C-44,-14 -34,-50 0,-50 Z"/>'
            '<path class="s" d="M-28,-2 L-8,4 L-12,12 L-30,6 Z M28,-2 L8,4 L12,12 L30,6 Z"/>')
    return T(128, 132, 0, .95, hood)
def ic_hood_iaido():
    return (drop(176, 190, 0, .6, 's') + drop(150, 214, 0, .4, 's') + speed_lines([(40, 96, 84, 120), (52, 64, 96, 92)], 6)
            + dagger(130, 120, 135, 1.05))
def ic_hood_petal():
    out = arc(128, 128, 96, 0, 300, 'sl', 6)
    for i in range(6):
        a = 60 * i
        out += blade_small(128 + 64 * math.cos(math.radians(a)), 128 + 64 * math.sin(math.radians(a)), a + 180, 1.15)
    return out + '<circle class="m" cx="128" cy="128" r="16"/>'

# sunpriest
def ic_sunpriest_heal():
    hands = ('<path class="m" d="M-80,0 C-70,40 -30,60 0,60 C30,60 70,40 80,0 C66,10 50,12 30,14 C20,26 -20,26 -30,14 C-50,12 -66,10 -80,0 Z"/>')
    heart = '<path class="m" d="M0,20 C-40,-6 -40,-40 -18,-44 C-8,-46 -2,-38 0,-32 C2,-38 8,-46 18,-44 C40,-40 40,-6 0,20 Z"/>'
    return (burst(128, 104, 50, 72, 9, 5, -90) + '<circle class="s" cx="128" cy="104" r="44" style="opacity:.5"/>' +
            T(128, 104, 0, 1.0, heart) + T(128, 150, 0, 1.0, hands))
def ic_sunpriest_shield():
    return sun(128, 128, 1.0, 46, 12, 'm') + T(128, 130, 0, .42, '<path class="d" style="opacity:.55" d="M0,-80 C30,-62 52,-64 64,-60 C66,10 40,56 0,84 C-40,56 -66,10 -64,-60 C-52,-64 -30,-62 0,-80 Z"/>')
def ic_sunpriest_cry():
    return ('<ellipse class="ml" cx="128" cy="70" rx="64" ry="18" style="stroke-width:12"/>' + burst(128, 70, 30, 54, 8, 4, 0)
            + figure(128, 168, 0, 1.25, 'm'))

# monk
def ic_monk_slam():
    return cracks(128, 206, 8) + burst(128, 206, 22, 40, 6, 6, 200) + palm(128, 132, 180, .9)
def ic_monk_whirl():
    out = spiral(128, 128, 20, 100, 1.2, 'sl', 7)
    for a in (0, 120, 240):
        x = 128 + 62 * math.cos(math.radians(a - 90)); y = 128 + 62 * math.sin(math.radians(a - 90))
        out += fist(x, y, a + 90, .5)
    return out
def ic_monk_cry():
    return spiral(128, 128, 64, 104, .9, 'sl', 7) + spiral(128, 128, 64, 104, .9, 'sl', 7, 180) + yinyang(128, 128, 0, .95)

# mage
def ic_mage_laser():
    return ('<path class="s" d="M92,150 L236,60 L240,96 L104,170 Z" style="opacity:.6"/>'
            '<path class="w" d="M96,154 L238,72 L240,86 L102,164 Z"/>' + snowflake(88, 166, 1.25)
            + snowflake(190, 104, .5) + snowflake(220, 140, .35))
def ic_mage_missile():
    met = ('<path class="s" d="M-10,-14 L-80,-80 L-60,-40 L-90,-50 Z" style="opacity:.85"/>'
           '<circle class="m" cx="0" cy="0" r="22"/><circle class="d" cx="6" cy="6" r="5" style="opacity:.4"/>')
    return T(160, 170, 0, 1.25, met) + T(80, 108, 0, .8, met) + T(184, 76, 0, .65, met)
def ic_mage_shield():
    out = '<circle class="ml" cx="128" cy="128" r="84" style="stroke-width:10"/><circle class="ml" cx="128" cy="128" r="58" style="stroke-width:6"/>'
    pts = [(128 + 58 * math.cos(math.radians(-90 + 144 * i)), 128 + 58 * math.sin(math.radians(-90 + 144 * i))) for i in range(6)]
    out += '<polyline class="ml" style="stroke-width:6" points="' + ' '.join(f'{x:.1f},{y:.1f}' for x, y in pts) + '"/>'
    for i in range(12):
        a = math.radians(30 * i + 15)
        x, y = 128 + 71 * math.cos(a), 128 + 71 * math.sin(a)
        out += T(x, y, 30 * i + 105, 1, '<path class="s" d="M-5,-5 L5,-5 L0,6 Z M-2,-5 L-2,-9"/>')
    return out

# darkmage
def ic_darkmage_missile():
    return ghost(170, 100, -25, 1.25) + ghost(92, 174, -25, .9) + ghost(70, 80, -25, .65)
def ic_darkmage_laser():
    return ('<path class="s" d="M150,40 A70,70 0 1 0 218,140 A54,54 0 1 1 150,40 Z" style="opacity:.9"/>'
            '<path class="w" d="M100,150 L232,96 L236,112 L106,162 Z"/>' + palm(84, 170, 70, .62))
def ic_darkmage_shield():
    cloak = ('<path class="m" d="M0,-90 C30,-90 40,-60 38,-40 C70,-20 80,40 84,86 L20,86 C40,40 30,0 0,-14 C-30,0 -40,40 -20,86 L-84,86 C-80,40 -70,-20 -38,-40 C-40,-60 -30,-90 0,-90 Z"/>'
             '<ellipse class="dk" cx="0" cy="-54" rx="22" ry="26"/>'
             '<circle class="s" cx="-8" cy="-54" r="4"/><circle class="s" cx="8" cy="-54" r="4"/>')
    return arc(128, 128, 100, 200, 340, 'sl', 6) + T(128, 136, 0, .9, cloak)

# sorceress
def ic_sorceress_laser():
    return ('<path class="m" d="M30,170 C60,120 90,190 128,140 C160,96 190,150 230,96 L234,130 C196,180 166,130 136,170 C100,220 66,150 38,200 Z"/>'
            + drop(60, 92, -30, .5, 's') + drop(200, 196, 30, .45, 's'))
def ic_sorceress_shield():
    return ('<circle class="ml" cx="128" cy="128" r="80" style="stroke-width:12"/>'
            '<path class="w" d="M76,90 A62,62 0 0 1 116,62" style="fill:none;stroke:#fff;stroke-width:10;stroke-linecap:round"/>'
            + figure(128, 140, 0, .9, 'g') + '<circle class="s" cx="200" cy="64" r="10"/><circle class="s" cx="214" cy="96" r="6"/>')
def ic_sorceress_missile():
    return (speed_lines([(30, 70, 70, 96), (30, 132, 76, 140), (40, 196, 80, 180)], 6) +
            drop(110, 100, 120, .9) + drop(176, 140, 120, 1.25) + drop(108, 184, 120, .8))

ICONS = [
    # (filename, symbol fn, primary, secondary)
    ('btn_attack_sword', ic_attack_sword, '#b8c6dc', '#ffffff'),
    ('btn_attack_bow', ic_attack_bow, '#8ee07a', '#e8ffe0'),
    ('btn_attack_magic', ic_attack_magic, '#6fa8ff', '#d8e8ff'),
    ('btn_attack_holy', ic_attack_holy, '#ffd75e', '#fff6d0'),
    ('btn_attack_fist', ic_attack_fist, '#ff8a3d', '#ffe0b0'),
    ('btn_dodge', ic_dodge, '#9fe8ff', '#ffffff'),
    ('btn_souls_light', ic_souls_light, '#ffd36b', '#fff4d0'),
    ('btn_souls_heavy', ic_souls_heavy, '#ff8a3d', '#ffe0b0'),
    ('btn_souls_guard', ic_souls_guard, '#5fd8ff', '#e0f8ff'),
    ('btn_souls_lock', ic_souls_lock, '#ff5a5a', '#ffd0d0'),
    ('skill_warrior_whirl', ic_warrior_whirl, '#ff9a3c', '#ffe0a0'),
    ('skill_warrior_slam', ic_warrior_slam, '#ffc65a', '#8a4a1a'),
    ('skill_warrior_cry', ic_warrior_cry, '#ff4d5e', '#ffc0a0'),
    ('skill_samurai_iaido', ic_samurai_iaido, '#ff5fb4', '#ffffff'),
    ('skill_samurai_petal', ic_samurai_petal, '#ff9ad0', '#fff0f6'),
    ('skill_samurai_dance', ic_samurai_dance, '#d14a8a', '#ffd0e8'),
    ('skill_harpy_feather', ic_harpy_feather, '#d8f0ff', '#6fb8ff'),
    ('skill_harpy_dive', ic_harpy_dive, '#9fe8ff', '#3a7aff'),
    ('skill_harpy_gale', ic_harpy_gale, '#7fd8ff', '#e0fbff'),
    ('skill_toybot_laser', ic_toybot_laser, '#ff3a3a', '#ffd0d0'),
    ('skill_toybot_missile', ic_toybot_missile, '#ffc65a', '#ff6a2a'),
    ('skill_toybot_shield', ic_toybot_shield, '#5ff6ff', '#ffffff'),
    ('skill_spartan_whirl', ic_spartan_whirl, '#ffa63d', '#fff0c0'),
    ('skill_spartan_slam', ic_spartan_slam, '#ffd36b', '#9a5a1a'),
    ('skill_spartan_cry', ic_spartan_cry, '#ff5a45', '#ffd0a0'),
    ('skill_elf_feather', ic_elf_feather, '#7ee86a', '#f0ffe0'),
    ('skill_elf_dive', ic_elf_dive, '#5fd8ff', '#2a6a3a'),
    ('skill_elf_gale', ic_elf_gale, '#3fe0a0', '#c0ffd8'),
    ('skill_ninja_iaido', ic_ninja_iaido, '#ff4d6d', '#1a1a2a'),
    ('skill_ninja_petal', ic_ninja_petal, '#ff8a3d', '#ffe0c0'),
    ('skill_ninja_dance', ic_ninja_dance, '#b36bff', '#2a1a4a'),
    ('skill_hood_dance', ic_hood_dance, '#7a7bff', '#10101a'),
    ('skill_hood_iaido', ic_hood_iaido, '#3ddc97', '#0a3a2a'),
    ('skill_hood_petal', ic_hood_petal, '#9be15d', '#e8ffd0'),
    ('skill_sunpriest_heal', ic_sunpriest_heal, '#ffd75e', '#fffbe0'),
    ('skill_sunpriest_shield', ic_sunpriest_shield, '#ffa94d', '#fff0c0'),
    ('skill_sunpriest_cry', ic_sunpriest_cry, '#ff7eb3', '#ffe8f0'),
    ('skill_monk_slam', ic_monk_slam, '#ffb13d', '#7a3a10'),
    ('skill_monk_whirl', ic_monk_whirl, '#ff7043', '#ffe0c0'),
    ('skill_monk_cry', ic_monk_cry, '#4fdcc0', '#e0fff8'),
    ('skill_mage_laser', ic_mage_laser, '#5fd2ff', '#ffffff'),
    ('skill_mage_missile', ic_mage_missile, '#8c8cff', '#e0e0ff'),
    ('skill_mage_shield', ic_mage_shield, '#45e6ff', '#d0faff'),
    ('skill_darkmage_missile', ic_darkmage_missile, '#c06bff', '#2a103a'),
    ('skill_darkmage_laser', ic_darkmage_laser, '#ff4dd2', '#ffd0f4'),
    ('skill_darkmage_shield', ic_darkmage_shield, '#8a7bff', '#1a1030'),
    ('skill_sorceress_laser', ic_sorceress_laser, '#3db8ff', '#e0f6ff'),
    ('skill_sorceress_shield', ic_sorceress_shield, '#5ff0e0', '#ffffff'),
    ('skill_sorceress_missile', ic_sorceress_missile, '#6b9bff', '#c8b0ff'),
]

def build_svg(fn, p, s):
    top = mix(p, '#ffffff', .18)
    mid = p
    bot = mix(p, '#000000', .62)
    outline = mix(p, '#000000', .72)
    sym_top = '#ffffff'
    sym_bot = mix(p, '#ffffff', .72)
    glow = mix(p, '#ffffff', .35)
    # secondary fx colour: if very dark, keep it but make the halo use primary
    halo = s if lum(s) > .35 else mix(p, '#ffffff', .5)
    # light backgrounds need dark-ish fx so they show
    fx = s
    css = f"""
    .m{{fill:url(#gs);stroke:{outline};stroke-width:5;stroke-linejoin:round;paint-order:stroke}}
    .ml{{fill:none;stroke:url(#gs);stroke-width:9;stroke-linecap:round;stroke-linejoin:round}}
    .g{{fill:{fx};opacity:.55}}
    .d{{fill:{outline}}}
    .dk{{fill:{mix(p,'#000000',.85)}}}
    .dl{{fill:none;stroke:{outline};stroke-linecap:round}}
    .s{{fill:{fx}}}
    .sl{{fill:none;stroke:{fx};stroke-width:6;stroke-linecap:round;stroke-linejoin:round;opacity:.9}}
    .w{{fill:#ffffff}}
    """
    return f"""<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">
<defs>
 <style>{css}</style>
 <linearGradient id="bg" x1="0" y1="0" x2="0" y2="1">
  <stop offset="0" stop-color="{top}"/><stop offset=".45" stop-color="{mid}"/><stop offset="1" stop-color="{bot}"/>
 </linearGradient>
 <radialGradient id="halo" cx=".5" cy=".5" r=".5">
  <stop offset="0" stop-color="{halo}" stop-opacity=".75"/><stop offset=".6" stop-color="{halo}" stop-opacity=".25"/><stop offset="1" stop-color="{halo}" stop-opacity="0"/>
 </radialGradient>
 <linearGradient id="gs" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="{sym_top}"/><stop offset="1" stop-color="{sym_bot}"/></linearGradient>
 <linearGradient id="gloss" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#fff" stop-opacity=".45"/><stop offset="1" stop-color="#fff" stop-opacity="0"/></linearGradient>
 <radialGradient id="vig" cx=".5" cy=".42" r=".6"><stop offset=".7" stop-color="#000" stop-opacity="0"/><stop offset="1" stop-color="#000" stop-opacity=".35"/></radialGradient>
 <filter id="glow" x="-30%" y="-30%" width="160%" height="160%">
  <feMorphology in="SourceAlpha" operator="dilate" radius="2" result="d"/>
  <feGaussianBlur in="d" stdDeviation="7" result="b"/>
  <feFlood flood-color="{glow}"/><feComposite in2="b" operator="in" result="g"/>
  <feGaussianBlur in="SourceAlpha" stdDeviation="3" result="sb"/><feOffset in="sb" dy="4" result="so"/>
  <feFlood flood-color="#000" flood-opacity=".45"/><feComposite in2="so" operator="in" result="sh"/>
  <feMerge><feMergeNode in="g"/><feMergeNode in="sh"/><feMergeNode in="SourceGraphic"/></feMerge>
 </filter>
 <clipPath id="c"><circle cx="128" cy="128" r="127"/></clipPath>
</defs>
<g clip-path="url(#c)">
 <circle cx="128" cy="128" r="128" fill="url(#bg)"/>
 <circle cx="128" cy="124" r="104" fill="url(#halo)"/>
 <g filter="url(#glow)">{fn()}</g>
 <circle cx="128" cy="128" r="128" fill="url(#vig)"/>
 <ellipse cx="128" cy="58" rx="96" ry="48" fill="url(#gloss)"/>
</g>
</svg>"""

if __name__ == '__main__':
    os.makedirs(OUT, exist_ok=True)
    for name, fn, p, s in ICONS:
        with open(os.path.join(OUT, name + '.svg'), 'w') as f:
            f.write(build_svg(fn, p, s))
    print(len(ICONS), 'svgs')
