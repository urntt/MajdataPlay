/**
 * The part of Universal Rhythm Chart (URC) that MajdataPlay runs in game, through the Jint JavaScript interpreter.
 *
 * It does what `urc convert --to pez` and `urc convert --to adx` do with their default options, but on text and
 * file names instead of files: the game unpacks and packs the archives itself, and finds the chart and info files. Every function takes and returns
 * one JSON string, so the C# side only exchanges strings with the interpreter.
 *
 * Built into Assets/Resources/Urc/UrcBundle.txt by build.mjs; see README.md.
 */
import { FIDELITIES, Report } from "@urc/convert";
import { type Chart, parseChart, Rat } from "@urc/core";
import {
	lowerToRpe,
	parseRpeLevelText,
	pezTextFiles,
	RPE_OFFSET_SIGN,
	readPec,
	readRpe,
	writeRpe,
} from "@urc/format-rpe";
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
	/** The text of the package's chart file (RPE JSON or PEC, whatever its name). */
	chart: string;
	/** The text of info.yml (or info.yaml), when the package has one. */
	infoYml?: string | null;
	/** The text of the older info.txt, when the package has one. */
	infoTxt?: string | null;
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

// The functions below follow the .pez branch of `load` in URC's packages/cli/src/io.ts (commit 0a89c9f), which the
// packages do not export. Keep them in step with it when URC changes; README.md says how to check.

/** The `key: value` lines of a package's info file, keys in lower case (info.yml, or the older info.txt). */
function readInfo(text: string): Map<string, string> {
	const info = new Map<string, string>();
	for (const line of text.replace(/^\uFEFF/, "").split(/\r\n|\r|\n/)) {
		const m = /^([A-Za-z][\w-]*)\s*:\s*(.*)$/.exec(line);
		if (!m) continue;
		let value = m[2]!.trim();
		if (value.startsWith('"')) {
			try {
				value = JSON.parse(value) as string;
			} catch {
				value = value.slice(1, -1);
			}
		} else {
			value = value.replace(/^'(.*)'$/, "$1");
		}
		info.set(m[1]!.toLowerCase(), value);
	}
	return info;
}

/** Package info, with the older info.txt names (Song, Picture) mapped to info.yml's; info.yml wins. */
function infoOf(infoYml: string | null | undefined, infoTxt: string | null | undefined): Map<string, string> {
	const info = new Map<string, string>();
	if (infoTxt) {
		const old = readInfo(infoTxt);
		for (const [from, to] of [
			["song", "music"],
			["picture", "illustration"],
		] as const) {
			if (old.has(from)) old.set(to, old.get(from)!);
		}
		for (const [k, v] of old) info.set(k, v);
	}
	if (infoYml) for (const [k, v] of readInfo(infoYml)) info.set(k, v);
	return info;
}

/** What info says about the chart, in URC terms: PEC has nothing of the sort in the chart itself. */
function metaFromInfo(info: Map<string, string>): Chart["meta"] {
	const level = info.get("level");
	const value = Number(info.get("difficulty"));
	const said = (k: string) => {
		const v = info.get(k);
		return v && v !== "null" ? v : undefined;
	};
	return {
		...(said("name") ? { title: said("name")! } : {}),
		...(said("composer") ? { artist: said("composer")! } : {}),
		...(said("charter") ? { charter: said("charter")! } : {}),
		...(said("illustrator") ? { illustrator: said("illustrator")! } : {}),
		...(level
			? {
					difficulty: {
						...parseRpeLevelText(level),
						...(Number.isFinite(value) && value > 0 ? { value } : {}),
					},
				}
			: {}),
	};
}

/** The package's own offset in seconds, which Phira adds to the chart's. */
function infoOffset(info: Map<string, string>): Rat {
	const text = info.get("offset");
	if (!text || text === "null") return Rat.ZERO;
	try {
		return Rat.fromDecimal(text);
	} catch {
		return Rat.ZERO;
	}
}

