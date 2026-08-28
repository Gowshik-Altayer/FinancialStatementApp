import { Injectable } from '@angular/core';
import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ReportArea, ReportFileFormat } from '../../shared/models/report.model';

@Injectable({ providedIn: 'root' })
export class ReportService {
  constructor(private readonly http: HttpClient) {}

  /** The full response (not just the body) so the caller can read the server-assigned
   * Content-Disposition filename rather than inventing its own. */
  download(area: ReportArea, format: ReportFileFormat): Observable<HttpResponse<Blob>> {
    const params = new HttpParams().set('format', format);
    return this.http.get(`/api/reports/${area}`, { params, responseType: 'blob', observe: 'response' });
  }
}
