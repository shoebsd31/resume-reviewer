import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  AiHistoryEntry,
  CandidateDetail,
  CandidateSummary,
  ReviewStatus,
} from './models';

@Injectable({ providedIn: 'root' })
export class CandidatesService {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  readonly candidates = signal<CandidateSummary[]>([]);
  readonly current = signal<CandidateDetail | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  readonly reviewedCount = computed(
    () => this.candidates().filter((c) => c.reviewStatus === 'Reviewed').length
  );

  async loadAll(): Promise<void> {
    this.loading.set(true);
    try {
      const data = await firstValueFrom(this.http.get<CandidateSummary[]>(`${this.base}/api/candidates`));
      this.candidates.set(data ?? []);
      this.error.set(null);
    } catch (e: unknown) {
      this.error.set((e as Error)?.message ?? 'load_failed');
    } finally {
      this.loading.set(false);
    }
  }

  async loadDetail(id: string): Promise<CandidateDetail | null> {
    this.loading.set(true);
    try {
      const d = await firstValueFrom(this.http.get<CandidateDetail>(`${this.base}/api/candidates/${id}`));
      this.current.set(d);
      return d;
    } catch (e: unknown) {
      this.error.set((e as Error)?.message ?? 'load_failed');
      return null;
    } finally {
      this.loading.set(false);
    }
  }

  async setReviewStatus(id: string, status: ReviewStatus): Promise<void> {
    await firstValueFrom(
      this.http.post<{ id: string; status: ReviewStatus }>(`${this.base}/api/candidates/${id}/review`, {
        status,
        updatedBy: 'user',
      })
    );
    this.candidates.update((rows) => rows.map((c) => (c.id === id ? { ...c, reviewStatus: status } : c)));
    const cur = this.current();
    if (cur && cur.id === id) this.current.set({ ...cur, reviewStatus: status });
  }

  async regenerateField(id: string, fieldName: string, extraInstructions: string | null): Promise<string> {
    const resp = await firstValueFrom(
      this.http.post<{ newValue: string; historyId: string }>(
        `${this.base}/api/candidates/${id}/ai-fields/${fieldName}/regenerate`,
        { extraInstructions, requestedBy: 'user' }
      )
    );
    return resp.newValue;
  }

  async regenerateAll(id: string): Promise<void> {
    await firstValueFrom(
      this.http.post<{ id: string; status: string }>(`${this.base}/api/candidates/${id}/ai-fields/regenerate-all`, {})
    );
  }

  async editField(id: string, fieldName: string, value: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${this.base}/api/candidates/${id}/ai-fields/${fieldName}/edit`, { value, updatedBy: 'user' })
    );
  }

  async revertField(id: string, fieldName: string): Promise<void> {
    await firstValueFrom(
      this.http.post(`${this.base}/api/candidates/${id}/ai-fields/${fieldName}/revert`, {})
    );
  }

  async fetchHistory(id: string, fieldName: string): Promise<AiHistoryEntry[]> {
    return firstValueFrom(
      this.http.get<AiHistoryEntry[]>(`${this.base}/api/candidates/${id}/ai-fields/${fieldName}/history`)
    );
  }

  async fetchReport(): Promise<CandidateDetail[]> {
    return firstValueFrom(this.http.get<CandidateDetail[]>(`${this.base}/api/report`));
  }
}
