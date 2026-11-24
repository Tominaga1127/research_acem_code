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
    /// <summary>Mainを実際に再生して、再生操作と一人称・三人称の視点切り替えを確認する。</summary>
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
            var eyes = playback.GetComponentsInChildren<Transform>().Single(item => item.name == "Eyes");
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
            var renderers = playback.GetComponentsInChildren<MeshRenderer>();
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
