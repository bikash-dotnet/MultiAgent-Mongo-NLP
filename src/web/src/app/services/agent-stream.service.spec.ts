import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { AgentStreamService } from './agent-stream.service';
import { SessionService } from './session.service';

class FakeHubConnection {
  static current: FakeHubConnection | null = null;
  handlers = new Map<string, Function>();
  started = false;
  stopped = false;

  constructor(public readonly url: string) {
    FakeHubConnection.current = this;
  }

  on(methodName: string, newMethod: Function): void {
    this.handlers.set(methodName, newMethod);
  }

  onreconnecting(): void {}
  onreconnected(): void {}
  onclose(): void {}

  start(): Promise<void> {
    this.started = true;
    return Promise.resolve();
  }

  stop(): Promise<void> {
    this.stopped = true;
    return Promise.resolve();
  }

  emit(eventName: string, status: string, detail: string): void {
    const handler = this.handlers.get('ReceiveAgentEvent');
    if (handler) {
      handler(eventName, status, detail);
    }
  }
}

vi.mock('@microsoft/signalr', () => {
  return {
    HubConnectionBuilder: class {
      private url = '';
      withUrl(url: string) {
        this.url = url;
        return this;
      }
      withAutomaticReconnect() {
        return this;
      }
      build() {
        return new FakeHubConnection(this.url);
      }
    }
  };
});

describe('AgentStreamService (SignalR)', () => {
  let service: AgentStreamService;

  beforeEach(() => {
    FakeHubConnection.current = null;
    sessionStorage.setItem('access_token', 'token-123');
    TestBed.configureTestingModule({
      providers: [{ provide: SessionService, useValue: { token: () => 'token-123' } }]
    });
    service = TestBed.inject(AgentStreamService);
  });

  it('connects to SignalR hub endpoint /hubs/agent-stream', () => {
    const sub = service.connect().subscribe();
    const hub = FakeHubConnection.current!;

    expect(hub.url).toBe('/hubs/agent-stream');
    expect(hub.started).toBe(true);
    sub.unsubscribe();
    expect(hub.stopped).toBe(true);
  });

  it('forwards ReceiveAgentEvent from hub to subscriber', () => {
    const received: string[] = [];
    const sub = service.connect().subscribe((event) => received.push(event.event));
    const hub = FakeHubConnection.current!;

    hub.emit('agent.executing', 'executing', 'Mongo');
    hub.emit('governance.approved', 'approved', 'req_1');

    expect(received).toEqual(['agent.executing', 'governance.approved']);
    sub.unsubscribe();
  });
});
