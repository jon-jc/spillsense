import { counties, incidents } from "../_lib/store.js";
import {
  parseQuery, applyFilters, countyStats,
  searchParamsOf, sendJson, sendValidationProblem,
} from "../_lib/query.js";

export default function handler(req, res) {
  const parsed = parseQuery(searchParamsOf(req));
  if (parsed.errors.length) return sendValidationProblem(res, parsed.errors);
  return sendJson(res, 200, countyStats(applyFilters(incidents, parsed), counties));
}
