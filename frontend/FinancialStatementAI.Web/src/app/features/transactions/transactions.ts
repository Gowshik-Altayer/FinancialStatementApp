import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import {
  CellValueChangedEvent,
  ColDef,
  FilterChangedEvent,
  GetRowIdFunc,
  GridApi,
  IDateFilterParams,
  ICellRendererParams,
  RowClassRules
} from 'ag-grid-community';
import { TransactionService } from '../../core/services/transaction.service';
import { CategoryService } from '../../core/services/category.service';
import { StatementService } from '../../core/services/statement.service';
import { NotificationService } from '../../core/services/notification.service';
import { Transaction } from '../../shared/models/transaction.model';
import { Category } from '../../shared/models/category.model';
import { StatementSummary } from '../../shared/models/statement.model';
import { TransactionSummary, TransactionTypeFilter } from '../../shared/models/transaction-query.model';
import { DataGrid } from '../../shared/components/data-grid/data-grid';
import { SelectFilter, SelectFilterOption, SelectFloatingFilter } from '../../shared/components/data-grid/select-filter';
import {
  formatTransactionAmount,
  renderCategoryCell,
  renderConfidenceCell,
  renderDescriptionCell,
  renderHistoryActionCell,
  renderStatementLinkCell
} from '../../shared/components/data-grid/transaction-cell-renderers';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { KpiCard } from '../../shared/components/kpi-card/kpi-card';
import { Skeleton } from '../../shared/components/skeleton/skeleton';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { EmptyState } from '../../shared/components/empty-state/empty-state';
import { TransactionHistoryDialog } from '../../shared/components/transaction-history-dialog/transaction-history-dialog';
import { reviewPriorityLabel } from '../../shared/utils/status-tone.util';

const REVIEW_PRIORITIES = ['HighConfidence', 'ReviewRecommended', 'ReviewRequired'] as const;

/// <summary>Server hard-caps a single page at 100 rows (PaginationDefaults.MaxPageSize) — loading
/// "all" transactions for client-side filtering means looping pages at that cap, not one big
/// request.</summary>
const LOAD_ALL_PAGE_SIZE = 100;

