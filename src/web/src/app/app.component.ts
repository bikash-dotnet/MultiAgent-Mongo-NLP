import { Component, OnDestroy, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { retry, Subscription, switchMap, timer } from 'rxjs';
import { Greeting } from './models/greeting';
import { NlpQueryResponse } from './models/nlp-query-response';
import { AgentStreamService } from './services/agent-stream.service';
import { NlpQueryService } from './services/nlp-query.service';
import { SessionService } from './services/session.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent implements OnInit, OnDestroy {
  greeting: Greeting | null = null;
  streamStatus = 'connecting';
  agentActivity: string[] = [];
  queryText = '';
  queryResult: NlpQueryResponse | null = null;
  error: string | null = null;
  private sub = new Subscription();

  constructor(
    private readonly session: SessionService,
    private readonly agents: AgentStreamService,
    private readonly nlp: NlpQueryService
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

  runQuery(): void {
    const utterance = this.queryText.trim();
    if (!utterance) {
      return;
    }

    this.sub.add(
      this.nlp.query(utterance).subscribe({
        next: (result) => {
          this.queryResult = result;
          this.streamStatus = result.kind;
        },
        error: () => {
          this.error = 'Query failed. Is the gateway running?';
        }
      })
    );
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
