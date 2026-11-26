using System.Collections.Generic;
using System.Text;

#nullable enable
namespace MajdataPlay.Import
{
    /// <summary>
    /// Why something could not be imported. Each value has the text key <c>MAJTEXT_IMPORT_FAIL_&lt;value&gt;</c>.
    /// </summary>
    internal enum ChartImportFailure
    {
        /// <summary>The file or folder is gone or cannot be read.</summary>
        NotFound,

        /// <summary>A RAR, 7z, gzip, bzip2 or xz archive, which the game cannot unpack; the detail is the format.</summary>
        UnsupportedArchive,

        /// <summary>The ZIP archive is damaged; the detail is the library's message.</summary>
        BrokenArchive,

        /// <summary>The archive has an entry that would be written outside the import folder.</summary>
        UnsafeArchive,

        /// <summary>Archives nested too deeply.</summary>
        TooDeep,

        /// <summary>No simai or Phira chart was found.</summary>
        NoChart,

        /// <summary>A chart file on its own, without its music.</summary>
        LoneChartFile,

        /// <summary>An audio, image or video file rather than a chart; the detail is the kind of file.</summary>
        MediaFile,

        /// <summary>A file of no known kind.</summary>
        UnknownFile,

        /// <summary>A chart without a music file.</summary>
        MissingMusic,

        /// <summary>A chart whose music the game cannot play; the detail is the format.</summary>
        UnsupportedMusic,

        /// <summary>An official Phigros chart, which URC cannot read yet.</summary>
        PhigrosChart,

        /// <summary>A binary (PBC) Phira chart, which URC cannot read yet.</summary>
        BinaryChart,

        /// <summary>URC could not convert the Phira chart; the detail is URC's reason.</summary>
        ConvertFailed,

        /// <summary>Copying the chart into the chart folder failed; the detail is the I/O message.</summary>
        CopyFailed,
    }

    /// <summary>
    /// Something that may surprise the player about an imported chart. Each value has the text key
    /// <c>MAJTEXT_IMPORT_WARN_&lt;value&gt;</c>.
    /// </summary>
    internal enum ChartImportWarning
    {
        /// <summary>No cover image was found.</summary>
        NoCover,

        /// <summary>The video is in a format the game does not play; the detail is the format.</summary>
        UnsupportedVideo,

        /// <summary>Files were renamed to the names the game reads; the detail lists them.</summary>
        Renamed,

        /// <summary>Entry names were decoded from a legacy code page; the detail is the encoding.</summary>
        LegacyNames,

        /// <summary>The chart text was converted to UTF-8; the detail is the encoding it was in.</summary>
        ConvertedText,
    }

    /// <summary>
    /// What one import did: the charts it added, what failed and why, and what was adjusted, for the loading screen and
    /// the import result dialog.
    /// </summary>
    internal sealed class ChartImportReport
    {
        /// <summary>One imported chart.</summary>
        internal readonly struct Chart
        {
            /// <summary>Initializes a new instance of the <see cref="Chart"/> struct.</summary>
            /// <param name="name">The folder name the chart got.</param>
            /// <param name="collection">The collection the chart went to.</param>
            public Chart(string name, string collection)
            {
                Name = name;
                Collection = collection;
            }

            /// <summary>Gets the folder name the chart got.</summary>
            public string Name { get; }

            /// <summary>Gets the collection the chart went to.</summary>
            public string Collection { get; }
        }

        /// <summary>One failure or warning.</summary>
        internal readonly struct Item
        {
            /// <summary>Initializes a new instance of the <see cref="Item"/> struct.</summary>
            /// <param name="source">The file, folder or chart the item is about.</param>
            /// <param name="textKey">The text key of the reason.</param>
            /// <param name="detail">Extra information inserted into the text, or null.</param>
            public Item(string source, string textKey, string? detail)
            {
                Source = source;
                TextKey = textKey;
                Detail = detail;
            }

            /// <summary>Gets the file, folder or chart the item is about.</summary>
            public string Source { get; }

            /// <summary>Gets the text key of the reason.</summary>
            public string TextKey { get; }

            /// <summary>Gets extra information inserted into the text, or null.</summary>
            public string? Detail { get; }

            /// <summary>Gets the localized line "source: reason".</summary>
            /// <returns>The line.</returns>
            public string ToText()
            {
                var reason = Detail is null ? TextKey.i18n() : TextKey.i18n(Detail);
                return "MAJTEXT_IMPORT_ITEM".i18n(Source, reason);
            }
        }

        /// <summary>The imported charts.</summary>
        private readonly List<Chart> _charts = new();

        /// <summary>The failures.</summary>
        private readonly List<Item> _failures = new();

        /// <summary>The warnings.</summary>
        private readonly List<Item> _warnings = new();

        /// <summary>Gets the imported charts.</summary>
        internal IReadOnlyList<Chart> Charts => _charts;

