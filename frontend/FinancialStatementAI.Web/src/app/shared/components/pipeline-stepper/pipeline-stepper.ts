import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

export type PipelineStageState = 'pending' | 'in-progress' | 'complete' | 'failed';

export interface PipelineStageViewModel {
  key: string;
  label: string;
  count: number;
  state: PipelineStageState;
}

const STAGE_ICONS: Record<string, string> = {
  upload: 'upload_file',
  'text-extraction': 'description',
  ocr: 'document_scanner',
  'transaction-extraction': 'receipt_long',
  'ai-classification': 'psychology',
  review: 'fact_check',
  reconciliation: 'balance',
  completed: 'task_alt'
};

/** Renders the statement-processing pipeline as a horizontal (desktop) / scrollable (mobile)
 * sequence of stages, each showing a count and completion state, clickable where a caller wants
 * to navigate to that stage's data (e.g. clicking "Review" on the Dashboard jumps to /review). */
@Component({
  selector: 'app-pipeline-stepper',
  standalone: true,
  imports: [MatIconModule, MatTooltipModule],
  templateUrl: './pipeline-stepper.html',
  styleUrl: './pipeline-stepper.scss'
})
export class PipelineStepper {
  @Input({ required: true }) stages: PipelineStageViewModel[] = [];
  @Output() stageClick = new EventEmitter<PipelineStageViewModel>();

  iconFor(key: string): string {
    return STAGE_ICONS[key] ?? 'radio_button_unchecked';
  }
}
