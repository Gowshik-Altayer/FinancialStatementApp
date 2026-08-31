import {
  IDoesFilterPassParams,
  IFilterComp,
  IFilterParams,
  IFloatingFilterComp,
  IFloatingFilterParams
} from 'ag-grid-community';

export interface SelectFilterOption {
  value: string;
  label: string;
}

export interface SelectFilterParams extends IFilterParams {
  options: SelectFilterOption[];
}

/// <summary>Dropdown-style "is one of" filter for enum-like columns (Category, Type, Confidence)
/// — AG Grid Community has no built-in Set Filter (that's Enterprise-only), so this fills the gap
/// with a plain <select>. Implemented as a vanilla class (not an Angular @Component) on purpose:
/// AG Grid Angular's framework-component wrapper never invokes Angular components passed as cell
/// renderers in this app (see data-grid.ts's onGridReady comment) — the same wrapper is used for
/// custom filters, so this sidesteps that bug entirely rather than risking it a second time.</summary>
export class SelectFilter implements IFilterComp {
  private eGui!: HTMLDivElement;
  private select!: HTMLSelectElement;
  private params!: SelectFilterParams;
  private value = '';

  init(params: SelectFilterParams): void {
    this.params = params;
    this.eGui = document.createElement('div');
    this.eGui.className = 'select-filter-popup';
    this.select = document.createElement('select');
    this.select.className = 'select-filter-input';
    this.populateOptions(params.options);
    this.select.addEventListener('change', () => {
      this.value = this.select.value;
      this.params.filterChangedCallback();
    });
    this.eGui.appendChild(this.select);
  }

  private populateOptions(options: SelectFilterOption[]): void {
    this.select.innerHTML = '';
    const blank = document.createElement('option');
    blank.value = '';
    blank.textContent = 'All';
    this.select.appendChild(blank);
    for (const option of options) {
      const el = document.createElement('option');
      el.value = option.value;
      el.textContent = option.label;
      this.select.appendChild(el);
    }
    this.select.value = this.value;
  }

  getGui(): HTMLElement {
    return this.eGui;
  }

  doesFilterPass(params: IDoesFilterPassParams): boolean {
    if (!this.value) return true;
    const field = this.params.colDef.field!;
    return (params.data as Record<string, unknown>)[field] === this.value;
  }

  isFilterActive(): boolean {
    return !!this.value;
  }

  getModel(): { value: string } | null {
    return this.value ? { value: this.value } : null;
  }

  setModel(model: { value: string } | null): void {
    this.value = model?.value ?? '';
    this.select.value = this.value;
  }

  /// <summary>Not a fixed AG Grid interface method — SelectFloatingFilter reaches into its parent
  /// filter instance via `parentFilterInstance` and calls whatever method the parent chooses to
  /// expose for this purpose (AG Grid's own documented pattern for custom filter/floating-filter
  /// pairs). Setting the value here (rather than just calling setModel) also pushes the change
  /// through filterChangedCallback so the grid actually re-filters.</summary>
  setFilterValue(value: string): void {
    this.value = value;
    this.select.value = value;
    this.params.filterChangedCallback();
  }
}

export interface SelectFloatingFilterParams extends IFloatingFilterParams {
  options: SelectFilterOption[];
}

/// <summary>Always-visible companion to SelectFilter, rendered in the floating-filter row under
/// the column header — matches the always-visible dropdown UX the custom Material filter panel
/// used to provide, instead of hiding the dropdown behind a menu-icon click.</summary>
export class SelectFloatingFilter implements IFloatingFilterComp {
  private eGui!: HTMLSelectElement;
  private params!: SelectFloatingFilterParams;

  init(params: SelectFloatingFilterParams): void {
    this.params = params;
    this.eGui = document.createElement('select');
    this.eGui.className = 'select-floating-filter';
    const blank = document.createElement('option');
    blank.value = '';
    blank.textContent = 'All';
    this.eGui.appendChild(blank);
    for (const option of params.options) {
      const el = document.createElement('option');
      el.value = option.value;
      el.textContent = option.label;
      this.eGui.appendChild(el);
    }
    this.eGui.addEventListener('change', () => {
      this.params.parentFilterInstance((instance) => {
        (instance as unknown as SelectFilter).setFilterValue(this.eGui.value);
      });
    });
  }

  getGui(): HTMLElement {
    return this.eGui;
  }

  onParentModelChanged(parentModel: { value: string } | null): void {
    this.eGui.value = parentModel?.value ?? '';
  }
}
