// Renders the API reference from the OpenAPI document.
//
// Reads the same spec the ASP.NET host generates at /openapi/v1.json, which is
// also published as a static openapi.json for the serverless deployment — so
// the reference stays correct on both hosts with no service to depend on.

import { applyStoredTheme, toggleTheme } from "./palette.js";

applyStoredTheme();
document.getElementById("theme-toggle").addEventListener("click", toggleTheme);

const escapeHtml = (s) =>
  String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

/** Prefers the live document, falling back to the published static copy. */
async function loadSpec() {
  for (const url of ["openapi/v1.json", "openapi.json"]) {
    try {
      const response = await fetch(url);
      if (response.ok) return await response.json();
    } catch { /* try the next source */ }
  }
  throw new Error("No OpenAPI document available.");
}

const slug = (method, path) =>
  `${method}-${path}`.toLowerCase().replace(/[^a-z0-9]+/g, "-").replace(/(^-|-$)/g, "");

function schemaType(schema) {
  if (!schema) return "";
  if (schema.$ref) return schema.$ref.split("/").pop();
  if (schema.type === "array") return `${schemaType(schema.items)}[]`;
  const types = Array.isArray(schema.type)
    ? schema.type.filter((t) => t !== "null")
    : [schema.type].filter(Boolean);
  return types.join(" | ") || "object";
}

/** Enum values live either on the schema or inside its anyOf branches. */
function enumValues(schema) {
  if (!schema) return [];
  if (schema.enum) return schema.enum.filter((v) => v !== null);
  for (const branch of schema.anyOf ?? schema.oneOf ?? []) {
    if (branch.enum) return branch.enum.filter((v) => v !== null);
  }
  return [];
}

