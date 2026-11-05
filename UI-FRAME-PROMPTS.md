# Nova Blade: Prompt ลายกรอบ UI (หน้ากระเป๋าโค้งจันทรา)

ชุดนี้เป็น**ภาพ 2D** ใช้กับ Midjourney, Leonardo, Ideogram หรือ ChatGPT (สร้างภาพ) **ไม่ต้องใช้ Tripo** เพราะ Tripo ทำโมเดล 3D ส่วนกรอบ UI เป็นรูปแบน

วางไฟล์ที่ได้ไว้ที่ `nova-blade-unity/Assets/Game/Resources/UI/Frames/` ตั้งชื่อตามหัวข้อ แล้วบอก Claude
เกมใช้ลายที่วาดด้วยโค้ดไปก่อน ไฟล์ไหนมาแล้วค่อยเปลี่ยนเป็นรูปจริงทีละไฟล์ ไม่ต้องรอครบ

---

## กฎทุกรูป (ใส่ท้าย prompt ทุกครั้ง)

```
game UI asset, ornate dark fantasy, polished antique gold metal with soft warm glow,
pale pink sakura blossoms, crisp clean edges, isolated on a transparent background,
no text, no letters, no characters, no drop shadow on the background
```

- พื้นหลังโปร่งใส (PNG) ดีที่สุด ถ้าได้พื้นลายตารางหมากรุกปลอมให้ gen ใหม่ ถ้าไม่ได้จริงๆ ใช้พื้นดำล้วนแล้วบอก Claude
- ห้ามมีตัวหนังสือในรูป เกมใส่ข้อความเองทั้งไทยและอังกฤษ

---

## ลำดับที่แนะนำ

ทำ 1 ถึง 3 ก่อน เห็นผลมากสุด ที่เหลือมีก็ดี

### 1. `frame_corner.png` (1024x1024) มุมกรอบ
```
top-left corner ornament of a game window frame, gold filigree scrollwork following a right angle,
three sakura blossoms with small leaves clustered on the outer corner,
the two arms taper out along the top edge and the left edge, the inside of the corner is empty
```
เกมกลับด้านใช้ครบ 4 มุม ลายต้องชิดมุมซ้ายบนของรูป

### 2. `slot_ring.png` (1024x1024) วงช่องสวมใส่
```
circular ornate gold ring seen straight on, thin elegant band with fine engraving,
perfectly round, soft golden glow around the ring, the centre of the ring is completely empty and transparent
```

### 3. `blossom.png` (512x512) ดอกซากุระบนเส้นโค้ง
```
single sakura blossom seen from the front, five pale pink petals, tiny gold centre,
one small gold leaf, delicate and luminous
```

### 4. `btn_gold.png` (1536x384) ปุ่มหลักสีทอง
```
wide horizontal game button, rounded rectangle, rich brushed gold surface, bright bevelled gold edge,
soft glow, even plain centre with no pattern, symmetrical left and right
```

### 5. `btn_teal.png` (768x384) ปุ่มรองสีเขียวหัวเป็ด
```
horizontal game button, rounded rectangle, deep dark teal glass surface, thin antique gold outline,
even plain centre with no pattern, symmetrical left and right
```

### 6. `frame_top.png` (1024x256) ลายกลางขอบกรอบ
```
small symmetrical ornament for the top edge of a game window, central gold diamond shape
with thin scrolls reaching left and right, slim and horizontal, wide empty space at both ends
```

### 7. `icon_bag.png` (512x512) ไอคอนถุงของ
```
small drawstring coin pouch icon, worn tan leather tied with a gold cord, 3/4 view
```

### 8. `hud_stage_plate.png` (1536x512) ป้ายด่านด้านบนตอนสู้
```
wide horizontal plaque for the top of a game screen, weathered dark grey-brown carved stone,
hexagonal shape with pointed left and right ends, thin antique gold border,
small gold diamond ornament on each pointed end, plain even centre with no pattern for text
```

---

## เช็กลิสต์

- [ ] `hud_stage_plate.png`

- [ ] `frame_corner.png`
- [ ] `slot_ring.png`
- [ ] `blossom.png`
- [ ] `btn_gold.png`
- [ ] `btn_teal.png`
- [ ] `frame_top.png`
- [ ] `icon_bag.png`
