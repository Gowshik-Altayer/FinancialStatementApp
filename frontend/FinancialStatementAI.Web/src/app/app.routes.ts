import { Routes } from '@angular/router';
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
        loadComponent: () => import('./features/dashboard/dashboard').then((m) => m.Dashboard)
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
        component: PlaceholderPage,
        data: { title: 'Transactions', note: 'Transaction search, filtering, and pagination arrive in Phase 13.' }
      },
      {
        path: 'review',
        component: PlaceholderPage,
        data: { title: 'Review', note: 'Human review and correction of low-confidence transactions arrives in Phase 12.' }
      },
      {
        path: 'reconciliation',
        component: PlaceholderPage,
        data: { title: 'Reconciliation', note: 'Statement reconciliation results arrive in Phase 11.' }
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
