// Slide-in detail drawer for a single incident.

import { api } from "./api.js";
import { fmtDateTime, fmtNum, labelize } from "./format.js";
import { flyToIncident } from "./map.js";

const drawer = () => document.getElementById("drawer");
const scrim = () => document.getElementById("drawer-scrim");
let lastFocus = null;

export function initDrawer() {
  document.getElementById("drawer-close").addEventListener("click", closeDrawer);
  scrim().addEventListener("click", closeDrawer);
  document.addEventListener("keydown", (e) => {
    if (e.key === "Escape" && !drawer().hidden) closeDrawer();
  });
}

export async function openDrawer(reportNumber) {
  lastFocus = document.activeElement;
  const el = drawer();
  const sc = scrim();
  el.hidden = false;
  sc.hidden = false;
  setTimeout(() => { el.classList.add("open"); sc.classList.add("open"); }, 10);

  document.getElementById("drawer-title").textContent = reportNumber;
  document.getElementById("drawer-sub").textContent = "";
  document.getElementById("drawer-badge").textContent = "";
  document.getElementById("drawer-body").innerHTML =
    `<p><span class="skeleton">Loading incident details…</span></p>`;

  let d;
  try {
    d = await api.incident(reportNumber);
  } catch (err) {
    document.getElementById("drawer-body").innerHTML =
      `<p class="empty">Could not load ${reportNumber}: ${err.message}</p>`;
    return;
  }

  const badge = document.getElementById("drawer-badge");
  badge.textContent = labelize(d.status);
  badge.className = `pill pill-${d.status}`;
  document.getElementById("drawer-sub").textContent =
    `Reported ${fmtDateTime(d.reportedAtUtc)}`;

  document.getElementById("drawer-body").innerHTML = bodyHtml(d);
  document.getElementById("drawer-close").focus();

  if (d.latitude != null) flyToIncident(d.latitude, d.longitude);
}

export function closeDrawer() {
  const el = drawer();
  const sc = scrim();
  el.classList.remove("open");
  sc.classList.remove("open");
  setTimeout(() => { el.hidden = true; sc.hidden = true; }, 220);
  lastFocus?.focus?.();
}

function bodyHtml(d) {
  const coords = d.latitude != null
    ? `${d.latitude.toFixed(5)}, ${d.longitude.toFixed(5)}`
    : "not georeferenced";
  const recovered = d.recoveredGallons != null && d.quantityGallons
    ? `${fmtNum(d.recoveredGallons)} gal (${Math.round((d.recoveredGallons / d.quantityGallons) * 100)}%)`
    : d.recoveredGallons != null ? `${fmtNum(d.recoveredGallons)} gal` : "—";

  return `
    <div class="drawer-desc">${escapeHtml(d.description)}</div>
    <dl class="detail-grid">
      ${detail("Substance", `${escapeHtml(d.substanceName)}`)}
      ${detail("Category", labelize(d.substanceCategory))}
      ${detail("Quantity", d.quantityGallons != null ? `${fmtNum(d.quantityGallons)} gal` : "unquantified")}
      ${detail("Recovered", recovered)}
      ${detail("Medium affected", labelize(d.medium))}
      ${detail("Source type", labelize(d.sourceType))}
      ${detail("County", d.county ? `${d.county} County` : "—")}
      ${detail("Ecology region", d.ecologyRegion ? `${d.ecologyRegion} Region` : "—")}
      ${detail("Waterbody", d.waterbodyName ?? "—", true)}
      ${detail("Location", d.locationDescription ?? "—", true)}
      ${detail("Coordinates (WGS 84)", coords, true, true)}
      ${detail("Responsible party", d.responsibleParty ?? "Unknown", true)}
      ${detail("Occurred", d.occurredAtUtc ? fmtDateTime(d.occurredAtUtc) : "—", true)}
      ${detail("Record updated", fmtDateTime(d.updatedAtUtc), true)}
    </dl>`;
}

function detail(label, value, wide = false, mono = false) {
  return `<div class="detail${wide ? " wide" : ""}">
    <dt>${label}</dt><dd${mono ? ' class="mono"' : ""}>${value}</dd>
  </div>`;
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
