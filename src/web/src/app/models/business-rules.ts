export enum RuleScope {
  Global = 'Global',
  Schema = 'Schema',
  Role = 'Role'
}

export interface RuleCondition {
  field: string;
  operator: string;
  values: string[];
  combine?: string;
}

export interface RuleAction {
  type: string;
  parameters: { [key: string]: string };
}

export interface BusinessRuleResponse {
  id: string;
  name: string;
  description?: string | null;
  scope: RuleScope;
  scopeValue?: string | null;
  priority: number;
  enabled: boolean;
  condition: RuleCondition;
  action: RuleAction;
  version: number;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface BusinessRuleRequest {
  name: string;
  description?: string | null;
  scope: RuleScope;
  scopeValue?: string | null;
  priority: number;
  enabled: boolean;
  condition: RuleCondition;
  action: RuleAction;
}

export enum ColumnDataType {
  String = 'String',
  Number = 'Number',
  Currency = 'Currency',
  Boolean = 'Boolean',
  Date = 'Date'
}

export interface ColumnDefinitionResponse {
  id: string;
  field: string;
  label: string;
  dataType: ColumnDataType;
  format?: string | null;
  order: number;
  visibleByDefault: boolean;
  exportable: boolean;
  sensitive: boolean;
  width?: string | null;
  version: number;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface ColumnDefinitionRequest {
  field: string;
  label: string;
  dataType: ColumnDataType;
  format?: string | null;
  order: number;
  visibleByDefault: boolean;
  exportable: boolean;
  sensitive: boolean;
  width?: string | null;
}

export interface ColumnDefinitionReorderRequest {
  ids: string[];
}

export interface RuleTestRequest {
  requesterRole: string;
  fields: string[];
}

export interface RuleTestResult {
  appliedRuleIds: string[];
  effects: Array<{
    ruleId: string;
    actionType: string;
    parameters: { [key: string]: string };
  }>;
  deniedOperators: string[];
  maskedColumns: string[];
  excludedColumns: string[];
  capRows?: number | null;
  setDefaults: { [key: string]: string };
  requireApprovalFields: string[];
  exemptRoles: string[];
}
