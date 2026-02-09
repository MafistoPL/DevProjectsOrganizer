import { Routes } from '@angular/router';
import { OrganizerPageComponent } from './pages/organizer/organizer-page.component';
import { RecentPageComponent } from './pages/recent/recent-page.component';
import { ScanPageComponent } from './pages/scan/scan-page.component';
import { SuggestionsPageComponent } from './pages/suggestions/suggestions-page.component';
import { TagsPageComponent } from './pages/tags/tags-page.component';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'scan' },
  { path: 'scan', component: ScanPageComponent, title: 'Scan' },
  { path: 'organizer', component: OrganizerPageComponent, title: 'Project Organizer' },
  { path: 'suggestions', component: SuggestionsPageComponent, title: 'Suggestions' },
  { path: 'tags', component: TagsPageComponent, title: 'Tags' },
  { path: 'recent', component: RecentPageComponent, title: 'Recent' },
  { path: '**', redirectTo: 'scan' }
];
