import { NgFor, NgIf } from '@angular/common';
import { Component, Input, OnDestroy } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltip, MatTooltipModule } from '@angular/material/tooltip';
import { Subscription } from 'rxjs';
import { ProjectSuggestionItem, SuggestionsService, type SuggestionStatus } from '../../../services/suggestions.service';


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
    MatSnackBarModule,
    MatSelectModule,
    MatTooltipModule
  ],
  templateUrl: './project-suggestion-list.component.html',
  styleUrl: './project-suggestion-list.component.scss'
})
export class ProjectSuggestionListComponent implements OnDestroy {
  @Input() mode: 'live' | 'suggestions' = 'live';

  layout: 'list' | 'grid' = 'list';
  scope: 'pending' | 'archive' = 'pending';
  openId: string | null = null;
  sortKey: 'name' | 'score' | 'createdAt' = 'name';
  sortDir: 'asc' | 'desc' = 'asc';
  searchTerm = '';
  private readonly subscription: Subscription;
  private exportTooltipTimer: number | null = null;

  private items: ProjectSuggestionItem[] = [];

  constructor(
    private readonly suggestionsService: SuggestionsService,
    private readonly snackBar: MatSnackBar
  ) {
    this.subscription = this.suggestionsService.items$.subscribe((items) => {
      this.items = items;
      if (this.openId && !items.some((item) => item.id === this.openId)) {
        this.openId = null;
      }
    });
  }

  get visibleItems(): ProjectSuggestionItem[] {
    const byScope = this.items.filter((item) => {
      if (this.mode === 'live') {
        return item.status === 'pending';
      }

      if (this.scope === 'pending') {
        return item.status === 'pending';
      }

      return item.status === 'accepted' || item.status === 'rejected';
    });

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

  toggleDetails(id: string): void {
    this.openId = this.openId === id ? null : id;
  }

  isOverflowing(el: HTMLElement | null): boolean {
    if (!el) {
      return false;
    }
    return el.scrollWidth > el.clientWidth;
  }

  async setStatus(id: string, status: SuggestionStatus): Promise<void> {
    await this.suggestionsService.setStatus(id, status);
  }

  async copyDebugJson(id: string): Promise<void> {
    try {
      const json = await this.suggestionsService.exportDebugJson(id);
      const copied = await this.writeClipboard(json);
      if (!copied) {
        this.snackBar.open('Copy failed', 'Close', { duration: 1500 });
        return;
      }

      this.snackBar.open('Copied to clipboard', undefined, { duration: 1200 });
    } catch {
      this.snackBar.open('Copy failed', 'Close', { duration: 1500 });
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

  ngOnDestroy(): void {
    this.subscription.unsubscribe();
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
}
