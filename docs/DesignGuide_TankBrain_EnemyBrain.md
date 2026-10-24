# IronWasteland — Tài liệu Thiết kế (Design Guide)

> Phạm vi: kiến trúc & logic của **TankBrain**, **EnemyBrain**, các hệ thống hỗ trợ
> (**OwnerRef / ColliderOwnerRef / TextDamageManager / TankView / TankDefinition...**)
> và ví dụ thực tế đã thiết kế: **TankBrain_BlackShadow** + **Skill_Test1 / Skill_Test2**.
>
> Tài liệu dựa trên code hiện tại trong `Assets/Scripts/` (branch `TankBrain`).

---

## 1. Tổng quan kiến trúc

Nguyên tắc cốt lõi của project là **tách dữ liệu – logic – hiển thị** thành 3 lớp:

```
┌─────────────────────────────────────────────────────────────────────┐
│  DATA (ScriptableObject / Prefab)                                  │
│  TankDefinition (bang stats theo Level, prefab, view, equipment)   │
│  EnemyStats (chỉnh trực tiếp trên prefab)                          │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ khởi tạo (Initialize)
┌──────────────────────────▼──────────────────────────────────────────┐
│  LOGIC / RUNTIME STATE (MonoBehaviour "Brain")                     │
│  TankBrain (abstract) ── TankBrain_BlackShadow, TankBrain_Test...   │
│  EnemyBrain (abstract) ── NormalEnemyBrain / EliteEnemyBrain + _Test│
│  - Giữ HP, Energy, cooldown, stats cuối, AI, skill                 │
│  - KHONG chứa visual/collider trực tiếp                            │
└──────────────────────────┬──────────────────────────────────────────┘
                           │ điều khiển (Move/Look/UseSkill) + event
┌──────────────────────────▼──────────────────────────────────────────┐
│  VISUAL / PHYSICAL (View hoặc Model)                               │
│  TankView  (prefab RIÊNG, spawn con của TankBrain)                 │
│  EnemyModel (cùng prefab với EnemyBrain, là con)                   │
│  - Sprite, Animator, pivot, collider                               │
│  - KHONG chứa logic game, chỉ forward event lên Brain              │
└─────────────────────────────────────────────────────────────────────┘

CORE (dùng chung, namespace IronWasteland):
  OwnerRef            → giữ tham chiếu "ai là chủ" của object
  ColliderOwnerRef    → OwnerRef cho collider + event trigger/collision + tracking
  TextDamageManager   → singleton pooling hiện số sát thương
  ReadOnlyAttribute   → attribute Editor chỉ-đọc cho Inspector
```

### Bảng file chính

| Hệ thống | File | Vai trò |
|---|---|---|
| TankBrain | `Assets/Scripts/Tanks/TankBrain.cs` | Lớp cha abstract, runtime state + input + skill framework |
| TankBrainContext | `Assets/Scripts/Tanks/TankBrainContext.cs` | Struct dữ liệu khởi tạo (Level, IdView, equipment ids, skill options) |
| TankDefinition | `Assets/Scripts/Tanks/TankDefinition.cs` | SO: stats theo level, prefab, view, equipment DB, `CreateTank()` |
| TankStats | `Assets/Scripts/Tanks/TankStats.cs` | Class stats (Add/Subtract/Negated/Clone) |
| TankView | `Assets/Scripts/Tanks/TankView.cs` | Visual/physical của Tank, action Move/Look/UseSkill/Stop |
| GameController | `Assets/Scripts/Tanks/GameController.cs` | Input → gọi TankBrain (WASD, chuột, J/K/L/I) |
| EnemyBrain | `Assets/Scripts/Enemies/EnemyBrain.cs` | Lớp cha abstract của địch: stats, HP, combat, hooks |
| EnemyModel | `Assets/Scripts/Enemies/EnemyModel.cs` | Visual + collider của địch, forward event lên Brain |
| OwnerRef | `Assets/Scripts/Core/OwnerRef.cs` | Giữ `UnityEngine.Object owner`, lookup an toàn theo kiểu |
| ColliderOwnerRef | `Assets/Scripts/Core/ColliderOwnerRef.cs` | OwnerRef + trigger/collision events + contact tracking |
| TextDamageManager | `Assets/Scripts/Core/TextDamageManager.cs` | Singleton pooling text sát thương (TMP world space) |
| BlackShadow | `Assets/Scripts/Tanks/BlackShadow/*` | Ví dụ tank cụ thể: brain + 2 skill + pooling |

---

## 2. TankBrain — thiết kế & logic

### 2.1 Vai trò

`TankBrain` (abstract, `[DisallowMultipleComponent]`) là **nơi duy nhất chứa runtime state của Tank**:

