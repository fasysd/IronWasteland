# Iron Wasteland — Cấu Trúc Dự Án & Chức Năng Từng Phần

> **Mục đích:** Bản đồ đầy đủ của repo: mỗi thư mục, mỗi file C# làm gì, ai gọi ai, prefab/scene nào đang dùng, phần nào đã xong — phần nào còn trống.
> **Đối tượng:** lập trình viên mới vào dự án, reviewer, và AI agent trước khi sửa code.
> **Cập nhật:** 2026-11-14 · **Phạm vi:** 148 file C# / 26.674 dòng trong `Assets/Scripts`, toàn bộ `Assets/Prefabs`, `Assets/Scenes`, `Assets/Settings`, `Packages`, `ProjectSettings`.

**Bốn tài liệu, bốn nhiệm vụ khác nhau — đừng trộn:**

| Tài liệu | Trả lời câu hỏi |
|---|---|
| [`docs/PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) | **Luật chơi & quy ước**: công thức sát thương, hệ khiên, CC, hiệu ứng, 8 kỹ năng mỗi xe, chỉ số GDD. Là "hiến pháp" của code. |
| **`docs/PROJECT_STRUCTURE.md`** (file này) | **Bản đồ code**: file nào ở đâu, class nào làm gì, nối với prefab nào. |
| [`PROJECT_OVERVIEW.md`](../PROJECT_OVERVIEW.md) | Bản tổng quan kiến trúc cũ (sinh từ knowledge-graph). Nhiều mục đã lạc hậu — xem ghi chú ở §12. |
| [`docs/UNITY_CO_BAN.md`](UNITY_CO_BAN.md) | **Nhập môn Unity**: cửa sổ, 10 thao tác thực tế, vòng lặp làm việc, debug, 5 lỗi tân binh hay mắc. |

---

## 1. Snapshot kỹ thuật

| Hạng mục | Giá trị |
|---|---|
| Engine | **Unity 6000.5.6f1** (`ProjectSettings/ProjectVersion.txt`) |
| Render pipeline | **URP 2D** — `com.unity.render-pipelines.universal` 17.6.0, profile ở `Assets/Settings/` |
| Input | **Unity Input System** 1.20.0, nhưng gameplay hiện đọc `Keyboard.current` / `Mouse.current` trực tiếp |
| UI | uGUI 2.5.0 + TextMesh Pro (có sẵn `Assets/TextMesh Pro`). **Chưa có Canvas/HUD nào trong scene** |
| 2D | 2D Animation 15.1.0, Aseprite 5.0.3, PSD Importer 14.0.3, SpriteShape 15.0.3, Tilemap (+Extras) |
| Test | `com.unity.test-framework` 1.7.0 có trong manifest nhưng **chưa có test NUnit/asmdef** — xem §8.3 |
| Khác | `com.ivanmurzak.unity.mcp` 0.93.3 (MCP server cho AI/automation), Visual Scripting 1.9.11, Timeline 1.8.12 |
| Assembly | **Không có `.asmdef`** → toàn bộ code vào `Assembly-CSharp`; thư mục `Assets/Scripts/Editor` tự thành assembly Editor-only |
| Layer dùng trong game | `Tank` (index 6), `Enemy` (7), `Terrain` (8) — khai báo trong `ProjectSettings/TagManager.asset` |
| Scene trong Build Settings | **Chỉ `Assets/Scenes/SampleScene.unity`** (enabled). `MainScene.unity` không nằm trong build |
| Số liệu code | 148 file `.cs`, 26.674 dòng; 6 xe tăng, 12 quái thường (bộ mới) + 5 bản legacy + 2 boss có logic, 2 file hệ thống màn chơi |

---

## 2. Kiến trúc 3 lớp (bất biến)

```
┌──────────────────────────── DATA ────────────────────────────┐
│ TankDefinition (SO) · TankStats · StageDefinition (SO)        │
│ EquipmentDatabase (SO) · EnemyStats (serialize trên prefab)   │
└──────────────────────────────┬───────────────────────────────┘
                               │ Initialize(context, stats)
┌───────────────────────── LOGIC / STATE ──────────────────────┐
│ TankBrain (abstract) → TankBrain_<Xe>                         │
│ EnemyBrain (abstract) → NormalEnemy_* / EliteEnemyBrain       │
│ StatusEffect · ShieldEffect · EquipmentBrain · StageManager    │
│ KHÔNG đọc Input, KHÔNG giữ SpriteRenderer/Animator            │
└──────────────────────────────┬───────────────────────────────┘
                               │ Move / Look / UseSkill + event va chạm
┌────────────────────── VISUAL & PHYSICAL ─────────────────────┐
│ TankView (prefab riêng, spawn làm con lúc runtime)            │
│ EnemyModel (con trong cùng prefab quái)                       │
│ ColliderOwnerRef · Collider2D · Rigidbody2D · Animator        │
│ KHÔNG tính sát thương; chỉ hiển thị + forward va chạm         │
└──────────────────────────────────────────────────────────────┘
```

Ba quy tắc tóm gọn (chi tiết + công thức: [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) §2–§4):

1. **Input chỉ ở lớp ngoài** — `GameController` gọi `SetMoveInput` / `SetLookTarget` / `UseSkill`; Brain không biết bàn phím là gì.
2. **Chỉ số không sửa tay** — mọi buff đi qua `AddBonusStats(key, flat, percent)`; `Final = Base × (1 + Percent) + Flat`.
3. **Sát thương đi một cửa** — `CombatUtility.ResolveDamage` + `DamageInfo`; đạn/AoE/text phải pool, không `Instantiate`/`Destroy` trong combat.

---

## 3. Bản đồ thư mục

```
IronWasteland/
├── Assets/
│   ├── Art/
│   │   ├── Tank/                     Sprite, Animations, VFX của 6 xe (Sprites/{GFSkill,RedNova,skill,
│   │   │                             WallTitan,WBSKILL}, VFX/{RedNova,WallTitan})
│   │   └── Enemies/<TênQuái>/        Mỗi quái 1 thư mục: sprite + Animations/ + Concept/
│   ├── Editor/                       (trống — xem Assets/Scripts/Editor)
│   ├── Plugins/NuGet/                Thư viện DLL kéo từ NuGet
│   ├── Prefabs/
│   │   ├── Tanks/<Xe>/               <Xe>.prefab (Brain) + <Xe>_View.prefab (View) + <Xe>.asset (TankDefinition)
│   │   │                             + prefab skill/VFX riêng của xe (BlackShadow, GreenFox, WaterB…)
│   │   ├── Equipment/                Defense_Heavy, Firepower_Cannon, Mobility_Heavy (prefab phụ kiện cũ)
│   │   ├── Enemies/                  5 prefab boss: BongMaTuDia, SEAUBEARUS, TuThanNguyenTo,
│   │   │                             TruongQuanNgucD1ONK, Enemy_Elite
│   │   ├── Enemies/Normal/           12 quái thường (bộ NormalEnemy_*)
│   │   └── Enemies/ChapterOne/       7 prefab chương 1 (5 quái + 2 boss có logic)
│   ├── Scenes/
│   │   ├── SampleScene.unity         Scene duy nhất trong build: GameController + PathfindingGrid2D + terrain test
│   │   └── MainScene.unity           Scene debug: GameController + quái test (không có trong build)
│   ├── Scripts/                      Toàn bộ code — chi tiết §4–§8
│   ├── Settings/                     URP asset, Renderer2D, VolumeProfile, EquipmentDatabase.asset
│   └── TextMesh Pro/                 Tài nguyên TMP (font, shader, material)
├── docs/
│   ├── PROJECT_CONTEXT.md            Luật & công thức (bản chuẩn cho AI/dev)
│   ├── PROJECT_STRUCTURE.md          File này
│   ├── UNITY_CO_BAN.md               Nhập môn Unity cho người mới (cửa sổ, thao tác, debug)
│   ├── Tài liệu thiết kế Game.txt    Bản text của GDD
│   └── figma_wireframes/             6 wireframe SVG (menu, stage select, garage, HUD, settings, result)
│                                     + LAYOUT_SPECIFICATION_GUIDE.md
├── Packages/manifest.json            Package list (§1)
├── ProjectSettings/                  Unity settings (version, layers, build scenes)
├── Tài liệu thiết kế Game.docx       GDD gốc (26 MB, bản Word đầy đủ)
├── DesignGuide_TankBrain_EnemyBrain_WithIndex.md.docx   Design guide dạng Word (có mục lục)
├── PROJECT_CONTEXT.md / PROJECT_OVERVIEW.md             Bản sao/tổng quan ở root
└── IronWasteland.slnx, Assembly-CSharp*.csproj          File solution do Unity sinh
```

### 3.1 Cây `Assets/Scripts` (148 file)

```
Scripts/
├── Core/            7 file   Nền tảng combat & định danh (CombatUtility, DamageInfo, OwnerRef…)
│   └── Effects/     7 file   StatusEffect + Burn/Shield/StatModifier/Intercept/Buff
├── Tanks/          12 file   Framework tank (TankBrain, TankView, Definition, Stats, Controller…)
│   ├── Equipment/   3 file   1 ô phụ kiện: EquipmentBrain, Database, Accessory_Test
│   ├── BlackShadow/11 file
│   ├── RedNova/     5 file
│   ├── WallTitan/   6 file
│   ├── GreenFox/   16 file
│   ├── Leviathan/   9 file
│   ├── WaterB/     15 file
│   └── Test/        1 file   TankBrain_Test (rỗng, dùng cho Tank.prefab)
├── Enemies/        17 file   Framework quái + bộ legacy chương 1 + 2 boss có logic + 1 boss khung
│   ├── Navigation/  2 file   A* grid + movement agent
│   └── Normal/     28 file   Quái thường đời mới (3 Base + 12 quái + 13 zone/skill/projectile)
├── Stages/          2 file   StageDefinition (SO) + StageManager (chạy đợt quái)
├── Tests/           3 file   SkillArena + 2 bộ self-check chạy tay trong Editor
├── Utilities/       3 file   Collider2DUtility, DamageTestTrigger, SpriteFactory
└── Editor/          1 file   ReadOnlyDrawer
```

---

## 4. Tầng Core — `Assets/Scripts/Core` (14 file)

Nền tảng dùng chung cho cả tank và quái. **Sửa ở đây là sửa toàn game** — đọc kỹ trước khi đổi.

| File | Class / kiểu | Chức năng | API chính |
|---|---|---|---|
| [`CombatUtility.cs`](../Assets/Scripts/Core/CombatUtility.cs) | `static CombatUtility` | Toàn bộ toán combat theo GDD: sát thương gốc, Def hiệu dụng, 4 đoạn logarit giảm sát thương, sát thương cuối | `CalculateTankRawDamage`, `CalculateEnemyRawDamage`, `CalculateEffectiveDefense`, `CalculateDamageReductionPercent`, `ResolveDamage` |
| [`DamageInfo.cs`](../Assets/Scripts/Core/DamageInfo.cs) | `struct DamageInfo` | Gói dữ liệu một đòn đánh đi xuyên pipeline | `Attacker`, `Target`, `RawDamage`, `FinalDamage`, `AbsorbedByShield`, `HealthDamage`, `ArmorPenetration`, `HitPosition`, 3 cờ `IsDirectToHealth` / `IsRedirected` / `IsDamageOverTime` |
| [`CrowdControlType.cs`](../Assets/Scripts/Core/CrowdControlType.cs) | `enum CrowdControlType` + `class ActiveCrowdControl` | 5 loại khống chế và bản ghi runtime (thời gian, cường độ) | `None/Root/Silence/Stun/Slow`(+`Knockback`), `Duration`, `RemainingTime`, `Strength` |
| [`OwnerRef.cs`](../Assets/Scripts/Core/OwnerRef.cs) | `class OwnerRef : MonoBehaviour` | "Vật này thuộc về ai" — trả về `UnityEngine.Object` bất kỳ (TankBrain, EnemyBrain, PlayerData…) | `Owner`, `HasExplicitOwner`, `RegisterOwner`, `TryGetOwner<T>` |
| [`ColliderOwnerRef.cs`](../Assets/Scripts/Core/ColliderOwnerRef.cs) | `class ColliderOwnerRef : OwnerRef` | Cầu nối **Collider2D → chủ sở hữu** + gom toàn bộ event va chạm/trigger, giữ danh sách collider đang chạm (chống sót `OnTriggerExit`) | `FromCollider`, `FromHit`, `GetContactOwner<T>`, `TryGetOtherOwner<T>`, `TriggerEnter/Stay/Exit`, `CollisionEnter/Stay/Exit`, `Contact`, `TryGetApproximateOverlapCenter`, `TryGetRandomOverlapPoint` |
| [`TextDamageManager.cs`](../Assets/Scripts/Core/TextDamageManager.cs) | `class TextDamageManager` (singleton) | Pool text sát thương bay (TMP world space), zero-GC trong combat | `Instance`, `Show(float damage, Vector3 worldPos)` |
| [`ReadOnlyAttribute.cs`](../Assets/Scripts/Core/ReadOnlyAttribute.cs) | `ReadOnlyAttribute` | Khoá field trên Inspector (dùng cho `baseStats`, `finalStats`, `currentHp`…) | `[ReadOnly]` |
| [`Effects/StatusEffect.cs`](../Assets/Scripts/Core/Effects/StatusEffect.cs) | `enum EffectStackPolicy` + `abstract StatusEffect` | Khung hiệu ứng kéo dài: thời gian, tick, stack, vòng đời | `duration/remainingTime/tickInterval/currentStacks/maxStacks`, `UpdateTick`, hook `OnApplied/OnTick/OnStackAdded/OnRemoved` |
| [`Effects/ShieldEffect.cs`](../Assets/Scripts/Core/Effects/ShieldEffect.cs) | `ShieldEffect : StatusEffect` | Khiên có trữ lượng, nhiều lớp, giới hạn số đòn | `ShieldAmount`, `RemainingShield`, `MaxAbsorbHits`, `AbsorbHits`, `Source` |
| [`Effects/BurnEffect.cs`](../Assets/Scripts/Core/Effects/BurnEffect.cs) | `BurnEffect : StatusEffect` | DoT thiêu đốt theo % Attack mỗi giây, stack `CapOnly` | `tickDamage`, `maxStacks` |
| [`Effects/StatModifierEffect.cs`](../Assets/Scripts/Core/Effects/StatModifierEffect.cs) | `StatModifierEffect : StatusEffect` | Buff/debuff chỉ số có thời hạn (không đụng `baseStats`) | `flatBonus`, `percentBonus` |
| [`Effects/EnemyAttackBuffEffect.cs`](../Assets/Scripts/Core/Effects/EnemyAttackBuffEffect.cs) | `EnemyAttackBuffEffect : StatusEffect` | Buff Attack cho quái trong khoảng thời gian | `attackBonus` |
| [`Effects/IDamageInterceptor.cs`](../Assets/Scripts/Core/Effects/IDamageInterceptor.cs) | `interface IDamageInterceptor` | Cơ chế **chặn sạch một đòn** (né, đỡ đòn, bất tử tạm thời) — khác khiên vì không có trữ lượng | `TryIntercept(DamageInfo)` |
| [`Effects/DamageInterceptEffect.cs`](../Assets/Scripts/Core/Effects/DamageInterceptEffect.cs) | `DamageInterceptEffect : StatusEffect, IDamageInterceptor` | Bản dựng sẵn: đỡ N đòn, cờ `BlockDamageOverTime` / `BlockRedirected`, event khi chặn thành công | `MaxNegateHits`, `NegatedHits`, `OnNegated` |

**Ai dùng:** `CombatUtility` ← `TankBrain.TakeDamage`, `EnemyBrain.TakeDamage`, mọi skill. `ColliderOwnerRef` ← mọi prefab đạn/vùng/quái. `TextDamageManager` ← skill khi gây sát thương (cần 1 object trong scene — `SampleScene` đã có).

---

## 5. Tầng Xe tăng — `Assets/Scripts/Tanks`

### 5.1 Framework (12 file gốc)

| File | Class | Chức năng | Điểm cần biết |
|---|---|---|---|
| [`TankBrain.cs`](../Assets/Scripts/Tanks/TankBrain.cs) | `abstract TankBrain` | **Trái tim runtime của tank**: máu/năng lượng/khiên, 4 cooldown, CC, status effect, 1 phụ kiện, pipeline sát thương, di chuyển, nhìn, dùng chiêu | `SkillCount = 4`; abstract `GetBaseCooldown`; virtual `OnUseSkill`, `GetSkillEnergyCost`, `GetSkillEnergyGain`, `OnBrainTick`; event `OnTakeDamageEvent`, `OnDealDamageEvent`, `OnHealEvent`, `OnSkillUsedEvent`, `OnShieldChangedEvent`, `OnShieldAbsorbedEvent`, `OnDeathEvent` |
| [`TankView.cs`](../Assets/Scripts/Tanks/TankView.cs) | `class TankView` + `enum TankAction` | Visual & vật lý: Animator, pivot thân/nòng, muzzle, collider; nhận lệnh `Move/Look/UseSkill/Stop` | Không xoay logic; `RegisterOwner`, `CollectOwnerRefs`, `PropagateOwner`, `GetOwnerRef` |
| [`TankDefinition.cs`](../Assets/Scripts/Tanks/TankDefinition.cs) | `TankDefinition : ScriptableObject` | Dữ liệu tĩnh 1 dòng xe: 2 mốc chỉ số cấp 1 & 90, prefab Brain, mảng prefab View, EquipmentDatabase, context | `CreateTank(context)`, `GetStatsForLevel(level)` (nội suy tuyến tính), `MinLevel=1`, `MaxLevel=90`; menu `Create > IronWasteland > Tank > Tank Definition` |
| [`TankBrainContext.cs`](../Assets/Scripts/Tanks/TankBrainContext.cs) | `struct TankBrainContext` | Tham số khởi tạo: `Level`, `IdView`, `EquipmentId`, `OptionSkill1`, `OptionSkill2` | Chính là chỗ chọn nhánh kỹ năng tùy chọn |
| [`TankStats.cs`](../Assets/Scripts/Tanks/TankStats.cs) | `class TankStats` | Bảng chỉ số: Attack, Defense, DamageRate, ArmorPenetration, MaxHealth, MaxEnergy, MoveSpeed, CooldownReduction | `CalculateFinalStats`, `Add/AddTo/Subtract/Clone/Zeroed`, cap `MaxCooldownReduction=0.6`, `MaxArmorPenetration=1` |
| [`TankUpgradeUtility.cs`](../Assets/Scripts/Tanks/TankUpgradeUtility.cs) | `static TankUpgradeUtility` | Chi phí "Ngọc ma pháp" để lên cấp 1→90: mỗi cấp 5/10/15/20/25 ngọc theo đoạn 2-20 / 21-40 / 41-60 / 61-80 / 81-90 | `GetCostForNextLevel(currentLevel)`, `GetUpgradeCost(startLevel, targetLevel)` |
| [`GameController.cs`](../Assets/Scripts/Tanks/GameController.cs) | `class GameController` | Điểm vào gameplay: dựng `TankBrainContext`, gọi `TankDefinition.CreateTank`, đọc input và chuyển lệnh | `PlayerTank`; WASD/mũi tên di chuyển, chuột ngắm, `J/K/L/I` = slot 0/1/2/3 |
| [`TankSlowDebuff.cs`](../Assets/Scripts/Tanks/TankSlowDebuff.cs) | `class TankSlowDebuff` | Làm chậm tạm thời qua bonus stats (không sửa logic di chuyển) | `TankSlowDebuff.Apply(tank, slowPercent, duration)` |
| [`Tank_tesster.cs`](../Assets/Scripts/Tanks/Tank_tesster.cs) | `class TankTester` | Script test gắn trong `MainScene` | — |
| [`IDamageRedirector.cs`](../Assets/Scripts/Tanks/IDamageRedirector.cs) | `interface IDamageRedirector` | Cơ chế **gánh hộ sát thương** cho đồng minh (Guardian Current/Wall, Earth Guardian…) | `TryRedirect(DamageInfo, out protector, out sharedAmount)` |
| [`ISkillBranchConfigurable.cs`](../Assets/Scripts/Tanks/ISkillBranchConfigurable.cs) | `interface ISkillBranchConfigurable` | Đổi nhánh option 1/2 ngay lúc chạy (dùng cho `SkillArena`) | `Option1Branch`, `Option2Branch` |
| [`DebugDame.cs`](../Assets/Scripts/Tanks/DebugDame.cs) | `class WaterB_ShieldDebug` (namespace `Tanks.WaterB`) | Debug Bubble Shield: tự gây sát thương mỗi `tickInterval` giây (`autoDamage`, `damagePerTick`) và hiện text sát thương |

### 5.2 Trang bị — `Tanks/Equipment` (3 file)

| File | Class | Chức năng |
|---|---|---|
| [`EquipmentBrain.cs`](../Assets/Scripts/Tanks/Equipment/EquipmentBrain.cs) | `EquipmentBrain : MonoBehaviour` | Lớp cha 1 phụ kiện: flat/percent bonus + hook passive. **Mỗi tank đúng 1 ô** |
| [`EquipmentDatabase.cs`](../Assets/Scripts/Tanks/Equipment/EquipmentDatabase.cs) | `EquipmentDatabase : ScriptableObject` | Kho prefab phụ kiện; **ID = index** trong `accessoryPrefabs`. ⚠ Asset thật `Assets/Settings/EquipmentDatabase.asset` vẫn serialize theo 3 danh sách cũ (`firepowerPrefabs` / `defensePrefabs` / `mobilityPrefabs`) nên `accessoryPrefabs` đang rỗng — phải nhập lại danh sách phụ kiện |
| [`Accessory_Test.cs`](../Assets/Scripts/Tanks/Equipment/Accessory_Test.cs) | `Accessory_Test : EquipmentBrain` | Phụ kiện mẫu **chỉ log** (khi trang bị, khi chủ gây sát thương) — dùng để kiểm tra hook của hệ 1 ô, không cộng chỉ số |

Hook của `EquipmentBrain`: `Equip(TankBrain)`, `UnEquip(TankBrain)`, `OnEquipped`, `OnUnequipped`, `OnBrainTick`, `OnOwnerTookDamage`, `OnOwnerDealtDamage`, `OnOwnerUsedSkill`.

### 5.3 Sáu xe tăng — ma trận 4 slot

Mỗi xe có **8 kỹ năng tồn tại** nhưng chỉ **4 slot đang gắn**: slot 0 thường, slot 1 chuyên biệt, slot 2 = chọn 1 trong 3 option 1, slot 3 = chọn 1 trong 3 option 2. Nhánh được chọn bằng `TankBrainContext.OptionSkill1/2`.

| Xe | Slot 0 (thường) | Slot 1 (chuyên biệt) | Slot 2 — Option 1 (0/1/2) | Slot 3 — Option 2 (0/1/2) |
|---|---|---|---|---|
| **Black Shadow** | Shadow Claw | Shadow Step (2 pha: đặt ảo ảnh → đổi chỗ/nổ) | Shadow Predator / Shadow Link / Shadow Guard | Shadow Ambush / Shadow Possession / Shadow Exchange |
| **Red Nova** | Nova Shot | Triple Nova | Overdrive / Laser Cannon / Execution Core | Nova Barrage / Nova Pierce / Nova Rush |
| **Wall Titan** | Earth Break | Earth Wall | Stone Fortress / Guardian Wall / Earth Prison | Immovable Titan / Earth Guardian / Earthquake |
| **Green Fox** | Fox Fang | Fox Mark (gắn ấn) | Mark Hunter / Burning Fox / Fox Trap | Mark Explosion / Fox Territory / Nine-Tailed Assault |
| **Leviathan** | Water Pulse | Water Bubble (nhốt địch) | Water Armor / Guardian Current / Regenerative Shell | Tidal Return / Leviathan Form / Tide Commander |
| **Water B** | Healing Water | Bubble Shield | Shared Healing / Life Drain / Bubble Protector | Mobile Altar / Water Turrets / Aqua Assault |

> Số liệu năng lượng/hồi chiêu/sát thương của từng chiêu nằm ngay trong `[Header]`/`[Tooltip]` của TankBrain tương ứng; bảng GDD gốc ở [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) §10.2.

#### 5.3.1 Black Shadow — `Tanks/BlackShadow` (11 file)

| File | Class | Chức năng |
|---|---|---|
| [`TankBrain_BlackShadow.cs`](../Assets/Scripts/Tanks/BlackShadow/TankBrain_BlackShadow.cs) | `TankBrain_BlackShadow : TankBrain` | Điều phối 4 slot, pool đạn/ảo ảnh, trạng thái cường hóa Shadow Predator, tính sát thương & hồi năng lượng |
| [`NormalSkill.cs`](../Assets/Scripts/Tanks/BlackShadow/NormalSkill.cs) | `NormalSkill` | Đạn Shadow Claw: bay theo nòng, xuyên/va chạm, trả event `TargetFound`; skill không tự trừ máu |
| [`SpecializedSkill.cs`](../Assets/Scripts/Tanks/BlackShadow/SpecializedSkill.cs) | `SpecializedSkill` | Ảo ảnh Shadow Step 2 pha (đặt → đổi vị trí + nổ) |
| [`OptionalSkill1.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill1.cs) | `OptionalSkill1` | Shadow Predator: cường hóa ATK, đạn to hơn, hạ gục hồi cooldown |
| [`OptionalSkill1_2.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill1_2.cs) | `OptionalSkill1_2` | Shadow Link: nối đồng minh, +25% speed cả hai, −20% CD cho đồng minh |
| [`OptionalSkill1_3.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill1_3.cs) | `OptionalSkill1_3` | Shadow Guard: bóng vệ tinh triệt tiêu đòn trong 3s |
| [`OptionalSkill1Orb.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill1Orb.cs) | `OptionalSkill1Orb` | Tàn hỏa đen bay về xe khi Predator bật |
| [`OptionalSkill2.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill2.cs) | `OptionalSkill2` | Shadow Ambush: dịch chuyển sau lưng địch, 200% ATK |
| [`OptionalSkill2_1.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill2_1.cs) | `OptionalSkill2_1` | Shadow Possession: nhập vào đồng minh 10s, miễn chỉ định/sát thương, +35% speed |
| [`OptionalSkill2_2.cs`](../Assets/Scripts/Tanks/BlackShadow/OptionalSkill2_2.cs) | `OptionalSkill2_2` | Shadow Exchange: tạo bản sao hút đòn (tối đa 3 đòn) → hoán đổi vị trí |
| [`ShadowExchangeClone.cs`](../Assets/Scripts/Tanks/BlackShadow/ShadowExchangeClone.cs) | `ShadowExchangeClone` | Thực thể bản sao của Shadow Exchange (máu = 50% HP đồng minh, 3 đòn, 10s) |

#### 5.3.2 Red Nova — `Tanks/RedNova` (5 file)

| File | Class | Chức năng |
|---|---|---|
| [`TankBrain_RedNova.cs`](../Assets/Scripts/Tanks/RedNova/TankBrain_RedNova.cs) | `TankBrain_RedNova : TankBrain, ISkillBranchConfigurable` | 4 slot + 6 option; Overdrive/Execution Core/Nova Rush là buff một đòn; Triple Nova bắn 3 tia hội tụ |
| [`Skill_NovaShot.cs`](../Assets/Scripts/Tanks/RedNova/Skill_NovaShot.cs) | `Skill_NovaShot` | Đạn năng lượng cơ bản (dùng cho Nova Shot & Triple Nova) |
| [`Skill_LaserCannon.cs`](../Assets/Scripts/Tanks/RedNova/Skill_LaserCannon.cs) | `Skill_LaserCannon` | Tia laser xuyên nhiều mục tiêu, chống trùng bằng `HashSet` |
| [`Skill_NovaBarrage.cs`](../Assets/Scripts/Tanks/RedNova/Skill_NovaBarrage.cs) | `Skill_NovaBarrage` | Oanh kích vùng nhiều đợt theo thời gian |
| [`RedNovaVfx.cs`](../Assets/Scripts/Tanks/RedNova/RedNovaVfx.cs) | `RedNovaVfx` | Aura Overdrive/Execution/Pierce, chùm laser, sóng xung kích, tàn ảnh Nova Rush |

#### 5.3.3 Wall Titan — `Tanks/WallTitan` (6 file)

| File | Class | Chức năng |
|---|---|---|
| [`TankBrain_WallTitan.cs`](../Assets/Scripts/Tanks/WallTitan/TankBrain_WallTitan.cs) | `TankBrain_WallTitan : TankBrain, ISkillBranchConfigurable` | Earth Break giảm sát thương theo khoảng cách; Earth Wall; Stone Fortress/Guardian Wall/Earth Prison; Immovable Titan/Earth Guardian/Earthquake (buff + redirect nằm trong brain) |
| [`Skill_EarthBreak.cs`](../Assets/Scripts/Tanks/WallTitan/Skill_EarthBreak.cs) | `Skill_EarthBreak` | Đạn đá, sát thương = ATK + DEF theo khoảng cách |
| [`Skill_EarthWall.cs`](../Assets/Scripts/Tanks/WallTitan/Skill_EarthWall.cs) | `Skill_EarthWall` | Tường chắn đường & chặn đạn, tồn tại 4s |
| [`Skill_EarthPrison.cs`](../Assets/Scripts/Tanks/WallTitan/Skill_EarthPrison.cs) | `Skill_EarthPrison` | Ngục đá nhốt địch rồi sập gây sát thương |
| [`Skill_Earthquake.cs`](../Assets/Scripts/Tanks/WallTitan/Skill_Earthquake.cs) | `Skill_Earthquake` | Vùng chấn động: slow + tick sát thương |
| [`WallTitanVfx.cs`](../Assets/Scripts/Tanks/WallTitan/WallTitanVfx.cs) | `WallTitanVfx` | Aura giáp đá, chốt neo, màn chắn, vết nứt, đá văng |

#### 5.3.4 Green Fox — `Tanks/GreenFox` (16 file)

| File | Class | Chức năng |
|---|---|---|
| [`TankBrain_GreenFox.cs`](../Assets/Scripts/Tanks/GreenFox/TankBrain_GreenFox.cs) | `TankBrain_GreenFox : TankBrain` | 4 slot theo GDD; quản lý ấn (Sign), thiêu đốt, bẫy, lãnh địa, 9 đuôi; cộng dồn ấn mỗi đòn qua `DamageEnemy` |
| [`GreenFox_Skill0.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_Skill0.cs) | `GreenFox_Skill0` | Fox Fang: đạn 100% ATK, 25% trói 1s; Tinh anh nhận thêm 120% ATK |
| [`GreenFox_Skill1.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_Skill1.cs) | `GreenFox_Skill1` | Fox Mark: chọn địch gần điểm ngắm nhất để gắn ấn |
| [`GreenFox_Skill1Sub.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_Skill1Sub.cs) | `Skill1Variant` + `GreenFox_Skill1Sub` | Thông số 3 biến thể option 1 (Mark Hunter / Burning Fox / Fox Trap) |
| [`GreenFox_Skill2Sub.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_Skill2Sub.cs) | `Skill2Variant` + `GreenFox_Skill2Sub` | Thông số 3 biến thể option 2 |
| [`GreenFox_Skill2.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_Skill2.cs) | `GreenFox_Skill2` | Nổ ấn (Mark Explosion) |
| [`Sign.cs`](../Assets/Scripts/Tanks/GreenFox/Sign.cs) | `Sign` | Dấu ấn trên địch: 8s, tối đa 10 cộng dồn, `Refresh` |
| [`GreenFox_BurnEffect.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_BurnEffect.cs) | `GreenFox_BurnEffect` | Thiêu đốt 0.5s/lần trong 6s; mỗi tick cũng cộng ấn |
| [`GreenFox_MagicTrap.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_MagicTrap.cs) | `GreenFox_MagicTrap` | Bẫy 15s: trói 2s rồi nổ 150% ATK; Tinh anh nhận 230% |
| [`GreenFox_FoxTerritory.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_FoxTerritory.cs) | `GreenFox_FoxTerritory` | Lãnh địa 6s: địch −30% speed, đồng minh +30%, hồi năng lượng cho Fox |
| [`GreenFox_NineTailedAssault.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_NineTailedAssault.cs) | `GreenFox_NineTailedAssault` | 9 đuôi, mỗi đuôi 15% ATK mỗi 1s trong 6s |
| [`GreenFox_NineTailsVisual.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_NineTailsVisual.cs) | `GreenFox_NineTailsVisual` | Chỉ hiển thị 9 đuôi |
| [`GreenFox_Tail.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_Tail.cs) | `GreenFox_Tail` | Một chiếc đuôi: xòe quạt, quất mục tiêu theo nhịp |
| [`GreenFox_KillStreamEffect.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_KillStreamEffect.cs) | `GreenFox_KillStreamEffect` | Luồng sáng xanh chảy về Fox khi hạ gục |
| [`GreenFox_RootEffect.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_RootEffect.cs) | `GreenFox_RootEffect` | Hiển thị trói chân |
| [`GreenFox_EnemyMoveModifier.cs`](../Assets/Scripts/Tanks/GreenFox/GreenFox_EnemyMoveModifier.cs) | `GreenFox_EnemyMoveModifier` | Gom mọi nguồn đổi tốc độ quái theo key (trói/chậm), nhân hệ số, gỡ hết thì trả gốc |

