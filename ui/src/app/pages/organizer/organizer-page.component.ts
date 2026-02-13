import { DatePipe, NgFor, NgIf } from '@angular/common';
import { Component, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatCardModule } from '@angular/material/card';
import { ProjectsService } from '../../services/projects.service';

@Component({
  selector: 'app-organizer-page',
  imports: [DatePipe, NgFor, NgIf, MatCardModule],
  templateUrl: './organizer-page.component.html',
  styleUrl: './organizer-page.component.scss'
})
export class OrganizerPageComponent {
  private readonly projectsService = inject(ProjectsService);
  readonly projects = toSignal(this.projectsService.projects$, { initialValue: [] });
}
