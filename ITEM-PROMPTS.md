# Nova Blade: Prompt ไอคอนไอเทม (กระเป๋า)

ตอนนี้ไอเทมในกระเป๋าใช้ไอคอนชั่วคราว (รูปทรงง่ายๆ ตามสีความหายาก)
ไฟล์นี้คือ prompt สำหรับ gen รูปไอคอนจริง ใช้กับ Midjourney, Leonardo, Ideogram หรือ ChatGPT (สร้างภาพ) ได้

วางไฟล์ที่ได้ไว้ที่ `nova-blade-unity/Assets/Game/Resources/UI/Items/` ตั้งชื่อตามคอลัมน์ "ชื่อไฟล์" แล้วบอก Claude
(อาวุธกับโล่ไม่ต้องทำ เกมถ่ายรูปจากโมเดล 3D ให้อยู่แล้ว)

---

## กฎทุกรูป (ใส่ท้าย prompt ทุกครั้ง)

```
single game item icon, centered, 3/4 view, dark fantasy RPG style, painterly realistic,
soft rim light, plain dark transparent background, no text, no frame, no border,
square 512x512
```

- **รูปเดียวต่อไฟล์** ไม่มีกรอบ ไม่มีตัวหนังสือ (เกมใส่กรอบสีความหายากให้เอง)
- พื้นหลังโปร่งใส (PNG) ดีที่สุด ถ้าไม่ได้ ใช้พื้นดำเรียบๆ
- ขนาด 512x512 หรือใหญ่กว่า (สี่เหลี่ยมจัตุรัส)

---

## 1. อุปกรณ์สวมใส่ (5 ชุด × 4 ชิ้น = 20 รูป)

แต่ละชุดคือความหายาก 1 ระดับ: หนัง (ธรรมดา) → เหล็ก (ดี) → อัศวิน (หายาก) → เงาราตรี (มหากาพย์) → มังกร (ตำนาน)

| ชื่อไฟล์ | ไอเทม | Prompt |
|---|---|---|
| `helm_leather.png` | หมวกหนัง | `simple worn leather cap helmet with stitched seams` |
| `armor_leather.png` | เกราะหนัง | `simple brown leather chest armor vest with straps and buckles` |
| `boots_leather.png` | รองเท้าหนัง | `pair of simple brown leather boots, worn and laced` |
| `ring_leather.png` | แหวนหนัง | `simple copper ring with a small dull brown stone` |
| `helm_iron.png` | หมวกเหล็ก | `iron nasal helmet, plain forged steel with rivets` |
| `armor_iron.png` | เกราะเหล็ก | `iron chainmail and plate chest armor, plain steel` |
| `boots_iron.png` | รองเท้าเหล็ก | `pair of iron-plated boots, plain steel greaves` |
| `ring_iron.png` | แหวนเหล็ก | `silver ring with a small green emerald` |
| `helm_knight.png` | หมวกอัศวิน | `polished knight great helm with blue plume and gold trim` |
| `armor_knight.png` | เกราะอัศวิน | `polished knight plate armor cuirass with blue tabard and gold trim` |
| `boots_knight.png` | รองเท้าอัศวิน | `pair of polished knight plate sabatons with gold trim` |
| `ring_knight.png` | แหวนอัศวิน | `gold signet ring with a deep blue sapphire` |
| `helm_shadow.png` | หมวกเงาราตรี | `dark assassin hood mask helmet, black and violet, glowing purple runes` |
| `armor_shadow.png` | เกราะเงาราตรี | `sleek black leather and dark steel armor with glowing violet runes` |
| `boots_shadow.png` | รองเท้าเงาราตรี | `pair of sleek black shadow boots with violet glowing trim` |
| `ring_shadow.png` | แหวนเงาราตรี | `black metal ring with a glowing violet amethyst and purple mist` |
| `helm_dragon.png` | หมวกมังกร | `legendary dragon helmet, red-gold scales, horns, glowing orange eyes` |
| `armor_dragon.png` | เกราะมังกร | `legendary dragon scale armor, crimson and gold, glowing ember cracks` |
| `boots_dragon.png` | รองเท้ามังกร | `pair of legendary dragon scale boots, crimson and gold, ember glow` |
| `ring_dragon.png` | แหวนมังกร | `legendary gold ring shaped like a dragon biting a glowing fire ruby` |

## 2. ยาและวัตถุดิบ (6 รูป)

| ชื่อไฟล์ | ไอเทม | Prompt |
|---|---|---|
| `potion.png` | ยาฟื้นพลัง | `round glass healing potion bottle with glowing red liquid and a cork` |
| `shard_0.png` | ผลึกธรรมดา | `small rough gray crystal shard` |
| `shard_1.png` | ผลึกดี | `small glowing green crystal shard` |
| `shard_2.png` | ผลึกหายาก | `glowing blue crystal shard cluster` |
| `shard_3.png` | ผลึกมหากาพย์ | `glowing violet crystal shard cluster with sparkles` |
| `shard_4.png` | ผลึกตำนาน | `radiant orange-gold crystal shard cluster with fiery glow` |

---

## ลำดับที่แนะนำ
ไม่ต้องทำครบทีเดียว เริ่มจากของที่เห็นบ่อยก่อน:
1. `potion.png`
2. ชุดหนังกับชุดเหล็ก (ของที่ดรอปบ่อยที่สุด)
3. ชุดมังกร (ของตำนาน ต้องดูดีที่สุด)
4. ที่เหลือ

## เช็กลิสต์
- [ ] potion
- [ ] ชุดหนัง 4 ชิ้น
- [ ] ชุดเหล็ก 4 ชิ้น
- [ ] ชุดอัศวิน 4 ชิ้น
- [ ] ชุดเงาราตรี 4 ชิ้น
- [ ] ชุดมังกร 4 ชิ้น
- [ ] ผลึก 5 ระดับ
