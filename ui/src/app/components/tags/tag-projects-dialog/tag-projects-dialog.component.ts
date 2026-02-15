import { DatePipe, NgFor, NgIf } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { TagLinkedProject } from '../../../services/tags.service';

export type TagProjectsDialogData = {
  tagName: string;
  projects: TagLinkedProject[];
};

@Component({
  selector: 'app-tag-projects-dialog',
  imports: [DatePipe, NgFor, NgIf, MatButtonModule, MatDialogModule],
  templateUrl: './tag-projects-dialog.component.html',
  styleUrl: './tag-projects-dialog.component.scss'
})
export class TagProjectsDialogComponent {
  constructor(@Inject(MAT_DIALOG_DATA) readonly data: TagProjectsDialogData) {}
}
