import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { NotificationService } from '../../core/services/notification.service';
import { TransactionService } from '../../core/services/transaction.service';
import { CategoryService } from '../../core/services/category.service';
import { Transaction } from '../../shared/models/transaction.model';
import { Category } from '../../shared/models/category.model';
import { TransactionTable } from '../../shared/components/transaction-table/transaction-table';
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
    DecimalPipe,
    FormsModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatCheckboxModule,
    TransactionTable,
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

  readonly queue = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly currentIndex = signal(0);
  readonly isLoading = signal(true);
  readonly loadError = signal(false);
  readonly saving = signal(false);

  selectedCategoryName = '';
  applyToAllWithSameMerchant = false;

  readonly current = computed<Transaction | null>(() => {
    const items = this.queue();
    const index = this.currentIndex();
    return index < items.length ? items[index] : null;
  });

  readonly lowConfidenceCount = computed(() => this.queue().filter((t) => t.reviewPriority === 'ReviewRequired').length);
  readonly remainingCount = computed(() => this.queue().length);

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((categories) => this.categories.set(categories));
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

    if (!this.selectedCategoryName || this.selectedCategoryName === transaction.categoryName) {
      this.advance();
      return;
    }

    this.saving.set(true);

    // Bulk applies the same category to every transaction sharing this one's exact merchant name
    // (requirement: individual vs bulk category correction) instead of just this one row.
    if (this.applyToAllWithSameMerchant) {
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

    this.transactionService.correctCategory(transaction.id, this.selectedCategoryName).subscribe({
      next: () => {
        this.saving.set(false);
        this.notifications.success('Category corrected.');
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
    this.selectedCategoryName = this.current()?.categoryName ?? '';
    this.applyToAllWithSameMerchant = false;
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
