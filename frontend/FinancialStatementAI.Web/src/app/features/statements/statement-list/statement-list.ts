import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { ColDef, FilterChangedEvent, GetRowIdFunc, GridApi, IDateFilterParams, ICellRendererParams } from 'ag-grid-community';
import { StatementService } from '../../../core/services/statement.service';
import { StatementSummary } from '../../../shared/models/statement.model';
import { DataGrid } from '../../../shared/components/data-grid/data-grid';
import { SelectFilter, SelectFilterOption, SelectFloatingFilter } from '../../../shared/components/data-grid/select-filter';
import { renderStatusBadgeCell } from '../../../shared/components/data-grid/status-badge-cell-renderer';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { Skeleton } from '../../../shared/components/skeleton/skeleton';
import { ErrorState } from '../../../shared/components/error-state/error-state';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { processingStatusLabel, processingStatusTone, reconciliationStatusTone } from '../../../shared/utils/status-tone.util';

const STATUS_OPTIONS = ['Uploaded', 'Processing', 'ExtractionFailed', 'ExtractionComplete', 'ClassificationComplete', 'PendingReview', 'Verified'];
const RECONCILIATION_OPTIONS = ['Reconciled', 'Mismatch', 'InsufficientInformation'];

/// <summary>Server hard-caps a single page at 100 rows (PaginationDefaults.MaxPageSize) — loading
/// "all" statements for client-side filtering means looping pages at that cap, same pattern as
/// the Transactions page's own loadAll().</summary>
const LOAD_ALL_PAGE_SIZE = 100;

function renderFileLinkCell(params: ICellRendererParams<StatementSummary>, router: Router): HTMLElement {
  const statement = params.data!;
  const link = document.createElement('a');
  link.textContent = statement.originalFileName;
  link.href = `/statements/${statement.id}`;
  link.addEventListener('click', (event) => {
    event.preventDefault();
    router.navigate(['/statements', statement.id]);
  });
  return link;
}

@Component({
  selector: 'app-statement-list',
  standalone: true,
  imports: [RouterLink, FormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, DataGrid, PageHeader, Skeleton, ErrorState, EmptyState],
  templateUrl: './statement-list.html',
  styleUrl: './statement-list.scss'
})
export class StatementList implements OnInit {
  private readonly statementService = inject(StatementService);
  private readonly router = inject(Router);

