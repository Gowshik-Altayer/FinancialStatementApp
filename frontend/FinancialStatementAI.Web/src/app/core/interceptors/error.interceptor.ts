import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';

/** Ensures every unhandled API failure still surfaces a user-friendly message
 * (see requirement #32 — no raw stack traces / silent failures in the UI) and that an
 * expired/invalid token bounces the user back to Login instead of leaving them stuck. */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const router = inject(Router);
  const authService = inject(AuthService);
  const snackBar = inject(MatSnackBar);

  return next(req).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        authService.logout();
        router.navigate(['/login']);
      } else if (error.status === 0) {
        snackBar.open('Unable to reach the server. Please check your connection.', 'Dismiss', { duration: 5000 });
      } else if (error.status >= 500) {
        snackBar.open('Something went wrong on our end. Please try again.', 'Dismiss', { duration: 5000 });
      }

      return throwError(() => error);
    })
  );
};
