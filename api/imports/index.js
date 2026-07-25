import { importRuns } from "../_lib/store.js";
import { sendJson } from "../_lib/query.js";

export default function handler(req, res) {
  // This deployment is a read-only replica of the ASP.NET system of record;
  // intake (POST) only exists where the database and ETL pipeline live.
  if (req.method && req.method !== "GET" && req.method !== "HEAD") {
    res.statusCode = 405;
    res.setHeader("Allow", "GET");
    res.setHeader("Content-Type", "application/problem+json; charset=utf-8");
    return res.end(JSON.stringify({
      title: "This deployment is a read-only replica.",
      detail: "CSV intake runs on the ASP.NET Core host, which owns the database and ETL pipeline.",
      status: 405,
    }));
  }
  return sendJson(res, 200, importRuns);
}
