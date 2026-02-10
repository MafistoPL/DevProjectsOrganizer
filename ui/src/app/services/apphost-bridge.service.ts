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

  private createId(): string {
    if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
      return crypto.randomUUID();
    }
    return Math.random().toString(36).slice(2) + Date.now().toString(36);
  }
}
