// Deployment settings for the dashboard. There is no build step: edit this
// file and redeploy. Everything here is public, client-side configuration.

export const config = {
  /**
   * CARTO basemap key. CARTO's raster basemaps (Positron and Dark Matter, the
   * canvases the dashboard was designed on) stopped serving tiles without a key
   * on 25 September 2026; an anonymous request gets a tile watermarked
   * "API KEY REQUIRED". Request a free key at https://carto.com/basemaps/apikey/
   * (no account needed; 5 M tile requests a month for non-commercial use) and
   * paste it here. The key rides on every tile URL, so it is public by design;
   * restrict it to your hostnames in CARTO's basemap dashboard.
   *
   * Left empty, the map draws on Esri's key-free light and dark gray canvases
   * instead (see map.js), so the dashboard works with no setup.
   */
  cartoBasemapKey: "",
};
