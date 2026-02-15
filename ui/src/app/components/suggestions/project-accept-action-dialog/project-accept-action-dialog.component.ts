import { Component, Inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export type ProjectAcceptAction = 'heuristics' | 'ai' | 'skip';

export type ProjectAcceptActionDialogData = {
  projectName: string;
};

export type ProjectAcceptDialogResult = {
  action: ProjectAcceptAction;
  projectName: string;
};

@Component({
  selector: 'app-project-accept-action-dialog',
  imports: [MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './project-accept-action-dialog.component.html',
  styleUrl: './project-accept-action-dialog.component.scss'
})
export class ProjectAcceptActionDialogComponent {
  projectName: string;

  constructor(
    private readonly dialogRef: MatDialogRef<ProjectAcceptActionDialogComponent, ProjectAcceptDialogResult>,
    @Inject(MAT_DIALOG_DATA) readonly data: ProjectAcceptActionDialogData
  ) {
    this.projectName = data.projectName;
  }

  get normalizedProjectName(): string {
    return this.projectName.trim();
  }

  choose(action: ProjectAcceptAction): void {
    const projectName = this.normalizedProjectName;
    if (!projectName) {
      return;
    }

    this.dialogRef.close({ action, projectName });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
