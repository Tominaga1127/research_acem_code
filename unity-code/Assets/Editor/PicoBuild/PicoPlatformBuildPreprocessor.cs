using Unity.XR.PXR;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace DanceResearch.Editor
{
    // PICO OpenXR SDK 1.4.0 calls appID.Trim() at callback order 0,
    // although newly created platform settings can have a null appID.
    public sealed class PicoPlatformBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var settings = PXR_PlatformSetting.Instance;
            if (settings == null)
            {
                throw new BuildFailedException("PICO Platform settings could not be loaded or created.");
            }

            if (settings.appID == null)
            {
                settings.appID = string.Empty;
            }
        }
    }
}
