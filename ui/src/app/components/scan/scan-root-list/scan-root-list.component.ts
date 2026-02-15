import { DatePipe, DecimalPipe, NgFor, NgIf } from '@angular/common';
import { Component, Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RootsService, type RootItem } from '../../../services/roots.service';

@Component({
  selector: 'app-scan-root-list',
  imports: [DatePipe, DecimalPipe, NgFor, NgIf, MatCardModule, MatCheckboxModule, MatTooltipModule],
  templateUrl: './scan-root-list.component.html',
  styleUrl: './scan-root-list.component.scss'
})
export class ScanRootListComponent {
  readonly roots: Signal<RootItem[]>;
  readonly selectedRootIds: Signal<string[]>;

  constructor(private readonly rootsService: RootsService) {
    this.roots = toSignal(this.rootsService.roots$, { initialValue: [] as RootItem[] });
    this.selectedRootIds = toSignal(this.rootsService.selectedRootIds$, {
      initialValue: [] as string[]
    });
  }

  isSelected(rootId: string): boolean {
    return this.selectedRootIds().includes(rootId);
  }

  toggleSelected(rootId: string, checked: boolean): void {
    this.rootsService.setRootSelected(rootId, checked);
  }
}
