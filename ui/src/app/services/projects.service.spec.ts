import { Subject } from 'rxjs';
import { ProjectsService } from './projects.service';

class BridgeMock {
  readonly events$ = new Subject<{ type: string; data?: unknown }>();
  requests: Array<{ type: string; payload?: unknown }> = [];
  private projects: any[] = [
    {
      id: 'proj-1',
      sourceSuggestionId: 's1',
      lastScanSessionId: 'scan-1',
      rootPath: 'D:\\code',
      name: 'dotnet-api',
      score: 0.88,
      kind: 'ProjectRoot',
      path: 'D:\\code\\dotnet-api',
      reason: 'markers: .sln',
      extensionsSummary: 'cs=10',
      markers: ['.sln'],
      techHints: ['csharp'],
      createdAt: '2026-02-13T10:00:00.000Z',
      updatedAt: '2026-02-13T10:00:00.000Z',
      tags: [{ id: 'tag-1', name: 'csharp' }]
    }
  ];

  async request<T>(type: string, payload?: unknown): Promise<T> {
    this.requests.push({ type, payload });
    if (type === 'projects.list') {
      return this.projects as T;
    }

    if (type === 'projects.runTagHeuristics' || type === 'projects.runAiTagSuggestions') {
      return {} as T;
    }

    if (type === 'projects.delete') {
      return { id: 'proj-1', deleted: true } as T;
    }

    throw new Error(`Unexpected request: ${type}`);
  }
}

describe('ProjectsService', () => {
  it('loads projects on startup and on projects.changed event', async () => {
    const bridge = new BridgeMock();
    const sut = new ProjectsService(bridge as any);

    await Promise.resolve();
    expect(bridge.requests.filter((item) => item.type === 'projects.list')).toHaveLength(1);

    bridge.events$.next({ type: 'projects.changed' });
    await Promise.resolve();

    expect(bridge.requests.filter((item) => item.type === 'projects.list')).toHaveLength(2);
  });

  it('can find project by source suggestion id', async () => {
    const bridge = new BridgeMock();
    const sut = new ProjectsService(bridge as any);
    await Promise.resolve();

    const project = sut.findBySourceSuggestionId('s1');
    expect(project?.id).toBe('proj-1');

    const missing = sut.findBySourceSuggestionId('missing');
    expect(missing).toBeNull();
  });

  it('calls host for post-accept actions', async () => {
    const bridge = new BridgeMock();
    const sut = new ProjectsService(bridge as any);
    await Promise.resolve();

    await sut.runTagHeuristics('proj-1');
    await sut.runAiTagSuggestions('proj-1');

    expect(bridge.requests).toContainEqual({
      type: 'projects.runTagHeuristics',
      payload: { projectId: 'proj-1' }
    });
    expect(bridge.requests).toContainEqual({
      type: 'projects.runAiTagSuggestions',
      payload: { projectId: 'proj-1' }
    });
  });

  it('deleteProject sends project id and confirm name, then reloads', async () => {
    const bridge = new BridgeMock();
    const sut = new ProjectsService(bridge as any);
    await Promise.resolve();
    bridge.requests = [];

    await sut.deleteProject('proj-1', 'dotnet-api');

    expect(bridge.requests).toEqual([
      { type: 'projects.delete', payload: { projectId: 'proj-1', confirmName: 'dotnet-api' } },
      { type: 'projects.list', payload: undefined }
    ]);
  });
});
