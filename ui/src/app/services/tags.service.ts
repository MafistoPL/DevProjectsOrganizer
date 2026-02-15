import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type TagItem = {
  id: string;
  name: string;
  isSystem: boolean;
  projectCount: number;
  createdAt: string;
  updatedAt: string;
};

export type TagLinkedProject = {
  id: string;
  name: string;
  path: string;
  kind: string;
  updatedAt: string;
};

@Injectable({ providedIn: 'root' })
export class TagsService {
  private readonly tagsSubject = new BehaviorSubject<TagItem[]>([]);
  readonly tags$ = this.tagsSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    void this.load();
  }

  async load(): Promise<void> {
    const tags = await this.bridge.request<TagItem[]>('tags.list');
    this.tagsSubject.next(tags);
  }

  async addTag(name: string): Promise<void> {
    await this.bridge.request<TagItem>('tags.add', { name });
    await this.load();
  }

  async updateTag(id: string, name: string): Promise<void> {
    await this.bridge.request<TagItem>('tags.update', { id, name });
    await this.load();
  }

  async deleteTag(id: string): Promise<void> {
    await this.bridge.request<{ id: string; deleted: boolean }>('tags.delete', { id });
    await this.load();
  }

  async listProjects(tagId: string): Promise<TagLinkedProject[]> {
    return await this.bridge.request<TagLinkedProject[]>('tags.projects', { id: tagId });
  }
}