#### 5.3.5 Leviathan — `Tanks/Leviathan` (9 file)

| File | Class | Chức năng |
|---|---|---|
| [`TankBrain_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/TankBrain_Leviathan.cs) | `TankBrain_Leviathan : TankBrain` | 4 slot + 6 option; Water Form đổi CD Water Pulse; buff DEF/redirect |
| [`NormalSkill_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/NormalSkill_Leviathan.cs) | `NormalSkill_Leviathan` | Sóng xung kích nước, xuyên qua đồng minh/đạn, chỉ dừng ở địch |
| [`SpecializedSkill_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/SpecializedSkill_Leviathan.cs) | `SpecializedSkill_Leviathan` + `enum BubbleState` | Bong bóng nhốt quái thường (Root), nổ sát thương + đẩy lùi; Tinh anh nhận sát thương thay vì bị nhốt |
| [`OptionalSkill1_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/OptionalSkill1_Leviathan.cs) | `OptionalSkill1_Leviathan` | Water Armor: +DEF mạnh, giảm nhẹ damage gây ra |
| [`OptionalSkill1_2_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/OptionalSkill1_2_Leviathan.cs) | `... : MonoBehaviour, IDamageRedirector` | Guardian Current: gánh % sát thương cho đồng minh |
| [`OptionalSkill1_3_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/OptionalSkill1_3_Leviathan.cs) | `OptionalSkill1_3_Leviathan` | Regenerative Shell: kén nước hồi máu liên tục |
| [`OptionalSkill2_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/OptionalSkill2_Leviathan.cs) | `OptionalSkill2_Leviathan` | Tidal Return: xoáy nước tại con trỏ |
| [`OptionalSkill2_2_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/OptionalSkill2_2_Leviathan.cs) | `OptionalSkill2_2_Leviathan` | Leviathan Form: đổi dạng, buff chỉ số |
| [`OptionalSkill2_3_Leviathan.cs`](../Assets/Scripts/Tanks/Leviathan/OptionalSkill2_3_Leviathan.cs) | `OptionalSkill2_3_Leviathan` | Tide Commander: chỉ huy thủy triều (buff vùng) |

