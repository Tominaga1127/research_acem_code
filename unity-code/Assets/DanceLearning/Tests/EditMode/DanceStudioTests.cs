using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DanceLearning.Tests
{
    /// <summary>
    /// Main.unityのスタジオ配置とマテリアルを検証する、Unity Test FrameworkのEditModeテスト。
    /// </summary>
    public class DanceStudioTests
    {
        /// <summary>XR Originが室内にあり、床の高さと四方向の壁の当たり判定が正しいことを確認する。</summary>
        [Test]
        public void MainEnclosesXrOriginWithFloorAndFourWalls()
        {
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var studio = GameObject.Find("Dance Studio");
            Assert.That(studio, Is.Not.Null);
            var origin = GameObject.Find("XR Origin (VR)").transform;
            var localOrigin = studio.transform.InverseTransformPoint(origin.position);
            // ワールド座標を部屋基準の座標に変換し、壁の内側にOriginが収まるかを確認する。
            Assert.That(Mathf.Abs(localOrigin.x), Is.LessThan(6));
            Assert.That(Mathf.Abs(localOrigin.z), Is.LessThan(8));
            var floor = studio.transform.Find("Floor").GetComponent<BoxCollider>();
            // 浮動小数点の誤差を許容し、床の上面とXR Originの高さを比較する。
            Assert.That(floor.bounds.max.y, Is.EqualTo(origin.position.y).Within(0.001f));
            // 前・後・左・右の順に、Originの1.6m上から20m先まで当たり判定を調べる。
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                var hits = Physics.RaycastAll(origin.position + Vector3.up * 1.6f, direction, 20);
                // 他の物体に当たっても、その方向にWalls配下の物体が1つ以上あれば成功とする。
                Assert.That(hits.Any(hit => hit.transform.IsChildOf(studio.transform.Find("Walls"))), Is.True);
            }
        }

        /// <summary>スタジオの構成と全描画部品のマテリアルを確認し、余分な実行時処理がないことを検証する。</summary>
        [Test]
        public void StudioHasNoRuntimeScriptsCamerasOrExtraLightsAndAllMaterialsAreValid()
        {
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var studio = GameObject.Find("Dance Studio");
            Assert.That(studio, Is.Not.Null);
            // 生成処理はエディター専用とし、スタジオ内にスクリプト・カメラ・ライトを残さない。
            Assert.That(studio.GetComponentsInChildren<MonoBehaviour>(), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<Camera>(), Is.Empty);
            Assert.That(studio.GetComponentsInChildren<Light>(), Is.Empty);
            // スタジオ直下がFloorとWallsの2つであることを確認する。
            Assert.That(studio.transform.childCount, Is.EqualTo(2));
            // 床・壁・装飾の全MeshRendererを順に調べ、マテリアルと対応シェーダーを確認する。
            foreach (var renderer in studio.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            }
        }
    }
}
