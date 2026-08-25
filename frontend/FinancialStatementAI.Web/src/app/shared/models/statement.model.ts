export type ProcessingStatus =
  | 'Uploaded'
  | 'Processing'
  | 'ExtractionFailed'
  | 'ExtractionComplete'
  | 'ClassificationComplete'
  | 'PendingReview'
  | 'Verified';

export interface StatementSummary {
  id: string;
  originalFileName: string;
  providerName: string | null;
  statementPeriodStart: string | null;
  statementPeriodEnd: string | null;
  transactionCount: number;
  totalDebits: number | null;
  totalCredits: number | null;
  processingStatus: ProcessingStatus;
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
}

export interface StatementStatus {
  id: string;
  processingStatus: ProcessingStatus;
  uploadedAt: string;
  processedAt: string | null;
}
