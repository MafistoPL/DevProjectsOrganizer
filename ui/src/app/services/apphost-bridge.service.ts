import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

type PendingRequest = {
  resolve: (value: any) => void;
  reject: (reason?: any) => void;
};

type HostResponse = {
  id: string;
  type: string;
  ok: boolean;
  data?: unknown;
  error?: string;
};

type HostEvent = {
  type: string;
  data?: unknown;
};

@Injectable({ providedIn: 'root' })
export class AppHostBridgeService {
  private readonly pending = new Map<string, PendingRequest>();
  private readonly webview = (window as any).chrome?.webview;
  private mockRoots: Array<{ id: string; path: string; status: string }> = [];
  private mockScans: Array<any> = [];
  private mockSuggestions: Array<any> = [];
  private readonly eventSubject = new Subject<HostEvent>();
  readonly events$ = this.eventSubject.asObservable();

  constructor() {
    if (this.webview) {
      this.webview.addEventListener('message', (event: MessageEvent<HostResponse>) => {
        const message = event.data;
        if (message?.id) {
          const pending = this.pending.get(message.id);
          if (!pending) {
            return;
          }
          this.pending.delete(message.id);
          if (message.ok) {
            pending.resolve(message.data);
          } else {
            pending.reject(new Error(message.error ?? 'Unknown host error'));
          }
          return;
        }

        if (message?.type) {
          this.eventSubject.next({ type: message.type, data: message.data });
        }
      });
    } else {
      this.loadMockRoots();
      this.loadMockSuggestions();
    }
  }

  request<T>(type: string, payload?: unknown): Promise<T> {
    if (!this.webview) {
      return this.mockRequest<T>(type, payload);
    }

    const id = this.createId();
    const promise = new Promise<T>((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
    });

    this.webview.postMessage({ id, type, payload });
    return promise;
  }

