import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { CategoryDetail } from '../../../shared/models/category.model';

export interface CategoryFormDialogData {
  category?: CategoryDetail;
}

export interface CategoryFormDialogResult {
  name: string;
  description: string | null;
}

/** Admin-only create/edit form for a single category (requirement 10). A dialog rather than an
 * inline row-expansion — unlike Reconciliation's read-only expand, this collects input that must
 * be validated and can be cancelled, which a dialog communicates more clearly than an inline form
 * competing for space with the card grid. */
@Component({
  selector: 'app-category-form-dialog',
  standalone: true,
  imports: [FormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  templateUrl: './category-form-dialog.html',
  styleUrl: './category-form-dialog.scss'
})
export class CategoryFormDialog {
  name: string;
  description: string;
  readonly isEdit: boolean;

  constructor(
    private readonly dialogRef: MatDialogRef<CategoryFormDialog, CategoryFormDialogResult>,
    @Inject(MAT_DIALOG_DATA) data: CategoryFormDialogData
  ) {
    this.isEdit = !!data.category;
    this.name = data.category?.name ?? '';
    this.description = data.category?.description ?? '';
  }

  get isValid(): boolean {
    return this.name.trim().length > 0;
  }

  save(): void {
    if (!this.isValid) return;
    this.dialogRef.close({ name: this.name.trim(), description: this.description.trim() || null });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
