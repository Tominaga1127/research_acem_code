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
    /// <summary>Mainを実際に再生して、ボタン、モーション、ユーザー視点の接続を確認する。</summary>
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
            var button = Object.FindAnyObjectByType<Button>();
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

            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position)
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits.FirstOrDefault().gameObject, Is.EqualTo(button.gameObject));
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
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

        /// <summary>目とカメラの中心が1mm以内にあるかを確認する。</summary>
        private static void AssertCameraAtEyes(Camera camera, Transform eyes)
        {
            Assert.That(Vector3.Distance(camera.transform.position, eyes.position), Is.LessThan(0.001f));
        }
    }
}
