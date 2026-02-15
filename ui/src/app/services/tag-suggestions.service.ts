import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type TagSuggestionStatus = 'pending' | 'accepted' | 'rejected';

export type TagSuggestionItem = {
  id: string;
  projectId: string;
  projectName: string;
  tagId: string | null;
  tagName: string;
  type: 'assignexisting' | 'createnew';
  source: 'heuristic' | 'ai';
  confidence: number;
  reason: string;
  createdAt: string;
  status: TagSuggestionStatus;
};

type TagSuggestionStatusPayload = 'Pending' | 'Accepted' | 'Rejected';

type HostTagSuggestionDto = {
  id: string;
  projectId: string;
  projectName: string;
  tagId: string | null;
  tagName: string;
  type: string;
  source: string;
  confidence: number;
  reason: string;
  createdAt: string;
  status: string;
};

@Injectable({ providedIn: 'root' })
export class TagSuggestionsService {
  private readonly itemsSubject = new BehaviorSubject<TagSuggestionItem[]>([]);
  readonly items$ = this.itemsSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    void this.load();
    this.bridge.events$.subscribe((event) => {
      if (event?.type === 'tagSuggestions.changed') {
        void this.load();
      }
    });
  }

  async load(): Promise<void> {
    const items = await this.bridge.request<HostTagSuggestionDto[]>('tagSuggestions.list');
    this.itemsSubject.next(items.map((item) => this.normalize(item)));
  }

  async setStatus(id: string, status: TagSuggestionStatus): Promise<TagSuggestionItem> {
    const payloadStatus: TagSuggestionStatusPayload =
      status === 'accepted' ? 'Accepted' : status === 'rejected' ? 'Rejected' : 'Pending';
    const updated = await this.bridge.request<HostTagSuggestionDto>('tagSuggestions.setStatus', {
      id,
      status: payloadStatus
    });
    const normalized = this.normalize(updated);
    this.upsert(normalized);
    return normalized;
  }

  async setPendingStatusForAll(status: 'accepted' | 'rejected'): Promise<number> {
    const pendingIds = this.itemsSubject
      .getValue()
      .filter((item) => item.status === 'pending')
      .map((item) => item.id);

    for (const id of pendingIds) {
      await this.setStatus(id, status);
    }

    return pendingIds.length;
  }

  async deleteSuggestion(id: string): Promise<void> {
    await this.bridge.request<{ id: string; deleted: boolean }>('tagSuggestions.delete', { id });
    const current = this.itemsSubject.getValue();
    this.itemsSubject.next(current.filter((item) => item.id !== id));
  }

  private upsert(item: TagSuggestionItem): void {
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

  private normalize(item: HostTagSuggestionDto): TagSuggestionItem {
    const status = item.status.toLowerCase();
    const normalizedStatus: TagSuggestionStatus =
      status === 'accepted' ? 'accepted' : status === 'rejected' ? 'rejected' : 'pending';

    const source = item.source.toLowerCase() === 'ai' ? 'ai' : 'heuristic';
    const type = item.type.toLowerCase() === 'createnew' ? 'createnew' : 'assignexisting';

    return {
      id: item.id,
      projectId: item.projectId,
      projectName: item.projectName,
      tagId: item.tagId,
      tagName: item.tagName,
      type,
      source,
      confidence: item.confidence,
      reason: item.reason,
      createdAt: item.createdAt,
      status: normalizedStatus
    };
  }
}
