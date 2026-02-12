import { AsyncPipe, DatePipe, DecimalPipe, NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { RootsService, type RootItem } from '../../../services/roots.service';
import { Observable } from 'rxjs';

@Component({
  selector: 'app-scan-root-list',
  imports: [AsyncPipe, DatePipe, DecimalPipe, NgFor, NgIf, MatCardModule, MatListModule],
  templateUrl: './scan-root-list.component.html',
  styleUrl: './scan-root-list.component.scss'
})
export class ScanRootListComponent {
  readonly roots$: Observable<RootItem[]>;

  constructor(private readonly rootsService: RootsService) {
    this.roots$ = this.rootsService.roots$;
  }
}
