using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace DanceLearning.Editor
{
    /// <summary>Mainにアバタープレハブを配置し、収録クリップと再生・視点・床面操作のUIを接続する。</summary>
    public static class DancePlaybackSceneBuilder
    {
        private const string ScenePath = "Assets/DanceLearning/Scenes/Main.unity";
        private const string AvatarPath = "Assets/DanceLearning/Prefabs/Avatars/Mocopi Avatar.prefab";
        private const string RecordingPath = "Assets/DanceLearning/Animations/MCPM_20261117_140210_refined.fbx";
        private const string MirrorMarkerMaterialPath = "Assets/DanceLearning/Materials/Studio/Dark Oak Trim.mat";
        private const string OakFloorMaterialPath = "Assets/DanceLearning/Materials/Studio/Oak Floor.mat";
        private const string SessionName = "Dance Playback";
        private const float ButtonHeight = 62;
        private const float ButtonRowOffset = 38;

        /// <summary>収録クリップ、シーンの既存XR Origin、操作ボタンを接続して保存する。</summary>
        [MenuItem("Dance Learning/Place Recording In Main")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // 開いているシーンに未保存の編集があれば、その編集を保持して終了する。
            if (scene.isDirty)
                throw new InvalidOperationException("Save the active scene before placing the dance recording.");
            if (scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath);
            if (GameObject.Find(SessionName) != null)
                throw new InvalidOperationException("Dance Playback is already in Main.");

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(AvatarPath);
            if (model == null)
                throw new InvalidOperationException("The default avatar prefab is missing.");
            var clip = AssetDatabase.LoadAllAssetsAtPath(RecordingPath).OfType<AnimationClip>()
                .Single(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
            var origin = GameObject.Find("XR Origin (VR)").GetComponent<XROrigin>();
            var camera = origin.Camera;
            var studio = GameObject.Find("Dance Studio");
            if (studio == null)
                throw new InvalidOperationException("Dance Studio is required to place the mirror.");

            // 既存の予備カメラを止め、ユーザーに表示する視点をXRカメラに統一する。
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<Camera>(out var otherCamera) && otherCamera != camera)
                    root.SetActive(false);

            // HMDの姿勢追跡を有効にし、並進成分は実行時にXR Originで相殺する。
            camera.GetComponent<TrackedPoseDriver>().trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            camera.transform.localPosition = Vector3.zero;

            var session = new GameObject(SessionName);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(model, session.transform);

            // 視点の基準はアバター側で設定した Eyes を使用する。
            var eyes = FindBone(avatar, "Eyes");
            session.transform.position = StartingPlacement(model, clip, origin.transform.position);

            var playback = session.AddComponent<DancePlayback>();
            var panel = CreateControlPanel(camera);
            var playButton = CreateButton(panel, "Play Recording", "Play  (A)",
                new Vector2(-146, ButtonRowOffset), 240, out _);
            var viewpointButton = CreateButton(panel, "Switch Viewpoint", "First Person  (B)",
                new Vector2(126, ButtonRowOffset), 280,
                out var viewpointLabel);
            UnityEventTools.AddPersistentListener(playButton.onClick, playback.PlayFromStart);
            UnityEventTools.AddPersistentListener(viewpointButton.onClick, playback.ToggleViewpoint);
            var serialized = new SerializedObject(playback);
            serialized.FindProperty("avatar").objectReferenceValue = avatar;
            serialized.FindProperty("clip").objectReferenceValue = clip;
            serialized.FindProperty("eyes").objectReferenceValue = eyes;
            serialized.FindProperty("xrOrigin").objectReferenceValue = origin.transform;
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("playButton").objectReferenceValue = playButton;
            serialized.FindProperty("viewpointButton").objectReferenceValue = viewpointButton;
            serialized.FindProperty("viewpointLabel").objectReferenceValue = viewpointLabel;
            ConfigureFloorControl(panel, playback, serialized);
            // 鏡面と枠の基準にスタジオを参照し、既存のステージ用マテリアルを使う。
            serialized.FindProperty("studio").objectReferenceValue = studio.transform;
            serialized.FindProperty("mirrorMarkerMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(MirrorMarkerMaterialPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene);
        }

        /// <summary>名前と大文字小文字が一致するボーンをモデル階層から取得する。</summary>
        private static Transform FindBone(GameObject avatar, string name)
        {
            return avatar.GetComponentsInChildren<Transform>().Single(bone => bone.name == name);
        }

        /// <summary>開始フレームの足先を床近くに置き、記録内の相対移動はそのまま使用する。</summary>
        private static Vector3 StartingPlacement(GameObject model, AnimationClip clip, Vector3 floorPosition)
        {
            var preview = UnityEngine.Object.Instantiate(model);
            try
            {
                clip.SampleAnimation(preview, 0);
                var left = FindBone(preview, "l_toes").position;
                var right = FindBone(preview, "r_toes").position;
                var center = (left + right) * 0.5f;
                center.y = Mathf.Min(left.y, right.y) - 0.025f;
                return floorPosition - center;
            }
            finally
            {
                // 位置合わせに使ったモデルはシーンに残さない。
                UnityEngine.Object.DestroyImmediate(preview);
            }
        }

        /// <summary>既存のパネルに床面ボタンを追加し、木目と切り替え先の処理を接続する。</summary>
        internal static void ConfigureFloorControl(RectTransform panel, DancePlayback playback, SerializedObject serialized)
        {
            var floorButton = CreateButton(panel, "Switch Floor", DancePlayback.CheckerFloorLabel,
                new Vector2(126, -ButtonRowOffset), 280, out var floorLabel);
            UnityEventTools.AddPersistentListener(floorButton.onClick, playback.ToggleFloor);
            serialized.FindProperty("floorButton").objectReferenceValue = floorButton;
            serialized.FindProperty("floorLabel").objectReferenceValue = floorLabel;
            serialized.FindProperty("oakFloorMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>(OakFloorMaterialPath);
        }

        /// <summary>XRカメラの視界の右上に、マウスで操作できるワールド空間のパネルを作る。</summary>
        private static RectTransform CreateControlPanel(Camera camera)
        {
            var panel = new GameObject("Playback UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            panel.transform.SetParent(camera.transform, false);
            // 上段の従来位置を保ち、床面ボタンを置く下段の分だけパネルの中心を下げる。
            panel.transform.localPosition = new Vector3(0.32f, 0.3906f, 1.2f);
            panel.transform.localScale = Vector3.one * 0.0013f;
            var rect = panel.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(532, ButtonHeight + 2 * ButtonRowOffset);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            // 別の入力モジュールがある場合は既存のEventSystemを使う。
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("Playback Event System", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

            return rect;
        }

        /// <summary>パネル内の指定位置に、共通の配色と文字サイズで操作ボタンを作る。</summary>
        private static Button CreateButton(RectTransform panel, string objectName, string caption, Vector2 position,
            float width, out Text text)
        {
            var buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            var buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.SetParent(panel, false);
            buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = position;
            buttonRect.sizeDelta = new Vector2(width, ButtonHeight);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.07f, 0.18f, 0.23f, 1);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.67f, 0.84f, 0.88f);
            button.colors = colors;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            FitToParent(label.GetComponent<RectTransform>(), button.transform);
            text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = caption;
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            return button;
        }

        /// <summary>UIの子要素を親と同じ大きさにする。</summary>
        private static void FitToParent(RectTransform rect, Transform parent)
        {
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
