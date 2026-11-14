# รายการเสียงของ Nova Blade Legends

เริ่ม 2026-11-14 · เกมยังไม่มีเสียงเลย ระบบเสียง (`Sfx.cs`) เล่นไฟล์ตามชื่อด้านล่าง **ไฟล์ไหนยังไม่มีก็เงียบ ไม่ error** ใส่ทีละไฟล์ได้

## สถานะ (2026-11-14)
เจ้าของเกมใส่มาแล้ว 50 ไฟล์ (โฟลเดอร์ `music/`, `moba/`, `การต่อสู้/`, `เสียงประกาศ/` และไฟล์ ui_* ที่ชั้นนอก) · ระบบหาไฟล์ในโฟลเดอร์ย่อยเหล่านี้เอง (`Sfx.Folders`)
- **ปรับแล้ว:** ตัดความเงียบหัว-ท้าย, ตัดเสียงยาวให้สั้นลง (ป้อมพัง 4 วิ, ระเบิด 3 วิ, ทุบพื้น 2.5 วิ, คริสตัลแตก 3.5 วิ, เกิดใหม่/บัฟ/บาเรีย/ผู้พิทักษ์ 3 วิ) แล้วค่อยเฟดออก, ปรับความดังให้เท่ากันตามกลุ่ม (เพลง -20 LUFS, ปุ่ม -22, เสียงต่อสู้ -18, ผู้ประกาศ -14) · ต้นฉบับอยู่ที่ `Assets/Game/ArtSource/Audio/`
- **ยังขาด (ตอนนี้ยืมเสียงใกล้เคียง):** `ui_open`→ui_select, `ui_close`/`ui_dialogue`→ui_click, `ui_reward`→coin, `ui_mvp`→ui_levelup, `ui_error`→block, `boss_roar`→guardian, `potion`→heal · **ยังไม่มีเลย:** `amb_forest`, `amb_river`, `amb_castle`, `amb_town`, `amb_sky`
- เพลงยาว 30 วิ วนบ่อย ถ้า gen ใหม่ได้ ขอ 1–2 นาที และให้ท้ายต่อกับหัวได้เนียน

## วิธีใส่ไฟล์
1. ตั้งชื่อตามคอลัมน์ "ไฟล์" แล้ววางที่ `Assets/Game/Resources/Audio/` (ใช้ `.ogg`, `.wav` หรือ `.mp3` ได้)
2. **อยากให้เสียงไม่ซ้ำ:** ใส่หลายไฟล์ต่อท้ายด้วย `_1`, `_2`, `_3` (เช่น `hit_light_1.ogg`, `hit_light_2.ogg`) เกมจะสุ่มเล่น
3. **ความยาว:** เสียงเอฟเฟกต์ไม่ควรเกิน 2 วินาที และตัดความเงียบหัว-ท้ายออก เพลงวนซ้ำได้ (loop) ยาว 1–3 นาที
4. **ลิขสิทธิ์:** ใช้ได้เฉพาะเสียงที่ใช้ในเกมได้ฟรี (CC0 / royalty-free) หรือที่ gen เอง จดแหล่งที่มาไว้ในตาราง "แหล่งที่มา" ท้ายไฟล์

## หาเสียงได้จากไหน
- **Kenney.nl (CC0 ฟรี ใช้ได้ทุกอย่าง):** ชุด *RPG Audio*, *Interface Sounds*, *Impact Sounds*, *UI Audio* ครอบคลุมเสียงกด ตีโดน และเหรียญ
- **OpenGameArt.org:** ค้น "fantasy music loop", "sword hit" แล้วเลือกเฉพาะไฟล์ที่ license เป็น CC0 หรือ CC-BY (CC-BY ต้องใส่ชื่อผู้สร้างในเครดิต)
- **Sonniss GameAudioGDC Bundle (ฟรี royalty-free):** เสียงคุณภาพสูงมาก ไฟล์ใหญ่ เลือกเฉพาะที่ใช้
- **AI gen เสียงเอฟเฟกต์:** ElevenLabs Sound Effects หรือ Stable Audio ใช้ prompt ในตาราง (ภาษาอังกฤษ)
- **AI gen เพลง:** Suno / Udio ใช้ prompt ในตาราง เช็คก่อนว่าแพ็กเกจที่ใช้ให้สิทธิ์นำไปใช้ในเกม

---

## เพลง (วนซ้ำ)

| ไฟล์ | ใช้ตอน | คำค้น / prompt |
|---|---|---|
| `music_lobby` | ล็อบบี้ | epic fantasy main menu theme, orchestral with soft choir, heroic but calm, loopable, 90 bpm |
| `music_story` | ด่านเนื้อเรื่อง / ตำนาน | dark fantasy adventure battle music, strings and taiko drums, driving rhythm, loopable |
| `music_moba` | Sky Lanes / สนามรบเลน | epic MOBA battle music, orchestral and electronic hybrid, energetic, loopable, 120 bpm |
| `music_boss` | บอสปรากฏ | intense boss battle music, heavy drums, brass, dark choir, loopable |
| `music_victory` | ชนะ (สั้น ไม่วน) | short triumphant victory fanfare, orchestral brass, 5 seconds |
| `music_defeat` | แพ้ (สั้น ไม่วน) | short sad defeat sting, low strings, 4 seconds |

## หน้าจอ (UI)