  readonly allStatements = signal<StatementSummary[]>([]);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);

  readonly search = signal('');
  private readonly columnFiltersActive = signal(false);
  readonly hasActiveFilters = computed(() => !!this.search().trim() || this.columnFiltersActive());

  // Mirrors Transactions' own search-box-plus-native-filters split: free text pre-filters the row
  // set here in JS, AG Grid's own per-column filters (see buildColumnDefs) run on top of that.
  readonly statements = computed(() => {
    const query = this.search().trim().toLowerCase();
    if (!query) return this.allStatements();
    return this.allStatements().filter(
      (s) =>
        s.originalFileName.toLowerCase().includes(query) ||
        (s.providerName ?? '').toLowerCase().includes(query) ||
        (s.accountHolderName ?? '').toLowerCase().includes(query) ||
        (s.accountNumberMasked ?? '').toLowerCase().includes(query)
    );
  });

  readonly getRowId: GetRowIdFunc<StatementSummary> = (params) => params.data.id;
  private gridApi?: GridApi<StatementSummary>;

  readonly columnDefs: ColDef<StatementSummary>[] = this.buildColumnDefs();

  private buildColumnDefs(): ColDef<StatementSummary>[] {
    const statusOptions: SelectFilterOption[] = STATUS_OPTIONS.map((s) => ({ value: s, label: processingStatusLabel(s) }));
    const reconciliationOptions: SelectFilterOption[] = RECONCILIATION_OPTIONS.map((r) => ({ value: r, label: processingStatusLabel(r) }));

    return [
      {
        headerName: 'File',
        field: 'originalFileName',
        cellRenderer: (p: ICellRendererParams<StatementSummary>) => renderFileLinkCell(p, this.router),
        filter: 'agTextColumnFilter',
        flex: 1.3,
        minWidth: 180
      },
      { headerName: 'Provider', field: 'providerName', valueFormatter: (p) => p.value ?? '—', filter: 'agTextColumnFilter', width: 150 },
      {
        headerName: 'Account',
        field: 'accountHolderName',
        valueGetter: (p) => p.data?.accountHolderName ?? p.data?.accountNumberMasked ?? '',
        valueFormatter: (p) => p.value || '—',
        filter: 'agTextColumnFilter',
        width: 160
      },
      {
        headerName: 'Statement Period',
        colId: 'statementPeriod',
        valueGetter: (p) => (p.data?.statementPeriodStart && p.data?.statementPeriodEnd ? `${p.data.statementPeriodStart} – ${p.data.statementPeriodEnd}` : ''),
        valueFormatter: (p) => p.value || '—',
        filter: false,
        width: 190
      },
      { headerName: 'Transactions', field: 'transactionCount', type: 'rightAligned', filter: 'agNumberColumnFilter', width: 140 },
      {
        headerName: 'Total Debits',
        field: 'totalDebits',
        type: 'rightAligned',
        valueFormatter: (p) => (p.value != null ? Number(p.value).toFixed(2) : '—'),
        filter: 'agNumberColumnFilter',
        width: 140
      },
      {
        headerName: 'Total Credits',
        field: 'totalCredits',
        type: 'rightAligned',
        valueFormatter: (p) => (p.value != null ? Number(p.value).toFixed(2) : '—'),
        filter: 'agNumberColumnFilter',
        width: 140
      },
      {
        headerName: 'Status',
        field: 'processingStatus',
        cellRenderer: (p: ICellRendererParams<StatementSummary>) => renderStatusBadgeCell(processingStatusLabel(p.value), processingStatusTone(p.value)),
        filter: SelectFilter,
        floatingFilterComponent: SelectFloatingFilter,
        filterParams: { options: statusOptions },
        floatingFilterComponentParams: { options: statusOptions },
        width: 160
      },
      {
        headerName: 'Reconciliation',
        field: 'reconciliationStatus',
        cellRenderer: (p: ICellRendererParams<StatementSummary>) => renderStatusBadgeCell(p.value ? processingStatusLabel(p.value) : null, p.value ? reconciliationStatusTone(p.value) : null),
        filter: SelectFilter,
        floatingFilterComponent: SelectFloatingFilter,
        filterParams: { options: reconciliationOptions },
        floatingFilterComponentParams: { options: reconciliationOptions },
        width: 170
      },
      {
        headerName: 'Uploaded',
        field: 'uploadedAt',
        valueFormatter: (p) => (p.value ? new Date(p.value).toLocaleString() : '—'),
        filter: 'agDateColumnFilter',
        filterParams: {
          comparator: (filterLocalDateAtMidnight: Date, cellValue: string | null) => {
            if (!cellValue) return -1;
            const cellDate = new Date(cellValue);
            cellDate.setHours(0, 0, 0, 0);
            if (cellDate < filterLocalDateAtMidnight) return -1;
            if (cellDate > filterLocalDateAtMidnight) return 1;
            return 0;
          }
        } satisfies IDateFilterParams,
        width: 170
      }
    ];
  }

  onGridApiReady(api: GridApi<StatementSummary>): void {
    this.gridApi = api;
  }

  onGridFilterChanged(_event: FilterChangedEvent<StatementSummary>): void {
    this.columnFiltersActive.set(this.gridApi?.isAnyFilterPresent() ?? false);
  }

  clearFilters(): void {
    this.search.set('');
    this.gridApi?.setFilterModel(null);
  }

  ngOnInit(): void {
    this.loadAll();
  }

  retry(): void {
    this.loadAll();
  }

  /// <summary>Loads every statement the user owns across as many 100-item pages as needed
  /// (PaginationDefaults.MaxPageSize server-side cap) — same load-all pattern as the Transactions
  /// page, so AG Grid's own native column filters and pagination footer operate over the full
  /// dataset client-side.</summary>
  private loadAll(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    this.statementService.getAll({ page: 1, pageSize: LOAD_ALL_PAGE_SIZE }).subscribe({
      next: (first) => {
        const totalPages = Math.ceil(first.totalCount / LOAD_ALL_PAGE_SIZE);
        if (totalPages <= 1) {
          this.allStatements.set(first.items);
          this.isLoading.set(false);
          return;
        }

        const remainingPages = Array.from({ length: totalPages - 1 }, (_, i) => this.statementService.getAll({ page: i + 2, pageSize: LOAD_ALL_PAGE_SIZE }));
        forkJoin(remainingPages).subscribe({
          next: (rest) => {
            this.allStatements.set(first.items.concat(...rest.map((r) => r.items)));
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
