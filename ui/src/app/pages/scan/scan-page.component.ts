import { Component, computed, Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ScanManageRootsDialogComponent } from '../../components/scan/scan-manage-roots-dialog/scan-manage-roots-dialog.component';
import { ScanResultsComponent } from '../../components/scan/scan-results/scan-results.component';
import { ScanRootListComponent } from '../../components/scan/scan-root-list/scan-root-list.component';
import { ScanStartDialogComponent } from '../../components/scan/scan-start-dialog/scan-start-dialog.component';
import { ScanStatusComponent } from '../../components/scan/scan-status/scan-status.component';
import { RootsService } from '../../services/roots.service';
import { ScanService } from '../../services/scan.service';

@Component({
  selector: 'app-scan-page',
  imports: [
    MatButtonModule,
    MatDialogModule,
    MatSnackBarModule,
    ScanRootListComponent,
    ScanStatusComponent,
    ScanResultsComponent
  ],
  templateUrl: './scan-page.component.html',
  styleUrl: './scan-page.component.scss'
})
export class ScanPageComponent {
  private readonly selectedRootIds: Signal<string[]>;
  readonly selectedRootCount: Signal<number>;

  constructor(
    private readonly dialog: MatDialog,
    private readonly rootsService: RootsService,
    private readonly scanService: ScanService,
    private readonly snackBar: MatSnackBar
  ) {
    this.selectedRootIds = toSignal(this.rootsService.selectedRootIds$, {
      initialValue: [] as string[]
    });
    this.selectedRootCount = computed(() => this.selectedRootIds().length);
  }

  openStartDialog(): void {
    this.dialog.open(ScanStartDialogComponent, {
      width: '520px'
    });
  }

  openManageRootsDialog(): void {
    this.dialog.open(ScanManageRootsDialogComponent, {
      width: '560px',
      data: {
        roots$: this.rootsService.roots$,
        rootsService: this.rootsService
      }
    });
  }

  async rescanSelectedRoots(): Promise<void> {
    const selectedRoots = this.rootsService.getSelectedRootsSnapshot();
    if (selectedRoots.length === 0) {
      this.snackBar.open('Select at least one root to rescan.', undefined, { duration: 1500 });
      return;
    }

    let queued = 0;
    for (const root of selectedRoots) {
      try {
        await this.scanService.startScan({
          mode: 'roots',
          rootId: root.id
        });
        queued += 1;
      } catch (error) {
        const message = error instanceof Error && error.message ? error.message : 'Rescan request failed';
        this.snackBar.open(`${message}: ${root.path}`, 'Close', { duration: 2500 });
      }
    }

    if (queued > 0) {
      this.snackBar.open(`Queued rescans for ${queued} root(s).`, undefined, { duration: 1700 });
    }
  }
}
