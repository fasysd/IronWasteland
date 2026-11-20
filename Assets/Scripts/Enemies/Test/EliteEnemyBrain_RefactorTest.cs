using System.Text;
using IronWasteland.Core;
using IronWasteland.Core.Effects;
using UnityEngine;

namespace IronWasteland.Enemies.Test
{
    public class MockEliteBoss : EliteEnemyBrain
    {
        public bool BurstStarted { get; private set; }
        public bool BurstSucceeded { get; private set; }
        public bool BurstFailed { get; private set; }
        public bool BurstEnded { get; private set; }
        public int BurstTickCount { get; private set; }

        public void SetupForTest(float hpThreshold = 0.5f, float chargeTime = 2f, float duration = 10f, float rewardDuration = 3f, float penaltyDuration = 3f, float atkMult = 1.25f, float defMult = 1.5f, float spdMult = 1.2f)
        {
            burstHpThreshold = hpThreshold;
            burstChargeTime = chargeTime;
            burstDuration = duration;
            rewardPhaseDuration = rewardDuration;
            penaltyPhaseDuration = penaltyDuration;
            burstAttackMultiplier = atkMult;
            burstDefenseMultiplier = defMult;
            burstMoveSpeedMultiplier = spdMult;

            var defaultStats = new EnemyStats
            {
                MaxHealth = 1000f,
                Attack = 100f,
                Defense = 50f,
                MoveSpeed = 10f
            };
            Initialize(defaultStats);
        }

        public void ManualTick(float deltaTime)
        {
            OnBrainTick(deltaTime);
        }

        protected override void OnBurstStart()
        {
            BurstStarted = true;
        }

        protected override void OnBurstTick(float deltaTime)
        {
            BurstTickCount++;
        }

        protected override void OnBurstSuccess()
        {
            BurstSucceeded = true;
        }

        protected override void OnBurstFail()
        {
            BurstFailed = true;
        }

        protected override void OnBurstEnded()
        {
            BurstEnded = true;
        }

        protected override void OnDeath()
        {
            base.OnDeath();
        }
    }

    public static class EliteEnemyBrain_RefactorTest
    {
        public static string RunAllTests()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ELITE ENEMY BRAIN REFACTOR TEST SUITE ===");

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

            // Test 1: Khởi tạo và Miễn nhiễm khống chế cứng (Stun / Root), nhưng nhận Slow
            {
                var go = new GameObject("TestBoss_1");
                var boss = go.AddComponent<MockEliteBoss>();
                boss.SetupForTest();

                Assert(boss.IsElite, "Test 1.1: IsElite is true");
                Assert(boss.IsImmuneToCrowdControl(CrowdControlType.Stun), "Test 1.2: Immune to Stun");
                Assert(boss.IsImmuneToCrowdControl(CrowdControlType.Root), "Test 1.3: Immune to Root");
                Assert(!boss.IsImmuneToCrowdControl(CrowdControlType.Slow), "Test 1.4: NOT immune to Slow");
                Assert(boss.BurstState == EliteBurstState.Inactive, "Test 1.5: Initial state is Inactive");

                Object.DestroyImmediate(go);
            }

            // Test 2: Kích hoạt đúng ngưỡng máu burstHpThreshold (50%)
            {
                var go = new GameObject("TestBoss_2");
                var boss = go.AddComponent<MockEliteBoss>();
                boss.SetupForTest(hpThreshold: 0.5f, chargeTime: 2f);
                boss.Stats.Defense = 0f; // Bỏ qua giảm trừ giáp để tính chính xác lượng máu trừ

                // Gây 400 damage (còn 600/1000 = 60% > 50%)
                boss.TakeDamage(new DamageInfo(null, boss, 400f, isDirectToHealth: true));
                Assert(boss.BurstState == EliteBurstState.Inactive, "Test 2.1: State remains Inactive above 50% HP");

                // Gây thêm 150 damage (còn 450/1000 = 45% <= 50%)
                boss.TakeDamage(new DamageInfo(null, boss, 150f, isDirectToHealth: true));
                Assert(boss.BurstState == EliteBurstState.Charging, "Test 2.2: State transitions to Charging when HP <= 50%");
                Assert(Mathf.Approximately(boss.BurstTimer, 2f), "Test 2.3: Charging timer set to 2s");

                Object.DestroyImmediate(go);
            }

