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
  const value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  return value || fallback;
}

/** Resolves a list of CSS variable names (e.g. the --fsai-chart-1..8 sequence) to real color
 * strings in one call, preserving order — the common case for building a Chart.js palette. */
export function resolveChartPalette(names: readonly string[]): string[] {
  return names.map((name) => resolveCssVar(name));
}
