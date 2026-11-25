using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DanceLearning.Tests.EditMode
{
    /// <summary>カメラ確認ウィンドウのメニュー操作と、階層下にあるカメラの表示を検証する。</summary>
    public sealed class CameraPoseWindowTests
    {
        private const string MenuPath = "Dance Learning/Show Camera Poses";

        [UnityTest]
        public IEnumerator MenuTogglesWindowAndDisplaysUpdatedWorldPoseOfInactiveCamera()
        {
            var parent = new GameObject("Camera Pose Test Rig");
            var camera = new GameObject("Camera Pose Test Camera").AddComponent<Camera>();
            camera.transform.SetParent(parent.transform, false);
            parent.transform.SetPositionAndRotation(new Vector3(3, 4, 5), Quaternion.Euler(0, 90, 0));
            camera.transform.localPosition = Vector3.forward;
            camera.gameObject.SetActive(false);
            try
            {
                FindWindow()?.Close();
                Assert.That(EditorApplication.ExecuteMenuItem(MenuPath), Is.True);
                yield return null;

                var window = FindWindow();
                Assert.That(window, Is.Not.Null);
                var label = window.rootVisualElement.Q<Label>("camera-poses");
                Assert.That(label, Is.Not.Null);
                Assert.That(label.text, Does.Contain("Camera Pose Test Rig / Camera Pose Test Camera (Inactive)"));
                Assert.That(label.text, Does.Contain(new Vector3(4, 4, 5).ToString("F3")));
                Assert.That(label.text, Does.Contain(parent.transform.eulerAngles.ToString("F2")));

                // 親の位置を変え、定期更新で一覧に反映されるまでエディターフレームを待つ。
                parent.transform.position = new Vector3(10, 11, 12);
                var expectedPosition = new Vector3(11, 11, 12).ToString("F3");
                var deadline = EditorApplication.timeSinceStartup + 3;
                while (!label.text.Contains(expectedPosition) && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(label.text, Does.Contain(expectedPosition));

                Assert.That(EditorApplication.ExecuteMenuItem(MenuPath), Is.True);
                Assert.That(FindWindow() == null, Is.True);
            }
            finally
            {
                FindWindow()?.Close();
                Object.DestroyImmediate(parent);
            }
        }

        /// <summary>メニューで開いたウィンドウを、エディター側のアセンブリ参照なしで探す。</summary>
        private static EditorWindow FindWindow()
        {
            return Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(window => window.GetType().FullName == "DanceLearning.Editor.CameraPoseWindow");
        }
    }
}
