import { copyFile, mkdir, writeFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const root = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const output = resolve(root, "Host", "wwwroot", "Scripts", "vendor");
const assets = new Map([
  ["node_modules/jquery/dist/jquery.js", "jquery.js"],
  ["node_modules/knockout/build/output/knockout-latest.debug.js", "knockout.js"],
  ["node_modules/@microsoft/signalr/dist/browser/signalr.js", "signalr.js"]
]);

await mkdir(output, { recursive: true });

for (const [source, destination] of assets) {
  await copyFile(resolve(root, source), resolve(output, destination));
}

await writeFile(resolve(output, ".client-assets.stamp"), "");
