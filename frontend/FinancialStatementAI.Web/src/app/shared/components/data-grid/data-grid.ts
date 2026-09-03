import { DOCUMENT } from '@angular/common';
import { Component, ElementRef, Input, Output, EventEmitter, OnInit, inject } from '@angular/core';
import { AgGridAngular } from 'ag-grid-angular';
import {
  AllCommunityModule,
  CellValueChangedEvent,
  ColDef,
  FilterChangedEvent,
  GetRowIdFunc,
  GridApi,
  GridReadyEvent,
  ModuleRegistry,
  PaginationChangedEvent,
  RowClassRules
} from 'ag-grid-community';

ModuleRegistry.registerModules([AllCommunityModule]);

// AG Grid's own theme CSS (~290KB) is loaded via a runtime <link> instead of bundled as a
// component style — Angular's per-component style budget treats it as this component's own
// bloat otherwise, which it isn't: it's a fixed third-party stylesheet, not something that grows
// with app code. A <link> also means the CSS ships as a real lazy network request the first time
// a DataGrid renders, not baked into the transactions route's JS chunk. Copied to /assets/ag-grid
// via angular.json's assets config (from node_modules/ag-grid-community/styles).
const AG_GRID_STYLESHEETS = ['assets/ag-grid/ag-grid.css', 'assets/ag-grid/ag-theme-quartz.css'];
let stylesheetsInjected = false;

/// <summary>Generic, reusable AG Grid wrapper (Community edition) — every page that wants a grid
/// calls this with its own columnDefs/rowData rather than each page wiring up ag-grid-angular
/// itself, so module registration, theming, and default column behavior (sortable/resizable) live
/// in exactly one place. First used by the Transactions page; any future page adopting AG Grid
/// reuses this same component.</summary>
@Component({
  selector: 'app-data-grid',
  standalone: true,
  imports: [AgGridAngular],
  templateUrl: './data-grid.html',
  styleUrl: './data-grid.scss'
})
export class DataGrid<TData = unknown> implements OnInit {
  private readonly document = inject(DOCUMENT);
  private readonly elementRef = inject(ElementRef<HTMLElement>);

  ngOnInit(): void {
    if (stylesheetsInjected) return;
    stylesheetsInjected = true;

    for (const href of AG_GRID_STYLESHEETS) {
      const link = this.document.createElement('link');
      link.rel = 'stylesheet';
      link.href = href;
      this.document.head.appendChild(link);
    }
  }

  @Input({ required: true }) columnDefs: ColDef<TData>[] = [];
  @Input({ required: true }) rowData: TData[] = [];
  @Input() getRowId?: GetRowIdFunc<TData>;
  @Input() rowClassRules?: RowClassRules<TData>;

  /// <summary>AG Grid's own pagination/sorting/filtering chrome is left off by default — every
  /// current caller already drives paging and filtering through its own API call, so turning
  /// these on would just give two competing UIs over the same data. A future page with a small,
  /// fully-client-side dataset can opt in via these inputs instead of building its own paging.</summary>
  @Input() pagination = false;
  @Input() paginationPageSize = 20;

  @Output() cellValueChanged = new EventEmitter<CellValueChangedEvent<TData>>();

  /// <summary>Fires once, right after the gridReady workaround below re-applies columnDefs, so a
  /// consumer that needs the live grid API (e.g. to clear column filters, or check
  /// isAnyFilterPresent()) doesn't have to build its own ViewChild/gridReady plumbing.</summary>
  @Output() gridApiReady = new EventEmitter<GridApi<TData>>();

