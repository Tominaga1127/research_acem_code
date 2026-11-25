using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DanceLearning.Editor
{
    /// <summary>編集中・再生中のカメラの位置と向きを Scene ビューに描画する。</summary>
    [InitializeOnLoad]
    internal static class CameraPoseOverlay
    {
        private const string MenuPath = "Dance Learning/Show Camera Poses";
        private const string SessionKey = "DanceLearning.CameraPoseOverlay.Visible";
        private static readonly Color ActiveCameraColor = new(0.22f, 0.9f, 1f, 1f);
        private static readonly Color InactiveCameraColor = new(0.22f, 0.9f, 1f, 0.35f);
        private static double nextRepaintTime;

        /// <summary>スクリプト再読み込み後も同じ Unity セッション内の表示状態を引き継ぐ。</summary>
        static CameraPoseOverlay()
        {
            if (SessionState.GetBool(SessionKey, false))
                SetVisible(true);
        }

        /// <summary>既存メニューから Scene ビューの表示を切り替える。</summary>
        [MenuItem(MenuPath)]
        private static void ToggleVisibility()
        {
            SetVisible(!SessionState.GetBool(SessionKey, false));
        }

        /// <summary>メニューのチェックマークに表示状態を反映する。</summary>
        [MenuItem(MenuPath, true)]
        private static bool UpdateMenuCheck()
        {
            Menu.SetChecked(MenuPath, SessionState.GetBool(SessionKey, false));
            return true;
        }

        /// <summary>表示中のみ描画と定期更新を登録し、切り替えはすぐに反映する。</summary>
        private static void SetVisible(bool visible)
        {
            SessionState.SetBool(SessionKey, visible);
            if (visible)
            {
                SceneView.duringSceneGui += DrawCameraPoses;
                EditorApplication.update += RepaintSceneViews;
            }
            else
            {
                SceneView.duringSceneGui -= DrawCameraPoses;
                EditorApplication.update -= RepaintSceneViews;
            }

            SceneView.RepaintAll();
        }

        /// <summary>操作中でないときも、再生や HMD 操作による姿勢の変化を約 0.1 秒ごとに反映する。</summary>
        private static void RepaintSceneViews()
        {
            var now = EditorApplication.timeSinceStartup;
            if (now < nextRepaintTime)
                return;

            nextRepaintTime = now + 0.1;
            SceneView.RepaintAll();
        }

        /// <summary>Scene ビューの再描画時に、現在のステージのカメラを点と矢印で示す。</summary>
        private static void DrawCameraPoses(SceneView sceneView)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var stage = StageUtility.GetCurrentStageHandle();
            var previousDepthTest = Handles.zTest;
            try
            {
                // 壁の裏側にあるカメラも確認できるようにし、描画後は元の深度設定に戻す。
                Handles.zTest = CompareFunction.Always;
                foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
                {
                    // 現在のステージで読み込まれているシーンのカメラだけを描画する。
                    if (camera == sceneView.camera || !camera.gameObject.scene.isLoaded || !stage.Contains(camera.gameObject))
                        continue;

                    var cameraTransform = camera.transform;
                    var position = cameraTransform.position;
                    using var drawingScope = new Handles.DrawingScope(
                        camera.isActiveAndEnabled ? ActiveCameraColor : InactiveCameraColor, Matrix4x4.identity);
                    var arrowSize = Mathf.Clamp(HandleUtility.GetHandleSize(position) * 0.7f, 0.15f, 1.5f);
                    Handles.SphereHandleCap(0, position, Quaternion.identity, arrowSize * 0.12f, EventType.Repaint);
                    Handles.ArrowHandleCap(0, position, cameraTransform.rotation, arrowSize, EventType.Repaint);
                }
            }
            finally
            {
                Handles.zTest = previousDepthTest;
            }
        }
    }
}
