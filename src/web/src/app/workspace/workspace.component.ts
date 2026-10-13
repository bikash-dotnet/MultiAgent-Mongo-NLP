import { ChangeDetectorRef, Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { retry, Subscription, switchMap, timer, take } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { Greeting } from '../models/greeting';
import { ConversationTurn } from '../models/conversation';
import { AgentEvent, AgentStreamService } from '../services/agent-stream.service';
import { SessionService } from '../services/session.service';
import { ChatThreadComponent } from '../chat/chat-thread.component';
import { GovernanceToggleComponent } from '../governance/governance-toggle.component';
import { ConversationService } from '../services/conversation.service';

interface Conversation {
  conversationId: string;
  intent: string;
  appliedSlots: string[];
  reportProgress: number;
  governanceStatus: string;
}

@Component({
  selector: 'app-workspace',
  standalone: true,
  imports: [CommonModule, ButtonModule, ChatThreadComponent],
  templateUrl: './workspace.component.html',
  styleUrl: './workspace.component.scss'
})
export class WorkspaceComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  agentActivity: string[] = [];
  latestEvent: AgentEvent | null = null;
  error: string | null = null;
  activeConversation: Conversation | null = null;
  private sub = new Subscription();

  get isAgentAvailable(): boolean {
    return !this.error && this.streamStatus !== 'reconnecting';
  }

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService,
    private readonly conversations: ConversationService,
    private readonly cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    this.session.sessionId();
    this.sub.add(
      this.session.bootstrap().subscribe({
        next: () => {
          this.listen();
          this.loadGreeting();
          this.cdr.markForCheck();
        },
        error: () => {
          this.error = 'Unable to load session. Start the ASP.NET Core gateway on port 5235.';
          this.cdr.markForCheck();
        }
      })
    );
  }

  private loadGreeting(): void {
    this.sub.add(
      this.session.greeting().subscribe({
        next: (greeting) => {
          this.greeting = greeting;
          this.cdr.markForCheck();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  downloadCsv(): void {
    if (!this.activeConversation) {
      return;
    }
    this.conversations
      .downloadReport(this.activeConversation.conversationId)
      .pipe(take(1))
      .subscribe((blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'report.csv';
        anchor.click();
        URL.revokeObjectURL(url);
      });
  }

  downloadXlsx(): void {
    if (!this.activeConversation) {
      return;
    }
    this.conversations
      .downloadXlsxReport(this.activeConversation.conversationId)
      .pipe(take(1))
      .subscribe((blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'report.xlsx';
        anchor.click();
        URL.revokeObjectURL(url);
      });
  }

  downloadPdf(): void {
    if (!this.activeConversation) {
      return;
    }
    this.conversations
      .downloadPdfReport(this.activeConversation.conversationId)
      .pipe(take(1))
      .subscribe((blob: Blob) => {
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = 'briefing.pdf';
        anchor.click();
        URL.revokeObjectURL(url);
      });
  }

  private listen(): void {
    this.sub.add(
      this.agents.connect().pipe(retry({ delay: () => timer(2000) })).subscribe({
        next: (event) => {
          this.latestEvent = event;
          this.streamStatus = event.event;
          this.agentActivity = [...this.agentActivity.slice(-4), event.event];
          this.cdr.markForCheck();
        },
        error: () => {
          this.streamStatus = 'reconnecting';
          this.latestEvent = { event: 'agent.reconnecting', data: 'reconnecting' };
          this.cdr.markForCheck();
        }
      })
    );
  }

  onTurnChange(turn: ConversationTurn | null): void {
    if (turn) {
      this.activeConversation = {
        conversationId: turn.conversationId,
        intent: turn.kind || 'Query',
        appliedSlots: turn.columns ? turn.columns.filter((c) => c.selected).map((c) => c.name) : [],
        reportProgress: turn.step === 'Complete' ? 100 : turn.step === 'BusinessImpact' ? 75 : 50,
        governanceStatus: turn.approvalRequired ? 'Approval Required' : 'Exempted'
      };
      this.cdr.markForCheck();
    }
  }

  setActiveConversation(conversation: Conversation): void {
    this.activeConversation = conversation;
    this.cdr.markForCheck();
  }
}