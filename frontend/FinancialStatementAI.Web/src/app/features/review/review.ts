import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatCardModule } from '@angular/material/card';
import { TransactionService } from '../../core/services/transaction.service';
import { Transaction } from '../../shared/models/transaction.model';
import { TransactionTable } from '../../shared/components/transaction-table/transaction-table';

/// <summary>Cross-statement human review queue (Phase 12): every transaction still awaiting
/// review, across all of the user's PendingReview statements, lowest classification confidence
/// first — so the transactions most likely to need a correction surface at the top.</summary>
@Component({
  selector: 'app-review',
  standalone: true,
  imports: [CommonModule, MatProgressSpinnerModule, MatCardModule, TransactionTable],
  templateUrl: './review.html',
  styleUrl: './review.scss'
})
export class Review implements OnInit {
  private readonly transactionService = inject(TransactionService);

  readonly transactions = signal<Transaction[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.transactionService.getReviewQueue().subscribe({
      next: (transactions) => {
        this.transactions.set(transactions);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }
}
