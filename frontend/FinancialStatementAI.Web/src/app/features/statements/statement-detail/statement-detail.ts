import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { MatSnackBar } from '@angular/material/snack-bar';
import { StatementService } from '../../../core/services/statement.service';
import { TransactionService } from '../../../core/services/transaction.service';
import { StatementDetail as StatementDetailModel } from '../../../shared/models/statement.model';
import { Transaction } from '../../../shared/models/transaction.model';
import { TransactionTable } from '../../../shared/components/transaction-table/transaction-table';

@Component({
  selector: 'app-statement-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, MatCardModule, MatChipsModule, MatProgressSpinnerModule, MatButtonModule, TransactionTable],
  templateUrl: './statement-detail.html',
  styleUrl: './statement-detail.scss'
})
export class StatementDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly statementService = inject(StatementService);
  private readonly transactionService = inject(TransactionService);
  private readonly snackBar = inject(MatSnackBar);

  readonly statement = signal<StatementDetailModel | null>(null);
  readonly transactions = signal<Transaction[]>([]);
  readonly isLoading = signal(true);
  readonly isReprocessing = signal(false);
  readonly isVerifying = signal(false);
  readonly notFound = signal(false);

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
        this.snackBar.open('Statement reprocessed.', 'Dismiss', { duration: 3000 });
      },
      error: () => {
        this.isReprocessing.set(false);
        this.snackBar.open('Reprocessing failed.', 'Dismiss', { duration: 3000 });
      }
    });
  }

  verify(): void {
    this.isVerifying.set(true);
    this.statementService.verify(this.statementId).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.isVerifying.set(false);
        this.snackBar.open('Statement marked as verified.', 'Dismiss', { duration: 3000 });
      },
      error: () => {
        this.isVerifying.set(false);
        this.snackBar.open('Verification failed — statement must be in PendingReview.', 'Dismiss', { duration: 4000 });
      }
    });
  }
}
