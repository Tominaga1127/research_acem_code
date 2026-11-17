using UnityEngine;

public sealed class LivePreviewFrameRate : MonoBehaviour
{
    private void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 72;
    }
}
