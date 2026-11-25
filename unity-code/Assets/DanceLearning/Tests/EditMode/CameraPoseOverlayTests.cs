using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DanceLearning.Tests.EditMode
{
    /// <summary>カメラ表示メニューがウィンドウを開かず、Scene ビューの表示状態を切り替えることを検証する。</summary>
    public sealed class CameraPoseOverlayTests
    {
        private const string MenuPath = "Dance Learning/Show Camera Poses";
        private const string SessionKey = "DanceLearning.CameraPoseOverlay.Visible";

        /// <summary>メニュー選択で表示状態が反転し、再選択で元に戻ることを確認する。</summary>
        [Test]
        public void MenuTogglesVisibilityWithoutOpeningWindow()
        {
            var wasVisible = SessionState.GetBool(SessionKey, false);
            var openWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            try
            {
                Assert.That(EditorApplication.ExecuteMenuItem(MenuPath), Is.True);
                Assert.That(SessionState.GetBool(SessionKey, false), Is.EqualTo(!wasVisible));
                Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>(), Is.EquivalentTo(openWindows));

                Assert.That(EditorApplication.ExecuteMenuItem(MenuPath), Is.True);
                Assert.That(SessionState.GetBool(SessionKey, false), Is.EqualTo(wasVisible));
                Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>(), Is.EquivalentTo(openWindows));
            }
            finally
            {
                // 途中で失敗した場合も、テスト前の表示状態に戻す。
                if (SessionState.GetBool(SessionKey, false) != wasVisible)
                    EditorApplication.ExecuteMenuItem(MenuPath);
            }
        }
    }
}
