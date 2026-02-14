import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';
import { ProjectsService } from './projects.service';

export type SuggestionStatus = 'pending' | 'accepted' | 'rejected';

export type ProjectSuggestionItem = {
  id: string;
  scanSessionId: string;
  rootPath: string;
  name: string;
  score: number;
  kind: string;
  path: string;
  reason: string;
  markers: string[];
  techHints: string[];
  extSummary: string;
  createdAt: string;
  status: SuggestionStatus;
};

type SuggestionStatusPayload = 'Pending' | 'Accepted' | 'Rejected';

type HostSuggestionDto = {
  id: string;
  scanSessionId: string;
  rootPath: string;
  name: string;
  score: number;
  kind: string;
  path: string;
  reason: string;
  extensionsSummary: string;
  markers: string[];
  techHints: string[];
  createdAt: string;
  status: string;
};

export type SuggestionsRegressionRootReport = {
  rootPath: string;
  snapshotScanSessionId: string;
  snapshotPath: string;
  baselineAcceptedCount: number;
  baselineRejectedCount: number;
  acceptedMissingCount: number;
  rejectedMissingCount: number;
  addedCount: number;
  acceptedMissingPaths: string[];
  rejectedMissingPaths: string[];
};

export type SuggestionsRegressionReport = {
  rootsAnalyzed: number;
  baselineAcceptedCount: number;
  baselineRejectedCount: number;
  acceptedMissingCount: number;
  rejectedMissingCount: number;
  addedCount: number;
  roots: SuggestionsRegressionRootReport[];
};

@Injectable({ providedIn: 'root' })
export class SuggestionsService {
  private readonly itemsSubject = new BehaviorSubject<ProjectSuggestionItem[]>([]);
  readonly items$ = this.itemsSubject.asObservable();

  constructor(
    private readonly bridge: AppHostBridgeService,
    private readonly projectsService: ProjectsService
  ) {
    void this.load();
    this.bridge.events$.subscribe((event) => {
      if (event?.type === 'scan.completed') {
        void this.load();
      }
    });
  }

  async load(): Promise<void> {
    const response = await this.bridge.request<HostSuggestionDto[]>('suggestions.list');
    this.itemsSubject.next(response.map((item) => this.normalize(item)));
  }

  async setStatus(id: string, status: SuggestionStatus): Promise<ProjectSuggestionItem> {
    const payloadStatus: SuggestionStatusPayload =
      status === 'accepted' ? 'Accepted' : status === 'rejected' ? 'Rejected' : 'Pending';
    const updated = await this.bridge.request<HostSuggestionDto>('suggestions.setStatus', {
      id,
      status: payloadStatus
    });
    const normalized = this.normalize(updated);
    this.upsert(normalized);
    if (payloadStatus === 'Accepted') {
      await this.projectsService.load();
    }
    return normalized;
  }

  async setPendingStatusForAll(status: 'accepted' | 'rejected'): Promise<number> {
    const pendingIds = this.itemsSubject
      .getValue()
      .filter((item) => item.status === 'pending')
      .map((item) => item.id);

    if (pendingIds.length === 0) {
      return 0;
    }

    for (const id of pendingIds) {
      await this.setStatus(id, status);
    }

    return pendingIds.length;
  }

  async deleteSuggestion(id: string): Promise<void> {
    await this.bridge.request<{ id: string; deleted: boolean }>('suggestions.delete', { id });
    const current = this.itemsSubject.getValue();
    this.itemsSubject.next(current.filter((item) => item.id !== id));
  }

  async exportArchiveJson(): Promise<{ path: string; count: number }> {
    return await this.bridge.request<{ path: string; count: number }>('suggestions.exportArchive');
  }

  async openArchiveFolder(): Promise<{ path: string }> {
    return await this.bridge.request<{ path: string }>('suggestions.openArchiveFolder');
  }

  async openPath(path: string): Promise<{ path: string }> {
    return await this.bridge.request<{ path: string }>('suggestions.openPath', { path });
  }

  async runRegressionReport(): Promise<SuggestionsRegressionReport> {
    return await this.bridge.request<SuggestionsRegressionReport>('suggestions.regressionReport');
  }

  async exportRegressionReport(): Promise<{ path: string; rootsAnalyzed: number }> {
    return await this.bridge.request<{ path: string; rootsAnalyzed: number }>(
      'suggestions.exportRegressionReport'
    );
  }

  private upsert(item: ProjectSuggestionItem): void {
    const current = this.itemsSubject.getValue();
    const index = current.findIndex((entry) => entry.id === item.id);
    if (index < 0) {
      this.itemsSubject.next([item, ...current]);
      return;
    }

    const next = [...current];
    next[index] = item;
    this.itemsSubject.next(next);
  }

  private normalize(item: HostSuggestionDto): ProjectSuggestionItem {
    const status = item.status.toLowerCase();
    const normalizedStatus: SuggestionStatus =
      status === 'accepted' ? 'accepted' : status === 'rejected' ? 'rejected' : 'pending';

    return {
      id: item.id,
      scanSessionId: item.scanSessionId,
      rootPath: item.rootPath,
      name: item.name,
      score: item.score,
      kind: item.kind,
      path: item.path,
      reason: item.reason,
      markers: item.markers ?? [],
      techHints: item.techHints ?? [],
      extSummary: item.extensionsSummary ?? '',
      createdAt: item.createdAt,
      status: normalizedStatus
    };
  }
}
