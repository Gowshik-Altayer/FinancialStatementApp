import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { DatePipe, PercentPipe } from '@angular/common';
import { RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDialog } from '@angular/material/dialog';
import { CellValueChangedEvent, ColDef, GetRowIdFunc, ICellRendererParams, RowClassRules } from 'ag-grid-community';
import { NotificationService } from '../../core/services/notification.service';
import { TransactionService } from '../../core/services/transaction.service';
import { CategoryService } from '../../core/services/category.service';
import { Transaction } from '../../shared/models/transaction.model';
import { Category } from '../../shared/models/category.model';
import { DataGrid } from '../../shared/components/data-grid/data-grid';
import {
  formatTransactionAmount,
  renderCategoryCell,
  renderConfidenceCell,
  renderDescriptionCell,
  renderHistoryActionCell,
  renderStatementLinkCell
} from '../../shared/components/data-grid/transaction-cell-renderers';
import { TransactionHistoryDialog } from '../../shared/components/transaction-history-dialog/transaction-history-dialog';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { KpiCard } from '../../shared/components/kpi-card/kpi-card';
import { ConfidenceIndicator } from '../../shared/components/confidence-indicator/confidence-indicator';
import { LoadingState } from '../../shared/components/loading-state/loading-state';
import { ErrorState } from '../../shared/components/error-state/error-state';
import { EmptyState } from '../../shared/components/empty-state/empty-state';

