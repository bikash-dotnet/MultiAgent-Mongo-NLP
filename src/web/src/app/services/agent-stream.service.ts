import { Injectable, NgZone } from '@angular/core';
import { Observable } from 'rxjs';
import { SessionService } from './session.service';

export interface AgentEvent {
  event: string;
  data: string;
}

@Injectable({ providedIn: 'root' })
export class AgentStreamService {
  constructor(
    private readonly session: SessionService,
    private readonly zone: NgZone
  ) {}

  connect(): Observable<AgentEvent> {
    return new Observable((subscriber) => {
      const token = this.session.token();
      const params = new URLSearchParams();
      if (token) {
        params.set('access_token', token);
      }
      const source = new EventSource(`/api/agents/stream?${params.toString()}`);
      const names = ['agent.idle', 'agent.started', 'agent.clarifying', 'agent.completed'];

      const handlers = names.map((name) => {
        const handler = (event: MessageEvent) => {
          this.zone.run(() => subscriber.next({ event: name, data: event.data }));
        };
        source.addEventListener(name, handler as EventListener);
        return { name, handler };
      });

      source.onerror = () => {
        this.zone.run(() => subscriber.error(new Error('sse-disconnected')));
      };

      return () => {
        for (const { name, handler } of handlers) {
          source.removeEventListener(name, handler as EventListener);
        }
        source.close();
      };
    });
  }
}
