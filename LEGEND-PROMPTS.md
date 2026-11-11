# Prompt สำหรับ gen สกินตำนาน 18 ตัว

สกินตำนาน = รางวัลเมื่อชนะตำนานของฮีโร่ระดับ **ตำนาน (ยาก)** ดูเรื่องโลกและสำนักใน [STORY-LORE.md](STORY-LORE.md)
หน้า ผม และรูปร่างต้องเป็น **คนเดิม** (ใช้ประโยคแรกของแต่ละตัวที่ล็อกหน้าไว้) แต่ชุดต้องดูต่างจากตัวปกติชัดเจน หรูขึ้น และมีสีกับตราของสำนักตัวเอง

## วิธีใช้
1. เอา **Base** + **คำบรรยายของตัวนั้น** + **Style** มาต่อกันเป็น prompt เดียว
2. ใส่ **Negative prompt** (ถ้าเครื่องมือมีช่อง)
3. ถ้าเครื่องมือรับภาพอ้างอิงได้ ให้ใส่การ์ดของตัวปกติไปด้วย หน้าจะได้เหมือนเดิม (การ์ดอยู่ที่ `Assets/Game/Resources/UI/Cards/herofull_<id>.png`)
4. เลือกภาพที่ **นิ้วแยกครบ 5 นิ้ว แขนไม่แนบตัว มือว่าง** แล้วแปลงเป็น 3D (Tripo แบบมี texture ไม่ต้อง rig)
5. เช็กก่อนส่ง:
   - นิ้วไม่ติดกัน
   - ผ้า กระโปรง และเสื้อคลุมไม่เกินกลางต้นขา
   - ไม่มีอาวุธ ไม่มีปีก
   - เกราะด้าน ไม่เงาวับเหมือนโครเมียม (เคยแก้เรื่องชุดเกราะเงาเกินไปแล้ว)
6. ตั้งชื่อไฟล์ `heroes/tpose/<id>_legend.glb` (id อยู่ในหัวข้อแต่ละตัว เช่น `spartan_legend.glb`)

**Base (ขึ้นต้นทุกตัว)**
```
Full body character concept, front view, A-pose with arms angled down and out from the body, legs slightly apart, standing straight, hands open with all five fingers clearly spread apart, empty hands, no weapon. Legendary premium skin version of the character, ornate and regal, same face and hairstyle as the base character.
```

**Style (ปิดท้ายทุกตัว)**
```
Stylized semi-realistic fantasy MOBA game art, legendary skin quality, rich ornate details, subtle magical glow on the trims and the faction emblem, clean readable silhouette, high detail PBR materials, matte finish not mirror-shiny, even neutral studio lighting, plain light grey background, centered, whole body visible from head to feet.
```

**Negative prompt (ทุกตัว)**
```
weapon, sword, axe, bow, staff, shield, holding object, clenched fist, fingers merged, mitten hands, long cape, long cloak, floor-length robe, wings, hair covering face, cropped, side view, back view, dynamic pose, crossed arms, extra limbs, blurry, dark background, chrome, mirror reflections, different face
```

---

## นักรบ · กองทัพหุบเหล็ก (เงินเหล็ก + แดงเข้ม · ตราโล่มีหอกไขว้)

### spartan — เลออน (Leon) · "ผู้พิทักษ์บัลลังก์" · ชาย
```
A broad-shouldered male shield guardian, early 30s, short black beard, stern face, no helmet. Legendary royal guard armour: brushed silver-steel breastplate engraved with a crimson shield-and-crossed-spears emblem, gold-trimmed crimson pteruges skirt to mid-thigh, layered silver pauldrons with small lion heads, silver greaves with crimson enamel, a short crimson sash with gold fringe over one shoulder, a faint golden glow along the emblem.
```

### knight — การ์แลนด์ (Garland) · "อัศวินโซ่ราชันย์" · ชาย
```
A tall male chain knight, early 30s, short swept brown hair, determined face, no helmet. Legendary dark-steel plate armour with silver filigree, heavy pauldrons wrapped in decorative silver chains, a crimson tabard to mid-thigh with the shield-and-crossed-spears emblem in silver, crimson-lined gauntlets with separate fingers, armoured boots with chain trim, glowing crimson runes along the chains.
```

### warrior — วัลก้า (Valka) · "ราชินีแห่งภูผา" · หญิง
```
A tall muscular female mountain barbarian, late 20s, wild braided ash-blonde hair, war paint across the eyes, fierce grin. Legendary mountain queen outfit: a white snow-wolf fur mantle with a small bone crown, iron and silver chest armour with crimson leather straps, a crimson wolf emblem on the belt buckle, white fur-trimmed kilt to mid-thigh, silver-banded bracers, heavy white fur boots, faint icy-blue glowing runes on the bracers.
```

