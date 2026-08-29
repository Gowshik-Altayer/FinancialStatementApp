import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { TransactionService } from './transaction.service';
import { Transaction } from '../../shared/models/transaction.model';
import { PagedResult } from '../../shared/models/paged-result.model';

describe('TransactionService', () => {
  let service: TransactionService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(TransactionService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getForStatement() fetches a statement\'s transactions', () => {
    const transactions: Partial<Transaction>[] = [{ id: 't1' }];

    service.getForStatement('stmt-1').subscribe((result) => expect(result).toEqual(transactions));

    const req = httpMock.expectOne('/api/statements/stmt-1/transactions');
    expect(req.request.method).toBe('GET');
    req.flush(transactions);
  });

  it('getReviewQueue() fetches the review queue', () => {
    const transactions: Partial<Transaction>[] = [{ id: 't1' }, { id: 't2' }];

    service.getReviewQueue().subscribe((result) => expect(result).toEqual(transactions));

    const req = httpMock.expectOne('/api/transactions/review-queue');
    expect(req.request.method).toBe('GET');
    req.flush(transactions);
  });

  it('search() defaults to page 1 / pageSize 20 with no other params when called with none', () => {
    const page: PagedResult<Partial<Transaction>> = { items: [], totalCount: 0, page: 1, pageSize: 20 };

    service.search().subscribe((result) => expect(result).toEqual(page));

    const req = httpMock.expectOne((r) => r.url === '/api/transactions');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('20');
    expect(req.request.params.has('search')).toBe(false);
    expect(req.request.params.has('categoryId')).toBe(false);
    expect(req.request.params.has('statementId')).toBe(false);
    req.flush(page);
  });

  it('search() forwards search/categoryId/statementId/page/pageSize as query params', () => {
    const page: PagedResult<Partial<Transaction>> = { items: [], totalCount: 0, page: 3, pageSize: 50 };

    service
      .search({ search: 'uber', categoryId: 'cat-1', statementId: 'stmt-1', page: 3, pageSize: 50 })
      .subscribe((result) => expect(result).toEqual(page));

    const req = httpMock.expectOne((r) => r.url === '/api/transactions');
    expect(req.request.params.get('search')).toBe('uber');
    expect(req.request.params.get('categoryId')).toBe('cat-1');
    expect(req.request.params.get('statementId')).toBe('stmt-1');
    expect(req.request.params.get('page')).toBe('3');
    expect(req.request.params.get('pageSize')).toBe('50');
    req.flush(page);
  });

  it('correctCategory() posts the category name and optional reason', () => {
    const updated: Partial<Transaction> = { id: 't1', categoryName: 'Groceries' };

    service.correctCategory('t1', 'Groceries', 'Wrong merchant match').subscribe((result) => expect(result).toEqual(updated));

    const req = httpMock.expectOne('/api/transactions/t1/corrections');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ categoryName: 'Groceries', reason: 'Wrong merchant match' });
    req.flush(updated);
  });

  it('correctTransaction() posts only the supplied fields', () => {
    const updated: Partial<Transaction> = { id: 't1', description: 'Corrected', amount: -12.5 };

    service.correctTransaction('t1', { description: 'Corrected', amount: -12.5 }).subscribe((result) => expect(result).toEqual(updated));

    const req = httpMock.expectOne('/api/transactions/t1/corrections');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ description: 'Corrected', amount: -12.5 });
    req.flush(updated);
  });

  it('bulkCorrectCategory() posts to the bulk corrections endpoint and returns the updated count', () => {
    const response = { updatedCount: 4, transaction: { id: 't1', categoryName: 'Groceries' } as Partial<Transaction> };

    service.bulkCorrectCategory('t1', 'Groceries', 'Same merchant').subscribe((result) => expect(result).toEqual(response));

    const req = httpMock.expectOne('/api/transactions/t1/corrections/bulk');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ categoryName: 'Groceries', reason: 'Same merchant' });
    req.flush(response);
  });
});
