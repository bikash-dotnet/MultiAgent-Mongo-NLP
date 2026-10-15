import { TestBed } from '@angular/core/testing';
import { AgentHubConnection, AGENT_HUB_CONNECTION_FACTORY, AgentStreamService } from './agent-stream.service';
import { SessionService } from './session.service';

class FakeHubConnection implements AgentHubConnection {
  handlers = new Map<string, Function>();
  started = false;
  stopped = false;

  constructor(public readonly url: string) {}

  on(methodName: string, newMethod: (...args: any[]) => any): void {
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

describe('AgentStreamService (SignalR)', () => {
  let service: AgentStreamService;
  let connection!: FakeHubConnection;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        { provide: SessionService, useValue: { token: () => 'token-123' } },
        {
          provide: AGENT_HUB_CONNECTION_FACTORY,
          useValue: (url: string) => {
            connection = new FakeHubConnection(url);
            return connection;
          }
        }
      ]
    });
    service = TestBed.inject(AgentStreamService);
  });

  it('connects to SignalR hub endpoint /hubs/agent-stream', () => {
    const sub = service.connect().subscribe();

    expect(connection.url).toBe('/hubs/agent-stream');
    expect(connection.started).toBe(true);
    sub.unsubscribe();
    expect(connection.stopped).toBe(true);
  });

  it('forwards ReceiveAgentEvent from hub to subscriber', () => {
    const received: string[] = [];
    const sub = service.connect().subscribe((event) => received.push(event.event));

    connection.emit('agent.executing', 'executing', 'Mongo');
    connection.emit('governance.approved', 'approved', 'req_1');

    expect(received).toEqual(['agent.executing', 'governance.approved']);
    sub.unsubscribe();
  });
});
