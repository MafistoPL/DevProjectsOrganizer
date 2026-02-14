import { Component, Inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

export type ProjectAcceptAction = 'heuristics' | 'ai' | 'skip';

export type ProjectAcceptActionDialogData = {
  projectName: string;
};

@Component({
  selector: 'app-project-accept-action-dialog',
  imports: [MatButtonModule, MatDialogModule],
  templateUrl: './project-accept-action-dialog.component.html',
  styleUrl: './project-accept-action-dialog.component.scss'
})
export class ProjectAcceptActionDialogComponent {
  constructor(
    private readonly dialogRef: MatDialogRef<ProjectAcceptActionDialogComponent, ProjectAcceptAction>,
    @Inject(MAT_DIALOG_DATA) readonly data: ProjectAcceptActionDialogData
  ) {}

  choose(action: ProjectAcceptAction): void {
    this.dialogRef.close(action);
  }
}
