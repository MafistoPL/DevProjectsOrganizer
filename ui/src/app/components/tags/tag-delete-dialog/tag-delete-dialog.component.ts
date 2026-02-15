import { NgIf } from '@angular/common';
import { Component, Inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

export type TagDeleteDialogData = {
  tagName: string;
};

@Component({
  selector: 'app-tag-delete-dialog',
  imports: [FormsModule, NgIf, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule],
  templateUrl: './tag-delete-dialog.component.html',
  styleUrl: './tag-delete-dialog.component.scss'
})
export class TagDeleteDialogComponent {
  typedName = '';

  constructor(
    private readonly dialogRef: MatDialogRef<TagDeleteDialogComponent>,
    @Inject(MAT_DIALOG_DATA) readonly data: TagDeleteDialogData
  ) {}

  get canConfirm(): boolean {
    return this.typedName === this.data.tagName;
  }

  cancel(): void {
    this.dialogRef.close(false);
  }

  confirm(): void {
    if (!this.canConfirm) {
      return;
    }

    this.dialogRef.close(true);
  }
}
