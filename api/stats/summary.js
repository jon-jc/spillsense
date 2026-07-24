import { incidents } from "../_lib/store.js";
import {
  parseQuery, applyFilters, summarize,
  searchParamsOf, sendJson, sendValidationProblem,
} from "../_lib/query.js";

export default function handler(req, res) {
  const parsed = parseQuery(searchParamsOf(req));
  if (parsed.errors.length) return sendValidationProblem(res, parsed.errors);
  return sendJson(res, 200, summarize(applyFilters(incidents, parsed)));
}
