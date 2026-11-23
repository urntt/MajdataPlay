// Bundles entry.ts and the URC packages it imports into one script for the Jint interpreter in game.
// Usage: URC_DIR=/path/to/universal-rhythm-chart node build.mjs
// URC_DIR must be a checkout with its dependencies installed (pnpm install). The output is
// Assets/Resources/Urc/UrcBundle.txt, a TextAsset loaded by UrcConverter.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { build } from "esbuild";

const here = dirname(fileURLToPath(import.meta.url));
const urcDir = resolve(process.env.URC_DIR ?? join(here, "../../../universal-rhythm-chart"));
const outFile = resolve(here, "../../Assets/Resources/Urc/UrcBundle.txt");

const packages = ["core", "convert", "format-simai", "format-rpe"];
const alias = Object.fromEntries(
	packages.map((name) => [`@urc/${name}`, join(urcDir, "packages", name, "src", "index.ts")]),
);

function git(...args) {
	return execFileSync("git", ["-C", urcDir, ...args], { encoding: "utf8" }).trim();
}

const commit = git("rev-parse", "HEAD");
const dirty = git("status", "--porcelain", "--untracked-files=no") !== "";
const version = JSON.parse(readFileSync(join(urcDir, "package.json"), "utf8")).version;

const result = await build({
	entryPoints: [join(here, "entry.ts")],
	bundle: true,
	format: "iife",
	platform: "neutral",
	target: "es2022",
	minify: true,
	legalComments: "none",
	alias,
	mainFields: ["module", "main"],
	write: false,
});

const header = `// Universal Rhythm Chart ${version} @ ${commit}${dirty ? " (with local changes)" : ""}, bundled by Tools/Urc/build.mjs. Do not edit.\n`;
mkdirSync(dirname(outFile), { recursive: true });
writeFileSync(outFile, header + result.outputFiles[0].text);
console.log(`wrote ${outFile} (${(result.outputFiles[0].text.length / 1024).toFixed(0)} KiB, URC ${commit.slice(0, 7)})`);
