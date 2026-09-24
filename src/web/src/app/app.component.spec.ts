import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { SessionService } from './services/session.service';
import { AgentStreamService } from './services/agent-stream.service';
import { ConversationService } from './services/conversation.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        {
          provide: SessionService,
          useValue: {
            sessionId: () => 'sess_test',
            role: () => null,
            bootstrap: () => of({ token: 't' }),
            greeting: () =>
              of({
                displayName: 'Bikash',
                period: 'morning',
                message: 'Hi Bikash, Good morning',
                chips: ['Just run it']
              })
          }
        },
        {
          provide: AgentStreamService,
          useValue: {
            connect: () => of({ event: 'agent.idle', data: '{"status":"idle"}' })
          }
        },
        {
          provide: ConversationService,
          useValue: {
            start: () => of({ conversationId: 'c1', step: 'Email', kind: 'ComplexLlmRequired', assistantMessage: 'Email?', control: 'email', deliveryOptions: ['EMAIL', 'CSV'], approvalRequired: false, downloadable: false, demoReport: false }),
            answer: () => of({}),
            get: () => of({}),
            downloadReport: () => of(new Blob()),
            getApproval: () => of({ enabled: true }),
            setApproval: () => of({ enabled: true })
          }
        }
      ]
    }).compileComponents();
  });

  it('hosts the chat thread', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('app-chat-thread')).toBeTruthy();
  });
});
