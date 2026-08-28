/// <summary>Matches the five GET /api/reports/{area} route segments — StatementsController,
/// TransactionsController's review-queue, ReconciliationController, and CategoriesController are
/// the underlying data each one reports on.</summary>
export type ReportArea = 'statements' | 'transactions' | 'review' | 'reconciliation' | 'categories';

export type ReportFileFormat = 'xlsx' | 'pdf';

export interface ReportAreaOption {
  area: ReportArea;
  label: string;
  description: string;
  icon: string;
}
