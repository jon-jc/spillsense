// Thin client for the SpillSense API. The same contract is served by the
// ASP.NET Core host and by the Vercel serverless functions, so this module
// needs no environment detection.

function buildQuery(filters, extra = {}) {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries({ ...filters, ...extra })) {
    if (value !== undefined && value !== null && value !== "") params.set(key, value);
  }
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

async function getJson(url) {
  const response = await fetch(url, { headers: { Accept: "application/json" } });
  if (!response.ok) {
    let detail = `${response.status} ${response.statusText}`;
    try {
      const body = await response.json();
      const errors = body?.errors?.query;
      if (Array.isArray(errors) && errors.length) detail = errors.join(" ");
    } catch { /* non-JSON error body */ }
    throw new Error(detail);
  }
  return response.json();
}

export const api = {
  incidents: (filters, { page, pageSize, sort } = {}) =>
    getJson(`api/incidents${buildQuery(filters, { page, pageSize, sort })}`),
  incident: (reportNumber) => getJson(`api/incidents/${encodeURIComponent(reportNumber)}`),
  geojson: (filters) => getJson(`api/incidents/geojson${buildQuery(filters)}`),
  summary: (filters) => getJson(`api/stats/summary${buildQuery(filters)}`),
  trend: (filters) => getJson(`api/stats/trend${buildQuery(filters)}`),
  countyStats: (filters) => getJson(`api/stats/counties${buildQuery(filters)}`),
  counties: () => getJson("api/counties"),
  imports: () => getJson("api/imports"),
  quarantine: (runId) => getJson(`api/imports/${runId}/quarantine`),
};
