import { NgFor } from '@angular/common';
import { Component } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';

@Component({
  selector: 'app-scan-root-list',
  imports: [NgFor, MatCardModule, MatListModule],
  templateUrl: './scan-root-list.component.html',
  styleUrl: './scan-root-list.component.scss'
})
export class ScanRootListComponent {
  readonly roots = [
    { path: 'D:\\code', status: 'scanned' },
    { path: 'C:\\src', status: 'changed' },
    { path: 'E:\\backup', status: 'scanning' }
  ];
}