## นักฆ่า · สำนักเงาซากุระ (ดำ + ชมพูซากุระ · ตราดอกซากุระ 5 กลีบ)

### samurai — ซากุระ (Sakura) · "ใบมีดกลีบพันปี" · หญิง
```
A lean female samurai assassin, early 20s, black hair in a high ponytail with a pink cherry-blossom pin, calm sharp eyes. Legendary outfit: a black lacquered kimono top embroidered with glowing pink sakura petals, black and gold shoulder guards with pink silk cords, a deep pink obi with a gold five-petal sakura crest, short black hakama to mid-thigh with petal-shaped hem, gold-trimmed shin guards, softly glowing pink petals on the fabric.
```

### ninja — คาเงะ (Kage) · "เงาจันทร์ดับ" · ชาย
```
A slim male shadow ninja, mid 20s, dark hair tied back, a black cloth mask over the lower face so only the eyes show, cold stare. Legendary outfit: a matte black shinobi suit with pink sakura stitching, a black lacquered chest plate with a small five-petal sakura crest, a long pink scarf ending at the waist, dark-steel shoulder and knee plates edged in rose gold, wrapped forearms, split-toe boots, faint pink glow at the scarf tips.
```

### hood — เรเวน (Raven) · "นกกาไร้สำนัก" · หญิง
```
A slender female hooded assassin, mid 20s, pale skin, short dark red hair, the hood pulled back so the face is fully visible, piercing eyes. Legendary outfit: black and deep-crimson leather armour with raven-feather trims on the shoulders, a short hooded capelet to the waist, a cracked five-petal sakura emblem on the chest (she left the clan), crossed chest straps, black corset with silver buckles, tight dark trousers, knee-high boots, faint crimson glow in the cracks of the emblem.
```

## พลธนู · ผู้พิทักษ์ป่าเขียว (เขียวมอส + น้ำตาล + ทองใบไม้ · ตราใบไม้สามแฉก)

### elf — เอลวิน (Elwyn) · "ผู้เฝ้าป่าแสงเงิน" · หญิง
```
A graceful female elf forest ranger, early 20s, long pointed ears, long silver-green hair in a side braid, green eyes. Legendary outfit: moss-green and white ranger armour with gold leaf-shaped shoulder plates, a fitted green tunic to mid-thigh embroidered with gold vines, a gold three-leaf emblem brooch, an empty quiver on the back with gold fittings, a leather bracer with a glowing green leaf gem, tall soft boots with gold trims, tiny glowing leaves along the hem.
```

### rowan — โรวัน (Rowan) · "พรานต้นไม้เก่า" · ชาย
```
A rugged male woodland hunter, early 30s, short brown hair, short stubble, weathered friendly face. Legendary outfit: an ancient-oak themed hooded jerkin with the hood down, bark-textured leather pauldrons, a mossy green mantle collar, a carved wooden three-leaf emblem on the chest strap, an empty quiver of dark wood on the back, archery bracer with amber inlay, sturdy boots with leaf trims, faint warm green glow from the amber.
```

### harpy — แอริส (Aeris) · "เหยี่ยวหน้าผาฟ้า" · หญิง
```
An agile female sky huntress, early 20s, short white-blonde hair with small feathers woven in, golden eyes. Legendary outfit: white and gold feathered armour with storm-blue accents, large feather-shaped gold shoulder pads, a fitted sleeveless top with a gold three-leaf emblem on a feather, a short feathered skirt to mid-thigh, gold arm bands, feathered greaves, light sandals, faint sky-blue glow along the feather tips. No wings.
```

## นักบวช · วิหารสุริยันจันทรา (ขาว + ทอง / สายจันทร์เงิน · ตราดวงตะวันโอบจันทร์เสี้ยว)

### sunpriest — โซเลีย (Solia) · "รุ่งอรุณศักดิ์สิทธิ์" · หญิง
```
A radiant female sun priestess, mid 20s, long golden hair under a small golden sun crown, warm amber eyes, serene face. Legendary outfit: white and gold high-priestess vestments to mid-thigh with layered gold sun-ray shoulder pieces, a large gold sun-and-crescent emblem on the chest, a gold belt with hanging sun charms, gold arm cuffs, white leggings, white boots with gold trim, a soft golden halo glow behind the crown.
```

