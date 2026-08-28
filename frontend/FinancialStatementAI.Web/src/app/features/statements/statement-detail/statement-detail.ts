import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { NotificationService } from '../../../core/services/notification.service';
import { StatementService } from '../../../core/services/statement.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { StatementDetail as StatementDetailModel } from '../../../shared/models/statement.model';
import { Transaction } from '../../../shared/models/transaction.model';
import { TransactionTable } from '../../../shared/components/transaction-table/transaction-table';
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
    TransactionTable,
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
  private readonly notifications = inject(NotificationService);

  readonly statement = signal<StatementDetailModel | null>(null);
  readonly transactions = signal<Transaction[]>([]);
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
      stage('ocr', 'OCR', s.extractionMethod !== null && s.extractionMethod !== 'DirectPdfText'),
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
