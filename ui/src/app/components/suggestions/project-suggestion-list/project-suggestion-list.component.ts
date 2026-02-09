import { NgFor } from '@angular/common';
import { Component } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

type ProjectSuggestionItem = {
  id: string;
  name: string;
  score: number;
  kind: string;
  path: string;
  reason: string;
  markers: string[];
  techHints: string[];
  extSummary: string;
  createdAt: string;
  status: 'pending' | 'accepted' | 'rejected';
};

@Component({
  selector: 'app-project-suggestion-list',
  imports: [
    NgFor,
    MatButtonModule,
    MatButtonToggleModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatTooltipModule
  ],
  templateUrl: './project-suggestion-list.component.html',
  styleUrl: './project-suggestion-list.component.scss'
})
export class ProjectSuggestionListComponent {
  layout: 'list' | 'grid' = 'list';
  openId: string | null = null;
  sortKey: 'name' | 'score' | 'createdAt' = 'name';
  sortDir: 'asc' | 'desc' = 'asc';
  searchTerm = '';

  readonly items: ProjectSuggestionItem[] = [
    {
      id: 's1',
      name: 'dotnet-api',
      score: 0.88,
      kind: 'ProjectRoot',
      path: 'D:\\code\\dotnet-api',
      reason: '.sln + csproj markers',
      markers: ['.sln', 'Api.csproj'],
      techHints: ['csharp', '.net'],
      extSummary: 'cs=142, json=18, md=3',
      createdAt: '2025-01-10',
      status: 'pending'
    },
    {
      id: 's2',
      name: 'c-labs',
      score: 0.73,
      kind: 'Collection',
      path: 'C:\\src\\c-labs',
      reason: 'ext histogram',
      markers: ['Makefile'],
      techHints: ['c', 'cpp'],
      extSummary: 'c=58, h=42, md=2',
      createdAt: '2024-11-02',
      status: 'pending'
    },
    {
      id: 's3',
      name: 'single-file-tool.ps1',
      score: 0.62,
      kind: 'SingleFileMiniProject',
      path: 'E:\\backup\\tools\\single-file-tool.ps1',
      reason: 'single file candidate',
      markers: ['*.ps1'],
      techHints: ['powershell'],
      extSummary: 'ps1=1',
      createdAt: '2023-06-15',
      status: 'pending'
    },
    {
      id: 's4',
      name: 'notes-parser',
      score: 0.57,
      kind: 'ProjectRoot',
      path: 'D:\\code\\notes-parser',
      reason: 'package.json',
      markers: ['package.json'],
      techHints: ['node', 'typescript'],
      extSummary: 'ts=24, json=6, md=1',
      createdAt: '2024-02-10',
      status: 'pending'
    },
    {
      id: 's5',
      name: 'kata-strings',
      score: 0.51,
      kind: 'Collection',
      path: 'C:\\src\\katas\\strings',
      reason: 'folder name',
      markers: [],
      techHints: ['practice'],
      extSummary: 'cs=12, txt=4',
      createdAt: '2022-09-30',
      status: 'pending'
    },
    {
      id: 's6',
      name: 'rust-playground',
      score: 0.76,
      kind: 'ProjectRoot',
      path: 'D:\\code\\rust-playground',
      reason: 'Cargo.toml',
      markers: ['Cargo.toml'],
      techHints: ['rust'],
      extSummary: 'rs=42, toml=1',
      createdAt: '2025-03-05',
      status: 'pending'
    },
    {
      id: 's7',
      name: 'go-http',
      score: 0.69,
      kind: 'ProjectRoot',
      path: 'D:\\code\\go-http',
      reason: 'go.mod',
      markers: ['go.mod'],
      techHints: ['go'],
      extSummary: 'go=38, md=2',
      createdAt: '2024-07-20',
      status: 'pending'
    },
    {
      id: 's8',
      name: 'cpp-cmake-tools',
      score: 0.71,
      kind: 'ProjectRoot',
      path: 'E:\\backup\\cpp-cmake-tools',
      reason: 'CMakeLists.txt',
      markers: ['CMakeLists.txt'],
      techHints: ['cpp'],
      extSummary: 'cpp=56, h=21',
      createdAt: '2023-12-01',
      status: 'pending'
    },
    {
      id: 's9',
      name: 'python-utils',
      score: 0.55,
      kind: 'Collection',
      path: 'C:\\src\\python-utils',
      reason: 'ext histogram',
      markers: [],
      techHints: ['python'],
      extSummary: 'py=17, md=1',
      createdAt: '2022-05-14',
      status: 'pending'
    },
    {
      id: 's10',
      name: 'course-js-2023',
      score: 0.48,
      kind: 'Collection',
      path: 'E:\\backup\\courses\\course-js-2023',
      reason: 'folder name',
      markers: [],
      techHints: ['course', 'javascript'],
      extSummary: 'js=44, html=8, css=6',
      createdAt: '2023-01-08',
      status: 'pending'
    }
  ];

  get visibleItems(): ProjectSuggestionItem[] {
    const term = this.searchTerm.trim().toLowerCase();
    const filtered = term
      ? this.items.filter((item) => item.name.toLowerCase().includes(term))
      : this.items;

    const sorted = [...filtered].sort((a, b) => {
      let result = 0;
      if (this.sortKey === 'name') {
        result = a.name.localeCompare(b.name);
      } else if (this.sortKey === 'score') {
        result = a.score - b.score;
      } else {
        result = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
      }
      return this.sortDir === 'asc' ? result : -result;
    });

    return sorted;
  }

  toggleDetails(id: string): void {
    this.openId = this.openId === id ? null : id;
  }

  isOverflowing(el: HTMLElement | null): boolean {
    if (!el) {
      return false;
    }
    return el.scrollWidth > el.clientWidth;
  }
}
