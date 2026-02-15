import { AsyncPipe, DecimalPipe, NgFor, NgIf } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ScanService, type ScanSessionView } from '../../../services/scan.service';
import { TagHeuristicsRunsService, type TagHeuristicsRun } from '../../../services/tag-heuristics-runs.service';
import { Observable, firstValueFrom } from 'rxjs';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-scan-status',
  imports: [
    AsyncPipe,
    DecimalPipe,
    NgFor,
    NgIf,
    MatButtonModule,
    MatCardModule,
    MatProgressBarModule
  ],
  templateUrl: './scan-status.component.html',
  styleUrl: './scan-status.component.scss'
})
export class ScanStatusComponent {
  private static readonly terminalStates = new Set(['Completed', 'Failed', 'Stopped']);
  @Input() selectedScanId: string | null = null;
  @Output() readonly selectedScanIdChange = new EventEmitter<string | null>();

  readonly scans$: Observable<ScanSessionView[]>;
  readonly tagHeuristicsRuns$: Observable<TagHeuristicsRun[]>;

  constructor(
    private readonly scanService: ScanService,
    private readonly tagHeuristicsRunsService: TagHeuristicsRunsService,
    private readonly dialog: MatDialog
  ) {
    this.scans$ = this.scanService.scans$;
    this.tagHeuristicsRuns$ = this.tagHeuristicsRunsService.runs$;
  }

  isIndeterminate(scan: ScanSessionView): boolean {
    return scan.state === 'Queued' || scan.state === 'Counting' || !scan.totalFiles;
  }

  async pause(scan: ScanSessionView): Promise<void> {
    await this.scanService.pause(scan.id);
  }

  async resume(scan: ScanSessionView): Promise<void> {
    await this.scanService.resume(scan.id);
  }

  async stop(scan: ScanSessionView): Promise<void> {
    await this.scanService.stop(scan.id);
  }

  canStop(scan: ScanSessionView): boolean {
    return !ScanStatusComponent.terminalStates.has(scan.state);
  }

  isSelected(scan: ScanSessionView): boolean {
    return this.selectedScanId === scan.id;
  }

  selectScan(scan: ScanSessionView): void {
    const nextId = this.selectedScanId === scan.id ? null : scan.id;
    this.selectedScanIdChange.emit(nextId);
  }

  async clearScan(scan: ScanSessionView): Promise<void> {
    if (scan.state !== 'Completed') {
      return;
    }

    const confirmed = await this.confirmClear(
      'Clear completed scan',
      `Remove completed scan item for "${scan.rootPath}" from this view?`
    );
    if (!confirmed) {
      return;
    }

    this.scanService.clearCompleted(scan.id);
  }

  async clearTagHeuristicsRun(run: TagHeuristicsRun): Promise<void> {
    if (run.state !== 'Completed') {
      return;
    }

    const confirmed = await this.confirmClear(
      'Clear completed tag heuristics run',
      `Remove completed tag heuristics run for "${run.projectName}" from this view?`
    );
    if (!confirmed) {
      return;
    }

    this.tagHeuristicsRunsService.clearCompleted(run.runId);
  }

  private async confirmClear(title: string, message: string): Promise<boolean> {
    const ref = this.dialog.open(ConfirmDialogComponent, {
      width: '420px',
      data: {
        title,
        message,
        confirmText: 'Clear',
        cancelText: 'Cancel',
        confirmColor: 'warn'
      }
    });

    const result = await firstValueFrom(ref.afterClosed());
    return result === true;
  }
}
