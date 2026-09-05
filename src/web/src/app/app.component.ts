import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { retry, Subscription, switchMap, timer } from 'rxjs';
import { Greeting } from './models/greeting';
import { AgentStreamService } from './services/agent-stream.service';
import { SessionService } from './services/session.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  error: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService
  ) {}

  ngOnInit(): void {
    this.session.sessionId();
    this.sub.add(
      this.session.bootstrap().pipe(switchMap(() => this.session.greeting())).subscribe({
        next: (greeting) => {
          this.greeting = greeting;
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
        next: () => {
          this.streamStatus = 'agent.idle';
        },
        error: () => {
          this.streamStatus = 'reconnecting';
        }
      })
    );
  }
}
