import { NgIf } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export type ProjectDeleteDialogData = {
  projectName: string;
};

@Component({
  selector: 'app-project-delete-dialog',
  imports: [
    FormsModule,
    NgIf,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './project-delete-dialog.component.html',
  styleUrl: './project-delete-dialog.component.scss'
})
export class ProjectDeleteDialogComponent {
  typedName = '';

  constructor(
    private readonly dialogRef: MatDialogRef<ProjectDeleteDialogComponent>,
    @Inject(MAT_DIALOG_DATA) readonly data: ProjectDeleteDialogData
  ) {}

  get canConfirm(): boolean {
    return this.typedName === this.data.projectName;
  }

  cancel(): void {
    this.dialogRef.close(null);
  }

  confirm(): void {
    if (!this.canConfirm) {
      return;
    }

    this.dialogRef.close(this.typedName);
  }
}
