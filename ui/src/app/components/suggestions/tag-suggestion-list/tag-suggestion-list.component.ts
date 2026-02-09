import { NgFor } from '@angular/common';
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatExpansionModule } from '@angular/material/expansion';

type TagSuggestionItem = {
  id: string;
  project: string;
  tag: string;
  action: 'add' | 'remove' | 'replace';
  confidence: number;
  reason: string;
  source: 'heuristic' | 'ai' | 'user';
  status: 'pending' | 'accepted' | 'rejected';
};

@Component({
  selector: 'app-tag-suggestion-list',
  imports: [NgFor, MatButtonModule, MatButtonToggleModule, MatExpansionModule],
  templateUrl: './tag-suggestion-list.component.html',
  styleUrl: './tag-suggestion-list.component.scss'
})
export class TagSuggestionListComponent {
  layout: 'list' | 'grid' = 'list';
  readonly items: TagSuggestionItem[] = [
    {
      id: 't1',
      project: 'dotnet-api',
      tag: 'csharp',
      action: 'add',
      confidence: 0.86,
      reason: 'csproj marker + cs histogram',
      source: 'heuristic',
      status: 'pending'
    },
    {
      id: 't2',
      project: 'notes-parser',
      tag: 'typescript',
      action: 'add',
      confidence: 0.74,
      reason: 'tsconfig + ts histogram',
      source: 'heuristic',
      status: 'pending'
    },
    {
      id: 't3',
      project: 'course-js-2023',
      tag: 'course',
      action: 'add',
      confidence: 0.58,
      reason: 'folder name contains course',
      source: 'heuristic',
      status: 'pending'
    },
    {
      id: 't4',
      project: 'rust-playground',
      tag: 'archive',
      action: 'add',
      confidence: 0.42,
      reason: 'last modified > 2y',
      source: 'ai',
      status: 'pending'
    }
  ];
}
