import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DashboardWidgetPreference, WidgetPreferenceItem } from '../../shared/models/dashboard.model';

@Injectable({ providedIn: 'root' })
export class DashboardConfigService {
  constructor(private readonly http: HttpClient) {}

  getMyConfig(): Observable<DashboardWidgetPreference[]> {
    return this.http.get<DashboardWidgetPreference[]>('/api/dashboard/config');
  }

  updateMyConfig(items: WidgetPreferenceItem[]): Observable<void> {
    return this.http.put<void>('/api/dashboard/config', { items });
  }
}
