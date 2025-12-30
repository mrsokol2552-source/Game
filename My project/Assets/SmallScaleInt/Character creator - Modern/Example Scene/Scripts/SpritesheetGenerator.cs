using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System.Collections;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace SmallScaleInc.CharacterCreatorModern
    {
    public class SpritesheetGenerator : MonoBehaviour
    {
        private string parentFolder = "SmallScaleInt/Character creator - Modern/Spritesheets";
        // --- Gear Slot References (Shoes, Chest, Legs, Head) ---
        [Header("Gear Animators")]
        public Animator shoesAnimator;
        public Animator chestAnimator;
        public Animator legsAnimator;
        public Animator headAnimator;
        
        [Header("Gear Sprite Renderers")]
        public SpriteRenderer shoesRenderer;
        public SpriteRenderer chestRenderer;
        public SpriteRenderer legsRenderer;
        public SpriteRenderer headRenderer;

        [Header("Gear UI TMP (Names)")]
        public TextMeshProUGUI shoesGearNameText;
        public TextMeshProUGUI chestGearNameText;
        public TextMeshProUGUI legsGearNameText;
        public TextMeshProUGUI headGearNameText;
        
        [Header("Gear UI TMP (Colors)")]
        public TextMeshProUGUI shoesColorText;
        public TextMeshProUGUI chestColorText;
        public TextMeshProUGUI legsColorText;
        public TextMeshProUGUI headColorText;

        // --- Weapon Slot Section ---
        [System.Serializable]
        public class Weapon
        {
            public GameObject weaponGO;            // The weapon GameObject.
            public Animator animator;              // Weapon animator.
            public SpriteRenderer spriteRenderer;  // Weapon sprite renderer.
        }
        
        [Header("Weapon Setup")]
        public Weapon[] weapons;  // Array of all available weapons.
        
        [Header("Weapon UI TMP")]
        public TextMeshProUGUI weaponNameText;
        public TextMeshProUGUI weaponColorText;
        
        // --- Backpack Slot Section ---
        [System.Serializable]
        public class Backpack
        {
            public GameObject backpackGO;            // The backpack GameObject.
            public Animator animator;                // Backpack animator.
            public SpriteRenderer spriteRenderer;    // Backpack sprite renderer.
        }
        
        [Header("Backpack Setup")]
        public Backpack[] backpacks;  // Array of all available backpacks.
        
        [Header("Backpack UI TMP")]
        public TextMeshProUGUI backpackNameText;
        public TextMeshProUGUI backpackColorText;
        
        // --- Shield Slot Section ---
        [System.Serializable]
        public class Shield
        {
            public GameObject shieldGO;            // The shield GameObject.
            public Animator animator;              // Shield animator.
            public SpriteRenderer spriteRenderer;  // Shield sprite renderer.
        }
        
        [Header("Shield Setup")]
        public Shield[] shields;  // Array of all available shields.
        
        [Header("Shield UI TMP")]
        public TextMeshProUGUI shieldNameText;
        public TextMeshProUGUI shieldColorText;

        // --- Mount Slot Section ---
        [System.Serializable]
        public class Mount
        {
            public GameObject mountGO;            // The mount GameObject.
            public Animator animator;             // Mount animator.
            public SpriteRenderer spriteRenderer; // Mount sprite renderer.
        }

        [Header("Mount Setup")]
        public Mount[] mounts;  // Array of all available mounts.

        [Header("Mount UI TMP")]
        public TextMeshProUGUI mountNameText;
        public TextMeshProUGUI mountColorText;

        [Header("Skin Color Setup")]
        public SpriteRenderer skinColorRenderer;   // The skin's SpriteRenderer.
        public TextMeshProUGUI skinColorText;        // TMP to display the skin color hex.
        public Toggle skinToggle; //If false, no skin will be visable or included in the generated spritesheets. 

        [Header("Shadow Setup")]
        public Animator shadowAnimator;
        public SpriteRenderer shadowRenderer;
        public TextMeshProUGUI shadowGearNameText;
        public TextMeshProUGUI shadowColorText;

        [Header("GunFire Setup")]
        public Animator gunFireAnimator;
        public SpriteRenderer gunFireRenderer;
        public TextMeshProUGUI gunFireNameText;
        public TextMeshProUGUI gunFireColorText;

        // New toggle to enable/disable GunFire display. if true the gunfire 
        // will be included in the generated spritesheets and extra spritesheets will be generated.
        public Toggle gunFireToggle;

        [Header("Load Screen UI")]
        public GameObject loadScreenPanel;
        public Slider loadProgressSlider;
        public TextMeshProUGUI currentlyGeneratingTMP;

        public Toggle sliceSpritesheets; // If true, slice the generated spritesheets

        public Toggle staticIdleAnimation; // If on, idle animations will be made static.
        public Toggle maxFramesToggle;   // 15 frames (default)
        public Toggle fourteenFramesToggle;   // 14 frames
        public Toggle twelveFramesToggle;   // 12 frames
        public Toggle tenFramesToggle;   // 10 frames
        public Toggle eightFramesToggle; // 8 frames
        public Toggle sixFramesToggle; // 6 frames
        public Toggle fourFramesToggle;  // 4 frames

        public Toggle outlineToggle;  // If on, draw a 1px black outline around the character
        public Toggle gradientOutlineToggle;

        [Header("Outline Gradient brightness = 0 = pure inverted colour; 1 = pure white")]
        public float gradientBrightness = 0.2f;


        public Toggle use128Toggle;
        public Toggle use64Toggle;



        void Start()
        {
            UpdateGearUI();
        }

        void Update()
        {
            UpdateGearUI();
        }

        /// <summary>
        /// Updates the UI for the gear slots (Shoes, Chest, Legs, Head)
        /// by retrieving each part’s animator name and sprite color.
        /// </summary>
        public void UpdateGearUI()
        {
            // Shoes
            if (shoesAnimator != null)
                shoesGearNameText.text = GetAnimatorName(shoesAnimator);
            if (shoesRenderer != null)
                shoesColorText.text = GetColorHex(shoesRenderer.color);
            if (!shoesRenderer.enabled)
            {
                shoesGearNameText.text = "None";
                shoesColorText.text = "#000000";
            }

            // Chest
            if (chestAnimator != null)
                chestGearNameText.text = GetAnimatorName(chestAnimator);
            if (chestRenderer != null)
                chestColorText.text = GetColorHex(chestRenderer.color);
            if (!chestRenderer.enabled)
            {
                chestGearNameText.text = "None";
                chestColorText.text = "#000000";
            }

            
            // Legs
            if (legsAnimator != null)
                legsGearNameText.text = GetAnimatorName(legsAnimator);
            if (legsRenderer != null)
                legsColorText.text = GetColorHex(legsRenderer.color);
            if (!legsRenderer.enabled)
            {
                legsGearNameText.text = "None";
                legsColorText.text = "#000000";
            }
            // Head
            if (headAnimator != null)
                headGearNameText.text = GetAnimatorName(headAnimator);
            if (headRenderer != null)
                headColorText.text = GetColorHex(headRenderer.color);
            if (!headRenderer.enabled)
            {
                headGearNameText.text = "None";
                headColorText.text = "#000000";
            }

            // Shadow (special case: show alpha value instead of hex)
            if (shadowAnimator != null)
                shadowGearNameText.text = GetAnimatorName(shadowAnimator);
            if (shadowRenderer != null)
                shadowColorText.text = GetAlphaValue(shadowRenderer.color);
            if (!shadowRenderer.enabled)
            {
                shadowGearNameText.text = "None";
                shadowColorText.text = "0%";
            }

            // GunFire (with toggle control)
            if (gunFireToggle != null && !gunFireToggle.isOn)
            {
                // If toggle is off, user chose to disable GunFire.
                gunFireNameText.text = "None";
                gunFireColorText.text = "#FFFFFF";
            }
            else
            {
                // Otherwise, show GunFire normally.
                if (gunFireAnimator != null)
                    gunFireNameText.text = GetAnimatorName(gunFireAnimator);
                if (gunFireRenderer != null)
                    gunFireColorText.text = GetColorHex(gunFireRenderer.color);
                if (gunFireRenderer != null && !gunFireRenderer.enabled)
                {
                    gunFireNameText.text = "None";
                    gunFireColorText.text = "#000000";
                }
            }

            UpdateWeaponUI();
            UpdateBackpackUI();
            UpdateShieldUI();
            UpdateMountUI();
            UpdateSkinColorUI();
        }

        /// <summary>
        /// Updates the weapon UI by checking which weapon's SpriteRenderer is enabled.
        /// The enabled weapon is considered the active weapon.
        /// </summary>
        private void UpdateWeaponUI()
        {
            Weapon activeWeapon = GetActiveWeapon();
            if (activeWeapon != null)
            {
                weaponNameText.text = GetWeaponName(activeWeapon);
                weaponColorText.text = GetColorHex(activeWeapon.spriteRenderer.color);
            }
            else
            {
                weaponNameText.text = "None";
                weaponColorText.text = "#000000";
            }
        }

        /// <summary>
        /// Iterates through the weapons array to find the weapon whose SpriteRenderer is enabled.
        /// </summary>
        /// <returns>The active Weapon if found; otherwise, null.</returns>
        private Weapon GetActiveWeapon()
        {
            foreach (var weapon in weapons)
            {
                if (weapon.spriteRenderer != null && weapon.spriteRenderer.enabled)
                {
                    return weapon;
                }
            }
            return null;
        }

        /// <summary>
        /// Updates the backpack UI by checking which backpack's SpriteRenderer is enabled.
        /// The enabled backpack is considered the active backpack.
        /// </summary>
        private void UpdateBackpackUI()
        {
            Backpack activeBackpack = GetActiveBackpack();
            if (activeBackpack != null)
            {
                backpackNameText.text = GetItemName(activeBackpack.animator, activeBackpack.backpackGO);
                backpackColorText.text = GetColorHex(activeBackpack.spriteRenderer.color);
            }
            else
            {
                backpackNameText.text = "None";
                backpackColorText.text = "#000000";
            }
        }

        /// <summary>
        /// Iterates through the backpacks array to find the backpack whose SpriteRenderer is enabled.
        /// </summary>
        /// <returns>The active Backpack if found; otherwise, null.</returns>
        private Backpack GetActiveBackpack()
        {
            foreach (var backpack in backpacks)
            {
                if (backpack.spriteRenderer != null && backpack.spriteRenderer.enabled)
                {
                    return backpack;
                }
            }
            return null;
        }

        /// <summary>
        /// Updates the shield UI by checking which shield's SpriteRenderer is enabled.
        /// The enabled shield is considered the active shield.
        /// </summary>
        private void UpdateShieldUI()
        {
            Shield activeShield = GetActiveShield();
            if (activeShield != null)
            {
                shieldNameText.text = GetItemName(activeShield.animator, activeShield.shieldGO);
                shieldColorText.text = GetColorHex(activeShield.spriteRenderer.color);
            }
            else
            {
                shieldNameText.text = "None";
                shieldColorText.text = "#000000";
            }
        }

        /// <summary>
        /// Iterates through the shields array to find the shield whose SpriteRenderer is enabled.
        /// </summary>
        /// <returns>The active Shield if found; otherwise, null.</returns>
        private Shield GetActiveShield()
        {
            foreach (var shield in shields)
            {
                if (shield.spriteRenderer != null && shield.spriteRenderer.enabled)
                {
                    return shield;
                }
            }
            return null;
        }

        /// <summary>
        /// Updates the mount UI by checking which mount's SpriteRenderer is enabled.
        /// The enabled mount is considered the active mount.
        /// </summary>
        private void UpdateMountUI()
        {
            Mount activeMount = GetActiveMount();
            if (activeMount != null)
            {
                mountNameText.text = GetItemName(activeMount.animator, activeMount.mountGO);
                mountColorText.text = GetColorHex(activeMount.spriteRenderer.color);
            }
            else
            {
                mountNameText.text = "None";
                mountColorText.text = "#000000";
            }
        }

        /// <summary>
        /// Iterates through the mounts array to find the mount whose SpriteRenderer is enabled.
        /// </summary>
        /// <returns>The active Mount if found; otherwise, null.</returns>
        private Mount GetActiveMount()
        {
            foreach (var mount in mounts)
            {
                if (mount.spriteRenderer != null && mount.spriteRenderer.enabled)
                {
                    return mount;
                }
            }
            return null;
        }


        private void UpdateSkinColorUI()
        {
            if(skinColorRenderer != null)
                skinColorText.text = GetColorHex(skinColorRenderer.color);
            else
                skinColorText.text = "#000000";
        }

        /// <summary>
        /// Retrieves the animator's name based on its runtimeAnimatorController.
        /// Falls back to the animator's GameObject name if not available.
        /// </summary>
        /// <param name="animator">The animator to query.</param>
        /// <returns>The name from the animator or its runtime controller.</returns>
        private string GetAnimatorName(Animator animator)
        {
            if (animator.runtimeAnimatorController != null)
                return animator.runtimeAnimatorController.name;
            return animator.name;
        }
        
        /// <summary>
        /// Retrieves the weapon's name using its animator or GameObject.
        /// </summary>
        /// <param name="weapon">The weapon to query.</param>
        /// <returns>The name of the weapon.</returns>
        private string GetWeaponName(Weapon weapon)
        {
            if (weapon.animator != null && weapon.animator.runtimeAnimatorController != null)
                return weapon.animator.runtimeAnimatorController.name;
            return weapon.weaponGO.name;
        }
        
        /// <summary>
        /// Retrieves the item name (for backpacks or shields) using its animator or GameObject.
        /// </summary>
        /// <param name="animator">The animator to query.</param>
        /// <param name="itemGO">The item GameObject.</param>
        /// <returns>The name of the item.</returns>
        private string GetItemName(Animator animator, GameObject itemGO)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
                return animator.runtimeAnimatorController.name;
            return itemGO.name;
        }
        
        /// <summary>
        /// Converts a Unity Color to a hexadecimal string (e.g., #RRGGBB).
        /// </summary>
        /// <param name="color">The color to convert.</param>
        /// <returns>A hex string representing the color.</returns>
        private string GetColorHex(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private string GetAlphaValue(Color color)
        {
            float alphaPercent = color.a * 100f;
            return alphaPercent.ToString("F0") + "%";
        }



    /// <summary>
    /// Combines individual spritesheets for each animation into new, composite spritesheets.
    /// It assumes a folder structure under "Spritesheets" where each gear item has a folder named
    /// exactly as its stored name (from the UI) and that folder contains the 15 spritesheets.
    /// The base layer ("NakedBody") is always used (and tinted by skin color).
    /// The layering order is:
    ///  0: NakedBody (with skin color), 1: Shoes, 2: Legs, 3: Chest, 4: Shield, 5: Weapon, 6: Backpack, 7: Head.
    /// The resulting combined spritesheets are saved into a new folder so as not to overwrite any originals.
    /// </summary>

//Works, DontChange

      public void StartCombineSpritesheets()
    {
        StartCoroutine(CombineCharacterSpritesheetsCoroutine());
    }

    /// <summary>
    /// Combines individual spritesheets for each animation into new composite spritesheets.
    /// Layering order:
    /// 0: Shadow (tinted by its alpha),
    /// 1: NakedBody (tinted by skin color; skipped if skinToggle is off),
    /// 2: Shoes,
    /// 3: Legs,
    /// 4: Chest,
    /// 5: Shield,
    /// 6: Weapon,
    /// 7: Backpack,
    /// 8: Head.
    /// Additionally, if GunFire is enabled (and weaponNameText does not start with "Melee", "Special2", or "None"):
    /// • For standard animations: if anim == "Attack1", overlay GunFire loaded from "Attack1.png".
    /// • Extra animations are generated:
    ///    - For Run/RunBackwards/StrafeLeft/StrafeRight, extra spritesheets named "RunAttack", etc.
    ///    - For CrouchIdle/CrouchRun, extra spritesheets named "CrouchAttack" and "CrouchRunAttack".
    /// Each extra animation overlays the GunFire texture loaded from the corresponding file (e.g., "Run.png", "CrouchIdle.png").
    /// </summary>
private IEnumerator CombineCharacterSpritesheetsCoroutine()
{
    // Activate load screen and reset progress.
    if (loadScreenPanel != null)
    {
        loadScreenPanel.SetActive(true);
        if (loadProgressSlider != null)
            loadProgressSlider.value = 0f;
    }
    if (currentlyGeneratingTMP != null)
        currentlyGeneratingTMP.text = "Starting spritesheet generation...";

    // Define standard animation names.
    string[] animations = new string[] {
        "Attack1", "Attack2", "Attack3", "Attack4",
        "CrouchIdle", "CrouchRun", "Die", "Idle", "Idle2", "Idle3",
        "Run", "RunBackwards", "StrafeLeft", "StrafeRight", "Walk",
        "TakeDamage", "Taunt"
    };

    // Get folder names from UI.
    string shoesFolder    = shoesGearNameText.text;
    string legsFolder     = legsGearNameText.text;
    string chestFolder    = chestGearNameText.text;
    string headFolder     = headGearNameText.text;
    string weaponFolder   = weaponNameText.text;
    string backpackFolder = backpackNameText.text;
    string shieldFolder   = shieldNameText.text;
    string mountFolder    = mountNameText.text;        // ← new mount slot
    string nakedBodyFolder = "NakedBody";
    string shadowFolder   = shadowGearNameText.text;

    // Setup paths…
    parentFolder = Path.Combine(Application.dataPath, "SmallScaleInt/Character creator - Modern/Spritesheets");
    string outputParent = Path.Combine(Application.dataPath, "SmallScaleInt/Character creator - Modern/Created Spritesheets");
    if (!Directory.Exists(outputParent))
        Directory.CreateDirectory(outputParent);
    string configFolder = Path.Combine(outputParent, "Character_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
    Directory.CreateDirectory(configFolder);

    // GunFire setup…
    bool includeGunFire = (gunFireToggle != null && gunFireToggle.isOn &&
                           !weaponNameText.text.StartsWith("Melee") &&
                           !weaponNameText.text.StartsWith("Special2") &&
                           !weaponNameText.text.StartsWith("None"));
    Color gunFireTint = includeGunFire ? ParseColorFromTMP(gunFireColorText.text) : Color.white;

    int totalAnimations = animations.Length;
    int processedAnimations = 0;

    // --- Standard animations loop ---
    foreach (string anim in animations)
    {
        if (currentlyGeneratingTMP != null)
            currentlyGeneratingTMP.text = "Generating " + anim + " spritesheet...";

        // Load naked body (for size) and shadow
        Texture2D nakedBodyTex = LoadTexture(Path.Combine(parentFolder, nakedBodyFolder, anim + ".png"));
        if (nakedBodyTex == null)
        {
            Debug.LogError("Missing NakedBody spritesheet for animation: " + anim);
            processedAnimations++;
            if (loadProgressSlider != null)
                loadProgressSlider.value = (float)processedAnimations / totalAnimations;
            yield return null;
            continue;
        }
        Texture2D shadowTex = (shadowFolder == "None")
            ? null
            : LoadTexture(Path.Combine(parentFolder, shadowFolder, anim + ".png"));
        float shadowAlpha = ParseAlphaPercentage(shadowColorText.text);

        // Load all gear layers (including new mount)
        Texture2D shoesTex     = (shoesFolder == "None")    ? null : LoadTexture(Path.Combine(parentFolder, shoesFolder, anim + ".png"));
        Texture2D legsTex      = (legsFolder == "None")     ? null : LoadTexture(Path.Combine(parentFolder, legsFolder, anim + ".png"));
        Texture2D mountTex     = (mountFolder == "None")    ? null : LoadTexture(Path.Combine(parentFolder, mountFolder, anim + ".png"));  // ← mount
        Texture2D chestTex     = (chestFolder == "None")    ? null : LoadTexture(Path.Combine(parentFolder, chestFolder, anim + ".png"));
        Texture2D shieldTex    = (shieldFolder == "None")   ? null : LoadTexture(Path.Combine(parentFolder, shieldFolder, anim + ".png"));
        Texture2D weaponTex    = (weaponFolder == "None")   ? null : LoadTexture(Path.Combine(parentFolder, weaponFolder, anim + ".png"));
        Texture2D backpackTex  = (backpackFolder == "None") ? null : LoadTexture(Path.Combine(parentFolder, backpackFolder, anim + ".png"));
        Texture2D headTex      = (headFolder == "None")     ? null : LoadTexture(Path.Combine(parentFolder, headFolder, anim + ".png"));

        // Prepare tints
        Color shoesTint   = shoesRenderer   != null ? shoesRenderer.color   : Color.white;
        Color legsTint    = legsRenderer    != null ? legsRenderer.color    : Color.white;
        Color mountTint   = ParseColorFromTMP(mountColorText.text);   // ← mount tint
        Color chestTint   = chestRenderer   != null ? chestRenderer.color   : Color.white;
        Color shieldTint  = ParseColorFromTMP(shieldColorText.text);
        Color weaponTint  = ParseColorFromTMP(weaponColorText.text);
        Color backpackTint= ParseColorFromTMP(backpackColorText.text);
        Color headTint    = headRenderer    != null ? headRenderer.color    : Color.white;

        // Build layer arrays in your desired order
        Texture2D[] layers = new Texture2D[] {
            shoesTex, legsTex, mountTex, chestTex,
            shieldTex, weaponTex, backpackTex, headTex
        };
        Color[] layerTints = new Color[] {
            shoesTint, legsTint, mountTint, chestTint,
            shieldTint, weaponTint, backpackTint, headTint
        };

        // Allocate two buffers:
        int W = nakedBodyTex.width, H = nakedBodyTex.height;
        Color[] finalPixels    = new Color[W * H]; // starts with shadow
        Color[] bodyGearPixels = new Color[W * H]; // will hold nakedBody+gear

        // 1) Shadow → finalPixels
        if (shadowTex != null)
        {
            var sp = shadowTex.GetPixels();
            for (int i = 0; i < sp.Length; i++)
            {
                sp[i].a *= shadowAlpha;
                finalPixels[i] = sp[i];
            }
        }
        else
        {
            for (int i = 0; i < finalPixels.Length; i++)
                finalPixels[i] = new Color(0,0,0,0);
        }

        // 2) NakedBody → bodyGearPixels
        if (skinToggle == null || skinToggle.isOn)
        {
            var nb = nakedBodyTex.GetPixels();
            Color skinCol = skinColorRenderer != null ? skinColorRenderer.color : Color.white;
            for (int i = 0; i < nb.Length; i++)
                bodyGearPixels[i] = nb[i] * skinCol;
        }

        // 3) Gear layers → bodyGearPixels
        for (int li = 0; li < layers.Length; li++)
        {
            var L = layers[li];
            if (L == null) continue;
            var pix = L.GetPixels();
            Color tint = layerTints[li];
            for (int i = 0; i < pix.Length; i++)
                pix[i] = pix[i] * tint;
            for (int i = 0; i < bodyGearPixels.Length; i++)
                bodyGearPixels[i] = AlphaBlend(bodyGearPixels[i], pix[i]);
        }

// 1) Shadow → finalPixels
// 2) NakedBody → bodyGearPixels
// 3) Gear layers → bodyGearPixels
// 4) Composite body+gear → finalPixels (but DON'T add gunfire yet)

for (int i = 0; i < finalPixels.Length; i++)
    finalPixels[i] = AlphaBlend(finalPixels[i], bodyGearPixels[i]);

// --- SAVE a copy of bodyGearPixels for outlining ---
// (before touching with gunfire!)
Color[] bodyGearCopy = (Color[])bodyGearPixels.Clone();

// 5) GunFire overlay (only on Attack1)
if (includeGunFire && anim == "Attack1")
{
    var gf = LoadTexture(Path.Combine(parentFolder, gunFireNameText.text, anim + ".png"));
    if (gf != null)
    {
        var gfp = gf.GetPixels();
        for (int i = 0; i < gfp.Length; i++)
            gfp[i] = gfp[i] * gunFireTint;
        for (int i = 0; i < finalPixels.Length; i++)
            finalPixels[i] = AlphaBlend(finalPixels[i], gfp[i]);
    }
}

// 6) Create Texture2D
Texture2D outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
outTex.SetPixels(finalPixels);
outTex.Apply();

// 7) Static idle tweak (optional)
if (staticIdleAnimation != null && staticIdleAnimation.isOn &&
    (anim == "Idle" || anim == "Idle2" || anim == "Idle3" || anim == "CrouchIdle"))
{
    outTex = MakeTextureStatic(outTex, 15, 8);
}

// 8) Resize (if needed)
if (use64Toggle != null && use64Toggle.isOn)
{
    outTex = ResizeTexturePixelPerfect(outTex, outTex.width / 2, outTex.height / 2);
}

// 9) FINAL: Apply outline (based on bodyGearCopy, not full final image!)
if (gradientOutlineToggle != null && gradientOutlineToggle.isOn)
{
    Color[] pixels = outTex.GetPixels();
    Color[] resizedBodyGear = ResizePixelsPixelPerfect(bodyGearCopy, W, H, outTex.width, outTex.height);

    Color[] outlinedPixels = AddGradientOutline(pixels, resizedBodyGear, outTex.width, outTex.height, gradientBrightness);
    outTex.SetPixels(outlinedPixels);
    outTex.Apply();
}
else if (outlineToggle != null && outlineToggle.isOn)
{
    Color[] pixels = outTex.GetPixels();
    Color[] resizedBodyGear = ResizePixelsPixelPerfect(bodyGearCopy, W, H, outTex.width, outTex.height);

    Color[] outlinedPixels = AddOutlineMask(pixels, resizedBodyGear, outTex.width, outTex.height, Color.black);
    outTex.SetPixels(outlinedPixels);
    outTex.Apply();
}


        string outputPath = Path.Combine(configFolder, anim + ".png");
        File.WriteAllBytes(outputPath, outTex.EncodeToPNG());
        Debug.Log($"Saved combined spritesheet for {anim} at {outputPath}");

        processedAnimations++;
        if (loadProgressSlider != null)
            loadProgressSlider.value = (float)processedAnimations / totalAnimations;
        yield return null;
    }

    // --- Extra Attack Animations with GunFire ---
    if (includeGunFire)
    {
        string[] extraAnimBases = { "Run", "RunBackwards", "StrafeLeft", "StrafeRight" };
        string[] extraAnimNames = { "RunAttack", "RunBackwardsAttack", "StrafeLeftAttack", "StrafeRightAttack" };

        for (int j = 0; j < extraAnimBases.Length; j++)
        {
            string baseAnim   = extraAnimBases[j];
            string newAnimName = extraAnimNames[j];
            if (currentlyGeneratingTMP != null)
                currentlyGeneratingTMP.text = "Generating " + newAnimName + " spritesheet...";

            // 1) Load nakedBody & shadow
            Texture2D nakedBodyTex = LoadTexture(Path.Combine(parentFolder, nakedBodyFolder, baseAnim + ".png"));
            if (nakedBodyTex == null)
            {
                Debug.LogError("Missing NakedBody spritesheet for: " + baseAnim);
                continue;
            }
            Texture2D shadowTex = (shadowFolder == "None")
                ? null
                : LoadTexture(Path.Combine(parentFolder, shadowFolder, baseAnim + ".png"));
            float shadowAlpha = ParseAlphaPercentage(shadowColorText.text);

            // 2) Load all gear layers
            Texture2D[] layers = new Texture2D[]
            {
                LoadTextureOrNull(shoesFolder,    baseAnim),
                LoadTextureOrNull(legsFolder,     baseAnim),
                LoadTextureOrNull(chestFolder,    baseAnim),
                LoadTextureOrNull(shieldFolder,   baseAnim),
                LoadTextureOrNull(weaponFolder,   baseAnim),
                LoadTextureOrNull(backpackFolder, baseAnim),
                LoadTextureOrNull(headFolder,     baseAnim)
            };
            Color[] layerTints = new Color[]
            {
                shoesRenderer.color,
                legsRenderer.color,
                chestRenderer.color,
                ParseColorFromTMP(shieldColorText.text),
                ParseColorFromTMP(weaponColorText.text),
                ParseColorFromTMP(backpackColorText.text),
                headRenderer.color
            };

            int W = nakedBodyTex.width, H = nakedBodyTex.height;
            Color[] finalPixels    = new Color[W*H];
            Color[] bodyGearPixels = new Color[W*H];

            // --- Shadow into finalPixels ---
            if (shadowTex != null)
            {
                var sp = shadowTex.GetPixels();
                for (int i = 0; i < sp.Length; i++)
                {
                    sp[i].a *= shadowAlpha;
                    finalPixels[i] = sp[i];
                }
            }

            // --- Build body+gear in bodyGearPixels ---
            // NakedBody
            Color[] nb = nakedBodyTex.GetPixels();
            Color skinCol = (skinToggle == null || skinToggle.isOn)
                ? skinColorRenderer.color
                : new Color(0,0,0,0);
            for (int i = 0; i < nb.Length; i++)
                bodyGearPixels[i] = nb[i] * skinCol;

            // Gear layers
            for (int li = 0; li < layers.Length; li++)
            {
                var L = layers[li];
                if (L == null) continue;
                var pix = L.GetPixels();
                for (int i = 0; i < pix.Length; i++)
                    pix[i] *= layerTints[li];
                for (int i = 0; i < bodyGearPixels.Length; i++)
                    bodyGearPixels[i] = AlphaBlend(bodyGearPixels[i], pix[i]);
            }

            // --- Outline on body+gear only ---
        if (gradientOutlineToggle != null && gradientOutlineToggle.isOn)
        {
            finalPixels = AddGradientOutline(finalPixels, bodyGearPixels, W, H, gradientBrightness);
        }
        else if (outlineToggle != null && outlineToggle.isOn)
        {
            finalPixels = AddOutlineMask(finalPixels, bodyGearPixels, W, H, Color.black);
        }


            // --- Composite body+gear onto finalPixels ---
            for (int i = 0; i < finalPixels.Length; i++)
                finalPixels[i] = AlphaBlend(finalPixels[i], bodyGearPixels[i]);

            // --- GunFire overlay ---
            Texture2D gfTex = LoadTexture(Path.Combine(parentFolder, gunFireNameText.text, baseAnim + ".png"));
            if (gfTex != null)
            {
                var gfp = gfTex.GetPixels();
                for (int i = 0; i < gfp.Length; i++)
                    gfp[i] *= gunFireTint;
                for (int i = 0; i < finalPixels.Length; i++)
                    finalPixels[i] = AlphaBlend(finalPixels[i], gfp[i]);
            }

            // --- Save ---
            Texture2D outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            outTex.SetPixels(finalPixels);
            outTex.Apply();

            if (use64Toggle != null && use64Toggle.isOn)
            {
                outTex = ResizeTexturePixelPerfect(outTex, outTex.width / 2, outTex.height / 2);
            }


            string outPath = Path.Combine(configFolder, newAnimName + ".png");
            File.WriteAllBytes(outPath, outTex.EncodeToPNG());
            Debug.Log("Saved extra spritesheet for " + newAnimName + " at " + outPath);

            processedAnimations++;
            if (loadProgressSlider != null)
                loadProgressSlider.value = (float)processedAnimations / (totalAnimations + extraAnimBases.Length);
            yield return null;
        }
    }


    // --- Extra Ride Animations with GunFire & Outline ---
    if (!string.IsNullOrEmpty(mountNameText.text) && mountNameText.text != "None")
    {
        string[] rideAnims = { "RideIdle", "RideRun" };

        foreach (var rideAnim in rideAnims)
        {
            if (currentlyGeneratingTMP != null)
                currentlyGeneratingTMP.text = "Generating " + rideAnim + " spritesheet...";

            // 1) Load naked body + shadow
            Texture2D nakedBodyTex = LoadTexture(Path.Combine(parentFolder, nakedBodyFolder, rideAnim + ".png"));
            if (nakedBodyTex == null)
            {
                Debug.LogError("Missing NakedBody for: " + rideAnim);
                continue;
            }
            Texture2D shadowTex = shadowFolder == "None"
                ? null
                : LoadTexture(Path.Combine(parentFolder, shadowFolder, rideAnim + ".png"));
            float shadowAlpha = ParseAlphaPercentage(shadowColorText.text);

            // 2) Load gear layers (including mount)
            Texture2D[] layers = new Texture2D[]
            {
                LoadTextureOrNull(shoesFolder,    rideAnim),
                LoadTextureOrNull(legsFolder,     rideAnim),
                LoadTextureOrNull(mountNameText.text, rideAnim),
                LoadTextureOrNull(chestFolder,    rideAnim),
                LoadTextureOrNull(shieldFolder,   rideAnim),
                LoadTextureOrNull(weaponFolder,   rideAnim),
                LoadTextureOrNull(backpackFolder, rideAnim),
                LoadTextureOrNull(headFolder,     rideAnim)
            };
            Color[] layerTints = new Color[]
            {
                shoesRenderer.color,
                legsRenderer.color,
                ParseColorFromTMP(mountColorText.text),
                chestRenderer.color,
                ParseColorFromTMP(shieldColorText.text),
                ParseColorFromTMP(weaponColorText.text),
                ParseColorFromTMP(backpackColorText.text),
                headRenderer.color
            };

            int W = nakedBodyTex.width, H = nakedBodyTex.height;
            Color[] finalPixels    = new Color[W * H];
            Color[] bodyGearPixels = new Color[W * H];

            // --- Shadow →
            if (shadowTex != null)
            {
                var sp = shadowTex.GetPixels();
                for (int i = 0; i < sp.Length; i++)
                {
                    sp[i].a *= shadowAlpha;
                    finalPixels[i] = sp[i];
                }
            }

            // --- Build body+gear →
            // NakedBody
            var nb = nakedBodyTex.GetPixels();
            var skinCol = (skinToggle == null || skinToggle.isOn)
                ? skinColorRenderer.color
                : new Color(0,0,0,0);
            for (int i = 0; i < nb.Length; i++)
                bodyGearPixels[i] = nb[i] * skinCol;

            // Gear
            for (int li = 0; li < layers.Length; li++)
            {
                var L = layers[li];
                if (L == null) continue;
                var pix = L.GetPixels();
                for (int i = 0; i < pix.Length; i++)
                    pix[i] *= layerTints[li];
                for (int i = 0; i < bodyGearPixels.Length; i++)
                    bodyGearPixels[i] = AlphaBlend(bodyGearPixels[i], pix[i]);
            }

            // --- Outline on body+gear only →
        if (gradientOutlineToggle != null && gradientOutlineToggle.isOn)
        {
            finalPixels = AddGradientOutline(finalPixels, bodyGearPixels, W, H, gradientBrightness);
        }
        else if (outlineToggle != null && outlineToggle.isOn)
        {
            finalPixels = AddOutlineMask(finalPixels, bodyGearPixels, W, H, Color.black);
        }

            // --- Composite body+gear over shadow →
            for (int i = 0; i < finalPixels.Length; i++)
                finalPixels[i] = AlphaBlend(finalPixels[i], bodyGearPixels[i]);

            // --- GunFire overlay (if any) →
            if (includeGunFire)
            {
                Texture2D gf = LoadTexture(Path.Combine(parentFolder, gunFireNameText.text, rideAnim + ".png"));
                if (gf != null)
                {
                    var gfp = gf.GetPixels();
                    for (int i = 0; i < gfp.Length; i++)
                        gfp[i] *= gunFireTint;
                    for (int i = 0; i < finalPixels.Length; i++)
                        finalPixels[i] = AlphaBlend(finalPixels[i], gfp[i]);
                }
            }

            // --- Save out →
            Texture2D outTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            outTex.SetPixels(finalPixels);
            outTex.Apply();

            if (use64Toggle != null && use64Toggle.isOn)
            {
                outTex = ResizeTexturePixelPerfect(outTex, outTex.width / 2, outTex.height / 2);
            }


            string path = Path.Combine(configFolder, rideAnim + ".png");
            File.WriteAllBytes(path, outTex.EncodeToPNG());
            Debug.Log("Saved extra ride spritesheet: " + rideAnim);

            processedAnimations++;
            if (loadProgressSlider != null)
                loadProgressSlider.value = (float)processedAnimations / (totalAnimations + rideAnims.Length);
            yield return null;
        }
    }

    if (loadProgressSlider != null)
        loadProgressSlider.value = 1f;
    if (currentlyGeneratingTMP != null)
        currentlyGeneratingTMP.text = "Generation complete!";
    yield return new WaitForSeconds(0.5f);
    if (loadScreenPanel != null)
        loadScreenPanel.SetActive(false);

    #if UNITY_EDITOR
    if (sliceSpritesheets != null && sliceSpritesheets.isOn)
    {
        AssetDatabase.Refresh();
        string[] files = Directory.GetFiles(configFolder, "*.png");
        foreach (string file in files)
        {
            string assetPath = "Assets" + file.Substring(Application.dataPath.Length);
            TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.spriteImportMode = SpriteImportMode.Multiple;
                ti.spritePixelsPerUnit = 100;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;

                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (tex != null)
                {
                    int columns = 15;
                    int rows = 8;
                    float sliceWidth = tex.width / (float)columns;
                    float sliceHeight = tex.height / (float)rows;
                    SpriteMetaData[] metaData = new SpriteMetaData[columns * rows];
                    for (int y = 0; y < rows; y++)
                    {
                        for (int x = 0; x < columns; x++)
                        {
                            SpriteMetaData smd = new SpriteMetaData();
                            smd.name = Path.GetFileNameWithoutExtension(assetPath) + "_" + y + "_" + x;
                            smd.rect = new Rect(x * sliceWidth, y * sliceHeight, sliceWidth, sliceHeight);
                            smd.pivot = new Vector2(0.5f, 0.5f);
                            metaData[y * columns + x] = smd;
                        }
                    }
                    ti.spritesheet = metaData;
                }
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }
        }
    }
    AssetDatabase.Refresh();
    ApplyFrameCountOption(configFolder);
    #endif

    yield break;
}

private Color[] ResizePixelsPixelPerfect(Color[] original, int origWidth, int origHeight, int newWidth, int newHeight)
{
    Color[] result = new Color[newWidth * newHeight];
    for (int y = 0; y < newHeight; y++)
    {
        for (int x = 0; x < newWidth; x++)
        {
            int origX = x * 2;
            int origY = y * 2;
            if (origX >= origWidth) origX = origWidth - 1;
            if (origY >= origHeight) origY = origHeight - 1;
            result[y * newWidth + x] = original[origY * origWidth + origX];
        }
    }
    return result;
}


private Texture2D ResizeTexturePixelPerfect(Texture2D source, int newWidth, int newHeight)
{
    Texture2D result = new Texture2D(newWidth, newHeight, source.format, false);
    for (int y = 0; y < newHeight; y++)
    {
        for (int x = 0; x < newWidth; x++)
        {
            // Calculate the source pixel to sample
            int srcX = Mathf.FloorToInt(x * (source.width / (float)newWidth));
            int srcY = Mathf.FloorToInt(y * (source.height / (float)newHeight));
            Color pixel = source.GetPixel(srcX, srcY);
            result.SetPixel(x, y, pixel);
        }
    }
    result.filterMode = FilterMode.Point; // Important: keep pixels sharp
    result.Apply();
    return result;
}


// Helper to avoid boilerplate:
private Texture2D LoadTextureOrNull(string folder, string animName)
{
    return (folder == "None")
        ? null
        : LoadTexture(Path.Combine(parentFolder, folder, animName + ".png"));
}
    
    
  private Texture2D LoadTexture(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("File not found: " + filePath);
            return null;
        }
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (tex.LoadImage(fileData))
            return tex;
        return null;
    }

    private Color ParseColorFromTMP(string hex)
    {
        Color parsedColor = Color.white;
        if (!string.IsNullOrEmpty(hex))
        {
            ColorUtility.TryParseHtmlString(hex, out parsedColor);
        }
        return parsedColor;
    }

    private float ParseAlphaPercentage(string percentageString)
    {
        if (string.IsNullOrEmpty(percentageString))
            return 1f;
        percentageString = percentageString.Trim();
        if (percentageString.EndsWith("%"))
            percentageString = percentageString.Substring(0, percentageString.Length - 1);
        if (float.TryParse(percentageString, out float percent))
            return Mathf.Clamp01(percent / 100f);
        return 1f;
    }

    private Color AlphaBlend(Color bottom, Color top)
    {
        float alpha = top.a;
        return top * alpha + bottom * (1f - alpha);
    }

    private Texture2D MakeTextureStatic(Texture2D original, int columns, int rows)
    {
        int tileWidth = original.width / columns;
        int tileHeight = original.height / rows;
        Color[] origPixels = original.GetPixels();
        Color[] newPixels = new Color[original.width * original.height];

        for (int row = 0; row < rows; row++)
        {
            for (int y = 0; y < tileHeight; y++)
            {
                int globalY = row * tileHeight + y;
                for (int x = 0; x < tileWidth; x++)
                {
                    int srcIndex = globalY * original.width + x;
                    Color staticColor = origPixels[srcIndex];
                    for (int col = 0; col < columns; col++)
                    {
                        int globalX = col * tileWidth + x;
                        int destIndex = globalY * original.width + globalX;
                        newPixels[destIndex] = staticColor;
                    }
                }
            }
        }
        Texture2D staticTexture = new Texture2D(original.width, original.height, original.format, false);
        staticTexture.SetPixels(newPixels);
        staticTexture.Apply();
        return staticTexture;
    }

private Texture2D PackSpritesheet(Texture2D original, int targetColumns)
{
    int originalColumns = 15;
    int rows = 8;
    int tileWidth = original.width / originalColumns;
    int tileHeight = original.height / rows;

    int[] mapping;
    if (targetColumns == 15)
    {
        mapping = new int[15];
        for (int i = 0; i < 15; i++) mapping[i] = i;
    }
    else if (targetColumns == 14)
    {
        // Remove columns 3,6,9,12,15 (1-indexed) → indices 2,5,8,11,14.
        mapping = new int[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13 };
    }
    else if (targetColumns == 12)
    {
        // Remove columns 3,6,9,12,15 (1-indexed) → indices 2,5,8,11,14.
        mapping = new int[] { 0, 1, 2, 3, 4, 6, 7, 8, 9, 10, 12, 13 };
    }
    else if (targetColumns == 10)
    {
        // Remove columns 3,6,9,12,15 (1-indexed) → indices 2,5,8,11,14.
        mapping = new int[] { 0, 1, 3, 4, 6, 7, 9, 10, 12, 13 };
    }
    else if (targetColumns == 8)
    {
        // Keep columns 1,3,5,7,9,11,13,15 (1-indexed) → indices 0,2,4,6,8,10,12,14.
        mapping = new int[] { 0, 2, 4, 6, 8, 10, 12, 14 };
    }
    else if (targetColumns == 6)
    {
        // Keep columns 1,3,5,7,9,11,13,15 (1-indexed) → indices 0,2,4,6,8,10,12,14.
        mapping = new int[] { 0, 3, 6, 9, 12, 14 };
    }
    else if (targetColumns == 4)
    {
        // Keep only columns 1,5,9,13 (1-indexed) → indices 0,5,10,14.
        mapping = new int[] { 0, 4, 8, 12 };
    }
    else
    {
        Debug.LogError("Unsupported targetColumns: " + targetColumns);
        return original;
    }

    int newWidth = tileWidth * targetColumns;
    int newHeight = tileHeight * rows;
    // Create the new texture explicitly in RGBA32 format.
    Texture2D packed = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);

    for (int row = 0; row < rows; row++)
    {
        for (int newCol = 0; newCol < targetColumns; newCol++)
        {
            int origCol = mapping[newCol];
            int origX = origCol * tileWidth;
            int origY = row * tileHeight;
            Color[] tilePixels = original.GetPixels(origX, origY, tileWidth, tileHeight);
            int newX = newCol * tileWidth;
            int newY = row * tileHeight;
            packed.SetPixels(newX, newY, tileWidth, tileHeight, tilePixels);
        }
    }
    packed.Apply();
    return packed;
}

    private void ApplyFrameCountOption(string configFolder)
    {
        #if UNITY_EDITOR
        int targetColumns = 15; // default
        if (fourteenFramesToggle != null && fourteenFramesToggle.isOn) targetColumns = 14;
        else if (twelveFramesToggle != null && twelveFramesToggle.isOn) targetColumns = 12;
        else if (tenFramesToggle != null && tenFramesToggle.isOn) targetColumns = 10;
        else if (eightFramesToggle != null && eightFramesToggle.isOn) targetColumns = 8;
        else if (sixFramesToggle != null && sixFramesToggle.isOn) targetColumns = 6;
        else if (fourFramesToggle != null && fourFramesToggle.isOn) targetColumns = 4;
        Debug.Log("Target columns for packing: " + targetColumns);
        
        if (targetColumns < 15)
        {
            string[] files = Directory.GetFiles(configFolder, "*.png");
            foreach (string file in files)
            {
                string assetPath = "Assets" + file.Substring(Application.dataPath.Length);
                TextureImporter ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (ti != null)
                {
                    ti.isReadable = true;
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                    Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    if (tex != null)
                    {
                        Texture2D packed = PackSpritesheet(tex, targetColumns);
                        byte[] pngData = packed.EncodeToPNG();
                        File.WriteAllBytes(file, pngData);
                    }
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }
            }
        }
        #endif
    }

    /// <summary>
    /// Takes a flat RGBA pixel array plus its dimensions
    /// and returns a new array where any transparent
    /// neighbor of an opaque pixel gets painted with
    /// outlineColor (a single‑pixel orthogonal dilation).
    /// </summary>
    private Color[] AddOutline(Color[] pixels, int width, int height, Color outlineColor)
    {
        Color[] result = (Color[])pixels.Clone();
        // Build a mask of where the character lives
        bool[] mask = new bool[pixels.Length];
        for (int i = 0; i < pixels.Length; i++)
            mask[i] = pixels[i].a > 0f;

        // Offsets for 4‑neighborhood:
        int[] dx = { -1, 1, 0, 0 };
        int[] dy = { 0, 0, -1, 1 };

        // For each pixel that is part of the character:
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (!mask[idx]) continue;

                // Check each neighbor:
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k];
                    int ny = y + dy[k];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height)
                        continue;

                    int nIdx = ny * width + nx;
                    if (!mask[nIdx])
                    {
                        // Paint an outline pixel
                        result[nIdx] = outlineColor;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Paints a 1px black outline in finalPixels around any body pixel
    /// in bodyPixels, but ignores the shadow that’s already in finalPixels.
    /// </summary>
    private Color[] AddOutlineMask(Color[] finalPixels,
                                Color[] bodyPixels,
                                int width, int height,
                                Color outlineColor)
    {
        bool[] mask = new bool[bodyPixels.Length];
        for (int i = 0; i < mask.Length; i++)
            mask[i] = bodyPixels[i].a > 0f;

        int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                if (!mask[idx]) continue;

                // for each 4‑neighbor:
                for (int k = 0; k < 4; k++)
                {
                    int nx = x + dx[k], ny = y + dy[k];
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    int nIdx = ny * width + nx;
                    if (!mask[nIdx])
                        finalPixels[nIdx] = outlineColor;
                }
            }
        }

        return finalPixels;
    }
    /// <summary>
