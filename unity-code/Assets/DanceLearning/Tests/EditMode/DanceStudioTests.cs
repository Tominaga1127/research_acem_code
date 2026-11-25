using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DanceLearning.Tests
{
    /// <summary>
    /// Main.unityのスタジオ配置、マテリアル、アバター設定を検証する、Unity Test FrameworkのEditModeテスト。
    /// </summary>
    public class DanceStudioTests
    {
        /// <summary>Mainのアバターが独立したプレハブであり、収録クリップを適用できることを確認する。</summary>
        [Test]
        public void MainUsesIndependentAvatarPrefabWithCompatibleRecording()
        {
            const string AvatarPath = "Assets/DanceLearning/Prefabs/Avatars/Mocopi Avatar.prefab";
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var session = GameObject.Find("Dance Playback");
            Assert.That(session, Is.Not.Null);
            var playback = new SerializedObject(session.GetComponent("DancePlayback"));
            var avatar = (GameObject)playback.FindProperty("avatar").objectReferenceValue;
            var clip = (AnimationClip)playback.FindProperty("clip").objectReferenceValue;
            var eyes = (Transform)playback.FindProperty("eyes").objectReferenceValue;

            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(avatar), Is.EqualTo(AvatarPath));
            Assert.That(AssetDatabase.GetDependencies(AvatarPath).Any(path => path.EndsWith(".fbx")), Is.False);
            Assert.That(eyes.IsChildOf(avatar.transform), Is.True);
            Assert.That(clip, Is.Not.Null);
            var boneBindings = AnimationUtility.GetCurveBindings(clip)
                .Where(binding => binding.type == typeof(Transform) && binding.path.Length > 0).ToArray();
            Assert.That(boneBindings, Is.Not.Empty);
            // Genericクリップが参照する全ボーンのパスを、配置済みアバターの階層と照合する。
            foreach (var path in boneBindings.Select(binding => binding.path).Distinct())
                Assert.That(avatar.transform.Find(path), Is.Not.Null, $"Missing animated bone: {path}");
        }

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
            // スタジオ直下がFloor・Walls・Ceilingの3つであることを確認する。
            Assert.That(studio.transform.childCount, Is.EqualTo(3));
            // 床・壁・装飾の全MeshRendererを順に調べ、マテリアルと対応シェーダーを確認する。
            foreach (var renderer in studio.GetComponentsInChildren<MeshRenderer>())
            {
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
                Assert.That(renderer.sharedMaterial.shader.isSupported, Is.True);
            }
        }

        /// <summary>床メッシュの上面を実座標で測り、X・Z方向とも50 cmごとに明暗が交互に並ぶことを確認する。</summary>
        [Test]
        public void CheckerFloorUsesHalfMeterSquares()
        {
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var floor = GameObject.Find("Dance Studio").transform.Find("Floor");
            var material = floor.GetComponent<MeshRenderer>().sharedMaterial;
            Assert.That(material.name, Is.EqualTo("Checker Floor"));
            var scale = material.GetTextureScale("_BaseMap");
            var offset = material.GetTextureOffset("_BaseMap");
            var texture = (Texture2D)material.GetTexture("_BaseMap");
            Assert.That(texture.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(texture.anisoLevel, Is.GreaterThan(1));

            var probe = new GameObject("Floor UV Probe") { hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Repeat };
            try
            {
                // 床と同じメッシュでレイを受け、実際に描画に使う上面のUVを取得する。
                probe.transform.SetPositionAndRotation(floor.position, floor.rotation);
                probe.transform.localScale = floor.lossyScale;
                var mesh = probe.AddComponent<MeshCollider>();
                mesh.sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
                Physics.SyncTransforms();
                var center = TiledFloorUv(mesh, scale, offset, 0, 0);
                var xStep = TiledFloorUv(mesh, scale, offset, 0.5f, 0) - center;
                var zStep = TiledFloorUv(mesh, scale, offset, 0, 0.5f) - center;
                Assert.That(xStep.magnitude, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(zStep.magnitude, Is.EqualTo(0.5f).Within(0.0001f));
                Assert.That(Vector2.Dot(xStep, zStep), Is.EqualTo(0).Within(0.0001f));
                Assert.That(Mathf.Repeat(center.x + 0.001f, 0.5f), Is.EqualTo(0.001f).Within(0.0001f));
                Assert.That(Mathf.Repeat(center.y + 0.001f, 0.5f), Is.EqualTo(0.001f).Within(0.0001f));

                // 元画像から隣り合う4マスの中心を読み、対角線上だけ同じ色になることを確認する。
                Assert.That(pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(texture))), Is.True);
                var cell = center + (xStep + zStep) * 0.5f;
                var first = pixels.GetPixelBilinear(cell.x, cell.y).grayscale;
                var alongX = pixels.GetPixelBilinear(cell.x + xStep.x, cell.y + xStep.y).grayscale;
                var alongZ = pixels.GetPixelBilinear(cell.x + zStep.x, cell.y + zStep.y).grayscale;
                var diagonal = cell + xStep + zStep;
                Assert.That(Mathf.Abs(first - alongX), Is.GreaterThan(0.25f));
                Assert.That(alongX, Is.EqualTo(alongZ).Within(0.001f));
                Assert.That(pixels.GetPixelBilinear(diagonal.x, diagonal.y).grayscale, Is.EqualTo(first).Within(0.001f));
            }
            finally
            {
                // 検証用のメッシュと画像はシーンやアセットに残さない。
                Object.DestroyImmediate(probe);
                Object.DestroyImmediate(pixels);
            }
        }

        /// <summary>床中央からの実距離で上面を調べ、材質の繰り返しとオフセットを適用したUVを返す。</summary>
        private static Vector2 TiledFloorUv(MeshCollider mesh, Vector2 scale, Vector2 offset, float x, float z)
        {
            var origin = mesh.transform.position + mesh.transform.right * x + mesh.transform.forward * z + Vector3.up;
            Assert.That(mesh.Raycast(new Ray(origin, Vector3.down), out var hit, 2), Is.True);
            return Vector2.Scale(hit.textureCoord, scale) + offset;
        }

        /// <summary>床から4 mの位置に天井があり、四方の壁と室内中央・四隅の上方を覆うことを確認する。</summary>
        [Test]
        public void CeilingCoversRoomAtFourMetersAboveFloor()
        {
            EditorSceneManager.OpenScene("Assets/DanceLearning/Scenes/Main.unity");
            var studio = GameObject.Find("Dance Studio").transform;
            var ceiling = studio.Find("Ceiling");
            Assert.That(ceiling, Is.Not.Null);
            var collider = ceiling.GetComponent<BoxCollider>();
            var floorHeight = studio.Find("Floor").GetComponent<BoxCollider>().bounds.max.y;
            var ceilingHeight = collider.bounds.min.y;
            Assert.That(ceilingHeight - floorHeight, Is.EqualTo(4f).Within(0.001f));
            // 四方の壁を順番に調べ、下端が床、上端が天井に接していることを確認する。
            var walls = studio.Find("Walls");
            foreach (var wallName in new[] { "Front Wall", "Back Wall", "Left Wall", "Right Wall" })
            {
                var bounds = walls.Find(wallName).GetComponent<BoxCollider>().bounds;
                Assert.That(bounds.min.y, Is.EqualTo(floorHeight).Within(0.001f), wallName);
                Assert.That(bounds.max.y, Is.EqualTo(ceilingHeight).Within(0.001f), wallName);
            }
            // 中央と、壁のすぐ内側の四隅から真上へレイを飛ばし、天井に当たるかを順番に調べる。
            foreach (var point in new[]
            {
                Vector3.zero, new Vector3(-5.9f, 0, -7.9f), new Vector3(5.9f, 0, -7.9f),
                new Vector3(-5.9f, 0, 7.9f), new Vector3(5.9f, 0, 7.9f)
            })
            {
                var ray = new Ray(studio.TransformPoint(point + Vector3.up), Vector3.up);
                Assert.That(collider.Raycast(ray, out var hit, 5), Is.True);
                Assert.That(hit.distance, Is.EqualTo(3f).Within(0.001f));
            }
            // 天井が既存のDirectional Lightを遮らず、室内の明るさを保つ設定を確認する。
            Assert.That(ceiling.GetComponent<MeshRenderer>().shadowCastingMode,
                Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
        }
    }
}
