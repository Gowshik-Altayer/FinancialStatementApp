import { TestBed } from '@angular/core/testing';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';
import { AuthService } from './auth.service';
import { AuthResponse } from '../../shared/models/user.model';

describe('AuthService', () => {
  let service: AuthService;
  let httpMock: HttpTestingController;

  const sampleResponse: AuthResponse = {
    token: 'sample.jwt.token',
    expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    userId: 'user-1',
    email: 'ada@example.com',
    firstName: 'Ada',
    lastName: 'Lovelace',
    role: 'User'
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    service = TestBed.inject(AuthService);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
    localStorage.clear();
  });

  it('starts unauthenticated with no stored session', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.currentUser()).toBeNull();
  });

  it('login() stores the session and marks the user authenticated', () => {
    service.login({ email: sampleResponse.email, password: 'secret' }).subscribe();

    const req = httpMock.expectOne('/api/auth/login');
    expect(req.request.method).toBe('POST');
    req.flush(sampleResponse);

    expect(service.isAuthenticated()).toBe(true);
    expect(service.currentUser()?.email).toBe(sampleResponse.email);
    expect(service.getToken()).toBe(sampleResponse.token);
  });

  it('logout() clears the session', () => {
    service.login({ email: sampleResponse.email, password: 'secret' }).subscribe();
    httpMock.expectOne('/api/auth/login').flush(sampleResponse);

    service.logout();

    expect(service.isAuthenticated()).toBe(false);
    expect(service.getToken()).toBeNull();
  });

  it('discards an expired stored session on initialization', () => {
    const expired: AuthResponse = { ...sampleResponse, expiresAtUtc: new Date(Date.now() - 60_000).toISOString() };
    localStorage.setItem('fsai.auth', JSON.stringify(expired));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });
    const freshService = TestBed.inject(AuthService);

    expect(freshService.isAuthenticated()).toBe(false);
  });
});
