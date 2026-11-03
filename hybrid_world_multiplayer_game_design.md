# Hybrid World MMORPG + Soulslike Combat

## World Architecture, Multiplayer Architecture & Game Design

**เอกสารออกแบบระบบเกม (Technical Design Document)**\
**Engine:** Unity + URP · **Language:** C# · **Camera:** Third Person ·
**World:** Hybrid World

------------------------------------------------------------------------

## 1. Vision และหลักการออกแบบ

เกมนี้ใช้โครงสร้าง **Hybrid World**: เมืองและพื้นที่สำรวจหลักเป็นโซน Open World
ที่เชื่อมต่อกัน ขณะที่ Dungeon, Boss Arena, PvP Arena และกิจกรรมบางประเภททำงานเป็น
Instance แยกออกมา

เป้าหมายคือให้ผู้เล่นรู้สึกว่าอยู่ในโลกเดียวกัน มีเมืองเป็นศูนย์กลาง
มีพื้นที่ให้สำรวจและทำกิจกรรมร่วมกับผู้เล่นอื่น แต่ยังควบคุมจำนวนผู้เล่น ประสิทธิภาพ
และความยุติธรรมของการต่อสู้ได้

### หลักการสำคัญ

-   **World is persistent, activities are instanced:** โลกหลักมีความต่อเนื่อง
    ส่วนกิจกรรมที่ต้องควบคุมกติกาแยกเป็น Instance
-   **Server authoritative:** Server เป็นผู้ตัดสินผลการต่อสู้ ไอเทม EXP เงิน
    และผลการแข่งขัน
-   **Composition over inheritance:** ประกอบความสามารถจาก Component และ
    Definition แทนการสร้างคลาสย่อยจำนวนมาก
-   **Data-driven:** ใช้ ScriptableObject สำหรับข้อมูลตั้งต้นของคลาส อาวุธ สกิล
    มอนสเตอร์ และพื้นที่
-   **PvE-first, Optional PvP:** ผู้เล่น PvE ไม่ถูกบังคับให้เข้าร่วม PvP
-   **Build freedom with trade-offs:** อาวุธข้ามคลาสได้ แต่มี Weapon Affinity
    และ Proficiency เป็นตัวกำหนดความถนัด

> ข้อควรระวัง: Unity Netcode for GameObjects (NGO) เป็น networking layer
> ไม่ใช่ระบบ MMORPG backend สำเร็จรูป ต้องมีระบบจัดการบัญชี ตัวละคร โลก Session,
> Matchmaking, Persistence และ Dedicated Server เพิ่มเติม

------------------------------------------------------------------------

# Part A --- World Architecture

## 2. ภาพรวม Hybrid World

โลกแบ่งออกเป็น 4 ประเภทพื้นที่

  -----------------------------------------------------------------------
  พื้นที่                     รูปแบบ                   วัตถุประสงค์
  ----------------------- ----------------------- -----------------------
  Capital City            Shared Zone             เมืองหลัก ร้านค้า NPC,
                                                  Crafting, Party, Social

  Open World Zones        Shared/Managed Zone     สำรวจ เก็บทรัพยากร เควสต์
                                                  มอนสเตอร์ World Boss

  Dungeon / Boss Instance Private Instance        ปาร์ตี้ PvE, Boss, Loot
                                                  และเนื้อเรื่อง

  PvP Arena               Competitive Instance    Duel, 3v3, 5v5 และ
                                                  Tournament
  -----------------------------------------------------------------------

### 2.1 World Topology

``` mermaid
flowchart TB
    Login[Login / Character Select] --> Gateway[World Gateway]
    Gateway --> City[Capital City<br/>Shared Zone]
    City --> OW1[Forest Zone]
    City --> OW2[Village / Lake Zone]
    City --> OW3[Mountain Zone]
    OW1 <--> OW2
    OW2 <--> OW3
    OW1 --> D1[Dungeon Instance]
    OW2 --> D2[Boss Instance]
    City --> Arena[PvP Arena Instance]
    City --> Housing[Personal / Guild Area]
    D1 --> Result[Reward & Progression]
    D2 --> Result
    Arena --> Rating[Match Result / Rating]
    Result --> City
    Rating --> City
```

**แนวทางการเชื่อมต่อ** - ผู้เล่นเดินทางระหว่าง Shared Zone ผ่านทางเชื่อม, ประตูเมือง,
เรือ หรือจุด Fast Travel - เมื่อเข้า Dungeon ระบบสร้างหรือเลือก Instance ที่เหมาะสม
แล้วส่งสมาชิก Party เข้าไป - เมื่อจบกิจกรรม Server
บันทึกผลและส่งผู้เล่นกลับจุดเชื่อมต่อที่กำหนด - ไม่ควรโหลดทั้งโลกเป็นฉากเดียวตั้งแต่เริ่มต้น
ให้แบ่งเป็น Zone และใช้ Additive Scene Loading ตามความเหมาะสม

