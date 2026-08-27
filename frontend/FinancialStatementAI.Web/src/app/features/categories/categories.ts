import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { forkJoin } from 'rxjs';
import { ChartConfiguration } from 'chart.js';
import { CategoryService } from '../../core/services/category.service';
import { AuthService } from '../../core/services/auth.service';
import { CategoryDetail, CategoryStats } from '../../shared/models/category.model';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { KpiCard } from '../../shared/components/kpi-card/kpi-card';
import { ChartCard } from '../../shared/components/chart-card/chart-card';
import { FilterPanel } from '../../shared/components/filter-panel/filter-panel';
import { StatusBadge } from '../../shared/components/status-badge/status-badge';
import { LoadingState } from '../../shared/components/loading-state/loading-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { CategoryFormDialog, CategoryFormDialogResult } from './category-form-dialog/category-form-dialog';

interface CategoryCardModel {
  detail: CategoryDetail;
  stats: CategoryStats | null;
}

const CHART_COLORS = [
  'var(--fsai-chart-1)',
  'var(--fsai-chart-2)',
  'var(--fsai-chart-3)',
  'var(--fsai-chart-4)',
  'var(--fsai-chart-5)',
  'var(--fsai-chart-6)',
  'var(--fsai-chart-7)',
  'var(--fsai-chart-8)'
];

/// <summary>Category taxonomy management (requirement 10) — per-category transaction count/spend
/// and AI-vs-human-corrected split, plus Admin-only create/edit/deactivate. Everyone can view;
/// mutation actions are hidden (and separately enforced server-side) for non-Admins.</summary>
@Component({
  selector: 'app-categories',
  standalone: true,
  imports: [
    DecimalPipe,
    FormsModule,
    MatIconModule,
    MatButtonModule,
    MatSlideToggleModule,
    PageHeader,
    KpiCard,
    ChartCard,
    FilterPanel,
    StatusBadge,
    LoadingState,
    ErrorState,
    EmptyState
  ],
  templateUrl: './categories.html',
  styleUrl: './categories.scss'
})
export class Categories implements OnInit {
  private readonly categoryService = inject(CategoryService);
  private readonly authService = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly categories = signal<CategoryDetail[]>([]);
  readonly stats = signal<CategoryStats[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);
  readonly showInactive = signal(false);

  search = '';

  readonly isAdmin = computed(() => this.authService.currentUser()?.role === 'Admin');

  readonly cards = computed<CategoryCardModel[]>(() => {
    const statsById = new Map(this.stats().map((s) => [s.categoryId, s]));
    const term = this.search.trim().toLowerCase();

    return this.categories()
      .filter((c) => this.showInactive() || c.isActive)
      .filter((c) => !term || c.name.toLowerCase().includes(term))
      .map((detail) => ({ detail, stats: statsById.get(detail.id) ?? null }))
      .sort((a, b) => (b.stats?.transactionCount ?? 0) - (a.stats?.transactionCount ?? 0));
  });

  readonly totalCategories = computed(() => this.categories().length);
  readonly activeCategories = computed(() => this.categories().filter((c) => c.isActive).length);
  readonly totalTransactionsClassified = computed(() => this.stats().reduce((sum, s) => sum + s.transactionCount, 0));
  readonly totalAmount = computed(() => this.stats().reduce((sum, s) => sum + s.totalAmount, 0));

  get hasActiveFilters(): boolean {
    return this.showInactive();
  }

  get distributionChartData(): ChartConfiguration['data'] {
    const ranked = [...this.stats()].sort((a, b) => b.transactionCount - a.transactionCount).filter((s) => s.transactionCount > 0);
    return {
      labels: ranked.map((s) => s.categoryName),
      datasets: [
        {
          data: ranked.map((s) => s.transactionCount),
          backgroundColor: ranked.map((_, i) => CHART_COLORS[i % CHART_COLORS.length])
        }
      ]
    };
  }

  readonly chartOptions: ChartConfiguration['options'] = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: { legend: { position: 'bottom', labels: { boxWidth: 12, font: { size: 11 } } } }
  };

  ngOnInit(): void {
    this.load();
  }

  onSearchValueChange(value: string): void {
    this.search = value;
  }

  clearFilters(): void {
    this.showInactive.set(false);
  }

  toggleShowInactive(): void {
    this.showInactive.set(!this.showInactive());
  }

  retry(): void {
    this.load();
  }

  openAddDialog(): void {
    const ref = this.dialog.open(CategoryFormDialog, { data: {} });
    ref.afterClosed().subscribe((result?: CategoryFormDialogResult) => {
      if (!result) return;
      this.categoryService.create(result).subscribe({
        next: () => {
          this.snackBar.open(`"${result.name}" created.`, undefined, { duration: 3000 });
          this.load();
        },
        error: (err) => this.snackBar.open(err?.error?.detail ?? 'Could not create category.', undefined, { duration: 4000 })
      });
    });
  }

  openEditDialog(category: CategoryDetail): void {
    const ref = this.dialog.open(CategoryFormDialog, { data: { category } });
    ref.afterClosed().subscribe((result?: CategoryFormDialogResult) => {
      if (!result) return;
      this.categoryService.update(category.id, result).subscribe({
        next: () => {
          this.snackBar.open(`"${result.name}" updated.`, undefined, { duration: 3000 });
          this.load();
        },
        error: (err) => this.snackBar.open(err?.error?.detail ?? 'Could not update category.', undefined, { duration: 4000 })
      });
    });
  }

  deactivate(category: CategoryDetail): void {
    this.categoryService.deactivate(category.id).subscribe({
      next: () => {
        this.snackBar.open(`"${category.name}" deactivated.`, undefined, { duration: 3000 });
        this.load();
      },
      error: () => this.snackBar.open('Could not deactivate category.', undefined, { duration: 4000 })
    });
  }

  reactivate(category: CategoryDetail): void {
    this.categoryService.reactivate(category.id).subscribe({
      next: () => {
        this.snackBar.open(`"${category.name}" reactivated.`, undefined, { duration: 3000 });
        this.load();
      },
      error: () => this.snackBar.open('Could not reactivate category.', undefined, { duration: 4000 })
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    forkJoin({
      categories: this.categoryService.getAllIncludingInactive(),
      stats: this.categoryService.getStats()
    }).subscribe({
      next: ({ categories, stats }) => {
        this.categories.set(categories);
        this.stats.set(stats);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set(true);
      }
    });
  }
}
