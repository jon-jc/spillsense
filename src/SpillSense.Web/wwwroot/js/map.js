// Leaflet incident map: clustered vector markers colored by substance
// category and sized by spill volume, with theme-matched basemaps.

import { categoryColor, isDark, CATEGORY_ORDER } from "./palette.js";
import { fmtDate, fmtNum, labelize } from "./format.js";

const WA_BOUNDS = [[45.3, -125.5], [49.2, -116.6]];

const TILES = {
  light: {
    url: "https://{s}.basemaps.cartocdn.com/light_all/{z}/{x}/{y}{r}.png",
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
  },
  dark: {
    url: "https://{s}.basemaps.cartocdn.com/dark_all/{z}/{x}/{y}{r}.png",
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> &copy; <a href="https://carto.com/attributions">CARTO</a>',
  },
};

let map;
let tileLayer;
let cluster;
let legendControl;
let onSelect = () => {};

export function initMap(selectHandler) {
  onSelect = selectHandler;
  map = L.map("map", { zoomSnap: 0.5, worldCopyJump: false });
  map.fitBounds(WA_BOUNDS, { padding: [10, 10] });
  map.setMaxBounds([[42.5, -130], [52, -112]]);

  applyBasemap();

  cluster = L.markerClusterGroup({
    maxClusterRadius: 46,
    spiderfyOnMaxZoom: true,
    showCoverageOnHover: false,
    disableClusteringAtZoom: 12,
  });
  map.addLayer(cluster);
  return map;
}

export function applyBasemap() {
  const theme = isDark() ? TILES.dark : TILES.light;
  if (tileLayer) map.removeLayer(tileLayer);
  tileLayer = L.tileLayer(theme.url, { attribution: theme.attribution, maxZoom: 18 });
  tileLayer.addTo(map);
}

/** Radius scales with log of gallons so the long tail stays readable. */
function markerRadius(gallons) {
  if (!gallons || gallons <= 0) return 5;
  return Math.min(18, 5 + Math.log10(gallons + 1) * 3.2);
}

export function renderIncidents(featureCollection) {
  cluster.clearLayers();
  const markers = featureCollection.features.map((feature) => {
    const p = feature.properties;
    const [lon, lat] = feature.geometry.coordinates;
    const marker = L.circleMarker([lat, lon], {
      radius: markerRadius(p.quantityGallons),
      color: "rgba(0,0,0,0.35)",
      weight: 1,
      fillColor: categoryColor(p.substanceCategory),
      fillOpacity: 0.82,
    });
    marker.bindPopup(popupHtml(p), { maxWidth: 280 });
    marker.on("popupopen", (e) => {
      e.popup.getElement()
        ?.querySelector(".popup-link")
        ?.addEventListener("click", () => onSelect(p.reportNumber));
    });
    return marker;
  });
  cluster.addLayers(markers);
  document.getElementById("map-count").textContent =
    `${featureCollection.features.length.toLocaleString()} mapped incidents`;
}

function popupHtml(p) {
  const qty = p.quantityGallons != null ? `${fmtNum(p.quantityGallons)} gal` : "unquantified";
  return `
    <div class="popup-title">${labelize(p.substanceCategory)} — ${qty}</div>
    <div class="popup-meta">${fmtDate(p.reportedAtUtc)} · ${p.county ?? "county n/a"} · ${labelize(p.sourceType)}</div>
    <span class="popup-link" role="button" tabindex="0">View ${p.reportNumber}</span>`;
}

/**
 * The legend is a real Leaflet control (not an overlaid div): controls live in
 * the map's own control layer, so the legend stays above tile/marker panes,
 * anchors to the map corner at any size, and repositions with the map chrome.
 */
export function renderLegend(buckets) {
  if (!legendControl) {
    legendControl = L.control({ position: "bottomleft" });
    legendControl.onAdd = () => {
      const div = L.DomUtil.create("div", "map-legend");
      div.setAttribute("aria-hidden", "true");
      // Keep map drag/scroll gestures from firing through the legend.
      L.DomEvent.disableClickPropagation(div);
      L.DomEvent.disableScrollPropagation(div);
      return div;
    };
    legendControl.addTo(map);
  }

  const present = new Set(buckets.filter((b) => b.count > 0).map((b) => b.key));
  const rows = CATEGORY_ORDER.filter((c) => present.has(c)).slice(0, 8);
  legendControl.getContainer().innerHTML = rows
    .map((c) => `<div class="legend-row"><span class="legend-dot" style="background:${categoryColor(c)}"></span>${labelize(c)}</div>`)
    .join("");
}

export function flyToIncident(lat, lon) {
  if (lat == null || lon == null) return;
  map.flyTo([lat, lon], Math.max(map.getZoom(), 11), { duration: 0.8 });
}

export function refreshMapTheme(lastFeatures) {
  applyBasemap();
  if (lastFeatures) renderIncidents(lastFeatures);
}
