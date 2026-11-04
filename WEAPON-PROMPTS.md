# Nova Blade: Prompt อาวุธเฉพาะสาย (12 ชิ้น)

ใช้กับ Tripo หรือ Meshy แบบเดียวกับตอนทำปีก
วางไฟล์ที่ได้ไว้ที่ `nova-blade-unity/heroes/weapons_new/` ตั้งชื่อตามคอลัมน์ "ชื่อไฟล์" แล้วบอก Claude ให้ใส่เข้าเกม

---

## กฎทุกชิ้น (ใส่ท้าย prompt ทุกครั้ง)

```
single weapon only, no character, no hand holding it, no stand, no base, no pedestal,
straight and centered, plain background, realistic dark fantasy game asset,
PBR textures, highly detailed, game-ready
```

- **ชิ้นเดียวต่อไฟล์** ห้ามมีมือ คน แท่นวาง หรือของอื่นติดมา
- **ตั้งตรง** ไม่เอียง ไม่บิด (ด้ามจับอยู่ปลายด้านหนึ่ง) เกมจะหาจุดจับเองจากรูปทรง
- ถ้าได้อาวุธ 2 ชิ้นในไฟล์เดียว (เช่น มีดคู่) หรือมีมือติดมา ให้ gen ใหม่
- โล่ให้หันด้านหน้าออก เห็นลวดลายชัด

---

## 1. นักรบ (Warrior)
| ชื่อไฟล์ | อาวุธ | Prompt |
|---|---|---|
| `w_greataxe.glb` | ขวานใหญ่สองมือ | `massive two-handed battle axe, dark forged steel double blade with engraved runes, long wooden haft wrapped in leather` |
| `sh_knight.glb` | โล่อัศวิน (ใช้คู่กับดาบ) | `heavy knight kite shield, battered steel with gold trim, embossed lion crest, leather straps on the back` |

## 2. นักฆ่า (Assassin)
| ชื่อไฟล์ | อาวุธ | Prompt |
|---|---|---|
| `w_katana.glb` | คาตานะ | `elegant katana, curved polished blade with wavy hamon line, black silk-wrapped hilt, round gold guard` |
| `w_kunai.glb` | คุไนดาบสั้น | `long assassin kunai dagger, blackened steel blade, ring pommel, red cloth wrapped grip` |

## 3. ธนู (Archer)
| ชื่อไฟล์ | อาวุธ | Prompt |
|---|---|---|
| `w_longbow.glb` | ธนูยาว | `elven longbow, carved pale wood with leaf engravings and silver tips, taut bowstring, no arrow` |
| `w_crossbow.glb` | หน้าไม้ | `heavy steel crossbow, dark wood stock, cranequin winch, loaded bolt` |

## 4. เวท (Mage)
| ชื่อไฟล์ | อาวุธ | Prompt |
|---|---|---|
| `w_arcanestaff.glb` | คทาเวท | `tall arcane mage staff, twisted dark wood, glowing blue crystal held in silver claws at the top` |
| `w_orb.glb` | ลูกแก้วเวท | `floating arcane orb in an ornate gold cage, swirling violet energy inside, small hand grip below` |

## 5. นักบวช (Cleric)
| ชื่อไฟล์ | อาวุธ | Prompt |
|---|---|---|
| `w_holystaff.glb` | คทาศักดิ์สิทธิ์ | `holy priestess staff, white and gold, radiant sun emblem at the top with a glowing golden gem` |
| `w_mace.glb` | ค้อนศักดิ์สิทธิ์ | `holy war mace, flanged silver head with gold inlay, short handle wrapped in white leather` |

## 6. นักสู้ (Fighter / Monk)
| ชื่อไฟล์ | อาวุธ | Prompt |
|---|---|---|
| `w_gauntlet.glb` | สนับมือเหล็ก (ข้างขวาข้างเดียว) | `single right-hand fighting gauntlet, articulated steel plates over knuckles, red leather wrist guard` |
| `w_claws.glb` | กรงเล็บ (ข้างขวาข้างเดียว) | `single tiger claw weapon, three long curved steel blades fixed to a leather hand brace` |

---

## เกมจะใช้ยังไง
- แต่ละชิ้นขายในร้าน (Gold) มีค่าพลังของตัวเอง และใช้ระบบความชำนาญ 5 ขั้นเหมือนดาบ
- **ความถนัด:** อาวุธตรงสายได้ดาเมจเพิ่ม เช่น เวทถือคทา หรือธนูถือธนูยาว
- โล่และสนับมือ/กรงเล็บ ผมจะใส่ให้ถือมือข้างที่ถูกต้องเอง (โล่แขนซ้าย, สนับมือ/กรงเล็บใส่ทั้งสองมือโดยกลับด้านให้)

## ลำดับที่แนะนำ
ไม่ต้องทำครบทีเดียว เริ่มจากสายที่ตอนนี้ยังไม่มีอาวุธในร้านเลยก่อน:
1. `w_longbow` (ธนู)
2. `w_arcanestaff` (เวท)
3. `w_holystaff` (นักบวช)
4. `w_gauntlet` (นักสู้)

## เช็กลิสต์
- [x] w_greataxe (อยู่ในเกมแล้ว)
- [x] sh_knight (อยู่ในเกมแล้ว)
- [x] w_katana (อยู่ในเกมแล้ว)
- [x] w_kunai (อยู่ในเกมแล้ว)
- [x] w_longbow (อยู่ในเกมแล้ว)
- [x] w_crossbow (อยู่ในเกมแล้ว)
- [x] w_arcanestaff (อยู่ในเกมแล้ว)
- [x] w_orb (อยู่ในเกมแล้ว)
- [x] w_holystaff (อยู่ในเกมแล้ว)
- [x] w_mace (อยู่ในเกมแล้ว)
- [x] w_gauntlet (อยู่ในเกมแล้ว)
- [x] w_claws (อยู่ในเกมแล้ว)
