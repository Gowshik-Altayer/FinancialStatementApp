import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { HttpErrorResponse } from '@angular/common/http';
import { StatementService } from '../../../core/services/statement.service';
import { PageHeader } from '../../../shared/components/page-header/page-header';

const ALLOWED_EXTENSIONS = ['.pdf', '.jpg', '.jpeg', '.png'];
const MAX_SIZE_BYTES = 20 * 1024 * 1024;

@Component({
  selector: 'app-statement-upload',
  standalone: true,
  imports: [CommonModule, MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule, PageHeader],
  templateUrl: './statement-upload.html',
  styleUrl: './statement-upload.scss'
})
export class StatementUpload {
  private readonly statementService = inject(StatementService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly router = inject(Router);

  readonly selectedFile = signal<File | null>(null);
  readonly previewUrl = signal<SafeResourceUrl | null>(null);
  readonly isImagePreview = signal(false);
  readonly isDragOver = signal(false);
  readonly isUploading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(): void {
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragOver.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) {
      this.setFile(file);
    }
  }

  onFileSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (file) {
      this.setFile(file);
    }
  }

  private setFile(file: File): void {
    this.errorMessage.set(null);

    const extension = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!ALLOWED_EXTENSIONS.includes(extension)) {
      this.errorMessage.set('Unsupported file type. Allowed types: PDF, JPG, JPEG, PNG.');
      return;
    }
    if (file.size > MAX_SIZE_BYTES) {
      this.errorMessage.set('File exceeds the maximum allowed size of 20 MB.');
      return;
    }

    this.selectedFile.set(file);
    const objectUrl = URL.createObjectURL(file);
    this.previewUrl.set(this.sanitizer.bypassSecurityTrustResourceUrl(objectUrl));
    this.isImagePreview.set(file.type.startsWith('image/'));
  }

  clearSelection(): void {
    this.selectedFile.set(null);
    this.previewUrl.set(null);
    this.errorMessage.set(null);
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  submit(): void {
    const file = this.selectedFile();
    if (!file) {
      return;
    }

    this.isUploading.set(true);
    this.errorMessage.set(null);

    this.statementService.upload(file).subscribe({
      next: (statement) => {
        this.isUploading.set(false);
        this.router.navigate(['/statements', statement.id]);
      },
      error: (error: HttpErrorResponse) => {
        this.isUploading.set(false);
        this.errorMessage.set(error.error?.detail ?? 'Upload failed. Please try again.');
      }
    });
  }
}
