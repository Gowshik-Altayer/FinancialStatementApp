import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

/** Icon + message + optional call-to-action for "there's genuinely nothing here yet" —
 * distinct from error-state (a failed load) and loading-state (still fetching). The action
 * button is exposed as an output rather than a router link so callers can navigate, open a
 * dialog, or trigger a refetch as appropriate. */
@Component({
  selector: 'app-empty-state',
  standalone: true,
  imports: [MatIconModule, MatButtonModule],
  templateUrl: './empty-state.html',
  styleUrl: './empty-state.scss'
})
export class EmptyState {
  @Input() icon = 'inbox';
  @Input({ required: true }) message = '';
  @Input() actionLabel?: string;
  @Output() action = new EventEmitter<void>();
}
