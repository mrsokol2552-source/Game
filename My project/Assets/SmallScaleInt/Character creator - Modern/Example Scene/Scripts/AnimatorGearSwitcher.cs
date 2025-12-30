using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro; // Required for TextMeshPro

namespace SmallScaleInc.CharacterCreatorModern
{
    public class AnimatorGearSwitcher : MonoBehaviour
    {
        [Header("Animator Controllers for Gear")]
        public List<RuntimeAnimatorController> animatorControllers;

        [Header("UI Buttons")]
        public Button nextButton;
        public Button previousButton;
        public Button randomButton; // Random button added

        [Header("Color Toggles")]
        public Toggle[] colorToggles = new Toggle[5];
        public Color[] toggleColors = new Color[5];
        // New Skin Color flag: if true, a random toggle from the above list is chosen.
        public bool isSkinColor;

        [Header("Weapon Settings")]
        public bool isWeapon; // Set true if this gear piece is a weapon.
        public Toggle[] weaponToggles; // List of toggles for weapons.

        [Header("Bag Settings")]
        public bool isBag; // Set true if this gear piece is a backpack.
        public Toggle[] bagToggles; // List of toggles for backpacks.

        [Header("Shield Settings")]
        public bool isShield; // Set true if this gear piece is a shield.
        public Toggle[] shieldToggles; // List of toggles for shields.

        [Header("Global Color Swatch Panel (shared by all gear pieces)")]
        public GameObject colorSwatchPanel; // Shared panel for all gear pieces
        public Button[] colorSwatchButtons; // Swatch buttons with their assigned colors
        public Button closeColorPickerButton; // Button to close the panel without action

        [Header("UI Color Info")]
        public TextMeshProUGUI colorInfoText; // TextMeshPro object to display swatch info

        [Header("Weapon Color Picker")]
        // List of weapon SpriteRenderers that should all update when the weapon color picker is used.
        public List<SpriteRenderer> weaponSpriteRenderers;

        public AnimationController animationController;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private int currentAnimatorIndex = 0;

        // Static variables to hold the active target(s) for color changes.
        public static SpriteRenderer currentTarget; // for gear (single target)
        public static List<SpriteRenderer> currentWeaponTargets; // for weapons (multiple targets)
        public static bool globalSwatchSetup = false;
        public static GameObject globalColorSwatchPanel;

        // What part of the body is the script attached to? Used on start to randomize the gear and color.
        public bool isHead; 
        public bool isChest;
        public bool isLegs;
        public bool isShoes;
        // The previous "isSkin" field remains (if used elsewhere) but now we use isSkinColor for skin color randomization.

        void Start()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            // Setup animator buttons.
            if (nextButton != null)
                nextButton.onClick.AddListener(NextAnimator);
            if (previousButton != null)
                previousButton.onClick.AddListener(PreviousAnimator);
            if (randomButton != null)
                randomButton.onClick.AddListener(RandomGear);

            // Initialize the first animator controller.
            if (animatorControllers.Count > 0)
                animator.runtimeAnimatorController = animatorControllers[currentAnimatorIndex];

            // Setup color toggles.
            for (int i = 0; i < colorToggles.Length; i++)
            {
                int index = i; // Local copy for the closure.
                if (colorToggles[index] != null)
                {
                    colorToggles[index].onValueChanged.AddListener(isOn =>
                    {
                        if (isOn)
                            ChangeColor(index);
                    });
                }
            }
            // Set colors of toggles.
            for (int i = 0; i < colorToggles.Length; i++)
            {
                if (colorToggles[i] != null && i < toggleColors.Length && colorToggles[i].targetGraphic != null)
                {
                    colorToggles[i].targetGraphic.color = toggleColors[i];
                }
            }
            
            // Setup the global color swatch panel and buttons only once.
            if (!globalSwatchSetup && colorSwatchPanel != null && colorSwatchButtons != null)
            {
                globalColorSwatchPanel = colorSwatchPanel;
                SetupColorSwatchButtons();
                SetupCloseButton();
                globalSwatchSetup = true;
                // Optionally, hide the panel by default.
                globalColorSwatchPanel.SetActive(false);
            }

            // If this gear piece should automatically randomize on start, include all possible types.
            if (isHead || isChest || isLegs || isShoes || isWeapon || isBag || isShield || isSkinColor)
            {
                RandomGear();
            }
        }

        void NextAnimator()
        {
            if (animatorControllers.Count == 0) return;
            currentAnimatorIndex = (currentAnimatorIndex + 1) % animatorControllers.Count;
            animator.runtimeAnimatorController = animatorControllers[currentAnimatorIndex];
        }

        void PreviousAnimator()
        {
            if (animatorControllers.Count == 0) return;
            currentAnimatorIndex--;
            if (currentAnimatorIndex < 0)
                currentAnimatorIndex = animatorControllers.Count - 1;
            animator.runtimeAnimatorController = animatorControllers[currentAnimatorIndex];
        }

        void ChangeColor(int toggleIndex)
        {
            if (spriteRenderer != null && toggleColors.Length > toggleIndex)
            {
                spriteRenderer.color = toggleColors[toggleIndex];
            }
        }

