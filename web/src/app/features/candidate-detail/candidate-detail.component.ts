import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { CandidatesService } from '../../core/candidates.service';
import { AI_FIELD_LABELS, AiHistoryEntry, CandidateDetail } from '../../core/models';
import { AiFieldComponent } from '../../shared/ai-field.component';
import { RegenerateDialogComponent, RegenerateDialogData, RegenerateDialogResult } from '../../shared/regenerate-dialog.component';

@Component({
  selector: 'app-candidate-detail',
  standalone: true,
  imports: [
    CommonModule, RouterLink,
    MatButtonModule, MatIconModule, MatCardModule, MatExpansionModule,
    MatChipsModule, MatDividerModule, MatTooltipModule, MatSidenavModule,
    MatDialogModule, MatSnackBarModule,
    AiFieldComponent,
  ],
  templateUrl: './candidate-detail.component.html',
})
export class CandidateDetailComponent implements OnInit {
  protected readonly svc = inject(CandidatesService);
  protected readonly route = inject(ActivatedRoute);
  protected readonly dialog = inject(MatDialog);
  protected readonly snack = inject(MatSnackBar);

  readonly busy = signal(false);
  readonly regenerated = signal<Set<string>>(new Set());
  readonly history = signal<AiHistoryEntry[] | null>(null);
  readonly historyField = signal<string | null>(null);

  readonly years = computed(() => {
    const c = this.svc.current();
    return c ? c.workExperiences.length : 0;
  });

  readonly aiFields = computed(() => {
    const c = this.svc.current();
    if (!c?.aiFields) return [];
    return [
      { name: 'AiSummary', value: c.aiFields.aiSummary ?? '' },
      { name: 'AiSeniorityLevel', value: c.aiFields.aiSeniorityLevel ?? '' },
      { name: 'AiSeniorityRationale', value: c.aiFields.aiSeniorityRationale ?? '' },
      { name: 'AiTopStrengths', value: this.tryFormat(c.aiFields.aiTopStrengths) },
      { name: 'AiSkillCategories', value: this.tryFormat(c.aiFields.aiSkillCategories) },
      { name: 'AiYearsExperienceEstimate', value: String(c.aiFields.aiYearsExperienceEstimate ?? '') },
      { name: 'AiSuggestedRoles', value: this.tryFormat(c.aiFields.aiSuggestedRoles) },
      { name: 'AiInterviewFocusAreas', value: this.tryFormat(c.aiFields.aiInterviewFocusAreas) },
    ];
  });

  label(name: string) { return AI_FIELD_LABELS[name] ?? name; }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) this.svc.loadDetail(id);
  }

  isUserEdited(field: string): boolean {
    const c = this.svc.current();
    return !!c?.aiOverrides.find((o) => o.fieldName === field)?.isUserEdited;
  }

  wasRegenerated(field: string): boolean {
    return this.regenerated().has(field);
  }

  canRevert(field: string): boolean {
    const c = this.svc.current();
    const ov = c?.aiOverrides.find((o) => o.fieldName === field);
    return !!ov?.isUserEdited && !!ov.originalAiValue;
  }

  async openRegenerate(field: string) {
    const c = this.svc.current();
    if (!c) return;
    const dialogRef = this.dialog.open<RegenerateDialogComponent, RegenerateDialogData, RegenerateDialogResult | null>(
      RegenerateDialogComponent, {
        data: {
          fieldName: field,
          modelName: 'stub-gpt-5.4-mini',
          originalPrompt: 'Generate ' + field + ' for candidate ' + c.fullName,
        },
      });
    const result = await dialogRef.afterClosed().toPromise();
    if (!result) return;
    this.busy.set(true);
    try {
      const value = await this.svc.regenerateField(c.id, field, result.extraInstructions || null);
      const r = new Set(this.regenerated());
      r.add(field);
      this.regenerated.set(r);
      this.snack.open(`${this.label(field)} regenerated`, 'OK', { duration: 2500 });
      await this.svc.loadDetail(c.id);
    } finally {
      this.busy.set(false);
    }
  }

  async regenerateAll() {
    const c = this.svc.current();
    if (!c) return;
    if (!confirm('Regenerate all AI fields for this candidate?')) return;
    this.busy.set(true);
    try {
      await this.svc.regenerateAll(c.id);
      const r = new Set<string>(this.aiFields().map((f) => f.name));
      this.regenerated.set(r);
      this.snack.open('All AI fields regenerated', 'OK', { duration: 2500 });
      await this.svc.loadDetail(c.id);
    } finally {
      this.busy.set(false);
    }
  }

  async editField(payload: { field: string; value: string }) {
    const c = this.svc.current();
    if (!c) return;
    this.busy.set(true);
    try {
      await this.svc.editField(c.id, payload.field, payload.value);
      const r = new Set(this.regenerated());
      r.delete(payload.field);
      this.regenerated.set(r);
      await this.svc.loadDetail(c.id);
    } finally {
      this.busy.set(false);
    }
  }

  async revertField(field: string) {
    const c = this.svc.current();
    if (!c) return;
    this.busy.set(true);
    try {
      await this.svc.revertField(c.id, field);
      await this.svc.loadDetail(c.id);
    } finally {
      this.busy.set(false);
    }
  }

  async showHistory(field: string) {
    const c = this.svc.current();
    if (!c) return;
    const rows = await this.svc.fetchHistory(c.id, field);
    this.historyField.set(field);
    this.history.set(rows);
  }

  closeHistory() {
    this.history.set(null);
    this.historyField.set(null);
  }

  async markReviewed() {
    const c = this.svc.current();
    if (!c) return;
    await this.svc.setReviewStatus(c.id, 'Reviewed');
    this.snack.open('Marked as reviewed', 'OK', { duration: 2000 });
  }

  async reject() {
    const c = this.svc.current();
    if (!c) return;
    await this.svc.setReviewStatus(c.id, 'Rejected');
    this.snack.open('Rejected', 'OK', { duration: 2000 });
  }

  private tryFormat(json: string | null | undefined): string {
    if (!json) return '';
    try {
      const v = JSON.parse(json);
      if (Array.isArray(v)) return v.join(', ');
      if (v && typeof v === 'object') {
        return Object.entries(v as Record<string, unknown>)
          .map(([k, vv]) => `${k}: ${Array.isArray(vv) ? (vv as unknown[]).join(', ') : vv}`)
          .join('\n');
      }
      return String(v);
    } catch {
      return json;
    }
  }
}
