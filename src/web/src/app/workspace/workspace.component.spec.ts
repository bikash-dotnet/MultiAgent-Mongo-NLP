import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { WorkspaceComponent } from './workspace.component';
import { SessionService } from '../services/session.service';
import { AgentStreamService } from '../services/agent-stream.service';
import { ConversationService } from '../services/conversation.service';

describe('WorkspaceComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [WorkspaceComponent],
      providers: [
        {
          provide: SessionService,
          useValue: {
            sessionId: () => 'sess_test',
            role: () => null,
            bootstrap: () => of({ token: 't' }),
            greeting: () => of({ displayName: 'Bikash', period: 'morning', message: 'Hi Bikash, Good morning', chips: ['Just run it'] })
          }
        },
        { provide: AgentStreamService, useValue: { connect: () => of({ event: 'agent.idle', data: '{}' }) } },
        { provide: ConversationService, useValue: { getApproval: () => of({ enabled: true }) } }
      ]
    }).compileComponents();
  });

  it('hosts the chat thread', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-chat-thread')).toBeTruthy();
  });
});
