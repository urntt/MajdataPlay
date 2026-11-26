// Checks that the bundle (Assets/Resources/Urc/UrcBundle.txt) converts exactly as the URC command line does.
// Usage: URC_DIR=/path/to/universal-rhythm-chart node check.mjs [maidata.txt]
// The maidata defaults to URC's examples/urc-demo. Work files go to a temporary folder that is removed afterwards.
import { execFileSync } from "node:child_process";
import { mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";
import vm from "node:vm";

const here = dirname(fileURLToPath(import.meta.url));
const urcDir = resolve(process.env.URC_DIR ?? join(here, "../../../universal-rhythm-chart"));
const bundlePath = resolve(here, "../../Assets/Resources/Urc/UrcBundle.txt");
const maidataPath = resolve(process.argv[2] ?? join(urcDir, "examples/urc-demo/maidata.txt"));
const cli = join(urcDir, "packages/cli/src/main.ts");

// fflate is a dependency of URC's command line; pnpm keeps it under node_modules/.pnpm.
const fflateDir = readdirSync(join(urcDir, "node_modules/.pnpm")).find((name) => name.startsWith("fflate@"));
if (!fflateDir) throw new Error(`fflate not found under ${urcDir}/node_modules/.pnpm; run pnpm install there`);
const { strFromU8, strToU8, unzipSync, zipSync } = await import(
	pathToFileURL(join(urcDir, "node_modules/.pnpm", fflateDir, "node_modules/fflate/esm/index.mjs")).href
);

const context = {};
vm.createContext(context);
vm.runInContext(readFileSync(bundlePath, "utf8"), context);
const bridge = context.UrcBridge;
const work = mkdtempSync(join(tmpdir(), "urc-check-"));
let ok = true;

function convert(input, to, output) {
	execFileSync("node", [cli, "convert", input, "--to", to, "-o", output], { stdio: "pipe" });
}

function expectSame(label, actual, expected) {
	const same = actual === expected;
	ok &&= same;
	console.log(`${label}: ${same ? "identical" : "DIFFERENT"}`);
}

try {
	// simai -> pez: the CLI reads an .adx holding the same file names the game passes.
	const maidata = readFileSync(maidataPath, "utf8");
	const files = ["track.mp3", "bg.png"];
	const pez = JSON.parse(bridge.simaiToPez(JSON.stringify({ maidata, files })));
	writeFileSync(join(work, "in.adx"), zipSync({ "maidata.txt": strToU8(maidata), "track.mp3": strToU8("x"), "bg.png": strToU8("y") }));
	convert(join(work, "in.adx"), "pez", join(work, "cli.pez"));
	const cliPez = unzipSync(readFileSync(join(work, "cli.pez")));
	for (const name of ["chart.json", "info.yml"]) {
		expectSame(`simai -> pez ${name}`, pez.files[name], strFromU8(cliPez[name]));
	}

	// pez -> simai: RPE, RPE with an info.yml offset, PEC stored as chart.json, PEC described by the older info.txt.
	const pec = [
		"0", "bp 0.000 150.000", "cp 0 0.000 1024.00 200.00", "cd 0 0.000 0.000", "ca 0 0.000 255", "cv 0 0.000 10.000",
		"n1 0 1.00 0.00 1 0", "# 1.000", "& 1.00", "n2 0 3.00 4.00 512.00 1 0", "# 1.000", "& 1.00",
		"n3 0 5.00 256.00 1 0", "# 1.000", "& 1.00", "n4 0 6.00 -256.00 1 0", "# 1.000", "& 1.00", "",
	].join("\r\n");
	const rpeChart = pez.files["chart.json"];
	const rpeInfo = pez.files["info.yml"];
	const cases = {
		rpe: { "chart.json": rpeChart, "info.yml": rpeInfo },
		"rpe-offset": { "chart.json": rpeChart, "info.yml": rpeInfo.replace(/^offset:.*$/m, "offset: 0.25") },
		"pec-as-json": {
			"chart.json": pec,
			"info.yml": 'name: "PEC Song"\nlevel: "IN Lv.13"\nchart: "chart.json"\nmusic: "song.ogg"\nillustration: "bg.jpg"\noffset: 0.1\n',
		},
		"pec-info-txt": {
			"chart.pec": pec,
			"info.txt": "#\nName: Old Song\nSong: song.ogg\nPicture: bg.png\nChart: chart.pec\nLevel: HD Lv.10\n",
		},
	};
	for (const [name, entries] of Object.entries(cases)) {
		const zipped = {};
		for (const [entry, text] of Object.entries(entries)) zipped[entry] = strToU8(text);
		writeFileSync(join(work, `${name}.pez`), zipSync(zipped));
		convert(join(work, `${name}.pez`), "simai", join(work, `${name}.txt`));
		const chartName = Object.keys(entries).find((entry) => !/^info\./i.test(entry));
		const simai = JSON.parse(
			bridge.pezToSimai(
				JSON.stringify({ chart: entries[chartName], infoYml: entries["info.yml"] ?? null, infoTxt: entries["info.txt"] ?? null }),
			),
		);
		expectSame(`pez -> simai ${name} (${simai.format})`, simai.maidata, readFileSync(join(work, `${name}.txt`), "utf8"));
	}
} finally {
	rmSync(work, { recursive: true, force: true });
}
process.exit(ok ? 0 : 1);
