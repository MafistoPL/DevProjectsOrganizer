import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { AppHostBridgeService } from './apphost-bridge.service';

export type ProjectTagItem = {
  id: string;
  name: string;
};

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
  tags: ProjectTagItem[];
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

  findBySourceSuggestionId(sourceSuggestionId: string): ProjectItem | null {
    const project =
      this.projectsSubject.getValue().find((item) => item.sourceSuggestionId === sourceSuggestionId) ?? null;
    return project;
  }

  async runTagHeuristics(projectId: string): Promise<{
    generatedCount: number;
    runId?: string;
    outputPath?: string;
  }> {
    return await this.bridge.request<{
      generatedCount: number;
      runId?: string;
      outputPath?: string;
    }>(
      'projects.runTagHeuristics',
      { projectId }
    );
  }

  async runAiTagSuggestions(projectId: string): Promise<{ action: string }> {
    return await this.bridge.request<{ action: string }>('projects.runAiTagSuggestions', { projectId });
  }

  async deleteProject(projectId: string, confirmName: string): Promise<{ id: string; deleted: boolean }> {
    const result = await this.bridge.request<{ id: string; deleted: boolean }>('projects.delete', {
      projectId,
      confirmName
    });
    await this.load();
    return result;
  }
}
