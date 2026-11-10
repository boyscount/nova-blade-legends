# Prompt สำหรับ gen ฮีโร่ใหม่ 17 ตัว (แบบเดียวกับการ์แลนด์)

ฮีโร่เดิมจะกลายเป็น **สกิน** ของฮีโร่ตัวเดียวกัน (ชื่อ สาย สกิลเหมือนเดิม) ตัวใหม่ต้องดู "ตรงสาย" ชัดๆ
ไฟล์ตัวเก่าเก็บไว้ทำสกิน: การ์แลนด์อยู่ที่ `art_in/old_heroes/knight.glb` ตัวอื่นจะถูกย้ายไปที่เดียวกันตอนแทน

## วิธีใช้
1. เอา **ส่วนหัว (Base)** + **คำบรรยายของตัวนั้น** + **ส่วนท้าย (Style)** มาต่อกันเป็น prompt เดียว
2. ใส่ **Negative prompt** ด้านล่าง (ถ้าเครื่องมือมีช่อง)
3. เลือกภาพที่ **นิ้วแยกครบ 5 นิ้ว แขนไม่แนบตัว มือว่าง** แล้วแปลงเป็น 3D (Tripo แบบมี texture, ไม่ต้อง rig)
4. เช็กก่อนส่ง: นิ้วไม่ติดกัน, ผ้า/กระโปรง/เสื้อคลุมไม่เกินกลางต้นขา (ยกเว้นบอกไว้), ไม่มีอาวุธติดมา
5. วางไฟล์ที่ `heroes/tpose/<id>_new.glb` (id อยู่ในหัวข้อแต่ละตัว เช่น `spartan_new.glb`)

**Base (ขึ้นต้นทุกตัว)**
```
Full body character concept, front view, A-pose with arms angled down and out from the body, legs slightly apart, standing straight, hands open with all five fingers clearly spread apart, empty hands, no weapon.
```

**Style (ปิดท้ายทุกตัว)**
```
Stylized semi-realistic fantasy MOBA game art, clean readable silhouette, high detail PBR materials, matte finish not mirror-shiny, even neutral studio lighting, plain light grey background, centered, whole body visible from head to feet.
```

**Negative prompt (ทุกตัว)**
```
weapon, sword, axe, bow, staff, shield, holding object, clenched fist, fingers merged, mitten hands, long cape, long cloak, floor-length robe, wings, hair covering face, cropped, side view, back view, dynamic pose, crossed arms, extra limbs, blurry, dark background, chrome
```

> หมายเหตุ: อาวุธ โล่ ธนู และคทา เกมใส่ให้เอง จึงห้ามมีในภาพทุกตัว

---

## นักรบ (Warrior)

### spartan — เลออน (Leon) · Shield Guardian of the Vale · ชาย
```
A broad-shouldered male shield guardian, early 30s, short black beard, stern face, no helmet. Bronze and dark-steel hoplite armour: sculpted bronze breastplate, layered leather pteruges skirt to mid-thigh, bronze greaves, a short crimson cloth sash over one shoulder, leather bracers with bronze plates, sandal boots with shin guards.
```

### warrior — วัลก้า (Valka) · Mountain Barbarian · หญิง
```
A tall muscular female mountain barbarian, late 20s, wild braided ash-blonde hair, war paint across the eyes, fierce grin. Fur mantle on the shoulders, iron-studded leather chest armour, a wide leather belt with a bone buckle, fur-trimmed leather kilt to mid-thigh, wrapped forearms with iron bracers, heavy fur boots.
```

## นักฆ่า (Assassin)

### samurai — ซากุระ (Sakura) · Cherry-Blossom Samurai · หญิง
```
A lean female samurai assassin, early 20s, black hair in a high ponytail with a pink cherry-blossom pin, calm sharp eyes. Fitted dark navy kimono top with pink sakura embroidery, lacquered black shoulder guards (sode) with pink cords, a red obi sash, short hakama to mid-thigh over dark leggings, armoured shin guards, tabi with sandals.
```

### ninja — คาเงะ (Kage) · Night Shadow Ninja · ชาย
```
A slim male shadow ninja, mid 20s, dark hair tied back, a black cloth mask over the lower face so only the eyes show, cold stare. Tight black and charcoal shinobi suit, dark leather chest strap with small pouches, wrapped forearms and shins, light dark-steel plates on the shoulders and knees, a short purple scarf, split-toe boots.
```

### hood — เรเวน (Raven) · Hooded Killer · หญิง
```
A slender female hooded assassin, mid 20s, pale skin, short dark red hair, the hood pulled back so the face is fully visible, piercing eyes. Fitted crimson and black leather armour, a short hooded capelet to the waist, crossed chest straps with throwing-knife sheaths, black leather corset, tight dark trousers, knee-high soft boots, fingerless gloves with separate fingers.
```

## นักธนู (Archer)

