import { NgFor, NgIf } from '@angular/common';
import { Component, Input, OnDestroy, effect, inject } from '@angular/core';
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
import { MatTooltip, MatTooltipModule } from '@angular/material/tooltip';
import { firstValueFrom } from 'rxjs';
import { ProjectsService } from '../../../services/projects.service';
import { ProjectSuggestionItem, SuggestionsService, type SuggestionStatus } from '../../../services/suggestions.service';
import {
  ProjectAcceptAction,
  ProjectAcceptActionDialogComponent
} from '../project-accept-action-dialog/project-accept-action-dialog.component';


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
export class ProjectSuggestionListComponent implements OnDestroy {
  @Input() mode: 'live' | 'suggestions' = 'live';

  private readonly suggestionsService = inject(SuggestionsService);
  private readonly projectsService = inject(ProjectsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  layout: 'list' | 'grid' = 'list';
  scope: 'pending' | 'archive' = 'pending';
  openId: string | null = null;
  sortKey: 'name' | 'score' | 'createdAt' = 'name';
  sortDir: 'asc' | 'desc' = 'asc';
  searchTerm = '';
  gridCardSizePercent = 100;
  private exportTooltipTimer: number | null = null;

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
        return item.status === 'pending';
      }

      if (this.scope === 'pending') {
        return item.status === 'pending';
      }

      return item.status === 'accepted' || item.status === 'rejected';
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
    await this.suggestionsService.setStatus(id, status);
    if (status !== 'accepted') {
      return;
    }

    const project = this.projectsService.findBySourceSuggestionId(id);
    if (!project) {
      this.snackBar.open('Project created, but action dialog is unavailable.', 'Close', { duration: 1800 });
      return;
    }

    const selectedAction = await this.promptPostAcceptAction(item?.name ?? project.name);
    if (selectedAction === 'skip') {
      return;
    }

    try {
      if (selectedAction === 'heuristics') {
        await this.projectsService.runTagHeuristics(project.id);
        this.snackBar.open('Tag heuristics queued', undefined, { duration: 1400 });
      } else {
        await this.projectsService.runAiTagSuggestions(project.id);
        this.snackBar.open('AI tag suggestions queued', undefined, { duration: 1400 });
      }
    } catch {
      this.snackBar.open('Post-accept action failed', 'Close', { duration: 1800 });
    }
  }

  shouldShowActions(item: ProjectSuggestionItem): boolean {
    return this.canAccept(item) || this.canReject() || this.canDelete();
  }

  canAccept(item: ProjectSuggestionItem): boolean {
    if (!this.isArchiveScope()) {
      return true;
    }

    return item.status === 'rejected';
  }

  canReject(): boolean {
    return !this.isArchiveScope();
  }

  canDelete(): boolean {
    return this.isArchiveScope();
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

  async exportArchiveJson(doneTooltip: MatTooltip): Promise<void> {
    try {
      await this.suggestionsService.exportArchiveJson();
      doneTooltip.show();
      if (this.exportTooltipTimer !== null) {
        window.clearTimeout(this.exportTooltipTimer);
      }
      this.exportTooltipTimer = window.setTimeout(() => {
        doneTooltip.hide();
        this.exportTooltipTimer = null;
      }, 1400);
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

  ngOnDestroy(): void {
    if (this.exportTooltipTimer !== null) {
      window.clearTimeout(this.exportTooltipTimer);
      this.exportTooltipTimer = null;
    }
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

  private isArchiveScope(): boolean {
    return this.mode === 'suggestions' && this.scope === 'archive';
  }

  private async promptPostAcceptAction(projectName: string): Promise<ProjectAcceptAction> {
    const ref = this.dialog.open(ProjectAcceptActionDialogComponent, {
      width: '520px',
      data: {
        projectName
      }
    });

    const selectedAction = await firstValueFrom(ref.afterClosed());
    return selectedAction ?? 'skip';
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
