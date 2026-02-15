import { NgFor } from '@angular/common';
import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { filter } from 'rxjs/operators';
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
  private readonly router = inject(Router);

  @ViewChild('contentInner', { read: ElementRef }) private contentInner?: ElementRef<HTMLElement>;

  readonly navItems = [
    { label: 'Scan', route: '/scan' },
    { label: 'Project Organizer', route: '/organizer' },
    { label: 'Suggestions', route: '/suggestions' },
    { label: 'Tags', route: '/tags' },
    { label: 'Recent', route: '/recent' }
  ];

  constructor() {
    this.router.events
      .pipe(
        filter((event) => event instanceof NavigationEnd),
        takeUntilDestroyed()
      )
      .subscribe(() => {
        this.resetContentScroll(4);
      });
  }

  onNavTabClick(): void {
    this.resetContentScroll(4);
  }

  private resetContentScroll(remainingRetries: number): void {
    window.setTimeout(() => {
      const container =
        this.contentInner?.nativeElement ??
        (document.querySelector('mat-tab-nav-panel.content-inner') as HTMLElement | null);
      if (!container) {
        if (remainingRetries > 0) {
          this.resetContentScroll(remainingRetries - 1);
        }
        return;
      }

      container.scrollTo({
        top: 0,
        behavior: 'auto'
      });

      if (remainingRetries > 0 && container.scrollTop > 0) {
        this.resetContentScroll(remainingRetries - 1);
      }
    }, 0);
  }
}
