import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { ScanManageRootsDialogComponent } from '../../components/scan/scan-manage-roots-dialog/scan-manage-roots-dialog.component';
import { ScanResultsComponent } from '../../components/scan/scan-results/scan-results.component';
import { ScanRootListComponent } from '../../components/scan/scan-root-list/scan-root-list.component';
import { ScanStartDialogComponent } from '../../components/scan/scan-start-dialog/scan-start-dialog.component';
import { ScanStatusComponent } from '../../components/scan/scan-status/scan-status.component';
import { RootsService } from '../../services/roots.service';

@Component({
  selector: 'app-scan-page',
  imports: [
    MatButtonModule,
    MatDialogModule,
    ScanRootListComponent,
    ScanStatusComponent,
    ScanResultsComponent
  ],
  templateUrl: './scan-page.component.html',
  styleUrl: './scan-page.component.scss'
})
export class ScanPageComponent {
  constructor(
    private readonly dialog: MatDialog,
    private readonly rootsService: RootsService
  ) {}

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
}
