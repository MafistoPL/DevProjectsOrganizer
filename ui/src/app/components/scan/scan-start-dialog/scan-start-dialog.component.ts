import { AsyncPipe, NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';
import { Observable, firstValueFrom } from 'rxjs';
import { RootsService, type RootItem } from '../../../services/roots.service';
import { ScanService } from '../../../services/scan.service';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-scan-start-dialog',
  imports: [
    AsyncPipe,
    FormsModule,
    NgFor,
    NgIf,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatSelectModule
  ],
  templateUrl: './scan-start-dialog.component.html',
  styleUrl: './scan-start-dialog.component.scss'
})
export class ScanStartDialogComponent {
  readonly roots$: Observable<RootItem[]>;
  mode: 'roots' | 'whole' | 'changed' = 'roots';
  selectedRootId: string | null = null;
  depthLimit = 4;

  constructor(
    private readonly rootsService: RootsService,
    private readonly scanService: ScanService,
    private readonly dialogRef: MatDialogRef<ScanStartDialogComponent>,
    private readonly dialog: MatDialog
  ) {
    this.roots$ = this.rootsService.roots$;
    void this.rootsService.load();
  }

  requiresRoot(): boolean {
    return this.mode !== 'whole';
  }

  filteredRoots(roots: RootItem[]): RootItem[] {
    if (this.mode === 'changed') {
      return roots.filter((root) => root.status === 'changed');
    }
    return roots;
  }

  onModeChange(): void {
    if (!this.requiresRoot()) {
      this.selectedRootId = null;
      return;
    }

    if (this.selectedRootId) {
      return;
    }
  }

  canStart(roots: RootItem[]): boolean {
    if (!this.requiresRoot()) {
      return true;
    }
    return Boolean(this.selectedRootId);
  }

  async startScan(): Promise<void> {
    if (this.mode === 'whole') {
      const confirmed = await this.confirmWholeScan();
      if (!confirmed) {
        return;
      }
    }

    if (this.requiresRoot() && !this.selectedRootId) {
      return;
    }

    const rootId = this.requiresRoot() ? this.selectedRootId : null;
    const depthLimit = this.mode === 'whole' ? null : this.depthLimit;
    await this.scanService.startScan({
      mode: this.mode,
      rootId,
      depthLimit
    });
    this.dialogRef.close();
  }

  private async confirmWholeScan(): Promise<boolean> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title: 'Whole computer scan',
        message:
          'This can take a long time and will pause other scans. Do you want to continue?',
        confirmText: 'Start',
        cancelText: 'Cancel',
        confirmColor: 'primary'
      }
    });
    const result = await firstValueFrom(ref.afterClosed());
    return Boolean(result);
  }
}
