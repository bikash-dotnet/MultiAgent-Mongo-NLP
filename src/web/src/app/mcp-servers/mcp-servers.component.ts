import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { McpServersService } from '../services/mcp-servers.service';
import { SessionService } from '../services/session.service';
import {
  McpHealth,
  McpServerDefinition,
  McpServerRequest,
  McpServerSummary,
  McpToolDescriptor,
  McpTransport,
  McpScope
} from '../models/mcp-servers';

type EditorMode = 'create' | 'edit';

@Component({
  selector: 'app-mcp-servers',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    CheckboxModule,
    ConfirmDialogModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TableModule,
    TagModule,
    TextareaModule,
    ToggleSwitchModule
  ],
  templateUrl: './mcp-servers.component.html',
  styleUrl: './mcp-servers.component.scss'
})
export class McpServersComponent implements OnInit {
  servers: McpServerSummary[] = [];
  loading = false;
  error: string | null = null;

  editorVisible = false;
  editorMode: EditorMode = 'create';
  selected: McpServerDefinition | null = null;
  saving = false;

  serverForm = {
    name: '',
    description: '',
    transport: McpTransport.Stdio,
    command: '',
    args: '',
    url: '',
    envKeys: '',
    enabled: true,
    allowedTools: [] as string[],
    readOnly: true,
    timeoutMs: 5000,
    scope: McpScope.Global,
    scopeValue: ''
  };

  testResult: McpTestResult | null = null;
  testDialogVisible = false;
  testingServerId: string | null = null;
  testing = false;
  envKeyStatuses: { name: string; configured: boolean }[] = [];
  discoveredTools: string[] = [];

  transportOptions = [
    { label: 'Stdio (process)', value: McpTransport.Stdio },
    { label: 'HTTP (Streamable HTTP)', value: McpTransport.Http }
  ];

  scopeOptions = [
    { label: 'Global (all chats)', value: McpScope.Global },
    { label: 'Schema (single database)', value: McpScope.Schema },
    { label: 'Role (specific roles)', value: McpScope.Role }
  ];

  healthSeverity(health: McpHealth): 'success' | 'warn' | 'danger' | 'secondary' | 'info' {
    switch (health) {
      case McpHealth.Healthy:
        return 'success';
      case McpHealth.Degraded:
        return 'warn';
      case McpHealth.Down:
        return 'danger';
      case McpHealth.Disabled:
        return 'secondary';
      default:
        return 'info';
    }
  }

  constructor(
    private readonly mcpServersService: McpServersService,
    private readonly session: SessionService,
    private readonly confirmation: ConfirmationService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.session.role() === 'Data Owner / Admin';
  }

  get editorTitle(): string {
    return this.editorMode === 'create' ? 'Create MCP server' : 'Edit MCP server';
  }

  get allowedToolOptions(): string[] {
    return Array.from(new Set([...this.discoveredTools, ...this.serverForm.allowedTools])).sort();
  }

  isToolAllowed(name: string): boolean {
    return this.serverForm.allowedTools.includes(name);
  }

  setToolAllowed(tool: McpToolDescriptor, allowed: boolean): void {
    if (!tool.readOnly || !this.canManage) {
      return;
    }

    const tools = new Set(this.serverForm.allowedTools);
    if (allowed) {
      tools.add(tool.name);
    } else {
      tools.delete(tool.name);
    }
    this.serverForm.allowedTools = Array.from(tools);
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.mcpServersService.listServers().subscribe({
      next: (response) => {
        this.servers = response.servers;
        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error = 'Unable to load MCP servers.';
        this.loading = false;
      }
    });
  }

  startCreate(): void {
    this.editorMode = 'create';
    this.selected = null;
    this.serverForm = {
      name: '',
      description: '',
      transport: McpTransport.Stdio,
      command: '',
      args: '',
      url: '',
      envKeys: '',
      enabled: true,
      allowedTools: [],
      readOnly: true,
      timeoutMs: 5000,
      scope: McpScope.Global,
      scopeValue: ''
    };
    this.envKeyStatuses = [];
    this.error = null;
    this.editorVisible = true;
  }

  closeEditor(): void {
    this.editorVisible = false;
    this.saving = false;
    this.error = null;
    this.envKeyStatuses = [];
  }

  startEdit(summary: McpServerSummary): void {
    this.mcpServersService.getServer(summary.id).subscribe({
      next: (response) => {
        this.applyDefinition(response.server);
        this.error = null;
        this.editorVisible = true;
      },
      error: () => (this.error = 'Unable to load MCP server details.')
    });
  }

  private applyDefinition(def: McpServerDefinition): void {
    this.editorMode = 'edit';
    this.selected = def;
    this.serverForm = {
      name: def.name,
      description: def.description ?? '',
      transport: def.transport,
      command: def.command ?? '',
      args: def.args.join(' '),
      url: def.url ?? '',
      envKeys: def.envKeys.map((e) => e.name).join('\n'),
      enabled: def.enabled,
      allowedTools: [...def.allowedTools],
      readOnly: def.readOnly,
      timeoutMs: def.timeoutMs,
      scope: def.scope,
      scopeValue: def.scopeValue ?? ''
    };
    this.envKeyStatuses = def.envKeys;
  }

