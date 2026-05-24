import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatIconModule } from '@angular/material/icon';
import { MatChipsModule } from '@angular/material/chips';
import { MatInputModule } from '@angular/material/input';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatCardModule } from '@angular/material/card';
import { CandidatesService } from '../../core/candidates.service';
import { CandidateSummary, ReviewStatus } from '../../core/models';

type ViewMode = 'table' | 'card';
const VIEW_KEY = 'resumereview.viewMode';

@Component({
  selector: 'app-candidates-list',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterLink,
    MatButtonModule, MatButtonToggleModule, MatIconModule, MatChipsModule,
    MatInputModule, MatFormFieldModule, MatSelectModule,
    MatTableModule, MatSortModule, MatCardModule,
  ],
  templateUrl: './candidates-list.component.html',
})
export class CandidatesListComponent implements OnInit {
  protected readonly svc = inject(CandidatesService);

  readonly view = signal<ViewMode>((localStorage.getItem(VIEW_KEY) as ViewMode) ?? 'table');
  readonly search = signal('');
  readonly statusFilter = signal<ReviewStatus | 'all'>('all');
  readonly seniorityFilter = signal<string | 'all'>('all');
  readonly sortField = signal<keyof CandidateSummary>('updatedAt');
  readonly sortDir = signal<'asc' | 'desc'>('desc');

  readonly displayedColumns = [
    'fullName', 'location', 'aiSeniorityLevel', 'aiYearsExperienceEstimate',
    'topSkill', 'reviewStatus', 'updatedAt'
  ];

  readonly seniorityOptions = ['Junior', 'Mid', 'Senior', 'Staff', 'Principal'];

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    const status = this.statusFilter();
    const sen = this.seniorityFilter();
    let rows = this.svc.candidates().filter((c) => {
      const matchesStatus = status === 'all' || c.reviewStatus === status;
      const matchesSeniority = sen === 'all' || c.aiSeniorityLevel === sen;
      const haystack = [c.fullName, c.email, c.aiSummary ?? '', ...(c.skills ?? [])].join(' ').toLowerCase();
      const matchesSearch = !term || haystack.includes(term);
      return matchesStatus && matchesSeniority && matchesSearch;
    });
    const field = this.sortField();
    const dir = this.sortDir() === 'asc' ? 1 : -1;
    rows = [...rows].sort((a, b) => {
      const av = (a[field] ?? '') as string | number;
      const bv = (b[field] ?? '') as string | number;
      return av > bv ? dir : av < bv ? -dir : 0;
    });
    return rows;
  });

  ngOnInit(): void {
    this.svc.loadAll();
  }

  setView(v: ViewMode) {
    this.view.set(v);
    localStorage.setItem(VIEW_KEY, v);
  }

  onSort(s: Sort) {
    if (!s.active || !s.direction) {
      this.sortField.set('updatedAt');
      this.sortDir.set('desc');
      return;
    }
    this.sortField.set(s.active as keyof CandidateSummary);
    this.sortDir.set(s.direction as 'asc' | 'desc');
  }

  reviewBadgeClass(s: ReviewStatus) {
    return s === 'Reviewed' ? 'bg-emerald-100 text-emerald-700'
         : s === 'Rejected' ? 'bg-rose-100 text-rose-700'
         : 'bg-slate-100 text-slate-700';
  }
}
