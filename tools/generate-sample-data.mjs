// Generates the synthetic sample dataset in data/sample/.
//
// The data is fabricated but shaped like real spill reporting: incidents
// cluster around ports, refineries, rivers, and highway corridors; diesel
// dominates; most spills are small with a long tail; older incidents are
// mostly closed. Company names are fictional. Deterministic (fixed seed) so
// regeneration produces identical files.
//
// Usage: node tools/generate-sample-data.mjs

import { writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const outDir = join(dirname(fileURLToPath(import.meta.url)), "..", "data", "sample");
mkdirSync(outDir, { recursive: true });

// --- deterministic RNG (mulberry32) ---------------------------------------
let seed = 0x5eed2026;
function rand() {
  seed |= 0;
  seed = (seed + 0x6d2b79f5) | 0;
  let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
}
const pick = (arr) => arr[Math.floor(rand() * arr.length)];
const weighted = (pairs) => {
  const total = pairs.reduce((s, [, w]) => s + w, 0);
  let r = rand() * total;
  for (const [value, w] of pairs) {
    r -= w;
    if (r <= 0) return value;
  }
  return pairs[pairs.length - 1][0];
};

// --- geography anchors ------------------------------------------------------
// type: marine | river | lake | land — drives medium and source mix.
const anchors = [
  { name: "Harbor Island, Seattle", lat: 47.575, lon: -122.35, county: "King", water: "Duwamish Waterway", type: "marine", w: 9 },
  { name: "Commencement Bay, Tacoma", lat: 47.27, lon: -122.42, county: "Pierce", water: "Commencement Bay", type: "marine", w: 8 },
  { name: "Budd Inlet, Olympia", lat: 47.06, lon: -122.9, county: "Thurston", water: "Budd Inlet", type: "marine", w: 5 },
  { name: "March Point, Anacortes", lat: 48.5, lon: -122.56, county: "Skagit", water: "Fidalgo Bay", type: "marine", w: 6 },
  { name: "Cherry Point, Ferndale", lat: 48.86, lon: -122.71, county: "Whatcom", water: "Strait of Georgia", type: "marine", w: 5 },
  { name: "Port Angeles Harbor", lat: 48.125, lon: -123.44, county: "Clallam", water: "Strait of Juan de Fuca", type: "marine", w: 4 },
  { name: "Bellingham Bay", lat: 48.74, lon: -122.5, county: "Whatcom", water: "Bellingham Bay", type: "marine", w: 4 },
  { name: "Port Gardner, Everett", lat: 47.98, lon: -122.22, county: "Snohomish", water: "Port Gardner Bay", type: "marine", w: 5 },
  { name: "Sinclair Inlet, Bremerton", lat: 47.56, lon: -122.62, county: "Kitsap", water: "Sinclair Inlet", type: "marine", w: 4 },
  { name: "Friday Harbor", lat: 48.535, lon: -123.01, county: "San Juan", water: "San Juan Channel", type: "marine", w: 2 },
  { name: "Grays Harbor, Aberdeen", lat: 46.97, lon: -123.8, county: "Grays Harbor", water: "Grays Harbor", type: "marine", w: 3 },
  { name: "Ilwaco", lat: 46.3, lon: -124.03, county: "Pacific", water: "Columbia River", type: "marine", w: 2 },
  { name: "Port Townsend", lat: 48.11, lon: -122.79, county: "Jefferson", water: "Admiralty Inlet", type: "marine", w: 2 },
  { name: "Oakland Bay, Shelton", lat: 47.21, lon: -123.1, county: "Mason", water: "Oakland Bay", type: "marine", w: 2 },
  { name: "Neah Bay", lat: 48.37, lon: -124.61, county: "Clallam", water: "Strait of Juan de Fuca", type: "marine", w: 1 },
  { name: "Vancouver waterfront", lat: 45.63, lon: -122.67, county: "Clark", water: "Columbia River", type: "river", w: 5 },
  { name: "Longview", lat: 46.14, lon: -122.94, county: "Cowlitz", water: "Columbia River", type: "river", w: 4 },
  { name: "Kennewick", lat: 46.22, lon: -119.14, county: "Benton", water: "Columbia River", type: "river", w: 4 },
  { name: "Spokane River", lat: 47.66, lon: -117.42, county: "Spokane", water: "Spokane River", type: "river", w: 4 },
  { name: "Wenatchee", lat: 47.42, lon: -120.31, county: "Chelan", water: "Columbia River", type: "river", w: 2 },
  { name: "Yakima River", lat: 46.6, lon: -120.51, county: "Yakima", water: "Yakima River", type: "river", w: 3 },
  { name: "Moses Lake", lat: 47.13, lon: -119.28, county: "Grant", water: "Moses Lake", type: "lake", w: 2 },
  { name: "Lake Washington, Bellevue", lat: 47.61, lon: -122.19, county: "King", water: "Lake Washington", type: "lake", w: 3 },
  { name: "I-5 corridor, Centralia", lat: 46.72, lon: -122.95, county: "Lewis", water: "", type: "land", w: 4 },
  { name: "I-90, Ellensburg", lat: 46.99, lon: -120.55, county: "Kittitas", water: "", type: "land", w: 3 },
  { name: "Walla Walla", lat: 46.06, lon: -118.34, county: "Walla Walla", water: "", type: "land", w: 2 },
  { name: "US-2, Douglas County", lat: 47.6, lon: -119.9, county: "Douglas", water: "", type: "land", w: 1 },
  { name: "SR-14, Skamania", lat: 45.69, lon: -121.9, county: "Skamania", water: "Columbia River", type: "river", w: 1 },
];
const anchorPairs = anchors.map((a) => [a, a.w]);

// --- classification tables --------------------------------------------------
const substancesBySource = {
  Vessel: [
    ["Diesel fuel", 5], ["Oily bilge water", 3], ["Hydraulic oil", 3],
    ["Gasoline", 2], ["Lube oil", 2], ["Bunker C (No. 6 fuel oil)", 1],
  ],
  Facility: [
    ["Diesel fuel", 4], ["Crude oil", 2], ["Bunker C (No. 6 fuel oil)", 2],
    ["Sodium hydroxide solution", 1], ["Untreated wastewater", 1], ["Jet fuel (Jet-A)", 1],
  ],
  Vehicle: [
    ["Diesel fuel", 6], ["Motor oil", 3], ["Hydraulic oil", 3],
    ["Antifreeze (ethylene glycol)", 2], ["Gasoline", 2],
  ],
  RailCar: [["Diesel fuel", 3], ["Crude oil", 2], ["Lube oil", 1]],
  Pipeline: [["Gasoline", 2], ["Jet fuel (Jet-A)", 2], ["Diesel fuel", 2]],
  Unknown: [["Unknown oil sheen", 5], ["Diesel fuel", 2], ["Mystery sheen", 2]],
};
const sourcesByType = {
  marine: [["Vessel", 6], ["Facility", 3], ["Unknown", 2], ["Pipeline", 0.5]],
  river: [["Facility", 3], ["Vehicle", 3], ["Vessel", 2], ["RailCar", 1], ["Unknown", 1]],
  lake: [["Vessel", 3], ["Vehicle", 2], ["Unknown", 1]],
  land: [["Vehicle", 6], ["Facility", 2], ["RailCar", 1], ["Pipeline", 0.5]],
};
const mediumByType = {
  marine: [["Marine Water", 8], ["Land", 1]],
  river: [["Fresh Water", 6], ["Land", 2], ["Groundwater", 1]],
  lake: [["Fresh Water", 6], ["Land", 1]],
  land: [["Land", 7], ["Groundwater", 2], ["Air", 0.5]],
};
const parties = [
  "Cascadia Marine Services LLC", "Rainier Towing & Barge Co.", "Pacific Crest Fuels Inc.",
  "Salish Sea Charters", "Evergreen Freight Lines", "Puget Terminal Operations LLC",
  "Columbia Basin Transport", "Orca Bay Seafoods Processing", "North Sound Excavation",
  "Chinook Petroleum Distributors", "Olympic Peninsula Logging Co.", "Selkirk Rail Services",
  "", "", "", "Unknown",
];
const descTemplates = {
  Vessel: [
    "Sheen observed alongside moored {sub_lc} source vessel; boom deployed by responding crew.",
    "Fuel overflow during vessel bunkering; absorbent pads applied, transfer halted.",
    "Recreational vessel reported taking on water; visible sheen at marina slip.",
    "Fishing vessel bilge discharge produced intermittent sheen near dock.",
  ],
  Facility: [
    "Transfer line drip at terminal loading rack; contained within secondary containment.",
    "Tank overfill alarm failure led to release at bulk fuel facility; vacuum truck dispatched.",
    "Valve gasket failure during product transfer; spill contained on paved apron.",
    "Stormwater outfall sheen traced to facility oil-water separator bypass.",
  ],
  Vehicle: [
    "Semi-truck saddle tank punctured in collision; fuel reached roadside ditch.",
    "Hydraulic line failure on excavator released fluid to gravel staging area.",
    "Overturned tanker trailer released product to highway shoulder; lanes closed during cleanup.",
    "Fuel tank rupture after vehicle left roadway; contaminated soil excavated.",
  ],
  RailCar: [
    "Rail car fitting leak discovered during yard inspection; drip pans placed.",
    "Locomotive fuel tank seep along mainline; ballast contamination noted.",
  ],
  Pipeline: [
    "Pressure drop triggered line shutdown; product released at valve station.",
    "Third-party excavation strike on distribution line; area evacuated during repair.",
  ],
  Unknown: [
    "Reported sheen of unknown origin; source investigation ongoing.",
    "Mystery sheen observed by ferry crew; drift modeling requested.",
    "Passerby reported petroleum odor and discoloration; no source located.",
  ],
};

// --- helpers ----------------------------------------------------------------
const pad = (n, w) => String(n).padStart(w, "0");
function fmtDate(d) {
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1, 2)}-${pad(d.getUTCDate(), 2)}T${pad(d.getUTCHours(), 2)}:${pad(d.getUTCMinutes(), 2)}:00Z`;
}
function csvField(v) {
  const s = String(v ?? "");
  return /[",\n]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
}
const header = [
  "ReportNumber", "ReportedAt", "OccurredAt", "Description", "Latitude", "Longitude",
  "LocationDescription", "Waterbody", "County", "Medium", "SubstanceName",
  "QuantityGallons", "RecoveredGallons", "SourceType", "ResponsibleParty", "Status",
];

// --- main dataset -----------------------------------------------------------
const rows = [];
const perYear = { 2020: 88, 2021: 95, 2022: 104, 2023: 112, 2024: 121, 2025: 118, 2026: 62 };
const counters = {};

for (const [yearStr, count] of Object.entries(perYear)) {
  const year = Number(yearStr);
  counters[year] = 1000 + Math.floor(rand() * 500);
  for (let i = 0; i < count; i++) {
    const anchor = weighted(anchorPairs);
    const source = weighted(sourcesByType[anchor.type]);
    const medium = weighted(mediumByType[anchor.type]);
    const substance = weighted(substancesBySource[source]);

    counters[year] += 1 + Math.floor(rand() * 6);
    const reportNumber = `ERTS-${year}-${pad(counters[year], 6)}`;

    const maxDay = year === 2026 ? 180 : 364;
    const reported = new Date(Date.UTC(year, 0, 1, 6 + Math.floor(rand() * 14), Math.floor(rand() * 60)));
    reported.setUTCDate(reported.getUTCDate() + Math.floor(rand() * maxDay));

    let occurredAt = "";
    if (rand() < 0.78) {
      const occurred = new Date(reported.getTime() - Math.floor(rand() * 48 * 60) * 60000);
      occurredAt = fmtDate(occurred);
    }

    // Long-tail quantities: mostly small, occasionally large.
    let quantity = "";
    let recovered = "";
    if (rand() < 0.85) {
      const magnitude = rand();
      let gal;
      if (magnitude < 0.55) gal = 0.1 + rand() * 5;
      else if (magnitude < 0.8) gal = 5 + rand() * 45;
      else if (magnitude < 0.95) gal = 50 + rand() * 450;
      else gal = 500 + rand() * 9500;
      quantity = gal.toFixed(1);
      if (rand() < 0.6) recovered = (gal * rand() * 0.9).toFixed(1);
    }

    const hasCoords = rand() < 0.93;
    const lat = (anchor.lat + (rand() - 0.5) * 0.09).toFixed(5);
    const lon = (anchor.lon + (rand() - 0.5) * 0.09).toFixed(5);

    const ageYears = 2026.5 - (year + reported.getUTCMonth() / 12);
    const status =
      ageYears > 1.5
        ? weighted([["Closed", 9], ["Referred To Other Agency", 1]])
        : weighted([["Closed", 5], ["Cleanup In Progress", 2], ["Under Investigation", 2], ["Reported", 1]]);

    const description = pick(descTemplates[source]).replace("{sub_lc}", substance.toLowerCase());
    const party = source === "Unknown" ? (rand() < 0.7 ? "" : "Unknown") : pick(parties);

    rows.push([
      reportNumber, fmtDate(reported), occurredAt, description,
      hasCoords ? lat : "", hasCoords ? lon : "",
      anchor.name, anchor.water, anchor.county, medium, substance,
      quantity, recovered, source.replace("RailCar", "Rail Car"), party, status,
    ]);
  }
}

const mainCsv = [header.join(","), ...rows.map((r) => r.map(csvField).join(","))].join("\n") + "\n";
writeFileSync(join(outDir, "spill_incidents_2020_2026.csv"), mainCsv);
console.log(`spill_incidents_2020_2026.csv: ${rows.length} rows`);

// --- quarantine demo file ---------------------------------------------------
// A small file where most rows are intentionally broken, to demonstrate
// intake validation and quarantine behavior.
const demo = [
  header.join(","),
  // valid
  'ERTS-2026-990001,2026-06-01T14:00:00Z,2026-06-01T09:30:00Z,Sheen near fuel dock reported by harbormaster.,47.60210,-122.33440,Elliott Bay Marina,Elliott Bay,King,Marine Water,Diesel fuel,12.0,6.5,Vessel,Cascadia Marine Services LLC,Cleanup In Progress',
  // swapped lat/lon
  'ERTS-2026-990002,2026-06-02T10:00:00Z,,Overturned tanker on highway shoulder.,-122.90070,47.03790,I-5 near Olympia,,Thurston,Land,Diesel fuel,300,120,Vehicle,Evergreen Freight Lines,Under Investigation',
  // future reported date
  'ERTS-2027-990003,2027-01-15T08:00:00Z,,Terminal transfer line drip.,47.27000,-122.42000,Commencement Bay,Commencement Bay,Pierce,Marine Water,Bunker C (No. 6 fuel oil),40,,Facility,Puget Terminal Operations LLC,Reported',
  // unknown county + bad medium
  'ERTS-2026-990004,2026-05-20T16:45:00Z,,Sheen of unknown origin.,48.12500,-123.44000,Port Angeles Harbor,Strait of Juan de Fuca,Jefferson Davis,Salt Water,Unknown oil sheen,,,Unknown,,Reported',
  // negative quantity
  'ERTS-2026-990005,2026-05-22T11:20:00Z,,Hydraulic line failure on excavator.,46.72000,-122.95000,Centralia staging yard,,Lewis,Land,Hydraulic oil,-25,,Vehicle,North Sound Excavation,Closed',
  // valid
  'ERTS-2026-990006,2026-06-03T09:10:00Z,2026-06-02T22:00:00Z,Bilge discharge sheen at marina.,48.74020,-122.50310,Squalicum Harbor,Bellingham Bay,Whatcom,Marine Water,Oily bilge water,3.5,1.0,Vessel,,Closed',
  // duplicate of 990001
  'ERTS-2026-990001,2026-06-04T12:00:00Z,,Duplicate submission of earlier marina sheen report.,47.60210,-122.33440,Elliott Bay Marina,Elliott Bay,King,Marine Water,Diesel fuel,12.0,6.5,Vessel,Cascadia Marine Services LLC,Cleanup In Progress',
  // missing substance + malformed report number
  'ERTS-26-9907,2026-06-05T13:30:00Z,,Odor complaint near stormwater outfall.,47.98000,-122.22000,Port Gardner,Port Gardner Bay,Snohomish,Marine Water,,,,Unknown,,Reported',
  // occurred after reported
  'ERTS-2026-990008,2026-06-06T08:00:00Z,2026-06-07T08:00:00Z,Fuel overflow during bunkering.,47.56100,-122.62500,Sinclair Inlet,Sinclair Inlet,Kitsap,Marine Water,Diesel fuel,20,5,Vessel,Rainier Towing & Barge Co.,Reported',
  // valid
  'ERTS-2026-990009,2026-06-07T15:25:00Z,,Locomotive fuel seep noted during inspection.,46.14350,-122.93800,BNSF yard Longview,Columbia River,Cowlitz,Land,Diesel fuel,8.0,,Rail Car,Selkirk Rail Services,Under Investigation',
].join("\n") + "\n";
writeFileSync(join(outDir, "quarantine_demo.csv"), demo);
console.log("quarantine_demo.csv: 10 rows (7 intentionally invalid)");
