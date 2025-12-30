using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace SmallScaleInc.CharacterCreatorModern
{
    public class GearPresetManager : MonoBehaviour
    {
        // This class holds the settings for each gear piece.
        [System.Serializable]
        public class GearPreset
        {
            [Header("Head")]
            public RuntimeAnimatorController headAnimator;
            public Color headColor;

            [Header("Chest")]
            public RuntimeAnimatorController chestAnimator;
            public Color chestColor;

            [Header("Legs")]
            public RuntimeAnimatorController legsAnimator;
            public Color legsColor;

            [Header("Shoes")]
            public RuntimeAnimatorController shoesAnimator;
            public Color shoesColor;
            
            [Header("Skin")]
            public Color skinColor;

            [Header("Weapon")]
            // Drag and drop the weapon GameObject (with a SpriteRenderer) that you want for this preset.
            public GameObject weapon;
            
            [Header("Shield (Optional)")]
            // Drag and drop the shield GameObject (with a SpriteRenderer) that you want for this preset.
            // Leave unassigned if no shield should be applied.
            public GameObject shield;
            public Color shieldColor;

            [Header("Backpack (Optional)")]
            // Drag and drop the backpack GameObject (with a SpriteRenderer) that you want for this preset.
            // Leave unassigned if no backpack should be applied.
            public GameObject backpack;
            public Color backpackColor;
        }

        [Header("Preset Settings")]
        // Define presets in the inspector.
        public List<GearPreset> presets = new List<GearPreset>();

        [Header("UI Toggles for Presets")]
        // Assign your toggles here. Optionally attach them to a ToggleGroup.
        public Toggle[] presetToggles;

        [Header("Gear Slot References")]
        // Assign your gear objects (they should have the corresponding Animator/SpriteRenderer)
        public Animator headAnimatorComponent;
        public SpriteRenderer headRenderer;

        public Animator chestAnimatorComponent;
        public SpriteRenderer chestRenderer;

        public Animator legsAnimatorComponent;
        public SpriteRenderer legsRenderer;

        public Animator shoesAnimatorComponent;
        public SpriteRenderer shoesRenderer;

        [Header("Skin Slot Reference")]
        // This is the single renderer for skin color.
        public SpriteRenderer skinRenderer;

        [Header("Weapon References")]
        // List all weapon GameObjects here. These weapons should have a SpriteRenderer component.
        // When applying a preset, the script disables the SpriteRenderer on each of these weapons,
        // then enables the one specified in the preset.
        public List<GameObject> allWeaponObjects = new List<GameObject>();

        [Header("Shield References")]
        // List all shield GameObjects here. They should have a SpriteRenderer component.
        public List<GameObject> allShieldObjects = new List<GameObject>();

        [Header("Backpack References")]
        // List all backpack GameObjects here. They should have a SpriteRenderer component.
        public List<GameObject> allBackpackObjects = new List<GameObject>();

        void Start()
        {
            // Warn if the number of toggles doesn't match the number of presets.
            if (presetToggles.Length != presets.Count)
            {
                Debug.LogWarning("The number of preset toggles does not match the number of presets defined.");
            }

            // Assign a listener to each toggle that applies the corresponding preset when turned on.
            for (int i = 0; i < presetToggles.Length; i++)
            {
                int index = i; // Local copy for the closure.
                if (presetToggles[i] != null)
                {
                    presetToggles[i].onValueChanged.AddListener(isOn =>
                    {
                        if (isOn)
                            ApplyPreset(index);
                    });
                }
            }
        }

        // This function applies the preset with the given index.
        public void ApplyPreset(int presetIndex)
        {
            if (presetIndex < 0 || presetIndex >= presets.Count)
            {
                Debug.LogError("Preset index out of range!");
                return;
            }

            GearPreset preset = presets[presetIndex];

            // Update head gear
            if (headAnimatorComponent != null)
                headAnimatorComponent.runtimeAnimatorController = preset.headAnimator;
            if (headRenderer != null)
                headRenderer.color = preset.headColor;

            // Update chest gear
            if (chestAnimatorComponent != null)
                chestAnimatorComponent.runtimeAnimatorController = preset.chestAnimator;
            if (chestRenderer != null)
                chestRenderer.color = preset.chestColor;

            // Update legs gear
            if (legsAnimatorComponent != null)
                legsAnimatorComponent.runtimeAnimatorController = preset.legsAnimator;
            if (legsRenderer != null)
                legsRenderer.color = preset.legsColor;

            // Update shoes gear
            if (shoesAnimatorComponent != null)
                shoesAnimatorComponent.runtimeAnimatorController = preset.shoesAnimator;
            if (shoesRenderer != null)
                shoesRenderer.color = preset.shoesColor;
            
            // Update skin color
            if (skinRenderer != null)
                skinRenderer.color = preset.skinColor;

            // Disable all weapon sprite renderers
            if (allWeaponObjects != null)
            {
                foreach (GameObject weaponObj in allWeaponObjects)
                {
                    if (weaponObj != null)
                    {
                        SpriteRenderer sr = weaponObj.GetComponent<SpriteRenderer>();
                        if (sr != null)
                            sr.enabled = false;
                    }
                }
            }

            // Enable the selected preset's weapon sprite renderer
            if (preset.weapon != null)
            {
                SpriteRenderer presetWeaponSR = preset.weapon.GetComponent<SpriteRenderer>();
                if (presetWeaponSR != null)
                    presetWeaponSR.enabled = true;
            }
            
            // Disable all shield sprite renderers
            if (allShieldObjects != null)
            {
                foreach (GameObject shieldObj in allShieldObjects)
                {
                    if (shieldObj != null)
                    {
                        SpriteRenderer sr = shieldObj.GetComponent<SpriteRenderer>();
                        if (sr != null)
                            sr.enabled = false;
                    }
                }
            }

            // Enable the selected preset's shield sprite renderer (if assigned)
            if (preset.shield != null)
            {
                SpriteRenderer presetShieldSR = preset.shield.GetComponent<SpriteRenderer>();
                if (presetShieldSR != null)
                {
                    presetShieldSR.color = preset.shieldColor;
                    presetShieldSR.enabled = true;
                }
            }

            // Disable all backpack sprite renderers
            if (allBackpackObjects != null)
            {
                foreach (GameObject backpackObj in allBackpackObjects)
                {
                    if (backpackObj != null)
                    {
                        SpriteRenderer sr = backpackObj.GetComponent<SpriteRenderer>();
                        if (sr != null)
                            sr.enabled = false;
                    }
                }
            }

            // Enable the selected preset's backpack sprite renderer (if assigned)
            if (preset.backpack != null)
            {
                SpriteRenderer presetBackpackSR = preset.backpack.GetComponent<SpriteRenderer>();
                if (presetBackpackSR != null)
                {
                    presetBackpackSR.color = preset.backpackColor;
                    presetBackpackSR.enabled = true;
                }
            }
        }
    }
}