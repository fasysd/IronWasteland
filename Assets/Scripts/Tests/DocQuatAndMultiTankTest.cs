using System;
using System.Collections.Generic;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.ElementalReaper;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Tanks;
using UnityEditor;
using UnityEngine;

namespace IronWasteland.Tests
{
    public static class DocQuatAndMultiTankTest
    {
        public static string RunTests()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== DOC QUAT ROTATION & MULTI-TANK TEST SUITE ===");
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

            // Test 1: DocQuat rotation on PlayDocQuat
            Run("1.1 DocQuat Rotation Matches Target Direction", () =>
            {
                GameObject bossGo = new GameObject("TestBoss");
                var vfxCtrl = bossGo.AddComponent<ElementalReaperVFXController>();
                var hitboxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Enemies/TuThanNguyenTo/VFX/Prefabs/VFX_DocQuatHitbox.prefab");
                
                // Gán prefab vào VFXController qua SerializedObject
                var so = new SerializedObject(vfxCtrl);
                so.FindProperty("docQuatHitboxPrefab").objectReferenceValue = hitboxPrefab;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Test angle 90° (Up)
                Vector3 dirUp = Vector3.up;
                var hitboxUp = vfxCtrl.PlayDocQuat(dirUp);
                if (hitboxUp == null) throw new Exception("PlayDocQuat returned null!");
                float angleUp = hitboxUp.transform.eulerAngles.z;
                if (Mathf.Abs(Mathf.DeltaAngle(angleUp, 90f)) > 1f)
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(hitboxUp.gameObject);
                    throw new Exception($"Expected angle 90°, got {angleUp}");
                }
                UnityEngine.Object.DestroyImmediate(hitboxUp.gameObject);

                // Test angle 180° (Left)
                Vector3 dirLeft = Vector3.left;
                var hitboxLeft = vfxCtrl.PlayDocQuat(dirLeft);
                float angleLeft = hitboxLeft.transform.eulerAngles.z;
                if (Mathf.Abs(Mathf.DeltaAngle(angleLeft, 180f)) > 1f)
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(hitboxLeft.gameObject);
                    throw new Exception($"Expected angle 180°, got {angleLeft}");
                }
                UnityEngine.Object.DestroyImmediate(hitboxLeft.gameObject);

