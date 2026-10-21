import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService, ConfirmationService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { CardModule } from 'primeng/card';
import { ToolbarModule } from 'primeng/toolbar';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { SelectModule } from 'primeng/select';
import { MultiSelectModule } from 'primeng/multiselect';
import { ChipModule } from 'primeng/chip';
import { TagModule } from 'primeng/tag';
import { BadgeModule } from 'primeng/badge';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { TabsModule } from 'primeng/tabs';
import { BusinessRulesService } from '../services/business-rules.service';
import { SessionService } from '../services/session.service';
import {
  BusinessRuleResponse,
  BusinessRuleRequest,
  ColumnDefinitionResponse,
  ColumnDefinitionRequest,
  ColumnDataType,
  RuleScope,
  RuleCondition,
  RuleAction
} from '../models/business-rules';

interface RuleDraft extends BusinessRuleRequest {
  id?: string;
  version: number;
  saving: boolean;
}

interface ColumnDraft extends ColumnDefinitionRequest {
  id?: string;
  version: number;
  saving: boolean;
}

const RULE_ACTIONS = [
  { label: 'Set default value', type: 'setDefault', params: [{ key: 'key', label: 'Key' }, { key: 'value', label: 'Value' }] },
  { label: 'Cap rows', type: 'capRows', params: [{ key: 'max', label: 'Max rows' }] },
  { label: 'Mask column', type: 'maskColumn', params: [{ key: 'field', label: 'Field' }] },
  { label: 'Exclude column', type: 'excludeColumn', params: [{ key: 'field', label: 'Field' }] },
  { label: 'Require approval', type: 'requireApproval', params: [{ key: 'field', label: 'Field' }] },
  { label: 'Exempt role', type: 'exemptRole', params: [{ key: 'role', label: 'Role' }] },
  { label: 'Deny operator', type: 'denyOperator', params: [{ key: 'operator', label: 'Operator' }] }
];

const SCOPE_OPTIONS = [
  { label: 'Global', value: 'Global' },
  { label: 'Schema', value: 'Schema' },
  { label: 'Role', value: 'Role' }
];

const DATA_TYPE_OPTIONS = [
  { label: 'String', value: 'String' },
  { label: 'Number', value: 'Number' },
  { label: 'Currency', value: 'Currency' },
  { label: 'Boolean', value: 'Boolean' },
  { label: 'Date', value: 'Date' }
];

@Component({
  selector: 'app-business-rules',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    CardModule,
    ToolbarModule,
    ButtonModule,
    TableModule,
    DialogModule,
    InputTextModule,
    TextareaModule,
    ToggleSwitchModule,
    SelectModule,
    MultiSelectModule,
    ChipModule,
    TagModule,
    BadgeModule,
    ConfirmDialogModule,
    TabsModule
  ],
  providers: [ConfirmationService],
  templateUrl: './business-rules.component.html',
  styleUrl: './business-rules.component.scss'
})
export class BusinessRulesComponent implements OnInit {
  rules: BusinessRuleResponse[] = [];
  columns: ColumnDefinitionResponse[] = [];

  loading = false;
  error: string | null = null;
  canManage = false;

  tabValue = 'rules';

  // Rule dialog
  ruleDialog = false;
  ruleDraft: RuleDraft = this.emptyRuleDraft();
  editingRule = false;
  conditionValuesText = '';

  // Rule tester
  testerRole = '';
  testerFields = '';
  testerResult: any = null;
  testing = false;

  // Column dialog
  columnDialog = false;
  columnDraft: ColumnDraft = this.emptyColumnDraft();
  editingColumn = false;

  constructor(
    private readonly businessRulesService: BusinessRulesService,
    private readonly session: SessionService,
    private readonly message: MessageService,
    private readonly confirmation: ConfirmationService,
    private readonly cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.canManage = this.session.role() === 'Data Owner / Admin';
    this.loadRules();
    this.loadColumns();
  }

  loadRules(): void {
    this.loading = true;
    this.businessRulesService.listRules().subscribe({
      next: (rules) => {
        this.rules = rules;
        this.loading = false;
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = 'Unable to load business rules.';
        this.loading = false;
      }
    });
  }

  loadColumns(): void {
    this.businessRulesService.listColumnDefinitions().subscribe({
      next: (columns) => {
        this.columns = columns.sort((a, b) => a.order - b.order);
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = 'Unable to load column definitions.';
      }
    });
  }

