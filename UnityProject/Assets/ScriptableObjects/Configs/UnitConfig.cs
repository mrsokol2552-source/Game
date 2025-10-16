using UnityEngine;

namespace RtsGame.Configs
{
    [CreateAssetMenu(menuName = "RTS/Config/Unit Config")]
    public class UnitConfig : ScriptableObject
    {
        public RtsGame.Domain.Units.UnitStats Stats = new RtsGame.Domain.Units.UnitStats();
        public GameObject Prefab;
    }
}

