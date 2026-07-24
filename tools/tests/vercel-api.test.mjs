// Contract tests for the serverless API layer, exercised directly (no HTTP
// server) via node:test. Run with: npm test
//
// These assert the same behaviors the ASP.NET integration suite asserts, so
// the two hosts cannot silently drift apart on the shared contract.

import { test } from "node:test";
import assert from "node:assert/strict";

import listHandler from "../../api/incidents/index.js";
import geojsonHandler from "../../api/incidents/geojson.js";
import detailHandler from "../../api/incidents/[reportNumber].js";
import summaryHandler from "../../api/stats/summary.js";
import trendHandler from "../../api/stats/trend.js";
import countyStatsHandler from "../../api/stats/counties.js";
import countiesHandler from "../../api/counties.js";
import importsHandler from "../../api/imports/index.js";
import quarantineHandler from "../../api/imports/[id]/quarantine.js";

function invoke(handler, url, query = {}) {
  const req = { url, query };
  const res = {
    statusCode: 200,
    headers: {},
    body: "",
    setHeader(key, value) { this.headers[key] = value; },
    end(chunk) { this.body = chunk ?? ""; },
  };
  handler(req, res);
  return { status: res.statusCode, json: res.body ? JSON.parse(res.body) : null };
}

test("incident list pages with envelope", () => {
  const { status, json } = invoke(listHandler, "/api/incidents?pageSize=12");
  assert.equal(status, 200);
  assert.equal(json.items.length, 12);
  assert.equal(json.page, 1);
  assert.ok(json.total > 600);
});

test("filters narrow results and combine with AND", () => {
  const all = invoke(listHandler, "/api/incidents").json.total;
  const marine = invoke(listHandler, "/api/incidents?medium=MarineWater").json.total;
  const marineDiesel = invoke(listHandler, "/api/incidents?medium=MarineWater&category=DieselFuel").json.total;
  assert.ok(marine < all);
  assert.ok(marineDiesel < marine);
  assert.ok(marineDiesel > 0);
});

test("county filter matches stats rollup", () => {
  const listTotal = invoke(listHandler, "/api/incidents?county=Thurston").json.total;
  const stats = invoke(countyStatsHandler, "/api/stats/counties").json;
  const thurston = stats.find((c) => c.county === "Thurston");
  assert.equal(listTotal, thurston.count);
  assert.equal(thurston.fipsCode, "53067");
  assert.equal(thurston.region, "Southwest");
});

test("bad enum returns problem details naming accepted values", () => {
  const { status, json } = invoke(listHandler, "/api/incidents?medium=Lava");
  assert.equal(status, 400);
  assert.match(json.errors.query[0], /MarineWater/);
});

test("bad bbox returns 400", () => {
  assert.equal(invoke(listHandler, "/api/incidents?bbox=1,2,3").status, 400);
});

test("quantity sort is descending with nulls last", () => {
  const { json } = invoke(listHandler, "/api/incidents?sort=-quantity&pageSize=5");
  const quantities = json.items.map((i) => i.quantityGallons);
  assert.ok(quantities.every((q) => q != null));
  for (let i = 1; i < quantities.length; i++) assert.ok(quantities[i - 1] >= quantities[i]);
});

test("detail returns full record; unknown report number 404s", () => {
  const first = invoke(listHandler, "/api/incidents?pageSize=1").json.items[0];
  const detail = invoke(detailHandler, `/api/incidents/${first.reportNumber}`,
    { reportNumber: first.reportNumber });
  assert.equal(detail.status, 200);
  assert.equal(detail.json.reportNumber, first.reportNumber);
  assert.ok("description" in detail.json);

  assert.equal(invoke(detailHandler, "/api/incidents/ERTS-1999-000000",
    { reportNumber: "ERTS-1999-000000" }).status, 404);
});

test("geojson is RFC 7946 with longitude-first positions", () => {
  const { json } = invoke(geojsonHandler, "/api/incidents/geojson?category=CrudeOil");
  assert.equal(json.type, "FeatureCollection");
  assert.ok(json.features.length > 0);
  const [lon, lat] = json.features[0].geometry.coordinates;
  assert.ok(lon < 0 && lat > 45);
});

test("summary totals reconcile with the incident list", () => {
  const summary = invoke(summaryHandler, "/api/stats/summary").json;
  const total = invoke(listHandler, "/api/incidents").json.total;
  assert.equal(summary.totalIncidents, total);
  const mediumSum = summary.byMedium.reduce((s, b) => s + b.count, 0);
  assert.equal(mediumSum, total);
});

test("trend is chronological YYYY-MM", () => {
  const points = invoke(trendHandler, "/api/stats/trend").json;
  assert.ok(points.length > 12);
  for (const p of points) assert.match(p.month, /^\d{4}-\d{2}$/);
  const sorted = [...points].sort((a, b) => a.month.localeCompare(b.month));
  assert.deepEqual(points, sorted);
});

test("reference and audit endpoints serve the snapshot", () => {
  assert.equal(invoke(countiesHandler, "/api/counties").json.length, 39);

  const runs = invoke(importsHandler, "/api/imports").json;
  assert.ok(runs.length >= 1);

  const withRejects = runs.find((r) => r.rejectedCount > 0);
  const quarantine = invoke(quarantineHandler,
    `/api/imports/${withRejects.id}/quarantine`, { id: String(withRejects.id) });
  assert.equal(quarantine.json.length, withRejects.rejectedCount);
  assert.ok(quarantine.json[0].reasons.length > 0);

  assert.equal(invoke(quarantineHandler, "/api/imports/9999/quarantine", { id: "9999" }).status, 404);
});
