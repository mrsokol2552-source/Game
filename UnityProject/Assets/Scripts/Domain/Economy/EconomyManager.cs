namespace RtsGame.Domain.Economy
{
    public class EconomyManager
    {
        private readonly EconomyState _state;

        public EconomyManager(EconomyState state)
        {
            _state = state;
        }

        // Advance economy by delta time (seconds); fill later with production/consumption rules
        public void Tick(float deltaTime)
        {
            // Intentionally left empty in skeleton
        }
    }
}