const TRANSACTION_TYPE_OPTIONS: TransactionTypeFilter[] = ['Debit', 'Credit', 'Payment', 'Refund', 'Purchase', 'Transfer', 'Other'];

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
    MatButtonModule,
    DataGrid,
    PageHeader,
    KpiCard,
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
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  readonly allTransactions = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly statements = signal<StatementSummary[]>([]);
  readonly summary = signal<TransactionSummary | null>(null);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);

  readonly transactionTypeOptions = TRANSACTION_TYPE_OPTIONS;

  readonly search = signal('');
  private readonly columnFiltersActive = signal(false);
  readonly hasActiveFilters = computed(() => !!this.search().trim() || this.columnFiltersActive());

  // AG Grid filters (per-column) run on whatever rowData it's given — pre-filtering by the search
  // box here, then handing the result to <app-data-grid>, combines the free-text search with AG
  // Grid's own native column filters without fighting AG Grid's quickFilter (which by default
  // ignores fields, like merchant, that aren't their own visible column).
  readonly transactions = computed(() => {
    const query = this.search().trim().toLowerCase();
    if (!query) return this.allTransactions();
    return this.allTransactions().filter(
      (t) =>
        (t.description ?? '').toLowerCase().includes(query) ||
        (t.merchant ?? '').toLowerCase().includes(query) ||
        (t.statementFileName ?? '').toLowerCase().includes(query)
    );
  });

  readonly getRowId: GetRowIdFunc<Transaction> = (params) => params.data.id;
  private gridApi?: GridApi<Transaction>;

  // A plain, once-assigned array rather than a computed() signal — DataGrid's AG Grid instance
  // only needs this rebuilt when categories/statements load; recomputing a fresh array on every
  // change detection pass (as a computed() would when read via columnDefs() in the template)
  // gives AG Grid a new array reference to compare against on every check, which is unnecessary
  // churn for something that only actually changes twice (once per lookup list loaded).
  columnDefs: ColDef<Transaction>[] = this.buildColumnDefs();

  private buildColumnDefs(): ColDef<Transaction>[] {
    const categoryOptions: SelectFilterOption[] = this.categories().map((c) => ({ value: c.name, label: c.name }));
    const statementOptions: SelectFilterOption[] = [...new Set(this.statements().map((s) => s.originalFileName))].map((name) => ({
      value: name,
      label: name
    }));
    const typeOptions: SelectFilterOption[] = TRANSACTION_TYPE_OPTIONS.map((t) => ({ value: t, label: t }));
    const confidenceOptions: SelectFilterOption[] = REVIEW_PRIORITIES.map((rp) => ({ value: rp, label: reviewPriorityLabel(rp) }));

    return [
      {
        headerName: 'Statement',
        field: 'statementFileName',
        cellRenderer: (p: ICellRendererParams<Transaction>) => renderStatementLinkCell(p, this.router),
        filter: SelectFilter,
        floatingFilterComponent: SelectFloatingFilter,
        filterParams: { options: statementOptions },
        floatingFilterComponentParams: { options: statementOptions },
        flex: 1.2,
        minWidth: 160
      },
      {
        headerName: 'Date',
        field: 'transactionDate',
        valueFormatter: (p) => p.value ?? '—',
        filter: 'agDateColumnFilter',
        filterParams: {
          // transactionDate is a plain "yyyy-MM-dd" string, not a JS Date — AG Grid's date filter
          // needs an explicit comparator to compare it against the date picked in its UI.
          comparator: (filterLocalDateAtMidnight: Date, cellValue: string | null) => {
            if (!cellValue) return -1;
            const cellDate = new Date(`${cellValue}T00:00:00`);
            if (cellDate < filterLocalDateAtMidnight) return -1;
            if (cellDate > filterLocalDateAtMidnight) return 1;
            return 0;
          }
        } satisfies IDateFilterParams,
        width: 140
      },
      {
        headerName: 'Description',
        field: 'description',
        cellRenderer: renderDescriptionCell,
        filter: 'agTextColumnFilter',
        // Matches description OR merchant text — replicates the old Merchant filter's behavior
        // without needing a separate visible Merchant column.
        filterValueGetter: (p) => `${p.data?.description ?? ''} ${p.data?.merchant ?? ''}`,
        flex: 2,
        minWidth: 220
      },
      {
        headerName: 'Amount',
        field: 'amount',
        width: 160,
        type: 'rightAligned',
        valueFormatter: (p) => formatTransactionAmount(p.value),
        filter: 'agNumberColumnFilter'
      },
      {
        headerName: 'Type',
        field: 'transactionType',
        width: 130,
        filter: SelectFilter,
        floatingFilterComponent: SelectFloatingFilter,
        filterParams: { options: typeOptions },
        floatingFilterComponentParams: { options: typeOptions }
      },
      {
        headerName: 'Category',
        field: 'categoryName',
        cellRenderer: renderCategoryCell,
        editable: true,
        // AG Grid's own built-in select editor (identified by string name, not a custom Angular
        // component class) — unaffected by the framework-wrapper issue above since it never goes
        // through ICellRendererAngularComp at all.
        cellEditor: 'agSelectCellEditor',
        cellEditorParams: { values: categoryOptions.map((o) => o.value) },
        filter: SelectFilter,
        floatingFilterComponent: SelectFloatingFilter,
        filterParams: { options: categoryOptions },
        floatingFilterComponentParams: { options: categoryOptions },
        width: 190
      },
      {
        headerName: 'Confidence',
        field: 'reviewPriority',
        cellRenderer: renderConfidenceCell,
        filter: SelectFilter,
        floatingFilterComponent: SelectFloatingFilter,
        filterParams: { options: confidenceOptions },
        floatingFilterComponentParams: { options: confidenceOptions },
        width: 180,
        sortable: false
      },
      {
        headerName: '',
        cellRenderer: (p: ICellRendererParams<Transaction>) => renderHistoryActionCell(p, this.dialog, TransactionHistoryDialog),
        width: 64,
        sortable: false,
        resizable: false,
        filter: false
      }
    ];
  }

  readonly rowClassRules: RowClassRules<Transaction> = {
    'row-duplicate': (params) => !!params.data?.isPotentialDuplicate
  };

  onCellValueChanged(event: CellValueChangedEvent<Transaction>): void {
    if (event.colDef.field !== 'categoryName' || !event.data) return;

    const transaction = event.data;
    const newCategoryName = event.newValue as string;
    if (!newCategoryName || newCategoryName === event.oldValue) return;

    this.transactionService.correctCategory(transaction.id, newCategoryName).subscribe({
      next: (updated) => {
        Object.assign(transaction, updated);
        this.notifications.success('Category corrected.');
      },
      error: () => {
        transaction.categoryName = event.oldValue;
        this.notifications.error('Correction failed — please try again.');
      }
    });
  }

  onGridApiReady(api: GridApi<Transaction>): void {
    this.gridApi = api;
  }

  onGridFilterChanged(_event: FilterChangedEvent<Transaction>): void {
    this.columnFiltersActive.set(this.gridApi?.isAnyFilterPresent() ?? false);
  }

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((categories) => {
      this.categories.set(categories);
      this.columnDefs = this.buildColumnDefs();
    });
    this.statementService.getAll({ pageSize: 100 }).subscribe((result) => {
      this.statements.set(result.items);
      this.columnDefs = this.buildColumnDefs();
    });
    this.loadSummary();
    this.loadAll();
  }

  clearFilters(): void {
    this.search.set('');
    this.gridApi?.setFilterModel(null);
  }

  retry(): void {
    this.loadSummary();
    this.loadAll();
  }

  private loadSummary(): void {
    this.transactionService.getSummary().subscribe({ next: (summary) => this.summary.set(summary) });
  }

  /// <summary>Loads every transaction the user owns across as many 100-item pages as needed
  /// (PaginationDefaults.MaxPageSize server-side cap), so AG Grid's own native column filters and
  /// pagination footer can operate over the full dataset client-side instead of round-tripping to
  /// the server per filter change.</summary>
  private loadAll(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    this.transactionService.search({ page: 1, pageSize: LOAD_ALL_PAGE_SIZE }).subscribe({
      next: (first) => {
        const totalPages = Math.ceil(first.totalCount / LOAD_ALL_PAGE_SIZE);
        if (totalPages <= 1) {
          this.allTransactions.set(first.items);
          this.isLoading.set(false);
          return;
        }

        const remainingPages = Array.from({ length: totalPages - 1 }, (_, i) =>
          this.transactionService.search({ page: i + 2, pageSize: LOAD_ALL_PAGE_SIZE })
        );
        forkJoin(remainingPages).subscribe({
          next: (rest) => {
            this.allTransactions.set(first.items.concat(...rest.map((r) => r.items)));
            this.isLoading.set(false);
          },
          error: () => {
            this.isLoading.set(false);
            this.loadError.set(true);
          }
        });
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set(true);
      }
    });
  }
}
