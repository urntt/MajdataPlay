using MajdataPlay.Diagnostics;
using Newtonsoft.Json.Linq;
#nullable enable
namespace MajdataPlay.Settings
{
    /// <summary>
    /// Upgrades the falling mode settings that older versions of this fork wrote, as part of <see cref="SettingMigration"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Before the settings were split, the fork stored the <c>Falling</c>, <c>Score</c> and saved <c>Mod</c> categories in
    /// settings.json. <see cref="SettingMigration"/> moves them to system-settings.json like the other system categories,
    /// so they stay on this device. The interface keys of the <c>Display</c> category move to the <c>Common</c> and
    /// <c>Rendering</c> categories through <see cref="SettingMigration.LegacyOptionMoves"/>.
    /// </para>
    /// <para>
    /// <see cref="MigrateLegacyUserDocument"/> runs first and gathers the keys that even older versions kept in the
    /// <c>Game</c> and <c>Mod</c> categories. <see cref="MigrateSystemDocument"/> runs last, on the categories in their
    /// current place: it moves the chart options from <c>Falling</c> to <c>Mod</c>, converts the old slide switches,
    /// resets the slide style once for settings older than <see cref="FallingOptions.CURRENT_REVISION"/>, and drops the
    /// <c>Mod</c> category unless <see cref="DebugOptions.SaveModSettings"/> is on. Current keys always win.
    /// </para>
    /// </remarks>
    internal static class FallingSettingMigration
    {
        /// <summary>
        /// The key of the old "keep slides original" switch, which the slide style replaced.
        /// </summary>
        private const string LegacySlideOriginalKey = "FallingSlideOriginal";

        // 以下分类名和键名是旧版设置文件的格式，不能跟随属性重命名。
        /// <summary>The category that older versions stored the falling options in.</summary>
        private const string FallingCategory = "Falling";

        /// <summary>The category of the gameplay modifiers.</summary>
        private const string ModCategory = "Mod";

        /// <summary>The category of the debug options.</summary>
        private const string DebugCategory = "Debug";

        /// <summary>The category that the oldest versions stored the falling and interface options in.</summary>
        private const string LegacyGameCategory = "Game";

        /// <summary>The category that the interface options moved to before the settings were split.</summary>
        private const string LegacyDisplayCategory = "Display";

        /// <summary>
        /// Gets the chart options that older versions stored in <c>Falling</c> and that now belong to <c>Mod</c>, under the same keys.
        /// </summary>
        private static readonly string[] s_fallingModKeys =
        {
            nameof(ModOptions.FallingBreakToFlick),
            nameof(ModOptions.FallingSlideTailToFlick),
            nameof(ModOptions.FallingTouchToTap),
        };

        /// <summary>
        /// Gets the interface options that the oldest versions stored in <c>Game</c> and later in <c>Display</c>.
        /// </summary>
        private static readonly string[] s_legacyGameListKeys =
        {
            nameof(CommonOptions.ListStyle),
            nameof(CommonOptions.ListPageSize),
            nameof(CommonOptions.ListInstantPreview),
        };

        /// <summary>
        /// Gets the falling options that the oldest versions stored in <c>Game</c>, under the same keys.
        /// </summary>
        private static readonly string[] s_legacyGameFallingKeys =
        {
            nameof(FallingOptions.FallingSpeed),
            nameof(FallingOptions.FallingLaneDividers),
            nameof(FallingOptions.FallingSweep),
            nameof(FallingOptions.FallingInfoFollowDisplay),
            nameof(FallingOptions.FallingDifficultyColor),
            nameof(FallingOptions.FallingScoreColor),
            nameof(FallingOptions.FallingJudgeLineColor),
        };

        /// <summary>
        /// Gets the play style and falling options that the oldest versions stored in <c>Mod</c>, which was only written
        /// while the mod settings were saved.
        /// </summary>
        private static readonly string[] s_legacyModFallingKeys =
        {
            nameof(FallingOptions.PlayStyle),
            nameof(FallingOptions.FallingVerticalJudge),
            LegacySlideOriginalKey,
        };

        /// <summary>
        /// Moves the falling and interface keys of the oldest single-file layout within settings.json, before
        /// <see cref="SettingMigration"/> moves the categories to their current documents.
        /// </summary>
        /// <param name="user">The root object of settings.json.</param>
        /// <returns>True when a legacy key was found and removed.</returns>
        internal static bool MigrateLegacyUserDocument(JObject user)
        {
            var changed = MoveLegacyFallingKeys(user);
            changed |= MoveLegacyListKeys(user);
            return changed;
        }

