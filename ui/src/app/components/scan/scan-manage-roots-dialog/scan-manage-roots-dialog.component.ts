import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Component, Inject, Optional } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { firstValueFrom } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MAT_DIALOG_DATA } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { RootsService, type RootItem } from '../../../services/roots.service';
import { Observable } from 'rxjs';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

type ManageRootsDialogData = {
  roots$?: Observable<RootItem[]>;
  rootsService?: RootsService;
};

@Component({
  selector: 'app-scan-manage-roots-dialog',
  imports: [
    AsyncPipe,
    FormsModule,
    NgFor,
    NgIf,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule
  ],
  templateUrl: './scan-manage-roots-dialog.component.html',
  styleUrl: './scan-manage-roots-dialog.component.scss'
})
export class ScanManageRootsDialogComponent {
  private readonly service: RootsService;
  readonly roots$: Observable<RootItem[]>;
  newRoot = '';
  editingId: string | null = null;
  editingPath = '';

  constructor(
    rootsService: RootsService,
    private readonly dialog: MatDialog,
    @Optional() @Inject(MAT_DIALOG_DATA) data?: ManageRootsDialogData
  ) {
    this.service = data?.rootsService ?? rootsService;
    this.roots$ = data?.roots$ ?? this.service.roots$;
    void this.service.load();
  }

  async addRoot(): Promise<void> {
    const path = this.newRoot.trim();
    if (!path) {
      return;
    }

    await this.service.addRoot(path);
    this.newRoot = '';
  }

  startEdit(root: RootItem): void {
    this.editingId = root.id;
    this.editingPath = root.path;
  }

  cancelEdit(): void {
    this.editingId = null;
    this.editingPath = '';
  }

  async saveEdit(): Promise<void> {
    if (!this.editingId) {
      return;
    }
    const path = this.editingPath.trim();
    if (!path) {
      return;
    }
    await this.service.updateRoot(this.editingId, path);
    this.cancelEdit();
  }

  async removeRoot(root: RootItem): Promise<void> {
    const confirmed = await this.confirmRemove(root.path);
    if (!confirmed) {
      return;
    }
    await this.service.deleteRoot(root.id);
    if (this.editingId === root.id) {
      this.cancelEdit();
    }
  }

  private confirmRemove(path: string): Promise<boolean> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Remove root',
        message: `Are you sure you want to remove "${path}"?`,
        confirmText: 'Remove',
        cancelText: 'Cancel',
        confirmColor: 'warn'
      }
    });

    return firstValueFrom(ref.afterClosed()).then((value) => Boolean(value));
  }
}
