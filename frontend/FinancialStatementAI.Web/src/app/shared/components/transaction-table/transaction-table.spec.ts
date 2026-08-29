import { TestBed } from '@angular/core/testing';
import { NotificationService } from '../../../core/services/notification.service';
import { Observable, of, throwError } from 'rxjs';
import { TransactionTable } from './transaction-table';
import { TransactionService } from '../../../core/services/transaction.service';
import { CategoryService } from '../../../core/services/category.service';
import { Transaction, ReviewPriority } from '../../models/transaction.model';
import { Category } from '../../models/category.model';

describe('TransactionTable', () => {
  let correctCategoryResult: Observable<Transaction>;
  let snackBarMessages: string[];

  const categories: Category[] = [
    { id: 'c1', name: 'Groceries' },
    { id: 'c2', name: 'Travel' }
  ];

  function buildTransaction(overrides: Partial<Transaction> = {}): Transaction {
    return {
      id: 't1',
      statementId: 's1',
      statementFileName: 'statement.pdf',
      transactionDate: '2026-01-08',
      postingDate: null,
      description: 'WHOLE FOODS MARKET',
      merchant: 'WHOLE FOODS MARKET',
      referenceNumber: null,
      debitAmount: 64.02,
      creditAmount: null,
      amount: -64.02,
      currency: 'USD',
      transactionType: 'Debit',
      pageSourceLocation: null,
      extractionConfidence: null,
      categoryId: null,
      categoryName: 'Other',
      classificationConfidence: 0.5,
      classificationMethod: 'Llm',
      classificationReason: null,
      reviewPriority: 'ReviewRequired',
      hasBeenCorrected: false,
      isPotentialDuplicate: false,
      duplicateOfTransactionId: null,
      corrections: [],
      ...overrides
    };
  }

  function createComponent(transactions: Transaction[]) {
    snackBarMessages = [];
    correctCategoryResult = of(buildTransaction());

    TestBed.configureTestingModule({
      imports: [TransactionTable],
      providers: [
        {
          provide: TransactionService,
          useValue: {
            correctCategory: () => correctCategoryResult
          }
        },
        { provide: CategoryService, useValue: { getAll: () => of(categories) } },
        // Toasts now go through NotificationService (which applies the shared tone/panelClass);
        // the assertions below still just check which message the user was shown.
        {
          provide: NotificationService,
          useValue: {
            success: (message: string) => snackBarMessages.push(message),
            warning: (message: string) => snackBarMessages.push(message),
            error: (message: string) => snackBarMessages.push(message)
          }
        }
      ]
    });

    const fixture = TestBed.createComponent(TransactionTable);
    fixture.componentInstance.transactions = transactions;
    fixture.detectChanges();
    return fixture;
  }

  it('loads the active categories on init', () => {
    const fixture = createComponent([buildTransaction()]);

    expect(fixture.componentInstance.categories()).toEqual(categories);
  });

  it('startEdit() enters edit mode and preloads the current category', () => {
    const transaction = buildTransaction({ categoryName: 'Groceries' });
    const fixture = createComponent([transaction]);

    fixture.componentInstance.startEdit(transaction);

    expect(fixture.componentInstance.editingId()).toBe('t1');
    expect(fixture.componentInstance.selectedCategoryName).toBe('Groceries');
  });

  it('cancelEdit() exits edit mode', () => {
    const transaction = buildTransaction();
    const fixture = createComponent([transaction]);
    fixture.componentInstance.startEdit(transaction);

    fixture.componentInstance.cancelEdit();

    expect(fixture.componentInstance.editingId()).toBeNull();
  });

  it('toggleHistory() expands then collapses the same row', () => {
    const transaction = buildTransaction();
    const fixture = createComponent([transaction]);

    fixture.componentInstance.toggleHistory(transaction);
    expect(fixture.componentInstance.expandedId()).toBe('t1');

    fixture.componentInstance.toggleHistory(transaction);
    expect(fixture.componentInstance.expandedId()).toBeNull();
  });

  it('save() with no category selected just exits edit mode without calling the API', () => {
    const transaction = buildTransaction({ categoryName: 'Other' });
    const fixture = createComponent([transaction]);
    fixture.componentInstance.startEdit(transaction);
    fixture.componentInstance.selectedCategoryName = '';

    fixture.componentInstance.save(transaction);

    expect(fixture.componentInstance.editingId()).toBeNull();
    expect(transaction.categoryName).toBe('Other');
  });

  it('save() with the same category already selected is a no-op that still exits edit mode', () => {
    const transaction = buildTransaction({ categoryName: 'Groceries' });
    const fixture = createComponent([transaction]);
    fixture.componentInstance.startEdit(transaction);

    fixture.componentInstance.save(transaction);

    expect(fixture.componentInstance.editingId()).toBeNull();
  });

  it('save() with a genuinely new category updates the row in place and shows a success message', () => {
    const transaction = buildTransaction({ categoryName: 'Other' });
    const fixture = createComponent([transaction]);
    correctCategoryResult = of(buildTransaction({ categoryName: 'Groceries', hasBeenCorrected: true }));
    fixture.componentInstance.startEdit(transaction);
    fixture.componentInstance.selectedCategoryName = 'Groceries';

    fixture.componentInstance.save(transaction);

    expect(transaction.categoryName).toBe('Groceries');
    expect(transaction.hasBeenCorrected).toBe(true);
    expect(fixture.componentInstance.editingId()).toBeNull();
    expect(fixture.componentInstance.savingId()).toBeNull();
    expect(snackBarMessages).toContain('Category corrected.');
  });

  it('save() shows a failure message and exits saving state when the API call fails', () => {
    const transaction = buildTransaction({ categoryName: 'Other' });
    const fixture = createComponent([transaction]);
    correctCategoryResult = throwError(() => new Error('rejected'));
    fixture.componentInstance.startEdit(transaction);
    fixture.componentInstance.selectedCategoryName = 'Groceries';

    fixture.componentInstance.save(transaction);

    expect(fixture.componentInstance.savingId()).toBeNull();
    expect(snackBarMessages).toContain('Correction failed.');
  });

  const priorityCases: Array<[ReviewPriority | null, string]> = [
    ['HighConfidence', 'High confidence'],
    ['ReviewRecommended', 'Review recommended'],
    ['ReviewRequired', 'Review required'],
    [null, 'Unclassified']
  ];

  for (const [priority, expected] of priorityCases) {
    it(`priorityLabel() maps ${priority} to "${expected}"`, () => {
      const fixture = createComponent([buildTransaction()]);
      const transaction = buildTransaction({ reviewPriority: priority });

      expect(fixture.componentInstance.priorityLabel(transaction)).toBe(expected);
    });
  }
});