  private mockRequest<T>(type: string, payload?: any): Promise<T> {
    switch (type) {
      case 'roots.list': {
        return Promise.resolve(this.mockRoots as T);
      }
      case 'roots.add': {
        const raw = typeof payload?.path === 'string' ? payload.path.trim() : '';
        if (!raw) {
          return Promise.reject(new Error('Root path cannot be empty.'));
        }
        const existing = this.mockRoots.find((root) => root.path === raw);
        if (existing) {
          return Promise.resolve(existing as T);
        }
        const root = {
          id: this.createId(),
          path: raw,
          status: 'not scanned'
        };
        this.mockRoots = [...this.mockRoots, root];
        this.saveMockRoots();
        return Promise.resolve(root as T);
      }
      case 'roots.update': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        const raw = typeof payload?.path === 'string' ? payload.path.trim() : '';
        if (!id) {
          return Promise.reject(new Error('Missing root id.'));
        }
        if (!raw) {
          return Promise.reject(new Error('Root path cannot be empty.'));
        }
        const duplicate = this.mockRoots.find((root) => root.path === raw && root.id !== id);
        if (duplicate) {
          return Promise.reject(new Error('Root path already exists.'));
        }
        const index = this.mockRoots.findIndex((root) => root.id === id);
        if (index === -1) {
          return Promise.reject(new Error('Root not found.'));
        }
        const updated = { ...this.mockRoots[index], path: raw };
        this.mockRoots = [
          ...this.mockRoots.slice(0, index),
          updated,
          ...this.mockRoots.slice(index + 1)
        ];
        this.saveMockRoots();
        return Promise.resolve(updated as T);
      }
      case 'roots.delete': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        if (!id) {
          return Promise.reject(new Error('Missing root id.'));
        }
        const existing = this.mockRoots.find((root) => root.id === id);
        if (!existing) {
          return Promise.resolve({ id, deleted: false } as T);
        }
        this.mockRoots = this.mockRoots.filter((root) => root.id !== id);
        this.saveMockRoots();
        return Promise.resolve({ id, deleted: true } as T);
      }
      case 'scan.list': {
        return Promise.resolve(this.mockScans as T);
      }
      case 'scan.start': {
        const scan = {
          id: this.createId(),
          rootPath: 'C:\\src',
          mode: payload?.mode ?? 'roots',
          state: 'Running',
          disk: 'C:',
          currentPath: 'C:\\src\\project\\file.cs',
          filesScanned: 120,
          totalFiles: 480,
          queueReason: null,
          outputPath: null
        };
        this.mockScans = [scan, ...this.mockScans];
        this.eventSubject.next({ type: 'scan.progress', data: scan });
        return Promise.resolve(scan as T);
      }
      case 'scan.pause': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        this.mockScans = this.mockScans.map((scan) =>
          scan.id === id ? { ...scan, state: 'Paused' } : scan
        );
        const updated = this.mockScans.find((scan) => scan.id === id);
        if (updated) {
          this.eventSubject.next({ type: 'scan.progress', data: updated });
        }
        return Promise.resolve({ id } as T);
      }
      case 'scan.resume': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        this.mockScans = this.mockScans.map((scan) =>
          scan.id === id ? { ...scan, state: 'Running' } : scan
        );
        const updated = this.mockScans.find((scan) => scan.id === id);
        if (updated) {
          this.eventSubject.next({ type: 'scan.progress', data: updated });
        }
        return Promise.resolve({ id } as T);
      }
      case 'scan.stop': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        this.mockScans = this.mockScans.filter((scan) => scan.id !== id);
        this.eventSubject.next({ type: 'scan.completed', data: { id } });
        return Promise.resolve({ id } as T);
      }
      case 'suggestions.list': {
        const items = [...this.mockSuggestions].sort(
          (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
        );
        return Promise.resolve(items as T);
      }
      case 'suggestions.setStatus': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        const statusRaw = typeof payload?.status === 'string' ? payload.status : '';
        if (!id) {
          return Promise.reject(new Error('Missing suggestion id.'));
        }

        const index = this.mockSuggestions.findIndex((item) => item.id === id);
        if (index < 0) {
          return Promise.reject(new Error('Suggestion not found.'));
        }

        const normalized =
          statusRaw.toLowerCase() === 'accepted'
            ? 'Accepted'
            : statusRaw.toLowerCase() === 'rejected'
              ? 'Rejected'
              : 'Pending';

        const updated = { ...this.mockSuggestions[index], status: normalized };
        this.mockSuggestions = [
          ...this.mockSuggestions.slice(0, index),
          updated,
          ...this.mockSuggestions.slice(index + 1)
        ];
        return Promise.resolve(updated as T);
      }
      case 'suggestions.exportDebug': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        if (!id) {
          return Promise.reject(new Error('Missing suggestion id.'));
        }

        const item = this.mockSuggestions.find((entry) => entry.id === id);
        if (!item) {
          return Promise.reject(new Error('Suggestion not found.'));
        }

        const debug = {
          suggestion: {
            ...item
          },
          source: {
            scanOutputPath: null,
            scanMode: 'roots',
            scanState: 'Completed'
          }
        };

        return Promise.resolve({
          id,
          json: JSON.stringify(debug, null, 2)
        } as T);
      }
      case 'suggestions.exportArchive': {
        const archive = this.mockSuggestions
          .filter((item) => item.status !== 'Pending')
          .map((item) => ({ ...item }));
        return Promise.resolve({
          path: 'C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\exports\\suggestions-archive-mock.json',
          count: archive.length
        } as T);
      }
      case 'suggestions.openArchiveFolder': {
        return Promise.resolve({
          path: 'C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\exports'
        } as T);
      }
      default:
        return Promise.reject(new Error(`Unknown mock request: ${type}`));
    }
  }

  private loadMockRoots(): void {
    const stored = localStorage.getItem('mockRoots');
    if (stored) {
      this.mockRoots = JSON.parse(stored);
      return;
    }

    this.mockRoots = [
      { id: this.createId(), path: 'D:\\code', status: 'scanned' },
      { id: this.createId(), path: 'C:\\src', status: 'changed' },
      { id: this.createId(), path: 'E:\\backup', status: 'scanning' }
    ];
    this.saveMockRoots();
  }

  private saveMockRoots(): void {
    localStorage.setItem('mockRoots', JSON.stringify(this.mockRoots));
  }

  private loadMockSuggestions(): void {
    this.mockSuggestions = [
      {
        id: 's1',
        scanSessionId: 'scan-1',
        rootPath: 'D:\\code',
        name: 'dotnet-api',
        score: 0.88,
        kind: 'ProjectRoot',
        path: 'D:\\code\\dotnet-api',
        reason: '.sln + csproj markers',
        extensionsSummary: 'cs=142, json=18, md=3',
        markers: ['.sln', 'Api.csproj'],
        techHints: ['csharp', '.net'],
        createdAt: '2025-01-10T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's2',
        scanSessionId: 'scan-2',
        rootPath: 'C:\\src',
        name: 'c-labs',
        score: 0.73,
        kind: 'Collection',
        path: 'C:\\src\\c-labs',
        reason: 'ext histogram',
        extensionsSummary: 'c=58, h=42, md=2',
        markers: ['Makefile'],
        techHints: ['c', 'cpp'],
        createdAt: '2024-11-02T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's3',
        scanSessionId: 'scan-3',
        rootPath: 'E:\\backup',
        name: 'single-file-tool.ps1',
        score: 0.62,
        kind: 'SingleFileMiniProject',
        path: 'E:\\backup\\tools\\single-file-tool.ps1',
        reason: 'single file candidate',
        extensionsSummary: 'ps1=1',
        markers: ['*.ps1'],
        techHints: ['powershell'],
        createdAt: '2023-06-15T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's4',
        scanSessionId: 'scan-4',
        rootPath: 'D:\\code',
        name: 'notes-parser',
        score: 0.57,
        kind: 'ProjectRoot',
        path: 'D:\\code\\notes-parser',
        reason: 'package.json',
        extensionsSummary: 'ts=24, json=6, md=1',
        markers: ['package.json'],
        techHints: ['node', 'typescript'],
        createdAt: '2024-02-10T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's5',
        scanSessionId: 'scan-5',
        rootPath: 'C:\\src',
        name: 'kata-strings',
        score: 0.51,
        kind: 'Collection',
        path: 'C:\\src\\katas\\strings',
        reason: 'folder name',
        extensionsSummary: 'cs=12, txt=4',
        markers: [],
        techHints: ['practice'],
        createdAt: '2022-09-30T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's6',
        scanSessionId: 'scan-6',
        rootPath: 'D:\\code',
        name: 'rust-playground',
        score: 0.76,
        kind: 'ProjectRoot',
        path: 'D:\\code\\rust-playground',
        reason: 'Cargo.toml',
        extensionsSummary: 'rs=42, toml=1',
        markers: ['Cargo.toml'],
        techHints: ['rust'],
        createdAt: '2025-03-05T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's7',
        scanSessionId: 'scan-7',
        rootPath: 'D:\\code',
        name: 'go-http',
        score: 0.69,
        kind: 'ProjectRoot',
        path: 'D:\\code\\go-http',
        reason: 'go.mod',
        extensionsSummary: 'go=38, md=2',
        markers: ['go.mod'],
        techHints: ['go'],
        createdAt: '2024-07-20T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's8',
        scanSessionId: 'scan-8',
        rootPath: 'E:\\backup',
        name: 'cpp-cmake-tools',
        score: 0.71,
        kind: 'ProjectRoot',
        path: 'E:\\backup\\cpp-cmake-tools',
        reason: 'CMakeLists.txt',
        extensionsSummary: 'cpp=56, h=21',
        markers: ['CMakeLists.txt'],
        techHints: ['cpp'],
        createdAt: '2023-12-01T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's9',
        scanSessionId: 'scan-9',
        rootPath: 'C:\\src',
        name: 'python-utils',
        score: 0.55,
        kind: 'Collection',
        path: 'C:\\src\\python-utils',
        reason: 'ext histogram',
        extensionsSummary: 'py=17, md=1',
        markers: [],
        techHints: ['python'],
        createdAt: '2022-05-14T12:00:00.000Z',
        status: 'Pending'
      },
      {
        id: 's10',
        scanSessionId: 'scan-10',
        rootPath: 'E:\\backup',
        name: 'course-js-2023',
        score: 0.48,
        kind: 'Collection',
        path: 'E:\\backup\\courses\\course-js-2023',
        reason: 'folder name',
        extensionsSummary: 'js=44, html=8, css=6',
        markers: [],
        techHints: ['course', 'javascript'],
        createdAt: '2023-01-08T12:00:00.000Z',
        status: 'Pending'
      }
    ];
  }

  private createId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return crypto.randomUUID();
    }
    return Math.random().toString(36).slice(2) + Date.now().toString(36);
  }
}
