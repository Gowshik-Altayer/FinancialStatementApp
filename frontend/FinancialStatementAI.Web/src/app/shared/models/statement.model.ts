export type ProcessingStatus =
  | 'Uploaded'
  | 'Processing'
  | 'ExtractionFailed'
  | 'ExtractionComplete'
  | 'ClassificationComplete'
  | 'PendingReview'
  | 'Verified';

export type ReconciliationStatus = 'Reconciled' | 'Mismatch' | 'InsufficientInformation';

export interface StatementSummary {
  id: string;
  originalFileName: string;
  providerName: string | null;
  accountHolderName: string | null;
  accountNumberMasked: string | null;
  statementPeriodStart: string | null;
  statementPeriodEnd: string | null;
  transactionCount: number;
  totalDebits: number | null;
  totalCredits: number | null;
  processingStatus: ProcessingStatus;
  reconciliationStatus: ReconciliationStatus | null;
  uploadedAt: string;
}

export interface StatementDetail {
  id: string;
  originalFileName: string;
  contentType: string;
  fileSizeBytes: number;
  documentType: string;
  accountHolderName: string | null;
  providerName: string | null;
  accountNumberMasked: string | null;
  statementPeriodStart: string | null;
  statementPeriodEnd: string | null;
  statementDate: string | null;
  openingBalance: number | null;
  closingBalance: number | null;
  totalDebits: number | null;
  totalCredits: number | null;
  totalPayments: number | null;
  totalPurchases: number | null;
  currency: string | null;
  processingStatus: ProcessingStatus;
  uploadedAt: string;
  processedAt: string | null;
  transactionCount: number;
  hasUsableText: boolean | null;
  extractedPageCount: number | null;
  extractionMethod: string | null;
  reconciliationStatus: ReconciliationStatus | null;
  extractionConfidence: number | null;
  isLowQualityExtraction: boolean;
}

export interface StatementStatus {
  id: string;
  processingStatus: ProcessingStatus;
  uploadedAt: string;
  processedAt: string | null;
}
