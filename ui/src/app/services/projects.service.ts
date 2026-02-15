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
  description: string;
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

export type TagHeuristicsRegressionProjectReport = {
  projectId: string;
  projectName: string;
  baselineAcceptedCount: number;
  baselineRejectedCount: number;
  acceptedMissingCount: number;
  rejectedMissingCount: number;
  addedCount: number;
};

export type TagHeuristicsRegressionReport = {
  projectsAnalyzed: number;
  baselineAcceptedCount: number;
  baselineRejectedCount: number;
  acceptedMissingCount: number;
  rejectedMissingCount: number;
  addedCount: number;
  projects: TagHeuristicsRegressionProjectReport[];
};

export type ProjectHeuristicsBatchResult = {
  total: number;
  processed: number;
  failed: number;
  generatedTotal: number;
  regressionReport: TagHeuristicsRegressionReport;
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
    regression?: {
      baselineAcceptedCount: number;
      baselineRejectedCount: number;
      acceptedMissingCount: number;
      rejectedMissingCount: number;
      addedCount: number;
    };
  }> {
    return await this.bridge.request<{
      generatedCount: number;
      runId?: string;
      outputPath?: string;
      regression?: {
        baselineAcceptedCount: number;
        baselineRejectedCount: number;
        acceptedMissingCount: number;
        rejectedMissingCount: number;
        addedCount: number;
      };
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
    const regressionProjects: TagHeuristicsRegressionProjectReport[] = [];
    let regressionBaselineAcceptedCount = 0;
    let regressionBaselineRejectedCount = 0;
    let regressionAcceptedMissingCount = 0;
    let regressionRejectedMissingCount = 0;
    let regressionAddedCount = 0;

    for (const [index, project] of projects.entries()) {
      let generatedCount = 0;
      try {
        const result = await this.runTagHeuristics(project.id);
        generatedCount = result.generatedCount ?? 0;
        generatedTotal += generatedCount;

        const regression = result.regression;
        if (regression) {
          regressionBaselineAcceptedCount += regression.baselineAcceptedCount;
          regressionBaselineRejectedCount += regression.baselineRejectedCount;
          regressionAcceptedMissingCount += regression.acceptedMissingCount;
          regressionRejectedMissingCount += regression.rejectedMissingCount;
          regressionAddedCount += regression.addedCount;
          regressionProjects.push({
            projectId: project.id,
            projectName: project.name,
            baselineAcceptedCount: regression.baselineAcceptedCount,
            baselineRejectedCount: regression.baselineRejectedCount,
            acceptedMissingCount: regression.acceptedMissingCount,
            rejectedMissingCount: regression.rejectedMissingCount,
            addedCount: regression.addedCount
          });
        } else {
          regressionProjects.push({
            projectId: project.id,
            projectName: project.name,
            baselineAcceptedCount: 0,
            baselineRejectedCount: 0,
            acceptedMissingCount: 0,
            rejectedMissingCount: 0,
            addedCount: 0
          });
        }
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
      generatedTotal,
      regressionReport: {
        projectsAnalyzed: regressionProjects.length,
        baselineAcceptedCount: regressionBaselineAcceptedCount,
        baselineRejectedCount: regressionBaselineRejectedCount,
        acceptedMissingCount: regressionAcceptedMissingCount,
        rejectedMissingCount: regressionRejectedMissingCount,
        addedCount: regressionAddedCount,
        projects: regressionProjects
      }
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

  async updateProjectDescription(
    projectId: string,
    description: string
  ): Promise<{ id: string; updated: boolean; description: string }> {
    const result = await this.bridge.request<{ id: string; updated: boolean; description: string }>(
      'projects.update',
      {
        projectId,
        description
      }
    );
    await this.load();
    return result;
  }
}
