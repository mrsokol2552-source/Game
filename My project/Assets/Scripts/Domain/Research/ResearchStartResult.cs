// [CODE-ID: SCRIPTS-DOMAIN-RESEARCH-RESEARCHSTARTRESULT]
// Logical block: Scripts/Domain/Research/ResearchStartResult.

namespace Game.Domain.Research
{
    public enum ResearchStartResult
    {
        Invalid,
        Started,
        AlreadyQueued,
        AlreadyDone,
        InsufficientResources
    }
}
