import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { StatementService } from '../../../core/services/statement.service';
import { StatementSummary } from '../../../shared/models/statement.model';

@Component({
  selector: 'app-statement-list',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatTableModule,
    MatSortModule,
    MatButtonModule,
    MatChipsModule,
    MatProgressSpinnerModule
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
  readonly statements = signal<StatementSummary[]>([]);
  readonly isLoading = signal(true);

  ngOnInit(): void {
    this.statementService.getAll().subscribe({
      next: (statements) => {
        this.statements.set(statements);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false)
    });
  }

  sortData(sort: Sort): void {
    const data = [...this.statements()];
    if (!sort.active || sort.direction === '') {
      this.statements.set(data);
      return;
    }

    const direction = sort.direction === 'asc' ? 1 : -1;
    data.sort((a, b) => {
      const valueA = (a as unknown as Record<string, unknown>)[sort.active];
      const valueB = (b as unknown as Record<string, unknown>)[sort.active];
      if (valueA == null || valueB == null) return 0;
      return valueA > valueB ? direction : valueA < valueB ? -direction : 0;
    });
    this.statements.set(data);
  }
}
