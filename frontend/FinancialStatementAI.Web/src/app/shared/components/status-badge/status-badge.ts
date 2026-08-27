import { Component, Input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { StatusTone } from '../../utils/status-tone.util';

/** Generic colored badge for any status value — the badge itself knows nothing about what
 * "Reconciled" or "PendingReview" means; callers resolve a StatusTone first (see
 * shared/utils/status-tone.util.ts) and pass it in, so this component stays reusable across
 * processing status, reconciliation status, and review priority without a switch statement here. */
@Component({
  selector: 'app-status-badge',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './status-badge.html',
  styleUrl: './status-badge.scss'
})
export class StatusBadge {
  @Input({ required: true }) label = '';
  @Input({ required: true }) tone: StatusTone = 'neutral';
  @Input() icon?: string;
}