## 3. Zone และ Instance Lifecycle

``` mermaid
stateDiagram-v2
    [*] --> Requested
    Requested --> Allocating: เลือก Zone / Instance
    Allocating --> Loading: Server พร้อม
    Loading --> Active: ผู้เล่นเข้าได้
    Active --> Draining: ไม่มีผู้เล่น / หมดเวลา
    Draining --> Saving: บันทึกสถานะ
    Saving --> Released: คืนทรัพยากร
    Released --> [*]
    Allocating --> Failed
    Loading --> Failed
    Failed --> Released
```

### 3.1 Zone Definition

สร้าง `ZoneDefinition` เป็น ScriptableObject เพื่อกำหนดข้อมูลพื้นที่ เช่น

-   `zoneId`
-   `displayName`
-   `sceneReference`
-   `zoneType` (Shared, Dungeon, Arena)
-   `maxPlayers`
-   `recommendedLevel`
-   `entryRequirements`
-   `spawnPoints`
-   `allowedActivities`

ตัวอย่างข้อมูลเชิงแนวคิด:

``` csharp
public enum ZoneType
{
    Shared,
    Dungeon,
    BossInstance,
    PvpArena
}

[CreateAssetMenu(menuName = "Game/World/Zone Definition")]
public sealed class ZoneDefinition : ScriptableObject
{
    public string zoneId;
    public string displayName;
    public ZoneType zoneType;
    public int maxPlayers;
    public int recommendedLevel;
}
```

ตัวอย่างนี้เป็น Definition ฝั่ง Unity เท่านั้น ไม่ใช่สถานะจริงของ Instance ที่กำลังเปิดอยู่

### 3.2 Zone Runtime แยกจาก Definition

  ส่วน                 หน้าที่
  ------------------- -----------------------------
  ZoneDefinition      ข้อมูลตั้งต้นของ Zone
  ZoneRuntime         สถานะ Zone ที่กำลังทำงาน
  InstanceRuntime     สถานะ Dungeon/Arena แต่ละรอบ
  ZoneService         เลือก Zone และจัดการการเข้าออก
  InstanceAllocator   จัดสรร Server/Instance
  SceneLoader         โหลดและปลดโหลด Scene
  SpawnService        เลือกจุดเกิดและตำแหน่งเริ่มต้น

อย่าเก็บผู้เล่นปัจจุบัน, เวลาเริ่ม Instance หรือผล Boss ลงใน ScriptableObject เพราะ
ScriptableObject เป็น Asset Definition ไม่ใช่ Runtime Save Data

## 4. Streaming และ Scene Structure

ตัวอย่างโครงสร้าง Scene:

``` text
Scenes/
├── Bootstrap/
│   └── Bootstrap.unity
├── Shared/
│   ├── CapitalCity.unity
│   ├── ForestZone.unity
│   ├── LakeVillage.unity
│   └── MountainZone.unity
├── Instances/
│   ├── Dungeon_AbandonedMine.unity
│   ├── Boss_SunkenTemple.unity
│   └── Arena_Colosseum.unity
└── UI/
    └── Frontend.unity
```

**แนวทางโหลด** 1. Bootstrap เริ่มระบบ Configuration, Authentication และ
Network 2. Client ขอเข้า World ผ่าน Gateway 3. Server ตรวจสิทธิ์และจัด
Zone/Instance 4. Client โหลด Scene ที่จำเป็น 5. Network สร้างหรือ Spawn
ตัวละครในตำแหน่งที่ Server กำหนด 6. เมื่อย้าย Zone ให้ทำ Transition และ Sync
สถานะก่อนปล่อย Scene เก่า

สำหรับ Open World ขนาดใหญ่ ให้เริ่มจาก Zone ขนาดเล็กที่เชื่อมต่อกันก่อน แล้วค่อยเพิ่ม
Streaming/Seamless Transition เมื่อมีข้อมูล profiling รองรับ

## 5. World Gameplay Loop

``` mermaid
flowchart LR
    Town[เมือง / เตรียมตัว] --> Explore[ออกสำรวจ]
    Explore --> Quest[เควสต์ / เก็บทรัพยากร]
    Explore --> Combat[ต่อสู้กับศัตรู]
    Combat --> Loot[รับ Loot / EXP]
    Quest --> Loot
    Loot --> Upgrade[อัปเกรดอุปกรณ์ / สกิล]
    Upgrade --> Town
    Explore --> Dungeon[เข้า Dungeon]
    Dungeon --> Boss[Boss Encounter]
    Boss --> Reward[รางวัลพิเศษ]
    Reward --> Town
```

