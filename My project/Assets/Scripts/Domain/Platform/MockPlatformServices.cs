using System;
using System.Collections.Generic;

namespace RTS.Domain.Platform
{
    /// <summary>
    /// Deterministic, in-memory mock implementation of IPlatformServices for tests and editor play.
    /// Tracks all lifecycle events and allows simulating successful or failing ads.
    /// </summary>
    public sealed class MockPlatformServices : IPlatformServices
    {
        public bool IsLoadingReady { get; private set; }
        public bool IsGameplayActive { get; private set; }
        public int FullscreenAdShowCount { get; private set; }
        public int RewardedVideoShowCount { get; private set; }
        public int RewardedSuccessCount { get; private set; }

        public bool AutoGrantReward { get; set; } = true;
        public string StoredPlayerData { get; set; } = string.Empty;

        public List<string> EventLog { get; } = new List<string>();

        public void NotifyLoadingReady()
        {
            IsLoadingReady = true;
            EventLog.Add("LoadingAPI.ready()");
        }

        public void GameplayStart()
        {
            IsGameplayActive = true;
            EventLog.Add("GameplayAPI.start()");
        }

        public void GameplayStop()
        {
            IsGameplayActive = false;
            EventLog.Add("GameplayAPI.stop()");
        }

        public void ShowFullscreenAd(Action onOpen = null, Action<bool> onClose = null)
        {
            FullscreenAdShowCount++;
            EventLog.Add("ShowFullscreenAd");

            bool wasActive = IsGameplayActive;
            if (wasActive) GameplayStop();

            onOpen?.Invoke();
            onClose?.Invoke(true);

            if (wasActive) GameplayStart();
        }

        public void ShowRewardedVideo(Action onOpen = null, Action onRewarded = null, Action onClose = null, Action<string> onError = null)
        {
            RewardedVideoShowCount++;
            EventLog.Add("ShowRewardedVideo");

            bool wasActive = IsGameplayActive;
            if (wasActive) GameplayStop();

            onOpen?.Invoke();

            if (AutoGrantReward)
            {
                RewardedSuccessCount++;
                onRewarded?.Invoke();
                onClose?.Invoke();
            }
            else
            {
                onError?.Invoke("User dismissed ad early");
                onClose?.Invoke();
            }

            if (wasActive) GameplayStart();
        }

        public void SavePlayerData(string jsonState, Action<bool> onComplete = null)
        {
            StoredPlayerData = jsonState;
            EventLog.Add($"SavePlayerData({jsonState.Length} bytes)");
            onComplete?.Invoke(true);
        }

        public void LoadPlayerData(Action<string> onComplete)
        {
            EventLog.Add("LoadPlayerData");
            onComplete?.Invoke(StoredPlayerData);
        }
    }
}
