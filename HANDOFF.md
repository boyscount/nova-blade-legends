# ส่งต่องาน Nova Blade Legends (อัปเดต 2026-11-07)

ไฟล์นี้สรุปสิ่งที่ต้องรู้ก่อนทำงานต่อจากเครื่องหรือโปรไฟล์อื่น รายละเอียดอยู่ในไฟล์ที่อ้างถึง

## อยู่ตรงไหนแล้ว

- **เกม:** Action RPG บน Unity 6000.6.7f1 (URP, UI Toolkit) ขึ้นเว็บ WebGL ทั้ง PC และมือถือ
- **เว็บ:** มือถือ https://boyscount.github.io/nova-blade-legends/ และ PC https://boyscount.github.io/nova-blade-legends/pc/
- **Open World โซน 1:** ทำถึงขั้น 4 แล้วบน PC และ **ถึงจุดตัดสินใจ** ต้องให้เจ้าของเกมลองเล่นก่อนทำขั้น 5 ดูสถานะใน [OPEN-WORLD-ZONE1.md](OPEN-WORLD-ZONE1.md)
- **โซนยังไม่มีในชุดมือถือ:** ถูกตัดออกตอน build มือถือ เพราะเรื่องหน่วยความจำ
- **แผนรวม:** [ROADMAP-CHECKLIST.md](ROADMAP-CHECKLIST.md)
- **กฎงบเหลี่ยม การ build และวิธีวัด:** [CHARACTER-PERF-RULES.md](CHARACTER-PERF-RULES.md)

## งานค้าง

- **รอเจ้าของเกมลองเล่น:** กลางวันกลางคืน ถ้ำลับ (หาเจอเองไหม) ควันตอนโดนตี และพื้นรองเท้าตอนเดิน
- **FPS ของโซนบน PC:** วัดล่าสุดราว 34 FPS ลดจาก 37–41 ของรอบก่อน และยังไม่รู้สาเหตุ (เกณฑ์ 40)
- **เท้าที่ยังเหลือ:** เชือกข้อเท้าของ Monk ยังยืดตอนก้าว และชายผ้าคลุมของ Lyra ยังมีจุดแหลม ต้องแก้ทีละตัว
- **มอนสเตอร์:** ยังไม่ได้ใส่การแก้พื้นรองเท้า (`FootWeights`) และยังไม่ได้ตรวจเท้าระยะใกล้
- **สาวกลัทธิกลางคืนยังดำ:** แสงวิญญาณไม่ติดเพราะสีเดิมของโมเดลเกือบดำ
- **ยังไม่ทำ:** จุดซ่อน 3 จาก 8 จุด, ป่าทึบที่มีต้นไม้จริง, บ้านไวกิ้ง, เอาโซนกลับเข้ามือถือ
- **ตอนปล่อยจริง:** เปลี่ยนการบีบอัดเว็บจาก Gzip เป็น Brotli

## วิธีทำงานที่ตกลงกันไว้

- **ถามก่อนทุกครั้ง:** ก่อน commit, ก่อนขึ้นเว็บ และแยกถามอีกครั้งก่อน push main (อนุมัติทีละรอบ)
- **ตอบเป็นภาษาไทย**
- **รอบทดสอบ:** build เฉพาะแพลตฟอร์มที่เปลี่ยน ใช้ Gzip (`NovaBuild.BuildWebPcTest` หรือ `BuildWebPhoneTest`); Brotli ใช้ตอนปล่อยจริง (`BuildWebGL`)
- **ระหว่าง build ห้ามเปิดโปรเจกต์ใน Unity Editor**
- **อย่าหยุด build เว็บกลางทาง:** อาจทำให้ไฟล์โค้ดของอีกชุดหาย ต้องเช็กว่าไฟล์ที่ `index.html` อ้างถึงมีครบทั้ง `Builds/WebGL` และ `Builds/WebGL_PC` ก่อนขึ้นเว็บ
- **ห้าม commit ไฟล์ที่ไม่เกี่ยวข้อง:** meta ของ `Assets/Art`, Weapons, CartoonFX, Polytope, Hovl, Vefects, Dungeon textures, `Packages`, `QualitySettings`, `ProjectSettings`, burst, coin, CityGround (เปลี่ยนเองจากการ import)
- **เพิ่ม `-force-d3d11` ตอนทดสอบด้วยภาพบน exe Windows:** ใน D3D12 ตัวละครบางตัวหายจากภาพในการทดสอบยาว ๆ ตอนนี้เจอแค่บน exe Windows ยังไม่เคยเจอบนเว็บ

