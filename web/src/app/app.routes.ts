import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', redirectTo: 'candidates', pathMatch: 'full' },
  {
    path: 'candidates',
    loadComponent: () =>
      import('./features/candidates-list/candidates-list.component').then((m) => m.CandidatesListComponent),
  },
  {
    path: 'candidates/:id',
    loadComponent: () =>
      import('./features/candidate-detail/candidate-detail.component').then((m) => m.CandidateDetailComponent),
  },
  {
    path: 'report',
    loadComponent: () =>
      import('./features/report/report.component').then((m) => m.ReportComponent),
  },
  { path: '**', redirectTo: 'candidates' },
];
