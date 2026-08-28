import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { LoadingOverlay } from './shared/components/loading-overlay/loading-overlay';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, LoadingOverlay],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  protected readonly title = signal('FinancialStatementAI.Web');
}
