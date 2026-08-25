import { Component, Input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';

/** Route-data-driven stand-in for a feature screen that hasn't been built yet in this phase.
 * Keeps navigation/routing fully wired end-to-end (requirement #27: lazy-loaded routes) while
 * being honest that the real screen lands in a later phase, rather than faking a finished one. */
@Component({
  selector: 'app-placeholder-page',
  standalone: true,
  imports: [MatCardModule],
  templateUrl: './placeholder-page.html',
  styleUrl: './placeholder-page.scss'
})
export class PlaceholderPage {
  @Input() title = '';
  @Input() note = '';
}
