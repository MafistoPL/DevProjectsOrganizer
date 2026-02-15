import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type TagHeuristicsRunState = 'Running' | 'Completed' | 'Failed';

export type TagHeuristicsRun = {
  runId: string;
  projectId: string;
  projectName: string;
  state: TagHeuristicsRunState;
  progress: number;
  message: string;
  startedAt: string;
  finishedAt: string | null;
  generatedCount: number | null;
};

type HostTagHeuristicsProgressPayload = {
  runId?: string;
  projectId?: string;
  projectName?: string;
  state?: string;
  progress?: number;
  message?: string;
  startedAt?: string;
  finishedAt?: string | null;
  generatedCount?: number | null;
};

@Injectable({ providedIn: 'root' })
export class TagHeuristicsRunsService {
  private readonly runsSubject = new BehaviorSubject<TagHeuristicsRun[]>([]);
  readonly runs$ = this.runsSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    this.bridge.events$.subscribe((event) => {
      if (event?.type !== 'tagHeuristics.progress') {
        return;
      }

      this.upsertFromEvent(event.data as HostTagHeuristicsProgressPayload | undefined);
    });
  }

  clearCompleted(runId: string): void {
    const current = this.runsSubject.getValue();
    this.runsSubject.next(current.filter((run) => !(run.runId === runId && run.state === 'Completed')));
  }

  private upsertFromEvent(payload: HostTagHeuristicsProgressPayload | undefined): void {
    if (!payload?.runId || !payload.projectId) {
      return;
    }

    const normalized = this.normalize(payload);
    const current = this.runsSubject.getValue();
    const index = current.findIndex((item) => item.runId === normalized.runId);
    if (index < 0) {
      this.runsSubject.next(this.sortRuns([normalized, ...current]));
      return;
    }

    const next = [...current];
    next[index] = normalized;
    this.runsSubject.next(this.sortRuns(next));
  }

  private normalize(payload: HostTagHeuristicsProgressPayload): TagHeuristicsRun {
    const progressRaw = typeof payload.progress === 'number' ? payload.progress : 0;
    const progress = Math.min(100, Math.max(0, progressRaw));
    const state = this.normalizeState(payload.state);
    const startedAt = payload.startedAt ?? new Date().toISOString();

    return {
      runId: payload.runId ?? '',
      projectId: payload.projectId ?? '',
      projectName: payload.projectName ?? '(unknown project)',
      state,
      progress,
      message: payload.message ?? '',
      startedAt,
      finishedAt: payload.finishedAt ?? null,
      generatedCount: typeof payload.generatedCount === 'number' ? payload.generatedCount : null
    };
  }

  private sortRuns(items: TagHeuristicsRun[]): TagHeuristicsRun[] {
    return [...items].sort(
      (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime()
    );
  }

  private normalizeState(raw: string | undefined): TagHeuristicsRunState {
    if (!raw) {
      return 'Running';
    }

    if (raw === 'Completed' || raw === 'Failed') {
      return raw;
    }

    return 'Running';
  }
}
