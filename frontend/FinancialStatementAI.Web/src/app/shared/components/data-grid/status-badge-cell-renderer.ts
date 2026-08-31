import { StatusTone } from '../../utils/status-tone.util';

/// <summary>Renders the same pill look as app-status-badge, but as plain DOM rather than an
/// Angular component — AG Grid Angular's framework-component wrapper never invokes Angular
/// components passed as cell renderers in this app (see data-grid.ts's onGridReady comment).
/// Styling lives in data-grid.scss's shared .cell-badge rules, so this matches
/// transaction-cell-renderers.ts's renderConfidenceCell pixel-for-pixel.</summary>
export function renderStatusBadgeCell(label: string | null, tone: StatusTone | null): HTMLElement {
  if (!label) {
    const empty = document.createElement('span');
    empty.className = 'muted';
    empty.textContent = '—';
    return empty;
  }

  const badge = document.createElement('span');
  badge.className = `cell-badge tone-${tone ?? 'neutral'}`;
  badge.textContent = label;
  return badge;
}
