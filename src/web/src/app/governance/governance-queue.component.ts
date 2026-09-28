import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AccessRequest } from '../models/governance';
import { GovernanceService } from '../services/governance.service';
import { SessionService } from '../services/session.service';

@Component({
  selector: 'app-governance-queue',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './governance-queue.component.html',
  styleUrl: './governance-queue.component.scss'
})
export class GovernanceQueueComponent implements OnInit {
  requests: AccessRequest[] = [];
  overrideNotes = '';
  error: string | null = null;

  constructor(
    private readonly governance: GovernanceService,
    private readonly session: SessionService
  ) {}

  ngOnInit(): void {
    this.refresh();
  }

  get canDecide(): boolean {
    const role = this.session.role() ?? '';
    return (
      role === 'Team Lead' ||
      role === 'Engineering Manager' ||
      role === 'Director' ||
      role === 'Data Owner / Admin'
    );
  }

  refresh(): void {
    this.governance.list().subscribe({
      next: (requests) => {
        this.requests = requests;
        this.error = null;
      },
      error: () => (this.error = 'Unable to load the approval queue.')
    });
  }

  approve(request: AccessRequest): void {
    this.governance.approve(request.id).subscribe({
      next: () => this.refresh(),
      error: () => (this.error = 'Unable to approve this request.')
    });
  }

  reject(request: AccessRequest): void {
    this.governance.reject(request.id).subscribe({
      next: () => this.refresh(),
      error: () => (this.error = 'Unable to reject this request.')
    });
  }

  override(request: AccessRequest): void {
    const notes = this.overrideNotes.trim();
    if (!notes) {
      this.error = 'Override notes are required.';
      return;
    }

    this.governance.override(request.id, notes).subscribe({
      next: () => {
        this.overrideNotes = '';
        this.refresh();
      },
      error: () => (this.error = 'Unable to override this request.')
    });
  }
}
