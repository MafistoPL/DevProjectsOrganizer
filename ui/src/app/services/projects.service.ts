import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type ProjectItem = {
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
export class ProjectsService {
  private readonly projectsSubject = new BehaviorSubject<ProjectItem[]>([]);
  readonly projects$ = this.projectsSubject.asObservable();

  constructor(private readonly bridge: AppHostBridgeService) {
    void this.load();
    this.bridge.events$.subscribe((event) => {
      if (event?.type === 'projects.changed') {
        void this.load();
      }
    });
  }

  async load(): Promise<void> {
    const projects = await this.bridge.request<ProjectItem[]>('projects.list');
    this.projectsSubject.next(projects);
  }
}
