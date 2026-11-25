using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEngine.Rendering.Universal;
#endif

namespace DanceLearning
{
    /// <summary>
    /// 収録と鏡像の再生、XR視点の切り替え、スタジオの床面操作を扱う。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class DancePlayback : MonoBehaviour
    {
        private const float ThirdPersonAimBelowHead = 0.75f;
        public const string CheckerFloorLabel = "Checker  (X)";
        private const string OakFloorLabel = "Oak  (X)";

        [SerializeField, Tooltip("Animator を持つ配置済みアバターのルート")] private GameObject avatar;
        [SerializeField, Tooltip("アバターのボーン階層に対応する AnimationClip")] private AnimationClip clip;
        [SerializeField] private Transform eyes;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Button playButton;
        [SerializeField] private Button viewpointButton;
        [SerializeField] private Text viewpointLabel;
        [SerializeField] private Button floorButton;
        [SerializeField] private Text floorLabel;
        [SerializeField] private Material oakFloorMaterial;
        [SerializeField, Min(0)] private float thirdPersonDistance = 2.8f;
        [SerializeField, Min(0)] private float thirdPersonHeight = 0.25f;
        [SerializeField, Tooltip("鏡面の基準となる Dance Studio のルート")] private Transform studio;
        [SerializeField] private Material mirrorMarkerMaterial;

        private enum Viewpoint { FirstPerson, ThirdPerson, Mirror }

        private float elapsed;
        private bool primaryButtonWasPressed;
        private bool secondaryButtonWasPressed;
        private bool floorButtonWasPressed;
        private MeshRenderer floorRenderer;
        private Material checkerFloorMaterial;
        private Viewpoint viewpoint;
        private Vector3 thirdPersonHeading;
        private Transform mirrorPlane;
        private GameObject reflectedAvatar;
        private Renderer[] sourceRenderers;
        private bool[] sourceRenderingWasOff;
        private float mirrorCameraDepth;
#if UNITY_EDITOR
        private UniversalAdditionalCameraData cameraData;
        private bool xrRenderingEnabled;
#endif

        public bool IsPlaying { get; private set; }

        /// <summary>スタジオの床と、起動時に割り当てられている市松模様を取得する。</summary>
        private void Awake()
        {
            floorRenderer = studio.Find("Floor").GetComponent<MeshRenderer>();
            checkerFloorMaterial = floorRenderer.sharedMaterial;
        }

        /// <summary>開始姿勢を適用し、ボタン入力を待つ。</summary>
        private void OnEnable()
        {
#if UNITY_EDITOR
            cameraData = viewCamera.GetUniversalAdditionalCameraData();
            xrRenderingEnabled = cameraData.allowXRRendering;
            UpdateEditorDisplay();
#endif
            Application.onBeforeRender += AlignBeforeRender;
            // 再生オブジェクトの向きから、三人称カメラを置く水平軸を固定する。
            thirdPersonHeading = Quaternion.Euler(0, transform.eulerAngles.y, 0) * Vector3.forward;
            ApplyPose(0);
            // 初回だけ、開始姿勢の頭の位置を基準に鏡像と鏡枠を作る。
            if (mirrorPlane == null)
                CreateMirror();
            mirrorPlane.gameObject.SetActive(true);
            UpdateSourceVisibility();
            playButton.gameObject.SetActive(true);
            FollowAvatar();
        }

        /// <summary>再生ボタンから呼び出し、クリップを先頭から1回再生する。</summary>
        public void PlayFromStart()
        {
            // 再生中や無効化中の入力では、再生位置を変えない。
            if (IsPlaying || !isActiveAndEnabled)
                return;

            elapsed = 0;
            ApplyPose(elapsed);
            IsPlaying = true;
            playButton.gameObject.SetActive(false);
        }

        /// <summary>再生時刻を保ったまま視点を切り替え、ボタンに現在の視点を表示する。</summary>
        public void ToggleViewpoint()
        {
            // 既存の一人称と三人称の後に鏡視点を追加し、次の入力で一人称に戻る。
            viewpoint = viewpoint switch
            {
                Viewpoint.FirstPerson => Viewpoint.ThirdPerson,
                Viewpoint.ThirdPerson => Viewpoint.Mirror,
                _ => Viewpoint.FirstPerson
            };
            viewpointLabel.text = viewpoint switch
            {
                Viewpoint.ThirdPerson => "Third Person  (B)",
                Viewpoint.Mirror => "Mirror  (B)",
                _ => "First Person  (B)"
            };
            UpdateSourceVisibility();
            FollowAvatar();
        }

        /// <summary>床のマテリアルだけを切り替え、現在の床の種類をボタンに表示する。</summary>
        public void ToggleFloor()
        {
            // 市松模様のときは木目に切り替え、木目のときは市松模様に戻す。
            var useOak = floorRenderer.sharedMaterial == checkerFloorMaterial;
            floorRenderer.sharedMaterial = useOak ? oakFloorMaterial : checkerFloorMaterial;
            floorLabel.text = useOak ? OakFloorLabel : CheckerFloorLabel;
        }

        /// <summary>PICO右手のAとBを再生と視点切り替えに、左手のXを床面切り替えに対応させる。</summary>
        private void Update()
        {
#if UNITY_EDITOR
            UpdateEditorDisplay();
#endif
            var controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            HandleButton(controller, CommonUsages.primaryButton, playButton, ref primaryButtonWasPressed);
            HandleButton(controller, CommonUsages.secondaryButton, viewpointButton, ref secondaryButtonWasPressed);
            var leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            HandleButton(leftController, CommonUsages.primaryButton, floorButton, ref floorButtonWasPressed);
        }

        /// <summary>有効なボタンを押した瞬間だけクリックとして扱い、長押し時の連続操作を防ぐ。</summary>
        private static void HandleButton(InputDevice controller, InputFeatureUsage<bool> usage, Button button,
            ref bool wasPressed)
        {
            var pressed = controller.TryGetFeatureValue(usage, out var value) && value;
            if (pressed && !wasPressed && button.isActiveAndEnabled && button.IsInteractable())
                button.onClick.Invoke();
            wasPressed = pressed;
        }

        /// <summary>再生中は記録時刻を進め、そのフレームのアバターにカメラを追従させる。</summary>
        private void LateUpdate()
        {
            if (IsPlaying)
            {
                elapsed = Mathf.Min(elapsed + Time.deltaTime, clip.length);
                ApplyPose(elapsed);
                // 最後の姿勢で止まり、次の再生を受け付ける。
                if (elapsed >= clip.length)
                {
                    IsPlaying = false;
                    playButton.gameObject.SetActive(true);
                }
            }

            FollowAvatar();
        }

        /// <summary>指定時刻の姿勢だけを評価し、自動再生やループを避ける。</summary>
        private void ApplyPose(float time)
        {
            clip.SampleAnimation(avatar, time);
            if (reflectedAvatar != null)
                clip.SampleAnimation(reflectedAvatar, time);
        }

        /// <summary>スタジオ中央に鏡面を固定し、鏡像と床から天井までの枠を配置する。</summary>
        private void CreateMirror()
        {
            var floor = studio.Find("Floor").GetComponent<BoxCollider>();
            var planePosition = floor.transform.TransformPoint(floor.center + Vector3.up * (floor.size.y * 0.5f));
            var headPosition = eyes.parent.position;
            var mirrorRotation = Quaternion.LookRotation(studio.forward);
            mirrorPlane = new GameObject("Mirror Plane").transform;
            mirrorPlane.SetParent(transform, false);
            mirrorPlane.SetPositionAndRotation(planePosition, mirrorRotation);
            mirrorCameraDepth = mirrorPlane.InverseTransformPoint(headPosition).z;

            // 鏡面の座標系で奥行きだけを反転する。クリップのルート移動と全ボーンに同じ反射がかかる。
            var reflection = new GameObject("Reflection Space").transform;
            reflection.SetParent(mirrorPlane, false);
            reflection.localScale = new Vector3(1, 1, -1);
            var playbackSpace = new GameObject("Reflected Playback Space").transform;
            playbackSpace.SetParent(reflection, false);
            playbackSpace.localPosition = mirrorPlane.InverseTransformPoint(transform.position);
            playbackSpace.localRotation = Quaternion.Inverse(mirrorRotation) * transform.rotation;
            reflectedAvatar = Instantiate(avatar, playbackSpace, false);
            reflectedAvatar.name = "Reflected Avatar";
            clip.SampleAnimation(reflectedAvatar, 0);

            // 元のアバターの各パーツの表示状態を保存し、視点を切り替えたときに復元できるようにする。
            sourceRenderers = avatar.GetComponentsInChildren<Renderer>(true);
            sourceRenderingWasOff = new bool[sourceRenderers.Length];
            for (var index = 0; index < sourceRenderers.Length; index++)
                sourceRenderingWasOff[index] = sourceRenderers[index].forceRenderingOff;

            var leftWall = studio.Find("Walls/Left Wall").GetComponent<BoxCollider>();
            var rightWall = studio.Find("Walls/Right Wall").GetComponent<BoxCollider>();
            var leftEdge = GetWallInnerEdge(leftWall, 1);
            var rightEdge = GetWallInnerEdge(rightWall, -1);
            var frameWidth = rightEdge - leftEdge;
            var frameCenter = (leftEdge + rightEdge) * 0.5f;
            // 枠用のマテリアルが設定されている場合に、床と天井の内面に上下端を合わせる。
            if (mirrorMarkerMaterial != null)
            {
                var ceiling = studio.Find("Ceiling").GetComponent<BoxCollider>();
                var underside = ceiling.center - Vector3.up * (ceiling.size.y * 0.5f);
                var frameHeight = mirrorPlane.InverseTransformPoint(ceiling.transform.TransformPoint(underside)).y;
                const float thickness = 0.045f;
                var color = new MaterialPropertyBlock();
                color.SetColor("_BaseColor", new Color(0.05f, 0.65f, 0.62f));
                var horizontal = new Vector3(frameWidth, thickness, thickness);
                var vertical = new Vector3(thickness, frameHeight, thickness);
                CreateMirrorMarker("Floor Marker", new Vector3(frameCenter, thickness * 0.5f, 0), horizontal, color);
                CreateMirrorMarker("Top Marker", new Vector3(frameCenter, frameHeight - thickness * 0.5f, 0), horizontal, color);
                CreateMirrorMarker("Left Marker", new Vector3(leftEdge + thickness * 0.5f, frameHeight * 0.5f, 0), vertical, color);
                CreateMirrorMarker("Right Marker", new Vector3(rightEdge - thickness * 0.5f, frameHeight * 0.5f, 0), vertical, color);
            }
        }

        /// <summary>左右の壁から、室内側の表面の横位置を鏡面の座標で求める。</summary>
        private float GetWallInnerEdge(BoxCollider wall, float inwardDirection)
        {
            var edge = wall.center + Vector3.right * (wall.size.x * 0.5f * inwardDirection);
            return mirrorPlane.InverseTransformPoint(wall.transform.TransformPoint(edge)).x;
        }

        /// <summary>枠の部材を表示専用のメッシュとして配置する。</summary>
        private void CreateMirrorMarker(string markerName, Vector3 position, Vector3 scale, MaterialPropertyBlock color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = markerName;
            marker.transform.SetParent(mirrorPlane, false);
            marker.transform.localPosition = position;
            marker.transform.localScale = scale;
            var collider = marker.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = mirrorMarkerMaterial;
            markerRenderer.SetPropertyBlock(color);
        }

        /// <summary>鏡視点では手前のモデルによる遮蔽を防ぎ、他の視点では元の表示設定に戻す。</summary>
        private void UpdateSourceVisibility()
        {
            var hideSource = isActiveAndEnabled && viewpoint == Viewpoint.Mirror;
            for (var index = 0; index < sourceRenderers.Length; index++)
            {
                if (sourceRenderers[index] != null)
                    sourceRenderers[index].forceRenderingOff = hideSource || sourceRenderingWasOff[index];
            }
        }

        /// <summary>描画直前のHMD追跡更新後に、アバターに合わせてカメラの姿勢を調整する。</summary>
        [BeforeRenderOrder(100)]
        private void AlignBeforeRender()
        {
            FollowAvatar();
        }

#if UNITY_EDITOR
        /// <summary>HMDが未接続の間は、Gameビューを通常のカメラで描画する。</summary>
        private void UpdateEditorDisplay()
        {
            var headset = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            var hasTracking = headset.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) && tracked;
            if (headset.name == "PICO Live Preview HMD")
            {
                // Live Previewは未接続でも装着中を返す。床基準の目の位置が届いてからXR描画に切り替える。
                hasTracking = headset.TryGetFeatureValue(CommonUsages.centerEyePosition, out var position) &&
                    position.sqrMagnitude > 0.000001f;
            }

            cameraData.allowXRRendering = xrRenderingEnabled && hasTracking;
        }
#endif

