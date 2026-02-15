import { NgFor, NgIf } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { TagSuggestionItem, TagSuggestionsService } from '../../../services/tag-suggestions.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-tag-suggestion-list',
  imports: [
    NgFor,
    NgIf,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './tag-suggestion-list.component.html',
  styleUrl: './tag-suggestion-list.component.scss'
})
export class TagSuggestionListComponent {
  private readonly suggestionsService = inject(TagSuggestionsService);
  private readonly snackBar = inject(MatSnackBar);
  layout: 'list' | 'grid' = 'list';
  scope: 'pending' | 'accepted' | 'rejected' = 'pending';
  sortKey: 'projectName' | 'tagName' | 'createdAt' = 'createdAt';
  sortDir: 'asc' | 'desc' = 'desc';
  searchTerm = '';
  openId: string | null = null;
  gridCardSizePercent = 100;
  private readonly itemsSignal = toSignal(this.suggestionsService.items$, { initialValue: [] as TagSuggestionItem[] });

  constructor(private readonly dialog: MatDialog) {}

  get isSearchEnabled(): boolean {
    return this.sortKey !== 'createdAt';
  }

  get gridScaleFactor(): string {
    return (this.gridCardSizePercent / 100).toFixed(2);
  }

  get visibleItems(): TagSuggestionItem[] {
    const byScope = this.itemsSignal().filter((item) => item.status === this.scope);

    const term = this.searchTerm.trim().toLowerCase();
    const filtered = !this.isSearchEnabled || !term
      ? byScope
      : byScope.filter((item) =>
          this.sortKey === 'projectName'
            ? item.projectName.toLowerCase().includes(term)
            : item.tagName.toLowerCase().includes(term)
        );

    return [...filtered].sort((a, b) => {
      let result = 0;
      if (this.sortKey === 'projectName') {
        result = a.projectName.localeCompare(b.projectName, undefined, { sensitivity: 'base' });
      } else if (this.sortKey === 'tagName') {
        result = a.tagName.localeCompare(b.tagName, undefined, { sensitivity: 'base' });
      } else {
        result = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }

      return this.sortDir === 'asc' ? result : -result;
    });
  }

  onSortKeyChange(nextKey: 'projectName' | 'tagName' | 'createdAt'): void {
    this.sortKey = nextKey;
    if (nextKey === 'createdAt') {
      this.sortDir = 'desc';
      this.searchTerm = '';
    }
  }

  toggleDetails(id: string): void {
    this.openId = this.openId === id ? null : id;
  }

  async accept(item: TagSuggestionItem): Promise<void> {
    if (item.status !== 'pending') {
      return;
    }

    await this.suggestionsService.setStatus(item.id, 'accepted');
  }

  async reject(item: TagSuggestionItem): Promise<void> {
    if (item.status !== 'pending') {
      return;
    }

    await this.suggestionsService.setStatus(item.id, 'rejected');
  }

  async deleteSuggestion(item: TagSuggestionItem): Promise<void> {
    if (item.status !== 'rejected') {
      return;
    }

    await this.suggestionsService.deleteSuggestion(item.id);
    this.snackBar.open('Tag suggestion deleted', undefined, { duration: 1200 });
  }

  async acceptAll(): Promise<void> {
    const confirmed = await this.confirmBulkAction(
      'Accept all tag suggestions',
      'Are you sure you want to accept all pending tag suggestions?',
      'Accept',
      'primary'
    );
    if (!confirmed) {
      return;
    }

    const updated = await this.suggestionsService.setPendingStatusForAll('accepted');
    this.snackBar.open(`Accepted ${updated} tag suggestion(s)`, undefined, { duration: 1400 });
  }

  async rejectAll(): Promise<void> {
    const confirmed = await this.confirmBulkAction(
      'Reject all tag suggestions',
      'Are you sure you want to reject all pending tag suggestions?',
      'Reject',
      'warn'
    );
    if (!confirmed) {
      return;
    }

    const updated = await this.suggestionsService.setPendingStatusForAll('rejected');
    this.snackBar.open(`Rejected ${updated} tag suggestion(s)`, undefined, { duration: 1400 });
  }

  private async confirmBulkAction(
    title: string,
    message: string,
    confirmText: string,
    confirmColor: 'primary' | 'accent' | 'warn'
  ): Promise<boolean> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title,
        message,
        confirmText,
        cancelText: 'Cancel',
        confirmColor
      }
    });

    const result = await firstValueFrom(ref.afterClosed());
    return result === true;
  }
}
