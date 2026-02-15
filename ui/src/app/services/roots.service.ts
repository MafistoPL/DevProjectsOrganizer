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
  private static readonly SelectionStateStorageKey = 'scan.selectedRoots.state.v1';

  private readonly rootsSubject = new BehaviorSubject<RootItem[]>([]);
  readonly roots$ = this.rootsSubject.asObservable();
  private readonly selectedRootIdsSubject = new BehaviorSubject<string[]>([]);
  readonly selectedRootIds$ = this.selectedRootIdsSubject.asObservable();
  private readonly selectedRootDepthByIdSubject = new BehaviorSubject<Record<string, number | null>>({});
  readonly selectedRootDepthById$ = this.selectedRootDepthByIdSubject.asObservable();
  private readonly storage = this.resolveStorage();

  constructor(private readonly bridge: AppHostBridgeService) {
    this.restoreSelectionState();
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
      this.persistSelectionState();
      return;
    }

    if (!selected && hasId) {
      this.selectedRootIdsSubject.next(current.filter((item) => item !== id));
      this.persistSelectionState();
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
      this.persistSelectionState();
      return;
    }

    this.selectedRootDepthByIdSubject.next({
      ...current,
      [id]: depth
    });
    this.persistSelectionState();
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
    this.persistSelectionState();
  }

  private sanitizeSelectedRootIds(roots: RootItem[]): void {
    const allowed = new Set(roots.map((root) => root.id));
    const current = this.selectedRootIdsSubject.getValue();
    const sanitized = current.filter((id) => allowed.has(id));
    let hasStateChanged = false;
    if (sanitized.length !== current.length) {
      this.selectedRootIdsSubject.next(sanitized);
      hasStateChanged = true;
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
      hasStateChanged = true;
    }

    if (hasStateChanged) {
      this.persistSelectionState();
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

  private restoreSelectionState(): void {
    if (!this.storage) {
      return;
    }

    try {
      const raw = this.storage.getItem(RootsService.SelectionStateStorageKey);
      if (!raw) {
        return;
      }

      const parsed = JSON.parse(raw) as {
        selectedRootIds?: unknown;
        selectedRootDepthById?: unknown;
      };

      const selectedRootIds = this.normalizeSelectedRootIds(parsed.selectedRootIds);
      const selectedRootDepthById = this.normalizeSelectedRootDepthById(parsed.selectedRootDepthById);

      this.selectedRootIdsSubject.next(selectedRootIds);
      this.selectedRootDepthByIdSubject.next(selectedRootDepthById);
    } catch {
      // Ignore invalid persisted state.
    }
  }

  private persistSelectionState(): void {
    if (!this.storage) {
      return;
    }

    try {
      this.storage.setItem(
        RootsService.SelectionStateStorageKey,
        JSON.stringify({
          selectedRootIds: this.selectedRootIdsSubject.getValue(),
          selectedRootDepthById: this.selectedRootDepthByIdSubject.getValue()
        })
      );
    } catch {
      // Ignore persistence errors (storage quota/private mode).
    }
  }

  private normalizeSelectedRootIds(value: unknown): string[] {
    if (!Array.isArray(value)) {
      return [];
    }

    const output: string[] = [];
    const seen = new Set<string>();
    for (const item of value) {
      if (typeof item !== 'string') {
        continue;
      }

      const id = item.trim();
      if (!id || seen.has(id)) {
        continue;
      }

      output.push(id);
      seen.add(id);
    }

    return output;
  }

  private normalizeSelectedRootDepthById(value: unknown): Record<string, number | null> {
    if (!value || typeof value !== 'object') {
      return {};
    }

    const entries = Object.entries(value as Record<string, unknown>);
    const output: Record<string, number | null> = {};
    for (const [rawId, rawDepth] of entries) {
      const id = rawId.trim();
      if (!id) {
        continue;
      }

      const depth = this.normalizeDepth(rawDepth as number | string | null);
      if (depth === null) {
        continue;
      }

      output[id] = depth;
    }

    return output;
  }

  private resolveStorage(): Storage | null {
    try {
      return globalThis.localStorage ?? null;
    } catch {
      return null;
    }
  }
}
