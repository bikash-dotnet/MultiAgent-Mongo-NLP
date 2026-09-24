import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { retry, Subscription, switchMap, timer } from 'rxjs';
import { Greeting } from './models/greeting';
import { AgentStreamService } from './services/agent-stream.service';
import { SessionService } from './services/session.service';
import { ChatThreadComponent } from './chat/chat-thread.component';
import { GovernanceToggleComponent } from './governance/governance-toggle.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, ChatThreadComponent, GovernanceToggleComponent],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  agentActivity: string[] = [];
  canManageGovernance = false;
  error: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService
  ) {}

  ngOnInit(): void {
    this.session.sessionId();
    this.canManageGovernance = this.session.role() === 'Data Owner / Admin';
    this.sub.add(
      this.session.bootstrap().pipe(switchMap(() => this.session.greeting())).subscribe({
        next: (greeting) => {
          this.greeting = greeting;
          this.canManageGovernance = this.session.role() === 'Data Owner / Admin';
          this.listen();
        },
        error: () => {
          this.error = 'Unable to load session. Start the ASP.NET Core gateway on port 5235.';
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  private listen(): void {
    this.sub.add(
      this.agents.connect().pipe(retry({ delay: () => timer(2000) })).subscribe({
        next: (event) => {
          this.streamStatus = event.event;
          this.agentActivity = [...this.agentActivity.slice(-4), event.event];
        },
        error: () => {
          this.streamStatus = 'reconnecting';
        }
      })
    );
  }
}
