import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { ReportService } from './report.service';

describe('ReportService', () => {
  let service: ReportService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(ReportService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('download() requests the area as a blob with the chosen format', () => {
    const blob = new Blob(['x'], { type: 'application/pdf' });

    service.download('transactions', 'pdf').subscribe((response) => expect(response.body).toEqual(blob));

    const req = httpMock.expectOne((r) => r.url === '/api/reports/transactions');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('format')).toBe('pdf');
    expect(req.request.responseType).toBe('blob');
    req.flush(blob);
  });

  it('download() targets the matching route for each report area', () => {
    (['statements', 'transactions', 'review', 'reconciliation', 'categories'] as const).forEach((area) => {
      service.download(area, 'xlsx').subscribe();
      const req = httpMock.expectOne((r) => r.url === `/api/reports/${area}`);
      req.flush(new Blob());
    });
  });
});
