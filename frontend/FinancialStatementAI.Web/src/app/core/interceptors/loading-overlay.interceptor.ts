import { inject } from '@angular/core';
import { HttpInterceptorFn } from '@angular/common/http';
import { finalize } from 'rxjs';
import { LoadingOverlayService } from '../services/loading-overlay.service';

const MUTATING_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);

/** Blocks the entire page behind a full-screen overlay for the duration of any mutating request
 * (create/update/delete — anything that changes server state), so a user cannot click elsewhere
 * and race an in-flight action. The overlay comes down only on the response — success or error —
 * via finalize(), never on a timer or optimistically.
 *
 * Applied at the HTTP layer rather than per-component: reprocess/verify/upload/login/register
 * each had their own local spinner-in-a-button, and Categories' create/update/deactivate had no
 * busy-lock at all (its dialog closes before the request even fires), so a user could trigger a
 * second mutation while the first was still in flight. One interceptor covers every current call
 * site and any future one, with no risk of a new mutation forgetting to wire itself up.
 *
 * GET requests are deliberately excluded — page-load fetches already have their own non-blocking
 * skeleton loaders (see shared/components/skeleton), and locking the whole UI on every navigation
 * would be a regression, not an improvement. */
export const loadingOverlayInterceptor: HttpInterceptorFn = (req, next) => {
  if (!MUTATING_METHODS.has(req.method)) {
    return next(req);
  }

  const overlay = inject(LoadingOverlayService);
  overlay.show();

  return next(req).pipe(finalize(() => overlay.hide()));
};
