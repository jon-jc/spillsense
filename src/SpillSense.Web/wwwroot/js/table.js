// Incident records table: server-side paging and sorting, inline volume
// bars, and status pills. Row click opens the detail drawer.

import { api } from "./api.js";
import { fmtDate, fmtInt, fmtNum, labelize } from "./format.js";

const PAGE_SIZE = 12;

const state = { page: 1, sort: "-reportedAt", maxGallons: 1 };
let openDetail = () => {};
let getFilters = () => ({});

export function initTable(filtersProvider, detailHandler) {
  getFilters = filtersProvider;
  openDetail = detailHandler;

  document.getElementById("page-prev").addEventListener("click", () => {
    if (state.page > 1) { state.page--; refreshTable(); }
  });
  document.getElementById("page-next").addEventListener("click", () => {
    state.page++; refreshTable();
  });
  for (const btn of document.querySelectorAll(".th-sort")) {
    btn.addEventListener("click", () => {
      const key = btn.dataset.sort;
      state.sort = state.sort === `-${key}` ? key : `-${key}`;
      state.page = 1;
      refreshTable();
    });
  }
}

export function resetTablePage() {
  state.page = 1;
}

export async function refreshTable() {
  const tbody = document.getElementById("incident-rows");
  tbody.innerHTML = `<tr class="no-click"><td colspan="7"><span class="skeleton">Loading incident records…</span></td></tr>`;

  let result;
  try {
    result = await api.incidents(getFilters(), { page: state.page, pageSize: PAGE_SIZE, sort: state.sort });
  } catch (err) {
    tbody.innerHTML = `<tr class="no-click"><td colspan="7" class="empty">Could not load incidents: ${err.message}</td></tr>`;
    return;
  }

  const { total, items } = result;
  state.maxGallons = Math.max(1, ...items.map((i) => i.quantityGallons ?? 0));

  document.getElementById("table-count").textContent = `${fmtInt(total)} matching incidents`;
  const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
  if (state.page > pages) { state.page = pages; return refreshTable(); }

  document.getElementById("page-info").textContent = total
    ? `Page ${state.page} of ${fmtInt(pages)}`
    : "";
  document.getElementById("page-prev").disabled = state.page <= 1;
  document.getElementById("page-next").disabled = state.page >= pages;

  for (const btn of document.querySelectorAll(".th-sort")) {
    const key = btn.dataset.sort;
    btn.querySelector(".sort-ind").textContent =
      state.sort === key ? "▲" : state.sort === `-${key}` ? "▼" : "";
  }

  if (!items.length) {
    tbody.innerHTML = `<tr class="no-click"><td colspan="7" class="empty">No incidents match the current filters.</td></tr>`;
    return;
  }

  tbody.innerHTML = items.map(rowHtml).join("");
  [...tbody.rows].forEach((row, i) => {
    row.addEventListener("click", () => openDetail(items[i].reportNumber));
    row.tabIndex = 0;
    row.addEventListener("keydown", (e) => {
      if (e.key === "Enter") openDetail(items[i].reportNumber);
    });
  });
}

function rowHtml(i) {
  const qty = i.quantityGallons;
  const share = qty ? Math.max(4, Math.round((qty / state.maxGallons) * 100)) : 0;
  return `<tr>
    <td>${fmtDate(i.reportedAtUtc)}</td>
    <td class="mono">${i.reportNumber}</td>
    <td>${escapeHtml(i.substanceName)}<span class="cell-sub">${labelize(i.substanceCategory)}</span></td>
    <td>${i.county ?? "—"}</td>
    <td>${labelize(i.sourceType)}</td>
    <td class="num"><span class="qty-cell">${qty != null ? fmtNum(qty) : "—"}<span class="qty-bar"><i style="width:${share}%"></i></span></span></td>
    <td><span class="pill pill-${i.status}">${labelize(i.status)}</span></td>
  </tr>`;
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
