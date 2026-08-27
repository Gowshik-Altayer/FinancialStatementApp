export interface TransactionQuery {
  search?: string;
  categoryId?: string;
  statementId?: string;
  dateFrom?: string; // yyyy-MM-dd
  dateTo?: string; // yyyy-MM-dd
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
