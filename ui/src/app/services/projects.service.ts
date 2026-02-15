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

export type ProjectHeuristicsBatchProgress = {
  index: number;
  total: number;
  projectName: string;
  generatedCount: number;
  generatedTotal: number;
  failed: number;
};

export type ProjectHeuristicsBatchResult = {
  total: number;
  processed: number;
  failed: number;
  generatedTotal: number;
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

  getSnapshot(): ProjectItem[] {
    return [...this.projectsSubject.getValue()];
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

  async runTagHeuristicsForAll(
    onProgress?: (progress: ProjectHeuristicsBatchProgress) => void
  ): Promise<ProjectHeuristicsBatchResult> {
    await this.load();
    const projects = this.projectsSubject.getValue();
    const total = projects.length;

    let processed = 0;
    let failed = 0;
    let generatedTotal = 0;

    for (const [index, project] of projects.entries()) {
      let generatedCount = 0;
      try {
        const result = await this.runTagHeuristics(project.id);
        generatedCount = result.generatedCount ?? 0;
        generatedTotal += generatedCount;
      } catch {
        failed += 1;
      } finally {
        processed += 1;
        onProgress?.({
          index: index + 1,
          total,
          projectName: project.name,
          generatedCount,
          generatedTotal,
          failed
        });
      }
    }

    return {
      total,
      processed,
      failed,
      generatedTotal
    };
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
