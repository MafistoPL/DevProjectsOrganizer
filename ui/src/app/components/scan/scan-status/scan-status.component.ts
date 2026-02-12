import { AsyncPipe, DecimalPipe, NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ScanService, type ScanSessionView } from '../../../services/scan.service';
import { Observable } from 'rxjs';

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
  readonly scans$: Observable<ScanSessionView[]>;

  constructor(private readonly scanService: ScanService) {
    this.scans$ = this.scanService.scans$;
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
}
