/*
@file: My project/Assets/Tests/EditMode/PlatformServicesTests.cs
@module: Platform/Tests
@purpose: Unit tests for IPlatformServices lifecycle, GameplayAPI transitions, and ad hooks
@entry: PlatformServicesTests
@deps: RTS.Domain.Platform
@tests: EditMode
@notes: Verifies early Yandex SDK contract in pure C# domain tests.
*/

using NUnit.Framework;
using RTS.Domain.Platform;

namespace Tests.EditMode
{
    [TestFixture]
    [Category("Gate")]
    public class PlatformServicesTests
    {
        [Test]
        public void LoadingReadyAndGameplayStartStopLifecycle()
        {
            var platform = new MockPlatformServices();

            Assert.IsFalse(platform.IsLoadingReady);
            Assert.IsFalse(platform.IsGameplayActive);

            platform.NotifyLoadingReady();
            Assert.IsTrue(platform.IsLoadingReady);

            platform.GameplayStart();
            Assert.IsTrue(platform.IsGameplayActive);

            platform.GameplayStop();
            Assert.IsFalse(platform.IsGameplayActive);
        }

        [Test]
        public void RewardedVideoTemporarilyStopsGameplayAndInvokesReward()
        {
            var platform = new MockPlatformServices();
            platform.GameplayStart();
            Assert.IsTrue(platform.IsGameplayActive);

            bool rewardedCalled = false;
            platform.ShowRewardedVideo(
                onRewarded: () => rewardedCalled = true
            );

            Assert.IsTrue(rewardedCalled, "Reward callback must be invoked on success.");
            Assert.AreEqual(1, platform.RewardedVideoShowCount);
            Assert.AreEqual(1, platform.RewardedSuccessCount);
            Assert.IsTrue(platform.IsGameplayActive, "Gameplay must automatically resume after ad completes.");
        }

        [Test]
        public void CloudDataSaveAndLoadRoundtrip()
        {
            var platform = new MockPlatformServices();
            string testJson = "{\"chapter\":2,\"day\":7,\"wallHp\":1200}";

            bool saved = false;
            platform.SavePlayerData(testJson, ok => saved = ok);
            Assert.IsTrue(saved);

            string loaded = null;
            platform.LoadPlayerData(data => loaded = data);
            Assert.AreEqual(testJson, loaded);
        }
    }
}
