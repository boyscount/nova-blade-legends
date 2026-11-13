# Prompt ฉากหลังหน้าจบเกม 6 สำนัก

ฉากหลังของหน้าจบเกม (Battle Chronicle) เปลี่ยนตามสายของฮีโร่ที่เล่น ถ้ายังไม่มีภาพ เกมใช้สีไล่เฉดของสำนักนั้นแทน
ดูเรื่องสำนักและสีประจำสำนักใน [STORY-LORE.md](STORY-LORE.md)

## วิธีใช้
1. เอา **Base** + **คำบรรยายของสำนัก** + **Style** มาต่อกันเป็น prompt เดียว
2. ขนาด **1920 × 1080** แนวนอน 16:9
3. **ห้ามมีตัวละคร คน หรือตัวหนังสือในภาพ** (เกมวางตัวฮีโร่ทางซ้าย และวางตัวหนังสือทางขวาเอง)
4. จัดฉากให้ **ฝั่งซ้าย 40% มีพื้นยืนว่างหรือแท่นหิน** สำหรับวางตัวฮีโร่ และ **ฝั่งขวาเรียบและมืดกว่า** ให้อ่านตัวหนังสือง่าย
5. บันทึกตามชื่อสาย แล้ววางที่ `Assets/Game/Resources/UI/EndBg/<สาย>.png`

| ไฟล์ | สาย | สำนัก |
|---|---|---|
| `warrior.png` | นักรบ | กองทัพหุบเหล็ก |
| `assassin.png` | นักฆ่า | สำนักเงาซากุระ |
| `archer.png` | พลธนู | ผู้พิทักษ์ป่าเขียว |
| `cleric.png` | นักบวช | วิหารสุริยันจันทรา |
| `monk.png` | สายต่อสู้ | อารามเพลิงทอง |
| `mage.png` | เวทมนตร์ | หอคอยนภาคราม |

**Base (ขึ้นต้นทุกภาพ)**
```
Wide cinematic dark fantasy background for a game victory screen, 16:9, no characters, no people, empty stone platform on the left side for a hero to stand on, the right side calmer and darker for interface text,
```

**Style (ปิดท้ายทุกภาพ)**
```
epic painterly MOBA key art style, rich atmospheric lighting, drifting magical particles and glowing embers, soft volumetric light, high detail, premium game art, no text, no letters, no logo, no watermark
```

---

### warrior.png — กองทัพหุบเหล็ก (แดงเข้ม + เงินเหล็ก)
```
a fortress of dark iron and silver in a mountain valley at dusk, crimson war banners with a shield-and-crossed-spears emblem, smoke and red embers in the sky, broken siege weapons, a raised stone platform with steel braziers on the left,
```

### assassin.png — สำนักเงาซากุระ (ดำ + ชมพูซากุระ)
```
a moonlit Japanese-style shrine in a dark sakura forest at night, glowing pink cherry blossom petals falling, black lacquered torii gate, paper lanterns with soft pink light, mist over still water, a wooden platform on the left,
```

### archer.png — ผู้พิทักษ์ป่าเขียว (เขียวมอส + ทองใบไม้)
```
an ancient elven forest with giant mossy trees, golden sunbeams through the canopy, glowing green fireflies, carved wooden archways with leaf motifs, a mossy stone ledge on the left,
```

### cleric.png — วิหารสุริยันจันทรา (น้ำเงินเข้ม + ทอง)
```
a grand white-and-gold temple at twilight with a huge golden sun disc and a silver crescent moon in the sky, marble columns, holy light rays, floating golden runes, a marble altar platform on the left,
```

### monk.png — อารามเพลิงทอง (ส้มเพลิง + ทองแดง)
```
a mountain monastery at sunset with golden pagoda roofs, giant bronze bells, burning braziers and swirling fire embers, prayer flags in the wind, stone steps leading to a training platform on the left,
```

### mage.png — หอคอยนภาคราม (น้ำเงินคราม + ม่วง)
```
a floating crystal spire among the stars, azure and violet nebula sky, glowing arcane circles and floating books, silver eight-pointed star symbols, a crystal platform with runes on the left,
```
