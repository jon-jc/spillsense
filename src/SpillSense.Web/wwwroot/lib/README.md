# Vendored frontend libraries

Committed (rather than CDN-loaded) so the dashboard works offline and builds
are reproducible without a Node toolchain.

| Library | Version | Source |
|---|---|---|
| Leaflet | 1.9.4 | https://unpkg.com/leaflet@1.9.4/dist/ |
| Leaflet.markercluster | 1.5.3 | https://unpkg.com/leaflet.markercluster@1.5.3/dist/ |
| Chart.js | 4.4.9 | https://unpkg.com/chart.js@4.4.9/dist/chart.umd.js |
| Scalar API Reference | 3.0.0 | https://cdn.jsdelivr.net/npm/@scalar/api-reference/dist/browser/standalone.js |

Base-map tiles are fetched from openstreetmap.org at runtime (attribution shown
on the map). The dashboard renders incident markers with vector `circleMarker`s,
so Leaflet's raster marker images are not needed.
