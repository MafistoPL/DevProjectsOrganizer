import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { OrganizerPageComponent } from './organizer-page.component';
import { ProjectsService } from '../../services/projects.service';
import { TagsService } from '../../services/tags.service';

describe('OrganizerPageComponent', () => {
  let fixture: ComponentFixture<OrganizerPageComponent>;
  let deleteProjectSpy: ReturnType<typeof vi.fn>;
  let updateDescriptionSpy: ReturnType<typeof vi.fn>;
  let attachTagSpy: ReturnType<typeof vi.fn>;
  let detachTagSpy: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    const projectsServiceMock = {
      projects$: new BehaviorSubject([
        {
          id: 'proj-1',
          sourceSuggestionId: 's1',
          lastScanSessionId: 'scan-1',
          rootPath: 'D:\\code',
          name: 'dotnet-api',
          description: 'Initial description',
          score: 0.88,
          kind: 'ProjectRoot',
          path: 'D:\\code\\dotnet-api',
          reason: 'markers: .sln',
          extensionsSummary: 'cs=10',
          markers: ['.sln'],
          techHints: ['csharp'],
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z',
          tags: [
            { id: 'tag-1', name: 'csharp' },
            { id: 'tag-2', name: 'backend' }
          ]
        }
      ]),
      deleteProject: vi.fn().mockResolvedValue({ id: 'proj-1', deleted: true }),
      updateProjectDescription: vi.fn().mockResolvedValue({
        id: 'proj-1',
        updated: true,
        description: 'Updated description'
      }),
      attachTag: vi.fn().mockResolvedValue({ projectId: 'proj-1', tagId: 'tag-3', attached: true }),
      detachTag: vi.fn().mockResolvedValue({ projectId: 'proj-1', tagId: 'tag-1', detached: true })
    };
    const tagsServiceMock = {
      tags$: new BehaviorSubject([
        {
          id: 'tag-1',
          name: 'csharp',
          isSystem: true,
          projectCount: 1,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        },
        {
          id: 'tag-2',
          name: 'backend',
          isSystem: false,
          projectCount: 1,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        },
        {
          id: 'tag-3',
          name: 'api',
          isSystem: false,
          projectCount: 0,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ])
    };
    deleteProjectSpy = projectsServiceMock.deleteProject;
    updateDescriptionSpy = projectsServiceMock.updateProjectDescription;
    attachTagSpy = projectsServiceMock.attachTag;
    detachTagSpy = projectsServiceMock.detachTag;

    await TestBed.configureTestingModule({
      imports: [OrganizerPageComponent],
      providers: [
        { provide: ProjectsService, useValue: projectsServiceMock },
        { provide: TagsService, useValue: tagsServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(OrganizerPageComponent);
    fixture.detectChanges();
  });

  it('renders accepted projects from service', () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('dotnet-api');
    expect(text).toContain('D:\\code\\dotnet-api');
    expect(text).toContain('csharp');
    expect(text).toContain('backend');
    expect(text).toContain('Initial description');
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

  it('allows editing project description', async () => {
    const project = fixture.componentInstance.projects()[0];
    fixture.componentInstance.beginEditDescription(project);
    fixture.componentInstance.editDescriptionValue = 'Updated description';

    await fixture.componentInstance.saveDescription(project);

    expect(updateDescriptionSpy).toHaveBeenCalledWith('proj-1', 'Updated description');
  });

  it('attaches selected existing tag to project', async () => {
    (fixture.componentInstance as any).snackBar = { open: vi.fn() };
    const project = fixture.componentInstance.projects()[0];

    fixture.componentInstance.setSelectedTagId(project.id, 'tag-3');
    await fixture.componentInstance.attachSelectedTag(project);

    expect(attachTagSpy).toHaveBeenCalledWith('proj-1', 'tag-3');
  });

  it('detaches tag from project', async () => {
    (fixture.componentInstance as any).snackBar = { open: vi.fn() };
    const project = fixture.componentInstance.projects()[0];

    await fixture.componentInstance.detachTag(project, 'tag-1');

    expect(detachTagSpy).toHaveBeenCalledWith('proj-1', 'tag-1');
  });
});
