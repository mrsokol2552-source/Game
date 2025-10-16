using Game.Application.Services;
using Game.Domain.Research;
using Game.Infrastructure.Configs;

namespace Game.Application.UseCases
{
    public class CompleteResearch
    {
        private readonly GameStateService game;

        public CompleteResearch(GameStateService game)
        {
            this.game = game;
        }

        public bool Execute(ResearchDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Id)) return false;
            var st = game.Research.GetStatus(def.Id);
            if (st == ResearchStatus.Done) return true;
            if (st == ResearchStatus.Locked) return false; // not started
            game.Research.SetStatus(def.Id, ResearchStatus.Done);
            return true;
        }
    }
}