  saveServer(): void {
    this.error = null;

    if (!this.serverForm.name.trim()) {
      this.error = 'Server name is required.';
      return;
    }

    const args = this.serverForm.args.split(/\s+/).filter((a) => a.trim().length > 0);
    const envKeys = this.serverForm.envKeys.split('\n').map((e) => e.trim()).filter((e) => e.length > 0);
    const allowedTools = [...this.serverForm.allowedTools];

    const request: McpServerRequest = {
      name: this.serverForm.name.trim(),
      description: this.serverForm.description.trim() || undefined,
      transport: this.serverForm.transport,
      command: this.serverForm.transport === McpTransport.Stdio ? this.serverForm.command.trim() || undefined : undefined,
      args,
      url: this.serverForm.transport === McpTransport.Http ? this.serverForm.url.trim() || undefined : undefined,
      envKeys,
      enabled: this.serverForm.enabled,
      allowedTools,
      readOnly: this.serverForm.readOnly,
      timeoutMs: this.serverForm.timeoutMs,
      scope: this.serverForm.scope,
      scopeValue: this.serverForm.scopeValue.trim() || undefined,
      version: this.selected ? this.selected.version : 1
    };

    this.saving = true;

    if (this.editorMode === 'create') {
      this.mcpServersService.createServer(request).subscribe({
        next: () => {
          this.saving = false;
          this.editorVisible = false;
          this.load();
        },
        error: (err) => {
          this.saving = false;
          this.error = err?.error?.error ?? 'Unable to create MCP server.';
        }
      });
      return;
    }

    if (!this.selected) {
      this.saving = false;
      return;
    }

    this.mcpServersService.updateServer(this.selected.id, request, this.selected.version).subscribe({
      next: () => {
        this.saving = false;
        this.editorVisible = false;
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.error = err?.error?.error ?? 'Unable to update MCP server.';
      }
    });
  }

  deleteServer(summary: McpServerSummary): void {
    this.confirmation.confirm({
      header: 'Delete MCP server',
      message: `Delete "${summary.name}"? This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.mcpServersService.deleteServer(summary.id).subscribe({
          next: () => this.load(),
          error: () => (this.error = 'Unable to delete MCP server.')
        });
      }
    });
  }

  enableServer(summary: McpServerSummary): void {
    this.mcpServersService.enableServer(summary.id).subscribe({
      next: () => this.load(),
      error: () => (this.error = 'Unable to enable MCP server.')
    });
  }

  disableServer(summary: McpServerSummary): void {
    this.mcpServersService.disableServer(summary.id).subscribe({
      next: () => this.load(),
      error: () => (this.error = 'Unable to disable MCP server.')
    });
  }

  testServer(summary: McpServerSummary): void {
    this.beginTest(summary.id);
    this.mcpServersService.getServer(summary.id).subscribe({
      next: (response) => {
        this.applyDefinition(response.server);
        this.runTest(summary.id);
      },
      error: () => {
        this.testing = false;
        this.error = 'Unable to load MCP server details.';
      }
    });
  }

  openTools(summary: McpServerSummary): void {
    this.beginTest(summary.id);
    this.mcpServersService.getServer(summary.id).subscribe({
      next: (response) => {
        this.applyDefinition(response.server);
        this.mcpServersService.listTools(summary.id).subscribe({
          next: (tools) => {
            this.testResult = { ok: true, health: McpHealth.Healthy, latencyMs: 0, tools, error: null };
            this.discoveredTools = tools.map((t) => t.name);
            this.testing = false;
            this.load();
          },
          error: () => {
            this.testResult = null;
            this.testing = false;
            this.error = 'Unable to load tools.';
          }
        });
      },
      error: () => {
        this.testing = false;
        this.error = 'Unable to load MCP server details.';
      }
    });
  }

  private beginTest(id: string): void {
    this.testing = true;
    this.testResult = null;
    this.testDialogVisible = true;
    this.testingServerId = id;
    this.discoveredTools = [];
    this.error = null;
  }

  private runTest(id: string): void {
    this.mcpServersService.testServer(id).subscribe({
      next: (result) => {
        this.testResult = result;
        this.discoveredTools = result.tools?.map((t) => t.name) ?? [];
        this.testing = false;
        this.load();
      },
      error: () => {
        this.testResult = null;
        this.testing = false;
        this.error = 'Connection test failed.';
      }
    });
  }

  closeTest(): void {
    this.testResult = null;
    this.testDialogVisible = false;
    this.testingServerId = null;
    this.discoveredTools = [];
    this.error = null;
  }

  envKeyLabel(status: { name: string; configured: boolean }): string {
    return `${status.name} ${status.configured ? '(configured)' : '(not configured)'}`;
  }
}

interface McpTestResult {
  ok: boolean;
  health: McpHealth;
  latencyMs: number;
  tools: McpToolDescriptor[];
  error: string | null;
}
