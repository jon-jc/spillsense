// Dashboard composition: filters drive every panel; the URL carries state.

import { api } from "./api.js";
import { applyStoredTheme, toggleTheme } from "./palette.js";
import { countUp, debounce, fmtCompact, fmtInt, labelize } from "./format.js";
import { currentFilters, setFilter, clearFilters, onFiltersChanged } from "./state.js";
import { initMap, renderIncidents, renderLegend, refreshMapTheme } from "./map.js";
import { renderTrend, renderCategories, renderMedium } from "./charts.js";
import { initTable, refreshTable, resetTablePage } from "./table.js";
import { initDrawer, openDrawer } from "./drawer.js";
import { loadImports, initImportUpload } from "./imports.js";
import { toast } from "./toast.js";

applyStoredTheme();

const FIELD_IDS = {
  search: "f-search", county: "f-county", region: "f-region", medium: "f-medium",
  category: "f-category", source: "f-source", status: "f-status", from: "f-from", to: "f-to",
};
const CHIP_LABELS = {
  search: "Search", county: "County", region: "Region", medium: "Medium",
  category: "Substance", source: "Source", status: "Status", from: "From", to: "To",
};

let lastGeoJson = null;

async function boot() {
  initDrawer();
  initMap(openDrawer);
  initTable(currentFilters, openDrawer);
  wireFilters();
  syncFieldsFromUrl();

  document.getElementById("theme-toggle").addEventListener("click", () => {
    toggleTheme();
    refreshMapTheme(lastGeoJson);
    refreshAnalytics(currentFilters());
  });

  populateCounties();
  hideUnavailableNav();
  // A fresh import changes the underlying data, so re-run every panel.
  initImportUpload(() => refreshAll(currentFilters()));
  onFiltersChanged(handleFilterChange);
  await refreshAll(currentFilters());
  loadImports();
}

function wireFilters() {
  for (const [key, id] of Object.entries(FIELD_IDS)) {
    const el = document.getElementById(id);
    const handler = key === "search"
      ? debounce(() => setFilter(key, el.value.trim()), 300)
      : () => setFilter(key, el.value);
    el.addEventListener(key === "search" ? "input" : "change", handler);
  }
  document.getElementById("f-clear").addEventListener("click", () => {
    clearFilters();
    syncFieldsFromUrl();
  });
}

function syncFieldsFromUrl() {
  const filters = currentFilters();
  for (const [key, id] of Object.entries(FIELD_IDS)) {
    document.getElementById(id).value = filters[key] ?? "";
  }
  renderChips(filters);
}

/** The interactive API reference only exists on the ASP.NET host; hide the
    link on hosts (e.g. the serverless deployment) that don't serve it. */
async function hideUnavailableNav() {
  try {
    const probe = await fetch("openapi/v1.json");
    if (!probe.ok) throw new Error();
  } catch {
    for (const el of document.querySelectorAll("[data-live-only]")) el.hidden = true;
  }
}

async function populateCounties() {
  try {
    const counties = await api.counties();
    const select = document.getElementById("f-county");
    for (const c of counties) {
      const option = document.createElement("option");
      option.value = c.name;
      option.textContent = c.name;
      select.append(option);
    }
    select.value = currentFilters().county ?? "";
  } catch {
    /* county filter stays usable as free text elsewhere; not fatal */
  }
}

const handleFilterChange = debounce((filters) => {
  renderChips(filters);
  resetTablePage();
  refreshAll(filters);
}, 120);

function syncExportLink(filters) {
  const params = new URLSearchParams(filters);
  const qs = params.toString();
  document.getElementById("export-csv").href = `api/incidents/export${qs ? `?${qs}` : ""}`;
}

function renderChips(filters) {
  syncExportLink(filters);
  const holder = document.getElementById("filter-chips");
  const entries = Object.entries(filters);
  document.getElementById("f-clear").hidden = entries.length === 0;
  holder.innerHTML = entries.map(([key, value]) =>
    `<button class="chip" type="button" data-key="${key}" aria-label="Remove ${CHIP_LABELS[key]} filter">
       <span><b>${CHIP_LABELS[key]}:</b> ${escapeHtml(labelize(value))}</span><span class="x">✕</span>
     </button>`).join("");
  for (const chip of holder.querySelectorAll(".chip")) {
    chip.addEventListener("click", () => {
      setFilter(chip.dataset.key, "");
      syncFieldsFromUrl();
    });
  }
}

async function refreshAll(filters) {
  await Promise.all([refreshAnalytics(filters), refreshMap(filters), refreshTable()]);
}

async function refreshMap(filters) {
  try {
    lastGeoJson = await api.geojson(filters);
    renderIncidents(lastGeoJson);
  } catch (err) {
    toast(`Map data failed to load: ${err.message}`);
  }
}

async function refreshAnalytics(filters) {
  let summary, trend;
  try {
    [summary, trend] = await Promise.all([api.summary(filters), api.trend(filters)]);
  } catch (err) {
    toast(`Analytics failed to load: ${err.message}`);
    return;
  }

  countUp(document.getElementById("kpi-count"), summary.totalIncidents, fmtInt);
  countUp(document.getElementById("kpi-gallons"), summary.totalGallons, fmtCompact);
  countUp(document.getElementById("kpi-recovered"), summary.recoveredGallons, fmtCompact);
  countUp(document.getElementById("kpi-mapped"), summary.withCoordinates, fmtInt);

  const recoveredPct = summary.totalGallons > 0
    ? `${Math.round((summary.recoveredGallons / summary.totalGallons) * 100)}% of spilled volume`
    : "";
  document.getElementById("kpi-recovered-note").textContent = recoveredPct;
  const mappedPct = summary.totalIncidents > 0
    ? `${Math.round((summary.withCoordinates / summary.totalIncidents) * 100)}% georeferenced`
    : "";
  document.getElementById("kpi-mapped-note").textContent = mappedPct;
  document.getElementById("kpi-count-note").textContent =
    Object.keys(currentFilters()).length ? "matching current filters" : "all recorded incidents";

  renderTrend(trend);
  renderCategories(summary.byCategory, summary.totalIncidents);
  renderMedium(summary.byMedium);
  renderLegend(summary.byCategory);
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

boot();
