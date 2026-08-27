import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

/** Icon + error message + retry button for a failed load — distinct from empty-state (a
 * successful load that just found nothing). Every page's HTTP error branch should render this
 * instead of a bare snackbar, so a failed page load always leaves the user a way to retry. */
@Component({
  selector: 'app-error-state',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  templateUrl: './error-state.html',
  styleUrl: './error-state.scss'
})
export class ErrorState {
  @Input() message = 'Something went wrong loading this data.';
  @Input() retryLabel = 'Retry';
  @Output() retry = new EventEmitter<void>();
}
