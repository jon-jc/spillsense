import { incidents } from "../_lib/store.js";
import { sendJson } from "../_lib/query.js";

export default function handler(req, res) {
  const reportNumber = decodeURIComponent(
    req.query?.reportNumber
    ?? new URL(req.url, "http://localhost").pathname.split("/").pop());

  const incident = incidents.find((i) => i.reportNumber === reportNumber);
  if (!incident) {
    res.statusCode = 404;
    return res.end();
  }
  return sendJson(res, 200, incident);
}
