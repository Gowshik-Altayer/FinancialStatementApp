import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { MatChipsModule } from '@angular/material/chips';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { TransactionService } from '../../../core/services/transaction.service';
import { CategoryService } from '../../../core/services/category.service';
import { Transaction } from '../../models/transaction.model';
import { Category } from '../../models/category.model';

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
    MatChipsModule,
    MatSelectModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './transaction-table.html',
  styleUrl: './transaction-table.scss'
})
export class TransactionTable implements OnInit {
  @Input({ required: true }) transactions: Transaction[] = [];
  @Input() showStatementColumn = false;

  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly snackBar = inject(MatSnackBar);

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
        this.snackBar.open('Category corrected.', 'Dismiss', { duration: 2500 });
      },
      error: () => {
        this.savingId.set(null);
        this.snackBar.open('Correction failed.', 'Dismiss', { duration: 3000 });
      }
    });
  }

  priorityLabel(transaction: Transaction): string {
    switch (transaction.reviewPriority) {
      case 'HighConfidence':
        return 'High confidence';
      case 'ReviewRecommended':
        return 'Review recommended';
      case 'ReviewRequired':
        return 'Review required';
      default:
        return 'Unclassified';
    }
  }
}