| ไฟล์ | ใช้ตอน | คำค้น / prompt |
|---|---|---|
| `ui_click` | กดปุ่มทุกปุ่ม | soft fantasy UI click, wooden and metal, short |
| `ui_open` | เปิดหน้าต่าง (ตั้งค่า ตำนาน ร้าน) | magical panel open whoosh with light chime |
| `ui_close` | ปิดหน้าต่าง | soft reverse whoosh close |
| `ui_select` | เลือกการ์ด / ฮีโร่ / โหมด | crisp selection chime, gold coin ring |
| `ui_levelup` | เลเวลอัป | level up fanfare sparkle, rising magical chime, 1.5 seconds |
| `ui_perk` | เลือกพลังเลเวลอัป | powerful magical card select, energy burst |
| `ui_reward` | ได้ทอง / รางวัล | coins pouring reward jingle |
| `ui_mvp` | ตรา MVP ขึ้น | grand MVP reveal, choir hit with shimmer |
| `ui_error` | กดไม่ได้ (พลังไม่พอ ล็อก) | soft negative buzz UI error |
| `ui_dialogue` | ไปบทพูดถัดไป (ตำนาน) | soft paper page turn |

## การต่อสู้

| ไฟล์ | ใช้ตอน | คำค้น / prompt |
|---|---|---|
| `swing` | ฟันปกติ (ยังไม่โดน) | fast sword swing whoosh |
| `swing_heavy` | ท่าที่ 3 ของคอมโบ / ตีหนัก | heavy weapon swing whoosh, deep |
| `hit_light` | ตีโดนศัตรู | sword hit flesh impact, short punchy |
| `hit_heavy` | ตีหนักโดน | heavy blunt impact with bone crunch |
| `hit_crit` | ติดคริ | critical hit impact with metallic ring |
| `bow_shot` | ยิงธนู / ขนนก | bow arrow release twang |
| `magic_cast` | ร่ายเวท (สายเวท) | magic spell cast whoosh, arcane energy |
| `magic_hit` | เวทโดน | magic impact explosion small, arcane |
| `block` | ป้องกัน / โล่รับ | metal shield block clang |
| `parry` | ปัดดาบตรงจังหวะ | sword parry clash with bright ring |
| `dodge` | หลบ / กลิ้ง | quick roll dodge cloth whoosh |
| `hurt` | ฮีโร่ของเราโดนตี | male/female grunt of pain short (ใช้ `hurt_m`, `hurt_f` ถ้าแยกเพศ) |
| `hero_death` | ฮีโร่ตาย | dramatic death impact with low boom |
| `foe_death` | มอนตาย | monster death growl short |
| `heal` | ฟื้นเลือด | holy healing chime, warm |
| `shield_up` | บาเรีย / โล่ | magic shield activate hum |
| `slam` | สกิลทุบพื้น | ground slam earth shatter impact |
| `whirl` | สกิลหมุน | spinning blade whirlwind whoosh loop short |
| `cry` | สกิลคำรามศึก | warrior battle cry shout with echo |
| `dash` | สกิลพุ่ง / อิไอโดะ | fast dash slash, air cut |
| `explode` | ระเบิด / มิสไซล์ | fantasy explosion fire burst |
| `boss_roar` | บอสปรากฏ / คลั่ง | giant monster roar |
| `coin` | เก็บทอง | single coin pickup |
| `potion` | ดื่มยา | potion drink gulp with sparkle |

## MOBA

| ไฟล์ | ใช้ตอน | คำค้น / prompt |
|---|---|---|
| `tower_shot` | ป้อมยิง | magic crystal turret shot |
| `tower_down` | ป้อมพัง | stone tower collapse crumble |
| `core_down` | คริสตัลหลักแตก | huge crystal shatter explosion |
| `recall` | กลับบ้าน (ระหว่างร่าย) | teleport charging hum, magical |
| `warp` | วาปถึงที่ | teleport arrival whoosh flash |
| `buff` | ได้บัฟป่า / พรผู้พิทักษ์ | power up buff aura magical |
| `guardian` | ผู้พิทักษ์ตื่น | ancient tree giant awaken roar, wood creak |
| `respawn` | เกิดใหม่ | resurrection magical chime |

## เสียงประกาศ (ผู้ประกาศ ภาษาอังกฤษเสียงหนักแน่น)

| ไฟล์ | ประโยค |
|---|---|
| `vo_firstblood` | "First Blood!" |
| `vo_double` | "Double Kill!" |
| `vo_triple` | "Triple Kill!" |
| `vo_spree` | "Killing Spree!" |
| `vo_tower` | "Tower Destroyed!" / "Enemy tower destroyed" |
| `vo_lost_tower` | "Your tower has fallen" |
| `vo_victory` | "Victory!" |
| `vo_defeat` | "Defeat" |
| `vo_guardian` | "The Guardian awakens" |

prompt (ElevenLabs Voice / TTS): *deep epic fantasy game announcer voice, dramatic, slight reverb*

## บรรยากาศ (วนเบาๆ)

| ไฟล์ | ใช้ตอน | คำค้น / prompt |
|---|---|---|
| `amb_forest` | ป่าแสงจันทร์ / หุบเขา | night forest ambience, crickets, light wind |
| `amb_river` | หุบธาร / แมพมีน้ำ | gentle river stream ambience |
| `amb_castle` | ปราสาทร้าง | dark castle ambience, distant wind, dripping |
| `amb_town` | จัตุรัส / เมือง | medieval town ambience, distant crowd |
| `amb_sky` | Sky Lanes | high altitude wind ambience, soft |

---

## แหล่งที่มา (กรอกเมื่อใส่ไฟล์)

| ไฟล์ | แหล่ง | license |
|---|---|---|
| | | |
