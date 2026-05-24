import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { NgxChartsModule, ScaleType } from '@swimlane/ngx-charts';
import { CandidatesService } from '../../core/candidates.service';
import { CandidateDetail } from '../../core/models';
import { computeReportData, LeaderboardEntry } from './report.data';

@Component({
  selector: 'app-report',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    MatCardModule, MatButtonModule, MatIconModule, MatChipsModule,
    MatSelectModule, MatFormFieldModule,
    NgxChartsModule,
  ],
  templateUrl: './report.component.html',
})
export class ReportComponent implements OnInit {
  protected readonly svc = inject(CandidatesService);

  readonly candidates = signal<CandidateDetail[]>([]);
  readonly loading = signal(true);
  readonly compareIds = signal<string[]>([]);
  readonly expandedLeaderboardId = signal<string | null>(null);
  readonly scheme: { domain: string[]; group: ScaleType; selectable: boolean; name: string } = {
    domain: ['#7c3aed', '#06b6d4', '#10b981', '#f59e0b', '#ef4444', '#8b5cf6', '#3b82f6'],
    group: ScaleType.Ordinal,
    selectable: true,
    name: 'ai',
  };

  readonly data = computed(() => computeReportData(this.candidates()));

  readonly comparison = computed(() => {
    const ids = new Set(this.compareIds());
    return this.candidates().filter((c) => ids.has(c.id));
  });

  async ngOnInit() {
    this.loading.set(true);
    try {
      const rows = await this.svc.fetchReport();
      this.candidates.set(rows);
      // Default-compare top two
      this.compareIds.set(rows.slice(0, 2).map((r) => r.id));
    } finally {
      this.loading.set(false);
    }
  }

  toggleExpanded(id: string) {
    this.expandedLeaderboardId.set(this.expandedLeaderboardId() === id ? null : id);
  }

  trackById(_: number, item: LeaderboardEntry) {
    return item.id;
  }
}
