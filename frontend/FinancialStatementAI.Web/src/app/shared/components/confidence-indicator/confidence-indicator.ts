import { Component, Input } from '@angular/core';
import { StatusTone } from '../../utils/status-tone.util';

// Mirrors the backend's ClassificationConfidenceThresholds.cs (HighConfidenceMinimum = 0.80,
// ReviewRecommendedMinimum = 0.60) — kept in sync manually since confidence bucketing is
// display-only logic here; the backend's TransactionResponse.reviewPriority is the source of
// truth wherever it's already present (this component's `priority` input, when supplied,
// always wins over recomputing the bucket from `score` — see priorityOrComputed below).
const HIGH_CONFIDENCE_MIN = 0.8;
const REVIEW_RECOMMENDED_MIN = 0.6;

/** Renders a 0–1 confidence score as a labeled bar + percentage. Accepts either a `priority`
 * string (the backend's already-computed reviewPriority, preferred when available) or just a
 * raw `score` to bucket client-side — used both where the API supplies reviewPriority
 * (transactions) and where only a raw score exists (e.g. a dashboard's average-confidence KPI). */
@Component({
  selector: 'app-confidence-indicator',
  standalone: true,
  templateUrl: './confidence-indicator.html',
  styleUrl: './confidence-indicator.scss'
})
export class ConfidenceIndicator {
  @Input({ required: true }) score = 0; // 0..1
  @Input() priority?: string | null;
  @Input() compact = false;

  get tone(): StatusTone {
    const bucket = this.priority ?? this.computedBucket();
    switch (bucket) {
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

  get label(): string {
    const bucket = this.priority ?? this.computedBucket();
    switch (bucket) {
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

  get percentDisplay(): number {
    return Math.round(this.score * 100);
  }

  private computedBucket(): string {
    if (this.score >= HIGH_CONFIDENCE_MIN) return 'HighConfidence';
    if (this.score >= REVIEW_RECOMMENDED_MIN) return 'ReviewRecommended';
    return 'ReviewRequired';
  }
}
