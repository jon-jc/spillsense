// Data-intake audit panel: import runs and their quarantined rows.

import { api } from "./api.js";
import { fmtDateTime, fmtInt, labelize } from "./format.js";

export async function loadImports() {
  const tbody = document.getElementById("import-rows");
  tbody.innerHTML = `<tr class="no-click"><td colspan="8"><span class="skeleton">Loading import history…</span></td></tr>`;

  let runs;
  try {
    runs = await api.imports();
  } catch (err) {
    tbody.innerHTML = `<tr class="no-click"><td colspan="8" class="empty">Could not load import history: ${err.message}</td></tr>`;
    return;
  }

  if (!runs.length) {
    tbody.innerHTML = `<tr class="no-click"><td colspan="8" class="empty">No imports yet — run <code>dotnet run -- import &lt;file.csv&gt;</code>.</td></tr>`;
    return;
  }

  tbody.innerHTML = runs.map((r) => `<tr data-id="${r.id}" data-rejects="${r.rejectedCount}">
      <td>${fmtDateTime(r.startedAtUtc)}</td>
      <td class="mono">${escapeHtml(r.sourceName)}</td>
      <td><span class="pill pill-${r.status}">${labelize(r.status)}</span></td>
      <td class="num">${fmtInt(r.totalRows)}</td>
      <td class="num">${fmtInt(r.insertedCount)}</td>
      <td class="num">${fmtInt(r.updatedCount)}</td>
      <td class="num">${fmtInt(r.rejectedCount)}</td>
      <td>${r.rejectedCount > 0 ? '<span class="popup-link">review</span>' : ""}</td>
    </tr>`).join("");

  for (const row of tbody.rows) {
    if (Number(row.dataset.rejects) > 0) {
      row.addEventListener("click", () => showQuarantine(Number(row.dataset.id), row));
    } else {
      row.classList.add("no-click");
    }
  }
}

async function showQuarantine(runId, row) {
  const panel = document.getElementById("quarantine-panel");
  const title = document.getElementById("quarantine-title");
  const holder = document.getElementById("quarantine-rows");

  panel.hidden = false;
  title.textContent = `Quarantined rows — ${row.cells[1].textContent}`;
  holder.innerHTML = `<span class="skeleton">Loading quarantined rows…</span>`;

  let records;
  try {
    records = await api.quarantine(runId);
  } catch (err) {
    holder.innerHTML = `<p class="empty">Could not load quarantine: ${err.message}</p>`;
    return;
  }

  holder.innerHTML = records.map((q) => `
    <div class="q-row">
      <strong>Row ${q.rowNumber}</strong>${q.reportNumber ? ` · <span class="mono">${escapeHtml(q.reportNumber)}</span>` : ""}
      <div class="raw">${escapeHtml(q.rawRow)}</div>
      <ul class="q-reasons">${q.reasons.map((r) => `<li>${escapeHtml(r)}</li>`).join("")}</ul>
    </div>`).join("");
  panel.scrollIntoView({ behavior: "smooth", block: "nearest" });
}

function escapeHtml(s) {
  return String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}
