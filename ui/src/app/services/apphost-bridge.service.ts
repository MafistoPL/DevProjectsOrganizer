import { Injectable, NgZone } from '@angular/core';
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

type MockRoot = {
  id: string;
  path: string;
  status: string;
  projectCount: number;
  ongoingSuggestionCount: number;
  lastScanState: string | null;
  lastScanAt: string | null;
  lastScanFiles: number | null;
};

type MockProject = {
  id: string;
  sourceSuggestionId: string;
  lastScanSessionId: string;
  rootPath: string;
  name: string;
  score: number;
  kind: string;
  path: string;
  reason: string;
  extensionsSummary: string;
  markers: string[];
  techHints: string[];
  createdAt: string;
  updatedAt: string;
};

@Injectable({ providedIn: 'root' })
export class AppHostBridgeService {
  private readonly pending = new Map<string, PendingRequest>();
  private readonly webview = (window as any).chrome?.webview;
  private mockRoots: MockRoot[] = [];
  private mockScans: Array<any> = [];
  private mockSuggestions: Array<any> = [];
  private mockProjects: MockProject[] = [];
  private readonly eventSubject = new Subject<HostEvent>();
  readonly events$ = this.eventSubject.asObservable();

  constructor(private readonly ngZone: NgZone) {
    if (this.webview) {
      this.webview.addEventListener('message', (event: MessageEvent<HostResponse>) => {
        const message = event.data;
        if (message?.id) {
          const pending = this.pending.get(message.id);
          if (!pending) {
            return;
          }
          this.pending.delete(message.id);
          this.ngZone.run(() => {
            if (message.ok) {
              pending.resolve(message.data);
            } else {
              pending.reject(new Error(message.error ?? 'Unknown host error'));
            }
          });
          return;
        }

        if (message?.type) {
          this.ngZone.run(() => {
            this.eventSubject.next({ type: message.type, data: message.data });
          });
        }
      });
    } else {
      this.loadMockRoots();
      this.loadMockScans();
      this.loadMockSuggestions();
      this.loadMockProjects();
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
          status: 'not scanned',
          projectCount: 0,
          ongoingSuggestionCount: 0,
          lastScanState: null,
          lastScanAt: null,
          lastScanFiles: null
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
      case 'projects.list': {
        const projects = [...this.mockProjects].sort(
          (a, b) => new Date(b.updatedAt).getTime() - new Date(a.updatedAt).getTime()
        );
        return Promise.resolve(projects as T);
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

        if (normalized === 'Accepted') {
          this.upsertMockProjectFromSuggestion(updated);
          this.eventSubject.next({
            type: 'projects.changed',
            data: { reason: 'suggestion.accepted', suggestionId: updated.id }
          });
        }

        return Promise.resolve(updated as T);
      }
      case 'suggestions.delete': {
        const id = typeof payload?.id === 'string' ? payload.id : '';
        if (!id) {
          return Promise.reject(new Error('Missing suggestion id.'));
        }

        const existed = this.mockSuggestions.some((item) => item.id === id);
        if (existed) {
          this.mockSuggestions = this.mockSuggestions.filter((item) => item.id !== id);
        }

        return Promise.resolve({ id, deleted: existed } as T);
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
      case 'suggestions.regressionReport': {
        return Promise.resolve({
          rootsAnalyzed: 2,
          baselineAcceptedCount: 3,
          baselineRejectedCount: 2,
          acceptedMissingCount: 0,
          rejectedMissingCount: 1,
          addedCount: 2,
          roots: [
            {
              rootPath: 'D:\\code',
              snapshotScanSessionId: 'scan-a',
              snapshotPath:
                'C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\scans\\scan-a.json',
              baselineAcceptedCount: 2,
              baselineRejectedCount: 1,
              acceptedMissingCount: 0,
              rejectedMissingCount: 1,
              addedCount: 1,
              acceptedMissingPaths: [],
              rejectedMissingPaths: ['D:\\code\\old-bad-hit']
            },
            {
              rootPath: 'C:\\src',
              snapshotScanSessionId: 'scan-b',
              snapshotPath:
                'C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\scans\\scan-b.json',
              baselineAcceptedCount: 1,
              baselineRejectedCount: 1,
              acceptedMissingCount: 0,
              rejectedMissingCount: 0,
              addedCount: 1,
              acceptedMissingPaths: [],
              rejectedMissingPaths: []
            }
          ]
        } as T);
      }
      case 'suggestions.exportRegressionReport': {
        return Promise.resolve({
          path: 'C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\exports\\suggestions-regression-mock.json',
          rootsAnalyzed: 2
        } as T);
      }
      case 'suggestions.openArchiveFolder': {
        return Promise.resolve({
          path: 'C:\\Users\\Mock\\AppData\\Roaming\\DevProjectsOrganizer\\exports'
        } as T);
      }
      case 'suggestions.openPath': {
        const path = typeof payload?.path === 'string' ? payload.path : '';
        if (!path) {
          return Promise.reject(new Error('Missing path.'));
        }

        return Promise.resolve({ path } as T);
      }
      default:
        return Promise.reject(new Error(`Unknown mock request: ${type}`));
    }
  }

  private loadMockRoots(): void {
    const stored = localStorage.getItem('mockRoots');
    if (stored) {
      this.mockRoots = JSON.parse(stored);
      this.mockRoots = this.mockRoots.map((root) => ({
        id: root.id,
        path: root.path,
        status: root.status,
        projectCount: root.projectCount ?? 0,
        ongoingSuggestionCount: root.ongoingSuggestionCount ?? 0,
        lastScanState: root.lastScanState ?? null,
        lastScanAt: root.lastScanAt ?? null,
        lastScanFiles: root.lastScanFiles ?? null
      }));
      return;
    }

    this.mockRoots = [
      {
        id: this.createId(),
        path: 'D:\\code',
        status: 'scanned',
        projectCount: 8,
        ongoingSuggestionCount: 2,
        lastScanState: 'Completed',
        lastScanAt: '2026-02-12T18:24:00.000Z',
        lastScanFiles: 1284992
      },
      {
        id: this.createId(),
        path: 'C:\\src',
        status: 'changed',
        projectCount: 5,
        ongoingSuggestionCount: 1,
        lastScanState: 'Completed',
        lastScanAt: '2026-02-12T17:12:00.000Z',
        lastScanFiles: 462193
      },
      {
        id: this.createId(),
        path: 'E:\\backup',
        status: 'scanning',
        projectCount: 3,
        ongoingSuggestionCount: 4,
        lastScanState: 'Running',
        lastScanAt: '2026-02-12T19:01:00.000Z',
        lastScanFiles: 228344
      }
    ];
    this.saveMockRoots();
  }

  private saveMockRoots(): void {
    localStorage.setItem('mockRoots', JSON.stringify(this.mockRoots));
  }

  private loadMockSuggestions(): void {
    const stored = localStorage.getItem('mockSuggestions');
    if (stored) {
      try {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed)) {
          this.mockSuggestions = parsed;
          return;
        }
      } catch {
        // Fallback to built-in fixtures.
      }
    }

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