        // Picks a random animator/color or random toggle (weapon, bag, shield, skin color) if applicable.
        public void RandomGear()
        {
            bool randomizationApplied = false;

            // Weapon randomization.
            if (isWeapon && weaponToggles != null && weaponToggles.Length > 0)
            {
                foreach (Toggle toggle in weaponToggles)
                {
                    if (toggle != null)
                        toggle.isOn = false;
                }
                int randomWeaponIndex = Random.Range(0, weaponToggles.Length);
                if (weaponToggles[randomWeaponIndex] != null)
                    weaponToggles[randomWeaponIndex].isOn = true;
                randomizationApplied = true;
            }

            // Bag randomization.
            if (isBag && bagToggles != null && bagToggles.Length > 0)
            {
                foreach (Toggle toggle in bagToggles)
                {
                    if (toggle != null)
                        toggle.isOn = false;
                }
                int randomBagIndex = Random.Range(0, bagToggles.Length);
                if (bagToggles[randomBagIndex] != null)
                    bagToggles[randomBagIndex].isOn = true;
                randomizationApplied = true;
            }

            // Shield randomization.
            if (isShield && shieldToggles != null && shieldToggles.Length > 0)
            {
                foreach (Toggle toggle in shieldToggles)
                {
                    if (toggle != null)
                        toggle.isOn = false;
                }
                int randomShieldIndex = Random.Range(0, shieldToggles.Length);
                if (shieldToggles[randomShieldIndex] != null)
                    shieldToggles[randomShieldIndex].isOn = true;
                randomizationApplied = true;
            }

            // Skin Color randomization using the colorToggles if isSkinColor is true.
            if (isSkinColor && colorToggles != null && colorToggles.Length > 0)
            {
                foreach (Toggle toggle in colorToggles)
                {
                    if (toggle != null)
                        toggle.isOn = false;
                }
                int randomSkinIndex = Random.Range(0, colorToggles.Length);
                if (colorToggles[randomSkinIndex] != null)
                    colorToggles[randomSkinIndex].isOn = true;
                randomizationApplied = true;
            }

            // If none of the toggle types are active, perform the default clothing/gear randomization.
            if (!randomizationApplied)
            {
                if (animatorControllers.Count > 0)
                {
                    currentAnimatorIndex = Random.Range(0, animatorControllers.Count);
                    animator.runtimeAnimatorController = animatorControllers[currentAnimatorIndex];
                }
                Color randomColor = Random.ColorHSV(0f, 1f, 0f, 1f, 0f, 1f, 1f, 1f);
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = randomColor;
                }
            }
        }

        // Sets up the swatch buttons to update the currently active target(s).
        void SetupColorSwatchButtons()
        {
            foreach (Button swatchButton in colorSwatchButtons)
            {
                if (swatchButton != null && swatchButton.targetGraphic != null)
                {
                    Color swatchColor = swatchButton.targetGraphic.color;
                    
                    // Add click listener: when clicked, update the appropriate target(s).
                    swatchButton.onClick.AddListener(() =>
                    {
                        // If weapon targets are active, update them all.
                        if (currentWeaponTargets != null && currentWeaponTargets.Count > 0)
                        {
                            foreach (SpriteRenderer sr in currentWeaponTargets)
                            {
                                if (sr != null)
                                    sr.color = swatchColor;
                            }
                            currentWeaponTargets = null;
                        }
                        // Otherwise, if a single gear target is active, update it.
                        else if (currentTarget != null)
                        {
                            currentTarget.color = swatchColor;
                            currentTarget = null;
                        }
                        // Hide the swatch panel after selection.
                        globalColorSwatchPanel.SetActive(false);
                    });

                    // Set up pointer enter and exit events for showing swatch info.
                    EventTrigger trigger = swatchButton.gameObject.GetComponent<EventTrigger>();
                    if (trigger == null)
                    {
                        trigger = swatchButton.gameObject.AddComponent<EventTrigger>();
                    }

                    // Pointer Enter event: display button name and hex code.
                    EventTrigger.Entry entryEnter = new EventTrigger.Entry();
                    entryEnter.eventID = EventTriggerType.PointerEnter;
                    entryEnter.callback.AddListener((data) =>
                    {
                        if (colorInfoText != null)
                        {
                            string hexCode = "#" + ColorUtility.ToHtmlStringRGB(swatchColor);
                            colorInfoText.text = swatchButton.gameObject.name + ": " + hexCode;
                        }
                    });
                    trigger.triggers.Add(entryEnter);

                    // Pointer Exit event: clear the text.
                    EventTrigger.Entry entryExit = new EventTrigger.Entry();
                    entryExit.eventID = EventTriggerType.PointerExit;
                    entryExit.callback.AddListener((data) =>
                    {
                        if (colorInfoText != null)
                        {
                            colorInfoText.text = "";
                        }
                    });
                    trigger.triggers.Add(entryExit);
                }
            }
        }

        // Sets up the close button to hide the swatch panel.
        void SetupCloseButton()
        {
            if (closeColorPickerButton != null)
            {
                closeColorPickerButton.onClick.AddListener(CloseColorPicker);
            }
        }

        // Call this method from a gear piece’s own "Open Color Picker" button.
        // It marks that gear piece as the current target and shows the swatch panel.
        public void OpenColorPicker()
        {
            currentTarget = spriteRenderer;
            currentWeaponTargets = null; // Clear any weapon target
            if (globalColorSwatchPanel != null)
            {
                globalColorSwatchPanel.SetActive(true);
            }
        }

        // Call this method from the weapon color picker button.
        // It assigns the weapon SpriteRenderers list to the static weapon targets and opens the swatch panel.
        public void OpenWeaponColorPicker()
        {
            currentWeaponTargets = weaponSpriteRenderers;
            currentTarget = null; // Ensure the gear target is cleared.
            if (globalColorSwatchPanel != null)
            {
                globalColorSwatchPanel.SetActive(true);
            }
        }

        // Closes the color swatch panel without applying any color.
        public void CloseColorPicker()
        {
            if (globalColorSwatchPanel != null)
            {
                globalColorSwatchPanel.SetActive(false);
            }
            // Clear both targets.
            currentTarget = null;
            if (currentWeaponTargets != null)
                currentWeaponTargets = null;
        }
    }
}
