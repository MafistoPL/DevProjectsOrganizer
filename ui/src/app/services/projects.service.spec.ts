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
      updatedAt: '2026-02-13T10:00:00.000Z'
    }
  ];

  async request<T>(type: string): Promise<T> {
    this.requests.push({ type });
    if (type !== 'projects.list') {
      throw new Error(`Unexpected request: ${type}`);
    }
    return this.projects as T;
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
});