        /// <summary>
        /// Upgrades the falling and mod categories in system-settings.json, after <see cref="SettingMigration"/> moved them there.
        /// </summary>
        /// <param name="system">The root object of system-settings.json.</param>
        /// <returns>True when the document was changed.</returns>
        internal static bool MigrateSystemDocument(JObject system)
        {
            var changed = MoveFallingModKeys(system);
            changed |= ConvertLegacySlideKeys(system);
            changed |= ResetFallingSlideStyleOnce(system);
            changed |= DropUnsavedMod(system);
            return changed;
        }

        /// <summary>
        /// Moves the falling options that the oldest versions kept in <c>Game</c> and <c>Mod</c> to the <c>Falling</c>
        /// category. When the document already has a <c>Falling</c> category, the legacy keys are only removed.
        /// </summary>
        /// <param name="root">The root object of settings.json in the single-file layout.</param>
        /// <returns>True when a legacy key was found and removed.</returns>
        private static bool MoveLegacyFallingKeys(JObject root)
        {
            var hasFalling = root.ContainsKey(FallingCategory);
            var falling = hasFalling ? null : new JObject();
            var hasLegacyKeys = false;
            if (root[LegacyGameCategory] is JObject game)
            {
                foreach (var key in s_legacyGameFallingKeys)
                {
                    var value = TakeLegacyValue(game, key, ref hasLegacyKeys);
                    if (falling is not null && value is not null)
                    {
                        falling[key] = value;
                    }
                }
            }
            if (root[ModCategory] is JObject mod)
            {
                var isModSaved = IsModSaved(root);
                foreach (var key in s_legacyModFallingKeys)
                {
                    var value = TakeLegacyValue(mod, key, ref hasLegacyKeys);
                    if (falling is not null && isModSaved && value is not null)
                    {
                        falling[key] = value;
                    }
                }
            }
            if (!hasLegacyKeys)
            {
                return false;
            }
            if (falling is not null)
            {
                root[FallingCategory] = falling;
            }
            MajDebug.LogInfo(nameof(FallingSettingMigration), "Moved the legacy falling settings to Falling");
            return true;
        }

        /// <summary>
        /// Moves the interface options that the oldest versions kept in <c>Game</c> to <c>Display</c>, from where
        /// <see cref="SettingMigration.LegacyOptionMoves"/> takes them on to <c>Common</c>. Keys already in
        /// <c>Display</c> win; the legacy keys are only removed.
        /// </summary>
        /// <param name="root">The root object of settings.json.</param>
        /// <returns>True when a legacy key was found and removed.</returns>
        private static bool MoveLegacyListKeys(JObject root)
        {
            if (root[LegacyGameCategory] is not JObject game)
            {
                return false;
            }
            var hasLegacyKeys = false;
            JObject? display = null;
            foreach (var key in s_legacyGameListKeys)
            {
                var value = TakeLegacyValue(game, key, ref hasLegacyKeys);
                if (value is null)
                {
                    continue;
                }
                display ??= GetOrCreateCategory(root, LegacyDisplayCategory);
                if (display is not null)
                {
                    CopyIfMissing(display, key, value);
                }
            }
            if (!hasLegacyKeys)
            {
                return false;
            }
            MajDebug.LogInfo(nameof(FallingSettingMigration), "Moved the legacy list settings from Game to Display");
            return true;
        }

        /// <summary>
        /// Moves Break to Flick, slide tail to Flick and Touch to Tap from <c>Falling</c> to <c>Mod</c>. Keys already in
        /// <c>Mod</c> win; whether <c>Mod</c> is kept is then decided by <see cref="DropUnsavedMod"/>.
        /// </summary>
        /// <param name="root">The root object of system-settings.json.</param>
        /// <returns>True when a legacy key was found and removed.</returns>
        private static bool MoveFallingModKeys(JObject root)
        {
            if (root[FallingCategory] is not JObject falling)
            {
                return false;
            }
            var hasLegacyKeys = false;
            JObject? mod = null;
            foreach (var key in s_fallingModKeys)
            {
                var value = TakeLegacyValue(falling, key, ref hasLegacyKeys);
                if (value is null)
                {
                    continue;
                }
                mod ??= GetOrCreateCategory(root, ModCategory);
                if (mod is not null)
                {
                    CopyIfMissing(mod, key, value);
                }
            }
            if (!hasLegacyKeys)
            {
                return false;
            }
            MajDebug.LogInfo(nameof(FallingSettingMigration), "Moved the falling chart options from Falling to Mod");
            return true;
        }

