using System;
using System.Collections.Generic;
using System.Text;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.D1onk;
using IronWasteland.Enemies.D1onk.VFX;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    public static class D1onk_AnimationVFXTest
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/TruongQuanNgucD1ONK .prefab";
        private const string VfxFolder = "Assets/Art/Enemies/TruongQuanNgucD1ONK/VFX/Prefabs";

        [MenuItem("Tools/IronWasteland/Run D1onk Animation & VFX Tests")]
        public static string RunAllTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== TRUONG QUAN NGUC D1ONK ANIMATION & VFX TEST SUITE ===");
            int total = 0;
            int passed = 0;

            void Run(string name, Action test)
            {
                total++;
                try
                {
                    test();
                    passed++;
                    sb.AppendLine($"[PASS] {name}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"[FAIL] {name}: {ex.Message}");
                }
            }

            // 1. Prefab Root & Components Checks
            Run("1.1 D1onk Prefab Exists and Loads", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Assert(prefab != null, "TruongQuanNgucD1ONK .prefab not found at " + PrefabPath);
            });

            Run("1.2 Boss Prefab Root Components and Layer", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var rb = prefab.GetComponent<Rigidbody2D>();
                Assert(rb != null, "Rigidbody2D missing on Root");
                Assert(prefab.GetComponent<D1onkProceduralAnimator>() != null, "D1onkProceduralAnimator missing on Root");
                Assert(prefab.GetComponent<D1onkVFXController>() != null, "D1onkVFXController missing on Root");
                Assert(prefab.layer == 7, $"Root layer must be 7 (Enemy), got {prefab.layer}");
                var model = prefab.transform.Find("EnemyModel");
                Assert(model != null && model.GetComponent<ColliderOwnerRef>() != null, "ColliderOwnerRef missing on EnemyModel");
            });

            Run("1.3 User Constraint: ColliderRef at VFX Prefabs MUST NOT have D1onk Boss as Owner", () =>
            {
                string[] vfxPrefabs = {
                    VfxFolder + "/VFX_D1onkMissileProjectile.prefab",
                    VfxFolder + "/VFX_D1onkTrungPhatLightning.prefab",
                    VfxFolder + "/VFX_D1onkLazeNguyenTo.prefab",
                    VfxFolder + "/VFX_D1onkToaNhietDienRong.prefab",
                    VfxFolder + "/VFX_D1onkVienBinhSummon.prefab"
                };

                foreach (var path in vfxPrefabs)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Assert(go != null, "Missing prefab: " + path);
                    var colRef = go.GetComponent<ColliderOwnerRef>();
                    Assert(colRef == null, $"User constraint violation: {go.name} MUST NOT have ColliderOwnerRef!");
                }
            });

            // 2. Body Hierarchy & 14 Guns Configuration Checks
            Run("2.1 Body Hierarchy References Wired in D1onkProceduralAnimator", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var anim = prefab.GetComponent<D1onkProceduralAnimator>();
                Assert(anim != null, "D1onkProceduralAnimator component missing");
                Assert(anim.Than != null, "'Than' reference is null");
                Assert(anim.Dau != null, "'Dau' reference is null");
                Assert(anim.VaiTrai != null, "'VaiTrai' reference is null");
                Assert(anim.VaiPhai != null, "'VaiPhai' reference is null");
                Assert(anim.VaiMatTrongTrai != null, "'VaiMatTrongTrai' reference is null");
                Assert(anim.VaiMatTrongPhai != null, "'VaiMatTrongPhai' reference is null");
                Assert(anim.TayTrai != null, "'TayTrai' reference is null");
                Assert(anim.TayPhai != null, "'TayPhai' reference is null");
                Assert(anim.ChienHam != null, "'ChienHam' reference is null");
                Assert(anim.Sung2NongParent != null, "'Sung2NongParent' reference is null");
                Assert(anim.Sung3NongParent != null, "'Sung3NongParent' reference is null");
                Assert(anim.AllGuns != null && anim.AllGuns.Length == 14, $"Expected 14 guns, got {anim.AllGuns?.Length ?? 0}");
            });

            Run("2.2 14 Guns Outward Firing Vectors (-gunTransform.right)", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var anim = prefab.GetComponent<D1onkProceduralAnimator>();
                Assert(anim.AllGuns.Length == 14, "Expected 14 guns");

                for (int i = 0; i < 14; i++)
                {
                    var gun = anim.AllGuns[i];
                    Assert(gun != null, $"Gun[{i}] is null");
                    Vector2 fireDir = (Vector2)(-gun.right);
                    Assert(fireDir.sqrMagnitude > 0.99f && fireDir.sqrMagnitude < 1.01f, $"Gun[{i}] fire vector magnitude must be 1.0");
                }
            });

            Run("2.3 D1onkVFXController Slot Wiring and 12 Normal Enemies Pool", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var ctrl = prefab.GetComponent<D1onkVFXController>();
                Assert(ctrl != null, "D1onkVFXController missing");
                Assert(ctrl.ProceduralAnimator != null, "proceduralAnimator not wired");
                Assert(ctrl.MissileProjectilePrefab != null, "missileProjectilePrefab not wired");
                Assert(ctrl.MuzzleFlashPrefab != null, "muzzleFlashPrefab not wired");
                Assert(ctrl.ExplosionVFXPrefab != null, "explosionVFXPrefab not wired");
                Assert(ctrl.TrungPhatLightningPrefab != null, "trungPhatLightningPrefab not wired");
                Assert(ctrl.LazeNguyenToPrefab != null, "lazeNguyenToPrefab not wired");
                Assert(ctrl.ToaNhietDienRongPrefab != null, "toaNhietDienRongPrefab not wired");
                Assert(ctrl.VienBinhSummonVFXPrefab != null, "vienBinhSummonVFXPrefab not wired");
                Assert(ctrl.ChargingAuraPrefab != null, "chargingAuraPrefab not wired");
                Assert(ctrl.StunVFXPrefab != null, "stunVFXPrefab not wired");
                Assert(ctrl.GunBarrels != null && ctrl.GunBarrels.Length == 14, "gunBarrels must have 14 entries");
                Assert(ctrl.NormalEnemyPrefabs != null && ctrl.NormalEnemyPrefabs.Length == 12, $"normalEnemyPrefabs expected 12, got {ctrl.NormalEnemyPrefabs?.Length ?? 0}");
            });

            // 3. Procedural Rotation & Invariant: Preservation of LocalPosition Across All States
            Run("3.1 Strict Invariant: LocalPosition Preserved for All Parts Across All 9 States", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var anim = instance.GetComponent<D1onkProceduralAnimator>();
                    anim.InitializeHierarchy();

                    D1onkAnimState[] states = {
                        D1onkAnimState.Idle,
                        D1onkAnimState.Move,
                        D1onkAnimState.PhaoHamDaNong,
                        D1onkAnimState.TrungPhat,
                        D1onkAnimState.LazeNguyenTo,
                        D1onkAnimState.ToaNhietDienRong,
                        D1onkAnimState.VienBinhTinhLinh,
                        D1onkAnimState.Charging,
                        D1onkAnimState.Stun,
                        D1onkAnimState.Die
                    };

                    foreach (var state in states)
                    {
                        anim.PlayAnimation(state, 2.0f);

                        // Advance animation across multiple timesteps
                        for (float t = 0f; t <= 1.5f; t += 0.25f)
                        {
                            anim.ManualTick(0.25f);
                            bool preserved = anim.VerifyPositionsPreserved();
                            Assert(preserved, $"Invariant violation: localPosition changed during state {state} at t={t:F2}s!");
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("3.2 Procedural Rotation (localRotation.z) Dynamically Modifies Angles", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var anim = instance.GetComponent<D1onkProceduralAnimator>();
                    anim.InitializeHierarchy();

                    // Check Idle rotation delta on arms
                    anim.SetState(D1onkAnimState.Idle);
                    anim.ManualTick(0.5f);
                    float idleArmZ = anim.GetCurrentDeltaZ(anim.TayTrai);
                    Assert(Mathf.Abs(idleArmZ) > 0.05f, "Idle should produce non-zero delta Z on TayTrai");

                    // Check TrungPhat strike rotation delta
                    anim.PlayAnimation(D1onkAnimState.TrungPhat, 1.5f);
                    anim.ManualTick(0.3f); // Phase 1: windup / raised arms (t = 0.3s, u = 0.2)
                    float windupZ = anim.GetCurrentDeltaZ(anim.TayTrai);
                    Assert(windupZ < -10f, $"TrungPhat windup expected TayTrai deltaZ < -10 deg, got {windupZ:F1}");

                    anim.ManualTick(0.6f); // Phase 2: slam down (t = 0.9s, u = 0.6)
                    float slamZ = anim.GetCurrentDeltaZ(anim.TayTrai);
                    Assert(slamZ > 5f, $"TrungPhat slam expected TayTrai deltaZ > 5 deg, got {slamZ:F1}");

                    // Check ToaNhietDienRong wide arms rotation
                    anim.PlayAnimation(D1onkAnimState.ToaNhietDienRong, 1.5f);
                    anim.ManualTick(0.2f);
                    float heatLeftZ = anim.GetCurrentDeltaZ(anim.TayTrai);
                    float heatRightZ = anim.GetCurrentDeltaZ(anim.TayPhai);
                    Assert(heatLeftZ < -25f, $"ToaNhietDienRong expected TayTrai spread < -25 deg, got {heatLeftZ:F1}");
                    Assert(heatRightZ > 25f, $"ToaNhietDienRong expected TayPhai spread > 25 deg, got {heatRightZ:F1}");

                    // Check pure Z rotation (X and Y must be 0)
                    Assert(Mathf.Approximately(anim.Than.localEulerAngles.x, 0f), "localEulerAngles.x must be 0");
                    Assert(Mathf.Approximately(anim.Than.localEulerAngles.y, 0f), "localEulerAngles.y must be 0");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("3.3 Smooth Ease-Out & No Angle Pop at Animation Completion", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var anim = instance.GetComponent<D1onkProceduralAnimator>();
                    anim.InitializeHierarchy();

                    // Play ToaNhietDienRong (1.5s) and verify that right before finish (t = 1.48s),
                    // the envelope eases angle close to 0 smoothly without a massive snap
                    anim.PlayAnimation(D1onkAnimState.ToaNhietDienRong, 1.5f);
                    anim.ManualTick(0.5f); // Peak
                    float peakZ = Mathf.Abs(anim.GetCurrentDeltaZ(anim.TayTrai));
                    Assert(peakZ > 30f, "Should reach peak spread");

                    anim.ManualTick(0.98f); // t = 1.48s (near end)
                    float nearEndZ = Mathf.Abs(anim.GetCurrentDeltaZ(anim.TayTrai));
                    Assert(nearEndZ < 10f, $"Angles must ease smoothly towards 0 before completion, got {nearEndZ:F1} deg");

                    anim.ManualTick(0.05f); // t = 1.53s (transition to Idle completed)
                    Assert(anim.CurrentState == D1onkAnimState.Idle, "State should reset to Idle");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            // 4. Passive: Multi-Barrel Salvo & Missile Projectile Simulation
            Run("4.1 Passive Multi-Barrel Salvo Spawns 14 Missiles Along -gun.right", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var missiles = ctrl.FireMultiBarrelSalvo();
                    Assert(missiles.Count == 14, $"Expected 14 missiles, got {missiles.Count}");

                    for (int i = 0; i < 14; i++)
                    {
                        var m = missiles[i];
                        Assert(m != null, $"Missile[{i}] is null");
                        Vector2 expectedDir = (Vector2)(-ctrl.GunBarrels[i].right);
                        float angleDiff = Vector2.Angle(m.FlyDirection, expectedDir);
                        Assert(angleDiff < 1.0f, $"Missile[{i}] flight direction mismatch (diff={angleDiff})");

                        // Simulate flight step
                        Vector3 posBefore = m.transform.position;
                        m.ManualTick(0.2f);
                        Vector3 posAfter = m.transform.position;
                        Assert(Vector3.Distance(posBefore, posAfter) > 0.5f, $"Missile[{i}] should advance in space");
                        Assert(m.CurrentSpeed > 12f, $"Missile[{i}] should accelerate");

                        UnityEngine.Object.DestroyImmediate(m.gameObject);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("4.2 Passive FireRandomBarrels Spawns Exactly N Missiles and Recoil Animation", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var anim = instance.GetComponent<D1onkProceduralAnimator>();

                    // Test 5 missiles (GDD on skill cast)
                    var missiles5 = ctrl.FireRandomBarrels(5);
                    Assert(missiles5.Count == 5, $"Expected 5 missiles, got {missiles5.Count}");
                    Assert(anim.CurrentState == D1onkAnimState.PhaoHamDaNong, "Animator must be in PhaoHamDaNong recoil state");
                    foreach (var m in missiles5) UnityEngine.Object.DestroyImmediate(m.gameObject);

                    // Test 2 missiles (GDD periodic 5s)
                    var missiles2 = ctrl.FireRandomBarrels(2);
                    Assert(missiles2.Count == 2, $"Expected 2 missiles, got {missiles2.Count}");
                    foreach (var m in missiles2) UnityEngine.Object.DestroyImmediate(m.gameObject);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("4.3 Missile Projectile Kinematic Rigidbody2D Verification", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_D1onkMissileProjectile.prefab");
                Assert(prefab != null, "Missile prefab missing");
                var rb = prefab.GetComponent<Rigidbody2D>();
                Assert(rb != null, "Rigidbody2D missing on missile prefab");
                Assert(rb.bodyType == RigidbodyType2D.Kinematic, "Missile Rigidbody2D must be Kinematic");
                var col = prefab.GetComponent<CircleCollider2D>();
                Assert(col != null && col.isTrigger, "CircleCollider2D isTrigger must be true");
            });

            // 5. Active Skills VFX Invocations
            Run("5.1 TrungPhat Lightning Strike VFX Invocation and Lifecycle", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    Vector3 target = new Vector3(5f, 2f, 0f);
                    var vfx = ctrl.PlayTrungPhat(target);
                    Assert(vfx != null, "PlayTrungPhat returned null");
                    Assert(vfx.transform.position == target, "VFX position mismatch");

                    // Simulate warning phase
                    vfx.ManualTick(0.3f);
                    Assert(!vfx.HasStruck, "Should still be in warning phase at 0.3s");

                    // Simulate strike
                    vfx.ManualTick(0.4f);
                    Assert(vfx.HasStruck, "Should have struck at 0.7s");

                    // Simulate completion
                    bool completed = false;
                    var vfx2 = ctrl.PlayTrungPhat(target, onComplete: () => completed = true);
                    vfx2.ManualTick(1.5f);
                    Assert(completed, "onComplete should be invoked after full strike duration finishes");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.2 LazeNguyenTo Laser Beam VFX Invocation and Sweep", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var vfx = ctrl.PlayLazeNguyenTo(Vector3.right, duration: 2.0f);
                    Assert(vfx != null, "PlayLazeNguyenTo returned null");
                    Assert(vfx.IsActive, "Laser should be active");
                    Assert(vfx.Direction == Vector2.right, "Laser direction mismatch");

                    vfx.ManualTick(1.0f);
                    Assert(vfx.IsActive, "Laser should still be active at 1.0s");

                    vfx.ManualTick(1.1f);
                    Assert(!vfx.IsActive, "Laser should stop after duration");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.3 ToaNhietDienRong Thermal Shockwave VFX Expansion", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var vfx = ctrl.PlayToaNhietDienRong();
                    Assert(vfx != null, "PlayToaNhietDienRong returned null");
                    Assert(vfx.IsActive, "Shockwave should be active");

                    float r0 = vfx.CurrentRadius;
                    vfx.ManualTick(0.5f);
                    float r1 = vfx.CurrentRadius;
                    Assert(r1 > r0, "Thermal shockwave radius must expand over time");

                    vfx.ManualTick(0.6f);
                    Assert(!vfx.IsActive, "Shockwave should complete after expansionDuration");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.4 VienBinhTinhLinh Summons 3-5 Normal Enemies from 12 Pool", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var summonList = ctrl.SummonReinforcements();
                    Assert(summonList.Count >= 3 && summonList.Count <= 5, $"Expected 3 to 5 summons, got {summonList.Count}");

                    foreach (var s in summonList)
                    {
                        Assert(s != null, "Summon VFX is null");
                        Assert(!s.HasSpawned, "Should not have spawned enemy initially");

                        // Advance summon ritual
                        s.ManualTick(0.9f);
                        Assert(s.HasSpawned, "Enemy should have spawned after summonDuration");
                        Assert(s.SpawnedEnemy != null, "SpawnedEnemy is null");

                        // Clean up spawned enemy
                        UnityEngine.Object.DestroyImmediate(s.SpawnedEnemy);
                        UnityEngine.Object.DestroyImmediate(s.gameObject);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.5 Charging Aura and Stun VFX States Activation & Cleanup", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var anim = instance.GetComponent<D1onkProceduralAnimator>();

                    // Test Charging
                    var aura = ctrl.StartCharging();
                    Assert(aura != null && aura.IsActive, "Charging aura should be active");
                    Assert(anim.CurrentState == D1onkAnimState.Charging, "Animator state must be Charging");
                    ctrl.StopCharging();
                    Assert(anim.CurrentState == D1onkAnimState.Idle, "Animator state should return to Idle");

                    // Test Stun
                    var stun = ctrl.PlayStun(duration: 3.0f);
                    Assert(stun != null && stun.IsActive, "Stun VFX should be active");
                    Assert(anim.CurrentState == D1onkAnimState.Stun, "Animator state must be Stun");
                    ctrl.StopStun();
                    Assert(anim.CurrentState == D1onkAnimState.Idle, "Animator state should return to Idle");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.6 Single Invocation of onComplete Callback for Stun", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    int callCount = 0;
                    var stun = ctrl.PlayStun(1.0f, onComplete: () => callCount++);
                    Assert(stun != null, "Stun VFX is null");

                    stun.ManualTick(1.2f);
                    Assert(callCount == 1, $"onComplete must be called exactly once, called {callCount} times");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.7 LazeNguyenTo Laser Obstacle Occlusion / Wall Stopping", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                GameObject wall = null;
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    wall = new GameObject("TestWall");
                    wall.transform.position = instance.transform.position + new Vector3(6f, 0f, 0f);
                    var wallCol = wall.AddComponent<BoxCollider2D>();
                    wallCol.size = new Vector2(1f, 10f);

                    var laser = ctrl.PlayLazeNguyenTo(Vector3.right, duration: 2.0f);
                    laser.ManualTick(0.1f);

                    Assert(laser.CurrentBeamLength < 8.0f, $"Laser beam must stop at obstacle distance (~6m), current length={laser.CurrentBeamLength:F2}");
                    laser.StopLaser();
                }
                finally
                {
                    if (wall != null) UnityEngine.Object.DestroyImmediate(wall);
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            Run("5.8 Reinforcements Summon Enemy Spawned Callback & Instances Validation", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<D1onkVFXController>();
                    var spawnedEnemies = new List<GameObject>();
                    var summonList = ctrl.SummonReinforcements(3, onEnemySpawned: enemy => spawnedEnemies.Add(enemy));

                    foreach (var s in summonList)
                    {
                        s.ManualTick(1.0f); // Complete summon
                    }

                    Assert(spawnedEnemies.Count == 3, $"Expected 3 spawned enemies via callback, got {spawnedEnemies.Count}");
                    foreach (var e in spawnedEnemies)
                    {
                        Assert(e != null, "Spawned enemy is null");
                        UnityEngine.Object.DestroyImmediate(e);
                    }
                    foreach (var s in summonList)
                    {
                        if (s != null) UnityEngine.Object.DestroyImmediate(s.gameObject);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            sb.AppendLine($"\nRESULT: {passed}/{total} tests passed ({(passed == total ? "100% SUCCESS" : "FAILURES DETECTED")}).");
            return sb.ToString();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }
    }
}