- `baseStats` — stats gốc do `TankDefinition` ghi vào khi khởi tạo (không sửa tay, `[ReadOnly]`).
- `bonusStats` — stats phụ cộng thêm từ bên ngoài (buff/debuff/equipment), luôn được **tính lại** = tổng các block theo `key`.
- `currentHealth` / `currentEnergy` — HP và Energy hiện tại.
- `skillCooldowns[4]` — 4 slot skill (`const int SkillCount = 4`).
- `m_context` (`TankBrainContext`) — dữ liệu khởi tạo (Level, IdView, equipment ids, skill options).

**Công thức:** `Stats (final) = baseStats + bonusStats` → lưu trong `m_FinalStats`, expose qua property `Stats`.
Level không đổi sau khi khởi tạo (clamp `1..90` theo `TankDefinition.MinLevel/MaxLevel`).

### 2.2 Chuỗi khởi tạo (startup flow)

```
GameController.Awake()
  ├─ xây TankBrainContext từ Inspector (level, viewId, equipment ids)
  ├─ tankDefinition.CreateTank(context)
  │    ├─ Instantiate(tankPrefab)
  │    ├─ brain.Initialize(context, GetStatsForLevel(level))   ← guard _initialized, chỉ chạy 1 lần
  │    │     ├─ clone baseStats, RecalculateStats()            ← final = base + bonus
  │    │     ├─ currentHealth = MaxHealth, currentEnergy = MaxEnergy
  │    │     └─ reset skillCooldowns
  │    ├─ brain.SetViewPrefab(viewPrefab) → SpawnView()
  │    │     ├─ Instantiate view dưới transform của brain
  │    │     └─ view.RegisterOwner(this)  → propagage Owner xuống các ColliderOwnerRef con
  │    └─ EquipLoadout(...) → EquipFirepowerCore / EquipMobilityCore / EquipDefenseCore
  └─ các frame sau: Update (tick cooldown) + FixedUpdate (di chuyển, gọi View)
```

`Awake()` của TankBrain: cache `Rigidbody2D` (trên chính GameObject, fallback child), ensure stats != null, `RecalculateStats()`, HP/Energy <= 0 → full.

### 2.3 Hệ thống Bonus Stats theo key

Mục đích: nhiều nguồn buff/efffect cùng cộng stat mà không đè lên nhau.

```csharp
AddBonusStats(key, delta)     // thêm/thay block theo key (1 key = 1 block, thay thế nếu trùng)
RemoveBonusStats(key)          // gỡ block
ClearBonusStats()              // xóa toàn bộ (khi hết buff)
// bên trong: RecalculateBonusStats():
//   bonusStats = Σ m_BonusStatsByKey.Values  →  RecalculateStats() → clamp HP/Energy theo Max mới
```

Lưu ý hiện tại: `AddBonusStats` **thay thế** block cùng key (phù hợp "buff mới nhất thắng"), `DamageMultiplier` mặc định của `Zeroed()` là **0** nên nếu source cộng thẳng có thể làm giảm hệ số — khi thiết kế buff mới cần dùng delta có `DamageMultiplier` phù hợp (xem mục 7 — điểm cần chú ý).

### 2.4 Input & Action model

TankBrain **không đọc input trực tiếp** — nó nhận lệnh (command) từ `GameController`:

| Method | Ý nghĩa |
|---|---|
| `SetMoveInput(Vector2)` | Nhận hướng di chuyển (clamp magnitude ≤ 1) |
| `SetLookTarget(Vector2 worldPoint)` | Nhận điểm ngắm trong world (chuột); set `m_HasLookTarget` |
| `ClearLookTarget()` | Tắt Look, nòng giữ nguyên hướng |
| `UseSkill(int index)` | Dùng skill 0..3 (xem 2.5) |

Mỗi frame:
- `Update()` → `TickCooldowns(deltaTime)` (trừ cooldown về 0).
- `FixedUpdate()`:
  - Physics: `m_Body.linearVelocity = m_MoveInput * Stats.MoveSpeed`.
  - Gọi `view.Move(dir)` hoặc `view.Stop()` (animation + xoay thân).
  - Nếu có look target → `view.Look(target)` **liên tục mỗi frame** (nòng bám mục tiêu, xử lý smoothing trong `TankView.UpdateLookRotation`).

Phân loại action (trong `TankView`): `Move`/`UseSkill*` là action "đóng" (một cái chạy tại một thời điểm), `Look` là action **liên tục chạy song song** (không dùng chung `CurrentAction`).

### 2.5 Framework Skill (4 slot)

Quy trình dùng skill — **cooldown check tập trung ở base class**, hành vi cụ thể ở lớp con:

```
UseSkill(index)
  ├─ check index ∈ [0, SkillCount)
  ├─ check skillCooldowns[index] <= 0        ← nếu đang CD thì bỏ qua (im lặng)
  ├─ skillCooldowns[index] = GetCooldown(index)
  │     └─ GetCooldownWithReduction(GetBaseCooldown(index))
  │           └─ baseCooldown * (1 - clamp(Stats.CooldownReduction, 0, 1))
  └─ OnUseSkill(index)                       ← VIRTUAL: lớp con override để bắn skill thật
```

