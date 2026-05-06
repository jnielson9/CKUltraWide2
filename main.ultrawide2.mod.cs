using HarmonyLib;
using Pug.RP;
using PugMod;
using System.Collections.Generic;
using Unity.Mathematics;

namespace jnielson9
{

    [HarmonyPatch(typeof(SendClientSubMapToPugMapSystem))]
    static class SendClientSubMapToPugMapSystem_Patch
    {
        static readonly System.Reflection.ConstructorInfo Int2Ctor =
             typeof(int2).GetConstructor(new[] { typeof(int), typeof(int) });

        static readonly System.Reflection.MethodInfo HorizontalScaleMethod =
            typeof(SendClientSubMapToPugMapSystem_Patch)
                .GetMethod(nameof(ScaleHorizontalVisibilityView),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        [HarmonyPatch("ComputeShouldBeViewedMemo")]
        [HarmonyTranspiler]

        static IEnumerable<CodeInstruction> ComputeShouldBeViewedMemo(IEnumerable<CodeInstruction> instructions)
        {
            // Find instructions resembling `int2 @int = new int2(20, 16);`
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchStartForward(
                new CodeMatch(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)20),
                new CodeMatch(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)16),
                new CodeMatch(System.Reflection.Emit.OpCodes.Call, Int2Ctor)
            );

            if (!codeMatcher.IsValid)
            {
                throw new System.Exception("jnielson9.UltraWide2: Could not patch ComputeShouldBeViewedMemo, missing IL reference for view frustrum");
            }

            // Skip instruction to pass default horizontal size to`HorizontalScaleMethod`
            // return new value and continue the default flow 
            codeMatcher.Advance(1);
            codeMatcher.InsertAndAdvance(
                new CodeInstruction(System.Reflection.Emit.OpCodes.Call, HorizontalScaleMethod)
            );

            return codeMatcher.Instructions();
        }

