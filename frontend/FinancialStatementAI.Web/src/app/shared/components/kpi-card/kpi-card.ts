import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { StatusTone } from '../../utils/status-tone.util';

export interface KpiTrend {
  /** Positive = up, negative = down, 0 = flat. Direction alone doesn't imply good/bad — set
   * `trendTone` explicitly (a rising failure count is bad news even though the number went up). */
  value: number;
  tone: StatusTone;
  label?: string; // e.g. "vs last 30 days"
}

/** One KPI metric on a summary row — used identically on the Dashboard, Transactions, Review,
 * and Reconciliation pages so every "total X" / "pending Y" widget in the app looks the same. */
@Component({
  selector: 'app-kpi-card',
  standalone: true,
  imports: [MatIconModule, MatProgressSpinnerModule],
  templateUrl: './kpi-card.html',
  styleUrl: './kpi-card.scss'
})
export class KpiCard {
  @Input({ required: true }) label = '';
  @Input() value: string | number = '';
  @Input() icon?: string;
  @Input() tone: StatusTone = 'info';
  @Input() trend?: KpiTrend;
  @Input() loading = false;
  @Input() clickable = false;
}
