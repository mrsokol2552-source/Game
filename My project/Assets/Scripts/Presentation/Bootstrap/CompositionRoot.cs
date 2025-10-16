using Game.Application.Services;
using Game.Application.UseCases;
using Game.Infrastructure.Configs;
using Game.Infrastructure.Persistence;
using UnityEngine;

namespace Game.Presentation.Bootstrap
{
    public class CompositionRoot : MonoBehaviour
    {
        public static GameStateService Game { get; private set; }

        [Header("Config")]
        public GameConfig GameConfig;
        public bool AutoStart = true;

        private SaveSystem saveSystem;

        private void Awake()
        {
            if (Game == null)
            {
                Game = new GameStateService();
            }
            saveSystem = new SaveSystem();

            if (AutoStart)
            {
                var start = new StartNewGame(Game);
                if (GameConfig != null && GameConfig.StartingResources != null)
                    start.Execute(GameConfig.StartingResources);
                else
                    start.Execute();
            }
        }

        private void Update()
        {
            // Advance domain-managed ticks; presentation supplies deltaTime.
            Game?.EconomyManager.Tick(Time.deltaTime);
        }

        public void Save()
        {
            new SaveGame(Game, saveSystem).Execute();
        }

        public void Load()
        {
            new LoadGame(Game, saveSystem).Execute();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Game, null)) return;
            // keep Game static until domain teardown is needed
        }
    }
}
