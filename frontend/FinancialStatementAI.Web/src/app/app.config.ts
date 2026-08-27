import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';

import { routes } from './app.routes';
import { jwtInterceptor } from './core/interceptors/jwt.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([jwtInterceptor, errorInterceptor])),
    // Material's own components (menu, sidenav, snackbar) still rely on the animations
    // engine internally. Only the imperative trigger()/state()/animate() authoring API is
    // deprecated in favor of animate.enter/leave — provideAnimationsAsync() itself is not.
    provideAnimationsAsync()
    // provideCharts() is intentionally NOT global — it's ~120kB and only the Dashboard,
    // Reconciliation, and Categories routes render charts. Each of those routes supplies it in
    // its own `providers` array (see app.routes.ts) so Chart.js only loads on navigation to a
    // chart-bearing page, keeping the initial bundle within budget.
  ]
};
