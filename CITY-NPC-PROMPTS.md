# Nova Blade: Prompt NPC เมืองหลัก (Tripo)

NPC 3 ตัวสำหรับเมืองหลักใน Phase 3 ยืนประจำจุด ไม่ต่อสู้ จึงไม่ถืออาวุธในมือ ของประจำอาชีพติดอยู่กับชุดแทน

**เริ่มจาก `npc_quest` ตัวเดียวก่อน** แล้วส่งให้ลองในเกม ถ้าผ่านค่อย gen อีก 2 ตัว จะได้ไม่เสียเครดิตซ้ำ

**ตั้งค่าใน Tripo:** T-Pose · ภาพผิว 2048 (HD)

**วางไฟล์:** `nova-blade-unity/heroes/npc/` ใช้ชื่อไฟล์ตามตาราง

**ถ้าได้แบบนี้ให้ gen ใหม่:**
- มีฐานหรือหินใต้เท้า
- แขนไม่กางตรง
- ถือของในมือ หรือมีของแตะพื้น
- มีตัวละครหรือเฟอร์นิเจอร์ติดมาด้วย
- ผิวมันแบบพลาสติก หรือมีรอยเหลี่ยมแตกบนหน้า

## ท้าย prompt ทุกตัว

```
standing in T-pose, arms straight out to the sides, palms down, empty hands, legs straight and slightly apart,
feet flat on the ground wearing shoes or boots, facing forward, no base, no pedestal, no weapons in hand, full body, single character,
stylized realistic fantasy game character, clean high detail textures, game-ready,
matte skin, soft natural skin, smooth clean surfaces, no cracked or faceted texture
```

## รายชื่อ (เรียงตามลำดับที่แนะนำ)

| ลำดับ | ตัวละคร | ไฟล์ | Prompt (ต่อด้วยท้าย prompt ด้านบน) |
|---|---|---|---|
| 1 | คนให้เควสต์ | `npc_quest.glb` | adult woman village elder and guild quest keeper, kind wise face with gentle smile lines, long silver hair in a side braid, deep teal hooded robe with gold moon embroidery, brown leather belt with rolled parchment scrolls and a small hanging lantern |
| 2 | พ่อค้า | `npc_merchant.glb` | middle-aged man cheerful traveling merchant, friendly round face, short brown beard, wine red tunic with gold trim, wide brimmed hat with a small feather, leather vest, many small pouches and glass potion bottles strapped to his belt, a large backpack with rolled cloth |
| 3 | ช่างตีอาวุธ | `npc_smith.glb` | strong middle-aged man master blacksmith, broad shoulders, muscular bare forearms, short dark hair, thick black beard, soot-darkened sleeveless shirt, heavy brown leather apron with bronze rivets, thick leather gloves, sturdy boots, a smith hammer and tongs hanging from his belt |

## รายการติดตาม

- [x] `npc_quest.glb` ทำโครงกระดูกแล้ว รอเข้าเกมตอนทำเมือง (เท้าเปล่า เพราะ prompt ไม่ได้ระบุรองเท้า)
- [x] `npc_merchant.glb` ทำโครงกระดูกแล้ว รอเข้าเกมตอนทำเมือง
- [x] `npc_smith.glb` ทำโครงกระดูกแล้ว รอเข้าเกมตอนทำเมือง
