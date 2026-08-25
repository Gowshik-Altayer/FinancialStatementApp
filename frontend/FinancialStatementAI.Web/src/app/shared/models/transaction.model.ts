export type ReviewPriority = 'HighConfidence' | 'ReviewRecommended' | 'ReviewRequired';

export interface TransactionCorrection {
  id: string;
  fieldName: string;
  originalValue: string | null;
  correctedValue: string;
  correctedByUserName: string | null;
  correctedAt: string;
  correctionReason: string | null;
}

export interface Transaction {
  id: string;
  statementId: string;
  statementFileName: string | null;
  transactionDate: string | null;
  postingDate: string | null;
  description: string;
  merchant: string | null;
  referenceNumber: string | null;
  debitAmount: number | null;
  creditAmount: number | null;
  amount: number | null;
  currency: string | null;
  transactionType: string;
  categoryId: string | null;
  categoryName: string | null;
  classificationConfidence: number | null;
  classificationMethod: string | null;
  classificationReason: string | null;
  reviewPriority: ReviewPriority | null;
  hasBeenCorrected: boolean;
  isPotentialDuplicate: boolean;
  duplicateOfTransactionId: string | null;
  corrections: TransactionCorrection[];
}
