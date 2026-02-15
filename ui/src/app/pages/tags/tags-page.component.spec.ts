import { ComponentFixture, TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { TagsPageComponent } from './tags-page.component';
import { TagsService } from '../../services/tags.service';

describe('TagsPageComponent', () => {
  let fixture: ComponentFixture<TagsPageComponent>;
  let addSpy: ReturnType<typeof vi.fn>;
  let updateSpy: ReturnType<typeof vi.fn>;
  let deleteSpy: ReturnType<typeof vi.fn>;
  let listProjectsSpy: ReturnType<typeof vi.fn>;
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
    dialogOpenSpy = vi.fn().mockReturnValue({
      afterClosed: () => of(true)
    });

    await TestBed.configureTestingModule({
      imports: [TagsPageComponent],
      providers: [{ provide: TagsService, useValue: serviceMock }]
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
  });
});
