import { NgFor } from '@angular/common';
import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { TopbarComponent } from '../topbar/topbar.component';

@Component({
  selector: 'app-shell',
  imports: [NgFor, RouterLink, RouterLinkActive, TopbarComponent, RouterOutlet, MatTabsModule],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent {
  readonly navItems = [
    { label: 'Scan', route: '/scan' },
    { label: 'Project Organizer', route: '/organizer' },
    { label: 'Suggestions', route: '/suggestions' },
    { label: 'Tags', route: '/tags' },
    { label: 'Recent', route: '/recent' }
  ];
}
