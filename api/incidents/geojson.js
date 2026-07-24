import { incidents } from "../_lib/store.js";
import {
  parseQuery, applyFilters, applySort, toSummaryDto,
  searchParamsOf, sendJson, sendValidationProblem,
} from "../_lib/query.js";

const GEOJSON_LIMIT = 5000;

export default function handler(req, res) {
  const parsed = parseQuery(searchParamsOf(req));
  if (parsed.errors.length) return sendValidationProblem(res, parsed.errors);

  const features = applySort(applyFilters(incidents, parsed), "-reportedAt")
    .filter((i) => i.latitude != null && i.longitude != null)
    .slice(0, GEOJSON_LIMIT)
    .map((i) => ({
      type: "Feature",
      geometry: { type: "Point", coordinates: [i.longitude, i.latitude] },
      properties: toSummaryDto(i),
    }));

  return sendJson(res, 200, { type: "FeatureCollection", features });
}