กิจกรรมหลักที่เหมาะกับโลกแบบนี้: - เมือง: ซื้อขาย, Crafting, Repair, Party Finder,
Guild, Social - Open World: Quest, Gathering, Elite Monster, World
Event, World Boss - Dungeon: กลไก Boss, ห้องลับ, Loot Table, Challenge -
Arena: Duel, Ranked, Team Match, Tournament

------------------------------------------------------------------------

# Part B --- Multiplayer Architecture

## 6. ภาพรวม Client / Server

แนะนำให้แยก **Game Client**, **Dedicated Game Server** และ **Backend
Services** ออกจากกัน

``` mermaid
flowchart TB
    subgraph Clients["Game Clients"]
        C1[Player Client A]
        C2[Player Client B]
        C3[Player Client C]
    end

    subgraph Entry["Entry Layer"]
        Auth[Authentication]
        Gateway[World Gateway]
        Match[Matchmaking]
    end

    subgraph GameServers["Dedicated Game Servers"]
        Z1[Shared Zone Server]
        Z2[Dungeon Instance Server]
        Z3[PvP Match Server]
    end

    subgraph Backend["Backend Services"]
        Character[Character Service]
        Inventory[Inventory / Item Service]
        Progression[EXP / Mastery Service]
        Social[Party / Guild Service]
        Result[Match Result / Reward Service]
    end

    subgraph Storage["Storage"]
        DB[(PostgreSQL)]
        Cache[(Redis - Optional)]
    end

    C1 --> Auth
    C2 --> Auth
    C3 --> Auth
    Auth --> Gateway
    Gateway --> Z1
    Match --> Z3
    Gateway --> Z2
    C1 <--> Z1
    C2 <--> Z1
    C3 <--> Z2
    C1 <--> Z3
    C2 <--> Z3
    Z1 --> Character
    Z2 --> Progression
    Z3 --> Result
    Character --> DB
    Inventory --> DB
    Progression --> DB
    Result --> DB
    Social --> DB
    Gateway -.-> Cache
    Match -.-> Cache
```

ภาพนี้เป็น Logical Architecture ไม่ได้หมายความว่าทุก Service ต้องเป็น
Microservice ตั้งแต่วันแรก สำหรับทีมเล็กสามารถเริ่มจาก Modular Monolith ฝั่ง
Backend และแยก Dedicated Game Server ตามหน้าที่ก่อน

## 7. ขอบเขตความรับผิดชอบ

  ------------------------------------------------------------------------
  Component               รับผิดชอบ                  ไม่ควรทำ
  ----------------------- ------------------------ -----------------------
  Game Client             Input, Camera, UI,       ตัดสิน Damage/Reward
                          Animation, VFX, Audio    

  Dedicated Server        Movement Validation,     ทำ UI
                          Combat, AI, Match State  

  World Gateway           จัดเส้นทางเข้า Zone และ     คำนวณ Combat
                          Server                   

  Matchmaking             จับคู่ผู้เล่นและเลือก Region    แก้ไข Inventory

  Character Service       ข้อมูลตัวละครและสถานะถาวร   ควบคุม Hit Detection

  Inventory Service       Item, Equipment,         ตัดสินผลการต่อสู้
                          Currency                 

  Result Service          บันทึกผล Match และแจก      รับผลจาก Client
                          Reward                   โดยไม่ตรวจสอบ
  ------------------------------------------------------------------------

## 8. Server-authoritative Combat

Client ส่ง **Input Intent** ไปยัง Server เช่น กดโจมตี, หลบ, ป้องกัน ไม่ส่งคำสั่งว่า
"โจมตีโดน 500 Damage" แล้วให้ Server เชื่อ

``` mermaid
sequenceDiagram
    participant Client
    participant Server as Dedicated Server
    participant Combat as Combat Rules
    participant DB as Backend / Database

    Client->>Server: Attack Input + Sequence
    Server->>Server: Validate State / Cooldown / Stamina
    Server->>Combat: Resolve Attack
    Combat->>Combat: Validate Range / Hit / Defense
    Combat-->>Server: Damage / Stagger Result
    Server-->>Client: Replicate Combat Result
    Server->>DB: Persist Important Progress
```

Server ควรตรวจสอบอย่างน้อย: - ผู้เล่นอยู่ในสถานะที่โจมตีได้หรือไม่ - Cooldown และ
Stamina ถูกต้องหรือไม่ - ระยะและทิศทางการโจมตีสมเหตุสมผลหรือไม่ - Hitbox/Attack
Window อยู่ในช่วงเวลาที่อนุญาตหรือไม่ - เป้าหมายอยู่ใน Zone/Instance เดียวกันหรือไม่ -
ผลลัพธ์และ Reward ถูกสร้างโดย Server เท่านั้น

## 9. Combat Networking สำหรับ Third Person Soulslike

