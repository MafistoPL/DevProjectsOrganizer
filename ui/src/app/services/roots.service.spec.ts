import { RootsService } from './roots.service';
import { AppHostBridgeService } from './apphost-bridge.service';

class BridgeMock {
  roots: Array<any> = [
    {
      id: 'root-1',
      path: 'D:\\code',
      status: 'scanned'
    },
    {
      id: 'root-2',
      path: 'C:\\src',
      status: 'changed'
    }
  ];

  async request<T>(type: string): Promise<T> {
    if (type === 'roots.list') {
      return this.roots as T;
    }

    throw new Error(`Unexpected request: ${type}`);
  }
}

describe('RootsService', () => {
  it('tracks selected roots and exposes selected snapshot', async () => {
    const bridge = new BridgeMock();
    const service = new RootsService(bridge as unknown as AppHostBridgeService);
    await service.load();

    service.setRootSelected('root-1', true);
    service.setRootSelected('root-2', true);

    expect(service.getSelectedRootIdsSnapshot()).toEqual(['root-1', 'root-2']);
    expect(service.getSelectedRootsSnapshot().map((item) => item.id)).toEqual(['root-1', 'root-2']);
  });

  it('sanitizes selected roots after roots reload', async () => {
    const bridge = new BridgeMock();
    const service = new RootsService(bridge as unknown as AppHostBridgeService);
    await service.load();

    service.setRootSelected('root-1', true);
    service.setRootSelected('root-2', true);

    bridge.roots = [bridge.roots[1]];
    await service.load();

    expect(service.getSelectedRootIdsSnapshot()).toEqual(['root-2']);
    expect(service.isRootSelected('root-1')).toBe(false);
    expect(service.isRootSelected('root-2')).toBe(true);
  });

  it('stores depth limit per selected root and exposes rescan targets', async () => {
    const bridge = new BridgeMock();
    const service = new RootsService(bridge as unknown as AppHostBridgeService);
    await service.load();

    service.setRootSelected('root-1', true);
    service.setRootDepth('root-1', '3');
    service.setRootSelected('root-2', true);
    service.setRootDepth('root-2', 1);

    const targets = service.getSelectedRescanTargetsSnapshot();
    expect(targets).toEqual([
      {
        root: bridge.roots[0],
        depthLimit: 3
      },
      {
        root: bridge.roots[1],
        depthLimit: 1
      }
    ]);
  });

  it('treats invalid depth values as null', async () => {
    const bridge = new BridgeMock();
    const service = new RootsService(bridge as unknown as AppHostBridgeService);
    await service.load();

    service.setRootSelected('root-1', true);
    service.setRootDepth('root-1', '0');
    expect(service.getRootDepth('root-1')).toBeNull();

    service.setRootDepth('root-1', 'abc');
    expect(service.getRootDepth('root-1')).toBeNull();

    service.setRootDepth('root-1', '2');
    expect(service.getRootDepth('root-1')).toBe(2);
  });
});
