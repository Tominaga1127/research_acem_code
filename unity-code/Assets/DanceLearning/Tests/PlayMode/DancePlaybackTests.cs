using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DanceLearning.Tests
{
    /// <summary>Mainの再生操作、鏡像、三つの視点を確認する。</summary>
    public sealed class DancePlaybackTests
    {
        /// <summary>操作前は静止し、ボタンを押すとモーションが進み、目の位置がカメラに反映される。</summary>
        [UnityTest]
        public IEnumerator MainPlaysRecordingFromButtonAndTracksEyes()
        {
            yield return SceneManager.LoadSceneAsync("Assets/DanceLearning/Scenes/Main.unity");
            var playback = Object.FindAnyObjectByType<DancePlayback>();
            Assert.That(playback, Is.Not.Null);
            // EditorはAnimatorなしでも姿勢を適用できるため、Playerに必要な構成を明示的に確認する。
            var avatar = playback.transform.GetChild(0);
            Assert.That(avatar.GetComponent<Animator>(), Is.Not.Null);
            var eyes = avatar.GetComponentsInChildren<Transform>().Single(item => item.name == "Eyes");
            var button = GameObject.Find("Play Recording").GetComponent<Button>();
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            var camera = cameras.Single(item => item.isActiveAndEnabled && item.CompareTag("MainCamera"));
            Assert.That(camera.GetComponent<TrackedPoseDriver>().trackingType,
                Is.EqualTo(TrackedPoseDriver.TrackingType.RotationAndPosition));
            Assert.That(playback.GetComponentsInChildren<MeshRenderer>(), Is.Not.Empty);

            Assert.That(button, Is.Not.Null);
            Assert.That(playback.IsPlaying, Is.False);
            var startPosition = eyes.position;
            var startRotation = eyes.rotation;
            var studio = GameObject.Find("Dance Studio").transform;
            var floor = studio.Find("Floor").GetComponent<BoxCollider>();
            Assert.That(startPosition.y - floor.bounds.max.y, Is.InRange(1.0f, 2.0f),
                "The starting pose should put the avatar's eyes above the studio floor.");
            yield return new WaitForSeconds(0.2f);
            Assert.That(Vector3.Distance(eyes.position, startPosition), Is.LessThan(0.001f));
            AssertCameraAtEyes(camera, eyes);

            ClickButton(button, camera);
            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(button.gameObject.activeSelf, Is.False);

            var deadline = Time.realtimeSinceStartup + 25;
            var moved = false;
            var eyeBounds = new Bounds(studio.InverseTransformPoint(startPosition), Vector3.zero);
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 5;
                // テスト中だけ時間を速め、各フレームの追従をクリップの最後まで確認する。
                while (playback.IsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    AssertCameraAtEyes(camera, eyes);
                    eyeBounds.Encapsulate(studio.InverseTransformPoint(eyes.position));
                    moved |= Vector3.Distance(startPosition, eyes.position) > 0.01f ||
                        Quaternion.Angle(startRotation, eyes.rotation) > 1;
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }

            Assert.That(moved, Is.True);
            Assert.That(eyeBounds.min.x >= -6 && eyeBounds.max.x <= 6 &&
                eyeBounds.min.y >= 0 && eyeBounds.max.y <= 4 &&
                eyeBounds.min.z >= -8 && eyeBounds.max.z <= 8, Is.True,
                $"The first-person view should stay inside the studio. Eye range: {eyeBounds.min:F3} to {eyeBounds.max:F3}.");
            Assert.That(playback.IsPlaying, Is.False);
            Assert.That(button.gameObject.activeSelf, Is.True);
            button.onClick.Invoke();
            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(Vector3.Distance(eyes.position, startPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(eyes.rotation, startRotation), Is.LessThan(0.1f));
        }

        /// <summary>再生中に視点を切り替え、各視点の位置と回転が正しく切り替わることを確認する。</summary>
        [UnityTest]
        public IEnumerator MainSwitchesViewpointDuringPlayback()
        {
            yield return SceneManager.LoadSceneAsync("Assets/DanceLearning/Scenes/Main.unity");
            var playback = Object.FindAnyObjectByType<DancePlayback>();
            var eyes = playback.transform.GetChild(0).GetComponentsInChildren<Transform>()
                .Single(item => item.name == "Eyes");
            var camera = Camera.main;
            var playButton = GameObject.Find("Play Recording").GetComponent<Button>();
            var viewpointButton = GameObject.Find("Switch Viewpoint").GetComponent<Button>();
            var label = viewpointButton.GetComponentInChildren<Text>();
            Assert.That(label.text, Is.EqualTo("First Person  (B)"));

            // 実機の代わりに追跡ドライバーが書き込むローカル姿勢を再現する。
            camera.GetComponent<TrackedPoseDriver>().enabled = false;
            var hmdRotation = Quaternion.Euler(17, 42, -11);
            camera.transform.SetLocalPositionAndRotation(new Vector3(0.2f, 1.6f, -0.3f), hmdRotation);

            // 初期フレームの姿勢追従とCanvasの描画が済んでからクリックする。
            eyes.rotation = Quaternion.Euler(25, 60, -30);
            yield return new WaitForSeconds(0.1f);
            AssertFirstPersonPose(camera, eyes, hmdRotation);
            ClickButton(viewpointButton, camera);
            var initialPosition = camera.transform.position;
            var initialRotation = camera.transform.rotation;
            // 頭の位置を保ったまま大きく回転させ、三人称の位置・向きが変わらないことを確かめる。
            eyes.parent.rotation = Quaternion.Euler(45, 170, 25) * eyes.parent.rotation;
            yield return null;
            Assert.That(Vector3.Distance(camera.transform.position, initialPosition), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, initialRotation), Is.LessThan(0.1f));
            ClickButton(viewpointButton, camera);
            Assert.That(label.text, Is.EqualTo("Mirror  (B)"));
            ClickButton(viewpointButton, camera);

            ClickButton(playButton, camera);
            yield return null;
            ClickButton(viewpointButton, camera);
            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(label.text, Is.EqualTo("Third Person  (B)"));
            AssertThirdPersonPose(camera, eyes, playback.transform.forward, 8f);

            var cameraPosition = camera.transform.position;
            hmdRotation = Quaternion.Euler(2, -3, 1);
            camera.transform.SetLocalPositionAndRotation(new Vector3(-0.1f, 1.7f, 0.25f), hmdRotation);
            yield return new WaitForSeconds(0.3f);
            Assert.That(Vector3.Distance(camera.transform.position, cameraPosition), Is.GreaterThan(0.001f));
            AssertThirdPersonPose(camera, eyes, playback.transform.forward, Quaternion.Angle(Quaternion.identity, hmdRotation));

            var eyesBeforeSwitch = eyes.position;
            ClickButton(viewpointButton, camera);
            Assert.That(playback.IsPlaying, Is.True);
            Assert.That(label.text, Is.EqualTo("Mirror  (B)"));
            ClickButton(viewpointButton, camera);
            Assert.That(label.text, Is.EqualTo("First Person  (B)"));
            Assert.That(eyes.position, Is.EqualTo(eyesBeforeSwitch));
            AssertFirstPersonPose(camera, eyes, hmdRotation);
        }

        /// <summary>収録全体でアバターの表示範囲が画面内に収まることを確認する。</summary>
        [UnityTest]
        public IEnumerator ThirdPersonKeepsAvatarInFrameThroughoutRecording()
        {
            yield return SceneManager.LoadSceneAsync("Assets/DanceLearning/Scenes/Main.unity");
            var playback = Object.FindAnyObjectByType<DancePlayback>();
            var camera = Camera.main;
            camera.GetComponent<TrackedPoseDriver>().enabled = false;
            var renderers = playback.transform.GetChild(0).GetComponentsInChildren<MeshRenderer>();
            playback.ToggleViewpoint();
            playback.PlayFromStart();

            var deadline = Time.realtimeSinceStartup + 25;
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 5;
                // 大きく見回すHMD入力も与え、許容範囲の端でアバターが収まることを確認する。
                while (playback.IsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    camera.transform.localRotation = Time.frameCount % 2 == 0
                        ? Quaternion.Euler(65, 0, 0)
                        : Quaternion.Euler(-65, 0, 0);
                    yield return null;
                    AssertAvatarInFrame(camera, renderers);
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }

            Assert.That(playback.IsPlaying, Is.False);
        }

        /// <summary>鏡面と枠の寸法、および枠から天井までを覆う白い壁を確認する。</summary>
        [UnityTest]
        public IEnumerator MirrorMarkerUsesStudioCenterAndAvatarHeight()
        {
            yield return SceneManager.LoadSceneAsync("Assets/DanceLearning/Scenes/Main.unity");
            var studio = GameObject.Find("Dance Studio").transform;
            var floor = studio.Find("Floor").GetComponent<BoxCollider>().bounds;
            var leftWall = studio.Find("Walls/Left Wall").GetComponent<BoxCollider>().bounds;
            var rightWall = studio.Find("Walls/Right Wall").GetComponent<BoxCollider>().bounds;
            var playback = Object.FindAnyObjectByType<DancePlayback>();
            var plane = playback.transform.Find("Mirror Plane");
            var bottom = plane.Find("Floor Marker").GetComponent<MeshRenderer>().bounds;
            var top = plane.Find("Top Marker").GetComponent<MeshRenderer>().bounds;
            var center = new Vector3(floor.center.x, floor.max.y, floor.center.z);
            Assert.That(Vector3.Distance(plane.position, center), Is.LessThan(0.001f));
            Assert.That(bottom.min.x, Is.EqualTo(leftWall.max.x).Within(0.001f));
            Assert.That(bottom.max.x, Is.EqualTo(rightWall.min.x).Within(0.001f));
            Assert.That(bottom.min.y, Is.EqualTo(floor.max.y).Within(0.001f));
            var upperWall = plane.Find("Wall Above Mirror").GetComponent<MeshRenderer>();
            var frontWall = studio.Find("Walls/Front Wall").GetComponent<MeshRenderer>();
            var ceiling = studio.Find("Ceiling").GetComponent<BoxCollider>().bounds;
            Assert.That(upperWall.sharedMaterial, Is.SameAs(frontWall.sharedMaterial));
            Assert.That(upperWall.bounds.min.y, Is.EqualTo(top.max.y).Within(0.001f));
            Assert.That(upperWall.bounds.max.y, Is.EqualTo(ceiling.min.y).Within(0.001f));
            Assert.That(upperWall.bounds.min.x, Is.EqualTo(leftWall.max.x).Within(0.001f));
            Assert.That(upperWall.bounds.max.x, Is.EqualTo(rightWall.min.x).Within(0.001f));

#if UNITY_EDITOR
            // 未再生のプレハブを独立に測り、枠の外寸に30 cmが加わっていることを確認する。
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/DanceLearning/Prefabs/Avatars/Mocopi Avatar.prefab");
            var model = Object.Instantiate(prefab);
            var modelRenderers = model.GetComponentsInChildren<MeshRenderer>();
            var modelHeight = modelRenderers.Max(item => item.bounds.max.y) -
                modelRenderers.Min(item => item.bounds.min.y);
            Object.Destroy(model);
            Assert.That(top.max.y - bottom.min.y, Is.EqualTo(modelHeight + 0.30f).Within(0.001f));
#endif
        }

        /// <summary>鏡像の移動と骨の向き、頭の位置とHMD姿勢から決まる鏡視点を確認する。</summary>
        [UnityTest]
        public IEnumerator MirrorReflectsRecordingAndFollowsHeadWithoutItsRotation()
        {
            yield return SceneManager.LoadSceneAsync("Assets/DanceLearning/Scenes/Main.unity");
            var playback = Object.FindAnyObjectByType<DancePlayback>();
            var source = playback.transform.GetChild(0);
            var plane = playback.transform.Find("Mirror Plane");
            var reflected = plane.Find("Reflection Space/Reflected Playback Space/Reflected Avatar");
            Assert.That(plane.Find("Floor Marker").GetComponent<MeshRenderer>().enabled, Is.True);
            var sourceBones = source.GetComponentsInChildren<Transform>();
            var reflectedBones = reflected.GetComponentsInChildren<Transform>();
            var head = sourceBones.Single(bone => bone.name == "Eyes").parent;
            var depth = plane.InverseTransformPoint(head.position).z;
            Assert.That(depth, Is.LessThan(0));

            var camera = Camera.main;
            camera.GetComponent<TrackedPoseDriver>().enabled = false;
            var hmdRotation = Quaternion.Euler(16, -27, 19);
            camera.transform.SetLocalPositionAndRotation(new Vector3(0.2f, 1.6f, 0.3f), hmdRotation);
            // XR Originが初期化時の床オフセットを書き終えてから視点を切り替える。
            yield return new WaitForSeconds(0.1f);
            playback.ToggleViewpoint();
            playback.ToggleViewpoint();
            playback.PlayFromStart();
            var start = head.position;
            var maxHeadDepth = float.NegativeInfinity;
            var moved = false;
            var deadline = Time.realtimeSinceStartup + 25f;
            var originalTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 5;
                // 全収録にわたり、各ボーンの位置と向きが鏡面に対して対称であることを確認する。
                while (playback.IsPlaying && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    for (var index = 0; index < sourceBones.Length; index++)
                    {
                        var bone = sourceBones[index];
                        var mirrorBone = reflectedBones[index];
                        var point = plane.InverseTransformPoint(bone.position);
                        point.z = -point.z;
                        Assert.That(Vector3.Distance(plane.TransformPoint(point), mirrorBone.position),
                            Is.LessThan(0.001f), bone.name);
                        var axis = plane.InverseTransformDirection(bone.localToWorldMatrix.MultiplyVector(Vector3.forward));
                        axis.z = -axis.z;
                        var mirrorAxis = mirrorBone.localToWorldMatrix.MultiplyVector(Vector3.forward);
                        Assert.That(Vector3.Distance(plane.TransformDirection(axis), mirrorAxis), Is.LessThan(0.001f),
                            bone.name);
                    }

                    var headInPlane = plane.InverseTransformPoint(head.position);
                    maxHeadDepth = Mathf.Max(maxHeadDepth, headInPlane.z);
                    moved |= Vector3.Distance(start, head.position) > 0.01f;
                    headInPlane.z = depth;
                    var target = plane.TransformPoint(headInPlane);
                    Assert.That(Vector3.Distance(camera.transform.position, target), Is.LessThan(0.001f),
                        $"Camera: {camera.transform.position:F4}, target: {target:F4}, frame: {Time.frameCount}");
                    Assert.That(Quaternion.Angle(camera.transform.rotation, plane.rotation * hmdRotation),
                        Is.LessThan(0.1f));
                }
            }
            finally
            {
                Time.timeScale = originalTimeScale;
            }

            Assert.That(playback.IsPlaying, Is.False);
            Assert.That(moved, Is.True);
            Assert.That(maxHeadDepth, Is.LessThan(0f), "The recording should stay in front of the mirror.");
            var cameraPosition = camera.transform.position;
            head.rotation = Quaternion.Euler(45, 100, -32);
            yield return null;
            Assert.That(Vector3.Distance(camera.transform.position, cameraPosition), Is.LessThan(0.001f));
            head.position += plane.right * 0.3f + plane.up * 0.2f + plane.forward * 0.4f;
            yield return null;
            var expected = cameraPosition + plane.right * 0.3f + plane.up * 0.2f;
            Assert.That(Vector3.Distance(camera.transform.position, expected), Is.LessThan(0.001f));
        }

        /// <summary>Gameビュー上でボタンにヒットすることを確かめ、クリックを送る。</summary>
        private static void ClickButton(Button button, Camera camera)
        {
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.FirstOrDefault().gameObject, Is.EqualTo(button.gameObject));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        /// <summary>三人称の固定オフセットと、画角内でのHMD回転量を確認する。</summary>
        private static void AssertThirdPersonPose(Camera camera, Transform eyes, Vector3 heading, float lookAngle)
        {
            var target = eyes.parent.position - heading * 2.8f + Vector3.up * 0.25f;
            var framingRotation = Quaternion.LookRotation(eyes.parent.position - Vector3.up * 0.75f - target);
            Assert.That(Vector3.Distance(camera.transform.position, target), Is.LessThan(0.001f));
            Assert.That(Quaternion.Angle(camera.transform.rotation, framingRotation), Is.EqualTo(lookAngle).Within(0.1f));
        }

        /// <summary>各メッシュを囲むボックスの8頂点を画面座標に変換して、表示範囲を確認する。</summary>
        private static void AssertAvatarInFrame(Camera camera, MeshRenderer[] renderers)
        {
            foreach (var renderer in renderers)
            {
                var bounds = renderer.bounds;
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    var viewport = camera.WorldToViewportPoint(point);
                    if (viewport.z < camera.nearClipPlane || viewport.x < 0 || viewport.x > 1 ||
                        viewport.y < 0 || viewport.y > 1)
                    {
                        Assert.Fail($"{renderer.name} is outside the viewport at frame {Time.frameCount}: {viewport}.");
                    }
                }
            }
        }

        /// <summary>目の位置に固定し、ピッチ・ロールを含む目の回転にHMDの回転を重ねることを確認する。</summary>
        private static void AssertFirstPersonPose(Camera camera, Transform eyes, Quaternion hmdRotation)
        {
            AssertCameraAtEyes(camera, eyes);
            var targetRotation = eyes.rotation * hmdRotation;
            Assert.That(Quaternion.Angle(camera.transform.rotation, targetRotation), Is.LessThan(0.1f));
        }

        /// <summary>目とカメラの中心が1mm以内にあるかを確認する。</summary>
        private static void AssertCameraAtEyes(Camera camera, Transform eyes)
        {
            Assert.That(Vector3.Distance(camera.transform.position, eyes.position), Is.LessThan(0.001f));
        }
    }
}
