# URC bundle

MajdataPlay imports and exports Phira `.pez` charts with
[Universal Rhythm Chart](https://github.com/urntt/universal-rhythm-chart) (URC). URC is written in TypeScript, so the
game runs it in [Jint](https://github.com/sebastienros/jint), a JavaScript interpreter written in C#
(`Assets/Packages/Jint.*`).

This folder builds the script the game loads: `Assets/Resources/Urc/UrcBundle.txt`.

- `entry.ts` imports URC's simai and RPE adapters and exposes two functions on `globalThis.UrcBridge`. Each function
  takes and returns one JSON string:
  - `simaiToPez` does what `urc convert <chart>.adx --to pez` does;
  - `pezToSimai` does what `urc convert <chart>.pez --to adx` does.

  Both use the default options. The game unpacks and packs the archives itself
  (`Assets/Scripts/Misc/Import/ChartImporter.cs` and `ChartExporter.cs`).
- `build.mjs` bundles `entry.ts` and the URC packages into one minified script. The first line of the output records
  the URC commit it was built from.

The bundle is committed, because the URC repository is private and CI cannot fetch it.

## Rebuilding after URC changes

You need Node 22.18 or newer, and a URC checkout with its dependencies installed (`pnpm install`).

```bash
cd Tools/Urc
npm install
URC_DIR=/path/to/universal-rhythm-chart node build.mjs
```

`URC_DIR` defaults to `../../../universal-rhythm-chart`, which is a checkout next to this repository.

## Checking the bundle

The bundle must produce the same files as the URC command line. With the bundle loaded in Node:

```js
const vm = require("node:vm");
const ctx = {};
vm.createContext(ctx);
vm.runInContext(fs.readFileSync("Assets/Resources/Urc/UrcBundle.txt", "utf8"), ctx);
ctx.UrcBridge.simaiToPez(JSON.stringify({ maidata, files: ["track.mp3", "bg.png"] }));
```

Compare the result with `urc convert` on an `.adx` holding the same files:

- `chart.json` and `info.yml` should match the `.pez`;
- `pezToSimai` should match `urc convert <pez> --to simai`.

Under Jint the conversion is slower than under Node; a dense 240-bar chart takes about 17 seconds on a desktop.
