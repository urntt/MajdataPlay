using Jint;
using Jint.Native;
using Jint.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>
    /// Runs the bundled Universal Rhythm Chart (URC) converters in a Jint JavaScript engine.
    /// </summary>
    /// <remarks>
    /// The bundle is built from Tools/Urc/entry.ts and exposes <c>UrcBridge.simaiToPez</c> and
    /// <c>UrcBridge.pezToSimai</c>, which take and return one JSON string each and use the default options of the
    /// <c>urc convert</c> command. The class has no Unity dependency, so it can be used from any thread; calls are
    /// serialized because a Jint engine is not thread safe.
    /// </remarks>
    internal sealed class UrcEngine
    {
        /// <summary>The longest time one conversion may run before it is stopped.</summary>
        /// <remarks>A dense 240-bar chart takes about 17 s on a desktop; phones are slower, so the limit is generous.</remarks>
        private static readonly TimeSpan s_conversionTimeout = TimeSpan.FromMinutes(10);

        /// <summary>The engine holding the bundle.</summary>
        private readonly Engine _engine;

        /// <summary>The <c>UrcBridge</c> object of the bundle.</summary>
        private readonly JsValue _bridge;

        /// <summary>Serializes calls into the engine.</summary>
        private readonly object _gate = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="UrcEngine"/> class and runs the bundle.
        /// </summary>
        /// <param name="bundleSource">The JavaScript source of the URC bundle.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bundleSource"/> is null.</exception>
        /// <exception cref="UrcConversionException">The bundle could not be run or does not define <c>UrcBridge</c>.</exception>
        internal UrcEngine(string bundleSource)
        {
            if (bundleSource is null)
            {
                throw new ArgumentNullException(nameof(bundleSource));
            }
            // Jint's memory limit counts every allocation, not live memory, so a large chart would hit any sensible
            // value; the timeout alone stops a conversion that runs away.
            _engine = new Engine(options =>
            {
                options.TimeoutInterval(s_conversionTimeout);
            });
            try
            {
                _engine.Execute(bundleSource);
            }
            catch (Exception e) when (e is JavaScriptException or JintException)
            {
                throw new UrcConversionException($"The URC bundle could not be loaded: {e.Message}", e);
            }
            _bridge = _engine.GetValue("UrcBridge");
            if (!_bridge.IsObject())
            {
                throw new UrcConversionException("The URC bundle does not define UrcBridge.");
            }
        }

        /// <summary>
        /// Converts one difficulty of a simai chart to the text files of a Phira .pez package.
        /// </summary>
        /// <param name="maidata">The text of maidata.txt.</param>
        /// <param name="inote">The simai inote number of the difficulty, or null for the last difficulty present.</param>
        /// <param name="files">The names of the files next to maidata.txt, from which the music and cover are chosen.</param>
        /// <returns>The files of the package and what the conversion reported.</returns>
        /// <exception cref="UrcConversionException">The chart could not be converted.</exception>
        internal UrcPezResult SimaiToPez(string maidata, int? inote, IReadOnlyList<string> files)
        {
            var args = JsonConvert.SerializeObject(new { maidata, inote, files });
            var result = Call("simaiToPez", args);
            var textFiles = result["files"] as JObject;
            return new UrcPezResult
            {
                Inote = result.Value<int>("inote"),
                ChartJson = textFiles?.Value<string>("chart.json") ?? string.Empty,
                InfoYaml = textFiles?.Value<string>("info.yml") ?? string.Empty,
                Music = result.Value<string>("music") ?? string.Empty,
                Illustration = result.Value<string>("illustration") ?? string.Empty,
                Warnings = ReadLines(result, "warnings"),
                Report = ReadLines(result, "report"),
            };
        }

        /// <summary>
        /// Converts the RPE chart of a Phira .pez package to maidata.txt.
        /// </summary>
        /// <param name="chartJson">The text of the chart file the package's info.yml names.</param>
        /// <returns>The maidata text, the media the chart refers to and what the conversion reported.</returns>
        /// <exception cref="UrcConversionException">The chart could not be converted.</exception>
        internal UrcSimaiResult PezToSimai(string chartJson)
        {
            var args = JsonConvert.SerializeObject(new { chart = chartJson });
            var result = Call("pezToSimai", args);
            return new UrcSimaiResult
            {
                Maidata = result.Value<string>("maidata") ?? string.Empty,
                Music = result.Value<string>("music"),
                Illustration = result.Value<string>("illustration"),
                Title = result.Value<string>("title"),
                Warnings = ReadLines(result, "warnings"),
                Report = ReadLines(result, "report"),
            };
        }

        /// <summary>
        /// Calls a function of <c>UrcBridge</c> with a JSON argument and parses its JSON result.
        /// </summary>
        /// <param name="function">The name of the function.</param>
        /// <param name="argsJson">The JSON argument.</param>
        /// <returns>The parsed result.</returns>
        /// <exception cref="UrcConversionException">The function threw or ran too long.</exception>
        private JObject Call(string function, string argsJson)
        {
            lock (_gate)
            {
                try
                {
                    _engine.Constraints.Reset();
                    var callee = _bridge.AsObject().Get(function);
                    var returned = _engine.Invoke(callee, argsJson);
                    return JObject.Parse(returned.AsString());
                }
                catch (Exception e) when (e is JavaScriptException or JintException or JsonException or InvalidOperationException)
                {
                    throw new UrcConversionException(e.Message, e);
                }
            }
        }

        /// <summary>
        /// Reads an array of strings from a result.
        /// </summary>
        /// <param name="result">The parsed result.</param>
        /// <param name="name">The name of the array.</param>
        /// <returns>The strings, or an empty list when the array is missing.</returns>
        private static IReadOnlyList<string> ReadLines(JObject result, string name)
        {
            if (result[name] is not JArray array)
            {
                return Array.Empty<string>();
            }
            var lines = new List<string>(array.Count);
            foreach (var item in array)
            {
                lines.Add(item.ToString());
            }
            return lines;
        }
    }

    /// <summary>
    /// The result of converting a simai chart to a Phira .pez package.
    /// </summary>
    internal sealed class UrcPezResult
    {
        /// <summary>Gets the simai inote number of the difficulty that was converted.</summary>
        public int Inote { get; init; }

        /// <summary>Gets the text of chart.json.</summary>
        public string ChartJson { get; init; } = string.Empty;

        /// <summary>Gets the text of info.yml.</summary>
        public string InfoYaml { get; init; } = string.Empty;

        /// <summary>Gets the name the package uses for the music file.</summary>
        public string Music { get; init; } = string.Empty;

        /// <summary>Gets the name the package uses for the illustration file.</summary>
        public string Illustration { get; init; } = string.Empty;

        /// <summary>Gets the warnings about the source chart.</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        /// <summary>Gets the fidelity summary and what the conversion changed, one line each.</summary>
        public IReadOnlyList<string> Report { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// The result of converting the RPE chart of a Phira .pez package to simai.
    /// </summary>
    internal sealed class UrcSimaiResult
    {
        /// <summary>Gets the text of maidata.txt.</summary>
        public string Maidata { get; init; } = string.Empty;

        /// <summary>Gets the name of the music file the chart refers to, or null when it names none.</summary>
        public string? Music { get; init; }

        /// <summary>Gets the name of the illustration file the chart refers to, or null when it names none.</summary>
        public string? Illustration { get; init; }

        /// <summary>Gets the title of the chart, or null when it has none.</summary>
        public string? Title { get; init; }

        /// <summary>Gets the warnings about the source chart.</summary>
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

        /// <summary>Gets the fidelity summary and what the conversion changed, one line each.</summary>
        public IReadOnlyList<string> Report { get; init; } = Array.Empty<string>();
    }

    /// <summary>
    /// The exception thrown when a URC conversion fails; its message is the reason URC gave.
    /// </summary>
    internal sealed class UrcConversionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UrcConversionException"/> class.
        /// </summary>
        /// <param name="message">The reason the conversion failed.</param>
        /// <param name="innerException">The exception that caused the failure, if any.</param>
        public UrcConversionException(string message, Exception? innerException = null) : base(message, innerException)
        {
        }
    }
}
