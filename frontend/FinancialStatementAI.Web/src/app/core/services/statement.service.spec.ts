import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { StatementService } from './statement.service';
import { StatementDetail } from '../../shared/models/statement.model';
import { PagedResult } from '../../shared/models/paged-result.model';
import { StatementSummary } from '../../shared/models/statement.model';

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

  it('getAll() fetches a page of statements with default paging when no query is given', () => {
    const page: PagedResult<Partial<StatementSummary>> = { items: [{ id: '1' }, { id: '2' }], totalCount: 2, page: 1, pageSize: 20 };

    service.getAll().subscribe((result) => expect(result).toEqual(page));

    const req = httpMock.expectOne((r) => r.url === '/api/statements');
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.has('search')).toBe(false);
    req.flush(page);
  });

  it('getAll() forwards search/status/reconciliationStatus/page/pageSize as query params', () => {
    const page: PagedResult<Partial<StatementSummary>> = { items: [], totalCount: 0, page: 2, pageSize: 10 };

    service
      .getAll({ search: 'chase', status: 'PendingReview', reconciliationStatus: 'Mismatch', page: 2, pageSize: 10 })
      .subscribe((result) => expect(result).toEqual(page));

    const req = httpMock.expectOne((r) => r.url === '/api/statements');
    expect(req.request.params.get('search')).toBe('chase');
    expect(req.request.params.get('status')).toBe('PendingReview');
    expect(req.request.params.get('reconciliationStatus')).toBe('Mismatch');
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('10');
    req.flush(page);
  });

  it('getById() fetches a single statement', () => {
    const detail: Partial<StatementDetail> = { id: 'abc' };

    service.getById('abc').subscribe((result) => expect(result).toEqual(detail));

    const req = httpMock.expectOne('/api/statements/abc');
    expect(req.request.method).toBe('GET');
    req.flush(detail);
  });

  it('reprocess() posts to the reprocess endpoint', () => {
    const detail: Partial<StatementDetail> = { id: 'abc', processingStatus: 'ClassificationComplete' };

    service.reprocess('abc').subscribe((result) => expect(result).toEqual(detail));

    const req = httpMock.expectOne('/api/statements/abc/reprocess');
    expect(req.request.method).toBe('POST');
    req.flush(detail);
  });

  it('verify() posts to the verify endpoint', () => {
    const detail: Partial<StatementDetail> = { id: 'abc', processingStatus: 'Verified' };

    service.verify('abc').subscribe((result) => expect(result).toEqual(detail));

    const req = httpMock.expectOne('/api/statements/abc/verify');
    expect(req.request.method).toBe('POST');
    req.flush(detail);
  });
});
