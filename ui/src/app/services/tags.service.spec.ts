import { TagsService } from './tags.service';

type BridgeRequest = {
  type: string;
  payload?: unknown;
};

class BridgeMock {
  readonly requests: BridgeRequest[] = [];
  private tags: Array<any> = [
    {
      id: 't1',
      name: 'csharp',
      isSystem: true,
      projectCount: 2,
      createdAt: '2026-02-13T10:00:00.000Z',
      updatedAt: '2026-02-13T10:00:00.000Z'
    }
  ];

  async request<T>(type: string, payload?: any): Promise<T> {
    this.requests.push({ type, payload });

    if (type === 'tags.list') {
      return [...this.tags] as T;
    }

    if (type === 'tags.add') {
      const tag = {
        id: `t${this.tags.length + 1}`,
        name: payload.name,
        isSystem: false,
        projectCount: 0,
        createdAt: '2026-02-13T11:00:00.000Z',
        updatedAt: '2026-02-13T11:00:00.000Z'
      };
      this.tags = [...this.tags, tag];
      return tag as T;
    }

    if (type === 'tags.update') {
      this.tags = this.tags.map((tag) =>
        tag.id === payload.id ? { ...tag, name: payload.name, updatedAt: '2026-02-13T12:00:00.000Z' } : tag
      );
      return this.tags.find((tag) => tag.id === payload.id) as T;
    }

    if (type === 'tags.delete') {
      this.tags = this.tags.filter((tag) => tag.id !== payload.id);
      return { id: payload.id, deleted: true } as T;
    }

    if (type === 'tags.projects') {
      return [
        {
          id: 'p1',
          name: 'dotnet-api',
          path: 'D:\\code\\dotnet-api',
          kind: 'ProjectRoot',
          updatedAt: '2026-02-14T09:00:00.000Z'
        }
      ] as T;
    }

    throw new Error(`Unexpected request: ${type}`);
  }
}

describe('TagsService', () => {
  it('loads tags on startup', async () => {
    const bridge = new BridgeMock();
    new TagsService(bridge as any);

    await Promise.resolve();

    const listCalls = bridge.requests.filter((item) => item.type === 'tags.list');
    expect(listCalls).toHaveLength(1);
  });

  it('addTag reloads list after create', async () => {
    const bridge = new BridgeMock();
    const service = new TagsService(bridge as any);
    await Promise.resolve();
    bridge.requests.length = 0;

    await service.addTag('cpp');

    expect(bridge.requests.map((item) => item.type)).toEqual(['tags.add', 'tags.list']);
  });

  it('updateTag and deleteTag reload list', async () => {
    const bridge = new BridgeMock();
    const service = new TagsService(bridge as any);
    await Promise.resolve();
    bridge.requests.length = 0;

    await service.updateTag('t1', 'backend');
    await service.deleteTag('t1');

    expect(bridge.requests.map((item) => item.type)).toEqual([
      'tags.update',
      'tags.list',
      'tags.delete',
      'tags.list'
    ]);
  });

  it('listProjects requests linked projects by tag id', async () => {
    const bridge = new BridgeMock();
    const service = new TagsService(bridge as any);
    await Promise.resolve();
    bridge.requests.length = 0;

    const projects = await service.listProjects('t1');

    expect(bridge.requests).toEqual([{ type: 'tags.projects', payload: { id: 't1' } }]);
    expect(projects).toHaveLength(1);
    expect(projects[0].name).toBe('dotnet-api');
  });
});
