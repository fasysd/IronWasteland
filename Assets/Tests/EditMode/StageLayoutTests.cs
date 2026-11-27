using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace IronWasteland.Tests
{
    public sealed class StageLayoutTests
    {
        [TestCase("1_2", false)]
        [TestCase("1_3", true)]
        [TestCase("1_4", true)]
        [TestCase("2_1", true)]
        [TestCase("2_2", false)]
        [TestCase("2_3", true)]
        [TestCase("2_4", true)]
        [TestCase("2_5", false)]
        public void Stage_gate_matches_the_mission_format(string stage, bool hasGate)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/Scenes/Stage_{stage}.unity");
            try
            {
                var gates = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                    .Where(component => component != null && component.GetType().FullName ==
                        "IronWasteland.Stages.StageWaveGate").ToArray();
                Assert.That(gates.Length, Is.EqualTo(hasGate ? 1 : 0));
                if (hasGate)
                    Assert.That(gates[0].GetComponent<BoxCollider2D>().isTrigger, Is.True);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("1_2")]
        [TestCase("1_3")]
        [TestCase("1_4")]
        [TestCase("2_1")]
        [TestCase("2_2")]
        [TestCase("2_3")]
        [TestCase("2_4")]
        [TestCase("2_5")]
        public void Stage_has_room_to_drive_and_safe_southern_entry_before_play(string stage)
        {
            var scene = EditorSceneManager.OpenPreviewScene($"Assets/Scenes/Stage_{stage}.unity");
            try
            {
                var roots = scene.GetRootGameObjects();
                var bounds = roots.Single(root => root.name == "Background")
                    .GetComponent<SpriteRenderer>().bounds;
                var north = roots.SingleOrDefault(root => root.name == "Background_North");
                Assert.That(north, Is.Not.Null, "The northern playable area has its own background in the scene.");
                var northBounds = north.GetComponent<SpriteRenderer>().bounds;
                Assert.That(northBounds.min.y, Is.EqualTo(bounds.max.y).Within(.02f), "The backgrounds must meet.");
                Assert.That(northBounds.min.x, Is.EqualTo(bounds.min.x).Within(.02f));
                Assert.That(northBounds.max.x, Is.EqualTo(bounds.max.x).Within(.02f));
                var controller = roots.SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>())
                    .Single(component => component != null && component.GetType().FullName ==
                        "IronWasteland.Tanks.GameController");
                var terrain = roots.SelectMany(root => root.GetComponentsInChildren<Collider2D>())
                    .Where(collider => collider.enabled && !collider.isTrigger &&
                        collider.gameObject.layer == LayerMask.NameToLayer("Terrain"))
                    .ToArray();

                Assert.That(bounds.size.x, Is.GreaterThanOrEqualTo(55f), "Playable background width");
                Assert.That(bounds.size.y, Is.GreaterThanOrEqualTo(31f), "Playable background height");
                Assert.That(terrain.Length, Is.GreaterThanOrEqualTo(2), "Solid terrain visible before Play");

                var type = controller.GetType();
                var solo = (Vector2)type.GetProperty("SpawnPosition").GetValue(controller);
                var coop = (System.Collections.Generic.IReadOnlyList<Transform>)type
                    .GetProperty("CoopSpawnPoints").GetValue(controller);
                Assert.That(coop, Is.Not.Null);
                Assert.That(coop.Count, Is.EqualTo(4));
                var positions = coop.Select(point => (Vector2)point.position).ToArray();
                Assert.That(positions.Distinct().Count(), Is.EqualTo(4));
                foreach (var position in positions.Append(solo))
                {
                    Assert.That(position.y, Is.InRange(bounds.min.y + 2f, bounds.min.y + 5f));
                    Assert.That(position.x, Is.InRange(bounds.min.x + 2f, bounds.max.x - 2f));
                    Assert.That(terrain.Any(collider => collider.OverlapPoint(position)), Is.False);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
