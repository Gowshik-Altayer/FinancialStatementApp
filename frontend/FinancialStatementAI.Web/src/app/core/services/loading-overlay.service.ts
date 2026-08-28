import { Injectable, computed, signal } from '@angular/core';

/** Ref-counted busy state for the full-page loading overlay (see loading-overlay.interceptor.ts
 * and shared/components/loading-overlay). Counting rather than a plain boolean matters because
 * two mutating requests can legitimately overlap (e.g. a component that fires two calls in
 * quick succession) — a boolean would have the first response's `hide()` drop the overlay while
 * the second request is still in flight, letting the user click through mid-action. */
@Injectable({ providedIn: 'root' })
export class LoadingOverlayService {
  private readonly pendingCount = signal(0);
  readonly isBusy = computed(() => this.pendingCount() > 0);

  show(): void {
    this.pendingCount.update((n) => n + 1);
  }

  hide(): void {
    this.pendingCount.update((n) => Math.max(0, n - 1));
  }
}
