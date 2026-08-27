import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ReconciliationQuery, ReconciliationSummary, ReconciliationSummaryCounts } from '../../shared/models/reconciliation.model';
import { PagedResult } from '../../shared/models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class ReconciliationService {
  constructor(private readonly http: HttpClient) {}

  getAll(query: ReconciliationQuery = {}): Observable<PagedResult<ReconciliationSummary>> {
    let params = new HttpParams().set('page', String(query.page ?? 1)).set('pageSize', String(query.pageSize ?? 20));
    if (query.status) params = params.set('status', query.status);
    if (query.search) params = params.set('search', query.search);

    return this.http.get<PagedResult<ReconciliationSummary>>('/api/reconciliation', { params });
  }

  getSummary(): Observable<ReconciliationSummaryCounts> {
    return this.http.get<ReconciliationSummaryCounts>('/api/reconciliation/summary');
  }
}
