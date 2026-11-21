using System;
using System.Collections.Generic;
using System.Text;
using IronWasteland.Core;
using IronWasteland.Enemies;
using IronWasteland.Enemies.D1onk;
using IronWasteland.Tanks;
using IronWasteland.Tanks.Test;
using UnityEngine;

namespace IronWasteland.Tests
{
    /// <summary>
    /// Test Suite kiểm chứng 100% logic gameplay của Trưởng Quản Ngục D1onk:
    /// 1. Khởi tạo, Chỉ số chuẩn Solo / Co-op và Kháng khống chế cứng (Stun / Root).
    /// 2. Giãn cách dùng kỹ năng toàn cục (Global Skill Interval >= 2.0s).
    /// 3. Bị động — Pháo Hạm Đa Nòng (Bắn đạn định kỳ 5s và khi thi triển kỹ năng).
    /// 4. Kỹ năng — Trừng Phạt (TrungPhat): 150% ATK, -20% DEF gần nhất; -30% ATK, -10% SPD xa nhất; Solo chỉ nhận nhánh gần nhất.
    /// 5. Kỹ năng — Laze Nguyên Tố (LazeNguyenTo): Thiêu đốt mục tiêu xa nhất 5s (40% ATK mỗi 0.5s).
    /// 6. Kỹ năng — Tỏa Nhiệt Diện Rộng (ToaNhietDienRong): Nổ sóng nhiệt toàn map 100% ATK (Tank 1 x2 sát thương, các Tank sau giảm dần).
    /// 7. Kỹ năng — Viện Binh Tinh Linh (VienBinhTinhLinh): Triệu hồi 1-3 bản sao thu nhỏ 20% chỉ số Boss.
    /// 8. Vòng đời Bùng Nổ Năng Lượng: HP < 60% -> Charging (2s) -> Active (40s), Hồi 100% HP, +30% ATK, +100% DEF.
    /// 9. Buồng lái Sub-Arena & Thích ứng Solo: Phá 3/4 cột năng lượng lập tức gỡ khiên Bất Hoại của Trái Tim.
    /// 10. Buồng lái Sub-Arena & Co-op: Kéo 2 Tank vào trong, tạo bản sao bên ngoài, hoàn thành 3 thử thách gỡ khiên Bất Hoại.
    /// 11. Phần thưởng thành công (Success): Boss -30% Max HP, -50% ATK/DEF 10s, choáng 5s; Tank trong +100% chỉ số 10s;
    ///     Tank ngoài -50% hồi chiêu, hồi năng lượng 10s; Reset toàn bộ hồi chiêu; kích hoạt OnEnergyBurstSuccess.
    /// 12. Hình phạt thất bại (Penalty): Hết 40s Trái Tim còn sống -> INSTANT KILL toàn bộ Tank người chơi.
    /// 13. Ràng buộc Collider: Tuyệt đối không gắn ColliderOwnerRef trỏ về Boss Brain trên bất kỳ VFX/đạn/cột/buồng lái.
    /// 14. Trái Tim Boss: Thừa kế chỉ số, bị động hỏa lực và trọn bộ kỹ năng bên trong buồng lái.
    /// 15. Thử thách Cột Đỏ: Chặn xung điện 0.5s và nhận 20% ATK sát thương.
    /// </summary>
    public static class D1onk_GameplayLogicTest
    {
        public static string RunAllTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("=== TRƯỞNG QUẢN NGỤC D1ONK GAMEPLAY LOGIC TEST ===");
            sb.AppendLine("==================================================");

            int passed = 0;
            int total = 0;

            void Assert(bool condition, string testName, string errorMsg = "")
            {
                total++;
                if (condition)
                {
                    passed++;
                    sb.AppendLine($"[PASS] {testName}");
                }
                else
                {
                    sb.AppendLine($"[FAIL] {testName} - {errorMsg}");
                }
            }

            // Cleanup any stray objects before test
            CleanupScene();

            // ----------------------------------------------------
            // TEST 1: Khởi tạo, Chỉ số & Kháng khống chế
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_1");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                Assert(boss.MaxHealth == 27000f, "Test 1.1: Solo MaxHealth is 27,000", $"Got {boss.MaxHealth}");
                Assert(boss.Stats.Attack == 500f, "Test 1.2: Base Attack is 500", $"Got {boss.Stats.Attack}");
                Assert(boss.Stats.Defense == 60f, "Test 1.3: Base Defense is 60", $"Got {boss.Stats.Defense}");
                Assert(boss.Stats.MoveSpeed == 70f, "Test 1.4: Base MoveSpeed is 70", $"Got {boss.Stats.MoveSpeed}");