#### 5.3.6 Water B — `Tanks/WaterB` (15 file)

| File | Class | Chức năng |
|---|---|---|
| [`TankBrain_WaterB.cs`](../Assets/Scripts/Tanks/WaterB/TankBrain_WaterB.cs) | `TankBrain_WaterB : TankBrain` | 4 slot; dùng `AddShield`, `AddBonusStats`, `Heal(amount, false)` để Shared Healing luôn chạy |
| [`WaterB_HealingProjectile.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_HealingProjectile.cs) | `WaterB_HealingProjectile` | Đạn Healing Water: 80% ATK + 1% maxHP, hồi 10 EN khi trúng |
| [`WaterB_BubbleShield.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_BubbleShield.cs) | `WaterB_BubbleShield` | Khiên 120% ATK + 10% maxHP, 4s; vỡ/hết hạn hồi 100% ATK + 5% maxHP |
| [`WaterB_SharedHealingLink.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_SharedHealingLink.cs) | `WaterB_SharedHealingLink` | Sợi dây nước: mọi hồi máu của một bên chia sẻ sang bên kia |
| [`WaterB_BubbleProtectorDome.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_BubbleProtectorDome.cs) | `WaterB_BubbleProtectorDome` | Vòm nước: đồng minh trong vùng +25 DEF, +25% ATK; hết 6s hồi máu |
| [`WaterB_MobileAltar.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_MobileAltar.cs) | `WaterB_MobileAltar` | Đài tế di động: Water B không tấn công, mỗi 2s hồi máu + khiên cho đồng minh |
| [`WaterB_WaterTurret.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_WaterTurret.cs) | `WaterB_WaterTurret` | Trụ nước 8s, mỗi 1s bắn 50% ATK, hồi 3 EN cho chủ |
| [`WaterB_TurretProjectile.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_TurretProjectile.cs) | `WaterB_TurretProjectile` | Đạn của trụ nước |
| [`WaterB_AquaAssaultVisual.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_AquaAssaultVisual.cs) | `WaterB_AquaAssaultVisual` | Hình ảnh Aqua Assault bám thân |
| [`WaterB_LifeDrainRangeVisual.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_LifeDrainRangeVisual.cs) | `WaterB_LifeDrainRangeVisual` | Vòng tròn vùng hồi máu của Life Drain |
| [`WaterB_LifeDrainStreamEffect.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_LifeDrainStreamEffect.cs) | `WaterB_LifeDrainStreamEffect` | Luồng sáng hút máu về đồng minh thấp máu nhất |
| [`WaterB_ShieldAura.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_ShieldAura.cs) | `WaterB_ShieldAura` | Khiên nhỏ bám xe, tự tắt khi khiên hết hạn/vỡ |
| [`WaterB_Skill1Sub.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_Skill1Sub.cs) | `Skill1WaterVariant` + `WaterB_Skill1Sub` | Thông số 3 biến thể option 1 |
| [`WaterB_Skill2Sub.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_Skill2Sub.cs) | `Skill2WaterVariant` + `WaterB_Skill2Sub` | Thông số 3 biến thể option 2 |
| [`WaterB_Utils.cs`](../Assets/Scripts/Tanks/WaterB/WaterB_Utils.cs) | `static WaterB_Utils` | Tiện ích chung: chuột → world, quét tank trong bán kính |

### 5.4 Prefab tank mẫu — `Tanks/Test` và mẫu skill tối giản

| File | Chức năng |
|---|---|
| [`Test/TankBrain_Test.cs`](../Assets/Scripts/Tanks/Test/TankBrain_Test.cs) | Tank rỗng kế thừa `TankBrain`, dùng cho `Tank.prefab` — prefab tối thiểu để test framework |

> Repo **không còn** thư mục `Tanks/Samples` (2 file skill mẫu cũ). Khi cần một skill mẫu ngắn nhất để copy, dùng [`Skill_EarthBreak.cs`](../Assets/Scripts/Tanks/WallTitan/Skill_EarthBreak.cs) (một đạn, một lần quét sát thương) hoặc [`Skill_NovaShot.cs`](../Assets/Scripts/Tanks/RedNova/Skill_NovaShot.cs).

---

## 6. Tầng Kẻ địch — `Assets/Scripts/Enemies`

### 6.1 Framework & boss (17 file)

| File | Class | Chức năng |
|---|---|---|
| [`EnemyBrain.cs`](../Assets/Scripts/Enemies/EnemyBrain.cs) | `abstract EnemyBrain` | Runtime quái: HP, stats, CC, status effect, khiên, interception, co-op ×2 HP, event `OnDeathEvent`; abstract `OnBrainTick` + `OnDeath`; virtual `OnDamaged`, `OnContact`, `IsImmuneToCrowdControl`, `OnCrowdControlResisted` |
| [`EnemyModel.cs`](../Assets/Scripts/Enemies/EnemyModel.cs) | `EnemyModel` | Visual + collider của quái (nằm trong cùng prefab với Brain); forward `Contact`, `TriggerEnter/Stay/Exit` |
| [`EnemyStats.cs`](../Assets/Scripts/Enemies/EnemyStats.cs) | `EnemyStats` | Attack / Defense / MaxHealth / MoveSpeed, serialize trên prefab |
| [`NormalEnemyBrain.cs`](../Assets/Scripts/Enemies/NormalEnemyBrain.cs) | `abstract NormalEnemyBrain` | Lớp đệm rỗng cho quái thường — không có logic chung |
| [`NormalEnemyBrain_Test.cs`](../Assets/Scripts/Enemies/NormalEnemyBrain_Test.cs) | `NormalEnemyBrain_Test : NormalEnemy_BaseChaser` | Giữ tương thích ngược; chỉ dùng ở `MainScene` |
| [`EliteEnemyBrain.cs`](../Assets/Scripts/Enemies/EliteEnemyBrain.cs) | `enum EnergyCoreState`, `EnergyCore`, `abstract EliteEnemyBrain` | Boss "Bùng nổ năng lượng": mở core khi HP ≤ ngưỡng, cửa sổ phá, phá hết → giảm DEF; hết giờ → chiêu trừng phạt. Miễn nhiễm CC cứng |
| [`EliteEnemyBrain_Test.cs`](../Assets/Scripts/Enemies/EliteEnemyBrain_Test.cs) | `EliteEnemyBrain_Test` | Boss rỗng (không logic) — **đang được gắn trên 5 prefab boss** (xem §12 mục 2) |
| [`ChasingEnemyBrain.cs`](../Assets/Scripts/Enemies/ChasingEnemyBrain.cs) | `abstract ChasingEnemyBrain : NormalEnemyBrain` | Lớp đệm legacy: tự tìm tank, lao tới bằng Rigidbody2D, quay mặt |
| [`BlackMoonBrain.cs`](../Assets/Scripts/Enemies/BlackMoonBrain.cs) | `BlackMoonBrain : ChasingEnemyBrain` | Mặt Trăng Đen (bản legacy): lao gần + xung bức xạ 0.5s/bán kính 2.5m |
| [`CrystalShooterBrain.cs`](../Assets/Scripts/Enemies/CrystalShooterBrain.cs) | `CrystalShooterBrain` | Xạ Thủ Tinh Thể (legacy): giữ khoảng cách, bắn laser xuyên |
| [`QuartzBruteBrain.cs`](../Assets/Scripts/Enemies/QuartzBruteBrain.cs) | `QuartzBruteBrain` | Quái Vật Thạch Anh (legacy): bắn 3–5 gai đuổi, mỗi gai trúng sau +25% |
| [`MeteorParasiteBrain.cs`](../Assets/Scripts/Enemies/MeteorParasiteBrain.cs) | `MeteorParasiteBrain` | Ký Sinh Thiên Thạch (legacy): lao nhanh, tự nổ, làm chậm 30%/5s |
| [`GraveyardWispBrain.cs`](../Assets/Scripts/Enemies/GraveyardWispBrain.cs) | `GraveyardWispBrain` | Tinh Linh Nghĩa Địa (legacy): cứ 3m để lại vệt lửa |
| [`DeadlandPhantomBrain.cs`](../Assets/Scripts/Enemies/DeadlandPhantomBrain.cs) | `DeadlandPhantomBrain` | Boss Bóng Ma Tử Địa: vết nứt không gian + dịch chuyển |
| [`SeauBearusBrain.cs`](../Assets/Scripts/Enemies/SeauBearusBrain.cs) | `SeauBearusBrain` | Boss SeauBearus: mỗi 10s hút 10% HP mục tiêu và hồi máu |
| [`FlamePatch.cs`](../Assets/Scripts/Enemies/FlamePatch.cs) | `FlamePatch` | Vệt lửa dùng chung: 1.5m, 5s, 100% ATK/s, slow 20% trong 2s; `Spawn(...)` |
| [`QuartzSpike.cs`](../Assets/Scripts/Enemies/QuartzSpike.cs) | `QuartzVolley` + `QuartzSpike` | Một loạt gai chia sẻ bộ đếm trúng để tăng 25%/gai |

### 6.2 Điều hướng — `Enemies/Navigation` (2 file)

| File | Class | Chức năng |
|---|---|---|
| [`PathfindingGrid2D.cs`](../Assets/Scripts/Enemies/Navigation/PathfindingGrid2D.cs) | `PathfindingNode`, `PathfindingGrid2D` (singleton, `DefaultExecutionOrder(-100)`) | Lưới A* 2D: quét terrain 1 lần, đếm "enemy presence" real-time, **zero-GC** bằng versioning. `InitializeGrid`, `RegisterEnemy`, `UnregisterEnemy`, `UpdateEnemyPosition`, `FindPathToRadiusA`, `FindPathToPoint`, `GetGridCoordinates`, `IsValidCoord` |
| [`EnemyMovementAgent.cs`](../Assets/Scripts/Enemies/Navigation/EnemyMovementAgent.cs) | `EnemyMovementAgent` | Chạy theo waypoint A*, tách né nhau (separation), đẩy lồng collider; `SetPath`, `SetSpeed`, `Stop`, `Teleport`, `Initialize` |

### 6.3 Quái thường đời mới — `Enemies/Normal` (28 file)

**Ba lớp base quyết định kiểu di chuyển:**

| Base | Kiểu | Cơ chế |
|---|---|---|
| [`NormalEnemy_BaseChaser.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_BaseChaser.cs) | Đuổi & tìm đường A* | Tham số bán kính **A** (tầm tiếp cận), **B** (tầm đuổi), **C** (lãnh thổ quanh **D**), **X/Y** (chờ & teleport về nhà); state machine `NormalEnemyState` |
| [`NormalEnemy_BasePatrol.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_BasePatrol.cs) | Tuần tra waypoint | `Loop` hoặc `PingPong`, dừng nghỉ tại điểm, không dùng A* |
| [`NormalEnemy_BaseStationary.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_BaseStationary.cs) | Đứng yên | Chỉ phát hiện mục tiêu trong bán kính C quanh D |

