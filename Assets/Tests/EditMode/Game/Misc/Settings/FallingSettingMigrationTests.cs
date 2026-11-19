using MajdataPlay.Settings;
using MajdataPlay.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

#nullable enable
namespace MajdataPlay.Tests.Game.Misc.Settings
{
    /// <summary>
    /// Covers <see cref="FallingSettingMigration"/> as part of <see cref="SettingMigration"/>: the falling, score and mod
    /// settings that older versions of this fork stored in settings.json move to system-settings.json and are upgraded.
    /// </summary>
    public class FallingSettingMigrationTests
    {
        /// <summary>Creates the serializer the settings readers pass to the migration.</summary>
        /// <returns>The serializer.</returns>
        private static JsonSerializer CreateSerializer()
        {
            return JsonSerializer.CreateDefault(MajEnv.UserJsonReaderOption);
        }

        /// <summary>Migrates a settings.json document of an older version, with no system-settings.json yet.</summary>
        /// <param name="userJson">The text of settings.json.</param>
        /// <returns>The migrated settings.json and system-settings.json documents.</returns>
        private static (JObject User, JObject System) Migrate(string userJson)
        {
            var user = JObject.Parse(userJson);
            var system = new JObject();
            SettingMigration.Migrate(user, system, CreateSerializer());
            return (user, system);
        }

        /// <summary>Deserializes settings the way the settings files are read.</summary>
        /// <param name="userJson">The text of settings.json.</param>
        /// <param name="systemJson">The text of system-settings.json, or null when the file does not exist.</param>
        /// <returns>The system settings.</returns>
        private static SystemSetting ReadSystem(string userJson, string? systemJson = null)
        {
            var (user, system) = MajEnv.DeserializeSettings(userJson, systemJson);
            Assert.That(user, Is.Not.Null);
            Assert.That(system, Is.Not.Null);
            return system!;
        }

        /// <summary>Checks that the fork's categories and interface keys leave settings.json for system-settings.json.</summary>
        [Test]
        public void Migrate_MovesForkSettingsToSystemDocument()
        {
            var (user, system) = Migrate("{\"Game\":{\"TapSpeed\":8},\"Display\":{\"ListStyle\":\"List\",\"ListPageSize\":30," +
                                         "\"ListInstantPreview\":true,\"GameplayOrientation\":\"Landscape\"}," +
                                         "\"Falling\":{\"PlayStyle\":\"Falling\",\"FallingSpeed\":8,\"FallingSettingsRevision\":1}," +
                                         "\"Score\":{\"ShareBestCount\":30},\"Debug\":{\"SaveModSettings\":true}," +
                                         "\"Mod\":{\"FallingTouchToTap\":true}}");

            Assert.That(user.ContainsKey("Falling") || user.ContainsKey("Score") || user.ContainsKey("Mod"), Is.False);
            Assert.That(((JObject)user["Display"]!).Count, Is.EqualTo(0));
            Assert.That(user["Game"]!["TapSpeed"]!.Value<float>(), Is.EqualTo(8f));
            Assert.That(system["Falling"]!["PlayStyle"]!.Value<string>(), Is.EqualTo("Falling"));
            Assert.That(system["Score"]!["ShareBestCount"]!.Value<int>(), Is.EqualTo(30));
            Assert.That(system["Mod"]!["FallingTouchToTap"]!.Value<bool>(), Is.True);
            Assert.That(system["Common"]!["ListStyle"]!.Value<string>(), Is.EqualTo("List"));
            Assert.That(system["Common"]!["ListPageSize"]!.Value<int>(), Is.EqualTo(30));
            Assert.That(system["Common"]!["ListInstantPreview"]!.Value<bool>(), Is.True);
            Assert.That(system["Rendering"]!["GameplayOrientation"]!.Value<string>(), Is.EqualTo("Landscape"));
            Assert.That(SettingMigration.Migrate(user, system, CreateSerializer()), Is.False, "migrated documents are current");
        }

