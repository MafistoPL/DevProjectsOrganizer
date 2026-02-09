import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-organizer-page',
  imports: [MatButtonModule, MatCardModule],
  templateUrl: './organizer-page.component.html',
  styleUrl: './organizer-page.component.scss'
})
export class OrganizerPageComponent {}
