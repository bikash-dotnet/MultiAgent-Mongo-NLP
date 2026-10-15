import { ChangeDetectorRef, Component, ElementRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { ChipModule } from 'primeng/chip';
import { Greeting } from '../models/greeting';
import { ConversationAnswer, ConversationTurn } from '../models/conversation';
import { ConversationService } from '../services/conversation.service';
import { AgentEvent } from '../services/agent-stream.service';
import { ResultsGridComponent } from '../results/results-grid.component';
import { AgentStatus } from '../models/agent-status';

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  demo?: boolean;
  timestamp?: string;
}

@Component({
  selector: 'app-chat-thread',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    ChipModule,
    ResultsGridComponent
  ],
  templateUrl: './chat-thread.component.html',
  styleUrl: './chat-thread.component.scss'
})
export class ChatThreadComponent implements OnChanges {
  readonly AgentStatus = AgentStatus;

  @Input() greeting: Greeting | null = null;
  @Input() latestEvent: AgentEvent | null = null;
  @Input() isAvailable = true;
  @Output() turnChange = new EventEmitter<ConversationTurn | null>();
  @ViewChild('scrollContainer') private scrollContainer?: ElementRef;

  messages: ChatMessage[] = [];
  turn: ConversationTurn | null = null;
  text = '';
  email = '';
  purpose = '';
  projectCode = '';
  businessImpact = '';
  managerEmail = '';
  selectedColumns = new Set<string>();
  delivery = 'CSV';
  pending = false;
  error: string | null = null;
  activeEventText: string | null = null;
  private hasAutoGreeted = false;

  get agentStatus(): AgentStatus {
    if (!this.isAvailable) {
      return AgentStatus.Unavailable;
    }
    if (this.pending || (this.latestEvent && ['agent.started', 'agent.executing', 'agent.clarifying'].includes(this.latestEvent.event))) {
      return AgentStatus.Working;
    }
    return AgentStatus.Idle;
  }

  get agentStatusText(): string {
    switch (this.agentStatus) {
      case AgentStatus.Working:
        return 'Working...';
      case AgentStatus.Idle:
        return 'Idle';
      case AgentStatus.Unavailable:
        return 'Unavailable';
    }
  }

