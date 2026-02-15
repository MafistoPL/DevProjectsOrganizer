import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type RootItem = {
  id: string;
  path: string;
  status: string;
  projectCount?: number;
  ongoingSuggestionCount?: number;
  lastScanState?: string | null;
  lastScanAt?: string | null;
  lastScanFiles?: number | null;
};

@Injectable({ providedIn: 'root' })
export class RootsService {
  private readonly rootsSubject = new BehaviorSubject<RootItem[]>([]);
  readonly roots$ = this.rootsSubject.asObservable();
  private readonly selectedRootIdsSubject = new BehaviorSubject<string[]>([]);
  readonly selectedRootIds$ = this.selectedRootIdsSubject.asObservable();
  private readonly selectedRootDepthByIdSubject = new BehaviorSubject<Record<string, number | null>>({});
  readonly selectedRootDepthById$ = this.selectedRootDepthByIdSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    void this.load();
  }

  async load(): Promise<void> {
    const roots = await this.bridge.request<RootItem[]>('roots.list');
    this.rootsSubject.next(roots);
    this.sanitizeSelectedRootIds(roots);
  }

  async addRoot(path: string): Promise<void> {
    await this.bridge.request<RootItem>('roots.add', { path });
    await this.load();
  }

  async updateRoot(id: string, path: string): Promise<void> {
    await this.bridge.request<RootItem>('roots.update', { id, path });
    await this.load();
  }

  async deleteRoot(id: string): Promise<void> {
    await this.bridge.request<{ id: string; deleted: boolean }>('roots.delete', { id });
    await this.load();
  }

  setRootSelected(rootId: string, selected: boolean): void {
    const id = rootId.trim();
    if (!id) {
      return;
    }

    const current = this.selectedRootIdsSubject.getValue();
    const hasId = current.includes(id);
    if (selected && !hasId) {
      this.selectedRootIdsSubject.next([...current, id]);
      return;
    }

    if (!selected && hasId) {
      this.selectedRootIdsSubject.next(current.filter((item) => item !== id));
    }
  }

  isRootSelected(rootId: string): boolean {
    return this.selectedRootIdsSubject.getValue().includes(rootId);
  }

  getSelectedRootIdsSnapshot(): string[] {
    return [...this.selectedRootIdsSubject.getValue()];
  }

  getSelectedRootsSnapshot(): RootItem[] {
    const selectedIds = new Set(this.selectedRootIdsSubject.getValue());
    return this.rootsSubject.getValue().filter((root) => selectedIds.has(root.id));
  }

  getSelectedRescanTargetsSnapshot(): Array<{ root: RootItem; depthLimit: number | null }> {
    const selectedIds = new Set(this.selectedRootIdsSubject.getValue());
    const depthById = this.selectedRootDepthByIdSubject.getValue();
    return this.rootsSubject
      .getValue()
      .filter((root) => selectedIds.has(root.id))
      .map((root) => ({
        root,
        depthLimit: depthById[root.id] ?? null
      }));
  }

  setRootDepth(rootId: string, rawDepth: number | string | null): void {
    const id = rootId.trim();
    if (!id) {
      return;
    }

    const depth = this.normalizeDepth(rawDepth);
    const current = this.selectedRootDepthByIdSubject.getValue();
    if (depth === null) {
      if (!(id in current)) {
        return;
      }

      const { [id]: _, ...next } = current;
      this.selectedRootDepthByIdSubject.next(next);
      return;
    }

    this.selectedRootDepthByIdSubject.next({
      ...current,
      [id]: depth
    });
  }

  getRootDepth(rootId: string): number | null {
    const id = rootId.trim();
    if (!id) {
      return null;
    }

    return this.selectedRootDepthByIdSubject.getValue()[id] ?? null;
  }

  clearSelectedRoots(): void {
    this.selectedRootIdsSubject.next([]);
    this.selectedRootDepthByIdSubject.next({});
  }

  private sanitizeSelectedRootIds(roots: RootItem[]): void {
    const allowed = new Set(roots.map((root) => root.id));
    const current = this.selectedRootIdsSubject.getValue();
    const sanitized = current.filter((id) => allowed.has(id));
    if (sanitized.length !== current.length) {
      this.selectedRootIdsSubject.next(sanitized);
    }

    const depthCurrent = this.selectedRootDepthByIdSubject.getValue();
    const depthSanitized = Object.entries(depthCurrent).reduce<Record<string, number | null>>(
      (acc, [id, depth]) => {
        if (allowed.has(id)) {
          acc[id] = depth;
        }
        return acc;
      },
      {}
    );
    if (Object.keys(depthSanitized).length !== Object.keys(depthCurrent).length) {
      this.selectedRootDepthByIdSubject.next(depthSanitized);
    }
  }

  private normalizeDepth(rawDepth: number | string | null): number | null {
    if (rawDepth === null) {
      return null;
    }

    const parsed =
      typeof rawDepth === 'number'
        ? rawDepth
        : Number.parseInt(String(rawDepth).trim(), 10);
    if (!Number.isFinite(parsed) || parsed <= 0) {
      return null;
    }

    return parsed;
  }
}
