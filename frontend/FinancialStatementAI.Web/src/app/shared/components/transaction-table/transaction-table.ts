import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { NotificationService } from '../../../core/services/notification.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { CategoryService } from '../../../core/services/category.service';
import { Transaction } from '../../models/transaction.model';
import { Category } from '../../models/category.model';
import { StatusBadge } from '../status-badge/status-badge';
import { reviewPriorityLabel, reviewPriorityTone } from '../../utils/status-tone.util';

/// <summary>Human review grid for transactions (Phase 12) — reused by both the statement detail
/// page (one statement's transactions) and the cross-statement review queue. Correcting a
/// category calls the API directly and updates the row in place, so callers don't need to
/// re-fetch their whole list after every edit.</summary>
@Component({
  selector: 'app-transaction-table',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    FormsModule,
    MatSelectModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    StatusBadge
  ],
  templateUrl: './transaction-table.html',
  styleUrl: './transaction-table.scss'
})
export class TransactionTable implements OnInit {
  @Input({ required: true }) transactions: Transaction[] = [];
  @Input() showStatementColumn = false;

  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly notifications = inject(NotificationService);

  readonly categories = signal<Category[]>([]);
  readonly editingId = signal<string | null>(null);
  readonly savingId = signal<string | null>(null);
  readonly expandedId = signal<string | null>(null);
  selectedCategoryName = '';

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((categories) => this.categories.set(categories));
  }

  startEdit(transaction: Transaction): void {
    this.editingId.set(transaction.id);
    this.selectedCategoryName = transaction.categoryName ?? '';
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  toggleHistory(transaction: Transaction): void {
    this.expandedId.set(this.expandedId() === transaction.id ? null : transaction.id);
  }

  save(transaction: Transaction): void {
    if (!this.selectedCategoryName || this.selectedCategoryName === transaction.categoryName) {
      this.editingId.set(null);
      return;
    }

    this.savingId.set(transaction.id);
    this.transactionService.correctCategory(transaction.id, this.selectedCategoryName).subscribe({
      next: (updated) => {
        Object.assign(transaction, updated);
        this.savingId.set(null);
        this.editingId.set(null);
        this.notifications.success('Category corrected.');
      },
      error: () => {
        this.savingId.set(null);
        this.notifications.error('Correction failed.');
      }
    });
  }

  // Delegates to the shared status-tone util rather than repeating the switch — this component
  // previously carried its own copy, so a wording change here silently disagreed with every other
  // page. Kept as a method (not inlined in the template) because it's part of the public API the
  // component's spec asserts against.
  priorityLabel(transaction: Transaction): string {
    return reviewPriorityLabel(transaction.reviewPriority);
  }

  readonly reviewPriorityTone = reviewPriorityTone;
}