### elf — เอลวิน (Elwyn) · Forest Ranger · หญิง
```
A graceful female elf forest ranger, early 20s, long pointed ears, long silver-green hair in a side braid, green eyes. Moss-green and brown leather ranger armour with leaf-shaped shoulder plates, a fitted green tunic to mid-thigh, a quiver strap across the chest with an empty quiver on the back, leather bracer on the left forearm, tall soft leather boots.
```

### rowan — โรวัน (Rowan) · Greenwood Hunter · ชาย
```
A rugged male woodland hunter, early 30s, short brown hair, short stubble, weathered friendly face. Dark green hooded jerkin with the hood down, brown leather vest with buckles, a quiver strap across the chest with an empty quiver on the back, fur collar, leather archery bracer on the left forearm, belt pouches, sturdy brown boots.
```

### harpy — แอริส (Aeris) · Sky Harpy Warrior · หญิง
```
An agile female sky huntress, early 20s, short white-blonde hair with small feathers woven in, golden eyes. Light white and gold feathered armour, feather-trimmed shoulder pads, a fitted sleeveless top, a short feathered skirt to mid-thigh, gold arm bands, wrapped legs with feathered greaves, light sandals. No wings.
```

## นักเวท (Mage)

### mage — อัลดริก (Aldric) · Azure-Cloak Archmage · ชาย
```
A wise male archmage, around 50, neat grey beard, swept-back grey hair, sharp blue eyes. Layered azure-blue mage coat to mid-thigh with silver rune embroidery, a high collar, a silver belt with glowing blue crystal pendants, fitted dark trousers, tall dark boots, rune bracelets on both wrists.
```

### darkmage — ไนท์ (Night) · Black-Coat Sorcerer · ชาย
```
A brooding male dark sorcerer, late 20s, shaggy black hair, pale skin, faint purple glowing veins near the eyes. A fitted black long coat cut short at mid-thigh with violet lining and silver clasps, a high collar, dark leather harness across the chest with small purple crystals, black trousers, armoured black boots, glowing purple rings.
```

### sorceress — แอเรีย (Aeria) · Azure Tide Witch · หญิง
```
An elegant female sea witch, mid 20s, long flowing teal hair with pearl clips, turquoise eyes. A fitted teal and deep-blue sea-silk dress cut to mid-thigh over dark leggings, shell-shaped shoulder pieces, coral and pearl jewellery, a waist sash with a glowing aquamarine gem, wrap sandals with silver anklets.
```

## นักบวช (Cleric)

### sunpriest — โซเลีย (Solia) · Priestess of the Sun · หญิง
```
A radiant female sun priestess, mid 20s, long golden hair under a small golden sun crown, warm amber eyes, serene face. White and gold priestess vestments to mid-thigh over white leggings, golden sun-ray shoulder pieces, a golden belt with a sun medallion, gold arm cuffs, white boots with gold trim.
```

### lyra — ไลร่า (Lyra) · Elven Moon Priestess · หญิง
```
A gentle female elven moon priestess, early 20s, long pointed ears, long silver hair, a silver crescent-moon circlet, pale blue eyes. Pale lavender and silver vestments to mid-thigh over silver leggings, crescent-moon shoulder clasps, a starry blue half-capelet to the waist, silver bracers, soft silver boots.
```

### flamepriest — เซราฟินา (Seraphina) · Priestess of the Sacred Flame · หญิง
```
A fierce female fire priestess, late 20s, long dark red hair, a golden flame crown, bright orange eyes. Crimson and gold battle vestments to mid-thigh, golden flame-shaped shoulder plates, a red sash with a glowing ember amulet, gold-embroidered red leggings, armoured gold-trimmed boots.
```

## นักสู้ (Fighter / Monk)

### monk — หมิง (Ming) · Golden Flame Fist · ชาย
```
A fit young male martial monk, early 20s, short black hair, determined face. A sleeveless saffron-orange and gold monk tunic to mid-thigh, a black sash belt, prayer beads around the neck, gold-wrapped forearm bandages and fingerless gloves with separate fingers, loose dark trousers tied at the shins, cloth shoes.
```

### tenzin — เทนซิน (Tenzin) · Iron-Armed Monk · ชาย
```
A huge bald male warrior monk, around 40, thick black beard, calm stern face. Maroon monk robes tied at the waist and cut to mid-thigh, one shoulder bare and muscular, heavy dark-iron forearm guards and iron shoulder plate, large wooden prayer beads across the chest, loose grey trousers wrapped at the shins, sandals.
```

### tiger — ไท่หู่ (Taihu) · Tiger-Style Brawler · ชาย
```
A powerful male tiger-style brawler, early 30s, short spiky dark hair with an orange streak, a scar over one eyebrow, wild grin. A sleeveless orange and black tiger-stripe martial vest open at the chest, a tiger-fur shoulder mantle, a black sash belt, wrapped fists with leather knuckle guards (fingers free), black loose trousers, wrapped shins, cloth shoes.
```
