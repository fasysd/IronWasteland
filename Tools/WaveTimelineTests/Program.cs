using System;
using System.Collections.Generic;
using IronWasteland.Stages;

namespace IronWasteland.Tests.Headless
{
    /// <summary>
    /// Test headless (không cần Unity) cho lịch chạy wave và layout vật cản.
    /// Chạy: dotnet run --project Tools/WaveTimelineTests
    /// Exit code 0 = tất cả PASS, 1 = có test FAIL.
    /// </summary>
    public static class Program
    {
        private static int s_Passed;
        private static int s_Failed;

        public static int Main()
        {
            Console.WriteLine("=== WaveTimeline ===");
            WaveBeforeStartTime_DoesNotFire();
            WaveAtExactStartTime_Fires();
            WavesFireInStartTimeOrder_NotListOrder();
        FiredCount_TracksStartedWaves();            EachWaveFiresExactlyOnce();
            TakeDue_ReturnsAllDueWavesInOrder();
            Reset_MakesWavesFireAgain();
            NegativeDeltaTime_DoesNotRewind();
            EmptyTimeline_IsFinishedImmediately();
            ZeroStartWave_FiresOnFirstTick();

            Console.WriteLine("=== ObstacleLayout ===");
            Layout_IsDeterministic();
            Layout_KeepsObstaclesInsideArenaBand();
            Layout_KeepsMinimumDistanceBetweenObstacles();
            Layout_KeepsCenterClear();
            Layout_CountMatchesRequest();
        Layout_AvoidsSpawnZones();

            Console.WriteLine();
            Console.WriteLine($"KET QUA: {s_Passed} PASS / {s_Failed} FAIL");
            return s_Failed == 0 ? 0 : 1;
        }

        // ---------------------------------------------------------------- WaveTimeline

        private static void WaveBeforeStartTime_DoesNotFire()
        {
            var timeline = new WaveTimeline(new[] { 10f });
            timeline.Advance(9.5f);

            Check("wave chưa tới StartTime thì không bắn", !timeline.TryTakeDue(out _));
        }

        private static void WaveAtExactStartTime_Fires()
        {
            var timeline = new WaveTimeline(new[] { 10f });
            timeline.Advance(10f);

            bool fired = timeline.TryTakeDue(out int index);
            Check("wave bắn đúng tại StartTime", fired && index == 0);
        }

        private static void FiredCount_TracksStartedWaves()
        {
            var timeline = new WaveTimeline(new[] { 5f, 1f, 20f });
            Check("mới tạo chưa bắn đợt nào", timeline.FiredCount == 0);

            timeline.Advance(10f);
            timeline.TakeDue(new List<int>());
            Check("bắn 2 đợt thì FiredCount = 2", timeline.FiredCount == 2);

            timeline.Reset();
            Check("Reset đưa FiredCount về 0", timeline.FiredCount == 0);
        }

        private static void WavesFireInStartTimeOrder_NotListOrder()        {
            // Danh sách cố tình để sai thứ tự: wave 20s đứng trước wave 5s.
            var timeline = new WaveTimeline(new[] { 20f, 5f });
            timeline.Advance(30f);

            var due = new List<int>();
            int count = timeline.TakeDue(due);

            Check("2 wave đều tới hạn", count == 2);
            Check("wave 5s bắn trước wave 20s dù nằm sau trong list",
                due.Count == 2 && due[0] == 1 && due[1] == 0);
        }

        private static void EachWaveFiresExactlyOnce()
        {
            var timeline = new WaveTimeline(new[] { 1f, 1f });
            timeline.Advance(5f);

            var due = new List<int>();
            timeline.TakeDue(due);
            timeline.Advance(5f);
            int again = timeline.TakeDue(due);

            Check("mỗi wave chỉ bắn 1 lần", due.Count == 2 && again == 0);
        }

        private static void TakeDue_ReturnsAllDueWavesInOrder()
        {
            var timeline = new WaveTimeline(new[] { 3f, 1f, 2f });
            timeline.Advance(3f);

            var due = new List<int>();
            timeline.TakeDue(due);

            Check("TakeDue trả đủ 3 wave theo thứ tự thời gian",
                due.Count == 3 && due[0] == 1 && due[1] == 2 && due[2] == 0);
        }

        private static void Reset_MakesWavesFireAgain()
        {
            var timeline = new WaveTimeline(new[] { 2f });
            timeline.Advance(2f);
            timeline.TakeDue(new List<int>());

            timeline.Reset();
            float elapsedAfterReset = timeline.Elapsed;
            bool before = timeline.TryTakeDue(out _);
            timeline.Advance(2f);
            bool after = timeline.TryTakeDue(out _);

            Check("Reset tua về 0 và wave bắn lại", !before && after);
            Check("Reset đưa Elapsed về 0", Math.Abs(elapsedAfterReset) < 0.0001f);
        }

        private static void NegativeDeltaTime_DoesNotRewind()
        {
            var timeline = new WaveTimeline(new[] { 5f });
            timeline.Advance(4f);
            timeline.Advance(-3f);

            Check("deltaTime âm không tua ngược thời gian", Math.Abs(timeline.Elapsed - 4f) < 0.0001f);
        }

