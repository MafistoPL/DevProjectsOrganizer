import { AsyncPipe, DatePipe, DecimalPipe, NgFor, NgIf } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ScanService, type ScanSessionView } from '../../../services/scan.service';
import { TagHeuristicsRunsService, type TagHeuristicsRun } from '../../../services/tag-heuristics-runs.service';
import { BehaviorSubject, Observable, combineLatest, firstValueFrom, map } from 'rxjs';
import { ConfirmDialogComponent } from '../../shared/confirm-dialog/confirm-dialog.component';

type ScanScope = 'active' | 'archived';
type SortDirection = 'desc' | 'asc';

@Component({
  selector: 'app-scan-status',
  imports: [
    AsyncPipe,
    DatePipe,
    DecimalPipe,
    NgFor,
    NgIf,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressBarModule
  ],
  templateUrl: './scan-status.component.html',
  styleUrl: './scan-status.component.scss'
})
export class ScanStatusComponent {
  private static readonly terminalStates = new Set(['Completed', 'Archived', 'Failed', 'Stopped']);
  private readonly startupTimestamp: number;
  private readonly scopeSubject = new BehaviorSubject<ScanScope>('active');
  private readonly pathQuerySubject = new BehaviorSubject<string>('');
  private readonly sortDirectionSubject = new BehaviorSubject<SortDirection>('desc');

  @Input() selectedScanId: string | null = null;
  @Output() readonly selectedScanIdChange = new EventEmitter<string | null>();

  readonly scans$: Observable<ScanSessionView[]>;
  readonly filteredScans$: Observable<ScanSessionView[]>;
  readonly tagHeuristicsRuns$: Observable<TagHeuristicsRun[]>;

  scanScope: ScanScope = 'active';
  pathQuery = '';
  sortDirection: SortDirection = 'desc';

  constructor(
    private readonly scanService: ScanService,
    private readonly tagHeuristicsRunsService: TagHeuristicsRunsService,
    private readonly dialog: MatDialog
  ) {
    this.startupTimestamp = this.scanService.getSessionStartedAt();
    this.scans$ = this.scanService.scans$;
    this.filteredScans$ = combineLatest([
      this.scans$,
      this.scopeSubject,
      this.pathQuerySubject,
      this.sortDirectionSubject
    ]).pipe(
      map(([scans, scope, pathQuery, sortDirection]) =>
        this.filterAndSortScans(scans, scope, pathQuery, sortDirection)
      )
    );
    this.tagHeuristicsRuns$ = this.tagHeuristicsRunsService.runs$;
  }

  setScope(scope: ScanScope | null): void {
    if (scope !== 'active' && scope !== 'archived') {
      return;
    }

    if (this.scanScope === scope) {
      return;
    }

    this.scanScope = scope;
    this.scopeSubject.next(scope);
  }

  updatePathQuery(value: string): void {
    this.pathQuery = value;
    this.pathQuerySubject.next(value);
  }

  toggleSortDirection(): void {
    this.sortDirection = this.sortDirection === 'desc' ? 'asc' : 'desc';
    this.sortDirectionSubject.next(this.sortDirection);
  }

  getEmptyMessage(): string {
    if (this.scanScope === 'archived') {
      return 'No archived scans yet.';
    }

    return 'No active scans yet.';
  }

  getSortDirectionLabel(): string {
    return this.sortDirection === 'desc' ? 'Newest first' : 'Oldest first';
  }

  getDisplayTimestamp(scan: ScanSessionView): string {
    return scan.finishedAt ?? scan.startedAt ?? scan.createdAt;
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

  private filterAndSortScans(
    scans: ScanSessionView[],
    scope: ScanScope,
    pathQuery: string,
    sortDirection: SortDirection
  ): ScanSessionView[] {
    const normalizedQuery = pathQuery.trim().toLowerCase();
    const filteredByScope = scans.filter((scan) => {
      if (scope === 'archived') {
        return scan.state === 'Archived';
      }

      return scan.state !== 'Archived' && this.toTimestamp(scan.createdAt) >= this.startupTimestamp;
    });

    const filteredByPath =
      normalizedQuery.length === 0
        ? filteredByScope
        : filteredByScope.filter((scan) => {
            const rootPath = scan.rootPath.toLowerCase();
            const currentPath = (scan.currentPath ?? '').toLowerCase();
            return rootPath.includes(normalizedQuery) || currentPath.includes(normalizedQuery);
          });

    return [...filteredByPath].sort((left, right) => {
      const leftTimestamp = this.toTimestamp(this.getDisplayTimestamp(left));
      const rightTimestamp = this.toTimestamp(this.getDisplayTimestamp(right));
      if (sortDirection === 'desc') {
        return rightTimestamp - leftTimestamp;
      }

      return leftTimestamp - rightTimestamp;
    });
  }

  private toTimestamp(rawDate: string): number {
    const parsed = Date.parse(rawDate);
    if (Number.isNaN(parsed)) {
      return 0;
    }

    return parsed;
  }
}