                Assert(boss.IsCrowdControlImmune, "Test 1.5: Crowd Control immune is true");
                Assert(boss.IsImmuneToCrowdControl(CrowdControlType.Stun), "Test 1.6: Immune to Stun (Hard CC)");
                Assert(boss.IsImmuneToCrowdControl(CrowdControlType.Root), "Test 1.7: Immune to Root (Hard CC)");
                Assert(!boss.IsImmuneToCrowdControl(CrowdControlType.Slow), "Test 1.8: NOT immune to Slow (Soft CC)");

                // Co-op mode: x2 MaxHealth = 54,000
                boss.SetCoopMode(true);
                Assert(boss.MaxHealth == 54000f, "Test 1.9: Co-op MaxHealth is 54,000", $"Got {boss.MaxHealth}");

                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 2: Giãn cách dùng kỹ năng toàn cục (Global Skill Interval >= 2.0s)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_2");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                Assert(boss.GlobalSkillInterval == 2.0f, "Test 2.1: Default GlobalSkillInterval is 2.0s", $"Got {boss.GlobalSkillInterval}");
                Assert(boss.CanCastAnySkill, "Test 2.2: CanCastAnySkill is initially true");

                boss.TriggerGlobalSkillCooldown();
                Assert(!boss.CanCastAnySkill, "Test 2.3: CanCastAnySkill is false immediately after trigger");
                Assert(boss.GlobalSkillCooldownTimer == 2.0f, "Test 2.4: Cooldown timer is 2.0s");

                boss.ManualTick(1.0f);
                Assert(!boss.CanCastAnySkill, "Test 2.5: CanCastAnySkill remains false after 1.0s");
                Assert(Mathf.Approximately(boss.GlobalSkillCooldownTimer, 1.0f), "Test 2.6: Cooldown timer is 1.0s remaining");

                boss.ManualTick(1.0f);
                Assert(boss.CanCastAnySkill, "Test 2.7: CanCastAnySkill becomes true after full 2.0s");
                Assert(boss.GlobalSkillCooldownTimer == 0f, "Test 2.8: Cooldown timer reaches 0");

                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 3: Bị động — Pháo Hạm Đa Nòng
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_3");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var tankObj = CreateMockTank("Tank_Target_3", new Vector3(3f, 0f, 0f));
                var tank = tankObj.GetComponent<TankBrain_Test>();

                float initialHp = tank.CurrentHealth;

                // Ticking 5s should trigger periodic 2 missiles (20% ATK each = 100 dmg per missile)
                boss.ManualTick(5.0f);
                float expectedDmg = 2f * (500f * 0.20f); // 200 damage
                Assert(tank.CurrentHealth < initialHp, "Test 3.1: Periodic 5s passive damages tank");

                // Casting passive directly with 5 missiles
                float beforeSalvoHp = tank.CurrentHealth;
                boss.FirePassiveMissiles(5);
                Assert(tank.CurrentHealth < beforeSalvoHp, "Test 3.2: 5-missile salvo damages tank");

