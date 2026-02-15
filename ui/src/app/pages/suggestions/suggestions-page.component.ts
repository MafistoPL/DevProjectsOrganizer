import { NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import {
  ConfirmDialogComponent
} from '../../components/shared/confirm-dialog/confirm-dialog.component';
import {
  ProjectSuggestionListComponent,
  ProjectSuggestionsScope
} from '../../components/suggestions/project-suggestion-list/project-suggestion-list.component';
import { TagSuggestionListComponent } from '../../components/suggestions/tag-suggestion-list/tag-suggestion-list.component';
import {
  SuggestionsRegressionReport,
  SuggestionsService
} from '../../services/suggestions.service';
import { firstValueFrom } from 'rxjs';

@Component({
  selector: 'app-suggestions-page',
  imports: [
    NgFor,
    NgIf,
    MatButtonModule,
    MatCardModule,
    MatSnackBarModule,
    ProjectSuggestionListComponent,
    TagSuggestionListComponent
  ],
  templateUrl: './suggestions-page.component.html',
  styleUrl: './suggestions-page.component.scss'
})
export class SuggestionsPageComponent {
  projectScope: ProjectSuggestionsScope = 'pending';
  regressionReport: SuggestionsRegressionReport | null = null;
  regressionError: string | null = null;
  isRegressionBusy = false;

  constructor(
    private readonly suggestionsService: SuggestionsService,
    private readonly snackBar: MatSnackBar,
    private readonly dialog: MatDialog
  ) {}

  async runRegressionReport(): Promise<void> {
    this.isRegressionBusy = true;
    this.regressionError = null;
    try {
      this.regressionReport = await this.suggestionsService.runRegressionReport();
      this.snackBar.open('Regression report ready', undefined, { duration: 1400 });
    } catch (error) {
      this.regressionReport = null;
      this.regressionError = this.getErrorMessage(error);
      this.snackBar.open('Regression report failed', 'Close', { duration: 1800 });
    } finally {
      this.isRegressionBusy = false;
    }
  }

  async exportRegressionReport(): Promise<void> {
    this.isRegressionBusy = true;
    this.regressionError = null;
    try {
      const result = await this.suggestionsService.exportRegressionReport();
      this.snackBar.open(`Exported: ${result.path}`, undefined, { duration: 1800 });
    } catch (error) {
      this.regressionError = this.getErrorMessage(error);
      this.snackBar.open('Regression export failed', 'Close', { duration: 1800 });
    } finally {
      this.isRegressionBusy = false;
    }
  }

  async acceptAllProjects(): Promise<void> {
    const confirmed = await this.confirmBulkAction(
      'Accept all project suggestions',
      'Are you sure you want to accept all pending project suggestions?',
      'Accept',
      'primary'
    );
    if (!confirmed) {
      return;
    }

    const updated = await this.suggestionsService.setPendingStatusForAll('accepted');
    this.snackBar.open(`Accepted ${updated} suggestion(s)`, undefined, { duration: 1400 });
  }

  async rejectAllProjects(): Promise<void> {
    const confirmed = await this.confirmBulkAction(
      'Reject all project suggestions',
      'Are you sure you want to reject all pending project suggestions?',
      'Reject',
      'warn'
    );
    if (!confirmed) {
      return;
    }

    const updated = await this.suggestionsService.setPendingStatusForAll('rejected');
    this.snackBar.open(`Rejected ${updated} suggestion(s)`, undefined, { duration: 1400 });
  }

  onProjectScopeChange(scope: ProjectSuggestionsScope): void {
    this.projectScope = scope;
  }

  async restoreAllRejectedProjects(): Promise<void> {
    const confirmed = await this.confirmBulkAction(
      'Restore rejected project suggestions',
      'Are you sure you want to restore all rejected project suggestions back to Pending?',
      'Restore',
      'primary'
    );
    if (!confirmed) {
      return;
    }

    const restored = await this.suggestionsService.restoreRejectedFromArchive();
    this.snackBar.open(`Restored ${restored} suggestion(s)`, undefined, { duration: 1400 });
  }

  async deleteAllRejectedProjects(): Promise<void> {
    const confirmed = await this.confirmBulkAction(
      'Delete rejected project suggestions',
      'Are you sure you want to permanently delete all rejected project suggestions?',
      'Delete',
      'warn'
    );
    if (!confirmed) {
      return;
    }

    const deleted = await this.suggestionsService.deleteRejectedFromArchive();
    this.snackBar.open(`Deleted ${deleted} suggestion(s)`, undefined, { duration: 1400 });
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

  private getErrorMessage(error: unknown): string {
    if (error instanceof Error && error.message) {
      return error.message;
    }
    return 'Unknown error';
  }
}
