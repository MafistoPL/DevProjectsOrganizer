import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { OrganizerPageComponent } from './organizer-page.component';
import { ProjectsService } from '../../services/projects.service';

describe('OrganizerPageComponent', () => {
  let fixture: ComponentFixture<OrganizerPageComponent>;

  beforeEach(async () => {
    const projectsServiceMock = {
      projects$: new BehaviorSubject([
        {
          id: 'proj-1',
          sourceSuggestionId: 's1',
          lastScanSessionId: 'scan-1',
          rootPath: 'D:\\code',
          name: 'dotnet-api',
          score: 0.88,
          kind: 'ProjectRoot',
          path: 'D:\\code\\dotnet-api',
          reason: 'markers: .sln',
          extensionsSummary: 'cs=10',
          markers: ['.sln'],
          techHints: ['csharp'],
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ])
    };

    await TestBed.configureTestingModule({
      imports: [OrganizerPageComponent],
      providers: [{ provide: ProjectsService, useValue: projectsServiceMock }]
    }).compileComponents();

    fixture = TestBed.createComponent(OrganizerPageComponent);
    fixture.detectChanges();
  });

  it('renders accepted projects from service', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('dotnet-api');
    expect(text).toContain('D:\\code\\dotnet-api');
  });
});
