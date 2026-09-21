/*
@file: My project/Assets/Scripts/Presentation/Pathfinding/CrowdingResolver.Throttle.cs
@module: presentation.pathfinding.crowding
@purpose: Extracted adaptive-throttling and effective-work-budget helpers for CrowdingResolver.
@entry: CROWD-05
@api: CrowdingResolver partial throttle helpers
@deps: UnityEngine
@data: smoothed frame-time budget and effective search/group counts
@perf: lightweight; intended to keep CrowdingResolver from spiking during heavy scenes
@thread: main thread only
@tests: My project/Assets/Tests/PlayMode/FpsStressTests.cs, manual stress verification
@config: AdaptiveThrottling, population scaling, debug-log settings
@assets: none
@notes: this logic is a candidate for reuse if other maintenance systems need the same frame-budget behavior
*/

using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-PATHFINDING-CROWDINGRESOLVER-THROTTLE]
// Logical block: CrowdingResolver adaptive-throttle extraction.

namespace Game.Presentation.Pathfinding
{
    public partial class CrowdingResolver
    {
        // [CROWD-05]
        // Effective search-radius, group-budget, and diagnostic logging under adaptive throttling.
        private int ComputeSearchRadius(int unitCount)
        {
            int radius = SearchRadius;
            if (AutoScaleByPopulation && UnitsPerRadiusStep > 0 && unitCount > 0)
            {
                int reduction = unitCount / UnitsPerRadiusStep;
                radius = Mathf.Max(MinSearchRadius, SearchRadius - reduction);
            }

            if (!AdaptiveThrottling) return radius;
            if (_avgFrameTime <= FrameTimeBoostLimit)
                radius = Mathf.Min(SearchRadius, radius + 1);
            else if (_avgFrameTime >= FrameTimeHardLimit)
                radius = Mathf.Max(MinSearchRadius, radius - 2);
            else if (_avgFrameTime > FrameTimeSoftLimit)
                radius = Mathf.Max(MinSearchRadius, radius - 1);

            return radius;
        }

        private int ComputeMaxGroups(int unitCount)
        {
            int groups = MaxGroupsPerTick;
            if (AutoScaleByPopulation && UnitsPerCrowdGroupStep > 0)
            {
                groups = Mathf.CeilToInt(unitCount / (float)UnitsPerCrowdGroupStep);
                if (MaxGroupsPerTick > 0) groups = Mathf.Min(groups, MaxGroupsPerTick);
                groups = Mathf.Max(MinGroupsPerTick, groups);
            }

            if (!AdaptiveThrottling) return groups;
            if (_avgFrameTime <= FrameTimeBoostLimit)
                groups = MaxGroupsPerTick > 0 ? Mathf.Min(MaxGroupsPerTick, groups + 2) : groups + 2;
            else if (_avgFrameTime >= FrameTimeHardLimit)
                groups = Mathf.Max(MinGroupsPerTick, groups / 2);
            else if (_avgFrameTime > FrameTimeSoftLimit)
                groups = Mathf.Max(MinGroupsPerTick, Mathf.CeilToInt(groups * 0.75f));

            if (MaxGroupsPerTick > 0) groups = Mathf.Min(groups, MaxGroupsPerTick);
            return Mathf.Max(MinGroupsPerTick, groups);
        }

        private void MaybeLog(int radius, int groups, int units)
        {
            if (!DebugLogEffective) return;
            if (Time.time - _lastLogTime < DebugLogInterval) return;
            if (radius == _lastLogRadius && groups == _lastLogGroups) return;

            _lastLogTime = Time.time;
            _lastLogRadius = radius;
            _lastLogGroups = groups;
            Debug.Log($"[CrowdingResolver] units={units} radius={radius} maxGroups={groups} frameTime={_avgFrameTime:F3}s");
        }
    }
}
