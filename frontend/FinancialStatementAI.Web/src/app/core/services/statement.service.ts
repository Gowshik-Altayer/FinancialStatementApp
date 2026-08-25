import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { StatementDetail, StatementStatus, StatementSummary } from '../../shared/models/statement.model';

@Injectable({ providedIn: 'root' })
export class StatementService {
  constructor(private readonly http: HttpClient) {}

  upload(file: File): Observable<StatementDetail> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<StatementDetail>('/api/statements/upload', formData);
  }

  getAll(): Observable<StatementSummary[]> {
    return this.http.get<StatementSummary[]>('/api/statements');
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
