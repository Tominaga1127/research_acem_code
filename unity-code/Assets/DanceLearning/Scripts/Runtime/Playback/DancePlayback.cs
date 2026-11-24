using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEngine.Rendering.Universal;
#endif

namespace DanceLearning
{
    /// <summary>
    /// FBXのモーションを再生し、操作に応じてXRカメラを一人称と三人称に切り替える。
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class DancePlayback : MonoBehaviour
    {
        private const float ThirdPersonAimBelowHead = 0.75f;
        private const float ThirdPersonLookLimit = 8f;

        [SerializeField] private GameObject avatar;
        [SerializeField] private AnimationClip clip;
        [SerializeField] private Transform eyes;
        [SerializeField] private Transform xrOrigin;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private Button playButton;
        [SerializeField] private Button viewpointButton;
        [SerializeField] private Text viewpointLabel;
        [SerializeField, Min(0)] private float thirdPersonDistance = 2.8f;
        [SerializeField, Min(0)] private float thirdPersonHeight = 0.25f;

        private float elapsed;
        private bool primaryButtonWasPressed;
        private bool secondaryButtonWasPressed;
        private bool isThirdPerson;
        private Vector3 thirdPersonHeading;
#if UNITY_EDITOR
        private UniversalAdditionalCameraData cameraData;
        private bool xrRenderingEnabled;
#endif

        public bool IsPlaying { get; private set; }

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
            isThirdPerson = !isThirdPerson;
            viewpointLabel.text = isThirdPerson ? "Third Person  (B)" : "First Person  (B)";
            FollowAvatar();
        }

        /// <summary>PICO右コントローラーのAとBを、画面の再生ボタンと視点ボタンに対応させる。</summary>
        private void Update()
        {
#if UNITY_EDITOR
            UpdateEditorDisplay();
#endif
            var controller = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            HandleButton(controller, CommonUsages.primaryButton, playButton, ref primaryButtonWasPressed);
            HandleButton(controller, CommonUsages.secondaryButton, viewpointButton, ref secondaryButtonWasPressed);
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
            if (isThirdPerson)
            {
                // 頭の付け根の位置に追従し、首が回ってもカメラのオフセットは回さない。
                var headPosition = eyes.parent.position;
                targetPosition = headPosition - thirdPersonHeading * thirdPersonDistance + Vector3.up * thirdPersonHeight;
                var bodyCenter = headPosition - Vector3.up * ThirdPersonAimBelowHead;
                var framingRotation = Quaternion.LookRotation(bodyCenter - targetPosition);

                // HMDの回転は全身を画面内に収めるための範囲に制限する。
                var trackedRotation = Quaternion.Inverse(xrOrigin.rotation) * viewCamera.transform.rotation;
                var lookRotation = Quaternion.RotateTowards(Quaternion.identity, trackedRotation, ThirdPersonLookLimit);
                xrOrigin.rotation = framingRotation * lookRotation * Quaternion.Inverse(trackedRotation);
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
        }
    }
}