- `GetBaseCooldown(int index)` — `virtual`, mặc định `5f`; lớp con trả về CD theo từng skill.
- `OnUseSkill(int index)` — `virtual`, mặc định `Debug.Log` + hiện TextDamage demo; lớp con override.
- Cooldown vẫn trừ trong `Update()` kể cả khi chưa bao giờ dùng skill (vô hại).

**Quy ước:** lớp con *chỉ* override `GetBaseCooldown` + `OnUseSkill`; *không* đụng vào việc tick CD.

### 2.6 Combat & Equipment

- `TakeDamage(float)` — `virtual`, hiện **TODO**: `currentHealth -= amount` (chưa tính Defense/ArmorPenetration).
- `Heal / GainEnergy / SpendEnergy` — clamp theo `Stats.MaxHealth/MaxEnergy`.
- `IsAlive => currentHealth > 0`.
- Equipment: dictionary `EquipmentSlot → EquipmentBrain`, 3 slot `Firepower/Mobility/Defense`.
  `EquipXCore(prefab)` → `Instantiate` prefab dưới brain → `instance.Equip(this)` (cộng stat + gán owner).
  `UnequipEquipment(slot)` → `equipment.UnEquip(this)`.

### 2.7 TankView — phần hiển thị

- Spawn **prefab riêng** (`SetViewPrefab`) chứ không nằm sẵn trong prefab TankBrain — cho phép 1 definition nhiều skin (`viewPrefabs[]`, index = `IdView`).
- Chỉ chứa: `Animator`, `firepowerPivot` (nòng, xoay độc lập), `bodyPivot` (thân + bánh), `muzzle`, smoothing `lookSmoothing`.
- Look được xử lý với góc ngắn nhất (`Mathf.DeltaAngle`) + exponential smoothing, rotation **world space** (không phụ thuộc parent xoay).
- `RegisterOwner` → `CollectOwnerRefs()` + `PropagateOwner()` đẩy owner xuống mọi `ColliderOwnerRef` con, để đối phương raycast/trigger biết đối tượng va chạm là ai.

## 3. EnemyBrain — thiết kế & logic

### 3.1 Vai trò & phân cấp

`EnemyBrain` (abstract) là **cha của mọi địch**, chứa logic + runtime state:

```
EnemyBrain (abstract)                 ← stats, HP, combat, hooks
  ├─ NormalEnemyBrain (abstract)      ← framework địch thường (để trống cho con)
  │    └─ NormalEnemyBrain_Test       ← ví dụ triển khai cụ thể
  └─ EliteEnemyBrain (abstract)       ← địch tinh anh: cơ chế Mang Nang Lượng
       └─ EliteEnemyBrain_Test
```

Khác với TankBrain:
- **Không có hệ thống skill chung** — "KY NANG: hoàn toàn do LOP CON quản lý. EnemyBrain không chứa gì về skill" (mỗi lớp con tự tạo field CD/timer + method riêng).
- Stats **chỉnh trực tiếp trên prefab** (`EnemyStats` serialize ngay trên Brain), không qua definition/level table (chưa có).
- Visual tách thành `EnemyModel` nhưng **nằm cùng prefab** (khác TankView là prefab riêng).

### 3.2 Trạng thái & lifecycle

Fields: `model`, `displayName`, `stats (EnemyStats)`, `currentHealth`, `m_Initialized`, `m_Dead`.

```
Awake()      → tự tìm model trong children (nếu Inspector trống),
               model.RegisterOwner(this),
               nếu chưa Initialize() → Initialize() (cho prefab đặt thẳng vào scene lúc debug)
Initialize(stats?) → gán stats override (nếu có), currentHealth = MaxHealth,
               m_Dead = false, RegisterOwner, model.PlaySpawn()
OnEnable/OnDisable → subscribe/unsubscribe model.Contact → HandleModelContact → OnContact
Update()     → nếu IsAlive: OnBrainTick(deltaTime)     ← MỖI FRAME, AI/di chuyền/timers
```

`IsAlive => !m_Dead && currentHealth > 0`.

### 3.3 Combat flow

```
TakeDamage(amount)                 // virtual
  ├─ if (!IsAlive) return
  ├─ currentHealth -= amount       // TODO: tính Defense
  ├─ model.PlayHit()               // visual
  ├─ OnDamaged(amount)             // hook virtual (phản ứng khi trúng đòn)
  └─ if (currentHealth <= 0) → currentHealth = 0 → Die()
        ├─ guard m_Dead (chỉ chết 1 lần)
        ├─ model.PlayDeath()
        └─ OnDeath()               // hook abstract BẮT BUỘC (drop item, spawn effect...)
Heal(amount) → clamp về MaxHealth.
```

