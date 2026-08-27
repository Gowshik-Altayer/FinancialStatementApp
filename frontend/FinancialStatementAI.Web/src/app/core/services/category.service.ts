import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  Category,
  CategoryDetail,
  CategoryStats,
  CreateCategoryRequest,
  UpdateCategoryRequest
} from '../../shared/models/category.model';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  getAllIncludingInactive(): Observable<CategoryDetail[]> {
    return this.http.get<CategoryDetail[]>('/api/categories/all');
  }

  getStats(): Observable<CategoryStats[]> {
    return this.http.get<CategoryStats[]>('/api/categories/stats');
  }

  create(request: CreateCategoryRequest): Observable<CategoryDetail> {
    return this.http.post<CategoryDetail>('/api/categories', request);
  }

  update(id: string, request: UpdateCategoryRequest): Observable<CategoryDetail> {
    return this.http.put<CategoryDetail>(`/api/categories/${id}`, request);
  }

  deactivate(id: string): Observable<CategoryDetail> {
    return this.http.post<CategoryDetail>(`/api/categories/${id}/deactivate`, null);
  }

  reactivate(id: string): Observable<CategoryDetail> {
    return this.http.post<CategoryDetail>(`/api/categories/${id}/reactivate`, null);
  }
}
