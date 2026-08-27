import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { CategoryService } from './category.service';
import { Category, CategoryDetail, CategoryStats } from '../../shared/models/category.model';

describe('CategoryService', () => {
  let service: CategoryService;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(CategoryService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('getAll() fetches the active category list', () => {
    const categories: Category[] = [
      { id: 'c1', name: 'Groceries' },
      { id: 'c2', name: 'Travel' }
    ];

    service.getAll().subscribe((result) => expect(result).toEqual(categories));

    const req = httpMock.expectOne('/api/categories');
    expect(req.request.method).toBe('GET');
    req.flush(categories);
  });

  it('getAllIncludingInactive() fetches every category', () => {
    const categories: CategoryDetail[] = [
      { id: 'c1', name: 'Groceries', description: null, isSystemDefined: true, isActive: true, createdAt: '2026-01-01T00:00:00Z' }
    ];

    service.getAllIncludingInactive().subscribe((result) => expect(result).toEqual(categories));

    const req = httpMock.expectOne('/api/categories/all');
    expect(req.request.method).toBe('GET');
    req.flush(categories);
  });

  it('getStats() fetches per-category usage stats', () => {
    const stats: CategoryStats[] = [
      { categoryId: 'c1', categoryName: 'Groceries', transactionCount: 4, totalAmount: 120, aiClassifiedPercent: 75, humanCorrectedPercent: 25 }
    ];

    service.getStats().subscribe((result) => expect(result).toEqual(stats));

    const req = httpMock.expectOne('/api/categories/stats');
    expect(req.request.method).toBe('GET');
    req.flush(stats);
  });

  it('create() posts a new category', () => {
    const created: CategoryDetail = { id: 'c2', name: 'Travel', description: 'Trips', isSystemDefined: false, isActive: true, createdAt: '2026-01-01T00:00:00Z' };

    service.create({ name: 'Travel', description: 'Trips' }).subscribe((result) => expect(result).toEqual(created));

    const req = httpMock.expectOne('/api/categories');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Travel', description: 'Trips' });
    req.flush(created);
  });

  it('update() puts changes to an existing category', () => {
    const updated: CategoryDetail = { id: 'c1', name: 'Groceries', description: 'Food', isSystemDefined: true, isActive: true, createdAt: '2026-01-01T00:00:00Z' };

    service.update('c1', { name: 'Groceries', description: 'Food' }).subscribe((result) => expect(result).toEqual(updated));

    const req = httpMock.expectOne('/api/categories/c1');
    expect(req.request.method).toBe('PUT');
    req.flush(updated);
  });

  it('deactivate() posts to the deactivate endpoint', () => {
    const deactivated: CategoryDetail = { id: 'c1', name: 'Groceries', description: null, isSystemDefined: true, isActive: false, createdAt: '2026-01-01T00:00:00Z' };

    service.deactivate('c1').subscribe((result) => expect(result).toEqual(deactivated));

    const req = httpMock.expectOne('/api/categories/c1/deactivate');
    expect(req.request.method).toBe('POST');
    req.flush(deactivated);
  });

  it('reactivate() posts to the reactivate endpoint', () => {
    const reactivated: CategoryDetail = { id: 'c1', name: 'Groceries', description: null, isSystemDefined: true, isActive: true, createdAt: '2026-01-01T00:00:00Z' };

    service.reactivate('c1').subscribe((result) => expect(result).toEqual(reactivated));

    const req = httpMock.expectOne('/api/categories/c1/reactivate');
    expect(req.request.method).toBe('POST');
    req.flush(reactivated);
  });
});
