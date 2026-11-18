using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DanceLearning.Tests
{
    public class DanceStudioTests
    {
        [Test]
        public void MainEnclosesXrOriginWithFloorAndFourWalls()
        {
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var studio = GameObject.Find("Dance Studio");
            Assert.That(studio, Is.Not.Null);
            var origin = GameObject.Find("XR Origin (VR)").transform;
            var localOrigin = studio.transform.InverseTransformPoint(origin.position);
            Assert.That(Mathf.Abs(localOrigin.x), Is.LessThan(6));
            Assert.That(Mathf.Abs(localOrigin.z), Is.LessThan(8));
            var floor = studio.transform.Find("Floor").GetComponent<BoxCollider>();
            Assert.That(floor.bounds.max.y, Is.EqualTo(origin.position.y).Within(0.001f));
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var hits = Physics.RaycastAll(origin.position + Vector3.up * 1.6f, direction, 20);
                Assert.That(hits.Any(hit => hit.transform.IsChildOf(studio.transform.Find("Walls"))), Is.True);
            }
        }

        [Test]
        public void StudioHasNoRuntimeScriptsCamerasOrExtraLightsAndAllMaterialsAreValid()
        {
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var studio = GameObject.Find("Dance Studio");
            Assert.That(studio, Is.Not.Null);
            Assert.That(studio.GetComponentsInChildren<MonoBehaviour>(), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<Camera>(), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<Light>(), Is.Empty);
            Assert.That(studio.transform.childCount, Is.EqualTo(2));
            foreach (var renderer in studio.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            }
        }
    }
}
