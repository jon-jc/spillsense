import { importRuns, quarantineByRun } from "../../_lib/store.js";
import { sendJson } from "../../_lib/query.js";

export default function handler(req, res) {
  const id = req.query?.id
    ?? new URL(req.url, "http://localhost").pathname.split("/").at(-2);

  if (!importRuns.some((r) => String(r.id) === String(id))) {
    res.statusCode = 404;
    return res.end();
  }
  return sendJson(res, 200, quarantineByRun[id] ?? []);
}
