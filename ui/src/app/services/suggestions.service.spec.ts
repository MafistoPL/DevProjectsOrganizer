import { BehaviorSubject, Subject } from 'rxjs';
import { SuggestionsService } from './suggestions.service';

type BridgeRequest = {
  type: string;
  payload?: unknown;
};

class BridgeMock {
  readonly events$ = new Subject<{ type: string; data?: unknown }>();
  readonly requests: BridgeRequest[] = [];
  private readonly suggestions = new BehaviorSubject<any[]>([
    {
      id: 'p1',
      scanSessionId: 'scan-1',
      rootPath: 'D:\\code',
      name: 'alpha',
      score: 0.7,
      kind: 'ProjectRoot',
      path: 'D:\\code\\alpha',
      reason: 'markers',
      extensionsSummary: 'cs=1',
      markers: [],
      techHints: [],
      createdAt: '2026-02-13T08:00:00.000Z',
      status: 'Pending'
    },
    {
      id: 'a1',
      scanSessionId: 'scan-1',
      rootPath: 'D:\\code',
      name: 'beta',
      score: 0.6,
      kind: 'ProjectRoot',
      path: 'D:\\code\\beta',
      reason: 'markers',
      extensionsSummary: 'cs=1',
      markers: [],
      techHints: [],
      createdAt: '2026-02-13T08:01:00.000Z',
      status: 'Accepted'
    },
    {
      id: 'p2',
      scanSessionId: 'scan-2',
      rootPath: 'D:\\code',
      name: 'gamma',
      score: 0.5,
      kind: 'ProjectRoot',
      path: 'D:\\code\\gamma',
      reason: 'markers',
      extensionsSummary: 'cs=1',
      markers: [],
      techHints: [],
      createdAt: '2026-02-13T08:02:00.000Z',
      status: 'Pending'
    }
  ]);

  async request<T>(type: string, payload?: any): Promise<T> {
    this.requests.push({ type, payload });

    if (type === 'suggestions.list') {
      return this.suggestions.value as T;
    }

    if (type === 'suggestions.setStatus') {
      const next = this.suggestions.value.map((item) =>
        item.id === payload.id ? { ...item, status: payload.status } : item
      );
      this.suggestions.next(next);
      return next.find((item) => item.id === payload.id) as T;
    }

    if (type === 'suggestions.delete') {
      const existed = this.suggestions.value.some((item) => item.id === payload.id);
      if (existed) {
        const next = this.suggestions.value.filter((item) => item.id !== payload.id);
        this.suggestions.next(next);
      }
      return { id: payload.id, deleted: existed } as T;
    }

    throw new Error(`Unexpected request: ${type}`);
  }
}

class ProjectsServiceMock {
  loadCalls = 0;

  async load(): Promise<void> {
    this.loadCalls += 1;
  }
}

describe('SuggestionsService', () => {
  it('setPendingStatusForAll updates only pending suggestions and returns updated count', async () => {
    const bridge = new BridgeMock();
    const projects = new ProjectsServiceMock();
    const sut = new SuggestionsService(bridge as any, projects as any);
    await sut.load();

    const updated = await sut.setPendingStatusForAll('accepted');

    expect(updated).toBe(2);
    const setStatusCalls = bridge.requests.filter((item) => item.type === 'suggestions.setStatus');
    expect(setStatusCalls).toHaveLength(2);
    expect(setStatusCalls.map((item) => (item.payload as any).id).sort()).toEqual(['p1', 'p2']);
    expect(projects.loadCalls).toBe(2);
  });

  it('setPendingStatusForAll returns 0 and does not call setStatus when no pending exists', async () => {
    const bridge = new BridgeMock();
    const projects = new ProjectsServiceMock();
    // Pre-accept all pending in fixture.
    await bridge.request('suggestions.setStatus', { id: 'p1', status: 'Accepted' });
    await bridge.request('suggestions.setStatus', { id: 'p2', status: 'Accepted' });
    bridge.requests.length = 0;

    const sut = new SuggestionsService(bridge as any, projects as any);
    await sut.load();
    const updated = await sut.setPendingStatusForAll('rejected');

    expect(updated).toBe(0);
    const setStatusCalls = bridge.requests.filter((item) => item.type === 'suggestions.setStatus');
    expect(setStatusCalls).toHaveLength(0);
    expect(projects.loadCalls).toBe(0);
  });

  it('setStatus accepted refreshes projects list once', async () => {
    const bridge = new BridgeMock();
    const projects = new ProjectsServiceMock();
    const sut = new SuggestionsService(bridge as any, projects as any);
    await sut.load();

    await sut.setStatus('p1', 'accepted');

    expect(projects.loadCalls).toBe(1);
  });

  it('setStatus accepted sends edited project name and description when provided', async () => {
    const bridge = new BridgeMock();
    const projects = new ProjectsServiceMock();
    const sut = new SuggestionsService(bridge as any, projects as any);
    await sut.load();

    await sut.setStatus('p1', 'accepted', 'alpha-renamed', 'project description');

    const setStatusCall = [...bridge.requests]
      .reverse()
      .find((item) => item.type === 'suggestions.setStatus');
    expect(setStatusCall).toBeDefined();
    expect((setStatusCall!.payload as any).projectName).toBe('alpha-renamed');
    expect((setStatusCall!.payload as any).projectDescription).toBe('project description');
  });

  it('restoreRejectedFromArchive updates only rejected suggestions to pending', async () => {
    const bridge = new BridgeMock();
    const projects = new ProjectsServiceMock();
    await bridge.request('suggestions.setStatus', { id: 'p1', status: 'Rejected' });
    await bridge.request('suggestions.setStatus', { id: 'p2', status: 'Rejected' });
    bridge.requests.length = 0;

    const sut = new SuggestionsService(bridge as any, projects as any);
    await sut.load();

    const updated = await sut.restoreRejectedFromArchive();

    expect(updated).toBe(2);
    const setStatusCalls = bridge.requests.filter((item) => item.type === 'suggestions.setStatus');
    expect(setStatusCalls).toHaveLength(2);
    expect(setStatusCalls.map((item) => (item.payload as any).status)).toEqual(['Pending', 'Pending']);
    expect(projects.loadCalls).toBe(0);
  });

  it('deleteRejectedFromArchive deletes only rejected suggestions', async () => {
    const bridge = new BridgeMock();
    const projects = new ProjectsServiceMock();
    await bridge.request('suggestions.setStatus', { id: 'p1', status: 'Rejected' });
    bridge.requests.length = 0;

    const sut = new SuggestionsService(bridge as any, projects as any);
    await sut.load();

    const deleted = await sut.deleteRejectedFromArchive();

    expect(deleted).toBe(1);
    const deleteCalls = bridge.requests.filter((item) => item.type === 'suggestions.delete');
    expect(deleteCalls).toHaveLength(1);
    expect((deleteCalls[0].payload as any).id).toBe('p1');
  });
});
