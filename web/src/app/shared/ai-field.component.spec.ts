import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { AiFieldComponent } from './ai-field.component';

describe('AiFieldComponent', () => {
  let fixture: ComponentFixture<AiFieldComponent>;
  let component: AiFieldComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AiFieldComponent],
      providers: [provideNoopAnimations()],
    }).compileComponents();
    fixture = TestBed.createComponent(AiFieldComponent);
    component = fixture.componentInstance;
    component.fieldName = 'AiSummary';
    component.label = 'AI summary';
    component.value = 'Initial value';
    fixture.detectChanges();
  });

  it('shows the AI-generated badge by default', () => {
    const badge = fixture.nativeElement.querySelector('[data-testid="ai-badge-AiSummary"]');
    expect(badge.textContent.trim()).toBe('AI-generated');
  });

  it('switches to Regenerated badge when wasRegenerated is true', () => {
    component.wasRegenerated = true;
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('[data-testid="ai-badge-AiSummary"]');
    expect(badge.textContent.trim()).toBe('Regenerated');
  });

  it('shows Modified by user when isUserEdited is true', () => {
    component.isUserEdited = true;
    fixture.detectChanges();
    const badge = fixture.nativeElement.querySelector('[data-testid="ai-badge-AiSummary"]');
    expect(badge.textContent.trim()).toBe('Modified by user');
  });

  it('emits regenerate event when button clicked', () => {
    const spy = jest.fn();
    component.onRegenerate.subscribe(spy);
    fixture.nativeElement.querySelector('[data-testid="regen-AiSummary"]').click();
    expect(spy).toHaveBeenCalledWith('AiSummary');
  });

  it('enters edit mode and emits edit on save', () => {
    const spy = jest.fn();
    component.onEdit.subscribe(spy);
    const editBtn = Array.from(fixture.nativeElement.querySelectorAll('button')).find(
      (b: any) => (b as HTMLElement).textContent?.trim().startsWith('edit')
    ) as HTMLElement;
    editBtn.click();
    fixture.detectChanges();
    component.draft = 'Edited value';
    component.saveEdit();
    expect(spy).toHaveBeenCalledWith({ field: 'AiSummary', value: 'Edited value' });
  });
});
