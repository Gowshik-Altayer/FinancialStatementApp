import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StatementDetail, StatementStatus, StatementSummary } from '../../shared/models/statement.model';
import { StatementQuery } from '../../shared/models/statement-query.model';
import { PagedResult } from '../../shared/models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class StatementService {
  constructor(private readonly http: HttpClient) {}

  upload(file: File): Observable<StatementDetail> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<StatementDetail>('/api/statements/upload', formData);
  }

  getAll(query: StatementQuery = {}): Observable<PagedResult<StatementSummary>> {
    let params = new HttpParams().set('page', String(query.page ?? 1)).set('pageSize', String(query.pageSize ?? 20));
    if (query.search) params = params.set('search', query.search);
    if (query.status) params = params.set('status', query.status);
    if (query.reconciliationStatus) params = params.set('reconciliationStatus', query.reconciliationStatus);

    return this.http.get<PagedResult<StatementSummary>>('/api/statements', { params });
  }

  getById(id: string): Observable<StatementDetail> {
    return this.http.get<StatementDetail>(`/api/statements/${id}`);
  }

  getStatus(id: string): Observable<StatementStatus> {
    return this.http.get<StatementStatus>(`/api/statements/${id}/status`);
  }

  reprocess(id: string): Observable<StatementDetail> {
    return this.http.post<StatementDetail>(`/api/statements/${id}/reprocess`, null);
  }

  verify(id: string): Observable<StatementDetail> {
    return this.http.post<StatementDetail>(`/api/statements/${id}/verify`, null);
  }
}
