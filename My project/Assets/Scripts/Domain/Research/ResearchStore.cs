using System;
using System.Collections.Generic;

namespace Game.Domain.Research
{
    public class ResearchStore
    {
        private readonly Dictionary<string, ResearchStatus> states = new Dictionary<string, ResearchStatus>();

        public event Action<string, ResearchStatus> OnChanged;

        public ResearchStatus GetStatus(string id)
        {
            if (string.IsNullOrEmpty(id)) return ResearchStatus.Locked;
            return states.TryGetValue(id, out var st) ? st : ResearchStatus.Locked;
        }

        public void SetStatus(string id, ResearchStatus status)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (GetStatus(id) == status) return;
            states[id] = status;
            OnChanged?.Invoke(id, status);
        }
    }
}

