import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDialog } from '@angular/material/dialog';
import { CellValueChangedEvent, ColDef, GetRowIdFunc, ICellRendererParams, RowClassRules } from 'ag-grid-community';
import { NotificationService } from '../../../core/services/notification.service';
import { StatementService } from '../../../core/services/statement.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { StatementDetail as StatementDetailModel } from '../../../shared/models/statement.model';
import { Transaction } from '../../../shared/models/transaction.model';
import { Category } from '../../../shared/models/category.model';
import { CategoryService } from '../../../core/services/category.service';
import { DataGrid } from '../../../shared/components/data-grid/data-grid';
import {
  formatTransactionAmount,
  renderCategoryCell,
  renderConfidenceCell,
  renderDescriptionCell,
  renderHistoryActionCell
} from '../../../shared/components/data-grid/transaction-cell-renderers';
import { TransactionHistoryDialog } from '../../../shared/components/transaction-history-dialog/transaction-history-dialog';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { StatusBadge } from '../../../shared/components/status-badge/status-badge';
import { LoadingState } from '../../../shared/components/loading-state/loading-state';
import { PipelineStepper, PipelineStageViewModel } from '../../../shared/components/pipeline-stepper/pipeline-stepper';
import { processingStatusLabel, processingStatusTone, reconciliationStatusTone } from '../../../shared/utils/status-tone.util';

@Component({
  selector: 'app-statement-detail',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    DataGrid,
    PageHeader,
    StatusBadge,
    LoadingState,
    PipelineStepper
  ],
  templateUrl: './statement-detail.html',
  styleUrl: './statement-detail.scss'
})
export class StatementDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly statementService = inject(StatementService);
  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly notifications = inject(NotificationService);
  private readonly dialog = inject(MatDialog);

  readonly statement = signal<StatementDetailModel | null>(null);
  readonly transactions = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly getRowId: GetRowIdFunc<Transaction> = (params) => params.data.id;
  readonly rowClassRules: RowClassRules<Transaction> = {
    'row-duplicate': (params) => !!params.data?.isPotentialDuplicate
  };

  /// <summary>Same column shape as the Transactions page's own grid (see
  /// shared/components/data-grid/transaction-cell-renderers.ts) minus native filters and the
  /// Statement column — every row here already belongs to the one statement this page shows.</summary>
  columnDefs: ColDef<Transaction>[] = this.buildColumnDefs();

  private buildColumnDefs(): ColDef<Transaction>[] {
    return [
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
  readonly isLoading = signal(true);
  readonly isReprocessing = signal(false);
  readonly isVerifying = signal(false);
  readonly notFound = signal(false);

  readonly processingStatusLabel = processingStatusLabel;
  readonly processingStatusTone = processingStatusTone;
  readonly reconciliationStatusTone = reconciliationStatusTone;

  // Mirrors DashboardService.BuildPipelineStages' reached-stage funnel logic (see backend
  // comments there), just scoped to this one statement instead of aggregated across many —
  // count is 1 (reached) or 0 (not yet), so the shared PipelineStepper renders identically here
  // and on the Dashboard.
  readonly pipelineStages = computed<PipelineStageViewModel[]>(() => {
    const s = this.statement();
    if (!s) return [];

    const stage = (key: string, label: string, reached: boolean): PipelineStageViewModel => ({
      key,
      label,
      count: reached ? 1 : 0,
      state: reached ? 'complete' : 'pending'
    });

    return [
      stage('upload', 'Upload', true),
      stage('text-extraction', 'Text Extraction', s.extractionMethod === 'DirectPdfText'),
      stage(
        'ocr',
        'OCR',
        s.extractionMethod !== null && s.extractionMethod !== 'DirectPdfText' && s.extractionMethod !== 'Spreadsheet'
      ),
      stage('transaction-extraction', 'Transaction Extraction', s.transactionCount > 0),
      stage('ai-classification', 'AI Classification', ['ClassificationComplete', 'PendingReview', 'Verified'].includes(s.processingStatus)),
      stage('review', 'Review', ['PendingReview', 'Verified'].includes(s.processingStatus)),
      stage('reconciliation', 'Reconciliation', s.reconciliationStatus !== null),
      stage('completed', 'Completed', s.processingStatus === 'Verified')
    ];
  });

  /** Uploaded but never processed — nothing has been extracted, so every metadata/balance field
   * on this page is necessarily blank. Drives the call-to-action banner. */
  readonly isUnprocessed = computed(() => this.statement()?.processingStatus === 'Uploaded');

  /** Processing ran to completion but yielded no transaction rows. Distinct from the above: the
   * document WAS read, so "not processed yet" would be misleading. */
  readonly processedWithNoTransactions = computed(() => {
    const s = this.statement();
    return !!s && s.processingStatus !== 'Uploaded' && s.transactionCount === 0;
  });

  private statementId = '';

  ngOnInit(): void {
    this.statementId = this.route.snapshot.paramMap.get('id')!;
    this.categoryService.getAll().subscribe((categories) => {
      this.categories.set(categories);
      this.columnDefs = this.buildColumnDefs();
    });
    this.load();
  }

  private load(): void {
    this.statementService.getById(this.statementId).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.isLoading.set(false);
        this.loadTransactions();
      },
      error: () => {
        this.notFound.set(true);
        this.isLoading.set(false);
      }
    });
  }

  private loadTransactions(): void {
    this.transactionService.getForStatement(this.statementId).subscribe((transactions) => this.transactions.set(transactions));
  }

  reprocess(): void {
    this.isReprocessing.set(true);
    this.statementService.reprocess(this.statementId).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.isReprocessing.set(false);
        this.loadTransactions();
        this.notifications.success('Statement reprocessed.');
      },
      error: () => {
        this.isReprocessing.set(false);
        this.notifications.error('Reprocessing failed.');
      }
    });
  }

  verify(): void {
    this.isVerifying.set(true);
    this.statementService.verify(this.statementId).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.isVerifying.set(false);
        this.notifications.success('Statement marked as verified.');
      },
      error: () => {
        this.isVerifying.set(false);
        this.notifications.error('Verification failed — statement must be in PendingReview.');
      }
    });
  }
}