  emptyRuleDraft(): RuleDraft {
    return {
      name: '',
      description: '',
      scope: RuleScope.Global,
      scopeValue: '',
      priority: 100,
      enabled: true,
      condition: { field: '', operator: '', values: [], combine: 'all' },
      action: { type: '', parameters: {} },
      version: 0,
      saving: false
    };
  }

  emptyColumnDraft(): ColumnDraft {
    return {
      field: '',
      label: '',
      dataType: ColumnDataType.String,
      format: '',
      order: 0,
      visibleByDefault: true,
      exportable: true,
      sensitive: false,
      width: '',
      version: 0,
      saving: false
    };
  }

  addRule(): void {
    this.editingRule = false;
    this.ruleDraft = this.emptyRuleDraft();
    this.conditionValuesText = '';
    this.ruleDialog = true;
  }

  editRule(rule: BusinessRuleResponse): void {
    this.editingRule = true;
    this.conditionValuesText = rule.condition.values.join(', ');
    this.ruleDraft = {
      id: rule.id,
      name: rule.name,
      description: rule.description ?? '',
      scope: rule.scope,
      scopeValue: rule.scopeValue ?? '',
      priority: rule.priority,
      enabled: rule.enabled,
      condition: { ...rule.condition },
      action: { ...rule.action, parameters: { ...rule.action.parameters } },
      version: rule.version,
      saving: false
    };
    this.ruleDialog = true;
  }

  saveRule(): void {
    if (!this.ruleDraft.name.trim()) {
      this.message.add({ severity: 'error', summary: 'Validation', detail: 'Rule name is required.' });
      return;
    }
    if (!this.ruleDraft.action.type.trim()) {
      this.message.add({ severity: 'error', summary: 'Validation', detail: 'Action type is required.' });
      return;
    }

    this.ruleDraft.saving = true;
    const request: BusinessRuleRequest = {
      name: this.ruleDraft.name,
      description: this.ruleDraft.description,
      scope: this.ruleDraft.scope,
      scopeValue: this.ruleDraft.scopeValue || undefined,
      priority: this.ruleDraft.priority,
      enabled: this.ruleDraft.enabled,
      condition: {
        field: this.ruleDraft.condition.field,
        operator: this.ruleDraft.condition.operator,
        values: this.conditionValuesText.split(',').map((v) => v.trim()).filter(Boolean),
        combine: this.ruleDraft.condition.combine || 'all'
      },
      action: {
        type: this.ruleDraft.action.type,
        parameters: { ...this.ruleDraft.action.parameters }
      }
    };

    const obs = this.editingRule && this.ruleDraft.id
      ? this.businessRulesService.updateRule(this.ruleDraft.id, request, this.ruleDraft.version)
      : this.businessRulesService.createRule(request);

    obs.subscribe({
      next: (saved) => {
        this.rules = this.editingRule && this.ruleDraft.id
          ? this.rules.map((r) => r.id === saved.id ? saved : r)
          : [...this.rules, saved];
        this.rules.sort((a, b) => a.priority - b.priority);
        this.ruleDialog = false;
        this.message.add({
          severity: 'success',
          summary: 'Saved',
          detail: `Business rule "${saved.name}" ${this.editingRule ? 'updated' : 'created'}.`
        });
      },
      error: (err) => {
        this.ruleDraft.saving = false;
        const serverError = err.error?.error ?? 'Unable to save rule.';
        if (err.status === 409) {
          this.message.add({ severity: 'error', summary: 'Conflict', detail: serverError });
        } else {
          this.message.add({ severity: 'error', summary: 'Save failed', detail: serverError });
        }
      }
    });
  }

  toggleRule(rule: BusinessRuleResponse): void {
    rule.enabled = !rule.enabled;
    const request: BusinessRuleRequest = {
      name: rule.name,
      description: rule.description,
      scope: rule.scope,
      scopeValue: rule.scopeValue,
      priority: rule.priority,
      enabled: rule.enabled,
      condition: rule.condition,
      action: rule.action
    };
    this.businessRulesService.updateRule(rule.id, request, rule.version).subscribe({
      next: (saved) => {
        rule.version = saved.version;
      },
      error: () => {
        rule.enabled = !rule.enabled;
        this.message.add({ severity: 'error', summary: 'Toggle failed', detail: 'Unable to update rule.' });
      }
    });
  }

