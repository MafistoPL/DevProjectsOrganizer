import { NgFor, NgIf } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDialog } from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatSnackBar } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { TagSuggestionItem, TagSuggestionsService } from '../../../services/tag-suggestions.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-tag-suggestion-list',
  imports: [NgFor, NgIf, MatButtonModule, MatButtonToggleModule, MatExpansionModule],
  templateUrl: './tag-suggestion-list.component.html',
  styleUrl: './tag-suggestion-list.component.scss'
})
export class TagSuggestionListComponent {
  private readonly suggestionsService = inject(TagSuggestionsService);
  private readonly snackBar = inject(MatSnackBar);
  layout: 'list' | 'grid' = 'list';
  private readonly itemsSignal = toSignal(this.suggestionsService.items$, { initialValue: [] as TagSuggestionItem[] });

  constructor(private readonly dialog: MatDialog) {}

  get items(): TagSuggestionItem[] {
    return [...this.itemsSignal()].sort(
      (a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime()
    );
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
