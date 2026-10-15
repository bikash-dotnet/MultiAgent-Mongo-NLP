import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { WorkspaceComponent } from './workspace.component';
import { SessionService } from '../services/session.service';
import { AgentStreamService } from '../services/agent-stream.service';
import { ConversationService } from '../services/conversation.service';

describe('WorkspaceComponent', () => {
  let csvCalls: string[];
  let xlsxCalls: string[];
  let pdfCalls: string[];

  const activeConversation = {
    conversationId: 'c-42',
    intent: 'Query',
    appliedSlots: [],
    reportProgress: 50,
    governanceStatus: 'Exempted'
  };

  beforeEach(async () => {
    csvCalls = [];
    xlsxCalls = [];
    pdfCalls = [];
    (URL as any).createObjectURL = () => 'blob:test';
    (URL as any).revokeObjectURL = () => {};

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
        {
          provide: ConversationService,
          useValue: {
            getApproval: () => of({ enabled: true }),
            downloadReport: (id: string) => {
              csvCalls.push(id);
              return of(new Blob(['csv']));
            },
            downloadXlsxReport: (id: string) => {
              xlsxCalls.push(id);
              return of(new Blob(['xlsx']));
            },
            downloadPdfReport: (id: string) => {
              pdfCalls.push(id);
              return of(new Blob(['pdf']));
            }
          }
        }
      ]
    }).compileComponents();
  });

  it('hosts the chat thread', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('app-chat-thread')).toBeTruthy();
  });

  it('downloads the PDF briefing for the active conversation', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();
    fixture.componentInstance.setActiveConversation(activeConversation);

    fixture.componentInstance.downloadPdf();

    expect(pdfCalls).toEqual(['c-42']);
  });

  it('downloads XLSX and CSV for the active conversation', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();
    fixture.componentInstance.setActiveConversation(activeConversation);

    fixture.componentInstance.downloadXlsx();
    fixture.componentInstance.downloadCsv();

    expect(xlsxCalls).toEqual(['c-42']);
    expect(csvCalls).toEqual(['c-42']);
  });

  it('does not download when no conversation is active', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();

    fixture.componentInstance.downloadPdf();
    fixture.componentInstance.downloadXlsx();
    fixture.componentInstance.downloadCsv();

    expect(pdfCalls).toEqual([]);
    expect(xlsxCalls).toEqual([]);
    expect(csvCalls).toEqual([]);
  });

  it('populates the conversation id when a turn changes', () => {
    const fixture = TestBed.createComponent(WorkspaceComponent);
    fixture.detectChanges();

    fixture.componentInstance.onTurnChange({
      conversationId: 'c-7',
      step: 'Complete',
      kind: 'ComplexLlmRequired',
      assistantMessage: 'done',
      control: 'none',
      deliveryOptions: ['EMAIL', 'CSV'],
      approvalRequired: false,
      downloadable: true,
      demoReport: false
    });

    expect(fixture.componentInstance.activeConversation?.conversationId).toBe('c-7');
  });
});
