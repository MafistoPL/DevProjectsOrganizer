import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-scan-page',
  imports: [MatButtonModule, MatCardModule],
  templateUrl: './scan-page.component.html',
  styleUrl: './scan-page.component.scss'
})
export class ScanPageComponent {}
