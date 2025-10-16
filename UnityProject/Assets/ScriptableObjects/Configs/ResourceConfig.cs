using System.Collections.Generic;
using UnityEngine;
using RtsGame.Domain.Economy;

namespace RtsGame.Configs
{
    [CreateAssetMenu(menuName = "RTS/Config/Resource Config")]
    public class ResourceConfig : ScriptableObject
    {
        public List<ResourceAmount> StartStocks = new();
    }
}

