import { Subject } from 'rxjs';
import { TagSuggestionsService } from './tag-suggestions.service';

type BridgeRequest = {
  type: string;
  payload?: unknown;
};

class BridgeMock {
  readonly events$ = new Subject<{ type: string; data?: unknown }>();
  readonly requests: BridgeRequest[] = [];
  private readonly items: any[] = [
    {
      id: 'ts-1',
      projectId: 'p-1',
      projectName: 'dotnet-api',
      tagId: 't-1',
      tagName: 'csharp',
      type: 'AssignExisting',
      source: 'Heuristic',
      confidence: 0.86,
      reason: 'marker:.csproj',
      createdAt: '2026-02-14T10:00:00.000Z',
      status: 'Pending'
    },
    {
      id: 'ts-2',
      projectId: 'p-2',
      projectName: 'cpp-tree',
      tagId: 't-2',
      tagName: 'cpp',
      type: 'AssignExisting',
      source: 'Heuristic',
      confidence: 0.8,
      reason: 'hint:cpp',
      createdAt: '2026-02-14T10:01:00.000Z',
      status: 'Pending'
    }
  ];

  async request<T>(type: string, payload?: any): Promise<T> {
    this.requests.push({ type, payload });

    if (type === 'tagSuggestions.list') {
      return this.items as T;
    }

    if (type === 'tagSuggestions.setStatus') {
      const item = this.items.find((entry) => entry.id === payload.id);
      if (!item) {
        throw new Error('Not found');
      }
      item.status = payload.status;
      return item as T;
    }

    if (type === 'tagSuggestions.delete') {
      const index = this.items.findIndex((entry) => entry.id === payload.id);
      if (index >= 0) {
        this.items.splice(index, 1);
        return { id: payload.id, deleted: true } as T;
      }

      return { id: payload.id, deleted: false } as T;
    }

    throw new Error(`Unexpected request: ${type}`);
  }
}

describe('TagSuggestionsService', () => {
  it('loads on startup and reloads on tagSuggestions.changed event', async () => {
    const bridge = new BridgeMock();
    const sut = new TagSuggestionsService(bridge as any);

    await Promise.resolve();
    expect(bridge.requests.filter((item) => item.type === 'tagSuggestions.list')).toHaveLength(1);

    bridge.events$.next({ type: 'tagSuggestions.changed' });
    await Promise.resolve();
    expect(bridge.requests.filter((item) => item.type === 'tagSuggestions.list')).toHaveLength(2);
  });

  it('setPendingStatusForAll updates all pending suggestions', async () => {
    const bridge = new BridgeMock();
    const sut = new TagSuggestionsService(bridge as any);
    await sut.load();

    const updated = await sut.setPendingStatusForAll('accepted');

    expect(updated).toBe(2);
    expect(bridge.requests.filter((item) => item.type === 'tagSuggestions.setStatus')).toHaveLength(2);
  });

  it('deleteSuggestion removes suggestion from local state and sends delete request', async () => {
    const bridge = new BridgeMock();
    const sut = new TagSuggestionsService(bridge as any);
    await sut.load();

    await sut.deleteSuggestion('ts-1');

    expect(bridge.requests).toContainEqual({
      type: 'tagSuggestions.delete',
      payload: { id: 'ts-1' }
    });
  });
});
