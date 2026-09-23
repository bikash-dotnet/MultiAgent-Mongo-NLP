import { Component, Input, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ConversationService } from '../services/conversation.service';

@Component({
  selector: 'app-governance-toggle',
  standalone: true,
  imports: [CommonModule],
  template: `
    @if (visible) {
      <label class="governance">
        <input
          id="governance-toggle"
          type="checkbox"
          [checked]="enabled"
          (change)="toggle($any($event.target).checked)" />
        Approval gate
      </label>
      @if (error) {
        <span class="error" role="alert">{{ error }}</span>
      }
    }
  `,
  styles: [`
    .governance {
      display: inline-flex;
      gap: 0.4rem;
      align-items: center;
      font-size: 0.85rem;
    }
    .error {
      color: #b00020;
      font-size: 0.8rem;
    }
  `]
})
export class GovernanceToggleComponent implements OnInit {
  @Input() visible = false;

  enabled = true;
  error: string | null = null;

  constructor(private readonly conversations: ConversationService) {}

  ngOnInit(): void {
    if (!this.visible) {
      return;
    }

    this.conversations.getApproval().subscribe({
      next: (flag) => (this.enabled = flag.enabled),
      error: () => (this.error = 'Unable to read the approval flag.')
    });
  }

  toggle(enabled: boolean): void {
    this.enabled = enabled;
    this.conversations.setApproval(enabled).subscribe({
      next: (flag) => {
        this.enabled = flag.enabled;
        this.error = null;
      },
      error: () => {
        this.enabled = !enabled;
        this.error = 'Unable to change the approval flag.';
      }
    });
  }
}
