// Formatting helpers shared across the dashboard.

const num = new Intl.NumberFormat("en-US");
const num1 = new Intl.NumberFormat("en-US", { maximumFractionDigits: 1 });
const dateFmt = new Intl.DateTimeFormat("en-US", { year: "numeric", month: "short", day: "numeric" });
const dateTimeFmt = new Intl.DateTimeFormat("en-US", {
  year: "numeric", month: "short", day: "numeric", hour: "numeric", minute: "2-digit", timeZoneName: "short",
});

export const fmtInt = (v) => (v == null ? "—" : num.format(v));
export const fmtNum = (v) => (v == null ? "—" : num1.format(v));
export const fmtDate = (iso) => (iso ? dateFmt.format(new Date(iso)) : "—");
export const fmtDateTime = (iso) => (iso ? dateTimeFmt.format(new Date(iso)) : "—");

/** Compact gallons: 1234567 -> "1.23M". */
export function fmtCompact(v) {
  if (v == null) return "—";
  if (Math.abs(v) >= 1e6) return `${num1.format(v / 1e6)}M`;
  if (Math.abs(v) >= 1e4) return `${num1.format(v / 1e3)}K`;
  return num1.format(v);
}

/** "UnderInvestigation" -> "Under investigation". */
export function labelize(value) {
  if (!value) return "—";
  return value
    .replace(/([a-z])([A-Z])/g, "$1 $2")
    .replace(/^./, (c) => c.toUpperCase())
    .replace(/\b(?!^)[A-Z](?=[a-z])/g, (c) => c.toLowerCase());
}

/** Animated count-up for KPI values. Always lands on the exact final value,
    even when animation frames never fire (hidden tab, reduced motion). */
export function countUp(el, target, formatter = fmtInt, duration = 550) {
  const done = () => { el.textContent = formatter(target); };
  if (target == null || document.hidden
      || matchMedia("(prefers-reduced-motion: reduce)").matches) {
    done();
    return;
  }
  const start = performance.now();
  const settle = setTimeout(done, duration + 120);
  function tick(now) {
    const t = Math.min(1, (now - start) / duration);
    const eased = 1 - (1 - t) ** 3;
    el.textContent = formatter(target * eased);
    if (t < 1) requestAnimationFrame(tick);
    else { clearTimeout(settle); done(); }
  }
  requestAnimationFrame(tick);
}

export function debounce(fn, ms = 250) {
  let handle;
  return (...args) => {
    clearTimeout(handle);
    handle = setTimeout(() => fn(...args), ms);
  };
}
