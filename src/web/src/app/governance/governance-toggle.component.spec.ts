import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GovernanceToggleComponent } from './governance-toggle.component';
import { ConversationService } from '../services/conversation.service';

describe('GovernanceToggleComponent', () => {
  let saved: boolean[];

  beforeEach(async () => {
    saved = [];
    await TestBed.configureTestingModule({
      imports: [GovernanceToggleComponent],
      providers: [
        {
          provide: ConversationService,
          useValue: {
            getApproval: () => of({ enabled: true }),
            setApproval: (enabled: boolean) => {
              saved.push(enabled);
              return of({ enabled });
            }
          }
        }
      ]
    }).compileComponents();
  });

  it('is hidden when not visible', () => {
    const fixture = TestBed.createComponent(GovernanceToggleComponent);
    fixture.componentInstance.visible = false;
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('#governance-toggle')).toBeNull();
  });

  it('loads and saves the flag', () => {
    const fixture = TestBed.createComponent(GovernanceToggleComponent);
    fixture.componentInstance.visible = true;
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('#governance-toggle') as HTMLInputElement;
    expect(input.checked).toBe(true);

    fixture.componentInstance.toggle(false);

    expect(saved).toEqual([false]);
  });
});
