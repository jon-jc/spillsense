// Color assignments follow the project's validated palette: categorical hues
// are bound to entities in fixed order (never cycled), with dark-mode steps
// selected for the dark surface rather than auto-inverted.

const LIGHT = {
  s1: "#2a78d6", s2: "#eb6834", s3: "#1baf7a", s4: "#eda100",
  s5: "#e87ba4", s6: "#008300", s7: "#4a3aa7", s8: "#e34948",
  muted: "#898781", ink2: "#52514e", ink3: "#898781",
  grid: "#e1e0d9", surface: "#fcfcfb",
};
const DARK = {
  s1: "#3987e5", s2: "#d95926", s3: "#199e70", s4: "#c98500",
  s5: "#d55181", s6: "#008300", s7: "#9085e9", s8: "#e66767",
  muted: "#898781", ink2: "#c3c2b7", ink3: "#898781",
  grid: "#2c2c2a", surface: "#1a1a19",
};

export const isDark = () => document.documentElement.dataset.theme === "dark";
export const tones = () => (isDark() ? DARK : LIGHT);

// Fixed slot order per substance category — identical in map, donut, and legend.
const CATEGORY_SLOTS = {
  DieselFuel: "s1",
  CrudeOil: "s2",
  Gasoline: "s3",
  LubeOrHydraulicOil: "s4",
  HeavyFuelOil: "s5",
  BilgeOrOilyWater: "s6",
  Chemical: "s7",
  JetFuel: "s8",
};

export const categoryColor = (category) => {
  const t = tones();
  return t[CATEGORY_SLOTS[category]] ?? t.muted;
};

export const CATEGORY_ORDER = [
  "DieselFuel", "CrudeOil", "Gasoline", "LubeOrHydraulicOil",
  "HeavyFuelOil", "BilgeOrOilyWater", "Chemical", "JetFuel",
  "Sewage", "Other", "Unknown",
];

export function applyStoredTheme() {
  const stored = localStorage.getItem("spillsense-theme");
  const preferred = stored ?? (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  document.documentElement.dataset.theme = preferred;
}

export function toggleTheme() {
  const next = isDark() ? "light" : "dark";
  document.documentElement.dataset.theme = next;
  localStorage.setItem("spillsense-theme", next);
}
