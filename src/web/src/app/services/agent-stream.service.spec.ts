import { TestBed } from '@angular/core/testing';
import { AgentStreamService } from './agent-stream.service';
import { SessionService } from './session.service';

type Handler = (event: MessageEvent) => void;

class FakeEventSource {
  static instances: FakeEventSource[] = [];
  private readonly listeners = new Map<string, Set<Handler>>();
  onerror: ((event: Event) => void) | null = null;
  closed = false;

  constructor(public readonly url: string) {
    FakeEventSource.instances.push(this);
  }

  addEventListener(name: string, handler: EventListener): void {
    const set = this.listeners.get(name) ?? new Set<Handler>();
    set.add(handler as Handler);
    this.listeners.set(name, set);
  }

  removeEventListener(name: string, handler: EventListener): void {
    this.listeners.get(name)?.delete(handler as Handler);
  }

  close(): void {
    this.closed = true;
  }

  listens(name: string): boolean {
    return (this.listeners.get(name)?.size ?? 0) > 0;
  }

  emit(name: string, data: string): void {
    for (const handler of this.listeners.get(name) ?? []) {
      handler(new MessageEvent(name, { data }));
    }
  }
}

const gatewayEventNames = [
  'agent.idle',
  'agent.started',
  'agent.executing',
  'agent.clarifying',
  'agent.completed',
  'governance.paused',
  'governance.exempted',
  'governance.request_enriched',
  'governance.manager_notified',
  'governance.approved',
  'governance.rejected',
  'report.ready',
  'report.email_simulated',
  'conversation.started',
  'conversation.resumed',
  'conversation.completed'
];

describe('AgentStreamService', () => {
  let service: AgentStreamService;

  beforeEach(() => {
    FakeEventSource.instances = [];
    (globalThis as unknown as { EventSource: unknown }).EventSource = FakeEventSource;
    sessionStorage.setItem('access_token', 'token-123');
    TestBed.configureTestingModule({
      providers: [{ provide: SessionService, useValue: { token: () => 'token-123' } }]
    });
    service = TestBed.inject(AgentStreamService);
  });

  it('subscribes to every event the gateway publishes', () => {
    const sub = service.connect().subscribe();
    const source = FakeEventSource.instances[0]!;

    for (const name of gatewayEventNames) {
      expect(source.listens(name)).toBe(true);
    }

    sub.unsubscribe();
    expect(source.closed).toBe(true);
  });

  it('forwards each gateway event to the subscriber', () => {
    const received: string[] = [];
    const sub = service.connect().subscribe((event) => received.push(event.event));
    const source = FakeEventSource.instances[0]!;

    source.emit('agent.executing', JSON.stringify({ status: 'executing', detail: 'Mongo' }));
    source.emit('governance.approved', JSON.stringify({ status: 'approved', detail: 'req_1' }));

    expect(received).toEqual(['agent.executing', 'governance.approved']);
    sub.unsubscribe();
  });

  it('carries the access token on the stream url', () => {
    const sub = service.connect().subscribe();

    expect(FakeEventSource.instances[0]!.url).toContain('access_token=token-123');
    sub.unsubscribe();
  });
});
