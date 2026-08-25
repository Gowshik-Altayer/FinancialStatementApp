import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { Transaction } from '../../shared/models/transaction.model';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  constructor(private readonly http: HttpClient) {}

  getForStatement(statementId: string): Observable<Transaction[]> {
    return this.http.get<Transaction[]>(`/api/statements/${statementId}/transactions`);
  }

  getReviewQueue(): Observable<Transaction[]> {
    return this.http.get<Transaction[]>('/api/transactions/review-queue');
  }

  correctCategory(transactionId: string, categoryName: string, reason?: string): Observable<Transaction> {
    return this.http.post<Transaction>(`/api/transactions/${transactionId}/corrections`, { categoryName, reason });
  }
}
