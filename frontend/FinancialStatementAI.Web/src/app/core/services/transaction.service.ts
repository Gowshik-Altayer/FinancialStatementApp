import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Transaction } from '../../shared/models/transaction.model';
import { TransactionQuery } from '../../shared/models/transaction-query.model';
import { PagedResult } from '../../shared/models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  constructor(private readonly http: HttpClient) {}

  getForStatement(statementId: string): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`/api/statements/${statementId}/transactions`);
  }

  getReviewQueue(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>('/api/transactions/review-queue');
  }

  search(query: TransactionQuery = {}): Observable<PagedResult<Transaction>> {
    let params = new HttpParams().set('page', String(query.page ?? 1)).set('pageSize', String(query.pageSize ?? 20));
    if (query.search) params = params.set('search', query.search);
    if (query.categoryId) params = params.set('categoryId', query.categoryId);
    if (query.statementId) params = params.set('statementId', query.statementId);

    return this.http.get<PagedResult<Transaction>>('/api/transactions', { params });
  }

  correctCategory(transactionId: string, categoryName: string, reason?: string): Observable<Transaction> {
    return this.http.post<Transaction>(`/api/transactions/${transactionId}/corrections`, { categoryName, reason });
  }
}
