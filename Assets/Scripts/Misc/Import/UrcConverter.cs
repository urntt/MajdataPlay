using Cysharp.Threading.Tasks;
using MajdataPlay.Diagnostics;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>
    /// Converts charts between simai and Phira .pez with the bundled Universal Rhythm Chart (URC) converters.
    /// </summary>
    /// <remarks>
    /// The bundle (Resources/Urc/UrcBundle, built by Tools/Urc) is loaded into a <see cref="UrcEngine"/> the first time
    /// a conversion is requested. Conversions run on the thread pool and return on the thread pool; callers that touch
    /// Unity objects switch back to the main thread themselves.
    /// </remarks>
    internal static class UrcConverter
    {
        /// <summary>The Resources path of the bundled converters.</summary>
        private const string BundleResourcePath = "Urc/UrcBundle";

        /// <summary>Serializes the creation of the engine.</summary>
        private static readonly SemaphoreSlim s_engineGate = new(1, 1);

        /// <summary>The engine, once it has been created.</summary>
        private static UrcEngine? s_engine;

        /// <summary>
        /// Converts one difficulty of a simai chart to the text files of a Phira .pez package.
        /// </summary>
        /// <param name="maidata">The text of maidata.txt.</param>
        /// <param name="inote">The simai inote number of the difficulty, or null for the last difficulty present.</param>
        /// <param name="files">The names of the files next to maidata.txt, from which the music and cover are chosen.</param>
        /// <param name="token">Cancels waiting for the engine; a conversion that has started runs to the end.</param>
        /// <returns>The files of the package and what the conversion reported.</returns>
        /// <exception cref="UrcConversionException">The bundle is missing or the chart could not be converted.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        internal static async UniTask<UrcPezResult> SimaiToPezAsync(string maidata, int? inote, IReadOnlyList<string> files,
                                                                   CancellationToken token = default)
        {
            var engine = await GetEngineAsync(token);
            await UniTask.SwitchToThreadPool();
            token.ThrowIfCancellationRequested();
            var result = engine.SimaiToPez(maidata, inote, files);
            LogReport("simai -> pez", result.Warnings, result.Report);
            return result;
        }

        /// <summary>
        /// Converts the chart of a Phira .pez package (RPE or PhiEdit PEC, told apart by content) to maidata.txt.
        /// </summary>
        /// <param name="chartText">The text of the package's chart file.</param>
        /// <param name="infoYml">The text of the package's info.yml, or null when it has none.</param>
        /// <param name="infoTxt">The text of the package's older info.txt, or null when it has none.</param>
        /// <param name="token">Cancels waiting for the engine; a conversion that has started runs to the end.</param>
        /// <returns>The maidata text, the media the chart refers to and what the conversion reported.</returns>
        /// <exception cref="UrcConversionException">The bundle is missing or the chart could not be converted.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        internal static async UniTask<UrcSimaiResult> PezToSimaiAsync(string chartText, string? infoYml, string? infoTxt,
                                                                     CancellationToken token = default)
        {
            var engine = await GetEngineAsync(token);
            await UniTask.SwitchToThreadPool();
            token.ThrowIfCancellationRequested();
            var result = engine.PezToSimai(chartText, infoYml, infoTxt);
            LogReport("pez -> simai", result.Warnings, result.Report);
            return result;
        }

        /// <summary>
        /// Gets the engine, loading the bundle on the main thread and running it on the thread pool the first time.
        /// </summary>
        /// <param name="token">Cancels waiting for the engine.</param>
        /// <returns>The engine.</returns>
        /// <exception cref="UrcConversionException">The bundle is missing or could not be run.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="token"/> was canceled.</exception>
        private static async UniTask<UrcEngine> GetEngineAsync(CancellationToken token)
        {
            if (s_engine is not null)
            {
                return s_engine;
            }
            await s_engineGate.WaitAsync(token);
            try
            {
                if (s_engine is not null)
                {
                    return s_engine;
                }
                await UniTask.SwitchToMainThread(token);
                var asset = Resources.Load<TextAsset>(BundleResourcePath);
                if (asset == null)
                {
                    throw new UrcConversionException($"The URC bundle Resources/{BundleResourcePath} is missing.");
                }
                var source = asset.text;
                Resources.UnloadAsset(asset);
                await UniTask.SwitchToThreadPool();
                var startedAt = DateTime.UtcNow;
                s_engine = new UrcEngine(source);
                MajDebug.LogInfo(nameof(UrcConverter), $"Loaded the URC bundle in {(DateTime.UtcNow - startedAt).TotalMilliseconds:F0} ms");
                return s_engine;
            }
            finally
            {
                s_engineGate.Release();
            }
        }

        /// <summary>
        /// Logs what a conversion reported.
        /// </summary>
        /// <param name="direction">The conversion, for the log.</param>
        /// <param name="warnings">The warnings about the source chart.</param>
        /// <param name="report">The fidelity summary and what the conversion changed.</param>
        private static void LogReport(string direction, IReadOnlyList<string> warnings, IReadOnlyList<string> report)
        {
            if (warnings.Count > 0)
            {
                MajDebug.LogWarning(nameof(UrcConverter), $"{direction} warnings:\n{string.Join("\n", warnings)}");
            }
            MajDebug.LogInfo(nameof(UrcConverter), $"{direction}:\n{string.Join("\n", report)}");
        }
    }
}
