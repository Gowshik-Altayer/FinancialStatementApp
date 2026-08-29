export type TransactionTypeFilter = 'Debit' | 'Credit' | 'Payment' | 'Refund' | 'Purchase' | 'Transfer' | 'Other';

export interface TransactionQuery {
  search?: string;
  merchant?: string;
  categoryId?: string;
  statementId?: string;
  dateFrom?: string; // yyyy-MM-dd
  dateTo?: string; // yyyy-MM-dd
  amountMin?: number;
  amountMax?: number;
  transactionType?: TransactionTypeFilter;
  processingStatus?: string;
  minConfidence?: number; // 0..1
  reviewPriority?: 'HighConfidence' | 'ReviewRecommended' | 'ReviewRequired';
  hasBeenCorrected?: boolean;
  page?: number;
  pageSize?: number;
}

export interface TransactionSummary {
  totalCount: number;
  highConfidenceCount: number;
  needingReviewCount: number;
  correctedCount: number;
}
