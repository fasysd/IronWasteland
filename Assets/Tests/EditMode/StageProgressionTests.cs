using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace IronWasteland.Tests
{
    public sealed class StageProgressionTests
    {
        [UnityTest]
        public IEnumerator Northern_areas_and_wave_gates_work_in_play_mode()
        {
            yield return new EnterPlayMode();
            yield return VerifyNorthernAreas();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Completing_objectives_early_cannot_skip_the_gate()
        {
            yield return new EnterPlayMode();
            yield return VerifyCannotClearBeforeCrossing();
            yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator Mones_waits_for_escort_takes_damage_and_reports_arrival()
        {
            yield return new EnterPlayMode();
            yield return VerifyMonesEscort();
            yield return new ExitPlayMode();
        }

        public static IEnumerator VerifyMonesEscort()
        {
            var load = SceneManager.LoadSceneAsync("Stage_1_3");
            while (!load.isDone || SceneManager.GetActiveScene().name != "Stage_1_3")
                yield return null;
            int loadedFrame = Time.frameCount;
            while (Time.frameCount <= loadedFrame + 1) yield return null;

            var escort = Find("Stages.EscortVehicle");
            var spawner = Find("Stages.EnemySpawner");
            var player = Property<Component>(Find("Tanks.GameController"), "PlayerTank");
            var radar = GameObject.Find("Radar_1").GetComponent(GameType("Stages.RadarStation"));
            var objectiveText = GameObject.Find("ObjectiveText").GetComponents<Component>()
                .Single(component => component.GetType().Name == "TextMeshProUGUI");
            Vector3 start = escort.transform.position;
            float oldScale = Time.timeScale;
            try
            {
                Time.timeScale = 5f;
                float until = Time.time + 1f;
                while (Time.time < until) yield return null;
                Assert.That(Vector3.Distance(escort.transform.position, start), Is.LessThan(.05f),
                    "Mones must wait for the first radar.");
                Assert.That(Property<int>(spawner, "WavesFired"), Is.Zero);
                Assert.That(Property<string>(objectiveText, "text"), Does.Contain("đứng yên 5 giây"));

                MoveAndSimulate(player, radar.transform.position);
                until = Time.time + .5f;
                while (Time.time < until) yield return null;
                Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1));
                Assert.That(Property<bool>(radar, "IsActivated"), Is.False);
                Assert.That(Property<string>(objectiveText, "text"), Does.Contain("Dọn quái quanh radar"));

                MoveAndSimulate(player, start);
                yield return null;
                MoveAndSimulate(player, radar.transform.position);
                yield return null;
                Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1));
                KillFirstWave(spawner, Find("Stages.StageWaveGate"), "1-3 radar");
                until = Time.time + 7f;
                while (!Property<bool>(radar, "IsActivated") && Time.time < until) yield return null;
                Assert.That(Property<bool>(radar, "IsActivated"), Is.True);

                MoveAndSimulate(player, escort.transform.position + 4f * Vector3.down);
                var wave = Property<IEnumerable>(spawner, "Waves").Cast<object>().First();
                var group = ((IEnumerable)wave.GetType().GetField("Spawns").GetValue(wave)).Cast<object>().First();
                var enemyPrefab = group.GetType().GetField("Enemy").GetValue(group);
                var enemy = (Component)Call(spawner, "Spawn", enemyPrefab,
                    (Vector2)escort.transform.position + Vector2.up * 2f, .01f);
                float health = Property<float>(escort, "Health01");
                until = Time.time + 3f;
                while (Property<float>(escort, "Health01") >= health && Time.time < until) yield return null;
                Assert.That(Property<float>(escort, "Health01"), Is.LessThan(health));
                Call(spawner, "Despawn", enemy);

                until = Time.time + 45f;
                while (!Property<bool>(escort, "HasArrived") && Time.time < until)
                {
                    MoveAndSimulate(player, escort.transform.position + 4f * Vector3.down);
                    yield return null;
                }
                Assert.That(Property<bool>(escort, "HasArrived"), Is.True);
                Assert.That(GameObject.Find("MonesExtraction"), Is.Not.Null);
                Assert.That(Property<string>(escort, "StatusLabel"), Is.EqualTo("ĐÃ TỚI ĐÍCH"));
            }
            finally
            {
                Time.timeScale = oldScale;
            }
        }

        public static IEnumerator VerifyNorthernAreas()
        {
            foreach (string code in new[] { "1_2", "1_3", "1_4", "2_1", "2_2", "2_3", "2_4", "2_5" })
            {
                string sceneName = $"Stage_{code}";
                var load = SceneManager.LoadSceneAsync(sceneName);
                while (!load.isDone || SceneManager.GetActiveScene().name != sceneName)
                    yield return null;
                int loadedFrame = Time.frameCount;
                while (Time.frameCount <= loadedFrame + 1) yield return null;

                Component spawner = Find("Stages.EnemySpawner");
                Component gate = (Component)Object.FindAnyObjectByType(GameType("Stages.StageWaveGate"));
                bool expectsGate = code is "1_3" or "1_4" or "2_1" or "2_3" or "2_4";
                Assert.That(gate != null, Is.EqualTo(expectsGate), $"{code} gate at runtime");
                Component tank = Property<Component>(Find("Tanks.GameController"), "PlayerTank");
                Rect bounds = Property<Rect>(Find("Core.StageBounds"), "Bounds");
                Assert.That(bounds.yMax, Is.EqualTo(47.25f).Within(.02f), code);
                var minimap = Find("UI.MinimapUI").GetComponentsInChildren<Image>(true);
                var southMap = minimap.Single(image => image.name == "StageBackground");
                var northMap = minimap.SingleOrDefault(image => image.name == "StageBackground_North");
                Assert.That(northMap, Is.Not.Null, $"Missing northern minimap image in {code}");
                Assert.That(southMap.rectTransform.anchorMax.y, Is.EqualTo(.5f).Within(.01f), code);
                Assert.That(northMap.rectTransform.anchorMin.y, Is.EqualTo(.5f).Within(.01f), code);

                VerifySpawnZones(spawner, gate, bounds, code);
                if (gate != null)
                {
                    if (code == "1_3")
                    {
                        Assert.That(Property<int>(spawner, "WavesFired"), Is.Zero, code);
                        MoveAndSimulate(tank, GameObject.Find("Radar_1").transform.position);
                        float waveDeadline = Time.time + 1f;
                        while (Property<int>(spawner, "WavesFired") == 0 && Time.time < waveDeadline)
                            yield return null;
                    }
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1), code);
                    Collider2D barrier = gate.GetComponentsInChildren<Collider2D>().Single(c => !c.isTrigger);
                    Assert.That(barrier.enabled, Is.True, code);
                    VerifyDivider(gate, code);
                    Call(spawner, "TickWaves", 180f);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1), code);
                    KillFirstWave(spawner, gate, code);
                    Assert.That(barrier.enabled, Is.False, code);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1), code);

                    var entrance = gate.GetComponent<BoxCollider2D>();
                    MoveAndSimulate(tank, gate.transform.position - 4f * Vector3.up);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1), code);
                    MoveAndSimulate(tank, entrance.bounds.center);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(2), code);
                    MoveAndSimulate(tank, gate.transform.position - 4f * Vector3.up);
                    MoveAndSimulate(tank, entrance.bounds.center);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(2), code);
                }
                else
                {
                    float secondWaveAt = code == "1_2" ? 40f : code == "2_2" ? 45f : 90f;
                    Call(spawner, "TickWaves", secondWaveAt - 1f);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(1), code);
                    Call(spawner, "TickWaves", 1f);
                    Assert.That(Property<int>(spawner, "WavesFired"), Is.EqualTo(2), code);
                }

                MoveAndSimulate(tank, new Vector2(0f, 24f));
                float cameraDeadline = Time.realtimeSinceStartup + 2f;
                while (Camera.main.transform.position.y <= 15.75f && Time.realtimeSinceStartup < cameraDeadline)
                    yield return null;
                Assert.That(tank.transform.position.y, Is.GreaterThan(15.75f), code);
                Assert.That(Camera.main.transform.position.y, Is.GreaterThan(15.75f), code);
                Assert.That(Call(Find("Enemies.Navigation.PathfindingGrid2D"), "FindPathToPoint",
                    new Vector2(0f, 16f), new Vector2(0f, 27f), Vector2.zero, 60f, .2f, .45f, 1f),
                    Is.Not.Null, code);
            }
        }

        public static IEnumerator VerifyCannotClearBeforeCrossing()
        {
            var load = SceneManager.LoadSceneAsync("Stage_1_4");
            while (!load.isDone || SceneManager.GetActiveScene().name != "Stage_1_4")
                yield return null;
            int loadedFrame = Time.frameCount;
            while (Time.frameCount <= loadedFrame + 1) yield return null;
            // This scenario reports synthetic mission progress. Keep the HUD from writing it to the save file.
            ((Behaviour)Find("UI.StageHUD")).enabled = false;
            var director = Find("Stages.StageDirector");
            var gate = Find("Stages.StageWaveGate");
            for (int i = 0; i < 3; i++) Call(director, "ReportTargetDestroyed", "core");
            Call(director, "Tick", 0f);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Running"));

            var spawner = Find("Stages.EnemySpawner");
            KillFirstWave(spawner, gate, "1-4");
            var player = Property<Component>(Find("Tanks.GameController"), "PlayerTank");
            MoveAndSimulate(player, gate.GetComponent<BoxCollider2D>().bounds.center);
            Call(director, "Tick", 0f);
            Assert.That(Property<object>(director, "State").ToString(), Is.EqualTo("Cleared"));
        }

        private static void VerifySpawnZones(Component spawner, Component gate, Rect bounds, string stage)
        {
            var zones = Property<IEnumerable>(spawner, "SpawnZones").Cast<Component>().ToArray();
            var terrain = LayerMask.GetMask("Terrain");
            int wave = 0;
            foreach (object entry in Property<IEnumerable>(spawner, "Waves"))
            {
                var groups = (IEnumerable)entry.GetType().GetField("Spawns").GetValue(entry);
                foreach (object group in groups)
                {
                    string id = (string)group.GetType().GetField("ZoneId").GetValue(group);
                    var candidates = string.IsNullOrWhiteSpace(id)
                        ? zones
                        : zones.Where(zone => Property<string>(zone, "ZoneId") == id).ToArray();
                    Assert.That(candidates.Length, Is.GreaterThan(0), $"{stage} wave {wave + 1}");
                    foreach (var zone in candidates)
                    {
                        var center = Property<Vector2>(zone, "Center");
                        float radius = Property<float>(zone, "Radius");
                        var margin = Vector2.one * radius;
                        Assert.That(bounds.Contains(center - margin) && bounds.Contains(center + margin), Is.True, stage);
                        Assert.That(Physics2D.OverlapCircle(center, radius, terrain), Is.Null, stage);
                        if (gate != null)
                        {
                            Assert.That(wave == 0 ? center.y + radius < gate.transform.position.y :
                                center.y - radius > gate.transform.position.y, Is.True, $"{stage} wave {wave + 1}");
                        }
                    }
                }
                wave++;
            }
        }

        private static void VerifyDivider(Component gate, string stage)
        {
            float y = gate.transform.position.y;
            int terrain = LayerMask.GetMask("Terrain");
            for (int x = -26; x <= 26; x++)
            {
                if (Mathf.Abs(x) < 6) continue;
                Assert.That(Physics2D.CircleCast(new Vector2(x, y - 3f), .8f, Vector2.up, 6f,
                    terrain).collider, Is.Not.Null, $"{stage} gap near wall X={x}");
            }
        }

        private static void KillFirstWave(Component spawner, Component gate, string stage)
        {
            var enemies = Property<IEnumerable>(spawner, "Active").Cast<Component>().ToArray();
            Assert.That(enemies.Length, Is.GreaterThan(1), stage);
            foreach (var enemy in enemies.Take(enemies.Length - 1)) Damage(enemy);
            Assert.That(Property<bool>(gate, "IsOpen"), Is.False, stage);
            Damage(enemies.Last());
            Assert.That(Property<bool>(gate, "IsOpen"), Is.True, stage);
        }

        private static void Damage(Component enemy)
        {
            enemy.GetType().GetMethod("TakeDamage", new[] { typeof(float) }).Invoke(enemy, new object[] { 1_000_000f });
        }

        private static void MoveAndSimulate(Component tank, Vector2 point)
        {
            var simulationMode = Physics2D.simulationMode;
            try
            {
                Physics2D.simulationMode = SimulationMode2D.Script;
                var body = Property<Rigidbody2D>(Property<object>(tank, "View"), "Body");
                tank.transform.position = point;
                body.position = point;
                body.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
                Physics2D.Simulate(Time.fixedDeltaTime);
            }
            finally
            {
                Physics2D.simulationMode = simulationMode;
            }
        }

        private static Type GameType(string name) => Type.GetType($"IronWasteland.{name}, Assembly-CSharp", true);
        private static Component Find(string name) => (Component)Object.FindAnyObjectByType(GameType(name));
        private static T Property<T>(object instance, string name) =>
            (T)instance.GetType().GetProperty(name).GetValue(instance);
        private static object Call(object instance, string method, params object[] args) =>
            instance.GetType().GetMethod(method).Invoke(instance, args);
    }
}
