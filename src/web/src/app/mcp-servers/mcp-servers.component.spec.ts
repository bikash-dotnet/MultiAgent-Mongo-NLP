import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { McpServersComponent } from './mcp-servers.component';
import { McpServersService } from '../services/mcp-servers.service';
import { SessionService } from '../services/session.service';
import {
  McpHealth,
  McpServerRequest,
  McpServerSummary,
  McpTestResult,
  McpToolDescriptor,
  McpTransport,
  McpScope
} from '../models/mcp-servers';

const summaries: McpServerSummary[] = [
  {
    id: 'mcp_mongo_ai_query',
    name: 'mongo-ai-query',
    transport: McpTransport.Stdio,
    scope: McpScope.Global,
    enabled: false,
    health: McpHealth.Disabled,
    toolCount: 4,
    updatedAt: '2026-10-19T12:00:00Z'
  },
  {
    id: 'mcp_custom',
    name: 'custom-server',
    transport: McpTransport.Http,
    scope: McpScope.Global,
    enabled: true,
    health: McpHealth.Healthy,
    toolCount: 2,
    updatedAt: '2026-10-19T12:00:00Z'
  }
];

const detail = {
  id: 'mcp_custom',
  name: 'custom-server',
  description: 'A custom MCP server',
  transport: McpTransport.Http,
  command: null,
  args: [],
  url: 'http://localhost:3100/mcp',
  envKeys: [
    { name: 'API_KEY', configured: true },
    { name: 'MONGO_URI', configured: false }
  ],
  enabled: true,
  allowedTools: ['get_schema', 'ask'],
  readOnly: true,
  timeoutMs: 5000,
  scope: McpScope.Global,
  scopeValue: null,
  version: 1,
  updatedAt: '2026-10-19T12:00:00Z',
  updatedBy: 'admin',
  health: McpHealth.Healthy,
  lastCheckedAt: '2026-10-19T12:05:00Z',
  lastError: null
};

const testResult: McpTestResult = {
  ok: true,
  health: McpHealth.Healthy,
  latencyMs: 45,
  tools: [
    { name: 'get_schema', description: 'schema', inputSchema: '{}', allowed: true, readOnly: true },
    { name: 'ask', description: 'ask', inputSchema: '{}', allowed: false, readOnly: true }
  ],
  error: null
};

function setup(role = 'Data Owner / Admin', list: McpServerSummary[] = summaries) {
  let created: McpServerRequest[] = [];
  let updated: { id: string; request: McpServerRequest; version: number }[] = [];
  let deleted: string[] = [];
  let enabled: string[] = [];
  let disabled: string[] = [];
  let tested: string[] = [];
  let listedTools: string[] = [];

  const service = {
    listServers: () => of(list),
    getServer: () => of(detail),
    createServer: (request: McpServerRequest) => {
      created.push(request);
      return of({ ...detail, id: 'mcp_new', name: request.name });
    },
    updateServer: (id: string, request: McpServerRequest, version: number) => {
      updated.push({ id, request, version });
      return of({ ...detail, id, name: request.name, version });
    },
    deleteServer: (id: string) => {
      deleted.push(id);
      return of(void 0);
    },
    enableServer: (id: string) => {
      enabled.push(id);
      return of({ ...detail, id, enabled: true });
    },
    disableServer: (id: string) => {
      disabled.push(id);
      return of({ ...detail, id, enabled: false });
    },
    testServer: (id: string) => {
      tested.push(id);
      return of(testResult);
    },
    listTools: (id: string) => {
      listedTools.push(id);
      return of(testResult.tools);
    }
  };

  TestBed.configureTestingModule({
    imports: [McpServersComponent],
    providers: [
      { provide: McpServersService, useValue: service },
      { provide: SessionService, useValue: { role: () => role } },
      {
        provide: ConfirmationService,
        useValue: {
          confirm: (options: { accept?: () => void }) => options.accept?.(),
          requireConfirmation$: of(null)
        }
      },
      { provide: MessageService, useValue: { add: () => {} } }
    ]
  });

  const fixture = TestBed.createComponent(McpServersComponent);
  const component = fixture.componentInstance;
  return { fixture, component, service, created, updated, deleted, enabled, disabled, tested, listedTools };
}