### 3.4 Hooks — hợp đồng với lớp con

| Hook | Loại | Khi nào gọi | Bắt buộc |
|---|---|---|---|
| `OnBrainTick(float dt)` | `abstract` | mỗi frame khi còn sống | **Có** |
| `OnDeath()` | `abstract` | 1 lần khi chết | **Có** |
| `OnDamaged(float amount)` | `virtual` | mỗi lần nhận sát thương, trước khi check chết | Không |
| `OnContact(ColliderOwnerRef other)` | `virtual` | Model forward sự kiện va chạm lên | Không |

**Template thiết kế địch mới:** kế thừa `NormalEnemyBrain` hoặc `EliteEnemyBrain`, cài 2 abstract, tự quản lý skill/timer riêng trong `OnBrainTick`.

### 3.5 EnemyModel — visual + physical

- Chứa `SpriteRenderer`, `BoxCollider2D`, `ColliderOwnerRef`; **không logic game**.
- `Awake`: tự nối self-references + subscribe `ownerRef.Contact` / `ownerRef.TriggerEnter`.
- `RegisterOwner(EnemyBrain)` do Brain gọi lúc khởi tạo → ghi `owner` và `ownerRef.SetOwner(value)` để bên ngoài (skill, raycast) tìm ra Brain qua collider.
- Forward event: `ownerRef.Contact → Model.Contact → Brain.OnContact`.
- Scaffold animation: `PlaySpawn/PlayHit/PlayDeath()` hiện là method rỗng TODO.
- Cấu trúc vật lý: **Brain là cha (có Rigidbody2D), Model là con** — collider trên Model tự attach vào Rigidbody2D của cha.

### 3.6 EliteEnemyBrain — cơ chế Mang Nang Lượng (khung)

- `energyCoreCount` (mặc định 3), trạng thái mỗi mang: `Closed → Open → Broken`.
- `OpenCores()` mở mang khi vào giai đoạn nhất định → người chơi có `breakWindowDuration` (10s) để `BreakCore(index)`:
  - Phá hết (`AllCoresBroken`) → `OnAllCoresBroken()` (TODO: giảm Defense theo `defenseBreakBonus`).
  - Hết giờ chưa phá hết → `OnBreakWindowExpired()` → `TryCastWeakenSkill()` (hồi chiêu hardcode 5s, `CastWeakenSkill()` TODO debuff).
- Đồng hồ `m_BreakWindowRemaining` + hồi chiêu weaken được tick trong **`OnBrainTick`** — minh họa đúng quy ước: timer của lớp con tự quản lý trong tick của nó.

## 4. Hệ thống hỗ trợ (Core)

### 4.1 OwnerRef — "ai sở hữu object này?"

File: `Assets/Scripts/Core/OwnerRef.cs` — `[DisallowMultipleComponent]`.

**Vấn đề giải quyết:** nhiều hệ thống (skill, raycast, trigger) cần biết *đối tượng va chạm này thuộc về ai* mà không muốn phụ thuộc vào type cụ thể.

```csharp
[SerializeField] UnityEngine.Object owner;                  // để trống = mặc định chính GameObject
public UnityEngine.Object Owner => owner != null ? owner : gameObject;
public void SetOwner(UnityEngine.Object value);
public bool TryGetOwner<T>(out T result) where T : UnityEngine.Object;   // check type an toàn
public T GetOwner<T>() where T : UnityEngine.Object;                      // trả null nếu sai type
public static OwnerRef From(Component / GameObject);                      // lookup tiện ích
```

- `owner` kề `UnityEngine.Object` nên **dùng chung** cho `TankBrain`, `EnemyBrain`, `PlayerData`, `GameObject...` mà không phải đổi code — bên đọc tự kiểm type bằng `TryGetOwner<T>()`.
- Gọi nhầm type → trả `false`/`null` chứ không ném exception (defensive).

### 4.2 ColliderOwnerRef — OwnerRef cho Collider (+ event & tracking)

File: `Assets/Scripts/Core/ColliderOwnerRef.cs` — `[RequireComponent(typeof(Collider2D))]`, kế thừa `OwnerRef`.

**Vai trò:**
1. Giữ `Owner` (kế thừa).
2. Phát event Trigger / Collision dạng C# `event`:
   - `TriggerEnter/Stay/Exit`, `CollisionEnter/Stay/Exit`
   - `Contact` (event gộp: chỉ fire khi đối phương **cũng có** `ColliderOwnerRef` — trả `ColliderOwnerRef` đối phương thay vì `Collider2D` thô).
3. **Theo dõi contact hiện tại** bằng 2 `HashSet<Collider2D>`:
   - `triggerColliders`: Enter → Add, Stay → không đổi, Exit → Remove.
   - `collisionColliders`: tương tự.
   - `FixedUpdate → CleanupInvalidColliders()`: loại collider đã Destroy/Disable/Inactive (đồng bộ với physics).
