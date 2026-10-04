import { Component, ElementRef, Input, ViewChild } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { RadioButtonModule } from 'primeng/radiobutton';
import { ChipModule } from 'primeng/chip';
import { Greeting } from '../models/greeting';
import { ConversationAnswer, ConversationTurn } from '../models/conversation';
import { ConversationService } from '../services/conversation.service';
import { ResultsGridComponent } from '../results/results-grid.component';

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  demo?: boolean;
  timestamp?: string;
}

interface Contact {
  id: string;
  name: string;
  initials: string;
  avatarClass: string;
  online: boolean;
  time: string;
  lastMessage: string;
  unreadCount?: number;
  statusText: string;
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
    CheckboxModule,
    RadioButtonModule,
    ChipModule,
    ResultsGridComponent
  ],
  templateUrl: './chat-thread.component.html',
  styleUrl: './chat-thread.component.scss'
})
export class ChatThreadComponent {
  @Input() greeting: Greeting | null = null;
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
  contactSearchQuery = '';
  showContactsMobile = false;

  contacts: Contact[] = [
    { id: '1', name: 'Multi-Agent Assistant', initials: 'AI', avatarClass: 'avatar-primary', online: true, time: 'Now', lastMessage: 'Ready to query listings & reports...', statusText: 'Online' },
    { id: '2', name: 'Olivia Bennett', initials: 'OB', avatarClass: 'avatar-info', online: true, time: '2m', lastMessage: 'Approved — a few small notes…', unreadCount: 2, statusText: 'Online' },
    { id: '3', name: 'Marcus Reyes', initials: 'MR', avatarClass: 'avatar-success', online: true, time: '1h', lastMessage: 'Data model validation completed.', statusText: 'Online' },
    { id: '4', name: 'Sara Khan', initials: 'SK', avatarClass: 'avatar-info', online: false, time: '3h', lastMessage: 'Customer interview notes are up.', statusText: 'Offline' },
    { id: '5', name: 'Diego Smania', initials: 'DS', avatarClass: 'avatar-warning', online: true, time: 'Yesterday', lastMessage: 'PR is ready for review.', unreadCount: 1, statusText: 'Online' }
  ];

  activeContactId = '1';

  get activeContact(): Contact {
    return this.contacts.find((c) => c.id === this.activeContactId) || this.contacts[0];
  }

  filteredContacts(): Contact[] {
    const query = this.contactSearchQuery.toLowerCase().trim();
    if (!query) return this.contacts;
    return this.contacts.filter((c) => c.name.toLowerCase().includes(query) || c.lastMessage.toLowerCase().includes(query));
  }

  selectContact(contact: Contact): void {
    this.activeContactId = contact.id;
    this.showContactsMobile = false;
  }

  constructor(private readonly conversations: ConversationService) {}

  send(): void {
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
    this.scrollToBottom();
  }

  private scrollToBottom(): void {
    setTimeout(() => {
      if (this.scrollContainer) {
        this.scrollContainer.nativeElement.scrollTop = this.scrollContainer.nativeElement.scrollHeight;
      }
    }, 50);
  }
}