เกมแนว Soulslike ไวต่อ Latency โดยเฉพาะ Dodge และ Parry
จึงควรแยกการตอบสนองภาพจากการตัดสินผลจริง

  -----------------------------------------------------------------------
  ระบบ                    Client                  Server
  ----------------------- ----------------------- -----------------------
  Movement                Prediction /            ตรวจความเร็วและตำแหน่ง
                          Interpolation           

  Light/Heavy Attack      เล่น Animation ทันทีตาม    ตรวจ State และคำนวณ Hit
                          Prediction              

  Dodge                   เล่น Animation/Feedback  ตรวจช่วง I-frame
                          ทันที                     และผลหลบ

  Parry                   แสดง Feedback           ตัดสิน Timing และผล Parry
                          แบบคาดการณ์              

  Damage                  แสดงตัวเลขจากผลที่ได้รับ     เป็นผู้คำนวณจริง

  Loot/EXP                แสดง UI                 ตรวจสิทธิ์และบันทึกจริง
  -----------------------------------------------------------------------

**ข้อเสนอสำหรับ Prototype** - เริ่มจาก Server-authoritative แบบง่ายก่อน - ใช้
Client-side prediction เฉพาะ Movement และการตอบสนองที่จำเป็น - ใช้
Interpolation สำหรับตัวละครอื่น - เพิ่ม Lag Compensation/Rollback
เฉพาะเมื่อผลทดสอบแสดงว่าจำเป็น - ทดสอบที่ Ping 50, 100, 150 และ 200 ms
เพื่อหาจุดที่ Combat ยังเล่นได้

อย่าเปิดให้ Client ตัดสิน Perfect Dodge หรือ Parry เอง เพราะเป็นช่องทางโกงที่สำคัญ

## 10. Multiplayer Session Flow

``` mermaid
sequenceDiagram
    participant P as Party Leader
    participant MM as Matchmaking
    participant Alloc as Server Allocator
    participant GS as Game Server
    participant DB as Backend

    P->>MM: Request Dungeon / Arena
    MM->>MM: Validate Party / Rules
    MM->>Alloc: Request Instance
    Alloc->>GS: Allocate Server
    GS-->>Alloc: Server Ready
    Alloc-->>MM: Connection Ticket
    MM-->>P: Join Information
    P->>GS: Connect with Ticket
    GS->>DB: Validate Character Snapshot
    DB-->>GS: Character Data
    GS-->>P: Spawn / Start Session
```

Ticket ควรมีอายุสั้น ผูกกับผู้เล่น/Party/Instance และตรวจสอบฝั่ง Server
ห้ามใช้ข้อมูลจาก Client เป็นหลักฐานว่าได้รับอนุญาตให้เข้า Instance

## 11. Persistence และ Database Boundary

ข้อมูลที่ควรเก็บถาวร: - Account และ Character - Level, EXP, Stats -
Inventory, Equipment, Currency - Weapon Mastery และ Skill Unlock - Quest
Progress - PvP Rating, Match History, Season - Guild/Party Data
ตามขอบเขตเกม

ข้อมูลที่ไม่จำเป็นต้องบันทึกทุกเฟรม: - ตำแหน่งตัวละครทุกเฟรม - Animation State
ทุกเฟรม - Combat VFX - ข้อมูลชั่วคราวของ Encounter

ใช้ Save Checkpoint หรือ Event ที่สำคัญ เช่น เปลี่ยนอุปกรณ์, ได้ Item, จบ Quest, จบ
Match และใช้ Transaction/Idempotency กับ Reward เพื่อป้องกันการแจกซ้ำเมื่อ Retry

## 12. PvE / PvP Rules แยกกัน

ใช้ Interface สำหรับกฎการต่อสู้ที่ต่างกัน แทนการกระจาย `if (isPvP)` ไปทั่วระบบ

``` csharp
public interface ICombatRules
{
    float ModifyDamage(float baseDamage);
    float ModifyStunDuration(float duration);
    float ModifyHealing(float baseHealing);
}
```

ตัวอย่างแนวคิด: - `PveCombatRules`: ใช้ค่า Balance สำหรับมอนสเตอร์และ Boss -
`PvpCombatRules`: ใช้ PvP Damage/CC/Healing Modifier - `CombatResolver`:
รับ Rules ที่ถูกต้องจาก Match/Zone Context

Skill Definition ใช้ร่วมกันได้ แต่แยกค่าปรับ PvE/PvP เช่น Damage Multiplier,
Stun Duration, Healing Multiplier และ Debuff Duration

### Optional PvP Rules

