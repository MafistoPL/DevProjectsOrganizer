import { DatePipe, NgFor, NgIf } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import { ConfirmDialogComponent } from '../../components/shared/confirm-dialog/confirm-dialog.component';
import {
  TagDeleteDialogComponent
} from '../../components/tags/tag-delete-dialog/tag-delete-dialog.component';
import {
  TagProjectsDialogComponent
} from '../../components/tags/tag-projects-dialog/tag-projects-dialog.component';
import { ProjectsService, TagHeuristicsRegressionReport } from '../../services/projects.service';
import { TagsService, type TagItem } from '../../services/tags.service';

@Component({
  selector: 'app-tags-page',
  imports: [
    DatePipe,
    NgFor,
    NgIf,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule
  ],
  templateUrl: './tags-page.component.html',
  styleUrl: './tags-page.component.scss'
})
export class TagsPageComponent {
  private readonly tagsService = inject(TagsService);
  private readonly projectsService = inject(ProjectsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly tags = toSignal(this.tagsService.tags$, { initialValue: [] });
  newTagName = '';
  editTagId: string | null = null;
  editTagName = '';
  readonly isApplyHeuristicsBusy = signal(false);
  readonly applyHeuristicsStatus = signal('');
  readonly heuristicsRegressionReport = signal<TagHeuristicsRegressionReport | null>(null);

  async addTag(): Promise<void> {
    try {
      await this.tagsService.addTag(this.newTagName);
      this.newTagName = '';
      this.snackBar.open('Tag added', undefined, { duration: 1200 });
    } catch (error) {
      this.snackBar.open(this.getErrorMessage(error), 'Close', { duration: 1600 });
    }
  }

  beginEdit(tag: TagItem): void {
    this.editTagId = tag.id;
    this.editTagName = tag.name;
  }

  cancelEdit(): void {
    this.editTagId = null;
    this.editTagName = '';
  }

  async saveEdit(tagId: string): Promise<void> {
    try {
      await this.tagsService.updateTag(tagId, this.editTagName);
      this.cancelEdit();
      this.snackBar.open('Tag updated', undefined, { duration: 1200 });
    } catch (error) {
      this.snackBar.open(this.getErrorMessage(error), 'Close', { duration: 1600 });
    }
  }

  async deleteTag(tag: TagItem): Promise<void> {
    const ref = this.dialog.open(TagDeleteDialogComponent, {
      width: '520px',
      data: {
        tagName: tag.name
      }
    });

    const confirmed = await firstValueFrom(ref.afterClosed());
    if (!confirmed) {
      return;
    }

    try {
      await this.tagsService.deleteTag(tag.id);
      this.cancelEdit();
      this.snackBar.open('Tag deleted', undefined, { duration: 1200 });
    } catch (error) {
      this.snackBar.open(this.getErrorMessage(error), 'Close', { duration: 1600 });
    }
  }

  async openProjects(tag: TagItem): Promise<void> {
    try {
      const projects = await this.tagsService.listProjects(tag.id);
      this.dialog.open(TagProjectsDialogComponent, {
        width: '760px',
        maxWidth: '95vw',
        data: {
          tagName: tag.name,
          projects
        }
      });
    } catch (error) {
      this.snackBar.open(this.getErrorMessage(error), 'Close', { duration: 1600 });
    }
  }

  async applyLatestHeuristicsToAllProjects(): Promise<void> {
    if (this.isApplyHeuristicsBusy()) {
      return;
    }

    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '460px',
      data: {
        title: 'Apply latest heuristics to all projects',
        message:
          'Run tag heuristics for all existing projects now? This can take a while on large project sets.',
        confirmText: 'Run',
        cancelText: 'Cancel',
        confirmColor: 'primary'
      }
    });

    const confirmed = await firstValueFrom(ref.afterClosed());
    if (!confirmed) {
      return;
    }

    this.isApplyHeuristicsBusy.set(true);
    this.applyHeuristicsStatus.set('Starting...');
    this.heuristicsRegressionReport.set(null);
    try {
      const summary = await this.projectsService.runTagHeuristicsForAll((progress) => {
        this.applyHeuristicsStatus.set(
          `[${progress.index}/${progress.total}] ${progress.projectName} - +${progress.generatedCount} (total +${progress.generatedTotal})`
        );
      });

      if (summary.total === 0) {
        this.applyHeuristicsStatus.set('No projects found.');
      } else {
        this.applyHeuristicsStatus.set(
          `Done. Processed ${summary.processed}/${summary.total}, generated +${summary.generatedTotal}, failed ${summary.failed}.`
        );
      }

      this.heuristicsRegressionReport.set(summary.regressionReport);
      this.snackBar.open(this.applyHeuristicsStatus(), undefined, { duration: 2200 });
    } catch (error) {
      this.applyHeuristicsStatus.set(this.getErrorMessage(error));
      this.snackBar.open(this.applyHeuristicsStatus(), 'Close', { duration: 2200 });
    } finally {
      this.isApplyHeuristicsBusy.set(false);
    }
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof Error && error.message) {
      return error.message;
    }

    return 'Operation failed';
  }
}