  deleteRule(rule: BusinessRuleResponse): void {
    this.confirmation.confirm({
      message: `Delete business rule "${rule.name}"?`,
      header: 'Confirm delete',
      icon: 'pi pi-trash',
      accept: () => {
        this.businessRulesService.deleteRule(rule.id).subscribe({
          next: () => {
            this.rules = this.rules.filter((r) => r.id !== rule.id);
            this.message.add({ severity: 'info', summary: 'Deleted', detail: `Rule "${rule.name}" removed.` });
          },
          error: () => {
            this.message.add({ severity: 'error', summary: 'Delete failed', detail: 'Unable to delete rule.' });
          }
        });
      }
    });
  }

  duplicateRule(rule: BusinessRuleResponse): void {
    this.editingRule = false;
    this.ruleDraft = {
      name: `Copy of ${rule.name}`,
      description: rule.description ?? '',
      scope: rule.scope,
      scopeValue: rule.scopeValue ?? '',
      priority: rule.priority + 1,
      enabled: false,
      condition: { ...rule.condition },
      action: { ...rule.action, parameters: { ...rule.action.parameters } },
      version: 0,
      saving: false
    };
    this.ruleDialog = true;
  }

  testRules(): void {
    if (!this.testerRole.trim()) {
      this.message.add({ severity: 'error', summary: 'Validation', detail: 'Requester role is required.' });
      return;
    }
    this.testing = true;
    this.testerResult = null;

    const fields = this.testerFields.split(',').map((f) => f.trim()).filter(Boolean);
    const requesterRole = this.testerRole;

    const enabledRules = this.rules.filter((r) => r.enabled);
    const applicable = enabledRules.filter((r) => {
      if (r.scope === RuleScope.Global) return true;
      if (r.scope === RuleScope.Role) return r.scopeValue === requesterRole;
      return false;
    })
    .sort((a, b) => a.priority - b.priority);

    const appliedRuleIds: string[] = [];
    const effects: any[] = [];
    let capRows: number | null = null;
    const setDefaults: { [key: string]: string } = {};
    const requireApprovalFields: string[] = [];
    const exemptRoles: string[] = [];
    const deniedOperators: string[] = [];
    const maskedColumns: string[] = [];
    const excludedColumns: string[] = [];

    for (const rule of applicable) {
      appliedRuleIds.push(rule.id);
      effects.push({
        ruleId: rule.id,
        actionType: rule.action.type,
        parameters: rule.action.parameters
      });

      switch (rule.action.type) {
        case 'capRows':
          const max = parseInt(rule.action.parameters['max'] || '', 10);
          if (!isNaN(max)) capRows = max;
          break;
        case 'setDefault':
          setDefaults[rule.action.parameters['key'] || ''] = rule.action.parameters['value'] || '';
          break;
        case 'requireApproval':
          requireApprovalFields.push(rule.action.parameters['field'] || '');
          break;
        case 'exemptRole':
          exemptRoles.push(rule.action.parameters['role'] || '');
          break;
        case 'denyOperator':
          deniedOperators.push(rule.action.parameters['operator'] || '');
          break;
        case 'maskColumn':
          maskedColumns.push(rule.action.parameters['field'] || '');
          break;
        case 'excludeColumn':
          excludedColumns.push(rule.action.parameters['field'] || '');
          break;
      }
    }

    this.testerResult = {
      requesterRole,
      fields,
      appliedRuleIds,
      effects,
      capRows,
      setDefaults,
      requireApprovalFields,
      exemptRoles,
      deniedOperators,
      maskedColumns,
      excludedColumns
    };
    this.testing = false;
  }

  // Column definitions
  addColumn(): void {
    this.editingColumn = false;
    this.columnDraft = this.emptyColumnDraft();
    this.columnDraft.order = this.columns.length;
    this.columnDialog = true;
  }

  editColumn(column: ColumnDefinitionResponse): void {
    this.editingColumn = true;
    this.columnDraft = {
      id: column.id,
      field: column.field,
      label: column.label,
      dataType: column.dataType,
      format: column.format ?? '',
      order: column.order,
      visibleByDefault: column.visibleByDefault,
      exportable: column.exportable,
      sensitive: column.sensitive,
      width: column.width ?? '',
      version: column.version,
      saving: false
    };
    this.columnDialog = true;
  }

