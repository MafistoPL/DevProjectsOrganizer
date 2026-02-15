import { NgIf } from '@angular/common';
import { Component, Input } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { ProjectSuggestionListComponent } from '../../suggestions/project-suggestion-list/project-suggestion-list.component';

@Component({
  selector: 'app-scan-results',
  imports: [NgIf, MatCardModule, ProjectSuggestionListComponent],
  templateUrl: './scan-results.component.html',
  styleUrl: './scan-results.component.scss'
})
export class ScanResultsComponent {
  @Input() selectedScanId: string | null = null;
}