  private loadMockScans(): void {
    const stored = localStorage.getItem('mockScans');
    if (!stored) {
      this.mockScans = [];
      return;
    }

    try {
      const parsed = JSON.parse(stored);
      this.mockScans = Array.isArray(parsed) ? parsed : [];
    } catch {
      this.mockScans = [];
    }
  }

  private loadMockProjects(): void {
    const now = new Date().toISOString();
    this.mockProjects = this.mockSuggestions
      .filter((item) => String(item.status).toLowerCase() === 'accepted')
      .map((item) => ({
        id: this.createId(),
        sourceSuggestionId: item.id,
        lastScanSessionId: item.scanSessionId,
        rootPath: item.rootPath,
        name: item.name,
        score: item.score,
        kind: item.kind,
        path: item.path,
        reason: item.reason,
        extensionsSummary: item.extensionsSummary,
        markers: item.markers ?? [],
        techHints: item.techHints ?? [],
        createdAt: now,
        updatedAt: now
      }));
  }

  private upsertMockProjectFromSuggestion(suggestion: any): void {
    const key = this.createMockProjectKey(suggestion.path, suggestion.kind);
    const now = new Date().toISOString();
    const index = this.mockProjects.findIndex(
      (item) => this.createMockProjectKey(item.path, item.kind) === key
    );

    const mapped: MockProject = {
      id: index >= 0 ? this.mockProjects[index].id : this.createId(),
      sourceSuggestionId: suggestion.id,
      lastScanSessionId: suggestion.scanSessionId,
      rootPath: suggestion.rootPath,
      name: suggestion.name,
      score: suggestion.score,
      kind: suggestion.kind,
      path: suggestion.path,
      reason: suggestion.reason,
      extensionsSummary: suggestion.extensionsSummary,
      markers: suggestion.markers ?? [],
      techHints: suggestion.techHints ?? [],
      createdAt: index >= 0 ? this.mockProjects[index].createdAt : now,
      updatedAt: now
    };

    if (index < 0) {
      this.mockProjects = [mapped, ...this.mockProjects];
      return;
    }

    this.mockProjects = [
      ...this.mockProjects.slice(0, index),
      mapped,
      ...this.mockProjects.slice(index + 1)
    ];
  }

  private createMockProjectKey(path: string, kind: string): string {
    return `${(kind ?? '').trim().toLowerCase()}::${(path ?? '').trim().toLowerCase()}`;
  }

  private createId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return crypto.randomUUID();
    }
    return Math.random().toString(36).slice(2) + Date.now().toString(36);
  }
}
