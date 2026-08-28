import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { StatementService } from '../../../core/services/statement.service';
import { StatementSummary } from '../../../shared/models/statement.model';
import { PageHeader } from '../../../shared/components/page-header/page-header';
import { FilterPanel } from '../../../shared/components/filter-panel/filter-panel';
import { StatusBadge } from '../../../shared/components/status-badge/status-badge';
import { Skeleton } from '../../../shared/components/skeleton/skeleton';
import { EmptyState } from '../../../shared/components/empty-state/empty-state';
import { processingStatusLabel, processingStatusTone, reconciliationStatusTone } from '../../../shared/utils/status-tone.util';

const STATUS_OPTIONS = ['Uploaded', 'Processing', 'ExtractionFailed', 'ExtractionComplete', 'ClassificationComplete', 'PendingReview', 'Verified'];
const RECONCILIATION_OPTIONS = ['Reconciled', 'Mismatch', 'InsufficientInformation'];

@Component({
  selector: 'app-statement-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatPaginatorModule,
    PageHeader,
    FilterPanel,
    StatusBadge,
    Skeleton,
    EmptyState
  ],
  templateUrl: './statement-list.html',
  styleUrl: './statement-list.scss'
})
export class StatementList implements OnInit {
  private readonly statementService = inject(StatementService);

  readonly displayedColumns = [
    'originalFileName',
    'providerName',
    'transactionCount',
    'totalDebits',
    'totalCredits',
    'processingStatus',
    'reconciliationStatus',
    'uploadedAt'
  ];
  readonly statusOptions = STATUS_OPTIONS;
  readonly reconciliationOptions = RECONCILIATION_OPTIONS;

  readonly statements = signal<StatementSummary[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);

  search = '';
  status = '';
  reconciliationStatus = '';
  pageIndex = 0;
  pageSize = 20;

  readonly processingStatusTone = processingStatusTone;
  readonly processingStatusLabel = processingStatusLabel;
  readonly reconciliationStatusTone = reconciliationStatusTone;

  get hasActiveFilters(): boolean {
    return !!this.status || !!this.reconciliationStatus;
  }

  ngOnInit(): void {
    this.load();
  }

  onSearchValueChange(value: string): void {
    this.search = value;
    this.pageIndex = 0;
    this.load();
  }

  clearFilters(): void {
    this.status = '';
    this.reconciliationStatus = '';
    this.pageIndex = 0;
    this.load();
  }

  onFilterChange(): void {
    this.pageIndex = 0;
    this.load();
  }

  onPage(event: PageEvent): void {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.load();
  }

  private load(): void {
    this.isLoading.set(true);
    this.statementService
      .getAll({
        search: this.search || undefined,
        status: this.status || undefined,
        reconciliationStatus: this.reconciliationStatus || undefined,
        page: this.pageIndex + 1,
        pageSize: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.statements.set(result.items);
          this.totalCount.set(result.totalCount);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      });
  }
}
