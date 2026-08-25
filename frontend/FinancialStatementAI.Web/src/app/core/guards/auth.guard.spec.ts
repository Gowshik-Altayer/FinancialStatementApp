import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { authGuard } from './auth.guard';
import { AuthService } from '../services/auth.service';

describe('authGuard', () => {
  function runGuard(isAuthenticated: boolean) {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { isAuthenticated: () => isAuthenticated } },
        { provide: Router, useValue: { createUrlTree: (commands: string[]) => ({ commands }) } }
      ]
    });

    return TestBed.runInInjectionContext(() => authGuard({} as never, {} as never));
  }

  it('allows navigation when the user is authenticated', () => {
    expect(runGuard(true)).toBe(true);
  });

  it('redirects to /login when the user is not authenticated', () => {
    const result = runGuard(false) as unknown as { commands: string[] };
    expect(result.commands).toEqual(['/login']);
  });
});