4. Query API: `IsTriggerInside`, `GetTriggerColliders()`, `TriggerCount`, `IsColliding`, `GetCollisionColliders()`, `IsContacting`, `GetContactOwner(...)`.
5. Owner helpers: `TryGetOtherOwner<T>(other, out result)` — lấy owner của đối phương qua collider.
6. Static lookup: `FromCollider(Collider2D)`, `FromHit(RaycastHit2D)`.
7. Lifecycle dọn dẹp: `OnDisable` clear 2 HashSet; `OnDestroy → ClearEvents()` (null toàn bộ event — tránh leak subscriber); `OnValidate` warn nếu thiếu Collider2D.

**Event flow chuẩn khi va chạm:**

```
Unity physics (2D)
  → ColliderOwnerRef.OnTriggerEnter2D(other)
       ├─ triggerColliders.Add(other)
       ├─ TriggerEnter?.Invoke(other)                       ← subscriber kiểu Collider2D
       └─ if FromCollider(other) != null → Contact?.Invoke(otherRef)   ← subscriber kiểu ColliderOwnerRef
            └─ EnemyModel.HandleContact → Model.Contact → EnemyBrain.OnContact
```

**Pattern đọc owner từ collider (dùng trong skill):**

```csharp
ColliderOwnerRef targetRef = ColliderOwnerRef.FromCollider(collider);
if (targetRef != null && targetRef.TryGetOwner(out EnemyBrain enemy)) { ... }
// hoặc: targetRef.GetOwner<EnemyBrain>()
```

### 4.3 TextDamageManager — singleton pooling hiển thị sát thương

File: `Assets/Scripts/Core/TextDamageManager.cs` — `[DisallowMultipleComponent]`.

**API duy nhất cần nhớ:**

```csharp
TextDamageManager.Instance?.Show(damage, worldPosition);
// damage: float (làm tròn sang int để hiển thị), worldPosition: vị trí gây sát thương
```

**Cách hoạt động:**

- **Singleton tự tạo:** truy cập `Instance` mà chưa có trong scene → `FindAnyObjectByType` → nếu vẫn null thì `new GameObject` + `AddComponent`. `Awake` guard instance trùng (giữ instance đầu tiên). `DontDestroyOnLoad` hiện đang **bị comment**.
- **Pooling:** `prewarmCount` (mặc định 20) object tạo sẵn trong container `TextDamagePool`; `maxActiveCount` (64) — vượt quá thì **tái sử dụng item cũ nhất** (đầu list `m_Active`), không tạo thêm.
- **Show():**
  1. `RentItem()` (pop pool / tái sử dụng / tạo mới).
  2. Spawn ngẫu nhiên trong bán kính `spawnRadius` quanh vị trí gây sát thương.
  3. Hướng bay = vector từ tâm → vị trí spawn (ra ngoài), luôn thêm `Vector3.forward` để nổi trên 2D.
  4. Set font/size/text (float → `Mathf.RoundToInt`), `ForceMeshUpdate()`, set outline (color/width + keyword `OUTLINE_ON` trên **material instance** riêng của TextDamage), `sortingOrder` để hiển thị trên sprite (URP 2D).
  5. Reset visual tại t=0 theo curve, bật GameObject, thêm vào `m_Active`.
- **Update():** duyệt `m_Active` **ngược** (RemoveAt an toàn index):
  - đủ `lifetime` (0.75s) → tắt + push về pool;
  - di chuyển `Direction * moveSpeed * dt`;
  - `ApplyVisual(progress)` → alpha theo `alphaOverLifetime`, scale theo `scaleOverLifetime` (2 AnimationCurve RIÊNG, mặc định giữ 1 đến 70% lifetime rồi về 0).
- Item = struct `ActiveItem` cache sẵn `GameObject/Transform/TextMeshPro/MeshRenderer/Direction/BaseScale/Elapsed` (tránh GetComponent trong Update).
- Text dùng **`TextMeshPro` (world space)**, không phải TMP_UGUI.

### 4.4 ReadOnlyAttribute + ReadOnlyDrawer

`Assets/Scripts/Core/ReadOnlyAttribute.cs` + `Assets/Scripts/Editor/ReadOnlyDrawer.cs`:
`[SerializeField, ReadOnly]` → hiển thị field trong Inspector **không cho sửa** (dùng cho `baseStats`, `bonusStats`, `level`, `view`, `m_context` của TankBrain — dữ liệu do code ghi, designer không sửa tay).

## 5. Ví dụ thực tế: TankBrain_BlackShadow

File: `Assets/Scripts/Tanks/BlackShadow/TankBrain_BlackShadow.cs`
Namespace: `IronWasteland.Tanks.BlackShadow`, kế thừa `TankBrain`.

