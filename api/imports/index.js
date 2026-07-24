import { importRuns } from "../_lib/store.js";
import { sendJson } from "../_lib/query.js";

export default function handler(req, res) {
  return sendJson(res, 200, importRuns);
}
