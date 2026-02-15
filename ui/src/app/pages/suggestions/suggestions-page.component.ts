import { NgFor, NgIf } from '@angular/common';
import { ChangeDetectorRef, Component, ElementRef, ViewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
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
    MatTooltipModule,
    ProjectSuggestionListComponent,
    TagSuggestionListComponent
  ],
  templateUrl: './suggestions-page.component.html',
  styleUrl: './suggestions-page.component.scss'
})
export class SuggestionsPageComponent {
  @ViewChild('regressionPanel') private regressionPanel?: ElementRef<HTMLElement>;

  projectScope: ProjectSuggestionsScope = 'pending';
  regressionReport: SuggestionsRegressionReport | null = null;
  regressionError: string | null = null;
  isRegressionBusy = false;

  constructor(
    private readonly suggestionsService: SuggestionsService,
    private readonly snackBar: MatSnackBar,
    private readonly dialog: MatDialog,
    private readonly cdr: ChangeDetectorRef
  ) {}

  async runRegressionReport(): Promise<void> {
    this.isRegressionBusy = true;
    this.regressionError = null;
    try {
      this.regressionReport = await this.suggestionsService.runRegressionReport();
      this.cdr.detectChanges();
      void this.scrollToRegressionPanel();
      this.snackBar.open('Regression report ready', undefined, { duration: 1400 });
    } catch (error) {
      this.regressionReport = null;
      this.regressionError = this.getErrorMessage(error);
      this.cdr.detectChanges();
      void this.scrollToRegressionPanel();
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

  private async scrollToRegressionPanel(): Promise<void> {
    for (let attempt = 0; attempt < 24; attempt += 1) {
      await this.waitForNextFrame();
      const panel = this.findRegressionPanel();
      if (!panel) {
        continue;
      }

      const shellScrollContainer = this.findShellScrollContainer();
      if (shellScrollContainer) {
        if (this.isElementTopVisibleInContainer(panel, shellScrollContainer)) {
          return;
        }

        const containerRect = shellScrollContainer.getBoundingClientRect();
        const panelRect = panel.getBoundingClientRect();
        const targetTop = Math.max(
          0,
          shellScrollContainer.scrollTop + (panelRect.top - containerRect.top) - 8
        );
        shellScrollContainer.scrollTo({
          top: targetTop,
          behavior: 'auto'
        });
        await this.waitForNextFrame();
        if (this.isElementTopVisibleInContainer(panel, shellScrollContainer)) {
          return;
        }
        continue;
      } else if (typeof panel.scrollIntoView === 'function') {
        panel.scrollIntoView({
          behavior: 'auto',
          block: 'start'
        });
      }
      return;
    }
  }

  private findRegressionPanel(): HTMLElement | null {
    if (this.regressionPanel?.nativeElement) {
      return this.regressionPanel.nativeElement;
    }

    if (typeof document === 'undefined') {
      return null;
    }

    return document.querySelector('[data-testid="regression-panel"]') as HTMLElement | null;
  }

  private findShellScrollContainer(): HTMLElement | null {
    if (typeof document === 'undefined') {
      return null;
    }

    return document.querySelector('mat-tab-nav-panel.content-inner') as HTMLElement | null;
  }

  private isElementTopVisibleInContainer(element: HTMLElement, container: HTMLElement): boolean {
    const elementRect = element.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    return elementRect.top >= containerRect.top - 2 && elementRect.top < containerRect.bottom - 36;
  }

  private waitForNextFrame(): Promise<void> {
    return new Promise((resolve) => {
      if (typeof requestAnimationFrame === 'function') {
        requestAnimationFrame(() => resolve());
        return;
      }

      window.setTimeout(() => resolve(), 0);
    });
  }
}
