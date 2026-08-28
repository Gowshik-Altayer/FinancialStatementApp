import { Injectable, inject } from '@angular/core';
import { MatSnackBar, MatSnackBarConfig } from '@angular/material/snack-bar';

/** Thin wrapper over MatSnackBar so every toast in the app agrees on tone, placement and
 * duration. Before this, each of ~18 call sites passed its own duration and action label
 * ad hoc (2500/3000/4000ms, 'Dismiss' or undefined) and none carried a tone, so a failure and a
 * success looked identical. The `fsai-snack-*` panel classes are defined in
 * styles/_components.scss. */
@Injectable({ providedIn: 'root' })
export class NotificationService {
  private readonly snackBar = inject(MatSnackBar);

  private readonly base: MatSnackBarConfig = {
    horizontalPosition: 'right',
    verticalPosition: 'bottom'
  };

  success(message: string): void {
    this.snackBar.open(message, 'Dismiss', { ...this.base, duration: 3000, panelClass: 'fsai-snack-success' });
  }

  warning(message: string): void {
    this.snackBar.open(message, 'Dismiss', { ...this.base, duration: 4000, panelClass: 'fsai-snack-warning' });
  }

  /** Errors stay on screen longer — they're the ones a user actually needs time to read. */
  error(message: string): void {
    this.snackBar.open(message, 'Dismiss', { ...this.base, duration: 5000, panelClass: 'fsai-snack-danger' });
  }
}