        /// <summary>Gets the failures.</summary>
        internal IReadOnlyList<Item> Failures => _failures;

        /// <summary>Gets the warnings.</summary>
        internal IReadOnlyList<Item> Warnings => _warnings;

        /// <summary>Gets a value indicating whether nothing was imported, failed or noted.</summary>
        internal bool IsEmpty => _charts.Count == 0 && _failures.Count == 0 && _warnings.Count == 0;

        /// <summary>Records an imported chart.</summary>
        /// <param name="name">The folder name the chart got.</param>
        /// <param name="collection">The collection the chart went to.</param>
        internal void AddChart(string name, string collection)
        {
            _charts.Add(new Chart(name, collection));
        }

        /// <summary>Records a failure.</summary>
        /// <param name="source">The file, folder or chart that failed.</param>
        /// <param name="failure">The reason.</param>
        /// <param name="detail">Extra information for the reason's text, or null.</param>
        internal void AddFailure(string source, ChartImportFailure failure, string? detail = null)
        {
            _failures.Add(new Item(source, $"MAJTEXT_IMPORT_FAIL_{failure}", detail));
        }

        /// <summary>Records a warning.</summary>
        /// <param name="source">The chart the warning is about.</param>
        /// <param name="warning">The warning.</param>
        /// <param name="detail">Extra information for the warning's text, or null.</param>
        internal void AddWarning(string source, ChartImportWarning warning, string? detail = null)
        {
            _warnings.Add(new Item(source, $"MAJTEXT_IMPORT_WARN_{warning}", detail));
        }

        /// <summary>Determines whether a failure of a kind was recorded.</summary>
        /// <param name="failure">The reason.</param>
        /// <returns>True when at least one failure has this reason.</returns>
        internal bool HasFailure(ChartImportFailure failure)
        {
            var key = $"MAJTEXT_IMPORT_FAIL_{failure}";
            foreach (var item in _failures)
            {
                if (item.TextKey == key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Determines whether a warning of a kind was recorded.</summary>
        /// <param name="warning">The warning.</param>
        /// <returns>True when at least one warning has this kind.</returns>
        internal bool HasWarning(ChartImportWarning warning)
        {
            var key = $"MAJTEXT_IMPORT_WARN_{warning}";
            foreach (var item in _warnings)
            {
                if (item.TextKey == key)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Gets the one-line summary, such as "Imported 3 charts, 1 failed".</summary>
        /// <returns>The summary.</returns>
        internal string GetSummary()
        {
            return "MAJTEXT_IMPORT_SUMMARY".i18n(_charts.Count, _failures.Count);
        }

        /// <summary>
        /// Gets the full report: the summary, the imported charts, every failure with its reason and every warning.
        /// </summary>
        /// <param name="maxCharts">The most imported charts listed by name; the rest are counted.</param>
        /// <returns>The text, one item per line.</returns>
        internal string ToText(int maxCharts = 30)
        {
            var builder = new StringBuilder();
            builder.Append(GetSummary());
            if (_failures.Count > 0)
            {
                builder.Append("\n\n").Append("MAJTEXT_IMPORT_FAILURES".i18n());
                foreach (var failure in _failures)
                {
                    builder.Append("\n• ").Append(failure.ToText());
                }
            }
            if (_warnings.Count > 0)
            {
                builder.Append("\n\n").Append("MAJTEXT_IMPORT_WARNINGS".i18n());
                foreach (var warning in _warnings)
                {
                    builder.Append("\n• ").Append(warning.ToText());
                }
            }
            if (_charts.Count > 0)
            {
                builder.Append("\n\n").Append("MAJTEXT_IMPORT_CHARTS".i18n());
                for (var i = 0; i < _charts.Count && i < maxCharts; i++)
                {
                    builder.Append("\n• ").Append(_charts[i].Name).Append(" → ").Append(_charts[i].Collection);
                }
                if (_charts.Count > maxCharts)
                {
                    builder.Append("\n• ").Append("MAJTEXT_IMPORT_MORE_CHARTS".i18n(_charts.Count - maxCharts));
                }
            }
            return builder.ToString();
        }

        /// <summary>
        /// Gets the text for the loading screen: the summary and the first failures.
        /// </summary>
        /// <param name="maxFailures">The most failures listed.</param>
        /// <returns>The text.</returns>
        internal string ToLoadingText(int maxFailures = 3)
        {
            var builder = new StringBuilder(GetSummary());
            for (var i = 0; i < _failures.Count && i < maxFailures; i++)
            {
                builder.Append('\n').Append(_failures[i].ToText());
            }
            if (_failures.Count > maxFailures)
            {
                builder.Append('\n').Append("MAJTEXT_IMPORT_MORE_FAILURES".i18n(_failures.Count - maxFailures));
            }
            return builder.ToString();
        }
    }
}
