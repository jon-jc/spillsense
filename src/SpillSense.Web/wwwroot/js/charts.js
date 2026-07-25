// Chart.js views. One hue per job: the trend and medium charts encode
// magnitude (single accent hue); the donut encodes identity (fixed
// per-category hues shared with the map legend).

import { tones, categoryColor } from "./palette.js";
import { fmtInt, fmtNum, labelize } from "./format.js";

let trendChart;
let categoryChart;
let mediumChart;

function baseOptions() {
  const t = tones();
  Chart.defaults.font.family = getComputedStyle(document.body).fontFamily;
  Chart.defaults.color = t.ink3;
  return {
    responsive: true,
    maintainAspectRatio: false,
    animation: { duration: matchMedia("(prefers-reduced-motion: reduce)").matches ? 0 : 400 },
    plugins: {
      legend: { display: false },
      tooltip: {
        backgroundColor: t.surface,
        titleColor: t.ink2,
        bodyColor: t.ink2,
        borderColor: t.grid,
        borderWidth: 1,
        padding: 10,
        displayColors: false,
      },
    },
  };
}

export function renderTrend(points) {
  const t = tones();
  const ctx = document.getElementById("chart-trend");
  trendChart?.destroy();

  const labels = points.map((p) => p.month);
  trendChart = new Chart(ctx, {
    type: "line",
    data: {
      labels,
      datasets: [{
        label: "Incidents",
        data: points.map((p) => p.count),
        borderColor: t.s1,
        borderWidth: 2,
        pointRadius: 0,
        pointHoverRadius: 4,
        pointHoverBackgroundColor: t.s1,
        tension: 0.3,
        fill: true,
        backgroundColor: (context) => {
          const { chartArea, ctx: g } = context.chart;
          if (!chartArea) return "transparent";
          const gradient = g.createLinearGradient(0, chartArea.top, 0, chartArea.bottom);
          gradient.addColorStop(0, `${t.s1}33`);
          gradient.addColorStop(1, `${t.s1}00`);
          return gradient;
        },
      }],
    },
    options: {
      ...baseOptions(),
      interaction: { mode: "index", intersect: false },
      plugins: {
        ...baseOptions().plugins,
        tooltip: {
          ...baseOptions().plugins.tooltip,
          callbacks: {
            title: (items) => items[0].label,
            label: (item) => {
              const p = points[item.dataIndex];
              return [`${fmtInt(p.count)} incidents`, `${fmtNum(p.gallons)} gal spilled`];
            },
          },
        },
      },
      scales: {
        x: {
          grid: { display: false },
          border: { color: t.grid },
          ticks: { maxTicksLimit: 6, maxRotation: 0 },
        },
        y: {
          beginAtZero: true,
          grid: { color: t.grid },
          border: { display: false },
          ticks: { maxTicksLimit: 5, precision: 0 },
        },
      },
    },
  });
}

/**
 * Pins the HTML center label to the doughnut's true center.
 *
 * The legend occupies the right of the chart box, so Chart.js draws the ring
 * left of the box's midpoint — centering the label on the box would leave it
 * visibly off the ring. The arc reports its own center, so use that, and
 * re-apply on every draw so resizes and legend reflows stay aligned.
 */
const donutCenterLabel = {
  id: "donutCenterLabel",
  afterDraw(chart) {
    const arc = chart.getDatasetMeta(0)?.data?.[0];
    const label = document.getElementById("donut-total")?.parentElement;
    if (!arc || !label) return;

    const { canvas } = chart;
    label.style.left = `${canvas.offsetLeft + arc.x}px`;
    label.style.top = `${canvas.offsetTop + arc.y}px`;
  },
};

export function renderCategories(buckets, total) {
  const ctx = document.getElementById("chart-category");
  categoryChart?.destroy();

  // Identity encoding: top categories keep their fixed hue; the tail folds
  // into a single muted slice so hues are never cycled or invented. It is
  // labelled "Remaining categories" rather than "Other", because "Other" is
  // itself a substance category that can appear among the top slices.
  const present = buckets.filter((b) => b.count > 0);
  const top = present.slice(0, 6);
  const rest = present.slice(6).reduce((sum, b) => sum + b.count, 0);

  const labels = [...top.map((b) => labelize(b.key)), ...(rest ? ["Remaining categories"] : [])];
  const data = [...top.map((b) => b.count), ...(rest ? [rest] : [])];
  const colors = [...top.map((b) => categoryColor(b.key)), ...(rest ? [tones().muted] : [])];

  document.getElementById("donut-total").textContent = fmtInt(total);

  categoryChart = new Chart(ctx, {
    type: "doughnut",
    plugins: [donutCenterLabel],
    data: {
      labels,
      datasets: [{
        data,
        backgroundColor: colors,
        borderColor: tones().surface,
        borderWidth: 2,
        hoverOffset: 6,
      }],
    },
    options: {
      ...baseOptions(),
      cutout: "72%",
      plugins: {
        ...baseOptions().plugins,
        legend: {
          display: true,
          position: "right",
          labels: { boxWidth: 9, boxHeight: 9, usePointStyle: true, pointStyle: "circle", padding: 10 },
        },
        tooltip: {
          ...baseOptions().plugins.tooltip,
          callbacks: {
            label: (item) => ` ${fmtInt(item.parsed)} incidents (${((item.parsed / Math.max(total, 1)) * 100).toFixed(1)}%)`,
          },
        },
      },
    },
  });
}

export function renderMedium(buckets) {
  const t = tones();
  const ctx = document.getElementById("chart-medium");
  mediumChart?.destroy();

  const rows = buckets.filter((b) => b.count > 0);
  mediumChart = new Chart(ctx, {
    type: "bar",
    data: {
      labels: rows.map((b) => labelize(b.key)),
      datasets: [{
        data: rows.map((b) => b.count),
        backgroundColor: t.s1,
        borderRadius: 4,
        maxBarThickness: 18,
      }],
    },
    options: {
      ...baseOptions(),
      indexAxis: "y",
      plugins: {
        ...baseOptions().plugins,
        tooltip: {
          ...baseOptions().plugins.tooltip,
          callbacks: {
            label: (item) => {
              const b = rows[item.dataIndex];
              return [` ${fmtInt(b.count)} incidents`, ` ${fmtNum(b.gallons)} gal spilled`];
            },
          },
        },
      },
      scales: {
        x: {
          beginAtZero: true,
          grid: { color: t.grid },
          border: { display: false },
          ticks: { maxTicksLimit: 5, precision: 0 },
        },
        y: {
          grid: { display: false },
          border: { color: t.grid },
        },
      },
    },
  });
}

export function destroyCharts() {
  trendChart?.destroy();
  categoryChart?.destroy();
  mediumChart?.destroy();
}
