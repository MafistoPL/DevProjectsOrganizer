import { NgFor, NgIf } from '@angular/common';
import { Component, EventEmitter, Input, Output, effect, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { ProjectsService } from '../../../services/projects.service';
import { ProjectSuggestionItem, SuggestionsService, type SuggestionStatus } from '../../../services/suggestions.service';
import {
  ProjectAcceptAction,
  ProjectAcceptDialogResult,
  ProjectAcceptActionDialogComponent
} from '../project-accept-action-dialog/project-accept-action-dialog.component';

export type ProjectSuggestionsScope = 'pending' | 'accepted' | 'rejected';

@Component({
  selector: 'app-project-suggestion-list',
  imports: [
    NgFor,
    NgIf,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatMenuModule,
    MatSnackBarModule,
    MatSelectModule,
    MatTooltipModule
  ],
  templateUrl: './project-suggestion-list.component.html',
  styleUrl: './project-suggestion-list.component.scss'
})
export class ProjectSuggestionListComponent {
  @Input() mode: 'live' | 'suggestions' = 'live';
  @Input() liveScanSessionId: string | null = null;
  @Output() scopeChange = new EventEmitter<ProjectSuggestionsScope>();

  private readonly suggestionsService = inject(SuggestionsService);
  private readonly projectsService = inject(ProjectsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  layout: 'list' | 'grid' = 'list';
  scope: ProjectSuggestionsScope = 'pending';
  openId: string | null = null;
  sortKey: 'name' | 'score' | 'createdAt' = 'name';
  sortDir: 'asc' | 'desc' = 'asc';
  searchTerm = '';
  gridCardSizePercent = 100;

  private readonly items = toSignal(this.suggestionsService.items$, {
    initialValue: [] as ProjectSuggestionItem[]
  });

  constructor() {
    effect(() => {
      const items = this.items();
      if (this.openId && !items.some((item) => item.id === this.openId)) {
        this.openId = null;
      }
    });
  }

  get visibleItems(): ProjectSuggestionItem[] {
    const byScopeRaw = this.items().filter((item) => {
      if (this.mode === 'live') {
        if (!this.liveScanSessionId) {
          return false;
        }

        return item.status === 'pending' && item.scanSessionId === this.liveScanSessionId;
      }

      if (this.scope === 'pending') {
        return item.status === 'pending';
      }

      if (this.scope === 'accepted') {
        return item.status === 'accepted';
      }

      return item.status === 'rejected';
    });
    const byScope = this.dedupeByPathAndKind(byScopeRaw);

    const term = this.searchTerm.trim().toLowerCase();
    const filtered = term
      ? byScope.filter((item) => item.name.toLowerCase().includes(term))
      : byScope;

    const sorted = [...filtered].sort((a, b) => {
      let result = 0;
      if (this.sortKey === 'name') {
        result = a.name.localeCompare(b.name);
      } else if (this.sortKey === 'score') {
        result = a.score - b.score;
      } else {
        result = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }
      return this.sortDir === 'asc' ? result : -result;
    });

    return sorted;
  }

  get showLiveEmpty(): boolean {
    return this.mode === 'live' && this.visibleItems.length === 0;
  }

  get gridScaleFactor(): string {
    return (this.gridCardSizePercent / 100).toFixed(2);
  }

  toggleDetails(id: string): void {
    this.openId = this.openId === id ? null : id;
  }

  isOverflowing(el: HTMLElement | null): boolean {
    if (!el) {
      return false;
    }
    return el.scrollWidth > el.clientWidth;
  }

  async setStatus(id: string, status: SuggestionStatus, item?: ProjectSuggestionItem): Promise<void> {
    if (status !== 'accepted') {
      await this.suggestionsService.setStatus(id, status);
      return;
    }

    const decision = await this.promptAcceptDecision(item?.name ?? '', '');
    if (!decision) {
      return;
    }

    await this.suggestionsService.setStatus(
      id,
      status,
      decision.projectName,
      decision.projectDescription
    );

    const project = this.projectsService.findBySourceSuggestionId(id);
    if (!project) {
      this.snackBar.open('Project created, but action dialog is unavailable.', 'Close', { duration: 1800 });
      return;
    }

    const selectedAction = decision.action;
    if (selectedAction === 'skip') {
      return;
    }

    try {
      if (selectedAction === 'heuristics') {
        const result = await this.projectsService.runTagHeuristics(project.id);
        this.snackBar.open(`Tag heuristics generated ${result.generatedCount}`, undefined, { duration: 1400 });
      } else {
        await this.projectsService.runAiTagSuggestions(project.id);
        this.snackBar.open('AI tag suggestions queued', undefined, { duration: 1400 });
      }
    } catch {
      this.snackBar.open('Post-accept action failed', 'Close', { duration: 1800 });
    }
  }

  setScope(scope: ProjectSuggestionsScope): void {
    this.scope = scope;
    this.scopeChange.emit(scope);
  }

  shouldShowActions(item: ProjectSuggestionItem): boolean {
    return this.canAccept(item) || this.canReject() || this.canDelete(item);
  }

  canAccept(item: ProjectSuggestionItem): boolean {
    if (this.mode === 'live') {
      return true;
    }

    if (this.scope === 'pending') {
      return true;
    }

    return this.scope === 'rejected' && item.status === 'rejected';
  }

  canReject(): boolean {
    if (this.mode === 'live') {
      return true;
    }

    return this.scope === 'pending';
  }

  canDelete(item: ProjectSuggestionItem): boolean {
    return this.mode === 'suggestions' && this.scope === 'rejected' && item.status === 'rejected';
  }

  async copyReason(reason: string): Promise<void> {
    const copied = await this.writeClipboard(reason);
    if (!copied) {
      this.snackBar.open('Copy failed', 'Close', { duration: 1500 });
      return;
    }

    this.snackBar.open('Reason copied', undefined, { duration: 1200 });
  }

  async copyPath(path: string): Promise<void> {
    const copied = await this.writeClipboard(path);
    if (!copied) {
      this.snackBar.open('Copy failed', 'Close', { duration: 1500 });
      return;
    }

    this.snackBar.open('Path copied', undefined, { duration: 1200 });
  }

  async openPath(path: string): Promise<void> {
    try {
      await this.suggestionsService.openPath(path);
      this.snackBar.open('Opened in Explorer', undefined, { duration: 1200 });
    } catch {
      this.snackBar.open('Open path failed', 'Close', { duration: 1500 });
    }
  }

  async exportArchiveJson(): Promise<void> {
    try {
      const result = await this.suggestionsService.exportArchiveJson();
      this.snackBar.open(`Exported ${result.count} archived suggestion(s)`, undefined, { duration: 1400 });
    } catch {
      this.snackBar.open('Archive export failed', 'Close', { duration: 1500 });
    }
  }

  async openArchiveFolder(): Promise<void> {
    try {
      const result = await this.suggestionsService.openArchiveFolder();
      this.snackBar.open(`Opened: ${result.path}`, undefined, { duration: 1500 });
    } catch {
      this.snackBar.open('Open folder failed', 'Close', { duration: 1500 });
    }
  }

  async deleteSuggestion(item: ProjectSuggestionItem): Promise<void> {
    await this.suggestionsService.deleteSuggestion(item.id);
    this.snackBar.open('Suggestion deleted', undefined, { duration: 1200 });
  }

  private async writeClipboard(text: string): Promise<boolean> {
    if (navigator.clipboard?.writeText) {
      try {
        await navigator.clipboard.writeText(text);
        return true;
      } catch {
        // Fallback below.
      }
    }

    const area = document.createElement('textarea');
    area.value = text;
    area.setAttribute('readonly', '');
    area.style.position = 'fixed';
    area.style.opacity = '0';
    area.style.pointerEvents = 'none';
    document.body.appendChild(area);
    area.select();
    const copied = document.execCommand('copy');
    document.body.removeChild(area);
    return copied;
  }

  private async promptAcceptDecision(
    projectName: string,
    projectDescription: string
  ): Promise<ProjectAcceptDialogResult | null> {
    const normalizedProjectName = projectName.trim();
    if (!normalizedProjectName) {
      return null;
    }

    const ref = this.dialog.open(ProjectAcceptActionDialogComponent, {
      width: '560px',
      disableClose: false,
      data: {
        projectName: normalizedProjectName,
        projectDescription: projectDescription.trim()
      }
    });

    const selected = await firstValueFrom(ref.afterClosed());
    if (!selected) {
      return null;
    }

    if (typeof selected === 'string') {
      return {
        action: selected as ProjectAcceptAction,
        projectName: normalizedProjectName,
        projectDescription: projectDescription.trim()
      };
    }

    const selectedName = selected.projectName?.trim();
    if (!selectedName) {
      return null;
    }

    return {
      action: selected.action,
      projectName: selectedName,
      projectDescription: selected.projectDescription?.trim() ?? ''
    };
  }

  private dedupeByPathAndKind(items: ProjectSuggestionItem[]): ProjectSuggestionItem[] {
    const newestFirst = [...items].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
    const seen = new Set<string>();
    const deduped: ProjectSuggestionItem[] = [];

    for (const item of newestFirst) {
      const key = `${item.kind}::${item.path.toLowerCase()}`;
      if (seen.has(key)) {
        continue;
      }
      seen.add(key);
      deduped.push(item);
    }

    return deduped;
  }
}
