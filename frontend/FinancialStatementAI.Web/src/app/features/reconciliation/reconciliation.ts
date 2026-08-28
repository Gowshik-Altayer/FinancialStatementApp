import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { ChartConfiguration } from 'chart.js';
import { ReconciliationService } from '../../core/services/reconciliation.service';
import { ReconciliationResultStatus, ReconciliationSummary, ReconciliationSummaryCounts } from '../../shared/models/reconciliation.model';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { KpiCard } from '../../shared/components/kpi-card/kpi-card';
import { ChartCard } from '../../shared/components/chart-card/chart-card';
import { FilterPanel } from '../../shared/components/filter-panel/filter-panel';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';
import { LoadingState } from '../../shared/components/loading-state/loading-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { reconciliationStatusTone, processingStatusLabel } from '../../shared/utils/status-tone.util';
import { resolveChartPalette } from '../../shared/utils/chart-theme.util';

// Resolved once, not passed as raw `var(--x)` strings — see chart-theme.util.ts for why Chart.js
// (Canvas 2D) can't parse CSS custom-property syntax and silently renders unresolved colors as
// solid black.
const [RECONCILED_COLOR, MISMATCH_COLOR, INSUFFICIENT_COLOR, PENDING_COLOR] = resolveChartPalette([
  '--fsai-chart-2', '--fsai-chart-4', '--fsai-chart-3', '--fsai-chart-8'
]);

/// <summary>Cross-statement reconciliation (requirement 9) — every statement's current
/// reconciliation result at once, with KPIs, a status chart, and detailed mismatch information,
/// as opposed to the existing per-statement reconciliation card on Statement Detail.</summary>
@Component({
  selector: 'app-reconciliation',
  standalone: true,
  imports: [
    RouterLink,
    DecimalPipe,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatIconModule,
    MatButtonModule,
    MatPaginatorModule,
    PageHeader,
    KpiCard,
    ChartCard,
    FilterPanel,
    StatusBadge,
    LoadingState,
    ErrorState,
    EmptyState
  ],
  templateUrl: './reconciliation.html',
  styleUrl: './reconciliation.scss'
})
export class Reconciliation implements OnInit {
  private readonly reconciliationService = inject(ReconciliationService);

  readonly items = signal<ReconciliationSummary[]>([]);
  readonly counts = signal<ReconciliationSummaryCounts | null>(null);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);
  readonly expandedId = signal<string | null>(null);

  search = '';
  status: ReconciliationResultStatus | '' = '';
  pageIndex = 0;
  pageSize = 20;

  readonly reconciliationStatusTone = reconciliationStatusTone;
  readonly processingStatusLabel = processingStatusLabel;

  get hasActiveFilters(): boolean {
    return !!this.status;
  }

  get statusChartData(): ChartConfiguration['data'] {
    const c = this.counts();
    if (!c) return { labels: [], datasets: [] };
    return {
      labels: ['Reconciled', 'Mismatch', 'Insufficient Info', 'Pending'],
      datasets: [{
        data: [c.reconciledCount, c.mismatchCount, c.insufficientInformationCount, c.pendingCount],
        backgroundColor: [RECONCILED_COLOR, MISMATCH_COLOR, INSUFFICIENT_COLOR, PENDING_COLOR]
      }]
    };
  }

  readonly chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } } }
  };

  ngOnInit(): void {
    this.loadSummary();
    this.load();
  }

  onSearchValueChange(value: string): void {
    this.search = value;
    this.pageIndex = 0;
    this.load();
  }

  onFilterChange(): void {
    this.pageIndex = 0;
    this.load();
  }

  clearFilters(): void {
    this.status = '';
    this.pageIndex = 0;
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  toggleExpanded(item: ReconciliationSummary): void {
    this.expandedId.set(this.expandedId() === item.statementId ? null : item.statementId);
  }

  retry(): void {
    this.loadSummary();
    this.load();
  }

  private loadSummary(): void {
    this.reconciliationService.getSummary().subscribe({ next: (counts) => this.counts.set(counts) });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    this.reconciliationService
      .getAll({
        status: this.status || undefined,
        search: this.search || undefined,
        page: this.pageIndex + 1,
        pageSize: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.totalCount.set(result.totalCount);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadError.set(true);
        }
      });
  }
}
