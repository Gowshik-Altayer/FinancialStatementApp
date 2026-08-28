import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { ChartConfiguration, ChartType } from 'chart.js';
import { forkJoin } from 'rxjs';
import { AuthService } from '../../core/services/auth.service';
import { DashboardService } from '../../core/services/dashboard.service';
import { DashboardConfigService } from '../../core/services/dashboard-config.service';
import { DashboardSummary, DashboardWidgetPreference } from '../../shared/models/dashboard.model';
import { KpiCard, KpiTrend } from '../../shared/components/kpi-card/kpi-card';
import { ChartCard } from '../../shared/components/chart-card/chart-card';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { Skeleton } from '../../shared/components/skeleton/skeleton';
import { PipelineStepper, PipelineStageViewModel } from '../../shared/components/pipeline-stepper/pipeline-stepper';
import { StatusTone, processingStatusLabel } from '../../shared/utils/status-tone.util';
import { resolveChartPalette } from '../../shared/utils/chart-theme.util';

interface KpiWidget {
  key: string;
  label: string;
  value: string | number;
  icon: string;
  tone: StatusTone;
  trend?: KpiTrend;
  route?: string;
}

interface ChartWidget {
  key: string;
  title: string;
  type: ChartType;
  data: ChartConfiguration['data'];
  options?: ChartConfiguration['options'];
}

// Resolved once per class load, not passed as raw `var(--x)` strings — Chart.js draws through
// Canvas 2D, whose fillStyle/strokeStyle can't parse CSS custom-property syntax at all (see
// chart-theme.util.ts). Handing it an unresolved "var(--fsai-chart-1)" silently falls back to
// canvas's default black fill, which is why every chart used to render solid black.
const CHART_PALETTE = resolveChartPalette([
  '--fsai-chart-1', '--fsai-chart-2', '--fsai-chart-3', '--fsai-chart-4',
  '--fsai-chart-5', '--fsai-chart-6', '--fsai-chart-7', '--fsai-chart-8'
]);

