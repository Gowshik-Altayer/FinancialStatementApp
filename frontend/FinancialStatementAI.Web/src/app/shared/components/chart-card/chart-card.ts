import { Component, Input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatIconModule } from '@angular/material/icon';
import { BaseChartDirective } from 'ng2-charts';
import { ChartConfiguration, ChartType } from 'chart.js';

/** Titled card wrapping a single Chart.js chart (via ng2-charts) with consistent sizing and
 * loading/empty states — every chart on the Dashboard/Reconciliation/Categories pages renders
 * through this instead of each page wiring up <canvas baseChart> and its own spinner/empty text. */
@Component({
  selector: 'app-chart-card',
  standalone: true,
  imports: [MatProgressSpinnerModule, MatIconModule, BaseChartDirective],
  templateUrl: './chart-card.html',
  styleUrl: './chart-card.scss'
})
export class ChartCard {
  @Input({ required: true }) title = '';
  @Input({ required: true }) type: ChartType = 'bar';
  @Input({ required: true }) data: ChartConfiguration['data'] = { labels: [], datasets: [] };
  @Input() options: ChartConfiguration['options'] = {};
  @Input() loading = false;
  @Input() height = 260;

  get isEmpty(): boolean {
    const datasets = this.data?.datasets ?? [];
    return datasets.length === 0 || datasets.every((d) => !d.data || d.data.length === 0 || (d.data as number[]).every((v) => !v));
  }
}