        /// <summary>Checks that the read settings keep the values of the fork's previous layout.</summary>
        [Test]
        public void Read_ForkSettingsKeepTheirValues()
        {
            var system = ReadSystem("{\"Display\":{\"ListStyle\":\"List\",\"ListPageSize\":30,\"GameplayOrientation\":\"Landscape\"}," +
                                    "\"Falling\":{\"PlayStyle\":\"Falling\",\"FallingSpeed\":8,\"FallingSlideStyle\":\"Floating\",\"FallingSettingsRevision\":1}," +
                                    "\"Debug\":{\"SaveModSettings\":true},\"Mod\":{\"FallingBreakToFlick\":true}}");

            Assert.That(system.Common.ListStyle, Is.EqualTo(ListStyleOption.List));
            Assert.That(system.Common.ListPageSize, Is.EqualTo(30));
            Assert.That(system.Rendering.GameplayOrientation, Is.EqualTo(GameplayOrientationOption.Landscape));
            Assert.That(system.Falling.PlayStyle, Is.EqualTo(PlayStyleOption.Falling));
            Assert.That(system.Falling.FallingSpeed, Is.EqualTo(8f));
            Assert.That(system.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Floating));
            Assert.That(system.Debug.SaveModSettings, Is.True);
            Assert.That(system.Mod.FallingBreakToFlick, Is.True);
        }

        /// <summary>Checks that system-settings.json keeps its own falling values over those still in settings.json.</summary>
        [Test]
        public void Migrate_ExistingSystemFallingWins()
        {
            var user = JObject.Parse("{\"Falling\":{\"FallingSpeed\":6,\"FallingSweep\":true}}");
            var system = JObject.Parse("{\"Falling\":{\"FallingSpeed\":9,\"FallingSettingsRevision\":1}}");

            SettingMigration.Migrate(user, system, CreateSerializer());

            Assert.That(user.ContainsKey("Falling"), Is.False);
            Assert.That(system["Falling"]!["FallingSpeed"]!.Value<float>(), Is.EqualTo(9f));
            Assert.That(system["Falling"]!["FallingSweep"]!.Value<bool>(), Is.True);
        }

        /// <summary>Checks that the oldest layout's interface keys in Game end up in Common, with Display winning over Game.</summary>
        [Test]
        public void Migrate_OldestListKeysMoveToCommon()
        {
            var (user, system) = Migrate("{\"Game\":{\"TapSpeed\":8,\"ListStyle\":\"List\",\"ListPageSize\":30},\"Display\":{\"ListStyle\":\"Cover\"}}");

            Assert.That(((JObject)user["Game"]!).Count, Is.EqualTo(1));
            Assert.That(system["Common"]!["ListStyle"]!.Value<string>(), Is.EqualTo("Cover"));
            Assert.That(system["Common"]!["ListPageSize"]!.Value<int>(), Is.EqualTo(30));
        }

        /// <summary>Checks that the falling chart options leave Falling for Mod and the old slide-tail switch becomes a Flick position.</summary>
        [Test]
        public void Migrate_MovesFallingChartOptionsToMod()
        {
            var (_, system) = Migrate("{\"Debug\":{\"SaveModSettings\":true},\"Falling\":{\"FallingSpeed\":7.5,\"FallingBreakToFlick\":true," +
                                      "\"FallingSlideTailToFlick\":true,\"FallingTouchToTap\":true}}");

            var falling = (JObject)system["Falling"]!;
            var mod = (JObject)system["Mod"]!;
            Assert.That(falling.Count, Is.EqualTo(2), "only FallingSpeed and the revision stay");
            Assert.That(mod["FallingBreakToFlick"]!.Value<bool>(), Is.True);
            Assert.That(mod["FallingSlideTailToFlick"]!.Value<string>(), Is.EqualTo("Start"));
            Assert.That(mod["FallingTouchToTap"]!.Value<bool>(), Is.True);
        }

