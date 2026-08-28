import { Component, inject } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { LoadingOverlayService } from '../../../core/services/loading-overlay.service';

/** Rendered once at the app root (see app.html), above the router-outlet, so it covers every
 * route including Login/Register — not just pages inside the authenticated Shell. Visibility is
 * driven entirely by LoadingOverlayService, which loading-overlay.interceptor.ts toggles for the
 * duration of any mutating HTTP request. Deliberately has no click-to-dismiss: the whole point is
 * that the user gets control back only when the action's response actually arrives. */
@Component({
  selector: 'app-loading-overlay',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  templateUrl: './loading-overlay.html',
  styleUrl: './loading-overlay.scss'
})
export class LoadingOverlay {
  private readonly overlayService = inject(LoadingOverlayService);
  readonly isBusy = this.overlayService.isBusy;
}
