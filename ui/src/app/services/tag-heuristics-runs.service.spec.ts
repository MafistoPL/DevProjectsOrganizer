import { Subject } from 'rxjs';
import { TagHeuristicsRunsService } from './tag-heuristics-runs.service';
import { AppHostBridgeService } from './apphost-bridge.service';

describe('TagHeuristicsRunsService', () => {
  it('creates and updates run from progress events', () => {
    const events$ = new Subject<{ type: string; data?: unknown }>();
    const bridge = {
      events$
    } as unknown as AppHostBridgeService;

    const service = new TagHeuristicsRunsService(bridge);
    const emitted: any[] = [];
    const subscription = service.runs$.subscribe((items) => emitted.push(items));

    events$.next({
      type: 'tagHeuristics.progress',
      data: {
        runId: 'run-1',
        projectId: 'project-1',
        projectName: 'dotnet-api',
        state: 'Running',
        progress: 30,
        message: 'Detecting',
        startedAt: '2026-02-14T23:00:00.000Z',
        finishedAt: null,
        generatedCount: null
      }
    });

    events$.next({
      type: 'tagHeuristics.progress',
      data: {
        runId: 'run-1',
        projectId: 'project-1',
        projectName: 'dotnet-api',
        state: 'Completed',
        progress: 100,
        message: 'Completed',
        startedAt: '2026-02-14T23:00:00.000Z',
        finishedAt: '2026-02-14T23:00:05.000Z',
        generatedCount: 3
      }
    });

    const latest = emitted.at(-1);
    expect(latest).toHaveLength(1);
    expect(latest[0].runId).toBe('run-1');
    expect(latest[0].state).toBe('Completed');
    expect(latest[0].generatedCount).toBe(3);

    subscription.unsubscribe();
  });

  it('clearCompleted removes only completed run', () => {
    const events$ = new Subject<{ type: string; data?: unknown }>();
    const bridge = {
      events$
    } as unknown as AppHostBridgeService;

    const service = new TagHeuristicsRunsService(bridge);
    const emitted: any[] = [];
    const subscription = service.runs$.subscribe((items) => emitted.push(items));

    events$.next({
      type: 'tagHeuristics.progress',
      data: {
        runId: 'run-running',
        projectId: 'project-1',
        projectName: 'active',
        state: 'Running',
        progress: 40,
        message: 'Running',
        startedAt: '2026-02-14T23:00:00.000Z',
        finishedAt: null,
        generatedCount: null
      }
    });
    events$.next({
      type: 'tagHeuristics.progress',
      data: {
        runId: 'run-completed',
        projectId: 'project-2',
        projectName: 'done',
        state: 'Completed',
        progress: 100,
        message: 'Completed',
        startedAt: '2026-02-14T23:01:00.000Z',
        finishedAt: '2026-02-14T23:01:05.000Z',
        generatedCount: 2
      }
    });

    service.clearCompleted('run-completed');

    const latest = emitted.at(-1);
    expect(latest).toHaveLength(1);
    expect(latest[0].runId).toBe('run-running');

    subscription.unsubscribe();
  });
});
