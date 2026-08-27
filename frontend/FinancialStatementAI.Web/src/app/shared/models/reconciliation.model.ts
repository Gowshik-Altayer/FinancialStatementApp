export type ReconciliationResultStatus = 'Reconciled' | 'Mismatch' | 'InsufficientInformation';

export interface ReconciliationSummary {
  statementId: string;
  statementFileName: string;
  openingBalance: number | null;
  totalCredits: number | null;
  totalDebits: number | null;
  expectedClosingBalance: number | null;
  statementClosingBalance: number | null;
  discrepancy: number | null;
  status: ReconciliationResultStatus;
  notes: string | null;
  createdAt: string;
}

export interface ReconciliationSummaryCounts {
  reconciledCount: number;
  mismatchCount: number;
  insufficientInformationCount: number;
  pendingCount: number;
  totalDiscrepancyAmount: number;
}

export interface ReconciliationQuery {
  status?: ReconciliationResultStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}
