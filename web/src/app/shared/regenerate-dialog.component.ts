import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

export interface RegenerateDialogData {
  fieldName: string;
  modelName: string;
  originalPrompt: string;
}

export interface RegenerateDialogResult {
  extraInstructions: string;
}

@Component({
  selector: 'app-regenerate-dialog',
  standalone: true,
  imports: [CommonModule, FormsModule, MatDialogModule, MatButtonModule, MatIconModule],
  template: `
    <h2 mat-dialog-title class="flex items-center gap-2">
      <mat-icon class="text-ai-600">auto_awesome</mat-icon>
      Regenerate {{ data.fieldName }}
    </h2>
    <mat-dialog-content class="!min-w-[420px]">
      <div class="text-xs text-slate-500 mb-2">Model: <code>{{ data.modelName }}</code></div>
      <details class="mb-3 text-xs text-slate-600">
        <summary class="cursor-pointer">Original prompt</summary>
        <pre class="whitespace-pre-wrap bg-slate-50 p-2 rounded mt-2">{{ data.originalPrompt || '(no prompt available)' }}</pre>
      </details>
      <label class="block text-sm font-medium mb-1">Extra instructions (optional)</label>
      <textarea rows="4"
                class="w-full border rounded p-2 text-sm"
                placeholder="e.g. make it more concise, emphasise cloud experience"
                [(ngModel)]="extra"
                data-testid="regen-extra"></textarea>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="cancel()">Cancel</button>
      <button mat-raised-button color="primary" data-testid="regen-submit"
              (click)="submit()">Regenerate</button>
    </mat-dialog-actions>
  `,
})
export class RegenerateDialogComponent {
  protected ref = inject<MatDialogRef<RegenerateDialogComponent, RegenerateDialogResult | null>>(MatDialogRef);
  protected data: RegenerateDialogData = inject(MAT_DIALOG_DATA);
  extra = '';

  cancel() { this.ref.close(null); }
  submit() { this.ref.close({ extraInstructions: this.extra }); }
}