describe('McpServersComponent', () => {
  it('renders server list with health badges', () => {
    const { component, fixture } = setup();
    component.ngOnInit();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('mongo-ai-query');
    expect(text).toContain('custom-server');
    expect(text).toContain('Disabled');
    expect(text).toContain('Healthy');
  });

  it('opens edit dialog and shows env keys without secret values', () => {
    const { component, fixture } = setup();
    const summary = summaries[1];
    component.startEdit(summary);
    fixture.detectChanges();

    expect(component.envKeyStatuses).toEqual([
      { name: 'API_KEY', configured: true },
      { name: 'MONGO_URI', configured: false }
    ]);
    expect(component.serverForm.envKeys).toContain('API_KEY');
    expect(component.serverForm.envKeys).toContain('MONGO_URI');

    const serialized = JSON.stringify(component.serverForm) + JSON.stringify(component.envKeyStatuses);
    expect(serialized).not.toContain('sk-');
    expect(serialized).not.toContain('mongodb://');
  });

  it('creates a new server', () => {
    const { component, fixture, created } = setup();
    component.startCreate();
    fixture.detectChanges();

    component.serverForm.name = 'new-server';
    component.serverForm.readOnly = true;
    component.saveServer();
    fixture.detectChanges();

    expect(created.length).toBe(1);
    expect(created[0].name).toBe('new-server');
    expect(created[0].readOnly).toBe(true);
  });

  it('toggles read-only tools on the allow-list and rejects write tools', () => {
    const { component } = setup();
    component.testServer(summaries[1]);

    const readTool = testResult.tools[0];
    expect(component.isToolAllowed(readTool.name)).toBe(true);

    component.setToolAllowed(readTool, false);
    expect(component.isToolAllowed(readTool.name)).toBe(false);

    component.setToolAllowed(readTool, true);
    expect(component.isToolAllowed(readTool.name)).toBe(true);

    const writeTool: McpToolDescriptor = {
      name: 'delete_all',
      description: 'destructive',
      inputSchema: '{}',
      allowed: false,
      readOnly: false
    };
    component.setToolAllowed(writeTool, true);
    expect(component.isToolAllowed(writeTool.name)).toBe(false);
  });

  it('updates existing server with version', () => {
    const { component, fixture, updated } = setup();
    const summary = summaries[1];
    component.startEdit(summary);
    fixture.detectChanges();

    component.serverForm.name = 'updated-server';
    component.saveServer();
    fixture.detectChanges();

    expect(updated.length).toBe(1);
    expect(updated[0].id).toBe('mcp_custom');
    expect(updated[0].version).toBe(1);
    expect(updated[0].request.name).toBe('updated-server');
  });

  it('deletes server with confirmation', () => {
    const { component, fixture, deleted } = setup();
    const summary = summaries[0];
    component.deleteServer(summary);
    fixture.detectChanges();

    expect(deleted.length).toBe(1);
    expect(deleted[0]).toBe('mcp_mongo_ai_query');
  });

  it('enables and disables server', () => {
    const { component, fixture, enabled, disabled } = setup();
    const disabledSummary = summaries[0];
    const enabledSummary = summaries[1];

    component.enableServer(disabledSummary);
    fixture.detectChanges();
    expect(enabled.length).toBe(1);
    expect(enabled[0]).toBe('mcp_mongo_ai_query');

    component.disableServer(enabledSummary);
    fixture.detectChanges();
    expect(disabled.length).toBe(1);
    expect(disabled[0]).toBe('mcp_custom');
  });

  it('tests connection and shows tools', () => {
    const { component, fixture, tested, listedTools } = setup();
    const summary = summaries[1];
    component.testServer(summary);
    fixture.detectChanges();

    expect(tested.length).toBe(1);
    expect(tested[0]).toBe('mcp_custom');
    expect(component.testResult?.latencyMs).toBe(45);
    expect(component.testResult?.tools.map((t) => t.name)).toEqual(['get_schema', 'ask']);
    expect(component.testDialogVisible).toBe(true);

    component.openTools(summary);
    fixture.detectChanges();
    expect(listedTools.length).toBe(1);
    expect(component.testResult?.tools.map((t) => t.name)).toEqual(['get_schema', 'ask']);
  });

  it('renders the empty state when the API returns no servers', () => {
    const { component, fixture } = setup('Data Owner / Admin', []);
    component.ngOnInit();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No MCP servers defined.');
  });

  it('hides mutating actions for non-admin users', () => {
    const { component, fixture } = setup('Analyst');
    component.ngOnInit();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).not.toContain('Create server');
  });
});
