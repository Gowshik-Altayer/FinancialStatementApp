import { Component, Input } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';

/** Standard "fetching data" block — replaces ad hoc <mat-spinner> + text pairs scattered
 * across pages with one consistent block. */
@Component({
  selector: 'app-loading-state',
  standalone: true,
  imports: [MatProgressSpinnerModule],
  templateUrl: './loading-state.html',
  styleUrl: './loading-state.scss'
})
export class LoadingState {
  @Input() message = 'Loading…';
}