BlackShadow là tank mẫu hiện đang chạy trong project với **2 skill**:

| Slot | Index | Skill | Loại | Damage (Inspector) | Cooldown |
|---|---|---|---|---|---|
| J | `SkillTest1Index = 0` | `Skill_Test1` | Đạn đạo (projectile) bắn từ Muzzle theo hướng Look | `skillTest1Damage = 36` | `skillTest1Cooldown = 5s` |
| K | `SkillTest2Index = 1` | `Skill_Test2` | Vùng (AoE) đặt tại vị trí Look, đánh theo nhịp | `skillTest2Damage = 10` | `skillTest2Cooldown = 5s` |
| L / I | 2 / 3 | — | Chưa ghi đè → fallback `base.OnUseSkill` (log demo) | — | 5s mặc định |

(Input do `GameController`: J/K/L/I — kích hoạt khi **nhả phím** `wasReleasedThisFrame`.)

### 5.1 Những gì BlackShadow override (đúng framework của TankBrain)

```csharp
protected override float GetBaseCooldown(int index)   // trả CD riêng theo index skill
protected override void OnUseSkill(int index)         // dispatch: index 0 → FireSkillTest1(),
                                                      //          index 1 → FireSkillTest2(),
                                                      //          khác → base.OnUseSkill(index)
```

Ngoài ra `Start()` prewarm 2 pool, `OnDestroy()` dọn toàn bộ instance + pool.

### 5.2 Kiến trúc Pooling của skill

Mỗi skill có **Design prefab + Pool Stack + HashSet theo dõi tất cả instance**:

```
[SerializeField] Skill_Test1 skillTest1Design;     // GameObject mẫu INACTIVE làm Design
Stack<Skill_Test1>   m_SkillPool;                  // pool rảnh
HashSet<Skill_Test1> m_AllSkills;                  // mọi instance đã tạo (để OnDestroy dọn)

Start() → PrewarmSkillPool():  CreateSkillInstance() × initialPoolSize (4) → push pool

CreateSkillInstance():
  Instantiate(design) → SetParent(null)            // tách khỏi TankBrain, đạn bay độc lập
  → SetActive(true) → Clear() → SetActive(false)   // đánh thức cache component + reset trạng thái
  → m_AllSkills.Add

GetSkillFromPool():   pop đến khi gặp instance còn sống (bỏ qua ref đã Destroy);
                      pool rảnh → CreateSkillInstance()
ReturnSkillToPool(s): Clear() → SetActive(false) → push (guard Contains chống push trùng)
```

Skill 2 tương tự (`skillTest2InitialPoolSize = 2`, pool/HashSet riêng).

**Nguyên tắc pooling của project:** instance của skill **không bao giờ bị Destroy** khi hết hiệu lực — chỉ `SetActive(false)` và trả về pool; `Destroy` chỉ xảy ra ở `OnDestroy()` của Brain.

### 5.3 Skill_Test1 — bắn đạn (projectile)

Prefab bắt buộc: `[RequireComponent(Rigidbody2D)]`, `[RequireComponent(ColliderOwnerRef)]`.
Inspector: `speed = 10`, `lifetime = 5s`, `hitCheckDelay = 0.1s`.

**Luồng bắn (`FireSkillTest1`):**

```
1. Guard: skillTest1Design != null, View/Muzzle có, HasLookTarget
2. direction = LookTarget - Muzzle.position (normalize; nếu ~0 → bỏ qua)
3. angle = Atan2 → rotation Quaternion.Euler(0,0,angle)
4. skill = GetSkillFromPool()
5. SetParent(null); SetPositionAndRotation(muzzle.position, rotation)
6. skill.Initialize(direction, ReturnSkillToPool)   ← Clear() bên trong, đăng ký return callback
7. skill.TargetFound += OnSkillTest1TargetFound       ← đăng ký TƯƠNG ỨNG VỚI LẦN DÙNG
8. skill.gameObject.SetActive(true)                  ← BẬT SAU CÙNG
```

**Luồng đạn (`Skill_Test1`):**

```
Initialize(): Clear() → m_Direction → transform.right = direction
              → m_Body.linearVelocity = direction * speed
              → subscribe m_OwnerRef.TriggerEnter
FixedUpdate(): giữ vận tốc (direction * speed)
Update():      đếm elapsed ≥ lifetime → FinishProjectile()
OnTriggerEntered(): lần va chạm đầu → coroutine CheckTargetsAfterDelay (chờ hitCheckDelay 0.1s)
CheckTargetsAfterDelay():
   quét m_OwnerRef.GetTriggerColliders()
     → ColliderOwnerRef.FromCollider → GetOwner<EnemyBrain>() → tập HashSet<EnemyBrain> (bỏ trùng)
   nếu không có địch → yield break (đạn tiếp tục bay)
   nếu có → m_HasHit = true
        → TargetFound?.Invoke(this, enemy) MỖI enemy    ← brain nhận event → trừ HP + TextDamage
        → FinishProjectile(): lưu callback → Clear() → returnToPool(this)
```

