import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';

/** Generic filter bar: a search box (debounced) plus an arbitrary row of caller-supplied filter
 * controls (selects, date pickers, ...) projected via <ng-content>. Reused as-is by Transactions,
 * Review, Reconciliation, and Categories rather than each page hand-rolling its own search+filter
 * row. Collapses the extra filter controls behind a toggle on mobile so the search box stays the
 * one always-visible control on small screens. */
@Component({
  selector: 'app-filter-panel',
  standalone: true,
  imports: [FormsModule, MatFormFieldModule, MatInputModule, MatIconModule, MatButtonModule],
  templateUrl: './filter-panel.html',
  styleUrl: './filter-panel.scss'
})
export class FilterPanel {
  @Input() searchPlaceholder = 'Search…';
  @Input() searchValue = '';
  @Input() hasActiveFilters = false;
  @Output() searchValueChange = new EventEmitter<string>();
  @Output() clearFilters = new EventEmitter<void>();

  readonly expanded = signal(false);

  private readonly searchInput$ = new Subject<string>();

  constructor() {
    this.searchInput$.pipe(debounceTime(300), distinctUntilChanged()).subscribe((value) => {
      this.searchValueChange.emit(value);
    });
  }

  onSearchInput(value: string): void {
    this.searchValue = value;
    this.searchInput$.next(value);
  }

  toggleExpanded(): void {
    this.expanded.set(!this.expanded());
  }
}
