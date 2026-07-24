// Loads the published data snapshot bundled with the serverless functions.
// See tools/export-vercel-data.mjs for how the snapshot is produced from the
// ASP.NET Core system of record.

import { readFileSync } from "node:fs";

const dataUrl = new URL("./data.json", import.meta.url);
const snapshot = JSON.parse(readFileSync(dataUrl, "utf8"));

export const counties = snapshot.counties;
export const incidents = snapshot.incidents;
export const importRuns = snapshot.imports;
export const quarantineByRun = snapshot.quarantine;
export const exportedAtUtc = snapshot.exportedAtUtc;
