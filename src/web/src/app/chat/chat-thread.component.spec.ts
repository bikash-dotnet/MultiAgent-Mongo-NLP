import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ChatThreadComponent } from './chat-thread.component';
import { ConversationService } from '../services/conversation.service';

const emailTurn = {
  conversationId: 'c1',
  step: 'Email',
  kind: 'ComplexLlmRequired',
  assistantMessage: 'Which email should receive it?',
  control: 'email',
  deliveryOptions: ['EMAIL', 'CSV'],
  approvalRequired: false,
  downloadable: false,
  demoReport: false
};

const columnsTurn = {
  conversationId: 'c1',
  step: 'Columns',
  kind: 'ComplexLlmRequired',
  assistantMessage: 'Confirm the columns.',
  control: 'columns',
  columns: [
    { name: 'name', selected: true },
    { name: 'price', selected: true }
  ],
  deliveryOptions: ['EMAIL', 'CSV'],
  approvalRequired: false,
  downloadable: false,
  demoReport: false
};

describe('ChatThreadComponent', () => {
  let startCalls: string[];
  let answerCalls: unknown[];

  beforeEach(async () => {
    startCalls = [];
    answerCalls = [];
    await TestBed.configureTestingModule({
      imports: [ChatThreadComponent],
      providers: [
        {
          provide: ConversationService,
          useValue: {
            start: (utterance: string) => {
              startCalls.push(utterance);
              return of(emailTurn);
            },
            answer: (_id: string, answer: unknown) => {
              answerCalls.push(answer);
              return of(columnsTurn);
            },
            downloadReport: () => of(new Blob()),
            get: () => of(emailTurn),
            getApproval: () => of({ enabled: true }),
            setApproval: () => of({ enabled: true })
          }
        }
      ]
    }).compileComponents();
  });

  it('uses a Send button with an accessible label', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const button = fixture.nativeElement.querySelector('button[type="submit"]') as HTMLButtonElement;

    expect(button.getAttribute('aria-label')).toBe('Send message');
    expect(button.textContent).toContain('Send');
  });

  it('starts a conversation and renders the email control', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.text = 'average price by market';
    component.send();
    fixture.detectChanges();

    expect(startCalls).toEqual(['average price by market']);
    expect(fixture.nativeElement.querySelector('#step-email')).toBeTruthy();
  });

  it('submits selected columns', () => {
    const fixture = TestBed.createComponent(ChatThreadComponent);
    fixture.detectChanges();
    const component = fixture.componentInstance;

    component.text = 'average price by market';
    component.send();
    fixture.detectChanges();
    component.submitEmail();
    fixture.detectChanges();

    component.selectedColumns = new Set(['name']);
    component.submitColumns();

    expect(answerCalls[0]).toEqual({ email: component.email });
  });
});