        public static int ScaleHorizontalVisibilityView(int x)
        {
            var cam = API.Rendering.GameCamera;
            if (cam == null || cam.outputWidth == 0)
                return x;

            return (int)(x * ((float)cam.GetPixelWidth() / cam.outputWidth));
        }
    }

    [System.Serializable]
    public class UltraWide2Config
    {
        // "safe" leaves a 4-tile streaming margin per side (clean edges, slight
        // letterboxing at 32:9). "full" pushes the view to the full 32:9 limit
        // (max horizontal real estate, brief tile artifacts at edges on movement).
        public string mode = "safe";

        // Allow advanced users to specify exactly how many tiles of margin the
        // server's 64-wide subscription region should keep on each side. Only
        // honored when mode == "safe". Larger = cleaner edges, more letterboxing.
        public int safeMarginTiles = 4;
    }

    public class UltraWide2 : IMod
    {
        // 32:9 ≈ 3.5556. Hard structural ceiling: at this aspect the world view
        // is ~60 tiles wide, and the engine's 64-wide subscription region only has
        // 2-tile margin. Anything wider would bleed past streamed terrain.
        private const float ASPECT_32_9 = 32f / 9f;

        // Vanilla world-view height in tiles. orthographicSize = zoom * 0.5 *
        // outputHeight / 16, so worldHeight = outputHeight / 16. Default
        // outputHeight = 270 → 16.875 tiles tall.
        private const float WORLD_HEIGHT_TILES = 270f / 16f;

        // Engine constant from Pug.Other.dll: subscription submap is 64 tiles wide.
        private const int SUBMAP_WIDTH_TILES = 64;

        private const string CONFIG_FILENAME = "UltraWide2_config.json";

        private static UltraWide2Config s_config = new UltraWide2Config();

        public void EarlyInit()
        {
        }

        public void Init()
        {
            s_config = LoadOrCreateConfig();
            BurstDisabler.DisableBurstForSystemAndJobs<SendClientSubMapToPugMapSystem>();
            SetupCameraPreferences(API.Rendering.GameCamera);
            SetupCameraPreferences(API.Rendering.UICamera);
            DisableMenuBorders();
        }

        public void ModObjectLoaded(UnityEngine.Object obj) { }

        public void Shutdown()
        {
        }

        public void Update()
        {
            // Keep maxOutputWidth in sync with the current screen aspect every
            // frame so window resizes / monitor swaps adapt cleanly.
            UpdateCamera(API.Rendering.GameCamera);
            UpdateCamera(API.Rendering.UICamera);
        }
        public bool CanBeUnloaded() => false;

        private void SetupCameraPreferences(PugCamera camera)
        {
            camera.SetPreferredOutputMode(OutputMode.MatchAspect);
            camera.minOutputWidth = camera.outputWidth;
            ApplyMaxOutputWidth(camera, GetEffectiveAspectCap());
        }

        private void UpdateCamera(PugCamera pugCamera)
        {
            if (pugCamera == null || pugCamera.outputHeight == 0)
                return;

            var srcCamera = pugCamera.camera;
            if (srcCamera == null || srcCamera.pixelRect.height <= 0f)
                return;

            float screenAspect = srcCamera.pixelRect.width / srcCamera.pixelRect.height;
            float aspect = UnityEngine.Mathf.Min(screenAspect, GetEffectiveAspectCap());
            ApplyMaxOutputWidth(pugCamera, aspect);
        }

        // Resolves the aspect cap from the loaded config. Always clamped to the
        // structural 32:9 ceiling regardless of what the user requested.
        private static float GetEffectiveAspectCap()
        {
            string mode = s_config?.mode?.Trim().ToLowerInvariant() ?? "safe";
            if (mode == "full")
            {
                return ASPECT_32_9;
            }

            int margin = UnityEngine.Mathf.Clamp(
                s_config?.safeMarginTiles ?? 4, 0, SUBMAP_WIDTH_TILES / 2 - 8);
            int safeWidthTiles = UnityEngine.Mathf.Max(16, SUBMAP_WIDTH_TILES - 2 * margin);
            float safeAspect = safeWidthTiles / WORLD_HEIGHT_TILES;
            return UnityEngine.Mathf.Min(safeAspect, ASPECT_32_9);
        }

        // Raises maxOutputWidth so PugCamera.GetPixelWidth(MatchAspect) is no
        // longer clamped at the engine's default 21:9 ceiling. We never lower it
        // -- if the game (or another mod) already set a wider value, we keep it.
        private static void ApplyMaxOutputWidth(PugCamera camera, float aspect)
        {
            int required = UnityEngine.Mathf.CeilToInt(camera.outputHeight * aspect);
            if (required % 2 != 0) required += 1;
            if (camera.maxOutputWidth < required)
            {
                camera.maxOutputWidth = required;
            }
        }

        private static UltraWide2Config LoadOrCreateConfig()
        {
            string path = System.IO.Path.Combine(
                UnityEngine.Application.persistentDataPath, CONFIG_FILENAME);
            try
            {
                if (System.IO.File.Exists(path))
                {
                    string json = System.IO.File.ReadAllText(path);
                    var loaded = UnityEngine.JsonUtility.FromJson<UltraWide2Config>(json);
                    if (loaded != null)
                    {
                        UnityEngine.Debug.Log(
                            $"[UltraWide2] Loaded config from {path} (mode={loaded.mode}, " +
                            $"safeMarginTiles={loaded.safeMarginTiles})");
                        return loaded;
                    }
                }

                var defaults = new UltraWide2Config();
                System.IO.File.WriteAllText(path, UnityEngine.JsonUtility.ToJson(defaults, true));
                UnityEngine.Debug.Log($"[UltraWide2] Wrote default config to {path}");
                return defaults;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"[UltraWide2] Could not read/write config at {path}: {ex.Message}. Using defaults.");
                return new UltraWide2Config();
            }
        }

        private void DisableMenuBorders()
        {
            var pauseMenuBorders = Manager.menu.pauseMenu.transform.Find("borders");
            if (pauseMenuBorders != null)
            {
                pauseMenuBorders.gameObject.SetActive(false);
            }
        }
    }
}