  saveColumn(): void {
    if (!this.columnDraft.field.trim()) {
      this.message.add({ severity: 'error', summary: 'Validation', detail: 'Field is required.' });
      return;
    }
    if (!this.columnDraft.label.trim()) {
      this.message.add({ severity: 'error', summary: 'Validation', detail: 'Label is required.' });
      return;
    }

    this.columnDraft.saving = true;
    const request: ColumnDefinitionRequest = {
      field: this.columnDraft.field,
      label: this.columnDraft.label,
      dataType: this.columnDraft.dataType,
      format: this.columnDraft.format || undefined,
      order: this.columnDraft.order,
      visibleByDefault: this.columnDraft.visibleByDefault,
      exportable: this.columnDraft.exportable,
      sensitive: this.columnDraft.sensitive,
      width: this.columnDraft.width || undefined
    };

    const obs = this.editingColumn && this.columnDraft.id
      ? this.businessRulesService.updateColumnDefinition(this.columnDraft.id, request, this.columnDraft.version)
      : this.businessRulesService.createColumnDefinition(request);

    obs.subscribe({
      next: (saved) => {
        this.columns = this.editingColumn && this.columnDraft.id
          ? this.columns.map((c) => c.id === saved.id ? saved : c)
          : [...this.columns, saved];
        this.columns = this.columns.sort((a, b) => a.order - b.order);
        this.columnDialog = false;
        this.message.add({
          severity: 'success',
          summary: 'Saved',
          detail: `Column definition "${saved.field}" ${this.editingColumn ? 'updated' : 'created'}.`
        });
      },
      error: (err) => {
        this.columnDraft.saving = false;
        const serverError = err.error?.error ?? 'Unable to save column definition.';
        if (err.status === 409) {
          this.message.add({ severity: 'error', summary: 'Conflict', detail: serverError });
        } else {
          this.message.add({ severity: 'error', summary: 'Save failed', detail: serverError });
        }
      }
    });
  }

  deleteColumn(column: ColumnDefinitionResponse): void {
    this.confirmation.confirm({
      message: `Delete column definition "${column.field}"?`,
      header: 'Confirm delete',
      icon: 'pi pi-trash',
      accept: () => {
        this.businessRulesService.deleteColumnDefinition(column.id).subscribe({
          next: () => {
            this.columns = this.columns.filter((c) => c.id !== column.id);
            this.message.add({ severity: 'info', summary: 'Deleted', detail: `Column "${column.field}" removed.` });
          },
          error: () => {
            this.message.add({ severity: 'error', summary: 'Delete failed', detail: 'Unable to delete column definition.' });
          }
        });
      }
    });
  }

  reorderColumns(up: boolean, index: number): void {
    if (up && index > 0) {
      [this.columns[index - 1], this.columns[index]] = [this.columns[index], this.columns[index - 1]];
    } else if (!up && index < this.columns.length - 1) {
      [this.columns[index + 1], this.columns[index]] = [this.columns[index], this.columns[index + 1]];
    }
    this.columns = this.columns.map((c, i) => ({ ...c, order: i }));
    this.persistColumnOrder();
  }

  private persistColumnOrder(): void {
    this.businessRulesService.reorderColumnDefinitions({ ids: this.columns.map((c) => c.id) }).subscribe();
  }

  scopeBadge(scope: RuleScope): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | null | undefined {
    switch (scope) {
      case RuleScope.Global: return 'info';
      case RuleScope.Schema: return 'secondary';
      case RuleScope.Role: return 'warn';
      default: return 'info';
    }
  }

  dataTypeBadge(type: ColumnDataType): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | null | undefined {
    switch (type) {
      case ColumnDataType.String: return 'info';
      case ColumnDataType.Number: return 'info';
      case ColumnDataType.Currency: return 'success';
      case ColumnDataType.Boolean: return 'secondary';
      case ColumnDataType.Date: return 'warn';
      default: return 'info';
    }
  }

  protected readonly RuleScope = RuleScope;
  protected readonly ColumnDataType = ColumnDataType;
  protected readonly RuleActions = RULE_ACTIONS;
  protected readonly ScopeOptions = SCOPE_OPTIONS;
  protected readonly DataTypeOptions = DATA_TYPE_OPTIONS;
  protected readonly Object = Object;

  actionParams(): { key: string; label: string }[] {
    const action = RULE_ACTIONS.find((a) => a.type === this.ruleDraft.action.type);
    return action?.params ?? [];
  }

  formatParams(params: { [key: string]: string }): string {
    return Object.entries(params)
      .filter(([, v]) => v)
      .map(([k, v]) => `${k}=${v}`)
      .join(', ');
  }
}
