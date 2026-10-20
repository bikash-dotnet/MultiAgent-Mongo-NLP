export enum McpTransport {
  Stdio = 'Stdio',
  Http = 'Http'
}

export enum McpScope {
  Global = 'Global',
  Schema = 'Schema',
  Role = 'Role'
}

export enum McpHealth {
  Healthy = 'Healthy',
  Degraded = 'Degraded',
  Down = 'Down',
  Disabled = 'Disabled'
}

export interface McpEnvKeyStatus {
  name: string;
  configured: boolean;
}

export interface McpServerSummary {
  id: string;
  name: string;
  transport: McpTransport;
  scope: McpScope;
  enabled: boolean;
  health: McpHealth;
  toolCount: number;
  updatedAt: string | null;
}

export interface McpServerDefinition {
  id: string;
  name: string;
  description: string | null;
  transport: McpTransport;
  command: string | null;
  args: string[];
  url: string | null;
  envKeys: McpEnvKeyStatus[];
  enabled: boolean;
  allowedTools: string[];
  readOnly: boolean;
  timeoutMs: number;
  scope: McpScope;
  scopeValue: string | null;
  version: number;
  updatedAt: string | null;
  updatedBy: string | null;
  health: McpHealth;
  lastCheckedAt: string | null;
  lastError: string | null;
}

export interface McpToolDescriptor {
  name: string;
  description: string | null;
  inputSchema: string | null;
  allowed: boolean;
  readOnly: boolean;
}

export interface McpTestResult {
  ok: boolean;
  health: McpHealth;
  latencyMs: number;
  tools: McpToolDescriptor[];
  error: string | null;
}

export interface McpServerRequest {
  id?: string;
  name: string;
  description?: string | null;
  transport: string;
  command?: string | null;
  args: string[];
  url?: string | null;
  envKeys: string[];
  enabled: boolean;
  allowedTools: string[];
  readOnly: boolean;
  timeoutMs: number;
  scope: string;
  scopeValue?: string | null;
  version: number;
}
