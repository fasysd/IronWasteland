using System;
using System.Collections;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using IronWasteland.Enemies;
using IronWasteland.Enemies.DeadlandPhantom;
using IronWasteland.Enemies.ElementalReaper;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Enemies.ElementalReaper.VFX;
using IronWasteland.Tanks;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    public static class ElementalReaper_Component4GameplayTest
    {
        private static int s_Passed;
        private static int s_Failed;
        private static readonly List<string> s_Logs = new List<string>();

        private const string BossPrefabPath = "Assets/Prefabs/Enemies/TuThanNguyenTo.prefab";
        private const string ClonePrefabPath = "Assets/Prefabs/Enemies/ElementalReaperClone.prefab";
        private const string ArenaPrefabPath = "Assets/Prefabs/Enemies/ElementalAltarsArena.prefab";
        private const string CrystalPrefabPath = "Assets/Prefabs/Enemies/ElementalCrystal.prefab";
        private const string VfxFolder = "Assets/Art/Enemies/TuThanNguyenTo/VFX/Prefabs";

        public static string RunAllTests()
        {
            s_Passed = 0;
            s_Failed = 0;
            s_Logs.Clear();

            s_Logs.Add("=== BOSS 8: ELEMENTAL REAPER COMPONENT 4 GAMEPLAY TEST SUITE ===");

            Test1_PrefabsIntegrityAndWiring();
            Test2_BaselineStatsAndCoopScaling();
            Test3_CrowdControlImmunity();
            Test4_ElementalShiftPassive15s();
            Test5_NormalPhaseSkillElementalRestriction();
            Test6_FacingAndRotationRules();
            Test7_CriticalConstraint_NoColliderOwnerRefOnHitboxes();
            Test8_ElementalCrystalMechanics();
            Test9_CloneBehaviorAndSkillExecution();
            Test10_EnergyBurstLifecycle_Charging();
            Test11_EnergyBurstLifecycle_Active_SoloAdaptation();
            Test12_EnergyBurstLifecycle_Active_CoopAdaptation();
            Test13_RewardPhase_SuccessResolution();
            Test14_PenaltyPhase_FailureAndPermanent6SkillsUnlock();
            Test15_ImmediateSkillReadinessOnBurstTransitions();

            s_Logs.Add($"\nRESULT: {s_Passed}/{s_Passed + s_Failed} tests passed ({(s_Failed == 0 ? "100% SUCCESS" : "FAILURES DETECTED")}).");
            return string.Join("\n", s_Logs);
        }

        private static void Run(string testName, Action testAction)
        {
            try
            {
                testAction();
                s_Passed++;
                s_Logs.Add($"[PASS] {testName}");
            }
            catch (Exception ex)
            {
                s_Failed++;
                s_Logs.Add($"[FAIL] {testName}: {ex.Message}");
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }

        #region Tests 1-3: Setup, Stats, CC

        private static void Test1_PrefabsIntegrityAndWiring()
        {
            Run("1.1 All Boss 8 Prefabs Exist and Load", () =>
            {
                var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
                Assert(boss != null, $"Boss prefab missing at {BossPrefabPath}");

                var clone = AssetDatabase.LoadAssetAtPath<GameObject>(ClonePrefabPath);
                Assert(clone != null, $"Clone prefab missing at {ClonePrefabPath}");

                var arena = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaPrefabPath);
                Assert(arena != null, $"Arena prefab missing at {ArenaPrefabPath}");

                var crystal = AssetDatabase.LoadAssetAtPath<GameObject>(CrystalPrefabPath);
                Assert(crystal != null, $"Crystal prefab missing at {CrystalPrefabPath}");
            });

            Run("1.2 Boss Prefab Wiring with Brain, Arena, VFX", () =>
            {
                var boss = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
                var brain = boss.GetComponent<ElementalReaperBrain>();
                Assert(brain != null, "ElementalReaperBrain missing on TuThanNguyenTo.prefab root");

                var vfxCtrl = boss.GetComponent<ElementalReaperVFXController>();
                Assert(vfxCtrl != null, "ElementalReaperVFXController missing on TuThanNguyenTo.prefab root");

                Assert(brain.ElementalAltarsArenaPrefab != null, "elementalAltarsArenaPrefab slot is not wired on brain");
            });
        }

        private static void Test2_BaselineStatsAndCoopScaling()
        {
            Run("2.1 Solo Baseline Stats (9500 HP, 250 ATK, 25 DEF, 90 SPD)", () =>
            {
                GameObject go = new GameObject("BossTestObj");
                try
                {
                    var brain = go.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    Assert(Mathf.Approximately(brain.MaxHealth, 9500f), $"MaxHealth expected 9500, got {brain.MaxHealth}");
                    Assert(Mathf.Approximately(brain.CurrentHealth, 9500f), $"CurrentHealth expected 9500, got {brain.CurrentHealth}");
                    Assert(Mathf.Approximately(brain.Stats.Attack, 250f), $"Attack expected 250, got {brain.Stats.Attack}");
                    Assert(Mathf.Approximately(brain.Stats.Defense, 25f), $"Defense expected 25, got {brain.Stats.Defense}");
                    Assert(Mathf.Approximately(brain.Stats.MoveSpeed, 90f), $"MoveSpeed expected 90, got {brain.Stats.MoveSpeed}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });

            Run("2.2 Co-op Scaling (19000 HP for 2 players)", () =>
            {
                GameObject go = new GameObject("BossCoopTestObj");
                try
                {
                    var brain = go.AddComponent<ElementalReaperBrain>();
                    EnemyStats coopStats = new EnemyStats
                    {
                        MaxHealth = 19000f,
                        Attack = 250f * 1.2f,
                        Defense = 25f * 1.1f,
                        MoveSpeed = 90f * 1.05f
                    };
                    brain.Initialize(coopStats);

                    Assert(Mathf.Approximately(brain.MaxHealth, 19000f), $"Co-op MaxHealth expected 19000, got {brain.MaxHealth}");
                    Assert(Mathf.Approximately(brain.Stats.Attack, 300f), $"Co-op Attack expected 300, got {brain.Stats.Attack}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });
        }

        private static void Test3_CrowdControlImmunity()
        {
            Run("3.1 Hard CC Immunity (Stun/Root immune, Slow vulnerable)", () =>
            {
                GameObject go = new GameObject("BossCCTestObj");
                try
                {
                    var brain = go.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    Assert(brain.IsImmuneToCrowdControl(CrowdControlType.Stun), "Boss must be immune to Stun");
                    Assert(brain.IsImmuneToCrowdControl(CrowdControlType.Root), "Boss must be immune to Root");
                    Assert(!brain.IsImmuneToCrowdControl(CrowdControlType.Slow), "Boss must NOT be immune to Slow");
                    Assert(!brain.IsImmuneToCrowdControl(CrowdControlType.Knockback), "Boss must NOT be immune to Knockback");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });
        }

        #endregion

        #region Tests 4-6: Elements, Skills, Rotation

        private static void Test4_ElementalShiftPassive15s()
        {
            Run("4.1 15s Cycle Water -> Poison -> Fire -> Water", () =>
            {
                GameObject go = new GameObject("BossShiftTestObj");
                try
                {
                    var brain = go.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    Assert(brain.CurrentElement == ElementalType.Water, "Initial element must be Water");

                    // Shift 1
                    brain.ShiftToNextElement();
                    Assert(brain.CurrentElement == ElementalType.Poison, "After 1st shift must be Poison");

                    // Shift 2
                    brain.ShiftToNextElement();
                    Assert(brain.CurrentElement == ElementalType.Fire, "After 2nd shift must be Fire");

                    // Shift 3
                    brain.ShiftToNextElement();
                    Assert(brain.CurrentElement == ElementalType.Water, "After 3rd shift must wrap to Water");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });
        }

        private static void Test5_NormalPhaseSkillElementalRestriction()
        {
            Run("5.1 Normal Phase Skill Elemental Restriction", () =>
            {
                GameObject go = new GameObject("BossSkillRestrictTest");
                try
                {
                    var brain = go.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();
                    brain.SetCurrentElement(ElementalType.Water);

                    // Skill timers: all ready
                    Assert(!brain.IsPermanentAllSkillsUnlocked, "Permanent unlock must be false initially");
                    Assert(brain.CurrentElement == ElementalType.Water, "Current element must be Water");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });
        }

        private static void Test6_FacingAndRotationRules()
        {
            Run("6.1 Rotation Rules (Move towards dir, attack towards target, self towards 0)", () =>
            {
                GameObject go = new GameObject("BossRotationTest");
                try
                {
                    var brain = go.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    // 1. Rotate to zero
                    brain.RotateToZero(instant: true);
                    Assert(Mathf.Approximately(go.transform.eulerAngles.z, 0f), $"Expected 0 rotation, got {go.transform.eulerAngles.z}");

                    // 2. Rotate towards target (e.g. at (10, 0) -> 0 deg; at (0, 10) -> 90 deg)
                    brain.RotateTowardsTarget(new Vector2(0f, 10f), instant: true);
                    float angle = go.transform.eulerAngles.z;
                    Assert(Mathf.Abs(angle - 90f) < 0.1f, $"Expected 90 degrees towards (0, 10), got {angle}");

                    // 3. Rotate towards velocity (e.g. (-10, 0) -> 180 deg)
                    brain.RotateTowardsDirection(new Vector2(-1f, 0f), instant: true);
                    float moveAngle = go.transform.eulerAngles.z;
                    Assert(Mathf.Abs(moveAngle - 180f) < 0.1f, $"Expected 180 degrees towards (-1, 0), got {moveAngle}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });
        }

        #endregion

        #region Test 7: Critical User Constraint

        private static void Test7_CriticalConstraint_NoColliderOwnerRefOnHitboxes()
        {
            Run("7.1 Critical Constraint: No ColliderOwnerRef on Skill Hitbox Prefabs", () =>
            {
                string[] hitboxPrefabs = new string[]
                {
                    VfxFolder + "/VFX_ThuyTrieuHitbox.prefab",
                    VfxFolder + "/VFX_DocQuatHitbox.prefab",
                    VfxFolder + "/VFX_LoanChuyenBurstHitbox.prefab",
                    VfxFolder + "/VFX_PhunDungNhamHitbox.prefab",
                    VfxFolder + "/VFX_FireTornadoZone.prefab",
                    VfxFolder + "/VFX_HoaTocFireTrailZone.prefab",
                    CrystalPrefabPath
                };

                foreach (var path in hitboxPrefabs)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert(prefab != null, $"Prefab missing at {path}");

                    var ownerRef = prefab.GetComponent<ColliderOwnerRef>();
                    Assert(ownerRef == null, $"CRITICAL CONSTRAINT VIOLATION: {path} has ColliderOwnerRef! Bullets would damage Boss.");
                }

                // Check child HoaTocDashHitbox on TuThanNguyenTo
                var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPrefabPath);
                var dashObj = bossPrefab.transform.Find("HoaTocDashHitbox");
                Assert(dashObj != null, "HoaTocDashHitbox child missing on Boss prefab");
                Assert(dashObj.GetComponent<ColliderOwnerRef>() == null, "CRITICAL CONSTRAINT VIOLATION: HoaTocDashHitbox has ColliderOwnerRef!");
            });

            Run("7.2 Bullet Hit Simulation on Hitbox Returns Null Owner", () =>
            {
                var thuyTrieuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_ThuyTrieuHitbox.prefab");
                GameObject wave = UnityEngine.Object.Instantiate(thuyTrieuPrefab);
                try
                {
                    var col = wave.GetComponent<Collider2D>();
                    Assert(col != null, "ThuyTrieu must have Collider2D");

                    // Simulate bullet resolving owner
                    var ownerRef = ColliderOwnerRef.FromCollider(col);
                    EnemyBrain resolvedEnemy = ownerRef != null ? ownerRef.GetOwner<EnemyBrain>() : null;

                    Assert(resolvedEnemy == null, "Bullet MUST NOT resolve Boss EnemyBrain from skill hitbox!");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(wave);
                }
            });
        }

        #endregion

        #region Tests 8-9: Crystals & Clones

        private static void Test8_ElementalCrystalMechanics()
        {
            Run("8.1 Crystal Effects (Circle: Stun, Square: 10% HP, Triangle: 1-hit Shield + ArmorPen)", () =>
            {
                GameObject tankGo = new GameObject("TestTank");
                GameObject cloneGo = new GameObject("TestClone");
                GameObject crystalGo = new GameObject("TestCrystal");

                try
                {
                    var tank = tankGo.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });

                    var clone = cloneGo.AddComponent<ElementalReaperClone>();
                    clone.Initialize(new EnemyStats { MaxHealth = 2375f, Attack = 250f, Defense = 25f, MoveSpeed = 90f });

                    var crystal = crystalGo.AddComponent<ElementalCrystal>();

                    // 1. Circle: Stun Clone 2s
                    crystal.SetupCrystal(CrystalShape.Circle, ElementalType.Fire, clone);
                    crystal.Collect(tank);
                    Assert(clone.HasCC(CrowdControlType.Stun), "Circle crystal must stun clone 2s");

                    // 2. Square: Deal 10% Max HP to Clone (237.5 dmg)
                    GameObject crystalGo2 = new GameObject("TestCrystalSquare");
                    var crystal2 = crystalGo2.AddComponent<ElementalCrystal>();
                    crystal2.SetupCrystal(CrystalShape.Square, ElementalType.Fire, clone);
                    float preHp = clone.CurrentHealth;
                    crystal2.Collect(tank);
                    float expectedHp = preHp - (clone.MaxHealth * 0.10f);
                    Assert(Mathf.Abs(clone.CurrentHealth - expectedHp) < 1f, $"Square crystal expected HP {expectedHp}, got {clone.CurrentHealth}");
                    UnityEngine.Object.DestroyImmediate(crystalGo2);

                    // 3. Triangle: Tank gains 1-hit shield and 25% ArmorPen for 3s
                    GameObject crystalGo3 = new GameObject("TestCrystalTriangle");
                    var crystal3 = crystalGo3.AddComponent<ElementalCrystal>();
                    crystal3.SetupCrystal(CrystalShape.Triangle, ElementalType.Fire, clone);
                    crystal3.Collect(tank);
                    Assert(tank.CurrentShield > 0f, "Triangle crystal must grant shield to Tank");
                    Assert(Mathf.Abs(tank.Stats.ArmorPenetration - 0.25f) < 0.05f, $"Tank ArmorPen expected 0.25, got {tank.Stats.ArmorPenetration}");
                    UnityEngine.Object.DestroyImmediate(crystalGo3);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tankGo);
                    UnityEngine.Object.DestroyImmediate(cloneGo);
                    UnityEngine.Object.DestroyImmediate(crystalGo);
                }
            });
        }

        private static void Test9_CloneBehaviorAndSkillExecution()
        {
            Run("9.1 Clone Stats (25% Boss Max HP, DEF 25, SPD 90)", () =>
            {
                GameObject cloneGo = new GameObject("CloneStatsTest");
                try
                {
                    var clone = cloneGo.AddComponent<ElementalReaperClone>();
                    EnemyStats stats = new EnemyStats { MaxHealth = 2375f, Attack = 250f, Defense = 25f, MoveSpeed = 90f };
                    clone.Initialize(stats);

                    Assert(Mathf.Approximately(clone.MaxHealth, 2375f), $"Clone MaxHealth expected 2375, got {clone.MaxHealth}");
                    Assert(Mathf.Approximately(clone.Stats.Attack, 250f), $"Clone Attack expected 250, got {clone.Stats.Attack}");
                    Assert(Mathf.Approximately(clone.Stats.Defense, 25f), $"Clone Defense expected 25, got {clone.Stats.Defense}");
                    Assert(Mathf.Approximately(clone.Stats.MoveSpeed, 90f), $"Clone MoveSpeed expected 90, got {clone.Stats.MoveSpeed}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(cloneGo);
                }
            });
        }

        #endregion

        #region Tests 10-15: Energy Burst Lifecycle

        private static void Test10_EnergyBurstLifecycle_Charging()
        {
            Run("10.1 Energy Burst Charging (Threshold 45%, 2s Charging, Immobile, Rotation 0)", () =>
            {
                GameObject bossGo = new GameObject("BossBurstChargingTest");
                try
                {
                    var brain = bossGo.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    Assert(brain.BurstState == EliteBurstState.Inactive, "Initial BurstState must be Inactive");

                    // Trigger Energy Burst (Máu <= 45% = 4275 HP)
                    brain.TriggerEnergyBurst();
                    Assert(brain.BurstState == EliteBurstState.Charging, $"BurstState expected Charging, got {brain.BurstState}");
                    Assert(brain.IsBurstCharging, "IsBurstCharging must be true");
                    Assert(Mathf.Approximately(brain.BurstTimer, 2.0f), $"BurstTimer expected 2.0s, got {brain.BurstTimer}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                }
            });
        }

        private static void Test11_EnergyBurstLifecycle_Active_SoloAdaptation()
        {
            Run("11.1 Burst Active Solo Adaptation (Only Fire Altar Active, Trap 1 Tank)", () =>
            {
                GameObject bossGo = new GameObject("BossBurstSoloTest");
                GameObject arenaGo = new GameObject("ArenaTest");
                GameObject tankGo = new GameObject("SoloTankTest");

                try
                {
                    var brain = bossGo.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    var arena = arenaGo.AddComponent<ElementalAltarsSubArena>();
                    arena.InitializeArena(brain);

                    var tank = tankGo.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                    tank.transform.position = new Vector2(0f, 0f);

                    List<TankBrain> candidateTanks = new List<TankBrain> { tank };
                    EnemyStats cloneStats = new EnemyStats { MaxHealth = 2375f, Attack = 250f, Defense = 25f, MoveSpeed = 90f };

                    arena.StartBurstPhase(candidateTanks, cloneStats);

                    Assert(arena.FireAltar.isActive, "Fire Altar MUST be active in Solo mode");
                    Assert(!arena.PoisonAltar.isActive, "Poison Altar must NOT be active in Solo mode");
                    Assert(!arena.WaterAltar.isActive, "Water Altar must NOT be active in Solo mode");
                    Assert(arena.FireAltar.trappedTank == tank, "Solo tank must be trapped in Fire Altar");
                    Assert((Vector2)tank.transform.position == arena.FireAltar.center + new Vector2(0f, -3f), "Tank must be teleported to Fire Altar offset");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(arenaGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });
        }

        private static void Test12_EnergyBurstLifecycle_Active_CoopAdaptation()
        {
            Run("12.1 Burst Active Co-op Adaptation (Nearest -> Fire, Furthest -> Poison, 3rd -> Water)", () =>
            {
                GameObject bossGo = new GameObject("BossBurstCoopTest");
                GameObject arenaGo = new GameObject("ArenaCoopTest");
                GameObject tank1Go = new GameObject("Tank1_Near");
                GameObject tank2Go = new GameObject("Tank2_Far");
                GameObject tank3Go = new GameObject("Tank3_Mid");

                try
                {
                    var brain = bossGo.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();
                    bossGo.transform.position = Vector2.zero;

                    var arena = arenaGo.AddComponent<ElementalAltarsSubArena>();
                    arena.InitializeArena(brain);

                    var tank1 = tank1Go.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank1.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                    tank1.transform.position = new Vector2(2f, 0f); // distance 2

                    var tank2 = tank2Go.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank2.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                    tank2.transform.position = new Vector2(20f, 0f); // distance 20

                    var tank3 = tank3Go.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank3.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                    tank3.transform.position = new Vector2(10f, 0f); // distance 10

                    List<TankBrain> candidateTanks = new List<TankBrain> { tank1, tank2, tank3 };
                    EnemyStats cloneStats = new EnemyStats { MaxHealth = 4500f, Attack = 300f, Defense = 27.5f, MoveSpeed = 94.5f };

                    arena.StartBurstPhase(candidateTanks, cloneStats);

                    Assert(arena.FireAltar.isActive, "Fire Altar must be active");
                    Assert(arena.PoisonAltar.isActive, "Poison Altar must be active");
                    Assert(arena.WaterAltar.isActive, "Water Altar must be active");

                    Assert(arena.FireAltar.trappedTank == tank1, "Tank 1 (nearest) must be in Fire Altar");
                    Assert(arena.PoisonAltar.trappedTank == tank2, "Tank 2 (furthest) must be in Poison Altar");
                    Assert(arena.WaterAltar.trappedTank == tank3, "Tank 3 (middle) must be in Water Altar");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(arenaGo);
                    UnityEngine.Object.DestroyImmediate(tank1Go);
                    UnityEngine.Object.DestroyImmediate(tank2Go);
                    UnityEngine.Object.DestroyImmediate(tank3Go);
                }
            });
        }

        private static void Test13_RewardPhase_SuccessResolution()
        {
            Run("13.1 Reward Phase (Shield Removed, Stun 3s, DEF -80% for 10s, Tank Buffs)", () =>
            {
                GameObject bossGo = new GameObject("BossRewardTest");
                GameObject tankGo = new GameObject("TankRewardTest");
                try
                {
                    var brain = bossGo.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    var tank = tankGo.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                    tank.TakeDamage(400f); // Current HP = 600

                    // Start burst -> Active -> Complete Success
                    brain.TriggerEnergyBurst();
                    brain.CompleteBurstSuccess();

                    Assert(brain.BurstState == EliteBurstState.RewardPhase, $"BurstState expected RewardPhase, got {brain.BurstState}");

                    // Check Stun on Boss
                    Assert(brain.HasCC(CrowdControlType.Stun), "Boss must be Stunned in RewardPhase");

                    // Check DEF reduction (-80% DEF: 25 -> 5)
                    Assert(Mathf.Abs(brain.Stats.Defense - 5f) < 0.1f, $"Boss DEF expected 5 (25 * 0.2), got {brain.Stats.Defense}");

                    // Check Tank healed +20% Max HP (600 + 200 = 800)
                    Assert(tank.CurrentHealth >= 799f, $"Tank HP expected ~800, got {tank.CurrentHealth}");

                    // Check Tank +25% ATK buff
                    Assert(Mathf.Abs(tank.Stats.Attack - 125f) < 0.1f, $"Tank ATK expected 125, got {tank.Stats.Attack}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });
        }

        private static void Test14_PenaltyPhase_FailureAndPermanent6SkillsUnlock()
        {
            Run("14.1 Penalty Phase (Explosion per Clone, Boss Heal 10%, Permanent 6 Skills Unlock)", () =>
            {
                GameObject bossGo = new GameObject("BossPenaltyTest");
                GameObject tankGo = new GameObject("TankPenaltyTest");
                try
                {
                    var brain = bossGo.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();
                    brain.TakeDamage(5000f);

                    var tank = tankGo.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                    tank.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 0f });

                    brain.TriggerEnergyBurst();
                    float preFailHp = brain.CurrentHealth;
                    brain.CompleteBurstFailure();

                    Assert(brain.BurstState == EliteBurstState.PenaltyPhase, $"BurstState expected PenaltyPhase, got {brain.BurstState}");

                    // Check Boss Heal 10% (+950 HP)
                    float expectedHp = preFailHp + (brain.MaxHealth * 0.10f);
                    Assert(Mathf.Abs(brain.CurrentHealth - expectedHp) < 1f, $"Boss HP expected {expectedHp}, got {brain.CurrentHealth}");

                    // Check Penalty damage dealt to tank (250 dmg)
                    Assert(Mathf.Abs(tank.CurrentHealth - 750f) < 1f, $"Tank HP expected 750, got {tank.CurrentHealth}");

                    // Check Permanent All 6 Skills Unlocked!
                    Assert(brain.IsPermanentAllSkillsUnlocked, "m_PermanentAllSkillsUnlocked must be true after PenaltyPhase!");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });
        }

        private static void Test15_ImmediateSkillReadinessOnBurstTransitions()
        {
            Run("15.1 Skill Timers Reset to Ready on Burst Transitions (No Delay)", () =>
            {
                GameObject bossGo = new GameObject("BossTimerTest");
                try
                {
                    var brain = bossGo.AddComponent<ElementalReaperBrain>();
                    brain.Initialize();

                    brain.TriggerEnergyBurst();
                    brain.CompleteBurstSuccess();

                    // Skill timers must be <= 0 so skills can be cast immediately
                    Assert(brain.ThuyTrieuTimer <= 0f, $"ThuyTrieuTimer expected <= 0, got {brain.ThuyTrieuTimer}");
                    Assert(brain.ThuyLieuTimer <= 0f, $"ThuyLieuTimer expected <= 0, got {brain.ThuyLieuTimer}");
                    Assert(brain.DocQuatTimer <= 0f, $"DocQuatTimer expected <= 0, got {brain.DocQuatTimer}");
                    Assert(brain.LoanChuyenTimer <= 0f, $"LoanChuyenTimer expected <= 0, got {brain.LoanChuyenTimer}");
                    Assert(brain.PhunDungNhamTimer <= 0f, $"PhunDungNhamTimer expected <= 0, got {brain.PhunDungNhamTimer}");
                    Assert(brain.HoaTocTimer <= 0f, $"HoaTocTimer expected <= 0, got {brain.HoaTocTimer}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                }
            });
        }

        #endregion
    }
}