  /// <summary>Passthrough of AG Grid's own filterChanged event — lets a consumer track whether any
  /// per-column filter is currently active without polling the API.</summary>
  @Output() filterChanged = new EventEmitter<FilterChangedEvent<TData>>();

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    filter: false,
    // Only takes effect on columns that also set a `filter` — harmless on columns without one.
    floatingFilter: true
  };

  onCellValueChanged(event: CellValueChangedEvent<TData>): void {
    this.cellValueChanged.emit(event);
  }

  onFilterChanged(event: FilterChangedEvent<TData>): void {
    this.filterChanged.emit(event);
  }

  /// <summary>Works around a confirmed AG Grid Angular initialization bug (reproduced on both
  /// v36.1.0 and v33.3.2): function-based cellRenderers passed via the initial [columnDefs] input
  /// are never actually invoked for rendering — cells stay empty even though the column model and
  /// the function references are provably intact (checked via the grid API directly). Re-applying
  /// the IDENTICAL columnDefs through the grid API once the grid signals it's ready reliably fixes
  /// rendering for every column; a plain redrawRows()/refreshCells() call does not. Root cause not
  /// fully isolated beyond that; this is the minimal, verified fix applied once per grid instance.</summary>
  onGridReady(event: GridReadyEvent<TData>): void {
    event.api.setGridOption('columnDefs', event.api.getColumnDefs());
    this.gridApiReady.emit(event.api);
    this.fixAutoHeightHeaderGap();
  }

  /// <summary>Same underlying defect as onGridReady above, resurfacing at a second lifecycle
  /// point: rows that enter the DOM for the first time when the user pages to a page they haven't
  /// visited yet don't render their cell renderers until something re-applies columnDefs — the
  /// grid's own row-recycling on subsequent visits to an already-rendered page works fine, which
  /// is why the symptom was reported as "only shows up the first time I page there." Re-applying
  /// on every pagination change is cheap (one page's worth of rows) and covers every page's first
  /// visit, not just the grid's very first paint.
  ///
  /// Deferred one tick (setTimeout 0): `paginationChanged` itself fires before the new page's row
  /// DOM actually exists — confirmed by testing every page of a 110-row / 6-page grid, where a
  /// synchronous reapply left the final (partial, 10-row) page's custom-rendered cells blank while
  /// plain-text cells on that same page rendered fine. Pushing the reapply to the next tick lets
  /// AG Grid finish creating that page's row elements first.</summary>
  onPaginationChanged(event: PaginationChangedEvent<TData>): void {
    setTimeout(() => {
      event.api.setGridOption('columnDefs', event.api.getColumnDefs());
      this.fixAutoHeightHeaderGap();
    });
  }

  /// <summary>Works around a third AG Grid Angular `domLayout="autoHeight"` defect: on first
  /// render, the sticky header container's height (".ag-grid-pinned-top-rows", which sets its own
  /// height from the CSS variable --ag-header-rows-height) is sometimes computed wildly larger
  /// than the header + floating-filter rows it actually holds — confirmed by inspecting the live
  /// DOM on a grid with only 3 rows: the variable was set to 768px (looking like a stale
  /// viewport-height fallback from a premature measurement) instead of the correct ~96px, leaving
  /// a large empty gap between the header and the visible rows for the lifetime of that grid
  /// instance. Once wrong, it is never re-asserted by AG Grid (confirmed empirically — nothing
  /// else in this file's workarounds, nor resetRowHeights()/a domLayout toggle/a rowData reset,
  /// corrects it), so it's safe to fix by directly measuring the real header and floating-filter
  /// row elements once they exist and overwriting the variable with their true combined height.
  /// Scoped to this instance's own host element via ElementRef so multiple DataGrids on one page
  /// never cross-correct each other.</summary>
  private fixAutoHeightHeaderGap(): void {
    setTimeout(() => {
      const host = this.elementRef.nativeElement;
      const pinnedTopRows = host.querySelector('.ag-grid-pinned-top-rows') as HTMLElement | null;
      const headerRow = host.querySelector('.ag-header-row-column') as HTMLElement | null;
      const filterRow = host.querySelector('.ag-header-row-filter') as HTMLElement | null;
      if (!pinnedTopRows) return;

      const realHeight = (headerRow?.offsetHeight ?? 0) + (filterRow?.offsetHeight ?? 0);
      if (realHeight > 0) {
        pinnedTopRows.style.setProperty('--ag-header-rows-height', `${realHeight}px`);
      }
    });
  }
}
