// Query parsing, filtering, and aggregation for the serverless deployment.
// Mirrors the ASP.NET Core implementation's semantics exactly: same parameter
// names, same validation messages, same response shapes — one API contract,
// two hosts.

const MEDIA = ["Unknown", "MarineWater", "FreshWater", "Groundwater", "Land", "Air"];
const CATEGORIES = ["Unknown", "CrudeOil", "DieselFuel", "Gasoline", "JetFuel", "HeavyFuelOil",
  "LubeOrHydraulicOil", "BilgeOrOilyWater", "Chemical", "Sewage", "Other"];
const SOURCES = ["Unknown", "Vessel", "Facility", "Pipeline", "Vehicle", "RailCar", "Aircraft", "Other"];
const STATUSES = ["Reported", "UnderInvestigation", "CleanupInProgress", "Closed", "ReferredToOtherAgency"];
const REGIONS = ["Unknown", "Northwest", "Southwest", "Central", "Eastern"];

function parseEnum(value, name, allowed, errors) {
  if (!value) return null;
  const normalized = value.replaceAll(" ", "").toLowerCase();
  const match = allowed.find((a) => a.toLowerCase() === normalized);
  if (match) return match;
  errors.push(`'${name}' value '${value}' is not recognized (expected one of: ${allowed.join(", ")}).`);
  return null;
}

function parseDate(value, name, errors) {
  if (!value) return null;
  const ms = Date.parse(value);
  if (Number.isNaN(ms)) {
    errors.push(`'${name}' value '${value}' is not a recognizable date.`);
    return null;
  }
  return ms;
}

export function parseQuery(params) {
  const errors = [];
  const q = (key) => params.get(key) || null;

  const parsed = {
    county: q("county")?.trim() || null,
    region: parseEnum(q("region"), "region", REGIONS, errors),
    medium: parseEnum(q("medium"), "medium", MEDIA, errors),
    category: parseEnum(q("category"), "category", CATEGORIES, errors),
    source: parseEnum(q("source"), "source", SOURCES, errors),
    status: parseEnum(q("status"), "status", STATUSES, errors),
    from: parseDate(q("from"), "from", errors),
    to: parseDate(q("to"), "to", errors),
    search: q("search")?.trim() || null,
    bbox: null,
    minGallons: null,
    hasCoordinates: null,
    page: 1,
    pageSize: 25,
    sort: null,
    errors,
  };

  if (parsed.from != null && parsed.to != null && parsed.from > parsed.to) {
    errors.push("'from' must not be after 'to'.");
  }

  const bbox = q("bbox");
  if (bbox) {
    const parts = bbox.split(",").map(Number);
    if (parts.length === 4 && parts.every(Number.isFinite) && parts[0] <= parts[2] && parts[1] <= parts[3]) {
      parsed.bbox = { minLon: parts[0], minLat: parts[1], maxLon: parts[2], maxLat: parts[3] };
    } else {
      errors.push("'bbox' must be 'minLon,minLat,maxLon,maxLat' with min <= max.");
    }
  }

  const minGallons = q("minGallons");
  if (minGallons != null) {
    const value = Number(minGallons);
    if (Number.isFinite(value)) parsed.minGallons = value;
    else errors.push(`'minGallons' value '${minGallons}' is not numeric.`);
  }

  const hasCoordinates = q("hasCoordinates");
  if (hasCoordinates != null) parsed.hasCoordinates = hasCoordinates === "true";

  const page = q("page");
  if (page != null) {
    parsed.page = Number(page);
    if (!Number.isInteger(parsed.page) || parsed.page < 1) errors.push("'page' must be >= 1.");
  }
  const pageSize = q("pageSize");
  if (pageSize != null) {
    parsed.pageSize = Number(pageSize);
    if (!Number.isInteger(parsed.pageSize) || parsed.pageSize < 1 || parsed.pageSize > 200) {
      errors.push("'pageSize' must be between 1 and 200.");
    }
  }

  const sort = q("sort");
  if (sort != null && !["reportedAt", "-reportedAt", "quantity", "-quantity"].includes(sort)) {
    errors.push("'sort' must be 'reportedAt' or 'quantity', optionally prefixed with '-'.");
  } else {
    parsed.sort = sort;
  }

  return parsed;
}

