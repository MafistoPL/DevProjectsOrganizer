import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { OrganizerPageComponent } from './organizer-page.component';
import { ProjectsService } from '../../services/projects.service';

describe('OrganizerPageComponent', () => {
  let fixture: ComponentFixture<OrganizerPageComponent>;
  let deleteProjectSpy: ReturnType<typeof vi.fn>;

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
      ]),
      deleteProject: vi.fn().mockResolvedValue({ id: 'proj-1', deleted: true })
    };
    deleteProjectSpy = projectsServiceMock.deleteProject;

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

  it('delete action deletes project after typed-name confirmation', async () => {
    const dialogOpenSpy = vi.fn().mockReturnValue({
      afterClosed: () => of('dotnet-api')
    });
    (fixture.componentInstance as any).dialog = { open: dialogOpenSpy };
    (fixture.componentInstance as any).snackBar = { open: vi.fn() };

    const project = fixture.componentInstance.projects()[0];
    await fixture.componentInstance.deleteProject(project);

    expect(dialogOpenSpy).toHaveBeenCalled();
    expect(deleteProjectSpy).toHaveBeenCalledWith('proj-1', 'dotnet-api');
  });
});