-   ผู้เล่นเลือกเข้าร่วม PvP เอง
-   แพ้ PvP ไม่เสีย Item, EXP หรือ Weapon Mastery
-   Ranked Rating ลดได้ตามผลการแข่งขัน
-   Ranked ใช้ Normalized Stats เพื่อจำกัดความได้เปรียบจาก Gear/Level
-   Casual ใช้ Build จริงเพื่อทดลอง Build
-   ออกจาก Match หรือ AFK ใช้ Penalty แยกจากการแพ้ปกติ

------------------------------------------------------------------------

# Part C --- Game Design

## 13. Third Person Camera & Character Control

### Camera Composition

``` mermaid
flowchart TB
    Input[Player Input] --> Move[Character Movement]
    Input --> CameraInput[Camera Input]
    CameraInput --> CameraRig[Camera Rig]
    CameraRig --> Camera[Third Person Camera]
    Move --> Animation[Animation State]
    Move --> Facing[Character Facing]
    Facing --> Combat[Combat Direction]
    Camera --> Aim[Aim / Lock-on Direction]
    Aim --> Combat
```

แนะนำ: - ใช้ Cinemachine สำหรับ Camera Rig และ Camera Behaviours -
กล้องติดตามจากด้านหลังตัวละคร ปรับระยะ/ความสูงได้ - มี Lock-on สำหรับ Boss
และศัตรูสำคัญ - รองรับ Free Camera สำหรับสำรวจและ Lock-on Camera สำหรับ
Combat - แยก Camera Rotation ออกจาก Character Rotation -
ป้องกันกล้องทะลุฉากด้วย Collision/Obstruction Handling - มือถือควรมี
Sensitivity, Camera Assist และปุ่ม Lock-on ที่ปรับได้

### Control Modes

  Action         Keyboard / Mouse   Controller             Mobile
  -------------- ------------------ ---------------------- ---------------
  Move           WASD               Left Stick             Virtual Stick
  Camera         Mouse              Right Stick            Drag Area
  Light Attack   Left Click         Face Button            UI Button
  Heavy Attack   Right Click        Face Button            UI Button
  Dodge          Space              Shoulder/Face Button   UI Button
  Block          Hold Key           Trigger                Hold Button
  Lock-on        Middle Click       Stick Press            Target Button
  Interact       E                  Face Button            UI Prompt

ปุ่มจริงควรผ่าน Input Action Mapping เพื่อให้เปลี่ยน Layout
และรองรับอุปกรณ์หลายประเภทได้

## 14. Soulslike Combat Loop

``` mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Attacking: Light / Heavy
    Idle --> Dodging: Dodge
    Idle --> Blocking: Hold Block
    Blocking --> Parrying: Timed Input
    Attacking --> Recovery
    Dodging --> Recovery
    Parrying --> CounterWindow: Successful Parry
    CounterWindow --> Attacking: Counter Attack
    Blocking --> Staggered: Guard Broken
    Attacking --> Staggered: Interrupted
    Recovery --> Idle
    Staggered --> Recovery
```

### Combat Features

  Feature         Design Intent           หมายเหตุด้าน Network
  --------------- ----------------------- ---------------------------
  Light Attack    โจมตีเร็ว ใช้ Stamina ต่ำ   ตรวจ Attack Window
  Heavy Attack    ช้าแต่แรง/สะสม Stagger    ตรวจ Wind-up และ Recovery
  Dodge           หลบและ reposition       Server ตรวจ I-frame
  Perfect Dodge   หลบในจังหวะพอดี           Server ตรวจ Timing
  Block           ลด Damage แลก Stamina   Server คำนวณ Guard Damage
  Parry           สวนกลับเมื่อจับจังหวะถูก      Server ตัดสิน Timing
  Stagger         เปิดช่องให้โจมตีต่อ          Server คุม Stagger State
  Finisher        ลงโทษเป้าหมายที่เสียหลัก     ตรวจเงื่อนไขฝั่ง Server

### Combat State แยกจาก Animation

อย่าใช้ Animation State เป็นแหล่งข้อมูลหลักในการตัดสิน Combat เพราะ Animation
อาจช้าหรือเปลี่ยนตาม Client

แยก: - `CharacterState`: Idle, Moving, Dodging, Staggered -
`CombatState`: Ready, Windup, Active, Recovery - `AnimationController`:
แสดง Animation ตาม State - `CombatResolver`: คำนวณผลการต่อสู้

## 15. Class & Hybrid Weapon System

หลักการคือ **อาวุธกำหนดรูปแบบการต่อสู้ ส่วนอาชีพกำหนดเอกลักษณ์ตัวละคร**

  Class      Identity            Weapon Affinity
  ---------- ------------------- -------------------------
  Warrior    Guard / Counter     Greatsword, Shield
  Archer     Precision / Mark    Bow, Crossbow
  Mage       Element / Spell     Staff, Wand
  Assassin   Critical / Shadow   Daggers, Katana
  Cleric     Heal / Holy         Holy Staff, Mace
  Monk       Combo / Flow        Gauntlets, Spirit Claws

