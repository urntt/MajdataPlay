# URC bundle

MajdataPlay imports and exports Phira `.pez` charts with
[Universal Rhythm Chart](https://github.com/urntt/universal-rhythm-chart) (URC). URC is written in TypeScript, so the
game runs it in [Jint](https://github.com/sebastienros/jint), a JavaScript interpreter written in C#
(`Assets/Packages/Jint.*`).

This folder builds the script the game loads: `Assets/Resources/Urc/UrcBundle.txt`.

- `entry.ts` imports URC's simai and RPE adapters and exposes two functions on `globalThis.UrcBridge`. Each function
  takes and returns one JSON string:
  - `simaiToPez` does what `urc convert <chart>.adx --to pez` does;
  - `pezToSimai` does what `urc convert <chart>.pez --to adx` does. It takes the text of the chart file and of
    `info.yml` / `info.txt`, tells RPE from PhiEdit (PEC) the way Phira does, reads the info and its `offset`, and
    refuses official Phigros and binary (PBC) charts.

  Both use the default options. The `.pez` reading follows `load` in URC's `packages/cli/src/io.ts`, which the packages
  do not export, so `entry.ts` keeps a copy of it; update it with URC. The game unpacks and packs the archives itself
  and finds the chart and info files (`Assets/Scripts/Misc/Import/ChartImporter.cs` and `ChartExporter.cs`).
- `build.mjs` bundles `entry.ts` and the URC packages into one minified script. The first line of the output records
  the URC commit it was built from.
- `check.mjs` compares the bundle with the URC command line (see below).

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

The bundle must produce the same files as the URC command line. With the URC checkout ready:

```bash
cd Tools/Urc
URC_DIR=/path/to/universal-rhythm-chart node check.mjs [maidata.txt]
```

It converts the maidata (URC's `examples/urc-demo` by default) to `.pez` with the bundle and with `urc convert`, and
converts four `.pez` packages back to simai both ways: RPE, RPE with an `info.yml` offset, PEC stored as `chart.json`,
and PEC described by the older `info.txt`. Every pair must be identical.

Under Jint the conversion is slower than under Node; a dense 240-bar chart takes about 17 seconds on a desktop.