### lyra — ไลร่า (Lyra) · "จันทร์เต็มดวง" · หญิง
```
A gentle female elven moon priestess, early 20s, long pointed ears, long silver hair, a silver crescent-moon circlet, pale blue eyes. Legendary outfit: silver and midnight-blue vestments to mid-thigh, crescent-moon shoulder clasps with small sapphires, a starry midnight-blue half-capelet to the waist, a silver sun-and-crescent emblem on the chest, silver bracers, soft silver boots, faint silvery moonlight glow on the trims.
```

### flamepriest — เซราฟินา (Seraphina) · "เพลิงพิพากษา" · หญิง
```
A fierce female fire priestess, late 20s, long dark red hair, a golden flame crown, bright orange eyes. Legendary outfit: crimson and gold battle vestments to mid-thigh, tall golden flame-shaped shoulder plates, a gold sun-and-crescent emblem wrapped in flames on the chest, a red sash with a glowing ember amulet, gold-embroidered crimson leggings, armoured gold boots, ember-orange glow along the gold edges.
```

## สายต่อสู้ · อารามเพลิงทอง (ส้มหญ้าฝรั่น + ทอง · ตราเปลวไฟในวงกลม)

### monk — หมิง (Ming) · "หมัดเพลิงพันปี" · ชาย
```
A fit young male martial monk, early 20s, short black hair, determined face. Legendary outfit: a sleeveless gold and deep-saffron monk tunic to mid-thigh with flame embroidery, a gold flame-in-a-circle emblem on the chest, a black and gold sash belt, large gold prayer beads around the neck, gold-wrapped forearms and fingerless gloves with separate fingers, loose dark trousers tied at the shins, cloth shoes, faint golden flame glow on the forearm wraps.
```

### tenzin — เทนซิน (Tenzin) · "แขนเหล็กผู้ตื่นรู้" · ชาย
```
A huge bald male warrior monk, around 40, thick black beard, calm stern face. Legendary outfit: deep maroon and gold robes tied at the waist and cut to mid-thigh, one shoulder bare and muscular, ornate dark-iron forearm guards inlaid with gold flame patterns, a gold-trimmed iron shoulder plate with the flame-in-a-circle emblem, oversized wooden and gold prayer beads, wrapped grey trousers, sandals, faint orange glow in the iron engravings.
```

### tiger — ไท่หู่ (Taihu) · "เจ้าพยัคฆ์ขาว" · ชาย
```
A powerful male tiger-style brawler, early 30s, short spiky dark hair with an orange streak, a scar over one eyebrow, wild grin. Legendary outfit: a sleeveless white-tiger vest with black stripes open at the chest, a white tiger-fur shoulder mantle with a tiger head clasp, an orange sash belt with a small flame-in-a-circle emblem, wrapped fists with gold knuckle guards (fingers free), black loose trousers, wrapped shins, cloth shoes, faint icy-blue glow in the tiger's eyes on the clasp.
```

## เวทมนตร์ · หอคอยนภาคราม (น้ำเงินคราม + ม่วง + เงิน · ตราดาวแปดแฉก)

### mage — อัลดริก (Aldric) · "จอมปราชญ์แห่งดวงดาว" · ชาย
```
A wise male archmage, around 50, neat grey beard, swept-back grey hair, sharp blue eyes. Legendary outfit: a layered deep-azure and violet mage coat to mid-thigh embroidered with silver constellations, a tall silver collar, a large silver eight-pointed star emblem on the chest, a silver belt with floating-looking blue crystal pendants, fitted dark trousers, tall dark boots, rune bracelets, faint starlight glow in the embroidery.
```

### darkmage — ไนท์ (Night) · "ผู้ถูกขับไล่แห่งนภา" · ชาย
```
A brooding male dark sorcerer, late 20s, shaggy black hair, pale skin, faint purple glowing veins near the eyes. Legendary outfit: a fitted black coat cut at mid-thigh with violet lining and torn silver star embroidery, a high collar with silver clasps, a broken eight-pointed star emblem on the chest (he was cast out), a dark leather harness set with glowing purple crystals, black trousers, armoured black boots, purple flame glow along the coat edges.
```

### sorceress — แอเรีย (Aeria) · "ราชินีคลื่นคราม" · หญิง
```
An elegant female sea witch, mid 20s, long flowing teal hair with pearl clips, turquoise eyes. Legendary outfit: a fitted deep-blue and violet sea-silk dress cut to mid-thigh over dark leggings, silver shell-shaped shoulder pieces, a pearl tiara, a silver eight-pointed star emblem set with an aquamarine on the waist sash, coral and pearl jewellery, wrap sandals with silver anklets, soft glowing water-like shimmer on the fabric.
```