const CHART_OPTIONS_COMPACT: ChartConfiguration['options'] = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } } }
};

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [DatePipe, KpiCard, ChartCard, PageHeader, ErrorState, EmptyState, PipelineStepper, Skeleton],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss'
})
export class Dashboard implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly dashboardService = inject(DashboardService);
  private readonly dashboardConfigService = inject(DashboardConfigService);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly summary = signal<DashboardSummary | null>(null);
  readonly widgetOrder = signal<Map<string, DashboardWidgetPreference>>(new Map());

  readonly firstName = computed(() => this.authService.currentUser()?.firstName ?? '');

  readonly pipelineStages = computed<PipelineStageViewModel[]>(() => this.summary()?.pipelineStages ?? []);

  readonly kpiWidgets = computed<KpiWidget[]>(() => {
    const summary = this.summary();
    if (!summary) return [];

    const kpis = summary.kpis;
    const reconciliationTotal = kpis.reconciledCount + kpis.mismatchCount + kpis.pendingReconciliationCount;

    const all: KpiWidget[] = [
      { key: 'kpi-total-statements', label: 'Total Statements', value: kpis.totalStatements, icon: 'description', tone: 'info', route: '/statements' },
      { key: 'kpi-in-progress', label: 'Processing', value: kpis.inProgressCount, icon: 'sync', tone: 'info' },
      { key: 'kpi-completed', label: 'Completed', value: kpis.completedCount, icon: 'task_alt', tone: 'success' },
      { key: 'kpi-failed', label: 'Failed', value: kpis.failedCount, icon: 'error', tone: kpis.failedCount > 0 ? 'danger' : 'neutral' },
      { key: 'kpi-transactions-processed', label: 'Transactions Processed', value: kpis.transactionsProcessed, icon: 'receipt_long', tone: 'info', route: '/transactions' },
      { key: 'kpi-transactions-needing-review', label: 'Needing Review', value: kpis.transactionsNeedingReview, icon: 'fact_check', tone: kpis.transactionsNeedingReview > 0 ? 'warning' : 'success', route: '/review' },
      {
        key: 'kpi-reconciliation-status',
        label: 'Reconciled',
        value: reconciliationTotal > 0 ? `${kpis.reconciledCount}/${reconciliationTotal}` : 'No data',
        icon: 'balance',
        tone: kpis.mismatchCount > 0 ? 'warning' : 'success',
        route: '/reconciliation'
      },
      {
        key: 'kpi-avg-confidence',
        label: 'Avg. AI Confidence',
        value: kpis.averageClassificationConfidence !== null ? `${Math.round(kpis.averageClassificationConfidence * 100)}%` : 'No data',
        icon: 'psychology',
        tone: 'info'
      },
      {
        key: 'kpi-avg-processing-time',
        label: 'Avg. Processing Time',
        value: kpis.averageProcessingTimeSeconds !== null ? formatDuration(kpis.averageProcessingTimeSeconds) : 'No data',
        icon: 'timer',
        tone: 'neutral'
      }
    ];

    if (summary.usersOverview) {
      all.push({
        key: 'widget-users-overview',
        label: 'Active Users',
        value: `${summary.usersOverview.activeUsers}/${summary.usersOverview.totalUsers}`,
        icon: 'group',
        tone: 'info'
      });
    }

    return this.visibleSorted(all);
  });

  readonly chartWidgets = computed<ChartWidget[]>(() => {
    const summary = this.summary();
    if (!summary) return [];

    const all: ChartWidget[] = [
      {
        key: 'chart-processing-status',
        title: 'Processing Status',
        type: 'doughnut',
        data: {
          labels: summary.processingStatusBreakdown.map((s) => processingStatusLabel(s.name)),
          datasets: [{ data: summary.processingStatusBreakdown.map((s) => s.count), backgroundColor: CHART_PALETTE }]
        },
        options: CHART_OPTIONS_COMPACT
      },
      {
        key: 'chart-processing-trend',
        title: 'Processing Trend',
        type: 'line',
        data: {
          labels: summary.processingTrend.map((p) => p.date),
          datasets: [
            { label: 'Uploaded', data: summary.processingTrend.map((p) => p.uploadedCount), borderColor: CHART_PALETTE[0], tension: 0.3 },
            { label: 'Completed', data: summary.processingTrend.map((p) => p.completedCount), borderColor: CHART_PALETTE[1], tension: 0.3 },
            { label: 'Failed', data: summary.processingTrend.map((p) => p.failedCount), borderColor: CHART_PALETTE[3], tension: 0.3 }
          ]
        },
        options: CHART_OPTIONS_COMPACT
      },
      {
        key: 'chart-transaction-categories',
        title: 'Spend by Category',
        type: 'bar',
        data: {
          labels: summary.transactionsByCategory.slice(0, 8).map((c) => c.categoryName),
          datasets: [{ label: 'Amount', data: summary.transactionsByCategory.slice(0, 8).map((c) => c.totalAmount), backgroundColor: CHART_PALETTE[0] }]
        },
        options: { ...CHART_OPTIONS_COMPACT, plugins: { legend: { display: false } } }
      },
      {
        key: 'chart-confidence-distribution',
        title: 'AI Confidence Distribution',
        type: 'bar',
        data: {
          labels: summary.confidenceDistribution.map((b) => b.name.replace(/([a-z])([A-Z])/g, '$1 $2')),
          datasets: [{ label: 'Transactions', data: summary.confidenceDistribution.map((b) => b.count), backgroundColor: [CHART_PALETTE[1], CHART_PALETTE[2], CHART_PALETTE[3], CHART_PALETTE[7]] }]
        },
        options: { ...CHART_OPTIONS_COMPACT, plugins: { legend: { display: false } } }
      },
      {
        key: 'chart-reconciliation-status',
        title: 'Reconciliation Status',
        type: 'doughnut',
        data: {
          labels: summary.reconciliationStatusBreakdown.map((s) => processingStatusLabel(s.name)),
          datasets: [{ data: summary.reconciliationStatusBreakdown.map((s) => s.count), backgroundColor: [CHART_PALETTE[1], CHART_PALETTE[3], CHART_PALETTE[2]] }]
        },
        options: CHART_OPTIONS_COMPACT
      },
      {
        key: 'chart-review-statistics',
        title: 'Review Statistics',
        type: 'bar',
        data: {
          labels: ['Pending', 'Corrected', 'AI Accepted'],
          datasets: [{
            label: 'Transactions',
            data: [summary.reviewStatistics.pendingCount, summary.reviewStatistics.correctedCount, summary.reviewStatistics.aiAcceptedCount],
            backgroundColor: [CHART_PALETTE[2], CHART_PALETTE[4], CHART_PALETTE[1]]
          }]
        },
        options: { ...CHART_OPTIONS_COMPACT, plugins: { legend: { display: false } } }
      }
    ];

    return this.visibleSorted(all);
  });

  readonly showRecentActivity = computed(() => this.isVisible('recent-activity'));
  readonly showPipeline = computed(() => this.isVisible('pipeline-stepper'));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(false);

    forkJoin({
      summary: this.dashboardService.getSummary(),
      config: this.dashboardConfigService.getMyConfig()
    }).subscribe({
      next: ({ summary, config }) => {
        this.summary.set(summary);
        this.widgetOrder.set(new Map(config.map((w) => [w.widgetKey, w])));
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  onStageClick(stage: PipelineStageViewModel): void {
    const routes: Record<string, string> = {
      review: '/review',
      reconciliation: '/reconciliation',
      'transaction-extraction': '/transactions'
    };
    const route = routes[stage.key];
    if (route) {
      this.router.navigate([route]);
    }
  }

  onKpiClick(widget: KpiWidget): void {
    if (widget.route) {
      this.router.navigate([widget.route]);
    }
  }

  private isVisible(key: string): boolean {
    const config = this.widgetOrder().get(key);
    return config ? config.isVisible : true;
  }

  private visibleSorted<T extends { key: string }>(widgets: T[]): T[] {
    return widgets
      .filter((w) => this.isVisible(w.key))
      .sort((a, b) => this.sortOrderFor(a.key) - this.sortOrderFor(b.key));
  }

  private sortOrderFor(key: string): number {
    return this.widgetOrder().get(key)?.sortOrder ?? 999;
  }
}

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${Math.round(seconds)}s`;
  const minutes = Math.floor(seconds / 60);
  const remainingSeconds = Math.round(seconds % 60);
  return remainingSeconds > 0 ? `${minutes}m ${remainingSeconds}s` : `${minutes}m`;
}