**Xử lý event subscriber:** `Clear()` đặt `TargetFound = null` (xóa **toàn bộ** subscriber của lần dùng trước) — tránh leak cộng dồn do mỗi lần bắn lại `+=`; `OnDisable` cũng gọi `Clear()`.

**Xử lý callback return:** `FinishProjectile` lưu `m_ReturnToPool` vào biến local **trước khi** `Clear()` (vì `Clear()` set nó = null), rồi mới gọi — pattern này lặp lại ở cả 2 skill.

### 5.4 Skill_Test2 — vùng sát thương theo nhịp (AoE)

Prefab bắt buộc: `[RequireComponent(CircleCollider2D)]` (set `isTrigger = true` trong `Awake`), `[RequireComponent(ColliderOwnerRef)]`.
Inspector: `duration = 3s`, `interval = 0.5s`.

**Luồng dùng:**

```
TankBrain_BlackShadow.FireSkillTest2():
   guard design + HasLookTarget
   skill = pool; SetParent(null); position = LookTarget (tâm vùng); SetActive(true)
   skill.Initialize(LookTarget, ReturnSkillTest2ToPool)
   skill.TargetFound += OnSkillTest2TargetFound
   skill.StartSkill()                       ← đăng ký event TRƯỚC khi khởi động vòng lặp

Skill_Test2.StartSkill() → coroutine AreaLoop():
   while (elapsed < duration):
       FindTargets()                        // quét GetTriggerColliders → HashSet<EnemyBrain>
                                             // → TargetFound?.Invoke mỗi enemy MỖI NHỊP
       yield WaitForSeconds(min(interval, duration - elapsed))
   → FinishSkill(): lưu callback → Clear() → returnToPool(this)
```

Khác Skill 1: **không cần va chạm mới quét** — mỗi `interval` quét 1 lần toàn bộ địch đang nằm trong vùng; `interval` < `duration` nên địch trong vùng ăn nhiều lần sát thương (10 dmg / 0.5s trong 3s).

### 5.5 Xử lý trúng đòn (Brain side)

Cả 2 skill đều chung một handler pattern trong `TankBrain_BlackShadow`:

```csharp
private void OnSkillTest1TargetFound(Skill_Test1 skill, EnemyBrain enemy)
{
    if (enemy == null) return;
    enemy.TakeDamage(skillTest1Damage);                                    // logic
    TextDamageManager.Instance?.Show(skillTest1Damage, skill.transform.position); // UI
}
// OnSkillTest2TargetFound tương tự, dùng skillTest2Damage
```

### 5.6 Sơ đồ tổng: 1 lần bắn Skill 1

```
Người chơi nhả phím J
 → GameController.Update → tank.UseSkill(0)
 → TankBrain.UseSkill: cooldown OK → skillCooldowns[0] = 5 * (1 - CDR) → OnUseSkill(0)
 → TankBrain_BlackShadow.OnUseSkill → FireSkillTest1()
 → pool → Initialize + TargetFound += handler → SetActive(true)
 → đạn bay (FixedUpdate) → trigger chạm collider địch
 → ColliderOwnerRef.TriggerEnter → Skill_Test1.OnTriggerEntered → delay 0.1s
 → quét triggerColliders → ColliderOwnerRef.FromCollider → GetOwner<EnemyBrain>()
 → TargetFound(this, enemy)
 → enemy.TakeDamage(36) → EnemyModel.PlayHit → (HP ≤ 0 → Die → OnDeath)
 → TextDamageManager.Instance.Show(36, vị trí đạn)   (pool → bay ra → fade → về pool)
 → FinishProjectile → Clear → ReturnSkillToPool → SetActive(false)
```

---

## 6. Công thức thiết kế (recipes)

### 6.1 Thêm skill mới cho một TankBrain con

1. Tạo script skill mới (ví dụ `Skill_X.cs`) trong thư mục tank:
   - `[RequireComponent]` đúng collider + `ColliderOwnerRef` (và `Rigidbody2D` nếu là đạn).
   - State machine tối thiểu: `m_Initialized`, `Clear()`, `Initialize(args, returnToPool)`, `FinishX()` (lưu callback trước khi `Clear()`).
   - Event `TargetFound` (hoặc event phù hợp), `Clear()` phải set `TargetFound = null`.
   - `OnDisable → Clear()`.
2. Trên prefab Design (GameObject **inactive**): gắn đủ component, đặt trong prefab tank.
3. Trên `TankBrain_<TenTank>`:
   - Thêm `[SerializeField] Skill_X skillXDesign`, pool `Stack`, `HashSet`, số lượng prewarm, damage, cooldown.
   - Override `GetBaseCooldown(index)` cho index của skill.
   - Override `OnUseSkill(index)` → `FireSkillX()`.
   - Prewarm trong `Start()`, dọn trong `OnDestroy()`.
