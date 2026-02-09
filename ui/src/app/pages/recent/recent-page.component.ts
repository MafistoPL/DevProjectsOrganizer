import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-recent-page',
  imports: [MatButtonModule, MatCardModule],
  templateUrl: './recent-page.component.html',
  styleUrl: './recent-page.component.scss'
})
export class RecentPageComponent {}
