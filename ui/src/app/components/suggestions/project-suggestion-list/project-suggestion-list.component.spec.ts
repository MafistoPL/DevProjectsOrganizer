import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { BehaviorSubject, of } from 'rxjs';
import { vi } from 'vitest';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ProjectSuggestionListComponent } from './project-suggestion-list.component';
import { SuggestionsService } from '../../../services/suggestions.service';
import { ProjectsService } from '../../../services/projects.service';

describe('ProjectSuggestionListComponent', () => {
  let fixture: ComponentFixture<ProjectSuggestionListComponent>;
  let component: ProjectSuggestionListComponent;
  let suggestionsServiceMock: {
    items$: BehaviorSubject<any[]>;
    setStatus: ReturnType<typeof vi.fn>;
    exportArchiveJson: ReturnType<typeof vi.fn>;
    openArchiveFolder: ReturnType<typeof vi.fn>;
    openPath: ReturnType<typeof vi.fn>;
    deleteSuggestion: ReturnType<typeof vi.fn>;
  };
  let projectsServiceMock: {
    findBySourceSuggestionId: ReturnType<typeof vi.fn>;
    runTagHeuristics: ReturnType<typeof vi.fn>;
    runAiTagSuggestions: ReturnType<typeof vi.fn>;
  };
  let dialogOpenSpy: ReturnType<typeof vi.fn>;

  beforeEach(async () => {
    suggestionsServiceMock = {
      items$: new BehaviorSubject<any[]>([]),
      setStatus: vi.fn().mockResolvedValue({}),
      exportArchiveJson: vi.fn().mockResolvedValue({ path: 'C:\\mock\\archive.json', count: 1 }),
      openArchiveFolder: vi.fn().mockResolvedValue({ path: 'C:\\mock' }),
      openPath: vi.fn().mockResolvedValue({ path: 'C:\\mock\\file' }),
      deleteSuggestion: vi.fn().mockResolvedValue(undefined)
    };

    projectsServiceMock = {
      findBySourceSuggestionId: vi.fn().mockReturnValue({
        id: 'project-1',
        name: 'dotnet-api'
      }),
      runTagHeuristics: vi.fn().mockResolvedValue({ generatedCount: 1 }),
      runAiTagSuggestions: vi.fn().mockResolvedValue({ action: 'AiTagSuggestionsQueued' })
    };

    const matDialogMock = {
      open: vi.fn().mockReturnValue({
        afterClosed: () => of({ action: 'skip', projectName: 'dotnet-api', projectDescription: '' })
      })
    };
    dialogOpenSpy = matDialogMock.open;

    const snackBarMock = {
      open: vi.fn()
    };

    await TestBed.configureTestingModule({
      imports: [ProjectSuggestionListComponent],
      providers: [
        provideNoopAnimations(),
        { provide: SuggestionsService, useValue: suggestionsServiceMock },
        { provide: ProjectsService, useValue: projectsServiceMock },
        { provide: MatDialog, useValue: matDialogMock },
        { provide: MatSnackBar, useValue: snackBarMock }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(ProjectSuggestionListComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('runs tag heuristics when accepted and selected in dialog', async () => {
    dialogOpenSpy.mockReturnValue({
      afterClosed: () => of({
        action: 'heuristics',
        projectName: 'dotnet-api-renamed',
        projectDescription: 'updated desc'
      })
    });

    await component.setStatus('suggestion-1', 'accepted', {
      id: 'suggestion-1',
      name: 'dotnet-api'
    } as any);

    expect(suggestionsServiceMock.setStatus).toHaveBeenCalledWith(
      'suggestion-1',
      'accepted',
      'dotnet-api-renamed',
      'updated desc'
    );
    expect(projectsServiceMock.findBySourceSuggestionId).toHaveBeenCalledWith('suggestion-1');
    expect(projectsServiceMock.runTagHeuristics).toHaveBeenCalledWith('project-1');
    expect(projectsServiceMock.runAiTagSuggestions).not.toHaveBeenCalled();
  });

  it('runs AI action when selected in dialog', async () => {
    dialogOpenSpy.mockReturnValue({
      afterClosed: () => of({ action: 'ai', projectName: 'dotnet-api', projectDescription: '' })
    });

    await component.setStatus('suggestion-1', 'accepted', {
      id: 'suggestion-1',
      name: 'dotnet-api'
    } as any);

    expect(projectsServiceMock.runAiTagSuggestions).toHaveBeenCalledWith('project-1');
    expect(projectsServiceMock.runTagHeuristics).not.toHaveBeenCalled();
  });

  it('cancels accept when dialog is closed without choice', async () => {
    dialogOpenSpy.mockReturnValue({ afterClosed: () => of(undefined) });

    await component.setStatus('suggestion-1', 'accepted', {
      id: 'suggestion-1',
      name: 'dotnet-api'
    } as any);

    expect(suggestionsServiceMock.setStatus).not.toHaveBeenCalled();
    expect(projectsServiceMock.runAiTagSuggestions).not.toHaveBeenCalled();
    expect(projectsServiceMock.runTagHeuristics).not.toHaveBeenCalled();
  });

  it('does not open post-accept dialog for rejected status', async () => {
    await component.setStatus('suggestion-1', 'rejected', {
      id: 'suggestion-1',
      name: 'dotnet-api'
    } as any);

    expect(dialogOpenSpy).not.toHaveBeenCalled();
    expect(projectsServiceMock.runAiTagSuggestions).not.toHaveBeenCalled();
    expect(projectsServiceMock.runTagHeuristics).not.toHaveBeenCalled();
  });
});
