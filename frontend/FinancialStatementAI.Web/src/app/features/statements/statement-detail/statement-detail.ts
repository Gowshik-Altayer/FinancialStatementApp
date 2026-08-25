import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatButtonModule } from '@angular/material/button';
import { StatementService } from '../../../core/services/statement.service';
import { StatementDetail as StatementDetailModel } from '../../../shared/models/statement.model';

@Component({
  selector: 'app-statement-detail',
  standalone: true,
  imports: [CommonModule, RouterLink, MatCardModule, MatChipsModule, MatProgressSpinnerModule, MatButtonModule],
  templateUrl: './statement-detail.html',
  styleUrl: './statement-detail.scss'
})
export class StatementDetail implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly statementService = inject(StatementService);

  readonly statement = signal<StatementDetailModel | null>(null);
  readonly isLoading = signal(true);
  readonly notFound = signal(false);

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id')!;
    this.statementService.getById(id).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.isLoading.set(false);
      },
      error: () => {
        this.notFound.set(true);
        this.isLoading.set(false);
      }
    });
  }
}
