namespace Game.Domain.Economy
{
    public class EconomyManager
    {
        private readonly EconomyState state;

        public EconomyManager(EconomyState state)
        {
            this.state = state;
        }

        // Advance economy counters by deltaTime (seconds).
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f) return;
            // Placeholder: no passive income. Logic to be added in later sprints.
        }
    }
}

