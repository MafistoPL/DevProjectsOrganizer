import { DatePipe, NgFor, NgIf } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { TagsService, type TagItem } from '../../services/tags.service';

@Component({
  selector: 'app-tags-page',
  imports: [
    DatePipe,
    NgFor,
    NgIf,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSnackBarModule
  ],
  templateUrl: './tags-page.component.html',
  styleUrl: './tags-page.component.scss'
})
export class TagsPageComponent {
  private readonly tagsService = inject(TagsService);
  private readonly snackBar = inject(MatSnackBar);

  readonly tags = toSignal(this.tagsService.tags$, { initialValue: [] });
  newTagName = '';
  editTagId: string | null = null;
  editTagName = '';

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

  async deleteTag(tagId: string): Promise<void> {
    try {
      await this.tagsService.deleteTag(tagId);
      this.cancelEdit();
      this.snackBar.open('Tag deleted', undefined, { duration: 1200 });
    } catch (error) {
      this.snackBar.open(this.getErrorMessage(error), 'Close', { duration: 1600 });
    }
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof Error && error.message) {
      return error.message;
    }

    return 'Operation failed';
  }
}