## คำสั่ง build และขึ้นเว็บ

ใช้ Git Bash ที่โฟลเดอร์โปรเจกต์ (`U` = `"/c/Program Files/Unity/Hub/Editor/6000.6.7f1/Editor/Unity.exe"`)

```bash
"$U" -batchmode -quit -projectPath "$(cygpath -w "$PWD")" -executeMethod NovaBuild.BuildWin -logFile Builds/win.log </dev/null
```

- **build เมธอดอื่น:** `NovaBuild.AddWildsAndWin` (สร้างฉากโซนกับบ้านใหม่แล้ว build Windows), `NovaBuild.BuildWebPcTest`, `NovaBuild.BuildWebPhoneTest`, `NovaBuild.HdHeroesAndWin` (หลังเปลี่ยนฮีโร่)
- **ขึ้นเว็บ:** สร้าง repo ใหม่ชั่วคราวบน branch `gh-pages` แล้วคัดลอก `Builds/WebGL` ไปไว้ที่ราก, คัดลอก `Builds/WebGL_PC` ไปไว้ที่ `pc/`, ใส่ไฟล์ `.nojekyll` แล้ว push แบบ `--force-with-lease` ไปที่ `gh-pages`
  - ต้องตั้ง `git config http.postBuffer 1048576000` ก่อน push
  - การเผยแพร่บน GitHub Pages บางครั้งล้มรอบแรก ให้ push commit ว่างซ้ำอีกครั้ง
  - สุดท้ายเช็กว่าทั้งสองหน้าชี้ไปที่ไฟล์ build ใหม่
- **commit message:** ลงท้ายด้วย `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`

## โหมดทดสอบใน exe Windows (`Builds/Win/NovaBlade.exe`)

| โหมด | ใช้ทำอะไร |
|---|---|
| `-wildstest <dir>` | ทั้งโซน: ประตู สะพาน เขา บ้าน ศัตรูตามพื้นที่ การเกิดใหม่ กลางวันกลางคืน (ใช้เวลาราว 5 นาที) |
| `-cavetest <dir>` | กลีบนำทาง ประตูถ้ำกลางวันและกลางคืน ห้องถ้ำ หีบวันละครั้ง ทางออก ควันตอนโดนตี (เซฟกลับเป็นเหมือนเดิม) |
| `-foottest <dir> <ids\|all>` | ภาพเท้าระยะใกล้ตอนเดินในเมือง เพิ่ม `-nomend` เพื่อดูแบบก่อนแก้ |
| `-gatewalk`, `-gaittest`, `-crowdtest`, `-bakecards`, `-weaponcards` | ตามชื่อ |

บนเว็บ: `?fps` แสดง FPS และ RAM, `?night` ทำให้ค่ำหลังเข้าโซน 5 วินาที, `?hitch&wildsparts` วัดโซนทีละส่วน (ที่อยู่ต้องมีคำว่า `hitch`)

## สร้างฮีโร่ใหม่

ใช้ Blender 5.2 กับสคริปต์ใน home-redar/nova-blade: `arena_prep.py` → `arena_autorig.py` → `glb_split.py` แล้วคัดลอกไป `Assets/Game/Models/Heroes` และรัน `NovaBuild.HdHeroesAndWin` จากนั้นสร้างภาพการ์ดใหม่ด้วย `-bakecards` งบฮีโร่ไม่เกิน 30,000 เหลี่ยม
