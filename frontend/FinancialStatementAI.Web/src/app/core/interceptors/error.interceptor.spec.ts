import { TestBed } from '@angular/core/testing';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { errorInterceptor } from './error.interceptor';
import { AuthService } from '../services/auth.service';

describe('errorInterceptor', () => {
  let httpMock: HttpTestingController;
  let http: HttpClient;
  let loggedOut: boolean;
  let navigatedTo: unknown[] | null;
  let snackBarCalls: Array<{ message: string; action: string; config: unknown }>;

  beforeEach(() => {
    loggedOut = false;
    navigatedTo = null;
    snackBarCalls = [];

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([errorInterceptor])),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { logout: () => (loggedOut = true) } },
        { provide: Router, useValue: { navigate: (commands: unknown[]) => (navigatedTo = commands) } },
        {
          provide: MatSnackBar,
          useValue: {
            open: (message: string, action: string, config: unknown) => snackBarCalls.push({ message, action, config })
          }
        }
      ]
    });
    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => httpMock.verify());

  it('logs out and redirects to /login on a 401', () => {
    http.get('/api/whatever').subscribe({ error: () => {} });

    httpMock.expectOne('/api/whatever').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(loggedOut).toBe(true);
    expect(navigatedTo).toEqual(['/login']);
  });

  it('shows a connectivity message on a network failure (status 0) without logging out', () => {
    http.get('/api/whatever').subscribe({ error: () => {} });

    httpMock.expectOne('/api/whatever').error(new ProgressEvent('error'), { status: 0, statusText: 'Unknown Error' });

    expect(snackBarCalls).toEqual([
      { message: 'Unable to reach the server. Please check your connection.', action: 'Dismiss', config: { duration: 5000 } }
    ]);
    expect(loggedOut).toBe(false);
  });

  it('shows a generic server-error message on a 500 without logging out', () => {
    http.get('/api/whatever').subscribe({ error: () => {} });

    httpMock.expectOne('/api/whatever').flush({}, { status: 500, statusText: 'Internal Server Error' });

    expect(snackBarCalls).toEqual([
      { message: 'Something went wrong on our end. Please try again.', action: 'Dismiss', config: { duration: 5000 } }
    ]);
    expect(loggedOut).toBe(false);
  });

  it('does not log out, redirect, or show a snackbar for an ordinary 400', () => {
    http.get('/api/whatever').subscribe({ error: () => {} });

    httpMock.expectOne('/api/whatever').flush({}, { status: 400, statusText: 'Bad Request' });

    expect(loggedOut).toBe(false);
    expect(navigatedTo).toBeNull();
    expect(snackBarCalls).toEqual([]);
  });

  it('still propagates the error to the caller', () => {
    let caught: unknown = null;
    http.get('/api/whatever').subscribe({ error: (err) => (caught = err) });

    httpMock.expectOne('/api/whatever').flush({}, { status: 404, statusText: 'Not Found' });

    expect(caught).not.toBeNull();
  });
});
