/**
 * The part of Universal Rhythm Chart (URC) that MajdataPlay runs in game, through the Jint JavaScript interpreter.
 *
 * It does what `urc convert --to pez` and `urc convert --to adx` do with their default options, but on text and
 * file names instead of files: the game unpacks and packs the archives itself. Every function takes and returns
 * one JSON string, so the C# side only exchanges strings with the interpreter.
 *
 * Built into Assets/Resources/Urc/UrcBundle.txt by build.mjs; see README.md.
 */
import { FIDELITIES, Report } from "@urc/convert";
import type { Chart } from "@urc/core";
import { lowerToRpe, pezTextFiles, readRpe, writeRpe } from "@urc/format-rpe";
import { lowerToSimai, readMaidata, writeMaidata } from "@urc/format-simai";

/** Jint has no structuredClone; URC only clones plain JSON defaults with it. */
if (typeof (globalThis as { structuredClone?: unknown }).structuredClone !== "function") {
	(globalThis as { structuredClone?: unknown }).structuredClone = (value: unknown) =>
		JSON.parse(JSON.stringify(value));
}

interface SimaiToPezArgs {
	/** The text of maidata.txt. */
	maidata: string;
	/** The simai inote number of the difficulty to convert; the last one present when missing. */
	inote?: number | null;
	/** The names of the files next to maidata.txt, as the CLI sees the members of an .adx. */
	files: string[];
}

interface PezToSimaiArgs {
	/** The text of the RPE chart file named by info.yml. */
	chart: string;
}

/** The fidelity summary and grouped diagnostics, as the CLI prints them. */
function describeReport(report: Report, source: Chart, outputCount: number): string[] {
	const s = report.summarize(
		source.interactions.map((ix) => ix.id),
		outputCount,
	);
	const lines = [
		`fidelity (${s.sourceInteractions} source interaction(s) -> ${s.outputInteractions} in ${s.target}): ${FIDELITIES.map((f) => `${f} ${s.counts[f]}`).join(", ")}`,
	];
	const seen = new Map<string, { n: number; text: string }>();
	for (const d of report.diagnostics) {
		const key = `${d.rule}|${d.message.replace(/\d+/g, "N")}`;
		const entry = seen.get(key);
		if (entry) {
			entry.n++;
		} else {
			seen.set(key, { n: 1, text: `[${d.fidelity}] ${d.rule}: ${d.message}` });
		}
	}
	for (const { n, text } of seen.values()) {
		lines.push(n > 1 ? `${text} (x${n})` : text);
	}
	return lines;
}

/** The same media choice as the CLI's .adx loader. */
function pickFile(files: readonly string[], pattern: RegExp): string | undefined {
	return files.find((name) => pattern.test(name));
}

/**
 * Converts one difficulty of a simai chart to the text files of a Phira .pez package.
 * @param argsJson A JSON SimaiToPezArgs.
 * @returns JSON with the files to write (`chart.json`, `info.yml`), the names the package uses for the music and
 * the illustration, the inote that was converted, warnings and the fidelity report.
 */
function simaiToPez(argsJson: string): string {
	const args = JSON.parse(argsJson) as SimaiToPezArgs;
	const result = readMaidata(args.maidata, {
		assets: {
			audio: pickFile(args.files, /^track\.(mp3|ogg|wav|m4a)$/i),
			cover: pickFile(args.files, /^bg\.(png|jpe?g)$/i),
			video: pickFile(args.files, /^pv\.mp4$/i),
		},
	});
	const warnings: string[] = [];
	for (const c of result.charts) {
		for (const d of c.diagnostics) warnings.push(`inote_${c.index}: ${d.severity}: ${d.message}`);
	}
	if (result.charts.length === 0) throw new Error("the chart has no readable difficulty");
	const picked =
		args.inote === undefined || args.inote === null
			? result.charts[result.charts.length - 1]!
			: result.charts.find((c) => c.index === args.inote);
	if (!picked) {
		throw new Error(`no difficulty inote_${args.inote}; available: ${result.charts.map((c) => c.index).join(", ")}`);
	}
	const report = new Report("pez");
	const lowered = lowerToRpe(picked.chart, report, {});
	const music = (picked.chart.assets ?? []).find((a) => a.role === "audio")?.uri ?? "song.mp3";
	const illustration = (picked.chart.assets ?? []).find((a) => a.role === "image")?.uri ?? "background.png";
	const rpe = writeRpe(lowered, { song: music, background: illustration });
	return JSON.stringify({
		inote: picked.index,
		files: pezTextFiles(lowered, rpe, { music, illustration }),
		music,
		illustration,
		warnings,
		report: describeReport(report, picked.chart, lowered.interactions.length),
	});
}

/**
 * Converts the RPE chart of a Phira .pez package to maidata.txt.
 * @param argsJson A JSON PezToSimaiArgs.
 * @returns JSON with the maidata text, the names of the music and illustration files the chart refers to, the
 * title, warnings and the fidelity report.
 */
function pezToSimai(argsJson: string): string {
	const args = JSON.parse(argsJson) as PezToSimaiArgs;
	const { chart, warnings } = readRpe(JSON.parse(args.chart) as unknown);
	const report = new Report("adx");
	const lowered = lowerToSimai(chart, report, {});
	const maidata = writeMaidata([{ chart: lowered }]);
	return JSON.stringify({
		maidata,
		music: (chart.assets ?? []).find((a) => a.role === "audio")?.uri ?? null,
		illustration: (chart.assets ?? []).find((a) => a.role === "image")?.uri ?? null,
		title: chart.meta.title ?? null,
		warnings,
		report: describeReport(report, chart, lowered.interactions.length),
	});
}

(globalThis as { UrcBridge?: unknown }).UrcBridge = { simaiToPez, pezToSimai };
