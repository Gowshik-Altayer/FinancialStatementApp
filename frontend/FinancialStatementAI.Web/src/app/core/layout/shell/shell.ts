import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { BreakpointObserver } from '@angular/cdk/layout';
import { map } from 'rxjs';
import { AuthService } from '../../services/auth.service';

const THEME_STORAGE_KEY = 'fsai.theme';
const SIDENAV_STORAGE_KEY = 'fsai.sidenav-collapsed';

interface NavLink {
  path: string;
  label: string;
  icon: string;
}

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    MatToolbarModule,
    MatSidenavModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatTooltipModule,
    MatSlideToggleModule
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss'
})
export class Shell {
  readonly navLinks: NavLink[] = [
    { path: '/dashboard', label: 'Dashboard', icon: 'dashboard' },
    { path: '/statements', label: 'Statements', icon: 'description' },
    { path: '/transactions', label: 'Transactions', icon: 'receipt_long' },
    { path: '/review', label: 'Review', icon: 'fact_check' },
    { path: '/reconciliation', label: 'Reconciliation', icon: 'balance' },
    { path: '/categories', label: 'Categories', icon: 'category' },
    { path: '/reports', label: 'Reports', icon: 'summarize' }
  ];

  private readonly breakpointObserver = inject(BreakpointObserver);

  // Below the desktop breakpoint (matches src/styles/_breakpoints.scss's `desktop` cutoff) the
  // sidenav becomes an overlay the user opens/closes, rather than a permanently-docked 240px
  // column stealing width from the page content — a fixed "side" mode sidenav is exactly the
  // kind of non-responsive layout requirement #1 calls out. Plain width-based, not CDK's
  // Breakpoints.Handset/Tablet (those also gate on pointer/hover media features, which don't
  // reliably match under viewport-only emulation) — this app only ever needs the width cutoff.
  readonly isHandset = toSignal(
    this.breakpointObserver.observe(['(max-width: 959.98px)']).pipe(map((result) => result.matches)),
    { initialValue: false }
  );

  // Activates the `:root[data-theme='dark']` token overrides in _tokens.scss, which existed but
  // had no UI trigger anywhere in the app until now — every dark-mode color was already defined
  // and correct, just unreachable.
  readonly isDarkMode = signal(localStorage.getItem(THEME_STORAGE_KEY) === 'dark');

  /** Collapsed = icon-only rail; expanded = full labels. Persisted, since this is a workspace
   * preference a user sets once and expects to stick across sessions. */
  readonly isCollapsed = signal(localStorage.getItem(SIDENAV_STORAGE_KEY) === 'true');


  constructor(
    protected readonly authService: AuthService,
    private readonly router: Router
  ) {
    this.applyTheme(this.isDarkMode());
  }

  toggleDarkMode(): void {
    const next = !this.isDarkMode();
    this.isDarkMode.set(next);
    this.applyTheme(next);
    localStorage.setItem(THEME_STORAGE_KEY, next ? 'dark' : 'light');
  }

  toggleCollapsed(): void {
    const next = !this.isCollapsed();
    this.isCollapsed.set(next);
    localStorage.setItem(SIDENAV_STORAGE_KEY, String(next));
    // No re-measure needed: the content offset is driven declaratively from the collapsed state
    // in shell.scss rather than from a width MatSidenavContainer has to measure. See the
    // `.nav-collapsed` / `.nav-expanded` rules there for why.
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  private applyTheme(dark: boolean): void {
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }
}