ทุกคลาสสามารถใช้อาวุธข้ามสายได้ แต่มี Affinity ต่างกัน เช่น: - ถนัดมาก:
โบนัสเริ่มต้นประมาณ +5--8% - ปานกลาง: +2--3% - นอกสาย: ไม่มีโบนัสพิเศษ

ค่าพวกนี้เป็นเพียงจุดเริ่มต้นสำหรับ Playtest ต้องปรับจากข้อมูลจริง
ไม่ควรทำให้การใช้อาวุธนอกสายไร้ประสิทธิภาพ

### Weapon Architecture

``` mermaid
flowchart TB
    Character[Player Character]
    Character --> Class[Class Definition]
    Character --> Loadout[Weapon Loadout]
    Character --> Skills[Skill Loadout]
    Loadout --> WeaponA[Primary Weapon]
    Loadout --> WeaponB[Secondary Weapon]
    WeaponA --> Moveset[Weapon Moveset]
    WeaponA --> Proficiency[Weapon Proficiency]
    Class --> Affinity[Weapon Affinity]
    Skills --> ClassSkills[Class Skills]
    Skills --> WeaponSkills[Weapon Skills]
    Skills --> Universal[Universal Skills]
    Moveset --> Combat[Combat Controller]
    Affinity --> Stats[Stat Calculator]
    Proficiency --> Unlocks[Mastery Unlocks]
```

### Suggested Weapon Set

-   Greatsword
-   Sword & Shield
-   Bow
-   Crossbow
-   Staff
-   Wand & Orb
-   Dual Daggers
-   Katana
-   Mace
-   Holy Staff
-   Gauntlets
-   Spirit Claws

เริ่ม Prototype ด้วย 4--6 อาวุธก่อน เช่น Greatsword, Sword & Shield, Bow,
Staff, Dual Daggers และ Gauntlets

### Weapon Proficiency

  Rank         Unlock Example
  ------------ -------------------------
  Novice       Basic Combo
  Apprentice   Advanced Combo
  Adept        Weapon Skill 1
  Expert       Weapon Skill 2
  Master       Mastery Skill / Passive

Weapon Mastery แยกจาก Character Level และบันทึกแยกตาม Weapon ID
การเปลี่ยนอาวุธไม่ทำให้ Mastery เดิมหาย

## 16. Unity Code Architecture

ใช้ Composition + ScriptableObject + Interface

``` text
Assets/Game/
├── Core/
│   ├── Bootstrap/
│   ├── Configuration/
│   └── Events/
├── World/
│   ├── Definitions/
│   ├── Zones/
│   ├── Instances/
│   ├── Streaming/
│   └── Spawn/
├── Character/
│   ├── Movement/
│   ├── Camera/
│   ├── Animation/
│   └── Interaction/
├── Combat/
│   ├── Core/
│   ├── States/
│   ├── Attacks/
│   ├── Defense/
│   ├── Damage/
│   └── Rules/
├── Classes/
│   ├── Definitions/
│   └── Runtime/
├── Weapons/
│   ├── Definitions/
│   ├── Movesets/
│   ├── Switching/
│   └── Proficiency/
├── Skills/
├── Progression/
├── AI/
├── Multiplayer/
│   ├── Networking/
│   ├── Replication/
│   ├── Prediction/
│   └── Session/
├── PvP/
│   ├── Duel/
│   ├── Arena/
│   ├── Matchmaking/
│   ├── Ranking/
│   └── Results/
├── UI/
└── Audio/
```

### Runtime Component Responsibilities

  Component           Responsibility
  ------------------- --------------------------------------
  PlayerCharacter     ประกอบ Component ของตัวละคร
  CharacterMotor      Movement และ Grounding
  CameraController    กล้องและ Lock-on
  ClassController     Class Identity และ Class Passive
  WeaponController    อาวุธที่ถือและ Moveset
  WeaponSwitcher      สลับอาวุธและ Transition
  WeaponProficiency   EXP/Rank ของอาวุธ
  SkillController     ตรวจและสั่งใช้ Skill
  CombatController    Orchestrate การโจมตี ไม่คำนวณทุกอย่างเอง
  CombatResolver      คำนวณ Damage/Defense/Hit
  StatCalculator      รวม Base Stats, Gear, Buff, Affinity
  NetworkCharacter    Network State และ Replication

**กฎป้องกัน God Object:** `CombatController` ควรประสานงานเท่านั้น ส่วน Damage,
Hit Detection, Stamina, Status Effect และ Cooldown ควรแยกเป็นระบบย่อย

