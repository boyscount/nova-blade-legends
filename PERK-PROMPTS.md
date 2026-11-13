# Prompt สำหรับ gen รูปพลังเลเวลอัป 8 แบบ

รูปจะแสดงในช่องสี่เหลี่ยมบนการ์ดเลือกพลัง (ดูภาพตัวอย่างของเจ้าของเกม: หัวใจหนามแดง, ป้อมนาฬิกาทราย, หัวใจเขียว)
ถ้ายังไม่มีรูป เกมจะใช้ไอคอนที่วาดด้วยโค้ดแทนไปก่อน

## วิธีใช้
1. เอา **Base** + **คำบรรยายของแต่ละรูป** + **Style** มาต่อกันเป็น prompt เดียว
2. ขนาด **512 × 512** สี่เหลี่ยมจัตุรัส ไม่ต้องโปร่งใส (มีพื้นหลังได้ เกมตัดให้พอดีช่อง)
3. **ห้ามมีตัวหนังสือในภาพ** (ชื่อพลังเกมใส่เอง สลับไทย/อังกฤษได้)
4. บันทึกชื่อไฟล์ตาม id แล้ววางที่ `Assets/Game/Resources/UI/Perks/<id>.png` เช่น `Assets/Game/Resources/UI/Perks/might.png`

**Base (ขึ้นต้นทุกรูป)**
```
Square game ability icon, a single ornate emblem centered in the frame, dark fantasy MOBA style,
```

**Style (ปิดท้ายทุกรูป)**
```
intricate gold filigree frame around the emblem, glowing magical light, rich painterly detail, dramatic lighting, dark background with soft mist, high quality game UI art, no text, no letters, no numbers, no watermark
```

---

| id | ชื่อ | คำบรรยาย (ใส่ระหว่าง Base กับ Style) |
|---|---|---|
| `might` | ตีแรง | `two crossed blades crackling with fiery red-orange energy, embers flying, a burning rune behind them,` |
| `haste` | สกิลไว | `a glowing cyan lightning bolt over a spinning gold clockwork dial, blue sparks, motion streaks,` |
| `leech` | ดูดเลือด | `a crimson heart wrapped in thorny dark-steel vines, red blood droplets rising into it, red glow,` |
| `swift` | ฝีเท้าไว | `a winged boot of gold and green leather, swirling wind trails and green light, feathers in the air,` |
| `ward` | โล่ฉุกเฉิน | `a radiant golden shield bubble with a sun-shaped crest, a protective dome of light, warm gold glow,` |
| `siege` | ทุบป้อม | `a golden castle tower being struck by a glowing war hammer, cracks of orange light in the stone, debris,` |
| `armor` | เกราะหนา | `a heavy silver-steel knight's shield with layered plates and rivets, cool blue-white metal shine,` |
| `vigor` | เลือดหนา | `a glowing emerald green heart with a heartbeat line of light across it, magic circle behind, green mist,` |

> โทนสีของแต่ละรูปตรงกับสีของพลังในเกม: ตีแรงแดงส้ม, สกิลไวฟ้า, ดูดเลือดแดงเข้ม, ฝีเท้าไวเขียวอ่อน, โล่ฉุกเฉินทอง, ทุบป้อมส้มทอง, เกราะหนาเงิน, เลือดหนาเขียว
