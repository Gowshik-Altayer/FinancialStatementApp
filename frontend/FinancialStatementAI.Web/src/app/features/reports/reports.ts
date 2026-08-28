import { Component, inject, signal } from '@angular/core';
import { HttpResponse } from '@angular/common/http';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ReportService } from '../../core/services/report.service';
import { NotificationService } from '../../core/services/notification.service';
import { LoadingOverlayService } from '../../core/services/loading-overlay.service';
import { PageHeader } from '../../shared/components/page-header/page-header';
import { ReportArea, ReportAreaOption, ReportFileFormat } from '../../shared/models/report.model';

/// <summary>Reports export — one XLSX/PDF download per data area (Statements, Transactions,
/// Review, Reconciliation, Categories), each scoped server-side to the current user exactly like
/// that area's own list page. Deliberately no filter UI here: every report always covers the
/// user's full data set for that area (see ReportGenerationService's FetchAllPagesAsync) —
/// filtering a report before download is a follow-up, not part of this first cut.</summary>
@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [MatCardModule, MatButtonModule, MatButtonToggleModule, MatIconModule, MatProgressSpinnerModule, PageHeader],
  templateUrl: './reports.html',
  styleUrl: './reports.scss'
})
export class Reports {
  private readonly reportService = inject(ReportService);
  private readonly notifications = inject(NotificationService);
  private readonly loadingOverlay = inject(LoadingOverlayService);

  readonly areas: ReportAreaOption[] = [
    { area: 'statements', label: 'Statements', description: 'Every uploaded statement, its processing status, and reconciliation status.', icon: 'description' },
    { area: 'transactions', label: 'Transactions', description: 'All transactions across every statement, with category and classification confidence.', icon: 'receipt_long' },
    { area: 'review', label: 'Review Queue', description: 'Transactions on statements still awaiting human review, lowest confidence first.', icon: 'fact_check' },
    { area: 'reconciliation', label: 'Reconciliation', description: 'The current reconciliation result for every statement that has been reconciled.', icon: 'balance' },
    { area: 'categories', label: 'Categories', description: 'The category taxonomy plus your own per-category transaction counts and spend.', icon: 'category' }
  ];

  readonly selectedFormat = signal<Record<ReportArea, ReportFileFormat>>({
    statements: 'xlsx',
    transactions: 'xlsx',
    review: 'xlsx',
    reconciliation: 'xlsx',
    categories: 'xlsx'
  });

  readonly downloadingArea = signal<ReportArea | null>(null);

  setFormat(area: ReportArea, format: ReportFileFormat): void {
    this.selectedFormat.update((current) => ({ ...current, [area]: format }));
  }

  download(area: ReportArea): void {
    if (this.downloadingArea()) {
      return;
    }

    const format = this.selectedFormat()[area];
    this.downloadingArea.set(area);
    this.loadingOverlay.show();

    this.reportService.download(area, format).subscribe({
      next: (response) => {
        this.saveResponseAsFile(response, area, format);
        this.downloadingArea.set(null);
        this.loadingOverlay.hide();
      },
      error: () => {
        this.notifications.error('Could not generate that report. Please try again.');
        this.downloadingArea.set(null);
        this.loadingOverlay.hide();
      }
    });
  }

  private saveResponseAsFile(response: HttpResponse<Blob>, area: ReportArea, format: ReportFileFormat): void {
    const blob = response.body;
    if (!blob) {
      this.notifications.error('The server returned an empty report.');
      return;
    }

    const fileName = this.extractFileName(response.headers.get('content-disposition')) ?? `${area}-report.${format}`;
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }

  private extractFileName(contentDisposition: string | null): string | null {
    if (!contentDisposition) {
      return null;
    }
    const match = /filename="?([^";]+)"?/i.exec(contentDisposition);
    return match?.[1] ?? null;
  }
}