**12 quái cụ thể:**

| File | Quái | Chức năng / chiêu |
|---|---|---|
| [`NormalEnemy_KIenLuaKhongLo.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_KIenLuaKhongLo.cs) | Kiến Lửa Khổng Lồ | Đuổi; chiêu tam giác theo hồi chiêu (`Skill_KIenLuaKhongLo`) |
| [`NormalEnemy_QuaiVatDamLay.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_QuaiVatDamLay.cs) | Quái Vật Đầm Lầy | Lao vào mục tiêu; để lại `SwampZone_QuaiVatDamLay` |
| [`NormalEnemy_NamDocGaiThach.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_NamDocGaiThach.cs) | Nấm Độc Gai Thạch | Đứng yên, tàng hình alpha 0.1, vùng độc `PoisonZone_NamDocGaiThach` |
| [`NormalEnemy_SaoDen.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_SaoDen.cs) | Sao Đen | Đứng yên, tạo `HealZone_SaoDen` hồi máu đồng loại |
| [`NormalEnemy_MatTroiDo.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_MatTroiDo.cs) | Mặt Trời Đỏ | Đứng yên, triệu hồi `ExplosionZone_MatTroiDo` tại vị trí tank; nổ xong sinh Ký Sinh |
| [`NormalEnemy_MatTrangDen.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_MatTrangDen.cs) | Mặt Trăng Đen | Đuổi; chiêu 2 pha teleport → sát thương diện rộng (`Skill_MatTrangDen`) |
| [`NormalEnemy_XenNuoc.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_XenNuoc.cs) | Xén Nước | Đuổi; khóa hướng lao thẳng quãng cố định |
| [`NormalEnemy_TinhLinhSamSetNho.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_TinhLinhSamSetNho.cs) | Tinh Linh Sấm Sét Nhỏ | Đuổi; sét lan `LightningBeam_TinhLinhSamSetNho` + `LightningChainScanner_TinhLinhSamSetNho` |
| [`NormalEnemy_TinhLinhNghiaDia.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_TinhLinhNghiaDia.cs) | Tinh Linh Nghĩa Địa | Tuần tra, rải `DamageZone_TinhLinhNghiaDia` (pool) |
| [`NormalEnemy_KySinhThienThach.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_KySinhThienThach.cs) | Ký Sinh Thiên Thạch | Đuổi; tự nổ trong tầm A hoặc khi chết (`Skill_KySinhThienThach`) |
| [`NormalEnemy_QuaiVatTinhThach.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_QuaiVatTinhThach.cs) | Quái Vật Thạch Anh | Bắn 3–5 đạn đuổi (`Projectile_QuaiVatTinhThach`), sát thương tăng 25%/viên trúng |
| [`NormalEnemy_XaThuTinhThe.cs`](../Assets/Scripts/Enemies/Normal/NormalEnemy_XaThuTinhThe.cs) | Xạ Thủ Tinh Thể | Bắn đạn xuyên (`Projectile_XaThuTinhThe`), 100% ATK mỗi tank trúng |

**Helper chiến đấu (13 file):** [`EnemyProjectileBase.cs`](../Assets/Scripts/Enemies/Normal/EnemyProjectileBase.cs) (khung đạn quái: bay, lifetime, va chạm, pool), [`Projectile_QuaiVatTinhThach.cs`](../Assets/Scripts/Enemies/Normal/Projectile_QuaiVatTinhThach.cs), [`Projectile_XaThuTinhThe.cs`](../Assets/Scripts/Enemies/Normal/Projectile_XaThuTinhThe.cs), [`Skill_KIenLuaKhongLo.cs`](../Assets/Scripts/Enemies/Normal/Skill_KIenLuaKhongLo.cs), [`Skill_KySinhThienThach.cs`](../Assets/Scripts/Enemies/Normal/Skill_KySinhThienThach.cs), [`Skill_MatTrangDen.cs`](../Assets/Scripts/Enemies/Normal/Skill_MatTrangDen.cs), [`DamageZone_TinhLinhNghiaDia.cs`](../Assets/Scripts/Enemies/Normal/DamageZone_TinhLinhNghiaDia.cs), [`ExplosionZone_MatTroiDo.cs`](../Assets/Scripts/Enemies/Normal/ExplosionZone_MatTroiDo.cs), [`HealZone_SaoDen.cs`](../Assets/Scripts/Enemies/Normal/HealZone_SaoDen.cs), [`PoisonZone_NamDocGaiThach.cs`](../Assets/Scripts/Enemies/Normal/PoisonZone_NamDocGaiThach.cs), [`SwampZone_QuaiVatDamLay.cs`](../Assets/Scripts/Enemies/Normal/SwampZone_QuaiVatDamLay.cs), [`LightningBeam_TinhLinhSamSetNho.cs`](../Assets/Scripts/Enemies/Normal/LightningBeam_TinhLinhSamSetNho.cs) + [`LightningChainScanner_TinhLinhSamSetNho.cs`](../Assets/Scripts/Enemies/Normal/LightningChainScanner_TinhLinhSamSetNho.cs).

> Mọi zone (`DamageZone`, `ExplosionZone`, `HealZone`, `PoisonZone`, `SwampZone`) **không tự gây sát thương**: chúng quét mục tiêu bằng `ColliderOwnerRef` rồi phát event để Brain tính sát thương. Đây là ví dụ mẫu của Rule 2.

---

## 7. Màn chơi — `Assets/Scripts/Stages` (2 file)

| File | Class | Chức năng |
|---|---|---|
| [`StageDefinition.cs`](../Assets/Scripts/Stages/StageDefinition.cs) | `StageDefinition : ScriptableObject` + nested `Wave`, `EnemySpawn` | Dữ liệu 1 màn: mã/tên, điều kiện thắng (`KillTarget`), thời lượng dự kiến, danh sách wave theo mốc `StartTime`, mỗi entry là prefab quái + số lượng + hệ số tốc độ. Menu: `Create > Iron Wasteland > Stage > Stage Definition` |
| [`StageManager.cs`](../Assets/Scripts/Stages/StageManager.cs) | `enum StageState`, `StageManager` | Chạy màn: spawn quanh tâm theo bán kính, đếm hạ gục qua `OnDeathEvent`, kiểm tra thắng (đủ KillTarget) / thua (tank chết), log tiến trình. Event `WaveStarted`, `KillsChanged`, `StageCleared`, `StageFailed` |

**Trạng thái:** code xong, **chưa có asset `StageDefinition` nào** và chưa gắn vào scene → hệ màn chơi hiện là hạ tầng chờ dữ liệu.

---

## 8. Tiện ích, Editor, Test — phần còn lại

### 8.1 `Scripts/Utilities` (3 file)

| File | Chức năng |
|---|---|
| [`Collider2DUtility.cs`](../Assets/Scripts/Utilities/Collider2DUtility.cs) | `TryGetApproximateOverlapCenter` (tâm vùng giao 2 collider bằng sampling) và `TryGetRandomOverlapPoint` — dùng để đặt text/efffect đúng chỗ va chạm |
| [`DamageTestTrigger.cs`](../Assets/Scripts/Utilities/DamageTestTrigger.cs) | Vật thể test: gây sát thương định kỳ cho tank chạm vào, cooldown riêng từng tank |
| [`SpriteFactory.cs`](../Assets/Scripts/Utilities/SpriteFactory.cs) | Sinh sprite bằng code (vòng tròn, vệt lửa) cho hiệu ứng chưa có art; cache theo kích thước |

### 8.2 `Scripts/Editor` (1 file)

| File | Chức năng |
|---|---|
| [`ReadOnlyDrawer.cs`](../Assets/Scripts/Editor/ReadOnlyDrawer.cs) | PropertyDrawer cho `[ReadOnly]`: vẫn mở foldout để xem, nhưng field con bị disable — đúng thiết kế "xem được, không sửa được" |

### 8.3 `Scripts/Tests` (3 file) — self-check chạy tay, **không phải NUnit**

| File | Chức năng |
|---|---|
| [`SkillArena.cs`](../Assets/Scripts/Tests/SkillArena.cs) | Sân test: spawn vòng quái giả, phím `1/2` đổi nhánh option 1, `3/4` đổi nhánh option 2, reset cooldown |
| [`RedNovaSkillCheck.cs`](../Assets/Scripts/Tests/RedNovaSkillCheck.cs) | Kiểm tra 8 chiêu Red Nova: năng lượng, cooldown, sát thương lên bia, VFX |
| [`WallTitanSkillCheck.cs`](../Assets/Scripts/Tests/WallTitanSkillCheck.cs) | Kiểm tra 8 chiêu Wall Titan: năng lượng, cooldown, sát thương, khiên, VFX |

> Manifest có `com.unity.test-framework` nhưng repo **không có** `.asmdef`, không có `[Test]`/`[UnityTest]` nào. Ba file trên là component gắn vào scene rồi bấm Play. Nếu muốn CI thật, phải tạo test assembly riêng.

---

## 9. Luồng runtime (đọc theo thứ tự này để hiểu game)

### 9.1 Khởi tạo tank

```
GameController.Awake()
  → new TankBrainContext { Level, IdView, EquipmentId, OptionSkill1, OptionSkill2 }
  → TankDefinition.CreateTank(context)
       → Instantiate(tankPrefab)                      // prefab chỉ có Brain, chưa có View
       → brain.Initialize(context, GetStatsForLevel(context.Level))
             ├─ baseStats = stats.Clone(); RecalculateStats()   // Final = Base×(1+%)+Flat
             ├─ currentHealth = MaxHealth; currentEnergy = MaxEnergy
             └─ cooldown[0..3] = 0
       → brain.SetViewPrefab(viewPrefabs[IdView])
             → Instantiate(view, brain.transform); view.RegisterOwner(brain)
       → EquipmentDatabase.GetPrefab(EquipmentId) → brain.EquipAccessory(prefab)
```

### 9.2 Mỗi frame của một tank

| Vòng | Việc |
|---|---|
| `Update` | giảm cooldown 4 slot · tick CC (hết hạn thì gỡ) · tick status effect (DoT/HoT/khiên hết hạn) · `Accessory.OnBrainTick` |
| `FixedUpdate` | `Rigidbody2D.linearVelocity = moveInput × MoveSpeed` (Slow nhân thêm `1 − strength`); `TankView.Move` / `TankView.Look` |
| `UseSkill(i)` (từ input) | kiểm `CanUseSkill` → `cooldown[i] == 0` → đủ năng lượng → trừ năng lượng → `OnUseSkill(i)` → set cooldown `GetCooldown(i)` → cộng năng lượng theo `GetSkillEnergyGain(i)` → event `OnSkillUsedEvent` |

### 9.3 Pipeline sát thương (giống nhau cho tank và quái)

```
TakeDamage(DamageInfo)
 [1] ProcessDamageInterception   // né/đỡ đòn/bất tử: chặn sạch → return, không event
 [2] ProcessDamageRedirection    // gánh hộ: trừ RawDamage, gọi protector.TakeDamage(IsDirectToHealth, IsRedirected)
 [3] CombatUtility.ResolveDamage // Def hiệu dụng → % giảm 4 đoạn logarit → FinalDamage
 [4] ProcessShieldAbsorption     // lớp khiên hết hạn sớm nhất hấp thụ trước
 [5] currentHealth -= còn lại    // IsDirectToHealth = true thì bỏ qua [4]
 → OnTakeDamageEvent + Accessory.OnOwnerTookDamage → chết thì Die()/OnDeath
```

### 9.4 AI quái thường

```
NormalEnemy_BaseChaser.OnBrainTick(dt)
  ├─ quét tank trong bán kính C quanh D (ScanAndValidateTarget)
  ├─ không có mục tiêu: đứng yên → quá X giây đi bộ về D → quá Y giây teleport về D
  └─ có mục tiêu:
       ├─ trong A  → dừng (hoặc gây chiêu nếu có)
       ├─ trong B  → PathfindingGrid2D.FindPathToRadiusA(...) → EnemyMovementAgent.SetPath(...)
       └─ ngoài B  → tiếp tục đuổi / về nhà theo cấu hình
  ├─ chiêu con (nếu có) tự đếm cooldown, phát event để Brain tính sát thương
  └─ chết → OnDeath() → OnDeathEvent → StageManager.OnEnemyDied()
```

### 9.5 Một màn chơi

```
StageManager.Start()
  → Initialize(): tìm GameController, tạo container "Enemies"
  → StartStage(): sort wave theo StartTime, State = Running
Update(): Elapsed += dt
  → SpawnDueWaves(): wave.StartTime ≤ Elapsed → Instantiate quái quanh arenaCenter
                     → enemy.Initialize(stats × SpeedMultiplier) ; nghe OnDeathEvent
  → TrackPlayer() → EvaluateOutcome(): player chết → Failed ; Kills ≥ KillTarget → Cleared
```

---

## 10. Prefab ↔ Script ↔ Art

### 10.1 Xe tăng

| Prefab Brain | View prefab | TankDefinition asset | Script chính |
|---|---|---|---|
| `Assets/Prefabs/Tanks/BlackShadow/BlackShadow.prefab` | `BlackShadow_View.prefab` | `BlackShadow.asset` | `TankBrain_BlackShadow` + 9 skill |
| `Assets/Prefabs/Tanks/RedNova/RedNova.prefab` | `RedNova_View.prefab` | `RedNova.asset` | `TankBrain_RedNova` + 3 skill + VFX |
| `Assets/Prefabs/Tanks/WallTitan/WallTitan.prefab` | `WallTitan_View.prefab` | `WallTitan.asset` | `TankBrain_WallTitan` + 4 skill + VFX |
| `Assets/Prefabs/Tanks/GreenFox/GreenFox .prefab` *(có dấu cách)* | `GreenFox_View.prefab` | `GreenFox.asset` | `TankBrain_GreenFox` + 15 script phụ |
| `Assets/Prefabs/Tanks/Leviathan/Leviathan.prefab` | `Leviathan_View.prefab` | `Leviathan.asset` | `TankBrain_Leviathan` + 8 skill |
| `Assets/Prefabs/Tanks/WaterB/WaterB.prefab` | `WaterB_View.prefab` | `WaterB.asset` | `TankBrain_WaterB` + 14 script phụ |
| `Assets/Prefabs/Tanks/Tank.prefab` | — | — | `TankBrain_Test` (prefab mẫu tối thiểu) |

### 10.2 Prefab quái → script

| Nhóm | Prefab | Script điều khiển |
|---|---|---|
| Chương 2 (đời mới) | `KIenLuaKhongLo`, `QuaiVatDamLay`, `NamDocGaiThach`, `SaoDen`, `MatTroiDo`, `MatTrangDen`, `XenNuoc`, `TinhLinhSamSetNho`, `TinhLinhNghiaDia`, `KySinhThienThach`, `QuaiVatTinhThach`, `XaThuTinhThe` | `NormalEnemy_<Tên>` tương ứng + helper cùng tên |
| Chương 1 (legacy) | `BlackMoon`, `CrystalShooter`, `QuartzBrute`, `MeteorParasite`, `GraveyardWisp` | `BlackMoonBrain`, `CrystalShooterBrain`, `QuartzBruteBrain`, `MeteorParasiteBrain`, `GraveyardWispBrain` |
| Boss có logic | `DeadlandPhantom`, `SeauBearus` | `DeadlandPhantomBrain`, `SeauBearusBrain` (kế thừa `ChasingEnemyBrain`) |
| Prefab boss khung | `Enemies/BongMaTuDia`, `SEAUBEARUS`, `TuThanNguyenTo`, `TruongQuanNgucD1ONK `, `Enemy_Elite` | **`EliteEnemyBrain_Test`** — chỉ có khung, chưa có logic |
| Prefab kỹ năng của xe | `Tanks/GreenFox/Skill0, Skill1, Skill2, Trap1, Burn, Root, "Circle 1", Explosion, gf_nine_tails, sign, 1BSkilActive, Trap_Explosion_1_SpiritBloom`; `Tanks/WaterB/Square, Turret, Share, Shield, 1CPF, 2APF, WaterB_Skill0, water_*` | Script visual/skill tương ứng trong `Tanks/<Xe>/` |

### 10.3 Art

- `Assets/Art/Tank/Sprites/<Xe>/` + `Animations/` + `VFX/<Xe>/` — art riêng từng xe.
- `Assets/Art/Enemies/<TênViệtHóa>/` — 19 thư mục (17 thư mục quái + `Concept` + `ChapterOne`), mỗi thư mục có sprite + `Animations/`; thêm `Concept/` cho bản vẽ ý tưởng.
- Animator dùng 2D Animation/Aseprite; sprite sheet nằm cùng thư mục art, **không** có atlas riêng trong Scripts.

### 10.4 Scene

| Scene | Nội dung | Vai trò |
|---|---|---|
| `SampleScene.unity` | Main Camera, Global Light 2D, `GameController`, `TextDamageManager`, `PathfindingGrid2D`, terrain (`Capsule`, `Square`, `Triangle`), vài quái | **Scene build duy nhất** — sân test hiện tại |
| `MainScene.unity` | `GameController` + `Main Camera` + `Tank_tesster` + quái test + `EnemyModel` | Scene debug tối giản, không trong build |

### 10.5 Settings & asset

| Asset | Vai trò |
|---|---|
| `Assets/Settings/UniversalRP.asset`, `Renderer2D.asset`, `DefaultVolumeProfile.asset` | Cấu hình URP 2D |
| `Assets/Settings/EquipmentDatabase.asset` | Kho phụ kiện — ⚠ còn serialize 3 danh sách cũ nên `accessoryPrefabs` (field hiện tại) rỗng; xem §5.2 và §12 |
| `Assets/Prefabs/Tanks/<Xe>/<Xe>.asset` | 6 `TankDefinition` — **schema không đồng nhất**: `RedNova`, `WallTitan`, `Leviathan` đã dùng `baseStatsLevel1`/`baseStatsLevel90`; `BlackShadow`, `GreenFox`, `WaterB` vẫn là schema cũ `statsTable` + `context.FirepowerEquipmentId/DefenseEquipmentId/MobilityEquipmentId` nên **không có chỉ số cấp 1/90** cho code hiện tại đọc (xem §12 mục 4) |
| `Assets/Settings/Scenes/URP2DSceneTemplate.unity` | Template tạo scene mới của Unity |

---

## 11. Đã có gì / Chưa có gì

### 11.1 Đã triển khai

- Framework tank hoàn chỉnh: chỉ số, năng lượng, 4 cooldown, CC, hiệu ứng, khiên nhiều lớp, chặn đòn, gánh hộ, phụ kiện.
- 6 xe tăng với đủ 8 kỹ năng/xe (4 slot + 6 option), VFX và pool riêng.
- Framework quái: HP/giáp/CC/effect/co-op ×2, A* zero-GC, 3 kiểu di chuyển, 12 quái thường + 5 bản legacy + 2 boss có logic, ~20 hiệu ứng vùng/đạn.
- Hệ màn chơi: `StageDefinition` + `StageManager` (wave theo thời gian, điều kiện thắng/thua, event).
- Công cụ: `[ReadOnly]`, text sát thương pool, sân test skill, 3 script self-check, `SkillArena`.

### 11.2 Chưa có (đừng giả định là có)

| Hạng mục | Bằng chứng |
|---|---|
| **UI/HUD, menu, garage** | Không có `Canvas` trong scene, không có script UI; chỉ có wireframe SVG trong `docs/figma_wireframes` |
| **Màn chơi thật** | Không có `StageDefinition.asset`, không có scene màn 1-1…2-5; `StageManager` chưa gắn ở scene nào |
| **Save/load, tiến trình, mở khóa xe** | Không có script persistence |
| **Co-op LAN** | Chỉ có `EnemyBrain.SetCoopMode` (×2 HP); không có netcode, không có đồng bộ transform |
| **20 phụ kiện GDD** | Chỉ `Accessory_Test`; 3 prefab `Defense_Heavy`/`Firepower_Cannon`/`Mobility_Heavy` là di sản 3-lõi, không có script riêng; `EquipmentDatabase.asset` còn serialize 3 danh sách cũ nên hiện không nạp được phụ kiện nào |
| **Test tự động/CI** | Không asmdef, không `[Test]`; 3 file trong `Scripts/Tests` là công cụ chạy tay |
| **Màn chơi & art hoàn chỉnh** | Art mới ở mức sprite/animation đặt theo từng thư mục quái; chưa có scene màn, chưa có UI |

---

## 12. Nợ kỹ thuật & điểm dễ sửa nhầm

1. **Hai thế hệ quái cùng tồn tại.** Bộ legacy (`ChasingEnemyBrain` + `*Brain.cs` ở `Enemies/`) và bộ mới (`NormalEnemy_*` ở `Enemies/Normal/`) trùng vai trò. Prefab đang trỏ lẫn lộn: `BlackMoon.prefab` → `BlackMoonBrain`, nhưng `MatTrangDen.prefab` → `NormalEnemy_MatTrangDen`; `DeadlandPhantom.prefab` → `DeadlandPhantomBrain`, nhưng `BongMaTuDia.prefab` → `EliteEnemyBrain_Test`. **Trước khi sửa một con quái, mở prefab xem script nào đang gắn** — sửa file không được dùng là sửa vào chỗ chết.
2. **5 prefab boss đang gắn `EliteEnemyBrain_Test`** — lớp này chỉ kế thừa khung, không có AI/chiêu nào → boss đứng yên, không tấn công. Các prefab đó cần được thay bằng brain thật khi làm boss.
3. **`PROJECT_OVERVIEW.md` đã lạc hậu**: mô tả `FirepowerCore/DefenseCore/MobilityCore` (đã bị thay bằng 1 ô `EquipmentBrain`), bỏ sót toàn bộ `Stages`, `Navigation`, `Enemies/Normal`, `Core/Effects`, và nói có "test suite Unity Test Framework" trong khi repo không có test NUnit. **Dùng `PROJECT_CONTEXT.md` + file này làm chuẩn.**
4. **Dữ liệu 6 `TankDefinition` lệch schema.** `BlackShadow.asset`, `GreenFox.asset`, `WaterB.asset` còn serialize theo schema cũ (`statsTable` + `DamageMultiplier` + `context.FirepowerEquipmentId/DefenseEquipmentId/MobilityEquipmentId`), trong khi `TankDefinition` hiện đọc `baseStatsLevel1` / `baseStatsLevel90` / `context.EquipmentId` → ba xe này không có chỉ số khi `CreateTank`. `Leviathan.asset` đúng schema nhưng `MoveSpeed = 100` (thang GDD) trong khi runtime là unit/giây (3–7,5) → cần chốt hệ số đổi. `RedNova` và `WallTitan` đã đúng cả schema lẫn đơn vị.
5. **Prefab `GreenFox .prefab` có dấu cách trong tên** — script load theo tên/đường dẫn sẽ vỡ; đổi tên cần sửa cả `.meta`.
6. **Chỉ `SampleScene` nằm trong Build Settings**, mà đó là scene test có terrain hình học. Build bây giờ sẽ ra "sân test", không phải game.
7. **Input đọc trực tiếp trong `GameController.Update`** (không dùng `InputSystem_Actions` asset). Muốn hỗ trợ gamepad/rebinding phải chuyển sang `PlayerInput`/action asset — hiện tại bàn phím + chuột là duy nhất.
8. **Không có asmdef** → mọi thay đổi biên dịch lại toàn bộ `Assembly-CSharp`; muốn test CI hoặc tách package phải thêm assembly.
9. **`TextDamageManager` là singleton phụ thuộc scene** — scene nào không đặt object này sẽ không hiện số sát thương (không báo lỗi rõ ràng).
10. **`Assets/Plugins/NuGet`** chứa DLL ngoài; khi update Unity/IL2CPP cần kiểm tra lại tương thích.

---

## 13. Quy trình thêm mới (tóm tắt — thao tác Unity chi tiết ở `UNITY_CO_BAN.md`)

| Muốn thêm | Các bước chính | File mẫu nên copy |
|---|---|---|
| **Skill mới cho xe có sẵn** | 1) Tạo `MonoBehaviour` skill (bay/AoE/buff) → 2) thêm field prefab + pool vào TankBrain → 3) khai báo `GetBaseCooldown`, `GetSkillEnergyCost/Gain` → 4) gọi trong `OnUseSkill` → 5) gắn prefab vào Inspector | `Skill_EarthBreak.cs`, `Skill_NovaShot.cs` |
| **Xe tăng mới** | 1) `class TankBrain_<Xe> : TankBrain` → 2) tạo prefab Brain (Rigidbody2D + Collider + `ColliderOwnerRef`) → 3) tạo prefab View (`TankView` + Animator + pivot + muzzle) → 4) tạo `TankDefinition` asset, gán prefab/View → 5) gắn vào `GameController` | `TankBrain_RedNova.cs` + `RedNova.prefab` |
| **Quái mới** | 1) chọn base (`BaseChaser` / `BasePatrol` / `BaseStationary`) → 2) override `OnBrainTick`/`OnDeath`, dùng `EnemyMovementAgent` + zone quét `ColliderOwnerRef` → 3) prefab: Brain + `EnemyModel` (sprite/collider) + `ColliderOwnerRef` → 4) thêm vào `StageDefinition` | `NormalEnemy_MatTroiDo.cs` |
| **Màn mới** | 1) tạo asset `StageDefinition` → 2) thêm wave + prefab quái + `KillTarget` → 3) scene mới: `GameController` + `StageManager` (gán definition) + `PathfindingGrid2D` + `TextDamageManager` → 4) thêm scene vào Build Settings | `StageManager.cs` |
| **Phụ kiện mới** | 1) `class Accessory_<Tên> : EquipmentBrain` → 2) override hook cần dùng → 3) prefab + thêm vào `EquipmentDatabase.asset` (ID = index) | `Accessory_Test.cs` |

**Checklist trước khi commit một skill** (luật gốc ở [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) §3): dùng pool, không `Instantiate` trong combat · skill không tự `TakeDamage`, chỉ phát event · mọi collider có `ColliderOwnerRef` · sát thương đi qua `CombatUtility` · không đụng `baseStats` · Brain không đọc Input · thử bằng `SkillArena`.

---

## 14. Bảng tra nhanh "muốn sửa X → đọc file nào"

| Câu hỏi | File |
|---|---|
| Công thức giảm sát thương / xuyên giáp | [`Core/CombatUtility.cs`](../Assets/Scripts/Core/CombatUtility.cs) |
| Vì sao đòn đánh bị chặn / chia sang đồng minh | [`Tanks/TankBrain.cs`](../Assets/Scripts/Tanks/TankBrain.cs) (`TakeDamage`), [`Core/Effects/DamageInterceptEffect.cs`](../Assets/Scripts/Core/Effects/DamageInterceptEffect.cs), [`Tanks/IDamageRedirector.cs`](../Assets/Scripts/Tanks/IDamageRedirector.cs) |
| Khiên cộng dồn / hết hạn / giới hạn đòn | [`Core/Effects/ShieldEffect.cs`](../Assets/Scripts/Core/Effects/ShieldEffect.cs), `TankBrain.ProcessShieldAbsorption` |
| Vì sao quái không đuổi / kẹt tường | [`Enemies/Navigation/PathfindingGrid2D.cs`](../Assets/Scripts/Enemies/Navigation/PathfindingGrid2D.cs), [`EnemyMovementAgent.cs`](../Assets/Scripts/Enemies/Navigation/EnemyMovementAgent.cs), base tương ứng trong `Enemies/Normal` |
| Vì sao số sát thương không hiện | [`Core/TextDamageManager.cs`](../Assets/Scripts/Core/TextDamageManager.cs) + object trong scene |
| Điều khiển / phím | [`Tanks/GameController.cs`](../Assets/Scripts/Tanks/GameController.cs) |
| Chỉ số xe theo cấp | [`Tanks/TankDefinition.cs`](../Assets/Scripts/Tanks/TankDefinition.cs), [`Tanks/TankStats.cs`](../Assets/Scripts/Tanks/TankStats.cs) |
| Chọn nhánh kỹ năng option | [`Tanks/TankBrainContext.cs`](../Assets/Scripts/Tanks/TankBrainContext.cs) + `Context.OptionSkill1/2` trong brain từng xe |
| Spawn quái / điều kiện thắng màn | [`Stages/StageDefinition.cs`](../Assets/Scripts/Stages/StageDefinition.cs), [`Stages/StageManager.cs`](../Assets/Scripts/Stages/StageManager.cs) |
| Luật & công thức gốc của GDD | [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) |
| Cách dựng prefab skill từng bước | [`UNITY_CO_BAN.md`](UNITY_CO_BAN.md) (thao tác Inspector/Prefab Mode) + file skill mẫu `Skill_EarthBreak.cs` |

---

*Tài liệu này mô tả trạng thái repo tại thời điểm cập nhật. Nếu số liệu trong GDD (file Word) khác code, xem bảng chốt ở [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) §10.7 — code là chuẩn cho các mục đã chốt tại đó.*
