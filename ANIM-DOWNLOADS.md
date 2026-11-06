# Nova Blade: ท่าที่ต้องโหลดจาก Mixamo (แยกตามสายอาชีพ)

ตอนนี้ทั้ง 12 ตัวใช้ท่ายืน เดิน และวิ่งชุด Great Sword เหมือนกันหมด
ไฟล์นี้คือรายการท่าที่จะทำให้แต่ละสายเคลื่อนไหวเป็นแบบของตัวเอง

โหลดที่ https://www.mixamo.com (ล็อกอินด้วยบัญชี Adobe)
วางไฟล์ที่ `nova-blade-unity/Assets/Game/Animations/<สาย>/` แล้วบอก Claude ให้ใส่เข้าเกม

---

## ตั้งค่าตอนกด Download (ใช้กับทุกท่า)

| ช่อง | ค่า |
|---|---|
| Format | **FBX for Unity (.fbx)** |
| Skin | **Without Skin** |
| Frames per Second | **30** |
| Keyframe Reduction | none |

- ท่า**เดินและวิ่ง** ให้ติ๊ก **In Place** ก่อนโหลด (อยู่ทางขวา ใต้แถบปรับค่า) ไม่งั้นตัวละครจะวิ่งหลุดออกจากจุดเดิม
- ถ้ามีแถบ **Overdrive** ให้ปล่อยไว้ค่าเดิม (50)
- **ตั้งชื่อไฟล์ใหม่ตามคอลัมน์ "ชื่อไฟล์"** เพราะท่าในแต่ละแพ็กมีชื่อซ้ำกัน (เช่น "standing walk forward" มีทั้งในแพ็กเวทและแพ็กธนู)

---

## 1. สายเวท: sorceress, mage, darkmage, sunpriest
โฟลเดอร์ `Animations/mage/`

| ชื่อไฟล์ | ค้นใน Mixamo | หมายเหตุ |
|---|---|---|
| `mage_idle.fbx` | `Pro Magic Pack` → standing idle | ยืนตัวตรง มือว่าง |
| `mage_walk.fbx` | `Pro Magic Pack` → standing walk forward | ติ๊ก In Place |
| `mage_run.fbx` | `Pro Magic Pack` → standing run forward | ติ๊ก In Place |
| `mage_cast_big.fbx` | `Standing 2H Magic Area Attack` | ใช้กับสกิลใหญ่ (ท่ายิงเวททั่วไปมีอยู่แล้ว) |

## 2. สายธนู: elf, hood
โฟลเดอร์ `Animations/archer/`

| ชื่อไฟล์ | ค้นใน Mixamo | หมายเหตุ |
|---|---|---|
| `bow_idle.fbx` | `Pro Longbow Pack` → standing idle 01 | ยืนถือธนู |
| `bow_walk.fbx` | `Pro Longbow Pack` → standing walk forward | ติ๊ก In Place |
| `bow_run.fbx` | `Pro Longbow Pack` → standing run forward | ติ๊ก In Place |
| `bow_dodge.fbx` | `Pro Longbow Pack` → standing dodge left | ถ้าไม่มี ข้ามได้ |

ท่ายิง (Draw Arrow, Aim Recoil) กับ Dodge Backward With Bow มีอยู่แล้ว ไม่ต้องโหลดซ้ำ

## 3. สายนักฆ่า: ninja
โฟลเดอร์ `Animations/assassin/`

| ชื่อไฟล์ | ค้นใน Mixamo | หมายเหตุ |
|---|---|---|
| `ninja_idle.fbx` | `Sword And Shield Idle` หรือ `Fighting Idle` | ท่ายืนพร้อมสู้ เบาๆ |
| `ninja_run.fbx` | `Fast Run` | ติ๊ก In Place, วิ่งโน้มตัว |
| `ninja_walk.fbx` | `Sneak Walk` | ติ๊ก In Place |
| `ninja_slash.fbx` | `Sword And Shield Slash` | ท่าฟันเร็ว |

## 4. สายนักสู้: monk
โฟลเดอร์ `Animations/monk/`

