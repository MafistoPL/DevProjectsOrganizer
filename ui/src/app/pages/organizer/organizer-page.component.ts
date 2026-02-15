import { DatePipe, NgFor, NgIf } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { firstValueFrom } from 'rxjs';
import {
  ProjectDeleteDialogComponent
} from '../../components/organizer/project-delete-dialog/project-delete-dialog.component';
import { ProjectsService, type ProjectItem } from '../../services/projects.service';
import { TagsService, type TagItem } from '../../services/tags.service';

@Component({
  selector: 'app-organizer-page',
  imports: [
    DatePipe,
    NgFor,
    NgIf,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSnackBarModule
  ],
  templateUrl: './organizer-page.component.html',
  styleUrl: './organizer-page.component.scss'
})
export class OrganizerPageComponent {
  private readonly projectsService = inject(ProjectsService);
  private readonly tagsService = inject(TagsService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly projects = toSignal(this.projectsService.projects$, { initialValue: [] });
  readonly tags = toSignal(this.tagsService.tags$, { initialValue: [] });
  editDescriptionProjectId: string | null = null;
  editDescriptionValue = '';
  private readonly selectedTagByProjectId = new Map<string, string>();

  async deleteProject(project: ProjectItem): Promise<void> {
    const ref = this.dialog.open(ProjectDeleteDialogComponent, {
      width: '520px',
      data: {
        projectName: project.name
      }
    });

    const confirmedName = await firstValueFrom(ref.afterClosed());
    if (typeof confirmedName !== 'string' || confirmedName.length === 0) {
      return;
    }

    try {
      const result = await this.projectsService.deleteProject(project.id, confirmedName);
      if (!result.deleted) {
        this.snackBar.open('Project was already removed.', undefined, { duration: 1400 });
        return;
      }

      this.snackBar.open('Project deleted', undefined, { duration: 1400 });
    } catch (error) {
      const message = error instanceof Error && error.message ? error.message : 'Project delete failed';
      this.snackBar.open(message, 'Close', { duration: 1800 });
    }
  }

  beginEditDescription(project: ProjectItem): void {
    this.editDescriptionProjectId = project.id;
    this.editDescriptionValue = project.description ?? '';
  }

  cancelEditDescription(): void {
    this.editDescriptionProjectId = null;
    this.editDescriptionValue = '';
  }

  async saveDescription(project: ProjectItem): Promise<void> {
    try {
      await this.projectsService.updateProjectDescription(project.id, this.editDescriptionValue);
      this.snackBar.open('Project description updated', undefined, { duration: 1400 });
      this.cancelEditDescription();
    } catch (error) {
      const message = error instanceof Error && error.message ? error.message : 'Project update failed';
      this.snackBar.open(message, 'Close', { duration: 1800 });
    }
  }

  getAttachableTags(project: ProjectItem): TagItem[] {
    const attachedTagIds = new Set(project.tags.map((tag) => tag.id));
    return this.tags().filter((tag) => !attachedTagIds.has(tag.id));
  }

  getSelectedTagId(projectId: string): string | null {
    return this.selectedTagByProjectId.get(projectId) ?? null;
  }

  setSelectedTagId(projectId: string, tagId: string | null): void {
    if (!tagId) {
      this.selectedTagByProjectId.delete(projectId);
      return;
    }

    this.selectedTagByProjectId.set(projectId, tagId);
  }

  async attachSelectedTag(project: ProjectItem): Promise<void> {
    const tagId = this.getSelectedTagId(project.id);
    if (!tagId) {
      this.snackBar.open('Select a tag first', 'Close', { duration: 1200 });
      return;
    }

    try {
      const result = await this.projectsService.attachTag(project.id, tagId);
      if (!result.attached) {
        this.snackBar.open('Tag is already attached', undefined, { duration: 1200 });
        return;
      }

      this.selectedTagByProjectId.delete(project.id);
      this.snackBar.open('Tag attached', undefined, { duration: 1200 });
    } catch (error) {
      const message = error instanceof Error && error.message ? error.message : 'Tag attach failed';
      this.snackBar.open(message, 'Close', { duration: 1800 });
    }
  }

  async detachTag(project: ProjectItem, tagId: string): Promise<void> {
    try {
      const result = await this.projectsService.detachTag(project.id, tagId);
      if (!result.detached) {
        this.snackBar.open('Tag was already detached', undefined, { duration: 1200 });
        return;
      }

      this.snackBar.open('Tag detached', undefined, { duration: 1200 });
    } catch (error) {
      const message = error instanceof Error && error.message ? error.message : 'Tag detach failed';
      this.snackBar.open(message, 'Close', { duration: 1800 });
    }
  }
}
