import { Injectable, computed, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';
import { AuthResponse, CurrentUser, LoginRequest, RegisterRequest } from '../../shared/models/user.model';

const STORAGE_KEY = 'fsai.auth';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly _currentUser = signal<CurrentUser | null>(this.readStoredUser());
  readonly currentUser = this._currentUser.asReadonly();
  readonly isAuthenticated = computed(() => this._currentUser() !== null);

  constructor(private readonly http: HttpClient) {}

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/login', request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>('/api/auth/register', request)
      .pipe(tap((response) => this.storeSession(response)));
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this._currentUser.set(null);
  }

  getToken(): string | null {
    return this.readStoredSession()?.token ?? null;
  }

  private storeSession(response: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(response));
    this._currentUser.set(this.toCurrentUser(response));
  }

  private readStoredSession(): AuthResponse | null {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return null;
    }

    try {
      return JSON.parse(raw) as AuthResponse;
    } catch {
      return null;
    }
  }

  private readStoredUser(): CurrentUser | null {
    const session = this.readStoredSession();
    if (!session) {
      return null;
    }

    if (new Date(session.expiresAtUtc).getTime() <= Date.now()) {
      localStorage.removeItem(STORAGE_KEY);
      return null;
    }

    return this.toCurrentUser(session);
  }

  private toCurrentUser(response: AuthResponse): CurrentUser {
    return {
      userId: response.userId,
      email: response.email,
      firstName: response.firstName,
      lastName: response.lastName,
      role: response.role
    };
  }
}
