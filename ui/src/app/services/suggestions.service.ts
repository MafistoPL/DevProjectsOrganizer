import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

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

@Injectable({ providedIn: 'root' })
export class SuggestionsService {
  private readonly itemsSubject = new BehaviorSubject<ProjectSuggestionItem[]>([]);
  readonly items$ = this.itemsSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
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

  async setStatus(id: string, status: SuggestionStatus): Promise<void> {
    const payloadStatus: SuggestionStatusPayload =
      status === 'accepted' ? 'Accepted' : status === 'rejected' ? 'Rejected' : 'Pending';
    const updated = await this.bridge.request<HostSuggestionDto>('suggestions.setStatus', {
      id,
      status: payloadStatus
    });
    this.upsert(this.normalize(updated));
  }

  async exportDebugJson(id: string): Promise<string> {
    const response = await this.bridge.request<{ id: string; json: string }>(
      'suggestions.exportDebug',
      { id }
    );
    return response.json;
  }

  async exportArchiveJson(): Promise<{ path: string; count: number }> {
    return await this.bridge.request<{ path: string; count: number }>('suggestions.exportArchive');
  }

  async openArchiveFolder(): Promise<{ path: string }> {
    return await this.bridge.request<{ path: string }>('suggestions.openArchiveFolder');
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