4. Gán Design prefab vào Inspector của tank prefab, bật key tương ứng trong `GameController` (J/K/L/I).

### 6.2 Thêm Tank mới

1. Tạo thư mục `Assets/Scripts/Tanks/<TenTank>/`, class `TankBrain_<TenTank> : TankBrain`.
2. Tạo `TankDefinition` asset (`Create → IronWasteland/Tank → Tank Definition`): bảng `StatsEntry` theo level (phải tăng dần, không trùng level), `tankPrefab`, `viewPrefabs[]`, `equipmentDatabase`, `context` mặc định.
3. Prefab TankBrain (chỉ Brain, **không** có View) + prefab TankView riêng (sprite/anim/pivots/muzzle).
4. Gán vào `GameController` (definition, level, viewId, equipment ids, vị trí spawn).

### 6.3 Thêm Enemy mới

1. Kế thừa `NormalEnemyBrain` hoặc `EliteEnemyBrain` (không kế thừa `EnemyBrain` trực tiếp nếu là loại địch thường/tinh anh đã có framework).
2. Cài `OnBrainTick(float)` (AI + timer + skill riêng) và `OnDeath()`.
3. Prefab: Rigidbody2D trên root (Brain), child `EnemyModel` có `SpriteRenderer + BoxCollider2D + ColliderOwnerRef`; Inspector trực tiếp `EnemyStats`.
4. `OnValidate` của Brain/Model tự nối reference — nhưng vẫn nên gán tay lần đầu và kiểm tra Console warning.

### 6.4 Quy ước chung cần tuân thủ

- **Không** đọc input trong Brain — chỉ nhận command (`SetMoveInput`, `SetLookTarget`, `UseSkill`).
- **Không** đặt logic game trong View/Model — chỉ visual + forward event.
- **Không** sửa `baseStats` tay (Inspector `[ReadOnly]`) — dùng `AddBonusStats/RemoveBonusStats` theo key.
- Skill/đạn qua **pool**, không Instantiate/Destroy liên tục.
- Luôn `Clear()` trước `Initialize()`; đăng ký event **sau** `Initialize`, trước `SetActive(true)`.
- Giải phóng event trong `OnDisable/OnDestroy` (`ClearEvents()` hoặc `-= ` thủ công).
- Dùng `ColliderOwnerRef.FromCollider(...)` + `TryGetOwner<T>` để tra owner — không dùng `GetComponent` trực tiếp ở nơi khác.

## 7. Điểm đã biết / TODO hiện tại trong code

Những mục này là **scaffold có chủ đích** (code đánh dấu TODO), cần cập nhật tài liệu khi làm xong:

| Hệ thống | TODO |
|---|---|
| `TankBrain.TakeDamage` | Chưa tính `Defense` / `ArmorPenetration` / `DamageMultiplier` |
| `EnemyBrain.TakeDamage` | Chưa tính `Defense` của địch |
| `TankBrain.GetBaseCooldown` | Mặc định hardcode 5s — bảng cooldown thật theo từng skill chưa có ở base |
| `TankBrain.FixedUpdate` | Di chuyển chỉ set `linearVelocity` thẳng — chưa có dash, do lội, terrain |
| `EliteEnemyBrain` | `OnAllCoresBroken` (giảm Defense), `CastWeakenSkill` (debuff) chưa làm — chưa có hệ thống buff/debuff |
| `EnemyModel` | `PlaySpawn/PlayHit/PlayDeath` là method rỗng (chưa có animation/VFX) |
| `TextDamageManager` | `DontDestroyOnLoad` đang bị comment — cần quyết định khi có nhiều scene |
| `GameController` | Input tạm thời if/else trực tiếp (chưa qua Input Actions asset) |
| `TankBrain.AddBonusStats` | Nhánh ContainsKey true/false làm cùng một việc (code thừa) — nên dọn |
| `GameController.Awake` | `transform.position.Set(...)` không có tác dụng gán vị trí (Set trả giá trị, không mutate) — bug tiềm ẩn, cần sửa thành gán trực tiếp |

### Thứ tự ưu tiên khi mở rộng

1. **Heal/Defense pipeline** (cả 2 Brain) → mở khóa equipment Defense có ý nghĩa.
2. **Hệ buff/debuff dùng `AddBonusStats(key, delta)`** — tiền đề cho Elite weaken skill.
3. **Bang cooldown thật per-skill** thay `GetBaseCooldown` hardcode.
4. **Enemy AI di chuyển/truy đuổi** trong `OnBrainTick` (dùng `Stats.MoveSpeed`).

---

*Tài liệu tạo từ codebase IronWasteland (branch `TankBrain`). Cập nhật khi thay đổi API.*







