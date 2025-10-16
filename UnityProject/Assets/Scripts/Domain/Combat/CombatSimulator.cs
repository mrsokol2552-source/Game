namespace RtsGame.Domain.Combat
{
    public static class CombatSimulator
    {
        // Very simple placeholder; extend with armor, resistances, etc.
        public static int ResolveAttack(int attackerDamage, int targetHealth, DamageType type)
        {
            var remaining = targetHealth - attackerDamage;
            return remaining < 0 ? 0 : remaining;
        }
    }
}

