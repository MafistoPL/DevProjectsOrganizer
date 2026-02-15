import { Subject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';
import { ScanService } from './scan.service';

describe('ScanService', () => {
  it('clearCompleted removes only completed scan rows', async () => {
    const events$ = new Subject<{ type: string; data?: unknown }>();
    const bridge = {
      events$,
      request: async (type: string) => {
        if (type === 'scan.list') {
          return [
            {
              id: 'scan-completed',
              rootPath: 'D:\\code',
              mode: 'roots',
              state: 'Completed',
              disk: 'D:',
              currentPath: null,
              filesScanned: 10,
              totalFiles: 10,
              queueReason: null,
              outputPath: 'C:\\mock\\scan-completed.json',
              eta: '00:00:00'
            },
            {
              id: 'scan-running',
              rootPath: 'C:\\src',
              mode: 'roots',
              state: 'Running',
              disk: 'C:',
              currentPath: 'C:\\src\\main.cpp',
              filesScanned: 5,
              totalFiles: 20,
              queueReason: null,
              outputPath: null,
              eta: '00:00:12'
            }
          ];
        }

        return {};
      }
    } as unknown as AppHostBridgeService;

    const service = new ScanService(bridge);
    await service.load();

    const emitted: any[] = [];
    const subscription = service.scans$.subscribe((items) => emitted.push(items));

    service.clearCompleted('scan-completed');

    const latest = emitted.at(-1);
    expect(latest).toHaveLength(1);
    expect(latest[0].id).toBe('scan-running');

    subscription.unsubscribe();
  });
});
