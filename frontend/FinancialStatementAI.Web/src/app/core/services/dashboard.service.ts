import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardSummary } from '../../shared/models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  constructor(private readonly http: HttpClient) {}

  getSummary(rangeDays = 30): Observable<DashboardSummary> {
    const params = new HttpParams().set('rangeDays', String(rangeDays));
    return this.http.get<DashboardSummary>('/api/dashboard/summary', { params });
  }
}
