import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { TransactionService } from '../../core/services/transaction.service';
import { CategoryService } from '../../core/services/category.service';
import { Transaction } from '../../shared/models/transaction.model';
import { Category } from '../../shared/models/category.model';
import { TransactionTable } from '../../shared/components/transaction-table/transaction-table';

/// <summary>Search/filter/paginate across every transaction the user owns, regardless of its
/// statement's processing status (Phase 13) — as opposed to the single-statement list or the
/// PendingReview-only review queue.</summary>
@Component({
  selector: 'app-transactions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatPaginatorModule,
    TransactionTable
  ],
  templateUrl: './transactions.html',
  styleUrl: './transactions.scss'
})
export class Transactions implements OnInit {
  private readonly transactionService = inject(TransactionService);
  private readonly categoryService = inject(CategoryService);
  private readonly searchChanged = new Subject<void>();

  readonly transactions = signal<Transaction[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly totalCount = signal(0);
  readonly isLoading = signal(true);

  search = '';
  categoryId = '';
  pageIndex = 0;
  pageSize = 20;

  ngOnInit(): void {
    this.categoryService.getAll().subscribe((categories) => this.categories.set(categories));

    this.searchChanged.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => {
      this.pageIndex = 0;
      this.load();
    });
    this.load();
  }

  onSearchInput(): void {
    this.searchChanged.next();
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
    this.transactionService
      .search({
        search: this.search || undefined,
        categoryId: this.categoryId || undefined,
        page: this.pageIndex + 1,
        pageSize: this.pageSize
      })
      .subscribe({
        next: (result) => {
          this.transactions.set(result.items);
          this.totalCount.set(result.totalCount);
          this.isLoading.set(false);
        },
        error: () => this.isLoading.set(false)
      });
  }
}
