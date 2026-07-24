// Filter state with URL synchronization: the querystring is the single
// source of truth, so any dashboard view is a shareable permalink.

const FILTER_KEYS = ["search", "county", "region", "medium", "category", "source", "status", "from", "to"];

const listeners = new Set();

export function currentFilters() {
  const params = new URLSearchParams(location.search);
  const filters = {};
  for (const key of FILTER_KEYS) {
    const value = params.get(key);
    if (value) filters[key] = value;
  }
  return filters;
}

export function setFilter(key, value) {
  const params = new URLSearchParams(location.search);
  if (value) params.set(key, value);
  else params.delete(key);
  const qs = params.toString();
  history.replaceState(null, "", qs ? `?${qs}` : location.pathname);
  notify();
}

export function clearFilters() {
  history.replaceState(null, "", location.pathname);
  notify();
}

export function onFiltersChanged(fn) {
  listeners.add(fn);
}

function notify() {
  const filters = currentFilters();
  for (const fn of listeners) fn(filters);
}

export const FILTERS = FILTER_KEYS;
