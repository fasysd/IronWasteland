using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Utils;

namespace IronWasteland.Tests
{
    public sealed class StageNavigationTests
    {
        [UnityTest]
        public IEnumerator Tanks_and_pathfinding_can_use_the_expanded_playable_area()
        {
            yield return new EnterPlayMode();

            var gameplay = Type.GetType("IronWasteland.Tanks.GameController, Assembly-CSharp", true);
            var navigation = Type.GetType("IronWasteland.Enemies.Navigation.PathfindingGrid2D, Assembly-CSharp", true);
            var stageBounds = Type.GetType("IronWasteland.Core.StageBounds, Assembly-CSharp", true);
            var terrainMask = LayerMask.GetMask("Terrain");

            foreach (string stage in new[] { "1_2", "1_3", "1_4", "2_1", "2_2", "2_3", "2_4", "2_5" })
            {
                yield return SceneManager.LoadSceneAsync($"Stage_{stage}", LoadSceneMode.Single);
                yield return new WaitForFixedUpdate();

                var controller = UnityEngine.Object.FindAnyObjectByType(gameplay);
                Assert.That(controller, Is.Not.Null, stage);
                var tank = (Component)gameplay.GetProperty("PlayerTank").GetValue(controller);
                Assert.That(tank, Is.Not.Null, stage);
                Assert.That((Vector2)tank.transform.position,
                    Is.EqualTo(new Vector2(0f, -12.3f)).Using(Vector2ComparerWithEqualsOperator.Instance), stage);

                var boundary = UnityEngine.Object.FindAnyObjectByType(stageBounds);
                Assert.That(boundary, Is.Not.Null, $"Stage {stage} loaded from another scene");
                var bounds = (Rect)stageBounds.GetProperty("Bounds").GetValue(boundary);
                Assert.That(bounds.Contains(new Vector2(25.8f, 4f)), Is.True, stage);

                var grid = navigation.GetProperty("Instance").GetValue(null);
                foreach (float x in new[] { -25.8f, 25.8f })
                {
                    var start = new Vector2(x, -3f);
                    var end = new Vector2(x, 4f);
                    AssertWalkablePath(navigation, grid, start, end, terrainMask, stage);
                }

                if (stage == "1_2")
                {
                    var start = new Vector2(-14f, 0.8f);
                    var end = new Vector2(-7f, 0.8f);
                    Assert.That(Physics2D.Linecast(start, end, terrainMask).collider, Is.Not.Null,
                        "The direct route crosses a cave wall");
                    AssertWalkablePath(navigation, grid, start, end, terrainMask, stage);
                }

                if (stage == "1_3" || stage == "2_3")
                {
                    float y = stage == "1_3" ? -1f : -2f;
                    Assert.That(Physics2D.CircleCast(new Vector2(-15f, y), 1.6f, Vector2.right, 30f,
                        terrainMask).collider, Is.Null, $"Escort lane in stage {stage}");
                }
            }

            yield return new ExitPlayMode();
        }

        private static void AssertWalkablePath(Type navigation, object grid, Vector2 start, Vector2 end,
            int terrainMask, string stage)
        {
            var path = (System.Collections.Generic.List<Vector2>)navigation.GetMethod("FindPathToPoint")
                .Invoke(grid, new object[] { start, end, Vector2.zero, 60f, .2f, .45f, 1f });
            Assert.That(path, Is.Not.Null.And.Not.Empty, $"Stage {stage} from {start} to {end}");
            Assert.That((path[path.Count - 1] - end).magnitude, Is.LessThan(.45f), stage);
            var previous = start;
            foreach (var waypoint in path)
            {
                Assert.That(Physics2D.Linecast(previous, waypoint, terrainMask).collider, Is.Null, stage);
                previous = waypoint;
            }
        }
    }
}
