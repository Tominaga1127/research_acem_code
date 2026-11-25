using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DanceLearning.Editor
{
    /// <summary>編集中または再生中のシーンにあるカメラのワールド座標と回転を確認するウィンドウ。</summary>
    public sealed class CameraPoseWindow : EditorWindow
    {
        private const string MenuPath = "Dance Learning/Show Camera Poses";
        private Label poseLabel;

        /// <summary>メニューを選ぶたびに確認ウィンドウを開閉する。</summary>
        [MenuItem(MenuPath)]
        private static void ToggleWindow()
        {
            if (HasOpenInstances<CameraPoseWindow>())
                GetWindow<CameraPoseWindow>().Close();
            else
                GetWindow<CameraPoseWindow>("Camera Poses");
        }

        /// <summary>メニューにウィンドウの開閉状態を反映する。</summary>
        [MenuItem(MenuPath, true)]
        private static bool UpdateMenuCheck()
        {
            Menu.SetChecked(MenuPath, HasOpenInstances<CameraPoseWindow>());
            return true;
        }

        /// <summary>スクロール可能な一覧を作り、開いた時点の値を表示する。</summary>
        private void CreateGUI()
        {
            var scrollView = new ScrollView();
            scrollView.contentContainer.style.paddingLeft = 8;
            scrollView.contentContainer.style.paddingTop = 6;
            scrollView.style.flexGrow = 1;
            poseLabel = new Label { name = "camera-poses" };
            poseLabel.style.whiteSpace = WhiteSpace.Normal;
            scrollView.Add(poseLabel);
            rootVisualElement.Add(scrollView);
            RefreshPoses();
        }

        /// <summary>エディターの定期更新に合わせ、再生中の視点切り替えやHMDによる変化も表示する。</summary>
        private void OnInspectorUpdate()
        {
            if (poseLabel != null)
                RefreshPoses();
        }

        /// <summary>非アクティブなカメラも含め、名前とワールド座標系の値を表示する。</summary>
        private void RefreshPoses()
        {
            var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
            var poses = new List<string>(cameras.Length);
            // 各カメラをシーン名と階層パスで識別し、無効なカメラには状態を付記する。
            foreach (var camera in cameras)
            {
                var cameraTransform = camera.transform;
                var status = camera.isActiveAndEnabled ? "" : " (Inactive)";
                poses.Add(
                    $"{camera.gameObject.scene.name} / {GetHierarchyPath(cameraTransform)}{status}\n" +
                    $"World Position (XYZ)  {cameraTransform.position:F3}\n" +
                    $"World Rotation (XYZ °)  {cameraTransform.eulerAngles:F2}");
            }

            poses.Sort(StringComparer.Ordinal);
            poseLabel.text = poses.Count == 0 ? "No cameras in the open scenes." : string.Join("\n\n", poses);
        }

        /// <summary>同名のカメラを区別できるよう、親を順にたどって階層パスを組み立てる。</summary>
        private static string GetHierarchyPath(Transform cameraTransform)
        {
            var path = cameraTransform.name;
            for (var parent = cameraTransform.parent; parent != null; parent = parent.parent)
                path = parent.name + " / " + path;
            return path;
        }
    }
}
