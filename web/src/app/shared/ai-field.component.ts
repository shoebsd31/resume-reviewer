import { Component, EventEmitter, Input, Output, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';

export type AiBadge = 'AI-generated' | 'Modified by user' | 'Regenerated';

@Component({
  selector: 'app-ai-field',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatButtonModule, MatTooltipModule],
  template: `
    <section
      class="ai-field rounded-md bg-white p-4 ai-field-border ai-glow"
      [attr.data-field]="fieldName"
      [attr.data-badge]="badge()">
      <header class="flex items-center justify-between gap-3 mb-2">
        <h3 class="flex items-center gap-2 text-sm font-semibold text-ai-700">
          <mat-icon class="!text-ai-600">auto_awesome</mat-icon>
          <span>{{ label }}</span>
          <span class="ml-2 inline-flex items-center text-xs font-medium px-2 py-0.5 rounded-full"
                [ngClass]="badgeClasses()"
                [attr.data-testid]="'ai-badge-' + fieldName">{{ badge() }}</span>
        </h3>
        <div class="flex gap-2">
          <button mat-stroked-button color="primary"
                  type="button"
                  (click)="onRegenerate.emit(fieldName)"
                  [disabled]="busy"
                  [attr.data-testid]="'regen-' + fieldName">
            <mat-icon>refresh</mat-icon>
            Regenerate
          </button>
          @if (!editing()) {
            <button mat-button type="button" (click)="startEdit()">
              <mat-icon>edit</mat-icon>
              Edit
            </button>
          }
          @if (canRevert) {
            <button mat-button type="button" (click)="onRevert.emit(fieldName)"
                    [attr.data-testid]="'revert-' + fieldName">
              Revert to AI value
            </button>
          }
          <button mat-button type="button" (click)="onShowHistory.emit(fieldName)">
            <mat-icon>history</mat-icon>
            History
          </button>
        </div>
      </header>

      @if (!editing()) {
        <p class="whitespace-pre-wrap text-sm text-slate-800" [attr.data-testid]="'ai-value-' + fieldName">{{ value || '—' }}</p>
      } @else {
        <textarea
          rows="4"
          class="w-full border rounded p-2 text-sm"
          [(ngModel)]="draft"
          [attr.data-testid]="'edit-' + fieldName"></textarea>
        <div class="flex justify-end gap-2 mt-2">
          <button mat-button type="button" (click)="cancelEdit()">Cancel</button>
          <button mat-raised-button color="primary" type="button" (click)="saveEdit()"
                  [attr.data-testid]="'save-' + fieldName">Save</button>
        </div>
      }
    </section>
  `,
})
export class AiFieldComponent {
  @Input({ required: true }) fieldName!: string;
  @Input({ required: true }) label!: string;
  @Input() value: string | null = '';
  @Input() isUserEdited = false;
  @Input() wasRegenerated = false;
  @Input() canRevert = false;
  @Input() busy = false;

  @Output() onRegenerate = new EventEmitter<string>();
  @Output() onEdit = new EventEmitter<{ field: string; value: string }>();
  @Output() onRevert = new EventEmitter<string>();
  @Output() onShowHistory = new EventEmitter<string>();

  readonly editing = signal(false);
  draft = '';

  badge(): AiBadge {
    if (this.isUserEdited) return 'Modified by user';
    if (this.wasRegenerated) return 'Regenerated';
    return 'AI-generated';
  }

  badgeClasses() {
    const b = this.badge();
    if (b === 'Modified by user') return 'bg-amber-100 text-amber-800';
    if (b === 'Regenerated') return 'bg-emerald-100 text-emerald-800';
    return 'bg-ai-100 text-ai-700';
  }

  startEdit() {
    this.draft = this.value ?? '';
    this.editing.set(true);
  }

  cancelEdit() {
    this.editing.set(false);
  }

  saveEdit() {
    this.editing.set(false);
    this.onEdit.emit({ field: this.fieldName, value: this.draft });
  }
}
