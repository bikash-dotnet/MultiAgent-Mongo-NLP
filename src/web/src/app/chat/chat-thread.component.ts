import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Greeting } from '../models/greeting';
import { ConversationAnswer, ConversationTurn } from '../models/conversation';
import { ConversationService } from '../services/conversation.service';

interface ChatMessage {
  role: 'user' | 'assistant';
  text: string;
  demo?: boolean;
}

@Component({
  selector: 'app-chat-thread',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './chat-thread.component.html',
  styleUrl: './chat-thread.component.scss'
})
export class ChatThreadComponent {
  @Input() greeting: Greeting | null = null;

  messages: ChatMessage[] = [];
  turn: ConversationTurn | null = null;
  text = '';
  email = '';
  purpose = '';
  projectCode = '';
  managerEmail = '';
  selectedColumns = new Set<string>();
  delivery = 'CSV';
  pending = false;
  error: string | null = null;

  constructor(private readonly conversations: ConversationService) {}

  send(): void {
    const utterance = this.text.trim();
    if (!utterance) {
      return;
    }

    this.messages = [...this.messages, { role: 'user', text: utterance }];
    this.pending = true;
    this.error = null;

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
    this.conversations.answer(this.turn.conversationId, answer).subscribe({
      next: (turn) => {
        if (answer.email) {
          this.messages = [...this.messages, { role: 'user', text: answer.email }];
        } else if (answer.purpose) {
          this.messages = [...this.messages, { role: 'user', text: answer.purpose }];
        } else if (answer.managerEmail) {
          this.messages = [...this.messages, { role: 'user', text: answer.managerEmail }];
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
    this.messages = [...this.messages, { role: 'assistant', text: turn.assistantMessage, demo: turn.demoReport }];
    if (turn.control === 'email') {
      this.email = turn.emailPrefill ?? this.email;
    }
    if (turn.control === 'columns' && turn.columns) {
      this.selectedColumns = new Set(turn.columns.filter((column) => column.selected).map((column) => column.name));
    }
    if (turn.validationError) {
      this.error = turn.validationError;
    }
  }
}
