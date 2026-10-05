import { Injectable, NgZone } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { SessionService } from './session.service';

export interface AgentEvent {
  event: string;
  data: string;
}

@Injectable({ providedIn: 'root' })
export class AgentStreamService {
  private hubConnection: HubConnection | null = null;

  constructor(
    private readonly session: SessionService,
    private readonly zone: NgZone
  ) {}

  connect(): Observable<AgentEvent> {
    return new Observable((subscriber) => {
      const token = this.session.token();
      const connection = new HubConnectionBuilder()
        .withUrl('/hubs/agent-stream', {
          accessTokenFactory: () => token ?? ''
        })
        .withAutomaticReconnect()
        .build();

      this.hubConnection = connection;

      connection.on('ReceiveAgentEvent', (name: string, status: string, detail: string) => {
        const payload = JSON.stringify({ status, detail });
        this.zone.run(() => subscriber.next({ event: name, data: payload }));
      });

      connection.onreconnecting(() => {
        this.zone.run(() => subscriber.next({ event: 'sse-disconnected', data: 'reconnecting' }));
      });

      connection.onreconnected(() => {
        this.zone.run(() => subscriber.next({ event: 'agent.idle', data: 'idle' }));
      });

      connection.onclose(() => {
        this.zone.run(() => subscriber.error(new Error('signalr-disconnected')));
      });

      connection
        .start()
        .catch(() => {
          this.zone.run(() => subscriber.error(new Error('signalr-connection-failed')));
        });

      return () => {
        connection.stop();
      };
    });
  }
}
