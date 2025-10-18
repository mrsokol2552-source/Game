using Game.Application.Services;
using Game.Domain.Research;

namespace Game.Application.UseCases
{
    public class CompleteResearch
    {
        private readonly GameStateService game;

        public CompleteResearch(GameStateService game)
        {
            this.game = game;
        }

        public bool Execute(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var st = game.Research.GetStatus(id);
            if (st == ResearchStatus.Done) return true;
            if (st == ResearchStatus.Locked) return false; // not started
            game.Research.SetStatus(id, ResearchStatus.Done);
            return true;
        }
    }
}