            // Test 3: Vận sức Charging đếm ngược -> chuyển sang Active -> gọi OnBurstStart và nhân Stats
            {
                var go = new GameObject("TestBoss_3");
                var boss = go.AddComponent<MockEliteBoss>();
                boss.SetupForTest(hpThreshold: 0.5f, chargeTime: 2f, duration: 10f, atkMult: 1.25f, defMult: 1.5f, spdMult: 1.2f);
                boss.TriggerEnergyBurst();

                Assert(boss.BurstState == EliteBurstState.Charging, "Test 3.1: Starts in Charging");
                boss.ManualTick(1.0f);
                Assert(boss.BurstState == EliteBurstState.Charging, "Test 3.2: Still Charging after 1s");
                Assert(Mathf.Approximately(boss.BurstTimer, 1.0f), "Test 3.3: Remaining charge time is 1s");

                boss.ManualTick(1.05f);
                Assert(boss.BurstState == EliteBurstState.Active, "Test 3.4: Transitions to Active after 2s");
                Assert(boss.BurstStarted, "Test 3.5: OnBurstStart was called");
                Assert(Mathf.Approximately(boss.BurstTimer, 10f), "Test 3.6: BurstTimer initialized to burstDuration (10s)");
                Assert(Mathf.Approximately(boss.Stats.Attack, 125f), "Test 3.7: Attack multiplied by 1.25 (100 -> 125)");
                Assert(Mathf.Approximately(boss.Stats.Defense, 75f), "Test 3.8: Defense multiplied by 1.5 (50 -> 75)");
                Assert(Mathf.Approximately(boss.Stats.MoveSpeed, 12f), "Test 3.9: MoveSpeed multiplied by 1.2 (10 -> 12)");

                Object.DestroyImmediate(go);
            }

            // Test 4: Giải cơ chế thành công -> RewardPhase -> Phát OnEnergyBurstSuccess -> Finished phục hồi stats
            {
                var go = new GameObject("TestBoss_4");
                var boss = go.AddComponent<MockEliteBoss>();
                boss.SetupForTest(hpThreshold: 0.5f, chargeTime: 0f, duration: 10f, rewardDuration: 2f, atkMult: 1.5f);
                boss.TriggerEnergyBurst();

                Assert(boss.BurstState == EliteBurstState.Active, "Test 4.1: Instant active when chargeTime is 0");

                bool globalEventFired = false;
                EliteEnemyBrain eventArg = null;
                System.Action<EliteEnemyBrain> handler = (b) =>
                {
                    globalEventFired = true;
                    eventArg = b;
                };
                EliteEnemyBrain.OnEnergyBurstSuccess += handler;

                boss.CompleteBurstSuccess();
                EliteEnemyBrain.OnEnergyBurstSuccess -= handler;

                Assert(boss.BurstState == EliteBurstState.RewardPhase, "Test 4.2: Transitions to RewardPhase on success");
                Assert(boss.BurstSucceeded, "Test 4.3: OnBurstSuccess called");
                Assert(globalEventFired && eventArg == boss, "Test 4.4: Static OnEnergyBurstSuccess event fired with boss reference");

                boss.ManualTick(2.1f);
                Assert(boss.BurstState == EliteBurstState.Finished, "Test 4.5: Transitions to Finished after rewardDuration");
                Assert(boss.BurstEnded, "Test 4.6: OnBurstEnded called");
                Assert(Mathf.Approximately(boss.Stats.Attack, 100f), "Test 4.7: Attack reverted back to 100");

                Object.DestroyImmediate(go);
            }

            // Test 5: Hết thời gian Active -> PenaltyPhase -> OnBurstFail -> Finished
            {
                var go = new GameObject("TestBoss_5");
                var boss = go.AddComponent<MockEliteBoss>();
                boss.SetupForTest(hpThreshold: 0.5f, chargeTime: 0f, duration: 5f, penaltyDuration: 2f);
                boss.TriggerEnergyBurst();

                Assert(boss.BurstState == EliteBurstState.Active, "Test 5.1: Active state");
                boss.ManualTick(3.0f);
                Assert(boss.BurstState == EliteBurstState.Active, "Test 5.2: Active after 3s");
                Assert(boss.BurstTickCount > 0, "Test 5.3: OnBurstTick invoked");

                boss.ManualTick(2.1f);
                Assert(boss.BurstState == EliteBurstState.PenaltyPhase, "Test 5.4: Transitions to PenaltyPhase when duration expires");
                Assert(boss.BurstFailed, "Test 5.5: OnBurstFail called");

                boss.ManualTick(2.1f);
                Assert(boss.BurstState == EliteBurstState.Finished, "Test 5.6: Transitions to Finished after penalty duration");

                Object.DestroyImmediate(go);
            }

            sb.AppendLine($"=== RESULT: {passed}/{total} TESTS PASSED ===");
            return sb.ToString();
        }
    }
}
