import { NgFor, NgIf } from '@angular/common';
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { ProjectSuggestionListComponent } from '../../components/suggestions/project-suggestion-list/project-suggestion-list.component';
import { TagSuggestionListComponent } from '../../components/suggestions/tag-suggestion-list/tag-suggestion-list.component';
import {
  SuggestionsRegressionReport,
  SuggestionsService
} from '../../services/suggestions.service';

@Component({
  selector: 'app-suggestions-page',
  imports: [
    NgFor,
    NgIf,
    MatButtonModule,
    MatCardModule,
    MatSnackBarModule,
    ProjectSuggestionListComponent,
    TagSuggestionListComponent
  ],
  templateUrl: './suggestions-page.component.html',
  styleUrl: './suggestions-page.component.scss'
})
export class SuggestionsPageComponent {
  regressionReport: SuggestionsRegressionReport | null = null;
  regressionError: string | null = null;
  isRegressionBusy = false;

  constructor(
    private readonly suggestionsService: SuggestionsService,
    private readonly snackBar: MatSnackBar
  ) {}

  async runRegressionReport(): Promise<void> {
    this.isRegressionBusy = true;
    this.regressionError = null;
    try {
      this.regressionReport = await this.suggestionsService.runRegressionReport();
      this.snackBar.open('Regression report ready', undefined, { duration: 1400 });
    } catch (error) {
      this.regressionReport = null;
      this.regressionError = this.getErrorMessage(error);
      this.snackBar.open('Regression report failed', 'Close', { duration: 1800 });
    } finally {
      this.isRegressionBusy = false;
    }
  }

  async exportRegressionReport(): Promise<void> {
    this.isRegressionBusy = true;
    this.regressionError = null;
    try {
      const result = await this.suggestionsService.exportRegressionReport();
      this.snackBar.open(`Exported: ${result.path}`, undefined, { duration: 1800 });
    } catch (error) {
      this.regressionError = this.getErrorMessage(error);
      this.snackBar.open('Regression export failed', 'Close', { duration: 1800 });
    } finally {
      this.isRegressionBusy = false;
    }
  }

  private getErrorMessage(error: unknown): string {
    if (error instanceof Error && error.message) {
      return error.message;
    }
    return 'Unknown error';
  }
}