        /// <summary>
        /// Turns the old slide switches into options: a true or false slide tail to Flick in <c>Mod</c> becomes Start or
        /// Off, and the old "keep slides original" switch in <c>Falling</c> is removed, so the slide style keeps its default.
        /// </summary>
        /// <param name="root">The root object of system-settings.json.</param>
        /// <returns>True when a legacy value was converted or removed.</returns>
        private static bool ConvertLegacySlideKeys(JObject root)
        {
            var isChanged = false;
            if (root[ModCategory] is JObject mod &&
                mod[nameof(ModOptions.FallingSlideTailToFlick)] is JValue { Type: JTokenType.Boolean } tailToFlick)
            {
                var option = (bool)tailToFlick ? FallingSlideFlickOption.Start : FallingSlideFlickOption.Off;
                mod[nameof(ModOptions.FallingSlideTailToFlick)] = option.ToString();
                isChanged = true;
            }
            if (root[FallingCategory] is JObject falling)
            {
                var hasLegacyKey = false;
                TakeLegacyValue(falling, LegacySlideOriginalKey, ref hasLegacyKey);
                isChanged |= hasLegacyKey;
            }
            if (isChanged)
            {
                MajDebug.LogInfo(nameof(FallingSettingMigration), "Converted the legacy slide switches to options");
            }
            return isChanged;
        }

        /// <summary>
        /// Removes the slide style of falling settings written before <see cref="FallingOptions.CURRENT_REVISION"/>, so it
        /// becomes the default falling style once, and writes the current revision. Later changes by the player are kept.
        /// </summary>
        /// <param name="root">The root object of system-settings.json.</param>
        /// <returns>True when the falling settings were older than the current revision.</returns>
        private static bool ResetFallingSlideStyleOnce(JObject root)
        {
            if (root[FallingCategory] is not JObject falling)
            {
                return false;
            }
            var revision = falling[nameof(FallingOptions.FallingSettingsRevision)] is JValue { Type: JTokenType.Integer } value
                ? (long)value
                : 0L;
            if (revision >= FallingOptions.CURRENT_REVISION)
            {
                return false;
            }
            falling.Remove(nameof(FallingOptions.FallingSlideStyle));
            falling[nameof(FallingOptions.FallingSettingsRevision)] = FallingOptions.CURRENT_REVISION;
            MajDebug.LogInfo(nameof(FallingSettingMigration), $"Reset the falling slide style of settings revision {revision} to Falling");
            return true;
        }

        /// <summary>
        /// Removes the <c>Mod</c> category unless the mod settings are saved, so they start from their defaults.
        /// </summary>
        /// <param name="root">The root object of system-settings.json.</param>
        /// <returns>True when the category was removed.</returns>
        private static bool DropUnsavedMod(JObject root)
        {
            if (!root.ContainsKey(ModCategory) || IsModSaved(root))
            {
                return false;
            }
            root.Remove(ModCategory);
            return true;
        }

        /// <summary>
        /// Determines whether a document turns on <see cref="DebugOptions.SaveModSettings"/>.
        /// </summary>
        /// <param name="root">The root object of the document that holds the <c>Debug</c> category.</param>
        /// <returns>True when the mod settings are saved; otherwise false.</returns>
        private static bool IsModSaved(JObject root)
        {
            return root[DebugCategory] is JObject debug &&
                   debug[nameof(DebugOptions.SaveModSettings)] is JValue { Type: JTokenType.Boolean } value &&
                   (bool)value;
        }

        /// <summary>
        /// Gets a category, creating it when the document does not have it.
        /// </summary>
        /// <param name="root">The root object of the document.</param>
        /// <param name="name">The name of the category.</param>
        /// <returns>The category, or null when the document holds a value that is not an object, which is left for the deserializer to reject.</returns>
        private static JObject? GetOrCreateCategory(JObject root, string name)
        {
            if (!root.TryGetValue(name, out var token))
            {
                var category = new JObject();
                root[name] = category;
                return category;
            }
            return token as JObject;
        }

        /// <summary>
        /// Removes a legacy key from a category.
        /// </summary>
        /// <param name="category">The category holding the legacy key.</param>
        /// <param name="key">The legacy key.</param>
        /// <param name="removed">Set to true when the key existed; left unchanged otherwise.</param>
        /// <returns>The value of the key, or null when the key is missing or its value is null.</returns>
        private static JToken? TakeLegacyValue(JObject category, string key, ref bool removed)
        {
            if (!category.TryGetValue(key, out var value))
            {
                return null;
            }
            category.Remove(key);
            removed = true;
            return value.Type == JTokenType.Null ? null : value;
        }

        /// <summary>
        /// Adds a legacy value to a category unless the category already has the key.
        /// </summary>
        /// <param name="category">The category that receives the value.</param>
        /// <param name="key">The key of the value.</param>
        /// <param name="legacyValue">The legacy value, or null when it was not stored.</param>
        private static void CopyIfMissing(JObject category, string key, JToken? legacyValue)
        {
            if (legacyValue is not null && !category.ContainsKey(key))
            {
                category[key] = legacyValue;
            }
        }
    }
}