function renderParams(parameters) {
  if (!parameters?.length) return "";
  const rows = parameters.map((p) => {
    const values = enumValues(p.schema);
    const enums = values.length
      ? `<div class="enum-list">${values.map((v) => `<span class="enum-val">${escapeHtml(v)}</span>`).join("")}</div>`
      : "";
    return `<tr>
      <td>${escapeHtml(p.name)}${p.required ? ' <span class="type">required</span>' : ""}</td>
      <td class="type">${escapeHtml(schemaType(p.schema))}<br><span class="type">${escapeHtml(p.in)}</span></td>
      <td>${escapeHtml(p.description ?? "")}${enums}</td>
    </tr>`;
  }).join("");

  return `<div>
    <div class="op-section-title">Parameters</div>
    <table class="params">
      <thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;
}

function renderResponses(responses) {
  const items = Object.entries(responses ?? {}).map(([code, body]) => {
    const cls = code.startsWith("2") ? "resp-2xx" : "resp-4xx";
    const schema = body.content?.["application/json"]?.schema
      ?? body.content?.["text/csv"]?.schema;
    const type = schemaType(schema);
    return `<span class="resp ${cls}"><b>${escapeHtml(code)}</b> ${escapeHtml(body.description ?? "")}${
      type ? ` <span class="type">${escapeHtml(type)}</span>` : ""}</span>`;
  }).join("");
  return items ? `<div><div class="op-section-title">Responses</div><div class="responses">${items}</div></div>` : "";
}

function renderOperation(method, path, op) {
  const id = slug(method, path);
  // Only GETs without path placeholders are safe to open directly.
  const runnable = method === "get" && !path.includes("{");
  const prettyPath = escapeHtml(path).replace(/\{(\w+)\}/g,
    (_, name) => `<span class="param">{${name}}</span>`);

  return `<article class="card op" id="${id}">
    <div class="op-head">
      <span class="verb verb-${method}">${method}</span>
      <span class="op-path">${prettyPath}</span>
      <span class="op-summary">${escapeHtml(op.summary ?? "")}</span>
    </div>
    <div class="op-body">
      ${op.description ? `<p class="op-desc">${escapeHtml(op.description)}</p>` : ""}
      ${renderParams(op.parameters)}
      ${renderResponses(op.responses)}
      ${runnable ? `<div class="try-row"><a class="try-link" href=".${escapeHtml(path)}" target="_blank" rel="noopener">Open live response ↗</a></div>` : ""}
    </div>
  </article>`;
}

function groupByTag(spec) {
  const groups = new Map();
  for (const [path, item] of Object.entries(spec.paths)) {
    for (const [method, op] of Object.entries(item)) {
      if (!["get", "post", "put", "patch", "delete"].includes(method)) continue;
      const tag = op.tags?.[0] ?? "General";
      if (!groups.has(tag)) groups.set(tag, []);
      groups.get(tag).push({ method, path, op });
    }
  }
  for (const ops of groups.values()) {
    ops.sort((a, b) => a.path.localeCompare(b.path) || a.method.localeCompare(b.method));
  }
  return groups;
}

function render(spec) {
  document.getElementById("api-title").textContent = spec.info?.title ?? "API";
  document.getElementById("api-description").textContent = spec.info?.description ?? "";
  document.getElementById("api-version").textContent = `OpenAPI ${spec.openapi}`;

  const groups = groupByTag(spec);
  const operationCount = [...groups.values()].reduce((n, ops) => n + ops.length, 0);
  document.getElementById("api-counts").textContent =
    `${operationCount} operations across ${groups.size} groups`;

  document.getElementById("operations").innerHTML = [...groups]
    .map(([tag, ops]) => `<section class="op-group" data-group="${escapeHtml(tag)}">
        <h2 class="op-group-head">${escapeHtml(tag)}</h2>
        ${ops.map((o) => renderOperation(o.method, o.path, o.op)).join("")}
      </section>`).join("");

  document.getElementById("docs-toc").innerHTML = [...groups]
    .map(([tag, ops]) => `<div data-toc-group="${escapeHtml(tag)}">
        <div class="toc-group-title">${escapeHtml(tag)}</div>
        ${ops.map((o) => `<a class="toc-link" href="#${slug(o.method, o.path)}">
            <span class="verb verb-${o.method}">${o.method}</span>
            <span>${escapeHtml(o.path.replace(/^\/api\//, ""))}</span>
          </a>`).join("")}
      </div>`).join("");

  wireFilter();
  wireScrollSpy();
}

function wireFilter() {
  const input = document.getElementById("op-search");
  input.addEventListener("input", () => {
    const term = input.value.trim().toLowerCase();
    let visible = 0;

    for (const op of document.querySelectorAll(".op")) {
      const match = !term || op.textContent.toLowerCase().includes(term);
      op.hidden = !match;
      if (match) visible++;
    }
    // Hide a group heading once every operation under it is filtered out.
    for (const group of document.querySelectorAll(".op-group")) {
      group.hidden = ![...group.querySelectorAll(".op")].some((op) => !op.hidden);
    }
    for (const link of document.querySelectorAll(".toc-link")) {
      const target = document.querySelector(link.getAttribute("href"));
      link.hidden = target?.hidden ?? false;
    }
    for (const tocGroup of document.querySelectorAll("[data-toc-group]")) {
      tocGroup.hidden = ![...tocGroup.querySelectorAll(".toc-link")].some((l) => !l.hidden);
    }

    document.getElementById("operations").dataset.empty = visible === 0 ? "true" : "false";
  });
}

/** Highlights the sidebar entry for the operation currently in view. */
function wireScrollSpy() {
  const links = new Map([...document.querySelectorAll(".toc-link")]
    .map((l) => [l.getAttribute("href").slice(1), l]));

  const observer = new IntersectionObserver((entries) => {
    for (const entry of entries) {
      if (!entry.isIntersecting) continue;
      for (const l of links.values()) l.classList.remove("active");
      links.get(entry.target.id)?.classList.add("active");
    }
  }, { rootMargin: "-84px 0px -70% 0px", threshold: 0 });

  for (const op of document.querySelectorAll(".op")) observer.observe(op);
}

try {
  render(await loadSpec());
} catch (err) {
  document.getElementById("operations").innerHTML =
    `<div class="card docs-empty">Could not load the API document: ${escapeHtml(err.message)}</div>`;
}