        private static void EmptyTimeline_IsFinishedImmediately()
        {
            var timeline = new WaveTimeline(new float[0]);
            Check("lịch rỗng coi như xong ngay", timeline.IsFinished && timeline.Count == 0);
        }

        private static void ZeroStartWave_FiresOnFirstTick()
        {
            var timeline = new WaveTimeline(new[] { 0f });
            timeline.Advance(0f);

            Check("wave StartTime 0 bắn ngay tick đầu", timeline.TryTakeDue(out int index) && index == 0);
        }

        // ---------------------------------------------------------------- ObstacleLayout

        private static void Layout_IsDeterministic()
        {
            var a = ObstacleLayout.Build(seed: 7, count: 6);
            var b = ObstacleLayout.Build(seed: 7, count: 6);

            bool same = a.Count == b.Count;
            for (int i = 0; same && i < a.Count; i++)
            {
                same = Math.Abs(a[i].X - b[i].X) < 0.0001f
                    && Math.Abs(a[i].Y - b[i].Y) < 0.0001f
                    && Math.Abs(a[i].Radius - b[i].Radius) < 0.0001f;
            }

            Check("cùng seed sinh cùng layout", same);

            var c = ObstacleLayout.Build(seed: 8, count: 6);
            bool different = false;
            for (int i = 0; i < a.Count && i < c.Count; i++)
            {
                if (Math.Abs(a[i].X - c[i].X) > 0.0001f) different = true;
            }
            Check("seed khác sinh layout khác", different);
        }

        private static void Layout_KeepsObstaclesInsideArenaBand()
        {
            var layout = ObstacleLayout.Build(seed: 3, count: 8);

            bool inside = true;
            for (int i = 0; i < layout.Count; i++)
            {
                float distance = (float)Math.Sqrt(layout[i].X * layout[i].X + layout[i].Y * layout[i].Y);
                if (distance < ObstacleLayout.MinCenterDistance - 0.0001f) inside = false;
                if (distance > ObstacleLayout.MaxRingDistance + 0.0001f) inside = false;
            }

            Check("vật cản nằm trong vành cho phép (không sát tâm, không ra ngoài)", inside);
        }

        private static void Layout_KeepsMinimumDistanceBetweenObstacles()
        {
            var layout = ObstacleLayout.Build(seed: 11, count: 10, attemptsPerObstacle: 64);
            float minGap = float.MaxValue;

            for (int i = 0; i < layout.Count; i++)
            {
                for (int j = i + 1; j < layout.Count; j++)
                {
                    float dx = layout[i].X - layout[j].X;
                    float dy = layout[i].Y - layout[j].Y;
                    float gap = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (gap < minGap) minGap = gap;
                }
            }

            Check("vật cản không chồng lên nhau",
                layout.Count < 2 || minGap >= ObstacleLayout.MinSeparation - 0.0001f);
        }

        private static void Layout_KeepsCenterClear()
        {
            var layout = ObstacleLayout.Build(seed: 5, count: 8);
            bool clear = true;
            for (int i = 0; i < layout.Count; i++)
            {
                float distance = (float)Math.Sqrt(layout[i].X * layout[i].X + layout[i].Y * layout[i].Y);
                if (distance < ObstacleLayout.MinCenterDistance - 0.0001f) clear = false;
            }

            Check("chừa khoảng trống quanh điểm xuất phát của người chơi", clear);
        }

        private static void Layout_CountMatchesRequest()
        {
            Check("xin 6 vật cản nhận đủ 6", ObstacleLayout.Build(seed: 2, count: 6).Count == 6);
            Check("xin 0 vật cản nhận rỗng", ObstacleLayout.Build(seed: 2, count: 0).Count == 0);
        }

        private static void Layout_AvoidsSpawnZones()
        {
            // Hai vùng spawn như trong scene thật: (+-12, 0), bán kính 5.
            var zones = new List<ObstaclePlacement>
            {
                new ObstaclePlacement(12f, 0f, 5f),
                new ObstaclePlacement(-12f, 0f, 5f),
            };

            var layout = ObstacleLayout.Build(seed: 4, count: 10, attemptsPerObstacle: 64, keepOut: zones);

            bool clear = true;
            for (int i = 0; i < layout.Count; i++)
            {
                for (int z = 0; z < zones.Count; z++)
                {
                    float dx = layout[i].X - zones[z].X;
                    float dy = layout[i].Y - zones[z].Y;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (distance < zones[z].Radius + layout[i].Radius + ObstacleLayout.KeepOutPadding - 0.0001f)
                    {
                        clear = false;
                    }
                }
            }

            Check("vật cản không lấn vào vùng spawn quái", clear);
            Check("vẫn đủ số vật cản dù có vùng cấm", layout.Count == 10);
        }

        // ---------------------------------------------------------------- helpers

        private static void Check(string name, bool condition)
        {
            if (condition)
            {
                s_Passed++;
                Console.WriteLine("  PASS  " + name);
            }
            else
            {
                s_Failed++;
                Console.WriteLine("  FAIL  " + name);
            }
        }
    }
}
