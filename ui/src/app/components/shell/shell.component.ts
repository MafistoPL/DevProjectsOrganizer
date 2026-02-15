import { NgFor } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { TopbarComponent } from '../topbar/topbar.component';
import { TagHeuristicsRunsService } from '../../services/tag-heuristics-runs.service';

@Component({
  selector: 'app-shell',
  imports: [NgFor, RouterLink, RouterLinkActive, TopbarComponent, RouterOutlet, MatTabsModule],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  private readonly _tagHeuristicsRunsService = inject(TagHeuristicsRunsService);

  readonly navItems = [
    { label: 'Scan', route: '/scan' },
    { label: 'Project Organizer', route: '/organizer' },
    { label: 'Suggestions', route: '/suggestions' },
    { label: 'Tags', route: '/tags' },
    { label: 'Recent', route: '/recent' }
  ];
}