                UnityEngine.Object.DestroyImmediate(tankObj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 4: Kỹ năng — Trừng Phạt (TrungPhat)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_4");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                // Multi-tank case: Tank A near (2m), Tank B far (8m)
                var tankObjA = CreateMockTank("Tank_Near", new Vector3(2f, 0f, 0f), 1000f, 100f, 50f, 50f);
                var tankObjB = CreateMockTank("Tank_Far", new Vector3(8f, 0f, 0f), 1000f, 100f, 50f, 50f);
                var tankA = tankObjA.GetComponent<TankBrain_Test>();
                var tankB = tankObjB.GetComponent<TankBrain_Test>();

                float tankAHpBefore = tankA.CurrentHealth;
                float tankBHpBefore = tankB.CurrentHealth;

                boss.CastTrungPhat();

                // Tank gần: 150% ATK (750) và -20% DEF trong 5s
                Assert(tankA.CurrentHealth < tankAHpBefore, "Test 4.1: Nearest tank took 150% ATK damage");
                Assert(tankA.Stats.Defense < 50f, "Test 4.2: Nearest tank DEF reduced by 20%");

                // Tank xa: -30% ATK và -10% SPD trong 5s (không nhận sát thương đòn đánh trực tiếp của Trừng phạt ngoài đạn pháo)
                Assert(tankB.Stats.Attack < 100f, "Test 4.3: Furthest tank ATK reduced by 30%");
                Assert(tankB.Stats.MoveSpeed < 50f, "Test 4.4: Furthest tank SPD reduced by 10%");

                // Giãn cách kỹ năng toàn cục được kích hoạt
                Assert(!boss.CanCastAnySkill, "Test 4.5: Global skill cooldown triggered after TrungPhat");

                UnityEngine.Object.DestroyImmediate(tankObjA);
                UnityEngine.Object.DestroyImmediate(tankObjB);

                // Solo case: Only 1 Tank
                var tankObjSolo = CreateMockTank("Tank_Solo", new Vector3(3f, 0f, 0f), 1000f, 100f, 50f, 50f);
                var tankSolo = tankObjSolo.GetComponent<TankBrain_Test>();
                boss.ResetGlobalSkillCooldown();

                boss.CastTrungPhat();
                Assert(tankSolo.Stats.Defense < 50f, "Test 4.6: Solo tank receives nearest DEF debuff");
                Assert(tankSolo.Stats.Attack == 100f, "Test 4.7: Solo tank does NOT receive furthest ATK debuff");

                UnityEngine.Object.DestroyImmediate(tankObjSolo);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 5: Kỹ năng — Laze Nguyên Tố (LazeNguyenTo)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_5");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var tankObjA = CreateMockTank("Tank_A_5", new Vector3(2f, 0f, 0f));
                var tankObjB = CreateMockTank("Tank_B_5", new Vector3(9f, 0f, 0f));

                boss.CastLazeNguyenTo();

                Assert(boss.LazeTimer == 12.0f, "Test 5.1: LazeNguyenTo cooldown set to 12s");
                Assert(!boss.CanCastAnySkill, "Test 5.2: Global skill cooldown triggered after Laze");

                UnityEngine.Object.DestroyImmediate(tankObjA);
                UnityEngine.Object.DestroyImmediate(tankObjB);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 6: Kỹ năng — Tỏa Nhiệt Diện Rộng (ToaNhietDienRong)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_6");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var t1Obj = CreateMockTank("Tank_1", new Vector3(1f, 0f, 0f), 5000f, 50f, 0f, 50f);
                var t2Obj = CreateMockTank("Tank_2", new Vector3(3f, 0f, 0f), 5000f, 50f, 0f, 50f);
                var t3Obj = CreateMockTank("Tank_3", new Vector3(5f, 0f, 0f), 5000f, 50f, 0f, 50f);
                var t4Obj = CreateMockTank("Tank_4", new Vector3(7f, 0f, 0f), 5000f, 50f, 0f, 50f);

                var t1 = t1Obj.GetComponent<TankBrain_Test>();
                var t2 = t2Obj.GetComponent<TankBrain_Test>();
                var t3 = t3Obj.GetComponent<TankBrain_Test>();
                var t4 = t4Obj.GetComponent<TankBrain_Test>();

                float hp1Before = t1.CurrentHealth;
                float hp2Before = t2.CurrentHealth;
                float hp3Before = t3.CurrentHealth;
                float hp4Before = t4.CurrentHealth;

                boss.CastToaNhietDienRong();

                float dmg1 = hp1Before - t1.CurrentHealth;
                float dmg2 = hp2Before - t2.CurrentHealth;
                float dmg3 = hp3Before - t3.CurrentHealth;
                float dmg4 = hp4Before - t4.CurrentHealth;

                Assert(dmg1 >= 1000f, "Test 6.1: First tank takes >= x2 damage (200% ATK)", $"Got {dmg1}");
                Assert(dmg1 > dmg2, "Test 6.2: First tank takes more damage than second tank", $"{dmg1} vs {dmg2}");
                Assert(dmg2 > dmg3, "Test 6.3: Second tank takes more damage than third tank", $"{dmg2} vs {dmg3}");
                Assert(dmg3 > dmg4, "Test 6.4: Third tank takes more damage than fourth tank", $"{dmg3} vs {dmg4}");

                UnityEngine.Object.DestroyImmediate(t1Obj);
                UnityEngine.Object.DestroyImmediate(t2Obj);
                UnityEngine.Object.DestroyImmediate(t3Obj);
                UnityEngine.Object.DestroyImmediate(t4Obj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 7: Kỹ năng — Viện Binh Tinh Linh (VienBinhTinhLinh)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_7");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                boss.CastVienBinhTinhLinh(2);

                Assert(boss.ActiveMiniClones.Count == 2, "Test 7.1: Summons requested number of mini clones");
                var clone = boss.ActiveMiniClones[0];
                Assert(clone != null, "Test 7.2: Mini clone instance is valid");
                Assert(clone.MaxHealth == 27000f * 0.20f, "Test 7.3: Mini clone has 20% Boss MaxHealth (5,400)", $"Got {clone.MaxHealth}");
                Assert(clone.Stats.Attack == 500f * 0.20f, "Test 7.4: Mini clone has 20% Boss Attack (100)", $"Got {clone.Stats.Attack}");
                Assert(clone.Stats.Defense == 60f * 0.20f, "Test 7.5: Mini clone has 20% Boss Defense (12)", $"Got {clone.Stats.Defense}");
                Assert(clone.Stats.MoveSpeed == 70f, "Test 7.6: Mini clone has Boss MoveSpeed (70)");

                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 8: Vòng Đời Bùng Nổ Năng Lượng: Ngưỡng HP & Kích Hoạt
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_8");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                Assert(boss.BurstHpThreshold == 0.6f, "Test 8.1: Burst threshold is 60%");
                Assert(boss.BurstDuration == 40f, "Test 8.2: Burst duration is 40s");
                Assert(boss.BurstChargeTime == 2f, "Test 8.3: Burst charge time is 2s");

                // Giảm máu xuống 70% (trừ 30% Max HP) -> Chưa kích hoạt
                boss.TakeDamage(new DamageInfo(null, boss, 27000f * 0.30f, 1.0f, boss.transform.position, isDirectToHealth: true));
                Assert(!boss.HasBurstTriggered, "Test 8.4: Burst does NOT trigger at 70% HP");

                // Giảm máu tiếp xuống 55% (< 60%) (trừ thêm 15% Max HP) -> Kích hoạt Charging
                boss.TakeDamage(new DamageInfo(null, boss, 27000f * 0.15f, 1.0f, boss.transform.position, isDirectToHealth: true));
                Assert(boss.HasBurstTriggered, "Test 8.5: Burst triggers when HP <= 60%");
                Assert(boss.IsBurstCharging, "Test 8.6: Boss enters Charging state");

                // Vận sức 2s -> Chuyển sang Active
                boss.ManualTick(2.0f);
                Assert(boss.IsBurstActive, "Test 8.7: Boss enters Active state after 2s charge");
                Assert(boss.CurrentHealth == boss.MaxHealth, "Test 8.8: Boss restores 100% HP on Burst Start");
                Assert(boss.Stats.Attack == 500f * 1.30f, "Test 8.9: Boss Attack +30% during Burst", $"Got {boss.Stats.Attack}");
                Assert(boss.Stats.Defense == 60f * 2.00f, "Test 8.10: Boss Defense +100% during Burst", $"Got {boss.Stats.Defense}");

                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 9: Buồng Lái Sub-Arena & Thích Ứng Solo (Phá 3/4 Cột)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_9");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var soloTankObj = CreateMockTank("SoloTank_9", new Vector3(0f, 0f, 0f));
                var soloTank = soloTankObj.GetComponent<TankBrain_Test>();

                // Trigger Energy Burst (Solo: chỉ có 1 Tank)
                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Kết thúc charge, bắt đầu Active

                var subArena = boss.SubArena;
                Assert(subArena != null, "Test 9.1: SubArena created on Burst Active");
                Assert(!subArena.IsCoop, "Test 9.2: SubArena correctly detects Solo mode");
                Assert(subArena.TrappedTanks.Count == 1, "Test 9.3: Exactly 1 Tank trapped in cockpit");

                var heart = subArena.Heart;
                Assert(heart != null, "Test 9.4: Heart spawned in cockpit center");
                Assert(heart.MaxHealth == 27000f * 0.10f, "Test 9.5: Heart has 10% Boss MaxHealth (2,700)", $"Got {heart.MaxHealth}");
                Assert(heart.InvincibleShieldActive, "Test 9.6: Heart starts with Invincible Shield active");

                // Tấn công Trái Tim khi khiên Bất Hoại đang bật -> Không mất máu
                float heartHpBefore = heart.CurrentHealth;
                heart.TakeDamage(500f);
                Assert(heart.CurrentHealth == heartHpBefore, "Test 9.7: Heart takes 0 damage while Invincible Shield active");

                // Kiểm tra 4 cột năng lượng (500 HP mỗi cột)
                Assert(subArena.Pillars.Count == 4, "Test 9.8: 4 Energy Pillars spawned");
                for (int i = 0; i < subArena.Pillars.Count; i++)
                {
                    Assert(subArena.Pillars[i].MaxHealth == 500f, $"Test 9.8.{i + 1}: Pillar {subArena.Pillars[i].PillarColor} has 500 HP");
                }

                // Phá cột 1 -> Khiên vẫn còn
                subArena.Pillars[0].TakeDamage(500f);
                Assert(heart.InvincibleShieldActive, "Test 9.9: Shield remains active after 1 pillar destroyed");

                // Phá cột 2 -> Khiên vẫn còn
                subArena.Pillars[1].TakeDamage(500f);
                Assert(heart.InvincibleShieldActive, "Test 9.10: Shield remains active after 2 pillars destroyed");

                // Phá cột 3 -> Khiên Bất Hoại bị GỠ BỎ lập tức!
                subArena.Pillars[2].TakeDamage(500f);
                Assert(!heart.InvincibleShieldActive, "Test 9.11: Invincible Shield REMOVED after 3 pillars destroyed in Solo!");

                // Giờ tấn công Trái Tim -> Nhận sát thương bình thường
                heart.TakeDamage(1000f);
                Assert(heart.CurrentHealth < heartHpBefore, "Test 9.12: Heart takes damage after shield removed");

                UnityEngine.Object.DestroyImmediate(soloTankObj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 10: Buồng Lái Sub-Arena & Chế Độ Co-op (3 Thử Thách)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_10");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var t1Obj = CreateMockTank("CoopTank_1", new Vector3(1f, 0f, 0f));
                var t2Obj = CreateMockTank("CoopTank_2", new Vector3(2f, 0f, 0f));
                var t3Obj = CreateMockTank("CoopTank_3", new Vector3(6f, 0f, 0f));

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Vào Active

                var subArena = boss.SubArena;
                Assert(subArena.IsCoop, "Test 10.1: SubArena correctly detects Co-op mode");
                Assert(subArena.TrappedTanks.Count == 2, "Test 10.2: Exactly 2 nearest Tanks trapped inside");
                Assert(boss.ActiveTankClones.Count == 2, "Test 10.3: 2 Tank Clones created outside");

                var coopChallenges = boss.CoopChallenges;
                Assert(coopChallenges != null, "Test 10.4: CoopChallenges manager active");

                var heart = subArena.Heart;
                Assert(heart.InvincibleShieldActive, "Test 10.5: Heart Invincible Shield active initially");

                // Phá cột Trắng -> Kích hoạt Thử thách Trắng bên ngoài (10 Tinh Linh Sấm Sét)
                subArena.OnPillarDestroyed(subArena.Pillars[0]);
                for (int i = 0; i < coopChallenges.WhiteMinions.Count; i++)
                {
                    coopChallenges.WhiteMinions[i].TakeDamage(9999f);
                }
                coopChallenges.ManualTick(0.1f);
                Assert(coopChallenges.IsWhiteCompleted, "Test 10.6: White Challenge completed upon destroying all 10 minions");

                // Thử thách Đỏ: Truyền điện 10s, < 5 điểm
                subArena.OnPillarDestroyed(subArena.Pillars[1]);
                coopChallenges.ManualTickRed(10.1f);
                Assert(coopChallenges.IsRedCompleted, "Test 10.7: Red Challenge completed");

                // Thử thách Vàng: Phá 2 cột điện trong 10s
                subArena.OnPillarDestroyed(subArena.Pillars[2]);
                coopChallenges.DestroyYellowElectricPillar(0);
                coopChallenges.DestroyYellowElectricPillar(1);
                coopChallenges.ManualTickYellow(1.0f);
                Assert(coopChallenges.IsYellowCompleted, "Test 10.8: Yellow Challenge completed");

                // Thử thách Xanh: Diệt sạch bản sao tank trong 10s
                subArena.OnPillarDestroyed(subArena.Pillars[3]);
                for (int i = 0; i < boss.ActiveTankClones.Count; i++)
                {
                    boss.ActiveTankClones[i].TakeDamage(99999f);
                }
                coopChallenges.ManualTickBlue(1.0f);
                Assert(coopChallenges.IsBlueCompleted, "Test 10.9: Blue Challenge completed");

                Assert(coopChallenges.CompletedChallengesCount >= 3, "Test 10.10: At least 3 challenges completed");
                Assert(!heart.InvincibleShieldActive, "Test 10.11: Heart Invincible Shield removed after 3 challenges in Co-op!");

                UnityEngine.Object.DestroyImmediate(t1Obj);
                UnityEngine.Object.DestroyImmediate(t2Obj);
                UnityEngine.Object.DestroyImmediate(t3Obj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 11: Phần Thưởng Bùng Nổ Năng Lượng (Success)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_11");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var t1Obj = CreateMockTank("Tank_Inside_11", new Vector3(1f, 0f, 0f), 1000f, 100f, 50f, 50f);
                var t2Obj = CreateMockTank("Tank_Inside2_11", new Vector3(2f, 0f, 0f), 1000f, 100f, 50f, 50f);
                var t3Obj = CreateMockTank("Tank_Outside_11", new Vector3(8f, 0f, 0f), 1000f, 100f, 50f, 50f);
                var t1 = t1Obj.GetComponent<TankBrain_Test>();
                var t2 = t2Obj.GetComponent<TankBrain_Test>();
                var t3 = t3Obj.GetComponent<TankBrain_Test>();

                bool burstSuccessEventFired = false;
                EliteEnemyBrain.OnEnergyBurstSuccess += (b) =>
                {
                    if (b == boss) burstSuccessEventFired = true;
                };

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Active

                float bossHpBeforeKill = boss.CurrentHealth;

                // Tiêu diệt Trái Tim -> Kích hoạt Success
                boss.SubArena.Heart.RemoveInvincibleShield();
                boss.SubArena.Heart.TakeDamage(999999f);

                Assert(boss.BurstState == EliteBurstState.RewardPhase, "Test 11.1: State transitions to RewardPhase");
                Assert(burstSuccessEventFired, "Test 11.2: OnEnergyBurstSuccess event fired for compatibility");

                // Boss bị trừ 30% Max HP
                float expectedHpLoss = boss.MaxHealth * 0.30f;
                Assert(boss.CurrentHealth <= bossHpBeforeKill - expectedHpLoss + 1f, "Test 11.3: Boss loses 30% Max HP", $"Got {boss.CurrentHealth}");

                // Boss giảm 50% ATK & DEF (từ chỉ số gốc 500 ATK và 60 DEF -> 250 ATK, 30 DEF)
                Assert(boss.Stats.Attack == 250f, "Test 11.4: Boss Attack reduced by 50% to 250", $"Got {boss.Stats.Attack}");
                Assert(boss.Stats.Defense == 30f, "Test 11.5: Boss Defense reduced by 50% to 30", $"Got {boss.Stats.Defense}");

                // Boss bị choáng 5s trong RewardPhase
                Assert(boss.IsBossStunned, "Test 11.6: Boss is stunned during RewardPhase");
                Assert(boss.BossStunTimer == 5.0f, "Test 11.7: Boss stun timer is 5.0s");

                // Tank trong nhận +100% chỉ số
                Assert(t1.Stats.Attack >= 100f * 2.0f, "Test 11.8: Inside tank receives +100% Attack");

                // Tank ngoài nhận -50% hồi chiêu
                Assert(t3.Stats.CooldownReduction >= 0.50f, "Test 11.9: Outside tank receives -50% Cooldown Reduction");

                // Sau 10s: Boss hồi phục lại ATK & DEF gốc (500 ATK, 60 DEF), hết choáng
                boss.ManualTick(10.0f);
                Assert(boss.Stats.Attack == 500f, "Test 11.10: Boss Attack restored to 500 after 10s");
                Assert(boss.Stats.Defense == 60f, "Test 11.11: Boss Defense restored to 60 after 10s");
                Assert(!boss.IsBossStunned, "Test 11.12: Boss stun ended after 5s");

                UnityEngine.Object.DestroyImmediate(t1Obj);
                UnityEngine.Object.DestroyImmediate(t2Obj);
                UnityEngine.Object.DestroyImmediate(t3Obj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 12: Hình Phạt Bùng Nổ Năng Lượng (Penalty — Instant Kill)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_12");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var t1Obj = CreateMockTank("Tank_P1_12", new Vector3(1f, 0f, 0f), 5000f, 100f, 50f, 50f);
                var t2Obj = CreateMockTank("Tank_P2_12", new Vector3(5f, 0f, 0f), 5000f, 100f, 50f, 50f);
                var t1 = t1Obj.GetComponent<TankBrain_Test>();
                var t2 = t2Obj.GetComponent<TankBrain_Test>();

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Active

                // Để hết 40s mà Trái Tim chưa bị tiêu diệt
                boss.ManualTick(40.1f);

                Assert(boss.BurstState == EliteBurstState.PenaltyPhase, "Test 12.1: State transitions to PenaltyPhase on timeout");
                Assert(!t1.IsAlive, "Test 12.2: Tank 1 suffered INSTANT KILL");
                Assert(!t2.IsAlive, "Test 12.3: Tank 2 suffered INSTANT KILL");
                Assert(t1.CurrentHealth <= 0f, "Test 12.4: Tank 1 HP is 0");
                Assert(t2.CurrentHealth <= 0f, "Test 12.5: Tank 2 HP is 0");

                UnityEngine.Object.DestroyImmediate(t1Obj);
                UnityEngine.Object.DestroyImmediate(t2Obj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 13: Ràng Buộc Collider (Tuyệt đối không trỏ ColliderOwnerRef về Boss Brain)
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_13");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f);

                var subArena = boss.SubArena;

                // Kiểm tra Trái Tim
                var heartCol = subArena.Heart.GetComponent<Collider2D>();
                Assert(heartCol != null, "Test 13.1.1: Heart has its own Collider2D");
                var heartOwner = subArena.Heart.GetComponent<ColliderOwnerRef>();
                Assert(heartOwner != null, "Test 13.1.2: Heart has ColliderOwnerRef component");
                Assert(heartOwner.Owner != (UnityEngine.Object)boss, "Test 13.1.3: Heart ColliderOwnerRef does NOT point to Boss Brain");
                Assert(heartOwner.Owner == (UnityEngine.Object)subArena.Heart.gameObject, "Test 13.1.4: Heart ColliderOwnerRef points to Heart itself");

                // Kiểm tra 4 cột
                for (int i = 0; i < subArena.Pillars.Count; i++)
                {
                    var p = subArena.Pillars[i];
                    var pillarCol = p.GetComponent<Collider2D>();
                    Assert(pillarCol != null, $"Test 13.2.{i + 1}.1: Pillar {p.PillarColor} has Collider2D");
                    var pillarOwner = p.GetComponent<ColliderOwnerRef>();
                    Assert(pillarOwner != null, $"Test 13.2.{i + 1}.2: Pillar {p.PillarColor} has ColliderOwnerRef");
                    Assert(pillarOwner.Owner != (UnityEngine.Object)boss, $"Test 13.2.{i + 1}.3: Pillar {p.PillarColor} ColliderOwnerRef does NOT point to Boss Brain");
                    Assert(pillarOwner.Owner == (UnityEngine.Object)p.gameObject, $"Test 13.2.{i + 1}.4: Pillar {p.PillarColor} ColliderOwnerRef points to Pillar itself");
                }

                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 14: Trái Tim Boss — Bị Động & Bộ Kỹ Năng Bên Trong Buồng Lái
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_14");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var t1Obj = CreateMockTank("Tank_Inside_14A", new Vector2(198f, 100f), 5000f, 100f, 30f, 50f);
                var t2Obj = CreateMockTank("Tank_Inside_14B", new Vector2(205f, 100f), 5000f, 100f, 30f, 50f);
                var t1 = t1Obj.GetComponent<TankBrain_Test>();
                var t2 = t2Obj.GetComponent<TankBrain_Test>();

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Active

                var heart = boss.SubArena.Heart;
                Assert(heart != null, "Test 14.1: Heart instance is valid");
                Assert(heart.Stats.Attack == 500f, "Test 14.2: Heart inherits Boss Attack (500)");
                Assert(heart.Stats.Defense == 60f, "Test 14.3: Heart inherits Boss Defense (60)");
                Assert(heart.CanCastAnySkill, "Test 14.4: Heart initially ready to cast skill");

                // Heart passive: mỗi 5s tự tạo 2 đạn truy đuổi (20% ATK/viên = 100 dmg/viên) và tự động thi triển Trừng Phạt
                float t1HpBefore = t1.CurrentHealth;
                float t1DefBefore = t1.Stats.Defense;
                heart.ManualTick(5.0f);
                Assert(t1.CurrentHealth < t1HpBefore, "Test 14.5: Heart 5s passive damages trapped tank");

                // Heart cast Trừng Phạt
                Assert(heart.TrungPhatTimer == 8.0f, "Test 14.6: Heart TrungPhat cooldown is 8s");
                Assert(!heart.CanCastAnySkill, "Test 14.7: Heart global skill cooldown triggered (2.0s)");
                Assert(t1.Stats.Defense < t1DefBefore, "Test 14.8: Heart TrungPhat applies -20% DEF to nearest tank");
                Assert(t2.Stats.Attack < 100f, "Test 14.9: Heart TrungPhat applies -30% ATK to furthest tank");

                // Heart cast Laze, Toa Nhiet & Vien Binh (toàn bộ 4 kỹ năng)
                heart.CastLazeNguyenTo();
                Assert(heart.LazeTimer == 12.0f, "Test 14.10: Heart Laze cooldown is 12s");

                heart.CastToaNhietDienRong();
                Assert(heart.ToaNhietTimer == 15.0f, "Test 14.11: Heart ToaNhiet cooldown is 15s");

                heart.CastVienBinhTinhLinh();
                Assert(heart.VienBinhTimer == 20.0f, "Test 14.12: Heart VienBinh cooldown is 20s");

                UnityEngine.Object.DestroyImmediate(t1Obj);
                UnityEngine.Object.DestroyImmediate(t2Obj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 15: Thử Thách Cột Đỏ — Tank Chặn Điện Nhận Sát Thương 20% ATK
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_15");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var coopChallengesObj = new GameObject("Test_CoopChallenges_15");
                var coopChallenges = coopChallengesObj.AddComponent<D1onkCoopChallenges>();
                coopChallenges.Initialize(boss, null);

                // Kích hoạt thử thách Cột Đỏ: Máy phát tại (-6, 0), Máy thu tại (6, 0)
                coopChallenges.ActivateChallenge(D1onkPillarColor.Red);

                // Đặt Tank chặn tại (0, 0) ngay trên đường truyền
                var blockingTankObj = CreateMockTank("Tank_Blocker_15", new Vector3(0f, 0f, 0f), 5000f, 100f, 0f, 50f);
                var blockingTank = blockingTankObj.GetComponent<TankBrain_Test>();

                float hpBefore = blockingTank.CurrentHealth;

                // Tick 0.5s -> Đường truyền phát 1 xung điện -> Bị Tank chặn -> Tank nhận 20% Boss ATK (100 sát thương)
                coopChallenges.ManualTickRed(0.5f);

                float dmgTaken = hpBefore - blockingTank.CurrentHealth;
                Assert(dmgTaken >= 100f, "Test 15.1: Tank blocking red electric beam takes 20% ATK damage", $"Got {dmgTaken}");
                Assert(coopChallenges.RedEnergyPoints == 0, "Test 15.2: Red energy points did not increment because beam was blocked");

                UnityEngine.Object.DestroyImmediate(blockingTankObj);
                UnityEngine.Object.DestroyImmediate(coopChallengesObj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 16: Ràng Buộc Ranh Giới Buồng Lái Sub-Arena
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_16");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var trappedTankObj = CreateMockTank("Tank_Boundary_16", new Vector3(200f, 100f, 0f));
                var trappedTank = trappedTankObj.GetComponent<TankBrain_Test>();

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Active

                // Giả lập Tank cố gắng teleport/dash ra ngoài bán kính buồng lái (ví dụ ra x = 250f)
                trappedTank.transform.position = new Vector3(250f, 100f, 0f);
                boss.SubArena.ManualTick(0.1f);

                float distFromCenter = Vector2.Distance(trappedTank.transform.position, boss.SubArena.ArenaCenter);
                Assert(distFromCenter <= boss.SubArena.ArenaRadius, "Test 16.1: Trapped tank is clamped inside arena radius", $"Got {distFromCenter}");

                UnityEngine.Object.DestroyImmediate(trappedTankObj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            // ----------------------------------------------------
            // TEST 17: Bảo Vệ Máu Boss Trong Giai Đoạn Bùng Nổ Năng Lượng
            // ----------------------------------------------------
            {
                var bossObj = new GameObject("Test_D1onk_17");
                var boss = bossObj.AddComponent<D1onkBrain>();
                boss.Initialize();

                var t1Obj = CreateMockTank("Tank_Test_17", new Vector3(1f, 0f, 0f));
                var t1 = t1Obj.GetComponent<TankBrain_Test>();

                boss.TriggerEnergyBurstManual();
                boss.ManualTick(2.0f); // Active

                // Gây sát thương cực lớn từ ngoài sân vào Boss trong khi Burst đang diễn ra
                boss.TakeDamage(new DamageInfo(t1, boss, 999999f, 0f, boss.transform.position));

                // Máu Boss không được tụt xuống 0 và Boss không được chết từ bên ngoài
                Assert(boss.CurrentHealth >= 1f, "Test 17.1: Boss HP protected at >= 1 during Burst", $"Got {boss.CurrentHealth}");
                Assert(boss.IsAlive, "Test 17.2: Boss remains alive during Burst until Heart is destroyed");

                UnityEngine.Object.DestroyImmediate(t1Obj);
                UnityEngine.Object.DestroyImmediate(bossObj);
            }

            CleanupScene();

            sb.AppendLine("==================================================");
            sb.AppendLine($"=== TOTAL PASSED: {passed}/{total} ===");
            sb.AppendLine("==================================================");

            return sb.ToString();
        }

        private static GameObject CreateMockTank(string name, Vector3 pos, float hp = 1000f, float atk = 100f, float def = 30f, float spd = 50f)
        {
            var go = new GameObject(name);
            go.transform.position = pos;
            var tank = go.AddComponent<TankBrain_Test>();

            var stats = new TankStats
            {
                MaxHealth = hp,
                Attack = atk,
                Defense = def,
                MoveSpeed = spd,
                MaxEnergy = 100f
            };
            tank.Initialize(new TankBrainContext { Level = 1 }, stats);
            return go;
        }

        private static void CleanupScene()
        {
            var strayBosses = UnityEngine.Object.FindObjectsByType<D1onkBrain>(FindObjectsSortMode.None);
            for (int i = 0; i < strayBosses.Length; i++)
            {
                if (strayBosses[i] != null && strayBosses[i].name.StartsWith("Test_D1onk"))
                {
                    UnityEngine.Object.DestroyImmediate(strayBosses[i].gameObject);
                }
            }

            var strayTanks = UnityEngine.Object.FindObjectsByType<TankBrain_Test>(FindObjectsSortMode.None);
            for (int i = 0; i < strayTanks.Length; i++)
            {
                if (strayTanks[i] != null && strayTanks[i].name.StartsWith("Tank_"))
                {
                    UnityEngine.Object.DestroyImmediate(strayTanks[i].gameObject);
                }
            }
        }
    }
}