import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { StatementService } from './statement.service';
import { StatementDetail, StatementSummary } from '../../shared/models/statement.model';

describe('StatementService', () => {
  let service: StatementService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(StatementService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('upload() posts multipart form data to /api/statements/upload', () => {
    const file = new File(['%PDF-1.4'], 'statement.pdf', { type: 'application/pdf' });
    const detail: Partial<StatementDetail> = { id: 'abc', originalFileName: 'statement.pdf' };

    service.upload(file).subscribe((result) => expect(result).toEqual(detail));

    const req = httpMock.expectOne('/api/statements/upload');
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    req.flush(detail);
  });

  it('getAll() fetches the statement list', () => {
    const summaries: Partial<StatementSummary>[] = [{ id: '1' }, { id: '2' }];

    service.getAll().subscribe((result) => expect(result).toEqual(summaries));

    const req = httpMock.expectOne('/api/statements');
    expect(req.request.method).toBe('GET');
    req.flush(summaries);
  });

  it('getById() fetches a single statement', () => {
    const detail: Partial<StatementDetail> = { id: 'abc' };

    service.getById('abc').subscribe((result) => expect(result).toEqual(detail));

    const req = httpMock.expectOne('/api/statements/abc');
    expect(req.request.method).toBe('GET');
    req.flush(detail);
  });

  it('reprocess() posts to the reprocess endpoint', () => {
    const detail: Partial<StatementDetail> = { id: 'abc', processingStatus: 'ExtractionComplete' };

    service.reprocess('abc').subscribe((result) => expect(result).toEqual(detail));

    const req = httpMock.expectOne('/api/statements/abc/reprocess');
    expect(req.request.method).toBe('POST');
    req.flush(detail);
  });
});
