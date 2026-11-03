# Nova Blade: Prompt สำหรับครีป ทหาร และบอส (แบบสมจริง ไม่การ์ตูน)

ใช้กับ Tripo หรือ Meshy แบบเดียวกับตอนทำฮีโร่
วางไฟล์ที่ได้ไว้ที่ `nova-blade-unity/heroes/enemies/` แล้วบอก Claude ให้ rig และใส่เข้าเกม

---

## กฎทุกตัว (ใส่ท้าย prompt ทุกครั้ง)

```
realistic dark fantasy game character, PBR textures, full body, standing in T-pose,
arms straight out to the sides, feet on the ground, no base, no pedestal,
no weapon effects, no particles, single character, neutral lighting,
game-ready, around 20k triangles
```

- ระบบ rig อัตโนมัติทำได้เฉพาะ**ร่างคน** (2 ขา 2 แขน) ที่ยืน **T-pose**
- **สัตว์ 4 ขา** (หมาป่า แมงมุม มังกร): ให้กด **Auto-Rig + Animation** ในตัว Tripo หรือ Meshy เลย แล้วส่งไฟล์ที่มีท่าวิ่งกับท่าตีมาด้วย
- ถ้าได้ท่าแอ็กชันหรือมีฐานติดมา ให้ gen ใหม่ เพราะท่า T-pose จะ rig ได้สะอาดกว่ามาก
- บอสให้เติมคำว่า `imposing silhouette, readable from a top-down camera` จะได้เห็นรูปร่างชัดตอนมองจากมุมบน

---

## 1. ครีปในด่าน (แทนตัวเหลี่ยม)

| ไฟล์ | แทนตัวเดิม | Prompt |
|---|---|---|
| `e_skeleton.glb` | โครงกระดูก | `undead skeleton warrior in rusted iron armor, tattered cloth, holding a chipped sword` |
| `e_goblin.glb` | ก็อบลิน | `feral goblin raider, green-grey leathery skin, crude leather armor, small and wiry` |
| `e_orc.glb` | ออร์ก | `massive orc brute, scarred grey-green skin, heavy iron shoulder plates, tusks` |
| `e_zombie.glb` | ซอมบี้ | `rotting zombie villager, torn peasant clothes, pale decayed skin` |
| `e_cultist.glb` | อสูร (demon) | `hooded dark cultist with glowing red eyes, black robes with crimson trim` |
| `e_ghoul.glb` | ยักษ์ | `hulking flesh ghoul giant, hunched, long arms, bone spikes on back` |

## 2. ทหารเดินเลน (โหมด 1 เลน / 3 เลน)

| ไฟล์ | ทีม | Prompt |
|---|---|---|
| `m_blue.glb` | ทีมเรา | `foot soldier in polished steel armor with blue tabard and round shield` |
| `m_red.glb` | ทีมศัตรู | `foot soldier in dark spiked armor with red tabard and jagged shield` |

## 3. บอส (น่ากลัว ใหญ่ ดูเด่น)

| ไฟล์ | ด่าน | Prompt |
|---|---|---|
| `b_treant.glb` | ป่าแสงจันทร์ (แทนราชาเห็ด) | `colossal corrupted treant lord, cracked bark body, glowing toxic green veins, mushrooms growing from shoulders, menacing` |
| `b_lich.glb` | ปราสาทร้าง | `towering lich king in black and gold armor, skull face with blue soul fire eyes, tattered crown and cape` |
| `b_dragon.glb` | หุบธารจันทรา / หุบเขาเขียวขจี | `fearsome dragon, dark green-black scales, spiked horns, torn wings, glowing amber eyes` (**กด Auto-Rig + Animation**) |

**ไม่ต้องทำใหม่:** ยมทูต (Reaper) และกัปตันกะโหลก (Skull Captain) เป็นโมเดลสมจริงอยู่แล้ว

---

## ลำดับที่แนะนำ
ไม่ต้องทำครบทีเดียว เริ่มจากชุดนี้ก่อนก็เปลี่ยนความรู้สึกเกมได้มากแล้ว:
1. `e_skeleton.glb`
2. `e_orc.glb`
3. `m_blue.glb` + `m_red.glb`
4. บอส 1 ตัว (`b_lich.glb` หรือ `b_treant.glb`)

## เช็กลิสต์
- [ ] e_skeleton
- [ ] e_goblin
- [ ] e_orc
- [ ] e_zombie
- [ ] e_cultist
- [ ] e_ghoul
- [ ] m_blue
- [ ] m_red
- [ ] b_treant
- [ ] b_lich
- [ ] b_dragon