| ชื่อไฟล์ | ค้นใน Mixamo | หมายเหตุ |
|---|---|---|
| `monk_idle.fbx` | `Bouncing Fight Idle` | ตั้งการ์ด |
| `monk_run.fbx` | `Running` | ติ๊ก In Place |
| `monk_punch1.fbx` | `Punching` | คอมโบ 1 |
| `monk_punch2.fbx` | `Hook Punch` | คอมโบ 2 |
| `monk_kick.fbx` | `Roundhouse Kick` (หรือ `Mma Kick`) | คอมโบ 3 |

## 5. สายนักรบ: spartan, warrior, samurai
ท่า Great Sword ปัจจุบันเข้ากับสายนี้อยู่แล้ว **ไม่ต้องโหลด**

---

## ถ้าหาชื่อตรงๆ ไม่เจอ
ชื่อใน Mixamo เปลี่ยนได้ ให้เลือกท่าที่หน้าตาใกล้เคียงที่สุดแล้วตั้งชื่อไฟล์ตามตาราง
ถ้าโหลดมาไม่ครบก็ไม่เป็นไร ท่าไหนไม่มี เกมจะใช้ท่าเดิมไปก่อน

## ลำดับที่แนะนำ
เห็นผลชัดสุดก่อน:
1. สายเวท: idle, walk, run (เพราะมี 4 ตัว)
2. สายธนู: idle, walk, run
3. นักสู้
4. นินจา

## เช็กลิสต์
- [ ] mage_idle
- [ ] mage_walk
- [ ] mage_run
- [ ] mage_cast_big
- [ ] bow_idle
- [ ] bow_walk
- [ ] bow_run
- [ ] bow_dodge
- [ ] ninja_idle
- [ ] ninja_run
- [ ] ninja_walk
- [ ] ninja_slash
- [ ] monk_idle
- [ ] monk_run
- [ ] monk_punch1
- [ ] monk_punch2
- [ ] monk_kick

---

## ท่ายืนหน้าเลือกตัวละคร (แยกผู้หญิง/ผู้ชาย)
วางไฟล์ที่ `nova-blade-unity/Assets/Game/Animations/` (โฟลเดอร์หลัก ไม่ใช่โฟลเดอร์ย่อย) **ชื่อไฟล์ตามนี้เป๊ะๆ** แล้วบอก Claude

| ชื่อไฟล์ | ค้นใน Mixamo | ใช้กับ |
|---|---|---|
| `Catwalk Idle To Twist R.fbx` | Catwalk Idle To Twist R | ฮีโร่ผู้หญิงทุกตัว |
| `Kneeling Down.fbx` | Kneeling Down | ฮีโร่ผู้ชายทุกตัว (เล่นครั้งเดียวแล้วค้าง) |

- ตั้งค่าเหมือนข้างบน: FBX for Unity, Without Skin, 30 fps
- โค้ดเตรียมไว้แล้ว: พอมีไฟล์ เกมจะใช้ท่านี้เฉพาะหน้าเลือกตัวละคร (ตอนสู้ใช้ท่าเดิม)

## ชุดแยกท่าเดินและท่ายืน ชาย/หญิง (ใส่เข้าเกมแล้ว)

ตั้งค่าตอนโหลดตามตารางด้านบน และติ๊ก **In Place** ถ้ามีตัวเลือกนี้ ชื่อท่าในเว็บอาจต่างเล็กน้อย ให้ค้นด้วยคำในตารางแล้วเลือกท่าที่ชอบ

วางไฟล์ที่ `Assets/Game/Animations/new/` ใช้ชื่อไฟล์ตามตาราง

| ไฟล์ | คำค้นใน Mixamo | ใช้กับ |
|---|---|---|
| `f_walk.fbx` | `Female Walk` | ฮีโร่หญิงตอนเดิน |
| `f_run.fbx` | `Female Run` หรือ `Running` | ฮีโร่หญิงตอนวิ่ง |
| `f_idle.fbx` | `Female Standing Pose` หรือ `Happy Idle` | ฮีโร่หญิงในหน้าเลือกตัวละคร |
| `m_walk.fbx` | `Standard Walk` | ฮีโร่ชายตอนเดิน |
| `m_run.fbx` | `Standard Run` | ฮีโร่ชายตอนวิ่ง |
| `m_idle.fbx` | `Standing Idle` | ฮีโร่ชายในหน้าเลือกตัวละคร |

- [x] `f_walk`
- [x] `f_run`
- [x] `f_idle`
- [x] `m_walk`
- [x] `m_run`
- [x] `m_idle`
