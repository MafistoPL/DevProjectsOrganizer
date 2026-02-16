import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type ScanSessionView = {
  id: string;
  rootPath: string;
  mode: string;
  state: string;
  disk: string;
  currentPath?: string | null;
  filesScanned: number;
  totalFiles?: number | null;
  queueReason?: string | null;
  outputPath?: string | null;
  progress: number;
  eta?: string | null;
  createdAt: string;
  startedAt?: string | null;
  finishedAt?: string | null;
};

export type StartScanPayload = {
  mode: 'roots' | 'whole' | 'changed';
  rootId?: string | null;
  depthLimit?: number | null;
};

type ScanEvent = {
  type: string;
  data?: unknown;
};

@Injectable({ providedIn: 'root' })
export class ScanService {
  private static readonly sessionStartedAt = Date.now();
  private readonly scansSubject = new BehaviorSubject<ScanSessionView[]>([]);
  readonly scans$ = this.scansSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    void this.load();
    this.bridge.events$.subscribe((event) => this.handleEvent(event as ScanEvent));
  }

  async load(): Promise<void> {
    const scans = await this.bridge.request<ScanSessionView[]>('scan.list');
    this.scansSubject.next(scans.map((scan) => this.normalize(scan)));
  }

  async startScan(payload: StartScanPayload): Promise<ScanSessionView> {
    const session = await this.bridge.request<ScanSessionView>('scan.start', payload);
    this.upsert(session);
    return session;
  }

  async pause(scanId: string): Promise<void> {
    await this.bridge.request('scan.pause', { id: scanId });
  }

  async resume(scanId: string): Promise<void> {
    await this.bridge.request('scan.resume', { id: scanId });
  }

  async stop(scanId: string): Promise<void> {
    await this.bridge.request('scan.stop', { id: scanId });
  }

  clearCompleted(scanId: string): void {
    const current = this.scansSubject.getValue();
    this.scansSubject.next(
      current.filter((scan) => !(scan.id === scanId && scan.state === 'Completed'))
    );
  }

  getSessionStartedAt(): number {
    return ScanService.sessionStartedAt;
  }

  private handleEvent(event: ScanEvent): void {
    if (!event?.type) {
      return;
    }

    if (event.type === 'scan.progress' && event.data) {
      this.upsert(event.data as ScanSessionView);
      return;
    }

    if (event.type === 'scan.completed' && event.data) {
      const payload = event.data as { id: string; outputPath?: string | null };
      const now = new Date().toISOString();
      if (payload.outputPath) {
        this.updateState(payload.id, {
          state: 'Completed',
          outputPath: payload.outputPath,
          finishedAt: now
        });
        return;
      }

      this.updateState(payload.id, {
        finishedAt: now
      });
      return;
    }

    if (event.type === 'scan.failed' && event.data) {
      const payload = event.data as { id: string };
      this.updateState(payload.id, { state: 'Failed', finishedAt: new Date().toISOString() });
    }
  }

  private updateState(scanId: string, update: Partial<ScanSessionView>): void {
    const current = this.scansSubject.getValue();
    const next = current.map((scan) =>
      scan.id === scanId ? this.normalize({ ...scan, ...update }) : scan
    );
    this.scansSubject.next(next);
  }

  private upsert(scan: ScanSessionView): void {
    const normalized = this.normalize(scan);
    const current = this.scansSubject.getValue();
    const index = current.findIndex((item) => item.id === normalized.id);
    if (index === -1) {
      this.scansSubject.next([normalized, ...current]);
      return;
    }
    const next = [...current];
    next[index] = normalized;
    this.scansSubject.next(next);
  }

  private normalize(scan: ScanSessionView): ScanSessionView {
    const totalFiles = scan.totalFiles ?? null;
    const filesScanned = scan.filesScanned ?? 0;
    const nowIso = new Date().toISOString();
    const createdAt = this.normalizeIsoTimestamp(scan.createdAt, nowIso) ?? nowIso;
    const startedAt = this.normalizeIsoTimestamp(scan.startedAt ?? null, null);
    const finishedAt = this.normalizeIsoTimestamp(scan.finishedAt ?? null, null);
    const progress =
      totalFiles && totalFiles > 0 ? Math.min(100, (filesScanned / totalFiles) * 100) : 0;
    return {
      ...scan,
      totalFiles,
      filesScanned,
      progress,
      createdAt,
      startedAt,
      finishedAt
    };
  }

  private normalizeIsoTimestamp(
    value: string | null | undefined,
    fallback: string | null
  ): string | null {
    if (!value) {
      return fallback;
    }

    const timestamp = Date.parse(value);
    if (Number.isNaN(timestamp)) {
      return fallback;
    }

    return new Date(timestamp).toISOString();
  }
}
