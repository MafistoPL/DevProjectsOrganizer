import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { ProjectSuggestionListComponent } from '../../components/suggestions/project-suggestion-list/project-suggestion-list.component';
import { TagSuggestionListComponent } from '../../components/suggestions/tag-suggestion-list/tag-suggestion-list.component';

@Component({
  selector: 'app-suggestions-page',
  imports: [
    MatButtonModule,
    MatCardModule,
    ProjectSuggestionListComponent,
    TagSuggestionListComponent
  ],
  templateUrl: './suggestions-page.component.html',
  styleUrl: './suggestions-page.component.scss'
})
export class SuggestionsPageComponent {}
