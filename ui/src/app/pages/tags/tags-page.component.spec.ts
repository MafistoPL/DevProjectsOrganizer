import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TagsPageComponent } from './tags-page.component';
import { ProjectsService } from '../../services/projects.service';
import { TagsService } from '../../services/tags.service';

describe('TagsPageComponent', () => {
  let fixture: ComponentFixture<TagsPageComponent>;
  let addSpy: ReturnType<typeof vi.fn>;
  let updateSpy: ReturnType<typeof vi.fn>;
  let deleteSpy: ReturnType<typeof vi.fn>;
  let listProjectsSpy: ReturnType<typeof vi.fn>;
  let runAllHeuristicsSpy: ReturnType<typeof vi.fn>;
  let dialogOpenSpy: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    const serviceMock = {
      tags$: new BehaviorSubject([
        {
          id: 'tag-1',
          name: 'csharp',
          isSystem: true,
          projectCount: 2,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        },
        {
          id: 'tag-2',
          name: 'custom',
          isSystem: false,
          projectCount: 0,
          createdAt: '2026-02-13T10:00:00.000Z',
          updatedAt: '2026-02-13T10:00:00.000Z'
        }
      ]),
      addTag: vi.fn().mockResolvedValue(undefined),
      updateTag: vi.fn().mockResolvedValue(undefined),
      deleteTag: vi.fn().mockResolvedValue(undefined),
      listProjects: vi.fn().mockResolvedValue([])
    };
    addSpy = serviceMock.addTag;
    updateSpy = serviceMock.updateTag;
    deleteSpy = serviceMock.deleteTag;
    listProjectsSpy = serviceMock.listProjects;

    const projectsServiceMock = {
      runTagHeuristicsForAll: vi.fn().mockImplementation(async (onProgress?: (value: any) => void) => {
        onProgress?.({
          index: 1,
          total: 1,
          projectName: 'dotnet-api',
          generatedCount: 2,
          generatedTotal: 2,
          failed: 0
        });

        return {
          total: 1,
          processed: 1,
          failed: 0,
          generatedTotal: 2,
          regressionReport: {
            projectsAnalyzed: 1,
            baselineAcceptedCount: 1,
            baselineRejectedCount: 0,
            acceptedMissingCount: 0,
            rejectedMissingCount: 0,
            addedCount: 1,
            projects: [
              {
                projectId: 'proj-1',
                projectName: 'dotnet-api',
                baselineAcceptedCount: 1,
                baselineRejectedCount: 0,
                acceptedMissingCount: 0,
                rejectedMissingCount: 0,
                addedCount: 1
              }
            ]
          }
        };
      })
    };
    runAllHeuristicsSpy = projectsServiceMock.runTagHeuristicsForAll;

    dialogOpenSpy = vi.fn().mockReturnValue({
      afterClosed: () => of(true)
    });

    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [
        { provide: TagsService, useValue: serviceMock },
        { provide: ProjectsService, useValue: projectsServiceMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(TagsPageComponent);
    (fixture.componentInstance as any).dialog = { open: dialogOpenSpy };
    fixture.detectChanges();
  });

  it('renders tags, keeps system tag non-deletable and calls add/update/delete actions', async () => {
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('csharp');
    expect(text).toContain('Seeded');

    const addInput: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="tag-add-input"]');
    addInput.value = 'cpp';
    addInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="tag-add-btn"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(addSpy).toHaveBeenCalledWith('cpp');

    (fixture.nativeElement.querySelector('[data-testid="tag-edit-btn"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    const editInput: HTMLInputElement = fixture.nativeElement.querySelector('[data-testid="tag-edit-input"]');
    editInput.value = 'backend';
    editInput.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="tag-save-btn"]') as HTMLButtonElement).click();
    await fixture.whenStable();

    expect(updateSpy).toHaveBeenCalledWith('tag-1', 'backend');

    const rows = Array.from(fixture.nativeElement.querySelectorAll('[data-testid="tag-row"]')) as HTMLElement[];
    const systemRow = rows.find((row) => row.textContent?.includes('csharp'));
    const customRow = rows.find((row) => row.textContent?.includes('custom'));
    expect(systemRow).toBeTruthy();
    expect(customRow).toBeTruthy();
    expect(systemRow!.querySelector('[data-testid="tag-delete-btn"]')).toBeNull();

    (customRow!.querySelector('[data-testid="tag-delete-btn"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(deleteSpy).toHaveBeenCalledWith('tag-2');

    (systemRow!.querySelector('[data-testid="tag-project-count-btn-tag-1"]') as HTMLButtonElement).click();
    await fixture.whenStable();
    expect(listProjectsSpy).toHaveBeenCalledWith('tag-1');
    expect(dialogOpenSpy).toHaveBeenCalled();

    (fixture.nativeElement.querySelector('[data-testid="tag-apply-heuristics-all-btn"]') as HTMLButtonElement)
      .click();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(runAllHeuristicsSpy).toHaveBeenCalledTimes(1);
    const status = fixture.nativeElement.querySelector('[data-testid="tag-apply-heuristics-status"]');
    expect(status?.textContent).toContain('Processed 1/1');
    const regression = fixture.nativeElement.querySelector('[data-testid="tag-heuristics-regression-report"]');
    expect(regression?.textContent).toContain('Projects analyzed: 1');
  });
});