/** Move beat 0 by `seconds` on the audio clock, the way an info.yml offset moves an RPE chart in Phira. */
function shiftAudio(chart: Chart, seconds: Rat): Chart {
	if (seconds.isZero()) return chart;
	const metric = chart.time.domains.find((d) => d.kind === "metric")?.id;
	const by = seconds.mul(BigInt(RPE_OFFSET_SIGN));
	const maps = chart.time.maps.map((m) =>
		m.from === metric && "segments" in m
			? { ...m, segments: m.segments.map((s) => ({ ...s, at: Rat.parse(s.at).add(by).toString() })) }
			: m,
	);
	return { ...chart, time: { ...chart.time, maps } };
}

/**
 * The chart format of a Phira chart file, as Phira decides it: the info's `format` if it names one, otherwise the
 * content: `{` with `"META"` is RPE, other JSON is the official Phigros format, other text is PEC. The game reports
 * a chart that is not UTF-8 text (PBC) before it calls in here.
 */
function phiraFormat(text: string, declared: string | undefined): "rpe" | "pec" | "pgr" | "pbc" {
	const named = declared?.toLowerCase();
	if (named === "rpe" || named === "pec" || named === "pgr" || named === "pbc") return named;
	const body = text.replace(/^\uFEFF/, "").trimStart();
	if (body.startsWith("{")) return body.includes('"META"') ? "rpe" : "pgr";
	return "pec";
}

/** Reads the chart of a .pez package the way URC's command line does. */
function readPezChart(args: PezToSimaiArgs): { chart: Chart; warnings: string[]; format: "rpe" | "pec" } {
	const info = infoOf(args.infoYml, args.infoTxt);
	const declared = info.get("format");
	switch (phiraFormat(args.chart, declared && declared !== "null" ? declared : undefined)) {
		case "pec": {
			const { chart, warnings } = readPec(args.chart, {
				meta: metaFromInfo(info),
				assets: {
					...(info.get("music") ? { music: info.get("music")! } : {}),
					...(info.get("illustration") ? { illustration: info.get("illustration")! } : {}),
				},
				infoOffset: infoOffset(info),
			});
			return { chart, warnings, format: "pec" };
		}
		case "pgr":
			throw new Error("unsupported-format:pgr: an official Phigros chart, which is not supported yet");
		case "pbc":
			throw new Error("unsupported-format:pbc: a binary chart (PBC), which is not supported yet");
		default: {
			let json: Record<string, unknown>;
			try {
				json = JSON.parse(args.chart) as Record<string, unknown>;
			} catch (e) {
				throw new Error(`not an RPE chart or URC document: the file is not JSON (${(e as Error).message})`);
			}
			if (typeof json.urc === "string") {
				return { chart: parseChart(json), warnings: [], format: "rpe" };
			}
			if (!Array.isArray(json.judgeLineList)) {
				throw new Error("JSON is neither a URC document nor an RPE chart");
			}
			const { chart, warnings } = readRpe(json);
			return { chart: shiftAudio(chart, infoOffset(info)), warnings, format: "rpe" };
		}
	}
}

/**
 * Converts the chart of a Phira .pez package (RPE or PEC) to maidata.txt.
 * @param argsJson A JSON PezToSimaiArgs.
 * @returns JSON with the maidata text, the format that was read, the names of the music and illustration files the
 * chart refers to, the title, warnings and the fidelity report.
 */
function pezToSimai(argsJson: string): string {
	const args = JSON.parse(argsJson) as PezToSimaiArgs;
	const { chart, warnings, format } = readPezChart(args);
	const report = new Report("adx");
	const lowered = lowerToSimai(chart, report, {});
	const maidata = writeMaidata([{ chart: lowered }]);
	return JSON.stringify({
		maidata,
		format,
		music: (chart.assets ?? []).find((a) => a.role === "audio")?.uri ?? null,
		illustration: (chart.assets ?? []).find((a) => a.role === "image")?.uri ?? null,
		title: chart.meta.title ?? null,
		warnings,
		report: describeReport(report, chart, lowered.interactions.length),
	});
}

(globalThis as { UrcBridge?: unknown }).UrcBridge = { simaiToPez, pezToSimai };
