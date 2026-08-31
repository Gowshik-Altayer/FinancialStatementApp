import { Component, Inject } from '@angular/core';
import { DatePipe } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { TransactionCorrection } from '../../models/transaction.model';

export interface TransactionHistoryDialogData {
  corrections: TransactionCorrection[];
}

/// <summary>Read-only correction history for one transaction (requirement #9's audit trail),
/// shown as a dialog rather than an in-grid expand row — AG Grid Community has no built-in
/// expandable-row feature (that's Master/Detail, Enterprise-only), and a dialog is arguably
/// clearer here anyway since it doesn't compete for vertical space with the grid. Shared (not
/// feature-scoped) since the Transactions, Review, and Statement Detail pages all open it.</summary>
@Component({
  selector: 'app-transaction-history-dialog',
  standalone: true,
  imports: [DatePipe, MatDialogModule, MatButtonModule],
  templateUrl: './transaction-history-dialog.html',
  styleUrl: './transaction-history-dialog.scss'
})
export class TransactionHistoryDialog {
  constructor(@Inject(MAT_DIALOG_DATA) public data: TransactionHistoryDialogData) {}
}
