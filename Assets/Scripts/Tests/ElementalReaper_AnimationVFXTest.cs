using System;
using System.Collections.Generic;
using System.Text;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.DeadlandPhantom;
using IronWasteland.Enemies.ElementalReaper;
using IronWasteland.Enemies.ElementalReaper.Hitboxes;
using IronWasteland.Enemies.ElementalReaper.VFX;
using IronWasteland.Tanks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace IronWasteland.Tests
{
    public static class ElementalReaper_AnimationVFXTest
    {
        private const string PrefabPath = "Assets/Prefabs/Enemies/TuThanNguyenTo.prefab";
        private const string AnimFolder = "Assets/Art/Enemies/TuThanNguyenTo/Animations";
        private const string VfxFolder = "Assets/Art/Enemies/TuThanNguyenTo/VFX/Prefabs";

        public static string RunAllTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ELEMENTAL REAPER ANIMATION & VFX TEST SUITE ===");
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

            // 1. Prefab Root Checks
            Run("1.1 Boss Prefab Exists and Loads", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                Assert(prefab != null, "TuThanNguyenTo.prefab not found at " + PrefabPath);
            });

            Run("1.2 Boss Prefab Root Components", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var rb = prefab.GetComponent<Rigidbody2D>();
                Assert(rb != null, "Rigidbody2D missing on Root");
                Assert(rb.bodyType == RigidbodyType2D.Kinematic, "Rigidbody2D must be Kinematic");
                Assert(prefab.GetComponent<ElementalReaperVFXController>() != null, "ElementalReaperVFXController missing on Root");
                Assert(prefab.layer == 7, "Root layer must be 7 (Enemy)");
                var model = prefab.transform.Find("EnemyModel");
                Assert(model != null && model.GetComponent<ColliderOwnerRef>() != null, "ColliderOwnerRef missing on EnemyModel");
            });

            Run("1.3 HoaTocDashHitbox on TuThanNguyenTo Prefab", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var dashObj = prefab.transform.Find("HoaTocDashHitbox");
                Assert(dashObj != null, "Child 'HoaTocDashHitbox' missing on TuThanNguyenTo prefab");
                var col = dashObj.GetComponent<CircleCollider2D>();
                Assert(col != null && col.isTrigger, "CircleCollider2D (trigger) missing on HoaTocDashHitbox");
                // User constraint: ColliderOwnerRef must NOT be on DashHitbox to prevent false damage targeting
                Assert(dashObj.GetComponent<ColliderOwnerRef>() == null, "User constraint: ColliderOwnerRef must NOT be on HoaTocDashHitbox");
                Assert(dashObj.GetComponent<HoaTocDashHitbox>() != null, "HoaTocDashHitbox component missing");
            });

            // 2. Bone Hierarchy & SpriteSkin Checks
            Run("2.1 EnemyModel and SpriteSkin Structure", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var modelTransform = prefab.transform.Find("EnemyModel");
                Assert(modelTransform != null, "Child 'EnemyModel' not found under Root");

                var modelObj = modelTransform.gameObject;
                var sr = modelObj.GetComponent<SpriteRenderer>();
                Assert(sr != null && sr.sprite != null, "SpriteRenderer or sprite missing on EnemyModel");

                var skin = modelObj.GetComponent<SpriteSkin>();
                Assert(skin != null, "SpriteSkin missing on EnemyModel");
                Assert(skin.rootBone != null, "SpriteSkin rootBone is null");
                Assert(skin.rootBone.name == "ThanTren", $"Root bone must be 'ThanTren' but was '{skin.rootBone.name}'");

                var bones = skin.boneTransforms;
                Assert(bones != null, "boneTransforms array is null");
                Assert(bones.Length == 27, $"Expected 27 bones, but got {bones.Length}");
            });

            Run("2.2 Complete 27-Bone Hierarchy Structure", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var modelTransform = prefab.transform.Find("EnemyModel");
                var thanTren = modelTransform.Find("ThanTren");
                Assert(thanTren != null, "'ThanTren' not found under EnemyModel");

                var thanDuoi = thanTren.Find("ThanDuoi");
                Assert(thanDuoi != null, "'ThanDuoi' not found under ThanTren");

                // Check 7 tail bones
                Transform currentTail = thanDuoi;
                for (int i = 1; i <= 7; i++)
                {
                    var tail = currentTail.Find("Duoi" + i);
                    Assert(tail != null, $"Duoi{i} not found under {(currentTail == thanDuoi ? "ThanDuoi" : currentTail.name)}");
                    currentTail = tail;
                }

                // Check middle and hind legs under ThanDuoi
                Assert(thanDuoi.Find("ChanTrai2_1/ChanTrai2_2") != null, "ChanTrai2 hierarchy missing under ThanDuoi");
                Assert(thanDuoi.Find("ChanPhai2_1/ChanPhai2_2") != null, "ChanPhai2 hierarchy missing under ThanDuoi");
                Assert(thanDuoi.Find("ChanTrai3_1/ChanTrai3_2") != null, "ChanTrai3 hierarchy missing under ThanDuoi");
                Assert(thanDuoi.Find("ChanPhai3_1/ChanPhai3_2") != null, "ChanPhai3 hierarchy missing under ThanDuoi");

                // Check pincers under ThanTren
                Assert(thanTren.Find("CangTrai1/CangTrai2/CangTrai3") != null, "CangTrai 3-joint hierarchy missing under ThanTren");
                Assert(thanTren.Find("CangPhai1/CangPhai2/CangPhai3") != null, "CangPhai 3-joint hierarchy missing under ThanTren");

                // Check front legs under ThanTren
                Assert(thanTren.Find("ChanTrai1_1/ChanTrai1_2") != null, "ChanTrai1 hierarchy missing under ThanTren");
                Assert(thanTren.Find("ChanPhai1_1/ChanPhai1_2") != null, "ChanPhai1 hierarchy missing under ThanTren");
            });

            Run("2.3 EnemySensorArea Setup", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var sensor = prefab.transform.Find("EnemySensorArea");
                Assert(sensor != null, "Child 'EnemySensorArea' missing");
                var col = sensor.GetComponent<CircleCollider2D>();
                Assert(col != null && col.isTrigger, "CircleCollider2D (trigger) missing on EnemySensorArea");
                Assert(Mathf.Approximately(col.radius, 20f), $"Sensor radius expected 20m, got {col.radius}");
                Assert(sensor.GetComponent<ColliderOwnerRef>() != null, "ColliderOwnerRef missing on EnemySensorArea");
                Assert(sensor.GetComponent<EnemySensorArea>() != null, "EnemySensorArea component missing");
            });

            Run("2.4 EnemySensorArea GetFurthestTarget Query", () =>
            {
                var go = new GameObject("TestSensor");
                var sensor = go.AddComponent<EnemySensorArea>();

                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankNearGo = UnityEngine.Object.Instantiate(tankPrefab, new Vector3(3f, 0f, 0f), Quaternion.identity);
                var tankFarGo = UnityEngine.Object.Instantiate(tankPrefab, new Vector3(15f, 0f, 0f), Quaternion.identity);
                var tankNear = tankNearGo.GetComponent<TankBrain>();
                var tankFar = tankFarGo.GetComponent<TankBrain>();
                tankNear.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f });
                tankFar.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f });

                sensor.RegisterTank(tankNear);
                sensor.RegisterTank(tankFar);

                try
                {
                    var nearest = sensor.GetNearestTarget(Vector2.zero);
                    var furthest = sensor.GetFurthestTarget(Vector2.zero);
                    Assert(nearest == tankNear, $"Nearest expected tankNear, got {nearest?.name}");
                    Assert(furthest == tankFar, $"Furthest expected tankFar, got {furthest?.name}");

                    // Test with exclusion
                    var furthestExcludingFar = sensor.GetFurthestTarget(Vector2.zero, excludeTank: tankFar);
                    Assert(furthestExcludingFar == tankNear, "Furthest excluding tankFar should be tankNear");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tankNearGo);
                    UnityEngine.Object.DestroyImmediate(tankFarGo);
                    UnityEngine.Object.DestroyImmediate(go);
                }
            });

            // 3. Animator & Animation Clips Checks
            string[] expectedClips = {
                "TuThanNguyenTo_Idle.anim",
                "TuThanNguyenTo_Move.anim",
                "TuThanNguyenTo_ThuyTrieu.anim",
                "TuThanNguyenTo_ThuyLieu.anim",
                "TuThanNguyenTo_DocQuat.anim",
                "TuThanNguyenTo_LoanChuyen.anim",
                "TuThanNguyenTo_PhunDungNham.anim",
                "TuThanNguyenTo_HoaToc_Charge.anim",
                "TuThanNguyenTo_HoaToc_Dash.anim",
                "TuThanNguyenTo_BurstCharging.anim",
                "TuThanNguyenTo_Stun.anim",
                "TuThanNguyenTo_Die.anim"
            };

            Run("3.1 All 12 Animation Clips Exist, Have Valid Curves and Matching Names", () =>
            {
                foreach (var clipName in expectedClips)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/" + clipName);
                    Assert(clip != null, "Missing clip: " + clipName);
                    string expectedObjName = System.IO.Path.GetFileNameWithoutExtension(clipName);
                    Assert(clip.name == expectedObjName, $"Clip object name expected '{expectedObjName}' but was '{clip.name}'");
                    var bindings = AnimationUtility.GetCurveBindings(clip);
                    Assert(bindings.Length > 0, $"Clip {clipName} has 0 curve bindings!");
                }

                // Verify specific GDD duration specs for BurstCharging (2.0s) and Stun (3.0s)
                var burstClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/TuThanNguyenTo_BurstCharging.anim");
                Assert(Mathf.Approximately(burstClip.length, 2.0f), $"BurstCharging length expected 2.0s, got {burstClip.length}");

                var stunClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AnimFolder + "/TuThanNguyenTo_Stun.anim");
                Assert(Mathf.Approximately(stunClip.length, 3.0f), $"Stun length expected 3.0s, got {stunClip.length}");
            });

            Run("3.2 Animator Controller State Machine Configuration", () =>
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimFolder + "/TuThanNguyenTo.controller");
                Assert(ctrl != null, "TuThanNguyenTo.controller missing");

                var sm = ctrl.layers[0].stateMachine;
                string[] expectedStates = {
                    "Idle", "Move", "ThuyTrieu", "ThuyLieu", "DocQuat", "LoanChuyen",
                    "PhunDungNham", "HoaToc_Charge", "HoaToc_Dash", "BurstCharging", "Stun", "Die"
                };

                var stateNames = new HashSet<string>();
                foreach (var s in sm.states) stateNames.Add(s.state.name);

                foreach (var state in expectedStates)
                {
                    Assert(stateNames.Contains(state), $"State '{state}' missing in AnimatorController");
                }

                // Check parameters
                var paramNames = new HashSet<string>();
                foreach (var p in ctrl.parameters) paramNames.Add(p.name);
                Assert(paramNames.Contains("MoveSpeed"), "Float param 'MoveSpeed' missing");
                Assert(paramNames.Contains("ThuyTrieu"), "Trigger param 'ThuyTrieu' missing");
                Assert(paramNames.Contains("ThuyLieu"), "Trigger param 'ThuyLieu' missing");
                Assert(paramNames.Contains("DocQuat"), "Trigger param 'DocQuat' missing");
                Assert(paramNames.Contains("LoanChuyen"), "Trigger param 'LoanChuyen' missing");
                Assert(paramNames.Contains("PhunDungNham"), "Trigger param 'PhunDungNham' missing");
                Assert(paramNames.Contains("HoaToc_Charge"), "Trigger param 'HoaToc_Charge' missing");
                Assert(paramNames.Contains("HoaToc_Dash"), "Trigger param 'HoaToc_Dash' missing");
                Assert(paramNames.Contains("BurstCharging"), "Trigger param 'BurstCharging' missing");
                Assert(paramNames.Contains("Stun"), "Trigger param 'Stun' missing");
                Assert(paramNames.Contains("Die"), "Trigger param 'Die' missing");

                // Verify canTransitionToSelf is disabled on all AnyState transitions to prevent frame stuttering
                foreach (var trans in sm.anyStateTransitions)
                {
                    Assert(!trans.canTransitionToSelf, $"Transition to {trans.destinationState.name} must have canTransitionToSelf=false");
                }
            });

            // 4. VFX Prefabs Checks
            string[] expectedVfx = {
                "VFX_ElementalAura.prefab",
                "VFX_ThuyTrieuHitbox.prefab",
                "VFX_ThuyLieu.prefab",
                "VFX_DocQuatHitbox.prefab",
                "VFX_LoanChuyenBurstHitbox.prefab",
                "VFX_PhunDungNhamHitbox.prefab",
                "VFX_FireTornadoZone.prefab",
                "VFX_HoaTocFireTrailZone.prefab",
                "VFX_BurstChargingAura.prefab",
                "VFX_BurstShieldAndCrystals.prefab"
            };

            Run("4.1 All 10 VFX Prefabs Exist", () =>
            {
                foreach (var vfxName in expectedVfx)
                {
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/" + vfxName);
                    Assert(go != null, "Missing VFX prefab: " + vfxName);
                }
            });

            Run("4.2 VFX Hitbox Collider & OwnerRef Verification", () =>
            {
                // ThuyTrieu: PolygonCollider2D trigger 4m cone 60 deg
                var thuyTrieu = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_ThuyTrieuHitbox.prefab");
                var polyTrieu = thuyTrieu.GetComponent<PolygonCollider2D>();
                Assert(polyTrieu != null && polyTrieu.isTrigger, "ThuyTrieu PolygonCollider2D trigger missing");
                Assert(thuyTrieu.GetComponent<ColliderOwnerRef>() == null, "User constraint: VFX hitbox MUST NOT have ColliderOwnerRef");
                Assert(thuyTrieu.GetComponent<ThuyTrieuHitbox>() != null, "ThuyTrieuHitbox missing");
                Assert(thuyTrieu.GetComponent<ThuyTrieuVFX>() != null, "ThuyTrieuVFX missing");

                // DocQuat: PolygonCollider2D trigger 4m 180 deg
                var docQuat = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_DocQuatHitbox.prefab");
                var polyDoc = docQuat.GetComponent<PolygonCollider2D>();
                Assert(polyDoc != null && polyDoc.isTrigger, "DocQuat PolygonCollider2D trigger missing");
                Assert(docQuat.GetComponent<ColliderOwnerRef>() == null, "User constraint: VFX hitbox MUST NOT have ColliderOwnerRef");
                Assert(docQuat.GetComponent<DocQuatHitbox>() != null, "DocQuatHitbox missing");

                // LoanChuyen: CircleCollider2D radius 3m trigger
                var loanChuyen = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_LoanChuyenBurstHitbox.prefab");
                var circleLoan = loanChuyen.GetComponent<CircleCollider2D>();
                Assert(circleLoan != null && circleLoan.isTrigger && Mathf.Approximately(circleLoan.radius, 3f), "LoanChuyen CircleCollider2D (radius 3m, trigger) missing");
                Assert(loanChuyen.GetComponent<ColliderOwnerRef>() == null, "User constraint: VFX hitbox MUST NOT have ColliderOwnerRef");
                Assert(loanChuyen.GetComponent<LoanChuyenBurstHitbox>() != null, "LoanChuyenBurstHitbox missing");

                // PhunDungNham: BoxCollider2D size 16m x 1.2m
                var phunDungNham = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_PhunDungNhamHitbox.prefab");
                var boxDungNham = phunDungNham.GetComponent<BoxCollider2D>();
                Assert(boxDungNham != null && boxDungNham.isTrigger, "PhunDungNham BoxCollider2D trigger missing");
                Assert(Mathf.Approximately(boxDungNham.size.x, 16f) && Mathf.Approximately(boxDungNham.size.y, 1.2f), "PhunDungNham Box size must be (16, 1.2)");
                Assert(phunDungNham.GetComponent<ColliderOwnerRef>() == null, "User constraint: VFX hitbox MUST NOT have ColliderOwnerRef");
                Assert(phunDungNham.GetComponent<PhunDungNhamHitbox>() != null, "PhunDungNhamHitbox missing");
                Assert(phunDungNham.GetComponent<LavaBeamVFX>() != null, "LavaBeamVFX missing");

                // FireTornado: CircleCollider2D radius 2m trigger
                var tornado = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_FireTornadoZone.prefab");
                var circleTornado = tornado.GetComponent<CircleCollider2D>();
                Assert(circleTornado != null && circleTornado.isTrigger && Mathf.Approximately(circleTornado.radius, 2f), "FireTornado CircleCollider2D (radius 2m, trigger) missing");
                Assert(tornado.GetComponent<ColliderOwnerRef>() == null, "User constraint: VFX hitbox MUST NOT have ColliderOwnerRef");
                Assert(tornado.GetComponent<FireTornadoZone>() != null, "FireTornadoZone missing");

                // HoaTocFireTrail: BoxCollider2D trigger
                var trail = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_HoaTocFireTrailZone.prefab");
                var boxTrail = trail.GetComponent<BoxCollider2D>();
                Assert(boxTrail != null && boxTrail.isTrigger, "HoaTocFireTrail BoxCollider2D trigger missing");
                Assert(trail.GetComponent<ColliderOwnerRef>() == null, "User constraint: VFX hitbox MUST NOT have ColliderOwnerRef");
                var trailZone = trail.GetComponent<HoaTocFireTrailZone>();
                Assert(trailZone != null, "HoaTocFireTrailZone missing");
                Assert(trail.transform.Find("TrailSprite") != null, "Child 'TrailSprite' missing in HoaTocFireTrailZone");
                Assert(trail.transform.Find("GroundFlames") != null, "Child 'GroundFlames' missing in HoaTocFireTrailZone");
            });

            Run("4.3 VFX Controller Prefab Slot Wiring", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var ctrl = prefab.GetComponent<ElementalReaperVFXController>();
                Assert(ctrl != null, "ElementalReaperVFXController missing");
                Assert(ctrl.ElementalAuraPrefab != null, "elementalAuraPrefab not wired");
                Assert(ctrl.ThuyTrieuHitboxPrefab != null, "thuyTrieuHitboxPrefab not wired");
                Assert(ctrl.ThuyLieuVFXPrefab != null, "thuyLieuVFXPrefab not wired");
                Assert(ctrl.DocQuatHitboxPrefab != null, "docQuatHitboxPrefab not wired");
                Assert(ctrl.LoanChuyenBurstHitboxPrefab != null, "loanChuyenBurstHitboxPrefab not wired");
                Assert(ctrl.PhunDungNhamHitboxPrefab != null, "phunDungNhamHitboxPrefab not wired");
                Assert(ctrl.FireTornadoZonePrefab != null, "fireTornadoZonePrefab not wired");
                Assert(ctrl.HoaTocFireTrailZonePrefab != null, "hoaTocFireTrailZonePrefab not wired");
                Assert(ctrl.BurstChargingAuraPrefab != null, "burstChargingAuraPrefab not wired");
                Assert(ctrl.BurstShieldAndCrystalsPrefab != null, "burstShieldAndCrystalsPrefab not wired");
                Assert(ctrl.HoaTocDashHitboxComponent != null, "hoaTocDashHitbox not wired on Boss prefab");
            });

            // 5. Runtime Instantiation & VFX Method Execution Test
            Run("5.1 Runtime Instantiation & VFX Invocation Test", () =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var instance = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var ctrl = instance.GetComponent<ElementalReaperVFXController>();
                    Assert(ctrl != null, "Controller component missing on instance");

                    // Test Elemental Aura switching
                    ctrl.SetElementalAura(ElementalType.Water);
                    Assert(ctrl.ActiveElementalAura != null && ctrl.ActiveElementalAura.CurrentElement == ElementalType.Water, "Water aura switch failed");
                    ctrl.SetElementalAura(ElementalType.Poison);
                    Assert(ctrl.ActiveElementalAura != null && ctrl.ActiveElementalAura.CurrentElement == ElementalType.Poison, "Poison aura switch failed");
                    ctrl.SetElementalAura(ElementalType.Fire);
                    Assert(ctrl.ActiveElementalAura != null && ctrl.ActiveElementalAura.CurrentElement == ElementalType.Fire, "Fire aura switch failed");

                    // Test Dash Hitbox activation & deactivation
                    ctrl.ActivateHoaTocDash(Vector2.right);
                    Assert(ctrl.HoaTocDashHitboxComponent != null && ctrl.HoaTocDashHitboxComponent.gameObject.activeSelf, "ActivateHoaTocDash failed to activate hitbox");
                    ctrl.DeactivateHoaTocDash();
                    Assert(!ctrl.HoaTocDashHitboxComponent.gameObject.activeSelf, "DeactivateHoaTocDash failed to deactivate hitbox");

                    // Test Spawning each VFX/Hitbox
                    var hitTrieu = ctrl.PlayThuyTrieu(Vector3.right);
                    Assert(hitTrieu != null && hitTrieu.IsInitialized, "PlayThuyTrieu failed");
                    UnityEngine.Object.DestroyImmediate(hitTrieu.gameObject);

                    var vfxLieu = ctrl.PlayThuyLieu();
                    Assert(vfxLieu != null, "PlayThuyLieu failed");
                    UnityEngine.Object.DestroyImmediate(vfxLieu.gameObject);

                    var hitDoc = ctrl.PlayDocQuat();
                    Assert(hitDoc != null && hitDoc.IsInitialized, "PlayDocQuat failed");
                    UnityEngine.Object.DestroyImmediate(hitDoc.gameObject);

                    var hitLoan = ctrl.PlayLoanChuyenBurst(Vector3.zero);
                    Assert(hitLoan != null && hitLoan.IsInitialized, "PlayLoanChuyenBurst failed");
                    UnityEngine.Object.DestroyImmediate(hitLoan.gameObject);

                    var hitPhun = ctrl.PlayPhunDungNham(Vector3.right);
                    Assert(hitPhun != null && hitPhun.IsInitialized, "PlayPhunDungNham failed");
                    UnityEngine.Object.DestroyImmediate(hitPhun.gameObject);

                    var zoneTornado = ctrl.SpawnFireTornado(new Vector3(5f, 5f, 0f));
                    Assert(zoneTornado != null && zoneTornado.IsInitialized, "SpawnFireTornado failed");
                    UnityEngine.Object.DestroyImmediate(zoneTornado.gameObject);

                    var zoneTrail = ctrl.SpawnHoaTocFireTrail(Vector3.zero, new Vector3(10f, 0f, 0f));
                    Assert(zoneTrail != null && zoneTrail.IsInitialized, "SpawnHoaTocFireTrail failed");
                    UnityEngine.Object.DestroyImmediate(zoneTrail.gameObject);

                    // Test Burst Charging
                    var auraCharge = ctrl.StartBurstCharging(2.0f);
                    Assert(auraCharge != null && auraCharge.IsCharging, "StartBurstCharging failed");
                    ctrl.StopBurstCharging();
                    Assert(!auraCharge.IsCharging, "StopBurstCharging failed");

                    // Test Burst Shield & Crystals
                    var shield = ctrl.ActivateBurstShield();
                    Assert(shield != null && shield.gameObject.activeSelf, "ActivateBurstShield failed");
                    ctrl.DeactivateBurstShield();
                    Assert(ctrl.ActiveBurstShield == null, "DeactivateBurstShield failed");

                    // Test Animation trigger helper methods
                    ctrl.PlayHoaTocDashAnimation();
                    ctrl.PlayStunAnimation();
                    ctrl.PlayDieAnimation();
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            });

            // 6. Hitbox Combat & Crowd Control Integration Tests
            Run("6.1 ThuyTrieu Combat & Knockback CC", () =>
            {
                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankGo = UnityEngine.Object.Instantiate(tankPrefab);
                var tank = tankGo.GetComponent<TankBrain>();
                tank.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f, Attack = 50f, Defense = 0f, MoveSpeed = 100f });

                var hitboxGo = new GameObject("Test_ThuyTrieu");
                hitboxGo.AddComponent<PolygonCollider2D>();
                hitboxGo.AddComponent<ColliderOwnerRef>();
                var hitbox = hitboxGo.AddComponent<ThuyTrieuHitbox>();
                hitbox.Initialize(null, 250f);

                try
                {
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 750f), $"Expected 750 HP, got {tank.CurrentHealth}");
                    Assert(tank.HasCC(CrowdControlType.Knockback), "Tank must have Knockback CC");

                    // Test single hit rule
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 750f), "Tank must not take damage twice from single wave");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hitboxGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });

            Run("6.2 DocQuat Combat & Slow CC & Def Reduction", () =>
            {
                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankGo = UnityEngine.Object.Instantiate(tankPrefab);
                var tank = tankGo.GetComponent<TankBrain>();
                tank.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f, Attack = 50f, Defense = 100f, MoveSpeed = 100f });

                var hitboxGo = new GameObject("Test_DocQuat");
                hitboxGo.AddComponent<PolygonCollider2D>();
                hitboxGo.AddComponent<ColliderOwnerRef>();
                var hitbox = hitboxGo.AddComponent<DocQuatHitbox>();
                hitbox.Initialize(null, 250f);

                try
                {
                    float expectedDamage = CombatUtility.ResolveDamage(250f, 100f, 0f);
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 1000f - expectedDamage), $"Expected {1000f - expectedDamage} HP, got {tank.CurrentHealth}");
                    Assert(tank.HasCC(CrowdControlType.Slow), "Tank must have Slow CC");
                    Assert(tank.GetEffect("DocQuat_DefDebuff") != null, "Tank must have StatModifierEffect 'DocQuat_DefDebuff'");
                    Assert(Mathf.Approximately(tank.Stats.Defense, 90f), $"Tank DEF should be reduced by 10% (100 -> 90), got {tank.Stats.Defense}");

                    // Single hit rule
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 1000f - expectedDamage), "Tank must not take damage twice");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hitboxGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });

            Run("6.3 LoanChuyen Combat & Stun CC", () =>
            {
                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankGo = UnityEngine.Object.Instantiate(tankPrefab);
                var tank = tankGo.GetComponent<TankBrain>();
                tank.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f, Attack = 50f, Defense = 0f, MoveSpeed = 100f });

                var hitboxGo = new GameObject("Test_LoanChuyen");
                hitboxGo.AddComponent<CircleCollider2D>();
                hitboxGo.AddComponent<ColliderOwnerRef>();
                var hitbox = hitboxGo.AddComponent<LoanChuyenBurstHitbox>();
                hitbox.Initialize(null, 250f);

                try
                {
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 750f), $"Expected 750 HP, got {tank.CurrentHealth}");
                    Assert(tank.HasCC(CrowdControlType.Stun), "Tank must have Stun CC");

                    // Single hit rule
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 750f), "Tank must not take damage twice");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hitboxGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });

            Run("6.4 PhunDungNham 120% ATK Combat", () =>
            {
                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankGo = UnityEngine.Object.Instantiate(tankPrefab);
                var tank = tankGo.GetComponent<TankBrain>();
                tank.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f, Attack = 50f, Defense = 0f, MoveSpeed = 100f });

                var hitboxGo = new GameObject("Test_PhunDungNham");
                hitboxGo.AddComponent<BoxCollider2D>();
                hitboxGo.AddComponent<ColliderOwnerRef>();
                var hitbox = hitboxGo.AddComponent<PhunDungNhamHitbox>();
                hitbox.Initialize(null, 250f); // 120% of 250 = 300 damage

                try
                {
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 700f), $"Expected 700 HP (1000 - 300), got {tank.CurrentHealth}");

                    // Single hit rule
                    hitbox.ApplyHitToTank(tank);
                    Assert(Mathf.Approximately(tank.CurrentHealth, 700f), "Tank must not take damage twice");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hitboxGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });

            Run("6.5 HoaTocDashHitbox Combat & Knockback Side Push", () =>
            {
                var tankPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tanks/Tank.prefab");
                var tankGo = UnityEngine.Object.Instantiate(tankPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity);
                var tank = tankGo.GetComponent<TankBrain>();
                tank.Initialize(new TankBrainContext { Level = 1 }, new TankStats { MaxHealth = 1000f, Attack = 50f, Defense = 0f, MoveSpeed = 100f });

                var hitboxGo = new GameObject("Test_HoaTocDash");
                hitboxGo.AddComponent<CircleCollider2D>();
                hitboxGo.AddComponent<ColliderOwnerRef>();
                var hitbox = hitboxGo.AddComponent<HoaTocDashHitbox>();
                hitbox.Activate(null, 250f, Vector2.right);

                try
                {
                    // Call private ApplyDashHit via reflection or triggering hit
                    var m = typeof(HoaTocDashHitbox).GetMethod("ApplyDashHit", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    Assert(m != null, "ApplyDashHit method missing on HoaTocDashHitbox");
                    m.Invoke(hitbox, new object[] { tank });

                    Assert(Mathf.Approximately(tank.CurrentHealth, 750f), $"Expected 750 HP, got {tank.CurrentHealth}");
                    Assert(tank.HasCC(CrowdControlType.Knockback), "Tank must receive Knockback CC from dash");

                    // Single hit rule
                    m.Invoke(hitbox, new object[] { tank });
                    Assert(Mathf.Approximately(tank.CurrentHealth, 750f), "Tank must not take damage twice from single dash");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(hitboxGo);
                    UnityEngine.Object.DestroyImmediate(tankGo);
                }
            });

            Run("6.6 HoaTocFireTrailZone Dynamic SetupTrail Resizing", () =>
            {
                var trailPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VfxFolder + "/VFX_HoaTocFireTrailZone.prefab");
                var trailGo = UnityEngine.Object.Instantiate(trailPrefab);
                var zone = trailGo.GetComponent<HoaTocFireTrailZone>();
                Assert(zone != null, "HoaTocFireTrailZone missing on instance");

                try
                {
                    Vector2 start = new Vector2(0f, 0f);
                    Vector2 end = new Vector2(16f, 0f); // 16m dash distance
                    zone.SetupTrail(start, end, null, 250f);

                    var boxCol = trailGo.GetComponent<BoxCollider2D>();
                    Assert(Mathf.Approximately(boxCol.size.x, 16f), $"BoxCollider size.x expected 16m, got {boxCol.size.x}");
                    Assert(Mathf.Approximately(boxCol.size.y, zone.TrailWidth), $"BoxCollider size.y expected {zone.TrailWidth}, got {boxCol.size.y}");

                    var trailSprite = trailGo.transform.Find("TrailSprite");
                    Assert(trailSprite != null, "TrailSprite missing");
                    var sr = trailSprite.GetComponent<SpriteRenderer>();
                    float renderedLength = trailSprite.localScale.x * (sr != null && sr.sprite != null ? sr.sprite.bounds.size.x : 1f);
                    Assert(Mathf.Abs(renderedLength - 16f) < 0.05f, $"TrailSprite rendered length expected 16m, got {renderedLength}");

                    var groundFlames = trailGo.transform.Find("GroundFlames");
                    Assert(groundFlames != null, "GroundFlames missing");
                    var ps = groundFlames.GetComponent<ParticleSystem>();
                    Assert(Mathf.Approximately(ps.shape.scale.x, 16f), $"Flames shape scale.x expected 16m, got {ps.shape.scale.x}");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(trailGo);
                }
            });

            sb.AppendLine($"\nRESULT: {passed}/{total} tests passed ({(passed == total ? "100% SUCCESS" : "FAILURES DETECTED")}).");
            return sb.ToString();
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