                // Test angle -45° (Down-Right)
                Vector3 dirDiag = new Vector3(1f, -1f, 0f).normalized;
                var hitboxDiag = vfxCtrl.PlayDocQuat(dirDiag);
                float angleDiag = hitboxDiag.transform.eulerAngles.z;
                if (Mathf.Abs(Mathf.DeltaAngle(angleDiag, -45f)) > 1f)
                {
                    UnityEngine.Object.DestroyImmediate(bossGo);
                    UnityEngine.Object.DestroyImmediate(hitboxDiag.gameObject);
                    throw new Exception($"Expected angle -45°, got {angleDiag}");
                }
                UnityEngine.Object.DestroyImmediate(hitboxDiag.gameObject);
                UnityEngine.Object.DestroyImmediate(bossGo);
            });

            // Test 2: GameController multi-tank mechanism
            Run("2.1 GameController Multi-Tank Configuration & Switch", () =>
            {
                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankDef = AssetDatabase.LoadAssetAtPath<TankDefinition>("Assets/Data/Tanks/TankRedNova.asset");
                if (tankDef == null)
                {
                    // Fallback search
                    string[] guids = AssetDatabase.FindAssets("t:TankDefinition");
                    if (guids.Length > 0)
                    {
                        tankDef = AssetDatabase.LoadAssetAtPath<TankDefinition>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
                if (tankDef == null) throw new Exception("Cannot find any TankDefinition asset");

                GameObject gcGo = new GameObject("TestGameController");
                var gc = gcGo.AddComponent<GameController>();

                // Configure 3 tanks
                var so = new SerializedObject(gc);
                var configsProp = so.FindProperty("tankConfigs");
                configsProp.arraySize = 3;

                // Tank 0 at (0, 0)
                var elem0 = configsProp.GetArrayElementAtIndex(0);
                elem0.FindPropertyRelative("tankName").stringValue = "Tank Alpha";
                elem0.FindPropertyRelative("tankDefinition").objectReferenceValue = tankDef;
                elem0.FindPropertyRelative("playerLevel").intValue = 10;
                elem0.FindPropertyRelative("spawnPosition").vector2Value = new Vector2(0f, 0f);

                // Tank 1 at (10, 5)
                var elem1 = configsProp.GetArrayElementAtIndex(1);
                elem1.FindPropertyRelative("tankName").stringValue = "Tank Beta";
                elem1.FindPropertyRelative("tankDefinition").objectReferenceValue = tankDef;
                elem1.FindPropertyRelative("playerLevel").intValue = 20;
                elem1.FindPropertyRelative("spawnPosition").vector2Value = new Vector2(10f, 5f);

                // Tank 2 at (-5, 8)
                var elem2 = configsProp.GetArrayElementAtIndex(2);
                elem2.FindPropertyRelative("tankName").stringValue = "Tank Gamma";
                elem2.FindPropertyRelative("tankDefinition").objectReferenceValue = tankDef;
                elem2.FindPropertyRelative("playerLevel").intValue = 30;
                elem2.FindPropertyRelative("spawnPosition").vector2Value = new Vector2(-5f, 8f);

                so.ApplyModifiedPropertiesWithoutUndo();

                // Setup Camera
                GameObject camGo = new GameObject("TestCamera");
                var cam = camGo.AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 0, -10f);

                var camProp = so.FindProperty("targetCamera");
                camProp.objectReferenceValue = cam;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Spawn tanks
                gc.SpawnAllTanks();
                if (gc.SpawnedTanks.Count != 3)
                {
                    throw new Exception($"Expected 3 spawned tanks, got {gc.SpawnedTanks.Count}");
                }

                // Check initial controlled tank (Index 0)
                gc.SwitchControlledTank(0, snapCamera: true);
                if (gc.ControlledTank != gc.SpawnedTanks[0])
                {
                    throw new Exception("ControlledTank does not match Tank 0");
                }
                if (gc.PlayerTank != gc.SpawnedTanks[0])
                {
                    throw new Exception("PlayerTank backward compatibility does not match Tank 0");
                }
                if (Mathf.Abs(cam.transform.position.x - 0f) > 0.01f || Mathf.Abs(cam.transform.position.y - 0f) > 0.01f)
                {
                    throw new Exception($"Camera did not snap to Tank 0: {cam.transform.position}");
                }

                // Switch to Tank 1
                gc.SwitchControlledTank(1, snapCamera: true);
                if (gc.ControlledTank != gc.SpawnedTanks[1])
                {
                    throw new Exception("ControlledTank does not match Tank 1");
                }
                if (Mathf.Abs(cam.transform.position.x - 10f) > 0.01f || Mathf.Abs(cam.transform.position.y - 5f) > 0.01f)
                {
                    throw new Exception($"Camera did not snap to Tank 1: {cam.transform.position}");
                }

                // Switch to Tank 2
                gc.SwitchControlledTank(2, snapCamera: true);
                if (gc.ControlledTank != gc.SpawnedTanks[2])
                {
                    throw new Exception("ControlledTank does not match Tank 2");
                }
                if (Mathf.Abs(cam.transform.position.x - (-5f)) > 0.01f || Mathf.Abs(cam.transform.position.y - 8f) > 0.01f)
                {
                    throw new Exception($"Camera did not snap to Tank 2: {cam.transform.position}");
                }

                // Cleanup
                foreach (var t in gc.SpawnedTanks)
                {
                    if (t != null) UnityEngine.Object.DestroyImmediate(t.gameObject);
                }
                UnityEngine.Object.DestroyImmediate(gcGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            });

            // Test 3: Random crystal generation on all 3 altars and outside arena
            Run("3.1 Crystals Spawning Across All Altars And Outside Arena Without Elemental Restriction", () =>
            {
                var crystalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ElementalCrystal.prefab");
                var clonePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ElementalReaperClone.prefab");

                GameObject arenaGo = new GameObject("TestSubArena");
                var arena = arenaGo.AddComponent<ElementalAltarsSubArena>();
                arena.CrystalPrefab = crystalPrefab;
                arena.ClonePrefab = clonePrefab;

                GameObject bossGo = new GameObject("TestBossForArena");
                bossGo.transform.position = new Vector2(0f, 0f);
                var bossBrain = bossGo.AddComponent<ElementalReaperBrain>();
                arena.InitializeArena(bossBrain);

                GameObject tankGo1 = new GameObject("TestTank1");
                var tank1 = tankGo1.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                tank1.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                tank1.transform.position = new Vector2(2f, 0f);

                GameObject tankGo2 = new GameObject("TestTank2");
                var tank2 = tankGo2.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                tank2.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                tank2.transform.position = new Vector2(20f, 0f);

                GameObject tankGo3 = new GameObject("TestTank3");
                var tank3 = tankGo3.AddComponent<IronWasteland.Tanks.Test.TankBrain_Test>();
                tank3.Initialize(new TankBrainContext(), new TankStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f });
                tank3.transform.position = new Vector2(10f, 0f);

                // Khởi động burst với 3 tank để mở cả 3 tế đàn
                var tanks = new List<TankBrain> { tank1, tank2, tank3 };
                arena.StartBurstPhase(tanks, new EnemyStats { MaxHealth = 1000f, Attack = 100f, Defense = 10f, MoveSpeed = 90f });

                // Kiểm tra các tinh thể được tạo ra
                var crystals = UnityEngine.Object.FindObjectsByType<ElementalCrystal>(FindObjectsSortMode.None);
                if (crystals.Length == 0) throw new Exception("No crystals spawned during StartBurstPhase!");

                bool foundInsideAltar = false;
                bool foundOutsideMainArena = false;
                HashSet<ElementalType> spawnedElements = new HashSet<ElementalType>();
                HashSet<CrystalShape> spawnedShapes = new HashSet<CrystalShape>();

                foreach (var c in crystals)
                {
                    spawnedElements.Add(c.Element);
                    spawnedShapes.Add(c.Shape);

                    Vector2 pos = c.transform.position;
                    // Kiểm tra nằm gần Boss (sân chính, bán kính < 15m)
                    if (pos.magnitude < 15f)
                    {
                        foundOutsideMainArena = true;
                    }
                    // Kiểm tra nằm ở 1 trong các tế đàn (x > 100)
                    if (pos.x > 100f)
                    {
                        foundInsideAltar = true;
                    }
                }

                if (!foundInsideAltar) throw new Exception("No crystals spawned inside altars!");
                if (!foundOutsideMainArena) throw new Exception("No crystals spawned outside arena (around Boss)!");

                arena.CleanupArena();
                UnityEngine.Object.DestroyImmediate(arenaGo);
                UnityEngine.Object.DestroyImmediate(bossGo);
                UnityEngine.Object.DestroyImmediate(tankGo1);
                UnityEngine.Object.DestroyImmediate(tankGo2);
                UnityEngine.Object.DestroyImmediate(tankGo3);
            });

            sb.AppendLine($"\nRESULT: {passed}/{total} tests passed ({(passed == total ? "100% SUCCESS" : "FAILED")}).");
            return sb.ToString();
        }
    }
}
