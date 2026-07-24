import { incidents } from "../_lib/store.js";
import {
  parseQuery, applyFilters, applySort,
  searchParamsOf, sendValidationProblem,
} from "../_lib/query.js";

const COLUMNS = [
  ["ReportNumber", (i) => i.reportNumber],
  ["ReportedAtUtc", (i) => i.reportedAtUtc],
  ["OccurredAtUtc", (i) => i.occurredAtUtc],
  ["County", (i) => i.county],
  ["EcologyRegion", (i) => i.ecologyRegion],
  ["Medium", (i) => i.medium],
  ["SubstanceName", (i) => i.substanceName],
  ["SubstanceCategory", (i) => i.substanceCategory],
  ["QuantityGallons", (i) => i.quantityGallons],
  ["RecoveredGallons", (i) => i.recoveredGallons],
  ["SourceType", (i) => i.sourceType],
  ["Status", (i) => i.status],
  ["Latitude", (i) => i.latitude],
  ["Longitude", (i) => i.longitude],
  ["LocationDescription", (i) => i.locationDescription],
  ["WaterbodyName", (i) => i.waterbodyName],
  ["ResponsibleParty", (i) => i.responsibleParty],
  ["Description", (i) => i.description],
];

function csvField(value) {
  const s = value == null ? "" : String(value);
  return /[",\r\n]/.test(s) ? `"${s.replaceAll('"', '""')}"` : s;
}

export default function handler(req, res) {
  const parsed = parseQuery(searchParamsOf(req));
  if (parsed.errors.length) return sendValidationProblem(res, parsed.errors);

  const rows = applySort(applyFilters(incidents, parsed), parsed.sort);
  const lines = [
    COLUMNS.map(([name]) => name).join(","),
    ...rows.map((i) => COLUMNS.map(([, pick]) => csvField(pick(i))).join(",")),
  ];

  const stamp = new Date().toISOString().slice(0, 10).replaceAll("-", "");
  res.statusCode = 200;
  res.setHeader("Content-Type", "text/csv; charset=utf-8");
  res.setHeader("Content-Disposition", `attachment; filename=spillsense-incidents-${stamp}.csv`);
  res.end(`${lines.join("\r\n")}\r\n`);
}
