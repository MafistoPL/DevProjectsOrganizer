import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type RootItem = {
  id: string;
  path: string;
  status: string;
};

@Injectable({ providedIn: 'root' })
export class RootsService {
  private readonly rootsSubject = new BehaviorSubject<RootItem[]>([]);
  readonly roots$ = this.rootsSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    void this.load();
  }

  async load(): Promise<void> {
    const roots = await this.bridge.request<RootItem[]>('roots.list');
    this.rootsSubject.next(roots);
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
}
