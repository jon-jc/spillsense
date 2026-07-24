// Publishes a data snapshot for the serverless (Vercel) deployment.
//
// The ASP.NET Core application is the system of record: it owns intake,
// validation, and the database. This script exports its API responses into
// api/_lib/data.json, which the Vercel functions serve read-only through the
// identical API contract. Re-run against a live instance whenever the
// published dataset should be refreshed.
//
// Usage: node tools/export-vercel-data.mjs [baseUrl]   (default http://localhost:5178)

import { writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const base = process.argv[2] ?? "http://localhost:5178";
const outPath = join(dirname(fileURLToPath(import.meta.url)), "..", "api", "_lib", "data.json");

async function get(path) {
  const response = await fetch(`${base}${path}`);
  if (!response.ok) throw new Error(`${path} -> ${response.status}`);
  return response.json();
}

const counties = await get("/api/counties");

// Page through summaries, then pull full details so the snapshot can serve
// the detail endpoint too.
const summaries = [];
let page = 1;
for (;;) {
  const result = await get(`/api/incidents?page=${page}&pageSize=200&sort=reportedAt`);
  summaries.push(...result.items);
  if (summaries.length >= result.total) break;
  page++;
}

const incidents = [];
for (const summary of summaries) {
  incidents.push(await get(`/api/incidents/${encodeURIComponent(summary.reportNumber)}`));
}

const imports = await get("/api/imports");
const quarantine = {};
for (const run of imports) {
  if (run.rejectedCount > 0) {
    quarantine[run.id] = await get(`/api/imports/${run.id}/quarantine`);
  }
}

writeFileSync(outPath, JSON.stringify({
  exportedAtUtc: new Date().toISOString(),
  source: base,
  counties,
  incidents,
  imports,
  quarantine,
}));

console.log(`Exported ${incidents.length} incidents, ${counties.length} counties, ` +
  `${imports.length} import runs -> ${outPath}`);
