import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { roleGuard } from './role.guard';
import { AuthService } from '../services/auth.service';
import { CurrentUser } from '../../shared/models/user.model';

describe('roleGuard', () => {
  function runGuard(user: CurrentUser | null, allowedRoles: CurrentUser['role'][]) {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: () => user } },
        { provide: Router, useValue: { createUrlTree: (commands: string[]) => ({ commands }) } }
      ]
    });

    return TestBed.runInInjectionContext(() => roleGuard(allowedRoles)({} as never, {} as never));
  }

  const admin: CurrentUser = { userId: '1', email: 'a@x.com', firstName: 'A', lastName: 'B', role: 'Admin' };

  it('allows navigation when the user has an allowed role', () => {
    expect(runGuard(admin, ['Admin'])).toBe(true);
  });

  it('redirects to /dashboard when the role is not allowed', () => {
    const result = runGuard(admin, ['Reviewer']) as unknown as { commands: string[] };
    expect(result.commands).toEqual(['/dashboard']);
  });

  it('redirects to /dashboard when there is no current user', () => {
    const result = runGuard(null, ['Admin']) as unknown as { commands: string[] };
    expect(result.commands).toEqual(['/dashboard']);
  });
});