        /// <summary>Checks that the mod settings are dropped unless they are saved, also when they came from settings.json.</summary>
        [Test]
        public void Migrate_UnsavedModIsDropped()
        {
            var (_, system) = Migrate("{\"Mod\":{\"PlaybackSpeed\":1.5},\"Falling\":{\"FallingTouchToTap\":true,\"FallingSettingsRevision\":1}}");

            Assert.That(system.ContainsKey("Mod"), Is.False);
            Assert.That(((JObject)system["Falling"]!).ContainsKey("FallingTouchToTap"), Is.False);
        }

        /// <summary>Checks that the old Keep Slides Original switch is dropped, so the slide style is the default Falling.</summary>
        /// <param name="isOriginal">The old switch value.</param>
        [TestCase(true)]
        [TestCase(false)]
        public void Migrate_DropsSlideOriginalSwitch(bool isOriginal)
        {
            var (_, system) = Migrate(new JObject { ["Falling"] = new JObject { ["FallingSlideOriginal"] = isOriginal } }.ToString());

            var falling = (JObject)system["Falling"]!;
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
            var system = ReadSystem($"{{\"Falling\":{{\"FallingSlideStyle\":\"{style}\",\"FallingSpeed\":8}}}}");

            Assert.That(system.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
            Assert.That(system.Falling.FallingSpeed, Is.EqualTo(8f), "other falling settings are kept");
            Assert.That(system.Falling.FallingSettingsRevision, Is.EqualTo(FallingOptions.CURRENT_REVISION));
        }

        /// <summary>Checks that the oldest layout's falling keys in Game and saved Mod end up in Falling.</summary>
        [Test]
        public void Read_OldestFallingKeysMoveToFalling()
        {
            var system = ReadSystem("{\"Game\":{\"FallingSpeed\":9,\"FallingSweep\":true},\"Debug\":{\"SaveModSettings\":true}," +
                                    "\"Mod\":{\"PlayStyle\":\"Falling\",\"FallingSlideOriginal\":true}}");

            Assert.That(system.Falling.FallingSpeed, Is.EqualTo(9f));
            Assert.That(system.Falling.FallingSweep, Is.True);
            Assert.That(system.Falling.PlayStyle, Is.EqualTo(PlayStyleOption.Falling));
            Assert.That(system.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
        }

        /// <summary>Checks that new settings use the Falling slide style, do not save mod settings, and survive a round trip.</summary>
        [Test]
        public void NewSettings_RoundTrip()
        {
            var system = new SystemSetting();
            system.Mod.PlaybackSpeed = 1.5f;
            var json = Serializer.Json.Serialize(system, MajEnv.UserJsonReaderOption);

            Assert.That(JObject.Parse(json).ContainsKey("Mod"), Is.False, "mod settings are not saved by default");
            var restored = ReadSystem("{}", json);
            Assert.That(restored.Falling.FallingSlideStyle, Is.EqualTo(FallingSlideStyleOption.Falling));
            Assert.That(restored.Falling.FallingSettingsRevision, Is.EqualTo(FallingOptions.CURRENT_REVISION));
            Assert.That(restored.Mod.PlaybackSpeed, Is.EqualTo(1f));

            system.Debug.SaveModSettings = true;
            var saved = ReadSystem("{}", Serializer.Json.Serialize(system, MajEnv.UserJsonReaderOption));
            Assert.That(saved.Mod.PlaybackSpeed, Is.EqualTo(1.5f), "saved mod settings are kept");
        }

        /// <summary>Checks the mod helpers: falling mode counts as a mod, and only chart-changing falling options block saving.</summary>
        [Test]
        public void IsAnyModActive_CountsFallingMode()
        {
            var system = new SystemSetting();
            Assert.That(system.IsAnyModActive(), Is.False);

            system.Falling.PlayStyle = PlayStyleOption.Falling;
            Assert.That(system.IsAnyModActive(), Is.True);
            Assert.That(system.IsAnyModActiveExceptPlayStyle(), Is.False);

            system.Mod.FallingTouchToTap = true;
            Assert.That(system.IsAnyModActiveExceptPlayStyle(), Is.True);
        }
    }
}
