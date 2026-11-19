using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Unity.XR.CoreUtils;

namespace DanceLearning.Editor
{
    /// <summary>Mainに収録済みFBXと再生UIを配置し、アバター用の目の位置を設定する。</summary>
    public static class DancePlaybackSceneBuilder
    {
        private const string ScenePath = "Assets/DanceLearning/Scenes/Main.unity";
        private const string RecordingPath = "Assets/DanceLearning/Animations/MCPM_20261117_140210_refined.fbx";
        private const string SessionName = "Dance Playback";

        /// <summary>収録クリップ、シーンの既存XR Origin、再生ボタンを接続して保存する。</summary>
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

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(RecordingPath);
            var clip = AssetDatabase.LoadAllAssetsAtPath(RecordingPath).OfType<AnimationClip>()
                .Single(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
            var origin = GameObject.Find("XR Origin (VR)").GetComponent<XROrigin>();
            var camera = origin.Camera;

            // 既存の予備カメラを止め、ユーザーに表示する視点をXRカメラに統一する。
            foreach (var root in scene.GetRootGameObjects())
                if (root.TryGetComponent<Camera>(out var otherCamera) && otherCamera != camera)
                    root.SetActive(false);

            // HMDの姿勢追跡を有効にし、並進成分は実行時にXR Originで相殺する。
            camera.GetComponent<TrackedPoseDriver>().trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            camera.transform.localPosition = Vector3.zero;

            var session = new GameObject(SessionName);
            var avatar = (GameObject)PrefabUtility.InstantiatePrefab(model, session.transform);

            // Playerでは非Legacyクリップの姿勢を適用するため、FBXのルートにAnimatorが必要。
            if (!avatar.TryGetComponent<Animator>(out var animator))
                animator = avatar.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var head = FindBone(avatar, "head");
            var foot = FindBone(avatar, "l_foot");
            var toe = FindBone(avatar, "l_toes");
            var forward = Vector3.ProjectOnPlane(toe.position - foot.position, Vector3.up).normalized;
            var eyes = new GameObject("Eyes").transform;
            eyes.SetParent(head);
            // このFBXには目のボーンがないため、頭の付け根から上5.5cm、前9cmを両目の中心とする。
            eyes.SetPositionAndRotation(head.position + Vector3.up * 0.055f + forward * 0.09f,
                Quaternion.LookRotation(forward));

            // メッシュのない収録なので、各ボーンに簡易形状を取り付けて身体の動きを表示する。
            AddBody(avatar, head);
            session.transform.position = StartingPlacement(model, clip, origin.transform.position);

            var playback = session.AddComponent<DancePlayback>();
            var button = CreateButton(camera);
            UnityEventTools.AddPersistentListener(button.onClick, playback.PlayFromStart);
            var serialized = new SerializedObject(playback);
            serialized.FindProperty("avatar").objectReferenceValue = avatar;
            serialized.FindProperty("clip").objectReferenceValue = clip;
            serialized.FindProperty("eyes").objectReferenceValue = eyes;
            serialized.FindProperty("xrOrigin").objectReferenceValue = origin.transform;
            serialized.FindProperty("viewCamera").objectReferenceValue = camera;
            serialized.FindProperty("playButton").objectReferenceValue = button;
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

        /// <summary>骨の位置関係がわかるよう、ボーン間をカプセルと手足の先端で描く。</summary>
        private static void AddBody(GameObject avatar, Transform head)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/DanceLearning/Materials/Studio/Dark Oak Trim.mat");
            // 追加する形状を探索対象に含めないよう、加工前のボーン一覧を取得する。
            foreach (var bone in avatar.GetComponentsInChildren<Transform>())
            {
                var parent = bone.parent;
                if (parent == null || parent == avatar.transform || bone.name.EndsWith("_End", StringComparison.Ordinal))
                    continue;
                var offset = bone.position - parent.position;
                if (offset.sqrMagnitude < 0.000001f || parent == head)
                    continue;

                // 近い位置から見たときに顔が視界を塞がないよう、頭部は影だけにする。
                var capsule = CreateBodyPart(parent, bone.name + " Segment", material, PrimitiveType.Capsule);
                capsule.transform.SetPositionAndRotation(parent.position + offset * 0.5f,
                    Quaternion.FromToRotation(Vector3.up, offset));
                var width = bone.name.Contains("torso") ? 0.20f : 0.055f;
                capsule.transform.localScale = new Vector3(width, offset.magnitude * 0.5f + width * 0.25f, width);
            }

            var headShape = CreateBodyPart(head, "Head Shape", material, PrimitiveType.Sphere);
            headShape.transform.position = head.position + Vector3.up * 0.07f;
            headShape.transform.localScale = new Vector3(0.15f, 0.19f, 0.15f);
            headShape.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;

            foreach (var name in new[] { "l_hand", "r_hand", "l_toes", "r_toes" })
            {
                var bone = FindBone(avatar, name);
                var tip = CreateBodyPart(bone, name + " Shape", material, PrimitiveType.Sphere);
                tip.transform.localScale = Vector3.one * 0.05f;
            }
        }

        /// <summary>描画専用の形状を作り、ボーンの子にして姿勢と一緒に動かす。</summary>
        private static GameObject CreateBodyPart(Transform parent, string name, Material material, PrimitiveType type)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent, false);
            shape.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
            return shape;
        }

        /// <summary>PCのマウスとPICOのAで使える、XRカメラの前に表示する再生ボタンを作る。</summary>
        private static Button CreateButton(Camera camera)
        {
            var panel = new GameObject("Playback UI", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            panel.transform.SetParent(camera.transform, false);
            panel.transform.localPosition = new Vector3(0, -0.23f, 1.2f);
            panel.transform.localScale = Vector3.one * 0.0016f;
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(240, 62);
            var canvas = panel.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = camera;

            var buttonObject = new GameObject("Play Recording", typeof(RectTransform), typeof(Image), typeof(Button));
            FitToParent(buttonObject.GetComponent<RectTransform>(), panel.transform);
            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.07f, 0.18f, 0.23f, 1);
            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.pressedColor = new Color(0.67f, 0.84f, 0.88f);
            button.colors = colors;

            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            FitToParent(label.GetComponent<RectTransform>(), button.transform);
            var text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = "Play  (A)";
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;

            // 別の入力モジュールがある場合は既存のEventSystemを使う。
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystem = new GameObject("Playback Event System", typeof(EventSystem),
                    typeof(InputSystemUIInputModule));
                eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }

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
