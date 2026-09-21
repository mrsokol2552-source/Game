/*
@file: My project/Assets/Scripts/Presentation/Bootstrap/CompositionRoot.Persistence.cs
@module: presentation.bootstrap
@purpose: Extracted save/restore callbacks and unit visual restore helpers for CompositionRoot.
@entry: CROOT-04
@api: CompositionRoot partial persistence helpers
@deps: SaveSystem, UnitView, UnitCombat, UnitSpawnerCommander, SpriteRenderer
@data: serialized unit snapshot restore path and visual defaults
@perf: save/load only
@thread: main thread only
@tests: save/load smoke tests and SelfTestRoutine restore assertions
@config: DefaultUnitPrefab, PlayerSprite, EnemySprite, sorting settings
@assets: unit prefab and faction sprites
@notes: restore logic must stay deterministic because save/load tests depend on it
*/

using System.Collections.Generic;
using System.Linq;
using Game.Infrastructure.Persistence;
using Game.Presentation.Input;
using Game.Presentation.Performance;
using Game.Presentation.View;
using UnityEngine;

// [CODE-ID: SCRIPTS-PRESENTATION-BOOTSTRAP-COMPOSITIONROOT-PERSISTENCE]
// Logical block: CompositionRoot save/restore helper extraction.

namespace Game.Presentation.Bootstrap
{
    public partial class CompositionRoot
    {
        // [CROOT-04]
        // Save/restore callbacks, restored-unit visuals, and sorting helpers.
        private IEnumerable<SaveSystem.UnitSnapshot> CaptureUnitsEx()
        {
            var units = Object.FindObjectsByType<UnitView>();
            return units.Select(unit =>
            {
                var combat = unit.GetComponent<UnitCombat>();
                return new SaveSystem.UnitSnapshot
                {
                    Position = unit.transform.position,
                    HasDestination = unit.TryGetDestination(out var destination),
                    Destination = destination,
                    Faction = combat != null ? (int)combat.Faction : (int)global::Game.Domain.Units.Faction.Player,
                    Health = combat != null ? combat.CurrentHealth : unit.Stats.MaxHealth
                };
            }).ToList();
        }

        private void RestoreUnitsEx(IEnumerable<SaveSystem.UnitSnapshot> states)
        {
            var existing = Object.FindObjectsByType<UnitView>();
            foreach (var unit in existing)
            {
                if (unit != null)
                {
                    Destroy(unit.gameObject);
                }
            }

            var prefab = ResolveRestorePrefab();
            if (prefab == null)
            {
                Debug.LogWarning("[CompositionRoot] No unit prefab assigned; cannot restore units.");
                return;
            }

            var spawner = Object.FindAnyObjectByType<UnitSpawnerCommander>();
            UnitView lastRestored = null;
            foreach (var snapshot in states)
            {
                var restored = Instantiate(prefab, snapshot.Position, Quaternion.identity);
                if (snapshot.HasDestination)
                {
                    restored.SetDestination(snapshot.Destination);
                }

                var combat = restored.GetComponent<UnitCombat>();
                if (combat == null)
                {
                    combat = restored.gameObject.AddComponent<UnitCombat>();
                }

                combat.Faction = (global::Game.Domain.Units.Faction)snapshot.Faction;
                combat.SetHealth(snapshot.Health > 0 ? snapshot.Health : restored.Stats.MaxHealth);
                ApplyRestoredUnitVisuals(restored, combat);
                lastRestored = restored;
            }

            if (spawner != null && lastRestored != null)
            {
                spawner.SetLastUnit(lastRestored);
            }
        }

        private UnitView ResolveRestorePrefab()
        {
            if (DefaultUnitPrefab != null)
            {
                return DefaultUnitPrefab;
            }

            var spawner = Object.FindAnyObjectByType<UnitSpawnerCommander>();
            return spawner != null ? spawner.UnitPrefab : null;
        }

        private void ApplyRestoredUnitVisuals(UnitView unit, UnitCombat combat)
        {
            var spriteRenderer = unit.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                ApplyFactionVisuals(spriteRenderer, combat.Faction);
                ApplyUnitSorting(spriteRenderer);
            }

            EnsureUnitVisualComponents(unit);
        }

        private void ApplyFactionVisuals(SpriteRenderer spriteRenderer, global::Game.Domain.Units.Faction faction)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.color = faction == global::Game.Domain.Units.Faction.Enemy ? Color.red : Color.white;
            if (faction == global::Game.Domain.Units.Faction.Enemy && EnemySprite != null)
            {
                spriteRenderer.sprite = EnemySprite;
                return;
            }

            if (faction == global::Game.Domain.Units.Faction.Player && PlayerSprite != null)
            {
                spriteRenderer.sprite = PlayerSprite;
            }
        }

        private void EnsureUnitVisualComponents(UnitView unit)
        {
            if (unit.GetComponent<UnitHpOverlay>() == null)
            {
                unit.gameObject.AddComponent<UnitHpOverlay>();
            }

            if (unit.GetComponent<UnitVisualCulling>() == null)
            {
                unit.gameObject.AddComponent<UnitVisualCulling>();
            }
        }

        private void ApplyUnitSorting(SpriteRenderer spriteRenderer)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(UnitSortingLayerName) && SortingLayerExists(UnitSortingLayerName))
            {
                spriteRenderer.sortingLayerName = UnitSortingLayerName;
            }

            spriteRenderer.sortingOrder = UnitSortingOrder;
        }

        private static bool SortingLayerExists(string name)
        {
            foreach (var layer in SortingLayer.layers)
            {
                if (layer.name == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
