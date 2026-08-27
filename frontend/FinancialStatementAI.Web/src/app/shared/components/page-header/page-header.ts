import { Component, Input } from '@angular/core';

/** Title + subtitle + action-buttons slot, used at the top of every feature page for consistent
 * spacing/typography instead of each page hand-rolling its own <h1>. Actions are projected via
 * <app-page-header><button ...></app-page-header> rather than an input, since action content is
 * arbitrary markup (buttons, menus), not a simple value. */
@Component({
  selector: 'app-page-header',
  standalone: true,
  templateUrl: './page-header.html',
  styleUrl: './page-header.scss'
})
export class PageHeader {
  @Input({ required: true }) title = '';
  @Input() subtitle?: string;
}
