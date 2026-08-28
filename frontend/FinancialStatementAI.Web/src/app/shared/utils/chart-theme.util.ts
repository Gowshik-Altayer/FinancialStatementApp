import { Chart } from 'chart.js';

/** The app's font stack, matching styles/_typography.scss. Kept as a literal here because canvas
 * needs a concrete family string — it can't read the --fsai-font-sans custom property. */
const CHART_FONT_FAMILY = "'Inter', 'Segoe UI', Roboto, system-ui, sans-serif";

// Chart.js ships its own default of "'Helvetica Neue', 'Helvetica', 'Arial', sans-serif", which it
// uses for anything a chart doesn't explicitly configure. Setting the global default here means
// every piece of chart text — including parts not covered by the per-chart options below, and any
// chart added later — renders in the app's face instead of silently falling back to Helvetica.
// This module is only imported by chart-bearing routes, so it costs nothing elsewhere.
Chart.defaults.font.family = CHART_FONT_FAMILY;

/** Resolves a CSS custom property (e.g. "--fsai-chart-1") to its actual computed color value
 * (e.g. "#0284c7"). Chart.js draws through the Canvas 2D API, and `ctx.fillStyle`/`strokeStyle`
 * cannot parse `var(--x)` syntax — that's a CSS-cascade feature, not something canvas's color
 * parser understands. Handing Chart.js the literal string "var(--fsai-chart-1)" makes every
 * fillStyle assignment silently fail and fall back to canvas's default black, which is why charts
 * rendered solid black regardless of how the tokens themselves were defined. Every chart dataset
 * color must be resolved through this (or resolveChartPalette) before reaching a ChartConfiguration. */
export function resolveCssVar(name: string, fallback = '#64748b'): string {
  if (typeof window === 'undefined' || typeof document === 'undefined') {
    return fallback;
  }

  // Resolved through a probe element rather than read straight off documentElement with
  // getPropertyValue(). getPropertyValue returns a custom property's *declared* value, and Angular
  // Material declares every M3 token using the CSS light-dark() function — so the raw read yields
  // the literal string "light-dark(#1b1b1f, #e5e1e6)". Canvas's colour parser doesn't understand
  // light-dark(), so assigning it silently leaves the previous fillStyle in place: the tooltip
  // title, for instance, rendered in Chart.js's default colour instead of the theme's. Assigning
  // the var to a real element and reading back the computed `color` makes the browser resolve
  // light-dark(), color-mix(), nested vars and the current colour-scheme down to a plain rgb()
  // that canvas accepts. The CSS-level fallback also covers a token that doesn't exist at all.
  const probe = document.createElement('span');
  probe.style.cssText = 'position:absolute;left:-9999px;top:-9999px;visibility:hidden;pointer-events:none';
  probe.style.color = `var(${name}, ${fallback})`;
  document.body.appendChild(probe);
  const resolved = getComputedStyle(probe).color;
  probe.remove();

  return resolved || fallback;
}

/** Resolves a list of CSS variable names (e.g. the --fsai-chart-1..8 sequence) to real color
 * strings in one call, preserving order — the common case for building a Chart.js palette. */
export function resolveChartPalette(names: readonly string[]): string[] {
  return names.map((name) => resolveCssVar(name));
}

/** Shared Chart.js options. Chart.js's out-of-the-box tooltip/legend/grid styling is generic and
 * doesn't know about the app's font or surface colors, so charts previously looked like a
 * third-party widget dropped onto the page. Every chart should spread this and then override only
 * what's genuinely specific to it, rather than each page re-declaring legend position and
 * responsive flags.
 *
 * Note this reads CSS variables at call time, so callers must invoke it (not hold a module-level
 * constant) if they need it to reflect a later light/dark theme switch. */
export function baseChartOptions(): Record<string, unknown> {
  const font = { family: CHART_FONT_FAMILY, size: 11 };
  const onSurface = resolveCssVar('--fsai-neutral', '#475569');
  const gridColor = resolveCssVar('--mat-sys-outline-variant', '#e1e4e9');
  const surface = resolveCssVar('--mat-sys-surface', '#ffffff');

  return {
    responsive: true,
    maintainAspectRatio: false,
    // Hovering anywhere in a column/slice band highlights it, rather than requiring a pixel-exact
    // hit on the mark itself — the difference between a chart that feels responsive and one that
    // feels broken.
    interaction: { mode: 'nearest', intersect: false },
    plugins: {
      legend: {
        position: 'bottom',
        labels: {
          boxWidth: 10,
          boxHeight: 10,
          usePointStyle: true,
          pointStyle: 'circle',
          padding: 16,
          color: onSurface,
          font
        }
      },
      tooltip: {
        backgroundColor: surface,
        titleColor: resolveCssVar('--mat-sys-on-surface', '#1a1c20'),
        bodyColor: onSurface,
        borderColor: gridColor,
        borderWidth: 1,
        padding: 10,
        cornerRadius: 8,
        displayColors: true,
        usePointStyle: true,
        titleFont: { ...font, size: 12, weight: 600 },
        bodyFont: font
      }
    },
    scales: {
      x: {
        grid: { display: false },
        border: { color: gridColor },
        ticks: { color: onSurface, font }
      },
      y: {
        beginAtZero: true,
        grid: { color: gridColor },
        border: { display: false },
        ticks: { color: onSurface, font }
      }
    }
  };
}

/** Same as baseChartOptions but with the cartesian scales stripped — doughnut/pie charts have no
 * axes, and leaving `scales` on them makes Chart.js log warnings. */
export function baseRadialChartOptions(): Record<string, unknown> {
  const options = baseChartOptions();
  delete options['scales'];
  return options;
}
