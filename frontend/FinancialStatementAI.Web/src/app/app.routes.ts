import { Routes } from '@angular/router';
import { provideCharts, withDefaultRegisterables } from 'ng2-charts';
import { authGuard } from './core/guards/auth.guard';
import { Shell } from './core/layout/shell/shell';
import { PlaceholderPage } from './shared/components/placeholder-page/placeholder-page';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login').then((m) => m.Login)
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register').then((m) => m.Register)
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard),
        // Chart.js registered here (not globally, see app.config.ts) so its ~120kB only loads
        // when a chart-bearing route is actually visited. Reconciliation/Categories routes get
        // the same providers once they're built.
        providers: [provideCharts(withDefaultRegisterables())]
      },
      {
        path: 'statements',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/statements/statement-list/statement-list').then((m) => m.StatementList)
          },
          {
            path: 'upload',
            loadComponent: () =>
              import('./features/statements/statement-upload/statement-upload').then((m) => m.StatementUpload)
          },
          {
            path: ':id',
            loadComponent: () =>
              import('./features/statements/statement-detail/statement-detail').then((m) => m.StatementDetail)
          }
        ]
      },
      {
        path: 'transactions',
        loadComponent: () => import('./features/transactions/transactions').then((m) => m.Transactions)
      },
      {
        path: 'review',
        loadComponent: () => import('./features/review/review').then((m) => m.Review)
      },
      {
        path: 'reconciliation',
        component: PlaceholderPage,
        data: {
          title: 'Reconciliation',
          note: 'Reconciliation status is shown per statement — see the Statements list (filterable by reconciliation status) or a statement\'s detail page. A dedicated cross-statement reconciliation report may arrive in a later phase.'
        }
      },
      {
        path: 'categories',
        component: PlaceholderPage,
        data: { title: 'Categories', note: 'Category management (create/edit/deactivate) arrives alongside classification in later phases.' }
      }
    ]
  },
  {
    path: '**',
    loadComponent: () => import('./shared/components/not-found/not-found').then((m) => m.NotFound)
  }
];
