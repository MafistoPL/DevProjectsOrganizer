import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

@Component({
  selector: 'app-suggestions-page',
  imports: [MatButtonModule, MatCardModule],
  templateUrl: './suggestions-page.component.html',
  styleUrl: './suggestions-page.component.scss'
})
export class SuggestionsPageComponent {}
