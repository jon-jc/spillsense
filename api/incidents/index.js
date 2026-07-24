import { incidents } from "../_lib/store.js";
import {
  parseQuery, applyFilters, applySort, toSummaryDto,
  searchParamsOf, sendJson, sendValidationProblem,
} from "../_lib/query.js";

export default function handler(req, res) {
  const parsed = parseQuery(searchParamsOf(req));
  if (parsed.errors.length) return sendValidationProblem(res, parsed.errors);

  const filtered = applyFilters(incidents, parsed);
  const pageItems = applySort(filtered, parsed.sort)
    .slice((parsed.page - 1) * parsed.pageSize, parsed.page * parsed.pageSize)
    .map(toSummaryDto);

  return sendJson(res, 200, {
    total: filtered.length,
    page: parsed.page,
    pageSize: parsed.pageSize,
    items: pageItems,
  });
}
