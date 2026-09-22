using System;

namespace RTS.Domain.Platform
{
    /// <summary>
    /// Contract for host platform operations (Yandex Games SDK, VK Play, Local Editor Mock).
    /// Provides lifecycle hooks (LoadingAPI, GameplayAPI), ad callbacks, and cloud saves.
    /// </summary>
    public interface IPlatformServices
    {
        bool IsGameplayActive { get; }

        /// <summary>
        /// Signals to the host platform that the initial assets have loaded and the game is interactive.
        /// Corresponds to Yandex SDK LoadingAPI.ready().
        /// </summary>
        void NotifyLoadingReady();

        /// <summary>
        /// Marks the beginning of active gameplay session.
        /// Corresponds to Yandex SDK GameplayAPI.start().
        /// </summary>
        void GameplayStart();

        /// <summary>
        /// Marks the pause or suspension of gameplay (e.g. during ads, pause menus, or window blur).
        /// Corresponds to Yandex SDK GameplayAPI.stop().
        /// </summary>
        void GameplayStop();

        /// <summary>
        /// Requests a fullscreen interstitial ad during natural game transitions (Dawn/Defeat).
        /// Automatically manages GameplayAPI.stop() before and GameplayAPI.start() after ad closes.
        /// </summary>
        void ShowFullscreenAd(Action onOpen = null, Action<bool> onClose = null);

        /// <summary>
        /// Requests a voluntary rewarded video for tactical incentives (Emergency Airstrike, Double Harvest, Tactical Resupply).
        /// Automatically pauses gameplay during viewing.
        /// </summary>
        void ShowRewardedVideo(Action onOpen = null, Action onRewarded = null, Action onClose = null, Action<string> onError = null);

        /// <summary>
        /// Persists JSON player data to cloud storage (up to 200 KB).
        /// </summary>
        void SavePlayerData(string jsonState, Action<bool> onComplete = null);

        /// <summary>
        /// Retrieves JSON player data from cloud storage.
        /// </summary>
        void LoadPlayerData(Action<string> onComplete);
    }
}
