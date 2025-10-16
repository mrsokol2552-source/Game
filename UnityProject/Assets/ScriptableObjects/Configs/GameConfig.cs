using UnityEngine;

namespace RtsGame.Configs
{
    [CreateAssetMenu(menuName = "RTS/Config/Game Config")]
    public class GameConfig : ScriptableObject
    {
        public ResourceConfig Resources;
        public UnitConfig[] Units;
    }
}