### Definition vs Runtime vs Save Data

  Data Type    ตัวอย่าง                               ที่เก็บ
  ------------ ------------------------------------ --------------------
  Definition   WeaponDefinition, SkillDefinition    ScriptableObject
  Runtime      Current HP, Cooldown, Current Zone   Runtime Components
  Save Data    Level, EXP, Mastery, Inventory       Backend Database

ตัวอย่าง Save Model:

``` csharp
[Serializable]
public sealed class CharacterSaveData
{
    public string characterId;
    public string classId;
    public string primaryWeaponId;
    public string secondaryWeaponId;
    public int level;
    public long experience;
    public List<WeaponMasteryData> weaponMasteries;
    public List<string> unlockedSkillIds;
}

[Serializable]
public sealed class WeaponMasteryData
{
    public string weaponId;
    public int masteryLevel;
    public long masteryExp;
}
```

------------------------------------------------------------------------

# Part D --- Technical Decisions & Roadmap

## 17. Technology Recommendation

  ---------------------------------------------------------------------------
  Layer                   Initial Recommendation  หมายเหตุ
  ----------------------- ----------------------- ---------------------------
  Engine                  Unity + URP             รองรับ PC และวางแผน Mobile
                                                  ได้

  Language                C#                      ใช้ Composition และ
                                                  Data-driven Design

  Camera                  Cinemachine             Third Person / Lock-on

  Input                   Unity Input System      Keyboard, Controller,
                                                  Mobile

  Networking              NGO หรือ solution        ต้องทดสอบ scale และ latency
                          ที่เหมาะกับ Dedicated      
                          Server                  

  Game Server             Dedicated Server Build  Server เป็นผู้ตัดสิน Combat

  Backend                 ASP.NET Core Modular    เหมาะกับทีมที่ถนัด .NET
                          Monolith                

  Database                PostgreSQL              Character, Inventory,
                                                  Progression

  Cache                   Redis (Optional)        Session/Matchmaking/Cache
                                                  เมื่อจำเป็น

  Observability           Structured Logs +       ติดตาม Server และ Match
                          Metrics                 
  ---------------------------------------------------------------------------

เลือก Networking Stack หลังทำ Prototype และทดสอบจำนวนผู้เล่นต่อ Zone, Network
Tick, Bandwidth และ Latency จริง อย่าตัดสินจากชื่อ Package อย่างเดียว

## 18. Roadmap ที่แนะนำ

``` mermaid
flowchart TD
    P1[Phase 1<br/>Offline Combat Prototype]
    P2[Phase 2<br/>Third Person Controller]
    P3[Phase 3<br/>Weapon / Skill / Progression]
    P4[Phase 4<br/>Small Shared Zone]
    P5[Phase 5<br/>Dedicated Server & Sync]
    P6[Phase 6<br/>Dungeon Instance]
    P7[Phase 7<br/>Duel / PvP Arena]
    P8[Phase 8<br/>Backend Persistence]
    P9[Phase 9<br/>Expand World & Content]

    P1 --> P2 --> P3 --> P4 --> P5 --> P6 --> P7 --> P8 --> P9
```

### Phase Detail

**Phase 1 --- Offline Combat Prototype** - Third Person Movement -
Camera Follow / Lock-on - Light / Heavy Attack - Dodge / Block / Parry -
Stamina / Stagger - Warrior + Greatsword

**Phase 2 --- Weapon & Class Foundation** - Weapon Definition - Weapon
Moveset - Weapon Switching - Class Affinity - Weapon Proficiency - เพิ่ม
Staff เพื่อทดสอบ Cross-class Build

**Phase 3 --- Small Shared Zone** - เมืองขนาดเล็ก - NPC และ Interaction -
Quest และ Enemy Spawn - Zone Transition

**Phase 4 --- Multiplayer Foundation** - Dedicated Server Build -
Connection / Authentication - Network Movement - Combat Replication -
Server-authoritative Damage

**Phase 5 --- Dungeon Instance** - Party - Instance Allocation - Boss
Encounter - Reward Persistence

**Phase 6 --- Optional PvP** - Duel - Arena 3v3 - Ranked Rating -
Normalized Stats - Disconnect / AFK Penalty

**Phase 7 --- Backend & Expansion** - Character Persistence - Inventory
/ Economy - More Zones - More Classes / Weapons - Monitoring / Load
Testing

## 19. ขอบเขต MVP ที่ควรทำจริงก่อน

อย่าเริ่มด้วย MMORPG ขนาดใหญ่ ให้สร้าง Vertical Slice ที่พิสูจน์ Core Loop ก่อน

**MVP แนะนำ** - 1 เมืองเล็ก - 1 Open World Zone - 1 Dungeon - 1 Boss - 2
คลาส (Warrior, Mage) - 4 อาวุธ (Greatsword, Sword & Shield, Staff, Bow) -
1--4 ผู้เล่นใน PvE Instance - 1 รูปแบบ Duel สำหรับทดสอบ PvP ภายหลัง - ระบบ
EXP, Loot และ Weapon Mastery ขั้นพื้นฐาน

