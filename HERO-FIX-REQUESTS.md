# Nova Blade: ของที่ต้องการเพิ่ม เพื่อให้ตัวละครขยับสวยที่สุด

มี 2 ส่วน ส่วน A สำคัญที่สุด

---

## A. Gen ฮีโร่ 5 ตัวใหม่เป็นท่า T-pose

**ทำไม:** ทั้ง 5 ตัวนี้ถูก gen มาในท่าแอ็กชัน (แขนลง ง้างธนู ยกขา ยืนบนฐาน)
ระบบ rig ต้องเดาข้อต่อจากท่าที่งออยู่ พอใส่ท่าเดินหรือวิ่ง เนื้อเลยบิดจนดูพิการ
ส่วนตัวที่ gen มาเป็น T-pose (เรเวน เอเรีย ไนต์ อัลดริค) ขยับได้สวยกว่ามาก

| ตัว | ไฟล์เดิม | ปัญหา |
|---|---|---|
| เอลวีน (Elwyn) | `h_archer_elf_archer_3d_mode` | แขนลง กำลังง้างธนู |
| คาเงะ (Kage) | `h_assassin_ninja_warrior_3d_m` | แขนแนบลำตัว |
| โซเลีย (Solia) | `h_cleric_sun_priestess_3d_m` | มือกำคทาแนบตัว |
| หมิง (Ming) | `h_monk_martial2` | ท่ากังฟู ยกขาข้างหนึ่ง |
| ไลร่า (Lyra) | `h_warrior_female_warrior_sta` | ท่ารูปปั้น ยืนบนฐาน |

### วิธีทำใน Tripo หรือ Meshy
1. ใช้รูปหรือ prompt เดิมของตัวนั้น (หน้าตาจะได้เหมือนเดิม)
2. เปิดตัวเลือก **T-Pose** (Tripo: ตัวเลือก Pose หรือ "T-pose"; Meshy: "A/T Pose" ให้เลือก T)
3. เติมท้าย prompt:
```
standing in T-pose, arms straight out to the sides, palms down, legs straight and slightly apart,
feet flat on the ground, no base, no pedestal, no rocks, full body, game-ready
```
4. **อาวุธ:** ให้ถือไว้ในมือได้ (ธนู คทา โล่ที่แขน) หรือสะพายหลัง (ดาบคาตานะของคาเงะ)
   แต่**ห้ามมีชิ้นไหนแตะพื้นหรือแตะลำตัว** เช่น คทายาวจรดพื้น
5. ถ้าได้ฐาน หินใต้เท้า หรือแขนไม่กางตรง ให้ gen ใหม่

**วางไฟล์:** `nova-blade-unity/heroes/tpose/` ใช้ชื่อ `elf.glb`, `ninja.glb`, `sunpriest.glb`, `monk.glb`, `spartan.glb`

---

## B. ท่าเพิ่มจาก Mixamo

ตั้งค่าเหมือนรอบก่อน: **FBX for Unity, Without Skin, 30 fps**
ท่าเดินกับท่าวิ่งให้ติ๊ก **In Place**

วางไว้ที่เดิม: `Assets/Game/Animations/new/` ตั้งชื่อตามคอลัมน์ "ชื่อไฟล์"

### B1. ท่ายืนโชว์ตัวในหน้าเลือกตัวละคร (ต้องขยับชัด ดูมีชีวิต)

| ชื่อไฟล์ | ใช้กับ | ค้นใน Mixamo |
|---|---|---|
| `show_warrior.fbx` | วัลก้า (ดาบใหญ่) | `Warrior Idle` |
| `show_shield.fbx` | ไลร่า (โล่) | `Sword And Shield Idle` |
| `show_samurai.fbx` | ซากุระ | `Standing Idle` แบบถือดาบ หรือ `Katana Idle` |
| `show_bow.fbx` | เอลวีน, เรเวน, แอริส | `Breathing Idle` หรือ Pro Longbow Pack → idle อีกท่าที่ขยับมากกว่า |
| `show_ninja.fbx` | คาเงะ | `Ninja Idle` |
| `show_monk.fbx` | หมิง | `Fight Idle` หรือ `Bouncing Fight Idle` |
| `show_mage.fbx` | อัลดริค, ไนต์, เอเรีย, โซเลีย | `Happy Idle` หรือ `Standing Idle Looking Around` |

### B2. เดินกับวิ่งให้สายนักรบโล่ (ไลร่า)

| ชื่อไฟล์ | ค้นใน Mixamo |
|---|---|
| `shield_walk.fbx` | `Sword And Shield Walk` (In Place) |
| `shield_run.fbx` | `Sword And Shield Run` (In Place) |

### B3. นักสู้ (หมิง) ยังขาดท่าเดิน

| ชื่อไฟล์ | ค้นใน Mixamo |
|---|---|
| `monk_walk.fbx` | `Walking` หรือ `Fight Walk` (In Place) |

---

## ถ้าหาชื่อตรงๆ ไม่เจอ
เลือกท่าที่ใกล้ที่สุดแล้วตั้งชื่อไฟล์ตามตาราง ท่าไหนยังไม่มี เกมจะใช้ท่าเดิมไปก่อน

## เช็กลิสต์
- [ ] elf.glb (T-pose)
- [ ] ninja.glb (T-pose)
- [ ] sunpriest.glb (T-pose)
- [ ] monk.glb (T-pose)
- [ ] spartan.glb (T-pose)
- [ ] show_warrior
- [ ] show_shield
- [ ] show_samurai
- [ ] show_bow
- [ ] show_ninja
- [ ] show_monk
- [ ] show_mage
- [ ] shield_walk
- [ ] shield_run
- [ ] monk_walk