export function applyFilters(incidents, f) {
  const search = f.search?.toLowerCase();
  return incidents.filter((i) => {
    if (f.county && i.county !== f.county) return false;
    if (f.region && i.ecologyRegion !== f.region) return false;
    if (f.medium && i.medium !== f.medium) return false;
    if (f.category && i.substanceCategory !== f.category) return false;
    if (f.source && i.sourceType !== f.source) return false;
    if (f.status && i.status !== f.status) return false;
    if (f.from != null && Date.parse(i.reportedAtUtc) < f.from) return false;
    if (f.to != null && Date.parse(i.reportedAtUtc) >= f.to) return false;
    if (f.minGallons != null && !(i.quantityGallons >= f.minGallons)) return false;
    if (f.hasCoordinates === true && (i.latitude == null || i.longitude == null)) return false;
    if (f.hasCoordinates === false && i.latitude != null && i.longitude != null) return false;
    if (f.bbox) {
      if (i.latitude == null || i.longitude == null) return false;
      const { minLon, minLat, maxLon, maxLat } = f.bbox;
      if (i.longitude < minLon || i.longitude > maxLon || i.latitude < minLat || i.latitude > maxLat) return false;
    }
    if (search) {
      const haystack = [i.substanceName, i.description, i.responsibleParty,
        i.locationDescription, i.waterbodyName, i.reportNumber]
        .filter(Boolean).join("\n").toLowerCase();
      if (!haystack.includes(search)) return false;
    }
    return true;
  });
}

export function applySort(incidents, sort) {
  const byDate = (a, b) => Date.parse(a.reportedAtUtc) - Date.parse(b.reportedAtUtc);
  // Match SQL NULL ordering: nulls first ascending, last descending.
  const byQty = (a, b) => {
    const qa = a.quantityGallons, qb = b.quantityGallons;
    if (qa == null && qb == null) return 0;
    if (qa == null) return -1;
    if (qb == null) return 1;
    return qa - qb;
  };
  const sorted = [...incidents];
  switch (sort) {
    case "reportedAt": return sorted.sort(byDate);
    case "quantity": return sorted.sort(byQty);
    case "-quantity": return sorted.sort((a, b) => byQty(b, a));
    default: return sorted.sort((a, b) => byDate(b, a));
  }
}

const round1 = (v) => Math.round(v * 10) / 10;

export function toSummaryDto(i) {
  return {
    id: i.id,
    reportNumber: i.reportNumber,
    reportedAtUtc: i.reportedAtUtc,
    county: i.county,
    medium: i.medium,
    substanceName: i.substanceName,
    substanceCategory: i.substanceCategory,
    quantityGallons: i.quantityGallons,
    sourceType: i.sourceType,
    status: i.status,
    latitude: i.latitude,
    longitude: i.longitude,
  };
}

function buckets(rows, keyOf) {
  const map = new Map();
  for (const row of rows) {
    const key = keyOf(row);
    const bucket = map.get(key) ?? { key, count: 0, gallons: 0 };
    bucket.count += 1;
    bucket.gallons += row.quantityGallons ?? 0;
    map.set(key, bucket);
  }
  return [...map.values()]
    .map((b) => ({ ...b, gallons: round1(b.gallons) }))
    .sort((a, b) => b.count - a.count);
}

export function summarize(rows) {
  return {
    totalIncidents: rows.length,
    totalGallons: round1(rows.reduce((s, i) => s + (i.quantityGallons ?? 0), 0)),
    recoveredGallons: round1(rows.reduce((s, i) => s + (i.recoveredGallons ?? 0), 0)),
    withCoordinates: rows.filter((i) => i.latitude != null && i.longitude != null).length,
    byMedium: buckets(rows, (i) => i.medium),
    byCategory: buckets(rows, (i) => i.substanceCategory),
    bySource: buckets(rows, (i) => i.sourceType),
    byStatus: buckets(rows, (i) => i.status),
  };
}

export function trend(rows) {
  const map = new Map();
  for (const i of rows) {
    const month = i.reportedAtUtc.slice(0, 7);
    const point = map.get(month) ?? { month, count: 0, gallons: 0 };
    point.count += 1;
    point.gallons += i.quantityGallons ?? 0;
    map.set(month, point);
  }
  return [...map.values()]
    .map((p) => ({ ...p, gallons: round1(p.gallons) }))
    .sort((a, b) => a.month.localeCompare(b.month));
}

export function countyStats(rows, countyList) {
  const meta = new Map(countyList.map((c) => [c.name, c]));
  return buckets(rows.filter((i) => i.county), (i) => i.county)
    .map((b) => ({
      county: b.key,
      region: meta.get(b.key)?.region ?? "Unknown",
      fipsCode: meta.get(b.key)?.fipsCode ?? "",
      count: b.count,
      gallons: b.gallons,
    }));
}

/* ------------------------- HTTP response helpers ------------------------- */

export function searchParamsOf(req) {
  return new URL(req.url, "http://localhost").searchParams;
}

export function sendJson(res, status, body) {
  res.statusCode = status;
  res.setHeader("Content-Type", "application/json; charset=utf-8");
  res.setHeader("Cache-Control", "public, max-age=60, s-maxage=300");
  res.end(JSON.stringify(body));
}

export function sendValidationProblem(res, errors) {
  res.statusCode = 400;
  res.setHeader("Content-Type", "application/problem+json; charset=utf-8");
  res.end(JSON.stringify({
    type: "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    title: "One or more validation errors occurred.",
    status: 400,
    errors: { query: errors },
  }));
}