เกณฑ์ผ่าน MVP: - ผู้เล่นเดินทางจากเมืองไปพื้นที่สำรวจและ Dungeon ได้ - Server
เป็นผู้ตัดสิน Damage และ Reward - ออกจากเกมแล้วข้อมูลตัวละครไม่หาย - Combat เล่นได้ที่
Latency ที่กำหนด - เปลี่ยนอาวุธแล้ว Skill/Moveset/Proficiency ทำงานถูกต้อง - PvE
และ PvP ใช้ Skill Definition ร่วมกันแต่ใช้ Balance Rules คนละชุด

## 20. ความเสี่ยงสำคัญ

  -----------------------------------------------------------------------
  ความเสี่ยง                ผลกระทบ                 วิธีลดความเสี่ยง
  ----------------------- ----------------------- -----------------------
  เริ่มทำ Open World        Scope บานและ            เริ่มจาก Zone เล็ก
  ใหญ่เกินไป                Performance ต่ำ          

  Client-authoritative    Cheat / Desync          Server ตรวจผลสำคัญ
  Combat                                          

  Combat ไวต่อ Ping        Dodge/Parry ไม่ยุติธรรม    Latency Testing และ
                                                  Prediction

  CombatController        แก้ยากและทดสอบยาก        แยก
  ใหญ่เกินไป                                        Resolver/State/Effect

  ทุกระบบเป็น Microservice  ภาระ Deploy/Debug สูง    เริ่ม Modular Monolith
  เร็วเกินไป                                        

  อาวุธและคลาสไม่สมดุล       Build บางแบบไร้ประโยชน์   Playtest และ Telemetry

  บันทึก Reward ซ้ำ          เศรษฐกิจเกมเสีย           Transaction +
                                                  Idempotency
  -----------------------------------------------------------------------

------------------------------------------------------------------------

## 21. คำศัพท์สำคัญ

  คำ                     ความหมาย
  ---------------------- -----------------------------------------
  Shared Zone            พื้นที่ที่ผู้เล่นหลายคนอยู่ร่วมกัน
  Instance               พื้นที่แยกเฉพาะกลุ่ม/กิจกรรม
  Dedicated Server       Server ที่รันเกมโดยไม่มีผู้เล่นควบคุมตัวละคร
  Server-authoritative   Server เป็นผู้ตัดสินสถานะจริง
  Replication            ส่งสถานะเกมไปยัง Client
  Prediction             Client คาดการณ์การตอบสนองเพื่อลดความหน่วง
  Interpolation          ทำให้การเคลื่อนไหวระหว่างข้อมูล Network ดูลื่น
  Lag Compensation       ชดเชยความหน่วงในการตรวจผลการกระทำ
  Persistence            การเก็บข้อมูลให้คงอยู่หลังออกจากเกม
  Idempotency            การทำคำขอซ้ำแล้วไม่สร้างผลซ้ำ
  Vertical Slice         ตัวอย่างเกมขนาดเล็กที่ครบวงจรและพิสูจน์แนวทางได้

------------------------------------------------------------------------

## 22. ข้อสรุปการออกแบบ

แนวทางที่เหมาะกับโปรเจกต์นี้คือ:

1.  **Hybrid World:** เมืองและพื้นที่สำรวจเป็น Shared Zone; Dungeon/Boss/Arena
    เป็น Instance
2.  **Third Person:** Free Camera สำหรับสำรวจ และ Lock-on สำหรับ Soulslike
    Combat
3.  **Dedicated Server:** Server เป็นผู้ตัดสิน Combat, Loot, EXP, Match
    Result
4.  **Modular Backend:** เริ่ม ASP.NET Core Modular Monolith + PostgreSQL
    ก่อนแยก Service เมื่อจำเป็น
5.  **Composition Architecture:** ScriptableObject สำหรับ Definition,
    Runtime Component สำหรับสถานะ, Database สำหรับ Save
6.  **Optional PvP:** PvE เต็มรูปแบบโดยไม่ต้อง PvP และแยก Balance Rules
7.  **Small Vertical Slice:** ทำเมือง 1 โซน Dungeon 1 แห่ง และ Boss 1
    ตัวให้เล่นจบวงจรก่อนขยาย

**หลักคิดสุดท้าย:** ออกแบบให้ขยายได้ แต่ลงมือสร้างเฉพาะสิ่งที่จำเป็นต่อ Vertical Slice
ก่อน สถาปัตยกรรมที่ดีไม่ใช่ระบบที่ซับซ้อนที่สุด แต่คือระบบที่ทีมพัฒนาและดูแลต่อได้จริง
