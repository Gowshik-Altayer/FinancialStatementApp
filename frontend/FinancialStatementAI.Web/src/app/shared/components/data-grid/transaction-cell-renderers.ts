import { Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { ICellRendererParams } from 'ag-grid-community';
import { Transaction } from '../../models/transaction.model';
import { reviewPriorityLabel, reviewPriorityTone } from '../../utils/status-tone.util';

/// <summary>Shared AG Grid cell-rendering building blocks for anything that displays a list of
/// Transactions — the Transactions page, the Review queue, and a Statement's own transaction list
/// all show the same Date/Description/Amount/Category/Confidence shape, so the actual DOM-building
/// logic lives here once instead of being copy-pasted three times.
///
/// Plain functions returning real DOM elements, not Angular components/cellRenderer classes — AG
/// Grid Angular's framework-component wrapper never invokes Angular components passed as cell
/// renderers in this app (confirmed via direct debug logging, reproduced on two AG Grid versions;
/// see data-grid.ts's onGridReady comment). Returning HTMLElements from plain functions sidesteps
/// that wrapper entirely.</summary>
/// <summary>Accounting-style amount formatting: negative values are wrapped in parentheses with
/// no minus sign ("(1,850.00)") rather than a leading "-", matching how bank/credit-card
/// statements themselves usually print debits — the same convention this app's own sample
/// statements and the challenge doc's own examples use.</summary>
export function formatTransactionAmount(value: number | null | undefined): string {
  if (value == null) return '—';
  const formatted = Math.abs(value).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  return value < 0 ? `(${formatted})` : formatted;
}

export function createGridIcon(name: string, opts: { title?: string; color?: string } = {}): HTMLElement {
  const icon = document.createElement('span');
  icon.className = 'material-icons cell-icon';
  icon.textContent = name;
  if (opts.title) icon.title = opts.title;
  if (opts.color) icon.style.color = opts.color;
  return icon;
}

export function renderStatementLinkCell(params: ICellRendererParams<Transaction>, router: Router): HTMLElement {
  const transaction = params.data!;
  const link = document.createElement('a');
  link.textContent = transaction.statementFileName ?? '';
  link.href = `/statements/${transaction.statementId}`;
  link.addEventListener('click', (event) => {
    event.preventDefault();
    router.navigate(['/statements', transaction.statementId]);
  });
  return link;
}

export function renderDescriptionCell(params: ICellRendererParams<Transaction>): HTMLElement {
  const transaction = params.data!;
  const wrapper = document.createElement('span');
  wrapper.append(transaction.description ?? '');
  if (transaction.isPotentialDuplicate) {
    wrapper.appendChild(createGridIcon('content_copy', { title: 'Flagged as a potential duplicate of another transaction' }));
  }
  return wrapper;
}

export function renderCategoryCell(params: ICellRendererParams<Transaction>): HTMLElement {
  const transaction = params.data!;
  const wrapper = document.createElement('span');
  wrapper.append(transaction.categoryName ?? 'Uncategorized');
  if (transaction.hasBeenCorrected) {
    wrapper.appendChild(createGridIcon('verified_user', { title: 'This category was corrected by a human reviewer', color: 'var(--fsai-info)' }));
  }
  return wrapper;
}

export function renderConfidenceCell(params: ICellRendererParams<Transaction>): HTMLElement {
  const transaction = params.data!;
  if (!transaction.reviewPriority) {
    const empty = document.createElement('span');
    empty.className = 'muted';
    empty.textContent = '—';
    return empty;
  }

  const badge = document.createElement('span');
  badge.className = `cell-badge tone-${reviewPriorityTone(transaction.reviewPriority)}`;
  badge.textContent = reviewPriorityLabel(transaction.reviewPriority);
  return badge;
}

/// <summary>Opens the same TransactionHistoryDialog the Transactions page uses — AG Grid Community
/// has no expandable "detail row" (that's the Enterprise-only Master/Detail feature), so a history
/// icon opening a dialog is the Community-compatible equivalent of the old inline expandable row.</summary>
export function renderHistoryActionCell(
  params: ICellRendererParams<Transaction>,
  dialog: MatDialog,
  historyDialogComponent: unknown
): HTMLElement {
  const transaction = params.data!;
  const wrapper = document.createElement('span');
  if (transaction.corrections.length > 0) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'grid-icon-button';
    button.title = 'View correction history';
    button.appendChild(createGridIcon('history'));
    button.addEventListener('click', () => {
      dialog.open(historyDialogComponent as never, { data: { corrections: transaction.corrections }, width: '480px' });
    });
    wrapper.appendChild(button);
  }
  return wrapper;
}
