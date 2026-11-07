using System;
using MajdataPlay.Settings;
using MajdataPlay.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Settings
{
    /// <summary>
    /// Covers <see cref="GameSettingMigration"/>, which upgrades settings.json documents of older versions while the file
    /// is read, and its use by <see cref="MajEnv.DeserializeUserSettings"/>.
    /// </summary>
    public class GameSettingMigrationTests
    {
        /// <summary>Creates the serializer the settings.json reader passes to the migration.</summary>
        /// <returns>The serializer.</returns>
        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.CreateDefault(MajEnv.UserJsonReaderOption);
        }

        /// <summary>Deserializes settings the way settings.json is read.</summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The settings.</returns>
        private static GameSetting Read(string json)
        {
            var setting = MajEnv.DeserializeUserSettings(json);
            Assert.That(setting, Is.Not.Null);
            return setting!;
        }

        /// <summary>Creates a legacy document whose Display category holds the given rendering quality level and render scale.</summary>
        /// <param name="quality">The legacy quality level, or -1 to leave it out.</param>
        /// <param name="scale">The legacy render scale, or -1 to leave it out.</param>
        /// <returns>The JSON text.</returns>
        private static string LegacyDocument(int quality, int scale)
        {
            var display = new JObject();
            if (quality >= 0)
            {
                display["RenderQuality"] = ((RenderQualityOption)quality).ToString();
            }
            if (scale >= 0)
            {
                display["RenderScale"] = scale;
            }
            return new JObject { ["Display"] = display }.ToString();
        }

        /// <summary>Checks that a document written by the current version is left unchanged.</summary>
        [Test]
        public void Migrate_CurrentDocumentIsUnchanged()
        {
            var setting = new GameSetting();
            setting.Rendering.RenderQuality = RenderingQualityOption.Refined;
            var document = JObject.Parse(Serializer.Json.Serialize(setting, MajEnv.UserJsonReaderOption));
            var original = (JObject)document.DeepClone();

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.False);
            Assert.That(JToken.DeepEquals(document, original), Is.True);
        }

        /// <summary>Checks that the legacy rendering keys leave Display and form a Rendering category, keeping the other Display keys.</summary>
        [Test]
        public void Migrate_MovesLegacyRenderingKeysToRendering()
        {
            var document = JObject.Parse("{\"Display\":{\"Skin\":\"custom\",\"RenderQuality\":\"High\",\"RenderScale\":100,\"FPSLimit\":60,\"VSync\":false}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            var display = (JObject)document["Display"]!;
            var rendering = (JObject)document["Rendering"]!;
            Assert.That(changed, Is.True);
            Assert.That(display.Count, Is.EqualTo(1));
            Assert.That(display["Skin"]!.Value<string>(), Is.EqualTo("custom"));
            Assert.That(rendering["RenderQuality"]!.Value<string>(), Is.EqualTo("Beautiful"));
            Assert.That(rendering["FPSLimit"]!.Value<int>(), Is.EqualTo(60));
            Assert.That(rendering["VSync"]!.Value<bool>(), Is.False);
            Assert.That(GameSettingMigration.Migrate(document, CreateSerializer()), Is.False, "a migrated document is current");
        }

        /// <summary>Checks the mapping of the six legacy quality levels and render scales that do not depend on the platform.</summary>
        /// <param name="quality">The legacy quality, or -1 for none.</param>
        /// <param name="scale">The legacy render scale, or -1 for none.</param>
        /// <param name="expectedQuality">The expected preset.</param>
        /// <param name="expectedAntiAliasing">The expected anti-aliasing.</param>
        /// <param name="expectedTexture">The expected texture quality.</param>
        /// <param name="expectedScale">The expected render scale.</param>
        /// <param name="expectedOutput">The expected output mode.</param>
        [TestCase(-1, -1, RenderingQualityOption.Performance, RenderingAntialiasingOption.Disabled, RenderingTextureQualityOption.High, 100, RenderingOutputMode.Direct)]
        [TestCase(0, 100, RenderingQualityOption.EnergySaving, RenderingAntialiasingOption.Disabled, RenderingTextureQualityOption.Medium, 100, RenderingOutputMode.Direct)]
        [TestCase(0, 80, RenderingQualityOption.Custom, RenderingAntialiasingOption.Disabled, RenderingTextureQualityOption.Medium, 80, RenderingOutputMode.Intermediate)]
        [TestCase(1, 100, RenderingQualityOption.Performance, RenderingAntialiasingOption.Disabled, RenderingTextureQualityOption.High, 100, RenderingOutputMode.Direct)]
        [TestCase(2, -1, RenderingQualityOption.Custom, RenderingAntialiasingOption.MSAA2x, RenderingTextureQualityOption.High, 100, RenderingOutputMode.Direct)]
        [TestCase(3, -1, RenderingQualityOption.Beautiful, RenderingAntialiasingOption.MSAA4x, RenderingTextureQualityOption.High, 100, RenderingOutputMode.Direct)]
        [TestCase(3, 100, RenderingQualityOption.Beautiful, RenderingAntialiasingOption.MSAA4x, RenderingTextureQualityOption.High, 100, RenderingOutputMode.Direct)]
        [TestCase(4, 100, RenderingQualityOption.Custom, RenderingAntialiasingOption.MSAA8x, RenderingTextureQualityOption.High, 100, RenderingOutputMode.Direct)]
        [TestCase(5, 150, RenderingQualityOption.Custom, RenderingAntialiasingOption.MSAA8x, RenderingTextureQualityOption.High, 150, RenderingOutputMode.Intermediate)]
        [TestCase(3, 50, RenderingQualityOption.Custom, RenderingAntialiasingOption.MSAA4x, RenderingTextureQualityOption.High, 50, RenderingOutputMode.Intermediate)]
        [TestCase(3, 5, RenderingQualityOption.Custom, RenderingAntialiasingOption.MSAA4x, RenderingTextureQualityOption.High, 10, RenderingOutputMode.Intermediate)]
        public void Read_MapsLegacyQualityLevels(int quality, int scale, RenderingQualityOption expectedQuality, RenderingAntialiasingOption expectedAntiAliasing,
                                                 RenderingTextureQualityOption expectedTexture, int expectedScale, RenderingOutputMode expectedOutput)
        {
            var rendering = Read(LegacyDocument(quality, scale)).Rendering;

            Assert.That(rendering.RenderQuality, Is.EqualTo(expectedQuality));
            Assert.That(rendering.AntiAliasing, Is.EqualTo(expectedAntiAliasing));
            Assert.That(rendering.TextureQuality, Is.EqualTo(expectedTexture));
            Assert.That(rendering.RenderScale, Is.EqualTo(expectedScale));
            Assert.That(rendering.OutputMode, Is.EqualTo(expectedOutput));
        }

        /// <summary>Checks that a legacy render scale without a quality level completes it with the old platform default.</summary>
        [Test]
        public void Read_LegacyScaleUsesPlatformDefaultQuality()
        {
            var rendering = Read(LegacyDocument(-1, 500)).Rendering;

            Assert.That(rendering.RenderQuality, Is.EqualTo(RenderingQualityOption.Custom));
            Assert.That(rendering.RenderScale, Is.EqualTo(200));
            Assert.That(rendering.OutputMode, Is.EqualTo(RenderingOutputMode.Intermediate));
#if UNITY_ANDROID || UNITY_IOS
            Assert.That(rendering.AntiAliasing, Is.EqualTo(RenderingAntialiasingOption.Disabled));
#else
            Assert.That(rendering.AntiAliasing, Is.EqualTo(RenderingAntialiasingOption.MSAA2x));
#endif
        }

        /// <summary>Checks that a legacy quality level stored as a number is read like its name.</summary>
        [Test]
        public void Read_LegacyNumericQuality()
        {
            var rendering = Read("{\"Display\":{\"RenderQuality\":0}}").Rendering;

            Assert.That(rendering.RenderQuality, Is.EqualTo(RenderingQualityOption.EnergySaving));
            Assert.That(rendering.TextureQuality, Is.EqualTo(RenderingTextureQualityOption.Medium));
        }

        /// <summary>Checks that legacy keys with null values are read as missing and still removed.</summary>
        [Test]
        public void Read_LegacyNullValuesAreMissing()
        {
            var document = JObject.Parse("{\"Display\":{\"RenderQuality\":null,\"RenderScale\":null,\"FPSLimit\":null,\"VSync\":null}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());
            var rendering = document.ToObject<GameSetting>(CreateSerializer())!.Rendering;

            Assert.That(changed, Is.True);
            Assert.That(((JObject)document["Display"]!).Count, Is.EqualTo(0));
            Assert.That(rendering.RenderQuality, Is.EqualTo(RenderingQualityOption.Performance));
            Assert.That(rendering.FPSLimit, Is.EqualTo(120));
            Assert.That(rendering.VSync, Is.True);
        }

        /// <summary>Checks that an existing Rendering category wins over legacy keys, including the frame pacing it stores.</summary>
        [Test]
        public void Read_ExistingRenderingWinsOverLegacy()
        {
            var rendering = Read("{\"Display\":{\"RenderQuality\":\"High\",\"FPSLimit\":60,\"VSync\":false},\"Rendering\":{\"RenderQuality\":\"Refined\",\"FPSLimit\":90,\"VSync\":true}}").Rendering;

            Assert.That(rendering.RenderQuality, Is.EqualTo(RenderingQualityOption.Refined));
            Assert.That(rendering.RenderScale, Is.EqualTo(150));
            Assert.That(rendering.FPSLimit, Is.EqualTo(90));
            Assert.That(rendering.VSync, Is.True);
        }

        /// <summary>Checks that legacy frame pacing fills in the keys missing from a newer Rendering category.</summary>
        [Test]
        public void Read_LegacyFramePacingFillsMissingKeys()
        {
            var rendering = Read("{\"Display\":{\"FPSLimit\":60,\"VSync\":false},\"Rendering\":{\"RenderQuality\":\"EnergySaving\"}}").Rendering;

            Assert.That(rendering.RenderQuality, Is.EqualTo(RenderingQualityOption.EnergySaving));
            Assert.That(rendering.TextureQuality, Is.EqualTo(RenderingTextureQualityOption.Medium));
            Assert.That(rendering.FPSLimit, Is.EqualTo(60));
            Assert.That(rendering.VSync, Is.False);
        }

        /// <summary>Checks that a null Rendering category is read as missing, so the legacy keys still migrate.</summary>
        [Test]
        public void Read_NullRenderingMigratesLegacyKeys()
        {
            var rendering = Read("{\"Display\":{\"RenderQuality\":\"VeryHigh\",\"FPSLimit\":30},\"Rendering\":null}").Rendering;

            Assert.That(rendering.RenderQuality, Is.EqualTo(RenderingQualityOption.Custom));
            Assert.That(rendering.AntiAliasing, Is.EqualTo(RenderingAntialiasingOption.MSAA8x));
            Assert.That(rendering.FPSLimit, Is.EqualTo(30));
        }

        /// <summary>Checks that migrated settings are saved in the current layout and read back unchanged.</summary>
        [Test]
        public void Read_MigratedSettingsSaveCurrentLayout()
        {
            var migrated = Read(LegacyDocument(5, 150));

            var json = Serializer.Json.Serialize(migrated, MajEnv.UserJsonReaderOption);
            var saved = JObject.Parse(json);
            var restored = Read(json).Rendering;

            Assert.That(((JObject)saved["Display"]!).ContainsKey("RenderQuality"), Is.False);
            Assert.That(((JObject)saved["Display"]!).ContainsKey("RenderScale"), Is.False);
            Assert.That(saved["Rendering"]!["RenderQuality"]!.Value<string>(), Is.EqualTo("Custom"));
            Assert.That(restored.AntiAliasing, Is.EqualTo(RenderingAntialiasingOption.MSAA8x));
            Assert.That(restored.RenderScale, Is.EqualTo(150));
            Assert.That(restored.OutputMode, Is.EqualTo(RenderingOutputMode.Intermediate));
        }

        /// <summary>Checks that the legacy interface keys leave Game and move to Display, keeping the other Game keys.</summary>
        [Test]
        public void Migrate_MovesLegacyListKeysToDisplay()
        {
            var document = JObject.Parse("{\"Game\":{\"TapSpeed\":8,\"ListStyle\":\"List\",\"ListPageSize\":30,\"ListInstantPreview\":true}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            var game = (JObject)document["Game"]!;
            var display = (JObject)document["Display"]!;
            Assert.That(changed, Is.True);
            Assert.That(game.Count, Is.EqualTo(1));
            Assert.That(game["TapSpeed"]!.Value<float>(), Is.EqualTo(8f));
            Assert.That(display["ListStyle"]!.Value<string>(), Is.EqualTo("List"));
            Assert.That(display["ListPageSize"]!.Value<int>(), Is.EqualTo(30));
            Assert.That(display["ListInstantPreview"]!.Value<bool>(), Is.True);
            Assert.That(GameSettingMigration.Migrate(document, CreateSerializer()), Is.False, "a migrated document is current");
        }

        /// <summary>Checks that interface keys already in Display win over the legacy keys in Game, which are removed.</summary>
        [Test]
        public void Migrate_ExistingDisplayListKeysWin()
        {
            var document = JObject.Parse("{\"Game\":{\"ListStyle\":\"List\",\"ListPageSize\":30},\"Display\":{\"ListStyle\":\"Cover\"}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            var game = (JObject)document["Game"]!;
            var display = (JObject)document["Display"]!;
            Assert.That(changed, Is.True);
            Assert.That(game.ContainsKey("ListStyle"), Is.False);
            Assert.That(game.ContainsKey("ListPageSize"), Is.False);
            Assert.That(display["ListStyle"]!.Value<string>(), Is.EqualTo("Cover"));
            Assert.That(display["ListPageSize"]!.Value<int>(), Is.EqualTo(30));
        }

        /// <summary>Checks that settings written before the move keep the interface values.</summary>
        [Test]
        public void Read_LegacyListKeysKeepTheirValues()
        {
            var setting = Read("{\"Game\":{\"ListStyle\":\"List\",\"ListPageSize\":30,\"ListInstantPreview\":true}}");

            Assert.That(setting.Display.ListStyle, Is.EqualTo(ListStyleOption.List));
            Assert.That(setting.Display.ListPageSize, Is.EqualTo(30));
            Assert.That(setting.Display.ListInstantPreview, Is.True);
        }

        /// <summary>Checks that the falling chart options leave Falling for Mod and the old slide-tail switch becomes a Flick position.</summary>
        [Test]
        public void Migrate_MovesFallingChartOptionsToMod()
        {
            var document = JObject.Parse("{\"Debug\":{\"SaveModSettings\":true},\"Falling\":{\"FallingSpeed\":7.5,\"FallingBreakToFlick\":true,\"FallingSlideTailToFlick\":true,\"FallingTouchToTap\":true}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            var falling = (JObject)document["Falling"]!;
            var mod = (JObject)document["Mod"]!;
            Assert.That(changed, Is.True);
            Assert.That(falling.Count, Is.EqualTo(2), "only FallingSpeed and the revision stay");
            Assert.That(falling["FallingSpeed"]!.Value<float>(), Is.EqualTo(7.5f));
            Assert.That(mod["FallingBreakToFlick"]!.Value<bool>(), Is.True);
            Assert.That(mod["FallingSlideTailToFlick"]!.Value<string>(), Is.EqualTo("Start"));
            Assert.That(mod["FallingTouchToTap"]!.Value<bool>(), Is.True);
            Assert.That(GameSettingMigration.Migrate(document, CreateSerializer()), Is.False, "a migrated document is current");
        }

        /// <summary>Checks that chart options already in Mod win over the legacy keys in Falling, which are removed.</summary>
        [Test]
        public void Migrate_ExistingModChartOptionsWin()
        {
            var document = JObject.Parse("{\"Debug\":{\"SaveModSettings\":true},\"Falling\":{\"FallingSlideTailToFlick\":true},\"Mod\":{\"FallingSlideTailToFlick\":\"End\"}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.True);
            Assert.That(((JObject)document["Falling"]!).ContainsKey("FallingSlideTailToFlick"), Is.False);
            Assert.That(document["Mod"]!["FallingSlideTailToFlick"]!.Value<string>(), Is.EqualTo("End"));
        }

        /// <summary>Checks that the moved chart options are dropped with the rest of Mod while Mod settings are not saved.</summary>
        [Test]
        public void Migrate_MovedChartOptionsFollowSaveModSettings()
        {
            var document = JObject.Parse("{\"Falling\":{\"FallingTouchToTap\":true,\"FallingSpeed\":7}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.True);
            Assert.That(document.ContainsKey("Mod"), Is.False);
            Assert.That(((JObject)document["Falling"]!).ContainsKey("FallingTouchToTap"), Is.False);
        }

        /// <summary>Checks that the old slide-tail switch in Mod becomes Start when on and Off when off.</summary>
        /// <param name="isOn">The old switch value.</param>
        /// <param name="expected">The expected Flick position.</param>
        [TestCase(true, "Start")]
        [TestCase(false, "Off")]
        public void Migrate_ConvertsSlideTailSwitch(bool isOn, string expected)
        {
            var document = new JObject
            {
                ["Debug"] = new JObject { ["SaveModSettings"] = true },
                ["Mod"] = new JObject { ["FallingSlideTailToFlick"] = isOn },
            };

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.True);
            Assert.That(document["Mod"]!["FallingSlideTailToFlick"]!.Value<string>(), Is.EqualTo(expected));
        }

        /// <summary>Checks that the old Keep Slides Original switch is dropped either way, so the slide style is the default Falling.</summary>
        /// <param name="isOriginal">The old switch value.</param>
        [TestCase(true)]
        [TestCase(false)]
        public void Migrate_DropsSlideOriginalSwitch(bool isOriginal)
        {
            var document = new JObject { ["Falling"] = new JObject { ["FallingSlideOriginal"] = isOriginal } };

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            var falling = (JObject)document["Falling"]!;
            Assert.That(changed, Is.True);
            Assert.That(falling.ContainsKey("FallingSlideOriginal"), Is.False);
            Assert.That(falling.ContainsKey("FallingSlideStyle"), Is.False);
            Assert.That(falling["FallingSettingsRevision"]!.Value<int>(), Is.EqualTo(FallingOptions.CURRENT_REVISION));
        }

        /// <summary>Checks that falling settings of an older revision have their slide style reset to Falling once.</summary>
        /// <param name="style">The stored slide style.</param>
        [TestCase("Standard")]
        [TestCase("Floating")]
        public void Read_OlderRevisionResetsSlideStyle(string style)
        {
            var setting = Read($"{{\"Falling\":{{\"FallingSlideStyle\":\"{style}\",\"FallingSpeed\":8}}}}");

            Assert.That(setting.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
            Assert.That(setting.Falling.FallingSpeed, Is.EqualTo(8f), "other falling settings are kept");
            Assert.That(setting.Falling.FallingSettingsRevision, Is.EqualTo(FallingOptions.CURRENT_REVISION));
        }

        /// <summary>Checks that a slide style chosen with the current revision is kept and the document is current.</summary>
        [Test]
        public void Migrate_KeepsSlideStyleOfCurrentRevision()
        {
            var document = JObject.Parse("{\"Falling\":{\"FallingSlideStyle\":\"Floating\",\"FallingSettingsRevision\":1}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.False);
            Assert.That(document["Falling"]!["FallingSlideStyle"]!.Value<string>(), Is.EqualTo("Floating"));
        }

        /// <summary>Checks that new settings use the Falling slide style and the current revision.</summary>
        [Test]
        public void NewSettings_UseFallingSlideStyle()
        {
            var setting = new GameSetting();

            Assert.That(setting.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
            Assert.That(setting.Falling.FallingSettingsRevision, Is.EqualTo(FallingOptions.CURRENT_REVISION));
            Assert.That(Read(Serializer.Json.Serialize(setting, MajEnv.UserJsonReaderOption)).Falling.FallingSlideStyle,
                        Is.EqualTo(FallingSlideStyleOption.Falling), "a round trip keeps it");
        }

        /// <summary>Checks that settings of the previous layout keep their chart options and get the Falling slide style when read.</summary>
        [Test]
        public void Read_LegacyFallingChartOptionsKeepTheirValues()
        {
            var setting = Read("{\"Debug\":{\"SaveModSettings\":true},\"Falling\":{\"FallingBreakToFlick\":true,\"FallingSlideTailToFlick\":true,\"FallingTouchToTap\":true,\"FallingSlideOriginal\":true}}");

            Assert.That(setting.Mod.FallingBreakToFlick, Is.True);
            Assert.That(setting.Mod.FallingSlideTailToFlick, Is.EqualTo(FallingSlideFlickOption.Start));
            Assert.That(setting.Mod.FallingTouchToTap, Is.True);
            Assert.That(setting.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
        }

        /// <summary>Checks that the oldest layout, with Keep Slides Original saved in Mod, also ends up with the Falling slide style.</summary>
        [Test]
        public void Read_OldestModSlideOriginalIsDropped()
        {
            var setting = Read("{\"Debug\":{\"SaveModSettings\":true},\"Mod\":{\"FallingSlideOriginal\":true}}");

            Assert.That(setting.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
        }

        /// <summary>Checks that null categories are removed and reported as a change.</summary>
        [Test]
        public void Migrate_RemovesNullCategories()
        {
            var document = JObject.Parse("{\"Audio\":null,\"Game\":{\"TapSpeed\":8}}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.True);
            Assert.That(document.ContainsKey("Audio"), Is.False);
            Assert.That(document["Game"]!["TapSpeed"]!.Value<float>(), Is.EqualTo(8f));
        }

        /// <summary>Checks that a Rendering category that is not an object is left for the deserializer to reject.</summary>
        [Test]
        public void Migrate_KeepsInvalidRenderingForTheDeserializer()
        {
            var document = JObject.Parse("{\"Display\":{\"FPSLimit\":60},\"Rendering\":5}");

            var changed = GameSettingMigration.Migrate(document, CreateSerializer());

            Assert.That(changed, Is.True);
            Assert.That(document["Rendering"]!.Value<int>(), Is.EqualTo(5));
            Assert.That(() => document.ToObject<GameSetting>(CreateSerializer()), Throws.InstanceOf<JsonException>());
        }

        /// <summary>Checks that a legacy quality level that cannot be read rejects the document, like any other invalid value.</summary>
        [Test]
        public void Migrate_InvalidLegacyQualityThrows()
        {
            var document = JObject.Parse("{\"Display\":{\"RenderQuality\":\"Bogus\"}}");

            Assert.That(() => GameSettingMigration.Migrate(document, CreateSerializer()), Throws.InstanceOf<JsonException>());
        }

        /// <summary>Checks the argument validation.</summary>
        [Test]
        public void Migrate_RejectsNullArguments()
        {
            Assert.That(() => GameSettingMigration.Migrate(null!, CreateSerializer()), Throws.InstanceOf<ArgumentNullException>());
            Assert.That(() => GameSettingMigration.Migrate(new JObject(), null!), Throws.InstanceOf<ArgumentNullException>());
        }
    }
}
