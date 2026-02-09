import { NgFor } from '@angular/common';
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';

@Component({
  selector: 'app-scan-manage-roots-dialog',
  imports: [NgFor, MatButtonModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatListModule],
  templateUrl: './scan-manage-roots-dialog.component.html',
  styleUrl: './scan-manage-roots-dialog.component.scss'
})
export class ScanManageRootsDialogComponent {
  readonly roots = ['D:\\code', 'C:\\src', 'E:\\backup'];
}
