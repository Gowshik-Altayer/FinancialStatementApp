import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { TransactionService } from '../../core/services/transaction.service';
import { CategoryService } from '../../core/services/category.service';
import { StatementService } from '../../core/services/statement.service';
import { Transaction } from '../../shared/models/transaction.model';
import { Category } from '../../shared/models/category.model';
import { StatementSummary } from '../../shared/models/statement.model';
import { TransactionSummary, TransactionTypeFilter } from '../../shared/models/transaction-query.model';
import { TransactionTable } from '../../shared/components/transaction-table/transaction-table';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { KpiCard } from '../../shared/components/kpi-card/kpi-card';
import { FilterPanel } from '../../shared/components/filter-panel/filter-panel';
import { Skeleton } from '../../shared/components/skeleton/skeleton';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { EmptyState } from '../../shared/components/empty-state/empty-state';

type ReviewPriorityFilter = '' | 'HighConfidence' | 'ReviewRecommended' | 'ReviewRequired';
type CorrectedFilter = '' | 'true' | 'false';

const TRANSACTION_TYPE_OPTIONS: TransactionTypeFilter[] = ['Debit', 'Credit', 'Payment', 'Refund', 'Purchase', 'Transfer', 'Other'];
const PROCESSING_STATUS_OPTIONS = ['Uploaded', 'Processing', 'ExtractionFailed', 'ExtractionComplete', 'ClassificationComplete', 'PendingReview', 'Verified'];

/// <summary>Search/filter/paginate across every transaction the user owns, regardless of its
/// statement's processing status (Phase 13, redesigned per requirement 7) — as opposed to the
/// single-statement list or the PendingReview-only review queue.</summary>
@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatPaginatorModule,
    TransactionTable,
    PageHeader,
    KpiCard,
    FilterPanel,
    Skeleton,
    ErrorState,
    EmptyState
  ],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss'
})
export class Transactions implements OnInit {
  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly statementService = inject(StatementService);

  readonly transactions = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly statements = signal<StatementSummary[]>([]);
  readonly summary = signal<TransactionSummary | null>(null);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);

  readonly transactionTypeOptions = TRANSACTION_TYPE_OPTIONS;
  readonly processingStatusOptions = PROCESSING_STATUS_OPTIONS;

  search = '';
  merchant = '';
  categoryId = '';
  statementId = '';
  dateFrom = '';
  dateTo = '';
  amountMin: number | null = null;
  amountMax: number | null = null;
  transactionType: TransactionTypeFilter | '' = '';
  processingStatus = '';
  reviewPriority: ReviewPriorityFilter = '';
  correctedFilter: CorrectedFilter = '';
  pageIndex = 0;
  pageSize = 20;

  readonly hasActiveFilters = computed(() =>
    !!(
      this.merchant ||
      this.categoryId ||
      this.statementId ||
      this.dateFrom ||
      this.dateTo ||
      this.amountMin !== null ||
      this.amountMax !== null ||
      this.transactionType ||
      this.processingStatus ||
      this.reviewPriority ||
      this.correctedFilter
    )
  );

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((categories) => this.categories.set(categories));
    this.statementService.getAll({ pageSize: 100 }).subscribe((result) => this.statements.set(result.items));
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
    this.merchant = '';
    this.categoryId = '';
    this.statementId = '';
    this.dateFrom = '';
    this.dateTo = '';
    this.amountMin = null;
    this.amountMax = null;
    this.transactionType = '';
    this.processingStatus = '';
    this.reviewPriority = '';
    this.correctedFilter = '';
    this.pageIndex = 0;
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  retry(): void {
    this.loadSummary();
    this.load();
  }

  private loadSummary(): void {
    this.transactionService.getSummary().subscribe({ next: (summary) => this.summary.set(summary) });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    this.transactionService
      .search({
        search: this.search || undefined,
        merchant: this.merchant || undefined,
        categoryId: this.categoryId || undefined,
        statementId: this.statementId || undefined,
        dateFrom: this.dateFrom || undefined,
        dateTo: this.dateTo || undefined,
        amountMin: this.amountMin ?? undefined,
        amountMax: this.amountMax ?? undefined,
        transactionType: this.transactionType || undefined,
        processingStatus: this.processingStatus || undefined,
        reviewPriority: this.reviewPriority || undefined,
        hasBeenCorrected: this.correctedFilter === '' ? undefined : this.correctedFilter === 'true',
        page: this.pageIndex + 1,
        pageSize: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.transactions.set(result.items);
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
