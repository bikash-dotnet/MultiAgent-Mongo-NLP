import { Inject, Injectable, InjectionToken, NgZone } from '@angular/core';
import { HubConnectionBuilder } from '@microsoft/signalr';
import { Observable } from 'rxjs';
import { SessionService } from './session.service';

export interface AgentEvent {
  event: string;
  data: string;
}

export interface AgentHubConnection {
  on(methodName: string, newMethod: (...args: any[]) => any): void;
  onreconnecting(callback: (...args: any[]) => void): void;
  onreconnected(callback: (...args: any[]) => void): void;
  onclose(callback: (...args: any[]) => void): void;
  start(): Promise<void>;
  stop(): Promise<void>;
}

export type AgentHubConnectionFactory = (url: string, token: string) => AgentHubConnection;

export const AGENT_HUB_CONNECTION_FACTORY = new InjectionToken<AgentHubConnectionFactory>(
  'AGENT_HUB_CONNECTION_FACTORY',
  {
    providedIn: 'root',
    factory: () => (url: string, token: string) =>
      new HubConnectionBuilder()
        .withUrl(url, { accessTokenFactory: () => token })
        .withAutomaticReconnect()
        .build()
  }
);

@Injectable({ providedIn: 'root' })
export class AgentStreamService {
  private hubConnection: AgentHubConnection | null = null;

  constructor(
    private readonly session: SessionService,
    private readonly zone: NgZone,
    @Inject(AGENT_HUB_CONNECTION_FACTORY)
    private readonly createConnection: AgentHubConnectionFactory
  ) {}

  connect(): Observable<AgentEvent> {
    return new Observable((subscriber) => {
      const token = this.session.token();
      const connection = this.createConnection('/hubs/agent-stream', token ?? '');

      this.hubConnection = connection;

      connection.on('ReceiveAgentEvent', (name: string, status: string, detail: string) => {
        const payload = JSON.stringify({ status, detail });
        this.zone.run(() => subscriber.next({ event: name, data: payload }));
      });

      connection.onreconnecting(() => {
        this.zone.run(() => subscriber.next({ event: 'agent.reconnecting', data: 'reconnecting' }));
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