/// <summary>Cross-statement human review queue (Phase 12, redesigned per requirement 8): every
/// transaction still awaiting review, lowest classification confidence first, worked one at a
/// time — accept the AI's suggestion, change the category, or skip — with the full remaining
/// queue visible below for overview/bulk correction. "Accept" is never an API call: the AI's
/// classification already stands unless a human corrects it, so accepting just means leaving it
/// alone and moving on (the same "implicitly accepted" semantic the Dashboard's AiAcceptedCount
/// KPI uses — see DashboardService.BuildReviewStatistics).</summary>
@Component({
  selector: 'app-review',
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    PercentPipe,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    DataGrid,
    PageHeader,
    KpiCard,
    ConfidenceIndicator,
    LoadingState,
    ErrorState,
    EmptyState
  ],
  templateUrl: './review.html',
  styleUrl: './review.scss'
})
export class Review implements OnInit {
  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly notifications = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);

  readonly queue = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly getRowId: GetRowIdFunc<Transaction> = (params) => params.data.id;
  readonly rowClassRules: RowClassRules<Transaction> = {
    'row-duplicate': (params) => !!params.data?.isPotentialDuplicate
  };

  /// <summary>Same column shape as the Transactions page's own grid (see
  /// shared/components/data-grid/transaction-cell-renderers.ts) minus native filters — the review
  /// queue is a short, already-fully-loaded list with no need to filter it, and always includes
  /// the Statement column since every row here spans a different statement.</summary>
  columnDefs: ColDef<Transaction>[] = this.buildColumnDefs();

  private buildColumnDefs(): ColDef<Transaction>[] {
    return [
      { headerName: 'Statement', field: 'statementFileName', cellRenderer: (p: ICellRendererParams<Transaction>) => renderStatementLinkCell(p, this.router), flex: 1.2, minWidth: 160 },
      { headerName: 'Date', field: 'transactionDate', valueFormatter: (p) => p.value ?? '—', width: 120 },
      { headerName: 'Description', field: 'description', cellRenderer: renderDescriptionCell, flex: 2, minWidth: 220 },
      { headerName: 'Amount', field: 'amount', width: 130, type: 'rightAligned', valueFormatter: (p) => formatTransactionAmount(p.value) },
      {
        headerName: 'Category',
        field: 'categoryName',
        cellRenderer: renderCategoryCell,
        editable: true,
        cellEditor: 'agSelectCellEditor',
        cellEditorParams: { values: this.categories().map((c) => c.name) },
        width: 170
      },
      { headerName: 'Confidence', field: 'reviewPriority', cellRenderer: renderConfidenceCell, width: 150, sortable: false },
      {
        headerName: '',
        cellRenderer: (p: ICellRendererParams<Transaction>) => renderHistoryActionCell(p, this.dialog, TransactionHistoryDialog),
        width: 64,
        sortable: false,
        resizable: false
      }
    ];
  }

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
  readonly currentIndex = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);
  readonly saving = signal(false);

  selectedCategoryName = '';
  applyToAllWithSameMerchant = false;

  // Editable copies of the current transaction's other correctable fields (requirement #9 — date,
  // description, merchant, amount, and debit/credit type, alongside category). Kept as plain
  // component fields bound via ngModel rather than a reactive form, matching selectedCategoryName's
  // existing pattern on this page.
  editDate = '';
  editDescription = '';
  editMerchant = '';
  editAmount: number | null = null;
  editTransactionType = '';

  readonly transactionTypeOptions = ['Debit', 'Credit', 'Payment', 'Refund', 'Purchase', 'Transfer', 'Other'];

  readonly current = computed<Transaction | null>(() => {
    const items = this.queue();
    const index = this.currentIndex();
    return index < items.length ? items[index] : null;
  });

  readonly lowConfidenceCount = computed(() => this.queue().filter((t) => t.reviewPriority === 'ReviewRequired').length);
  readonly remainingCount = computed(() => this.queue().length);

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((categories) => {
      this.categories.set(categories);
      this.columnDefs = this.buildColumnDefs();
    });
    this.load();
  }

  retry(): void {
    this.load();
  }

  acceptSuggestion(): void {
    this.advance();
  }

  skip(): void {
    const items = this.queue();
    const index = this.currentIndex();
    if (index < items.length - 1) {
      this.currentIndex.set(index + 1);
    } else {
      this.currentIndex.set(0);
    }
    this.resetSelectionForCurrent();
  }

  saveAndNext(): void {
    const transaction = this.current();
    if (!transaction) return;

    // Bulk applies the same category to every transaction sharing this one's exact merchant name
    // (requirement: individual vs bulk category correction) — scoped to category only, since
    // "apply this category to every X transaction" is the only one of the correctable fields that
    // makes sense as a batch operation across unrelated rows.
    if (this.applyToAllWithSameMerchant) {
      if (!this.selectedCategoryName || this.selectedCategoryName === transaction.categoryName) {
        this.advance();
        return;
      }

      this.saving.set(true);
      this.transactionService.bulkCorrectCategory(transaction.id, this.selectedCategoryName).subscribe({
        next: ({ updatedCount }) => {
          this.saving.set(false);
          this.notifications.success(
            `Category corrected for ${updatedCount} transaction${updatedCount === 1 ? '' : 's'} from "${transaction.merchant}".`
          );
          this.advanceRemovingMerchant(transaction.merchant);
        },
        error: () => {
          this.saving.set(false);
          this.notifications.error('Bulk correction failed — please try again.');
        }
      });
      return;
    }

    const fields: Parameters<TransactionService['correctTransaction']>[1] = {};
    if (this.selectedCategoryName && this.selectedCategoryName !== transaction.categoryName) {
      fields.categoryName = this.selectedCategoryName;
    }
    if (this.editDate && this.editDate !== (transaction.transactionDate ?? '')) {
      fields.transactionDate = this.editDate;
    }
    if (this.editDescription && this.editDescription !== transaction.description) {
      fields.description = this.editDescription;
    }
    if (this.editMerchant && this.editMerchant !== (transaction.merchant ?? '')) {
      fields.merchant = this.editMerchant;
    }
    if (this.editAmount !== null && this.editAmount !== transaction.amount) {
      fields.amount = this.editAmount;
    }
    if (this.editTransactionType && this.editTransactionType !== transaction.transactionType) {
      fields.transactionType = this.editTransactionType;
    }

    if (Object.keys(fields).length === 0) {
      this.advance();
      return;
    }

    this.saving.set(true);
    this.transactionService.correctTransaction(transaction.id, fields).subscribe({
      next: () => {
        this.saving.set(false);
        this.notifications.success('Transaction corrected.');
        this.advance();
      },
      error: () => {
        this.saving.set(false);
        this.notifications.error('Correction failed — please try again.');
      }
    });
  }

  private advance(): void {
    // Reviewed items drop out of the queue entirely (accepted or corrected) so the pending
    // count reflects real remaining work, not just where the reviewer currently is in the list.
    const items = [...this.queue()];
    items.splice(this.currentIndex(), 1);
    this.queue.set(items);
    this.resetSelectionForCurrent();
  }

  /** advance()'s bulk-correction counterpart: every queued item sharing the corrected merchant
   * was just updated server-side too, so all of them drop out of the queue at once, not just the
   * one the reviewer was looking at. */
  private advanceRemovingMerchant(merchant: string | null): void {
    const items = this.queue().filter((t) => t.merchant !== merchant);
    this.queue.set(items);
    if (this.currentIndex() >= items.length) {
      this.currentIndex.set(0);
    }
    this.resetSelectionForCurrent();
  }

  private resetSelectionForCurrent(): void {
    const transaction = this.current();
    this.selectedCategoryName = transaction?.categoryName ?? '';
    this.applyToAllWithSameMerchant = false;
    this.editDate = transaction?.transactionDate ?? '';
    this.editDescription = transaction?.description ?? '';
    this.editMerchant = transaction?.merchant ?? '';
    this.editAmount = transaction?.amount ?? null;
    this.editTransactionType = transaction?.transactionType ?? '';
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadError.set(false);

    this.transactionService.getReviewQueue().subscribe({
      next: (transactions) => {
        this.queue.set(transactions);
        this.currentIndex.set(0);
        this.resetSelectionForCurrent();
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadError.set(true);
      }
    });
  }
}
