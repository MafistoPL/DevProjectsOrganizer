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

  clearSelectedRoots(): void {
    this.selectedRootIdsSubject.next([]);
  }

  private sanitizeSelectedRootIds(roots: RootItem[]): void {
    const allowed = new Set(roots.map((root) => root.id));
    const current = this.selectedRootIdsSubject.getValue();
    const sanitized = current.filter((id) => allowed.has(id));
    if (sanitized.length !== current.length) {
      this.selectedRootIdsSubject.next(sanitized);
    }
  }
}
