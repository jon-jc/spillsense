// Leaflet incident map: clustered vector markers colored by substance
// category and sized by spill volume, with theme-matched basemaps.

import { categoryColor, isDark, CATEGORY_ORDER } from "./palette.js";
import { fmtDate, fmtNum, labelize } from "./format.js";
import { config } from "./config.js";

const WA_BOUNDS = [[45.3, -125.5], [49.2, -116.6]];

/**
 * Basemaps, light and dark, from two providers.
 *
 * CARTO's Positron and Dark Matter are the canvases the dashboard was designed
 * on, but since 25 September 2026 CARTO serves its raster tiles only with an
 * API key: an anonymous request gets a tile watermarked "API KEY REQUIRED".
 * They are used when a key is configured (config.js). Without one, or when
 * CARTO refuses the key for the host serving the page (a key can be restricted
 * to named hostnames; any other host gets a 403), the map draws on Esri's
 * key-free light and dark gray canvases, which serve the same purpose: a
 * neutral ground that leaves the incident layer in the foreground.
 */
const BASEMAPS = {
  carto: {
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
    options: { maxZoom: 18 },
    light: "https://basemaps.cartocdn.com/rastertiles/light_all/{z}/{x}/{y}{r}.png?key={key}",
    dark: "https://basemaps.cartocdn.com/rastertiles/dark_all/{z}/{x}/{y}{r}.png?key={key}",
  },
  esri: {
    attribution: 'Tiles &copy; <a href="https://www.esri.com/">Esri</a> &mdash; Esri, HERE, Garmin, &copy; OpenStreetMap contributors, and the GIS user community',
    // The canvas services are drawn down to zoom 16; Leaflet scales those tiles
    // up for the last two levels instead of requesting blanks.
    options: { maxNativeZoom: 16, maxZoom: 18 },
    light: "https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Light_Gray_Base/MapServer/tile/{z}/{y}/{x}",
    dark: "https://server.arcgisonline.com/ArcGIS/rest/services/Canvas/World_Dark_Gray_Base/MapServer/tile/{z}/{y}/{x}",
  },
};

/** Failed tiles from the keyed CARTO layer before the map gives up on it. */
const CARTO_FAILURES_TOLERATED = 3;

let map;
let tileLayer;
let cluster;
let legendControl;
let onSelect = () => {};
let cartoRefused = false;

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

function basemapProvider() {
  return config.cartoBasemapKey && !cartoRefused ? "carto" : "esri";
}

export function applyBasemap() {
  const provider = basemapProvider();
  const basemap = BASEMAPS[provider];
  if (tileLayer) map.removeLayer(tileLayer);
  tileLayer = L.tileLayer(basemap[isDark() ? "dark" : "light"], {
    attribution: basemap.attribution,
    key: config.cartoBasemapKey,
    ...basemap.options,
  });
  if (provider === "carto") watchCartoTiles(tileLayer);
  tileLayer.addTo(map);
}

/**
 * A key CARTO will not honour for this host answers every tile with a 403.
 * Rather than leave the map blank, fall back to the key-free canvas for the
 * rest of the session once a few tiles have failed (a single failed tile may
 * only be a dropped connection).
 */
function watchCartoTiles(layer) {
  let failures = 0;
  layer.on("tileerror", () => {
    if (cartoRefused || ++failures < CARTO_FAILURES_TOLERATED) return;
    cartoRefused = true;
    console.warn(
      "CARTO basemap tiles are not loading; falling back to Esri's canvas basemap. " +
      "Check the basemap key in js/config.js and the hosts it is allowed on.",
    );
    if (tileLayer === layer) applyBasemap();
  });
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
