# Nova Blade: Prompt ฮีโร่ HD (Tripo)

ภาพผิวต้นฉบับของ 7 ตัวนี้ละเอียดแค่ 1024 ชุดนี้ gen ใหม่เป็น 2048 เพื่อใช้กับเว็บบนคอมและตัว Windows

**เริ่มแค่วัลก้าตัวเดียวก่อน** แล้วส่งให้ลองในเกม ผลทดสอบพบว่าเพิ่มเป็น 2048 อย่างเดียวคมขึ้นเล็กน้อย ปัญหาหลักคือผิวมันและรอยเหลี่ยม ดูเหตุผลใน [CHARACTER-PERF-RULES.md](CHARACTER-PERF-RULES.md)

**เติมลงในท้าย prompt ด้วย:** `matte skin, soft natural skin, smooth clean armor surfaces, no cracked or faceted texture`

**ตั้งค่าใน Tripo:** T-Pose · ภาพผิว 2048 (HD) · ถ้ามีรูปหรือ prompt ต้นฉบับของตัวนั้น ให้ใช้ของเดิมก่อน หน้าตาจะเหมือนเดิมกว่า

**วางไฟล์:** `nova-blade-unity/heroes/tpose/` ใช้ชื่อไฟล์ตามตาราง

**ถ้าได้แบบนี้ให้ gen ใหม่:** มีฐานหรือหินใต้เท้า · แขนไม่กางตรง · อาวุธแตะพื้นหรือแตะลำตัว

## ท้าย prompt ทุกตัว

```
standing in T-pose, arms straight out to the sides, palms down, legs straight and slightly apart,
feet flat on the ground, facing forward, no base, no pedestal, full body, single character,
stylized realistic fantasy game character, clean high detail textures, game-ready
```

## รายชื่อ (เรียงตามลำดับที่แนะนำ)

| ลำดับ | ตัวละคร | ไฟล์ | Prompt (ต่อด้วยท้าย prompt ด้านบน) |
|---|---|---|---|
| 1 | Valka วัลก้า | `warrior.glb` | adult woman mountain barbarian warrior, long dark braided hair, fur-lined leather and bronze armor, heavy fur cape, leather boots, a large battle sword held in her right hand pointing straight out, determined face |
| 2 | Sakura ซากุระ | `samurai.glb` | adult woman samurai, long pale blonde hair with straight bangs, dark navy kimono with pink cherry blossom pattern, red obi sash, black boots with red laces, a katana held in her right hand pointing straight out, scabbard at the waist |
| 3 | Aeris แอริส | `harpy.glb` | adult woman sky harpy warrior, large white feathered wings spread out from the back, white hood, light silver and leather armor with feather details, elegant fierce face, empty hands |
| 4 | Raven เรเวน | `hood.glb` | adult woman hooded assassin, black hood and short cloak, dark fitted leather armor with belts, a short dagger held in her right hand pointing straight out, sharp eyes |
| 5 | Aldric อัลดริค | `mage.glb` | adult man archmage, long azure blue robe with silver embroidery, deep blue cloak, trimmed grey beard, leather belt with small pouches, empty hands |
| 6 | Night ไนต์ | `darkmage.glb` | adult man sorcerer, long black layered coat with violet trim, high collar, dark hair, pale sharp face, silver buckles, empty hands |
| 7 | Aeria เอเรีย | `sorceress.glb` | adult woman water witch, flowing azure and turquoise robe with wave patterns, long blue hair, silver circlet, light fabric sash, empty hands |

## เช็กลิสต์

- [x] `warrior.glb` เข้าเกมแล้ว
- [x] `samurai.glb` เข้าเกมแล้ว
- [x] `harpy.glb` เข้าเกมแล้ว
- [x] `hood.glb` เข้าเกมแล้ว
- [x] `mage.glb` เข้าเกมแล้ว
- [x] `darkmage.glb` เข้าเกมแล้ว
- [x] `sorceress.glb` เข้าเกมแล้ว