/// Like AddOutlineMask, but each outline pixel is colored by
/// taking the nearest opaque body pixel and darkening it.
/// </summary>
public float redColor   = 0.5f;
public float greenColor = 0.5f;
public float blueColor  = 0.5f;

/// <summary>
/// Paints a 1px outline around any body pixel, sampling the nearest body pixel colour,
/// inverting it, and then lightening it toward white by lightenFactor.
/// finalPixels[] already contains shadow; bodyPixels[] is your nakedBody+gear composite.
/// </summary>
private Color[] AddGradientOutline(
    Color[] finalPixels,
    Color[] bodyPixels,
    int width,
    int height,
    float lightenFactor = 0.5f  // 0 = pure inverted colour; 1 = pure white
)
{
    Color[] result = (Color[])finalPixels.Clone();

    // build mask of where body exists
    bool[] mask = new bool[bodyPixels.Length];
    for (int i = 0; i < mask.Length; i++)
        mask[i] = bodyPixels[i].a > 0f;

    // 4‑neighbour offsets
    int[] dx = { -1, 1, 0, 0 }, dy = { 0, 0, -1, 1 };

    // for every body pixel, paint its transparent neighbours
    for (int y = 0; y < height; y++)
    {
        for (int x = 0; x < width; x++)
        {
            int idx = y * width + x;
            if (!mask[idx]) continue;

            // sample the body pixel colour
            Color src = bodyPixels[idx];

            // invert it
            Color inverted = new Color(1f - src.r, 1f - src.g, 1f - src.b, 1f);

            // lighten toward white
            Color outlineCol = Color.Lerp(inverted, Color.white, lightenFactor);

            // paint 1‑px thick outline
            for (int k = 0; k < 4; k++)
            {
                int nx = x + dx[k], ny = y + dy[k];
                if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                int nIdx = ny * width + nx;
                if (!mask[nIdx])
                {
                    result[nIdx] = outlineCol;
                }
            }
        }
    }

    return result;
}





    }
}