        /// <summary>視点に応じてXR Originを調整し、HMDの並進を相殺する。</summary>
        private void FollowAvatar()
        {
            var targetPosition = eyes.position;
            if (viewpoint == Viewpoint.Mirror)
            {
                // 頭ボーンの位置から横と高さを取り、奥行きは再生開始位置に固定する。
                var head = mirrorPlane.InverseTransformPoint(eyes.parent.position);
                targetPosition = mirrorPlane.TransformPoint(new Vector3(head.x, head.y, mirrorCameraDepth));
                // XR Originは鏡の正面を向け、HMDのヨー・ピッチ・ロールをそのまま重ねる。
                xrOrigin.rotation = mirrorPlane.rotation;
            }
            else if (viewpoint == Viewpoint.ThirdPerson)
            {
                // 頭の付け根の位置に追従し、首が回ってもカメラのオフセットは回さない。
                var headPosition = eyes.parent.position;
                targetPosition = headPosition - thirdPersonHeading * thirdPersonDistance + Vector3.up * thirdPersonHeight;
                var bodyCenter = headPosition - Vector3.up * ThirdPersonAimBelowHead;
                // XR Originは基準の構図に向け、HMDの回転は子カメラのローカル回転としてそのまま重ねる。
                xrOrigin.rotation = Quaternion.LookRotation(bodyCenter - targetPosition);
            }
            else
            {
                // 一人称では目のワールド回転を基準にして、HMDのローカル回転を重ねる。
                xrOrigin.rotation = eyes.rotation;
            }

            xrOrigin.position += targetPosition - viewCamera.transform.position;
        }

        /// <summary>コンポーネントを無効化したときに再生を終了する。</summary>
        private void OnDisable()
        {
            Application.onBeforeRender -= AlignBeforeRender;
#if UNITY_EDITOR
            if (cameraData != null)
                cameraData.allowXRRendering = xrRenderingEnabled;
#endif
            IsPlaying = false;
            if (mirrorPlane != null)
            {
                mirrorPlane.gameObject.SetActive(false);
                UpdateSourceVisibility();
            }
        }
    }
}
