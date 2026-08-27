/** The five-color status vocabulary every status-driven UI piece in this app maps onto —
 * see src/styles/_tokens.scss for the actual colors (--fsai-success, --fsai-warning, etc.).
 * Kept as a small named type (not a raw string) so `status-badge`/`confidence-indicator`
 * consumers get autocomplete/type-checking on the only five values that mean anything visually. */
export type StatusTone = 'success' | 'warning' | 'danger' | 'info' | 'neutral';

/** Maps a Statement.processingStatus value (see statement.model.ts's ProcessingStatus) to a tone.
 * Kept here rather than inside status-badge itself so the badge component stays generic and
 * every page/table that renders a processing status agrees on the same color meaning. */
export function processingStatusTone(status: string): StatusTone {
  switch (status) {
    case 'Verified':
    case 'ClassificationComplete':
    case 'ExtractionComplete':
      return 'success';
    case 'PendingReview':
      return 'warning';
    case 'ExtractionFailed':
      return 'danger';
    case 'Processing':
      return 'info';
    case 'Uploaded':
    default:
      return 'neutral';
  }
}

/** Maps a ReconciliationStatus value to a tone. */
export function reconciliationStatusTone(status: string | null | undefined): StatusTone {
  switch (status) {
    case 'Reconciled':
      return 'success';
    case 'Mismatch':
      return 'danger';
    case 'InsufficientInformation':
      return 'warning';
    default:
      return 'neutral';
  }
}

/** Maps a Transaction.reviewPriority value to a tone — mirrors the same 0.80/0.60 confidence
 * thresholds the backend's TransactionResponse mapper already applies (see
 * ClassificationConfidenceThresholds.cs), just translated to a display color here. */
export function reviewPriorityTone(priority: string | null | undefined): StatusTone {
  switch (priority) {
    case 'HighConfidence':
      return 'success';
    case 'ReviewRecommended':
      return 'warning';
    case 'ReviewRequired':
      return 'danger';
    default:
      return 'neutral';
  }
}

/** Human-readable label for a review priority — the raw enum values read awkwardly in the UI
 * (e.g. "ReviewRequired"). Centralized here since both status-badge consumers and any future
 * chart legend need the same wording. */
export function reviewPriorityLabel(priority: string | null | undefined): string {
  switch (priority) {
    case 'HighConfidence':
      return 'High confidence';
    case 'ReviewRecommended':
      return 'Review recommended';
    case 'ReviewRequired':
      return 'Review required';
    default:
      return 'Unclassified';
  }
}

/** Human-readable label for a processing status — splits PascalCase into words
 * ("PendingReview" -> "Pending Review") rather than hand-listing every enum value, so a newly
 * added status still renders sensibly without a code change here. */
export function processingStatusLabel(status: string): string {
  return status.replace(/([a-z])([A-Z])/g, '$1 $2');
}
