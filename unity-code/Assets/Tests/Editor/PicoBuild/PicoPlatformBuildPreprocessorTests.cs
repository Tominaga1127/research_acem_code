using DanceResearch.Editor;
using NUnit.Framework;
using Pico.Platform.Editor;
using Unity.XR.PXR;
using UnityEngine;
using UnityEngine.TestTools;

namespace DanceResearch.Tests.Editor
{
    public sealed class PicoPlatformBuildPreprocessorTests
    {
        private PXR_PlatformSetting previousSettings;
        private PXR_PlatformSetting testSettings;

        [SetUp]
        public void SetUp()
        {
            previousSettings = PXR_PlatformSetting.Instance;
            testSettings = ScriptableObject.CreateInstance<PXR_PlatformSetting>();
            PXR_PlatformSetting.Instance = testSettings;
        }

        [TearDown]
        public void TearDown()
        {
            PXR_PlatformSetting.Instance = previousSettings;
            Object.DestroyImmediate(testSettings);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void UnconfiguredAppIdDoesNotCrashSdkPreprocessor(string appId)
        {
            testSettings.appID = appId;
            var preprocessor = new PicoPlatformBuildPreprocessor();
            var sdkPreprocessor = new PlatformPreprocessor();
            Assert.That(preprocessor.callbackOrder, Is.LessThan(sdkPreprocessor.callbackOrder));

            preprocessor.OnPreprocessBuild(null);

            LogAssert.Expect(LogType.Warning, "appID is not configured");
            Assert.DoesNotThrow(() => sdkPreprocessor.OnPreprocessBuild(null));
            Assert.That(testSettings.appID, Is.EqualTo(appId ?? string.Empty));
        }

        [Test]
        public void ConfiguredAppIdIsPreserved()
        {
            testSettings.appID = " 123456 ";
            new PicoPlatformBuildPreprocessor().OnPreprocessBuild(null);
            Assert.DoesNotThrow(() => new PlatformPreprocessor().OnPreprocessBuild(null));
            Assert.That(testSettings.appID, Is.EqualTo(" 123456 "));
        }
    }
}