  constructor(
    private readonly conversations: ConversationService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['latestEvent'] && this.latestEvent) {
      this.handleAgentEvent(this.latestEvent);
    }
    if (changes['greeting'] || changes['latestEvent']) {
      this.checkAutoGreeting();
    }
  }

  private checkAutoGreeting(): void {
    if (!this.hasAutoGreeted && this.greeting) {
      const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      this.messages = [
        ...this.messages,
        {
          role: 'assistant',
          text: this.greeting.message,
          timestamp: timeStr
        }
      ];
      this.hasAutoGreeted = true;
      this.cdr.markForCheck();
      this.scrollToBottom();
    }
  }

  private apply(turn: ConversationTurn): void {
    this.turn = turn;
    this.pending = false;
    const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    this.messages = [...this.messages, { role: 'assistant', text: turn.assistantMessage, demo: turn.demoReport, timestamp: timeStr }];
    if (turn.control === 'email') {
      this.email = turn.emailPrefill ?? this.email;
    }
    if (turn.control === 'columns' && turn.columns) {
      this.selectedColumns = new Set(turn.columns.filter((column) => column.selected).map((column) => column.name));
    }
    if (turn.step !== 'BusinessImpact') {
      this.businessImpact = '';
    }
    if (turn.validationError) {
      this.error = turn.validationError;
    }
    this.turnChange.emit(turn);
    this.cdr.markForCheck();
    this.scrollToBottom();
  }

  private handleAgentEvent(event: AgentEvent): void {
    switch (event.event) {
      case 'agent.idle':
        this.activeEventText = 'Agent Stream Connected';
        break;
      case 'agent.started':
        this.activeEventText = 'Agent Started Processing...';
        break;
      case 'agent.executing':
        this.activeEventText = 'Executing Query across Data Sources...';
        break;
      case 'agent.clarifying':
        this.activeEventText = 'Agent Requesting Clarification...';
        break;
      case 'agent.completed':
        this.activeEventText = 'Agent Execution Completed';
        break;
      case 'governance.paused':
        this.activeEventText = 'Governance Review Triggered (Paused)';
        break;
      case 'governance.request_enriched':
        this.activeEventText = 'Governance Request Enriched';
        break;
      case 'governance.manager_notified':
        this.activeEventText = 'Manager Approval Notification Sent';
        break;
      case 'governance.approved':
        this.activeEventText = 'Governance Request Approved';
        break;
      case 'governance.rejected':
        this.activeEventText = 'Governance Request Rejected';
        break;
      case 'report.ready':
        this.activeEventText = 'Report Ready for Download';
        break;
      case 'report.email_simulated':
        this.activeEventText = 'Report Email Dispatched';
        break;
      case 'conversation.started':
        this.activeEventText = 'Conversation Active';
        break;
      case 'conversation.resumed':
        this.activeEventText = 'Conversation Resumed';
        break;
      case 'conversation.completed':
        this.activeEventText = 'Conversation Finished';
        break;
      case 'agent.reconnecting':
        this.activeEventText = 'Reconnecting Stream...';
        break;
      default:
        this.activeEventText = event.event;
        break;
    }
  }

  get wordCount(): number {
    const trimmed = this.text.trim();
    if (!trimmed) {
      return 0;
    }
    return trimmed.split(/\s+/).length;
  }

  get isWordLimitExceeded(): boolean {
    return this.wordCount > 1000;
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      if (this.isAvailable && !this.pending && this.text.trim() && !this.isWordLimitExceeded && (!this.turn || this.turn.step === 'Complete')) {
        this.send();
      }
    }
  }

  send(): void {
    if (this.isWordLimitExceeded) {
      return;
    }
    const utterance = this.text.trim();
    if (!utterance) {
      return;
    }

    const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    this.messages = [...this.messages, { role: 'user', text: utterance, timestamp: timeStr }];
    this.text = '';
    this.pending = true;
    this.error = null;
    this.scrollToBottom();

    this.conversations.start(utterance).subscribe({
      next: (turn) => this.apply(turn),
      error: () => {
        this.error = 'Query failed. Is the gateway running?';
        this.pending = false;
      }
    });
  }

  submitEmail(): void {
    this.answer({ email: this.email.trim() });
  }

  submitPurpose(): void {
    this.answer({ purpose: this.purpose.trim(), projectCode: this.projectCode.trim() || undefined });
  }

  submitBusinessImpact(): void {
    this.answer({ businessImpact: this.businessImpact.trim() });
  }

  submitManagerEmail(): void {
    this.answer({ managerEmail: this.managerEmail.trim() });
  }

  submitColumns(): void {
    this.answer({ columns: Array.from(this.selectedColumns) });
  }

  submitDelivery(): void {
    this.answer({ delivery: this.delivery });
  }

  toggleColumn(name: string, checked: boolean): void {
    const next = new Set(this.selectedColumns);
    if (checked) {
      next.add(name);
    } else {
      next.delete(name);
    }
    this.selectedColumns = next;
  }

  useChip(chip: string): void {
    this.text = chip;
    this.send();
  }

  download(): void {
    if (!this.turn?.downloadable) {
      return;
    }

    this.conversations.downloadReport(this.turn.conversationId).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = 'report.csv';
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }

  private answer(answer: ConversationAnswer): void {
    if (!this.turn) {
      return;
    }

    this.pending = true;
    this.error = null;
    const timeStr = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    this.conversations.answer(this.turn.conversationId, answer).subscribe({
      next: (turn) => {
        if (answer.email) {
          this.messages = [...this.messages, { role: 'user', text: answer.email, timestamp: timeStr }];
        } else if (answer.purpose) {
          this.messages = [...this.messages, { role: 'user', text: answer.purpose, timestamp: timeStr }];
        } else if (answer.businessImpact) {
          this.messages = [...this.messages, { role: 'user', text: answer.businessImpact, timestamp: timeStr }];
        } else if (answer.managerEmail) {
          this.messages = [...this.messages, { role: 'user', text: answer.managerEmail, timestamp: timeStr }];
        } else if (answer.columns) {
          this.messages = [...this.messages, { role: 'user', text: answer.columns.join(', '), timestamp: timeStr }];
        } else if (answer.delivery) {
          this.messages = [...this.messages, { role: 'user', text: answer.delivery, timestamp: timeStr }];
        }
        this.apply(turn);
      },
      error: () => {
        this.error = 'Unable to continue the conversation.';
        this.pending = false;
      }
    });
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (this.scrollContainer) {
        this.scrollContainer.nativeElement.scrollTop = this.scrollContainer.nativeElement.scrollHeight;
      }
    }, 50);
  }
}
