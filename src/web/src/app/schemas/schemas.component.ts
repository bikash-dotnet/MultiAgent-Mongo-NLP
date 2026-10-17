import { Component, OnInit } from '@angular/core';
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
import { SchemasService } from '../services/schemas.service';
import { SessionService } from '../services/session.service';
import {
  CreateSchemaRequest,
  SchemaDataSource,
  SchemaDefinition,
  SchemaField,
  SchemaFieldRequest,
  SchemaFieldType,
  SchemaSummary,
  UpdateSchemaRequest
} from '../models/schemas';

type EditorMode = 'create' | 'edit';

@Component({
  selector: 'app-schemas',
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
    TextareaModule
  ],
  templateUrl: './schemas.component.html',
  styleUrl: './schemas.component.scss'
})
export class SchemasComponent implements OnInit {
  schemas: SchemaSummary[] = [];
  loading = false;
  error: string | null = null;

  editorVisible = false;
  editorMode: EditorMode = 'create';
  selected: SchemaDefinition | null = null;
  saving = false;

  schemaForm = {
    name: '',
    dataSource: SchemaDataSource.Mongo,
    database: 'sample_airbnb',
    collection: 'listingsAndReviews',
    description: '',
    isActiveSource: false
  };

  fields: SchemaField[] = [];

  fieldDialogVisible = false;
  fieldEditIndex: number | null = null;
  fieldError = '';
  fieldDraft: SchemaField = this.emptyField();

  importVisible = false;
  importing = false;
  importError = '';
  importText = '';
  importJson = '';
  importPreview: SchemaField[] = [];

  dataSourceOptions = [
    { label: 'MongoDB', value: SchemaDataSource.Mongo },
    { label: 'Enterprise Core REST', value: SchemaDataSource.EnterpriseCoreREST },
    { label: 'Elasticsearch', value: SchemaDataSource.Elasticsearch }
  ];

  fieldTypeOptions = [
    { label: 'String', value: SchemaFieldType.String },
    { label: 'Number', value: SchemaFieldType.Number },
    { label: 'Boolean', value: SchemaFieldType.Boolean },
    { label: 'Date', value: SchemaFieldType.Date },
    { label: 'Object', value: SchemaFieldType.Object },
    { label: 'Array', value: SchemaFieldType.Array }
  ];

  roleOptions = ['Data Owner / Admin', 'DataOwner', 'Admin', 'Analyst'];

  constructor(
    private readonly schemasService: SchemasService,
    private readonly session: SessionService,
    private readonly confirmation: ConfirmationService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.session.role() === 'Data Owner / Admin';
  }

  get editorTitle(): string {
    return this.editorMode === 'create' ? 'Create schema' : 'Edit schema';
  }

  load(): void {
    this.loading = true;
    this.error = null;
    this.schemasService.list().subscribe({
      next: (response) => {
        this.schemas = response.schemas;
        this.loading = false;
      },
      error: () => {
        this.error = 'Unable to load schemas.';
        this.loading = false;
      }
    });
  }

  startCreate(): void {
    this.editorMode = 'create';
    this.selected = null;
    this.schemaForm = {
      name: '',
      dataSource: SchemaDataSource.Mongo,
      database: 'sample_airbnb',
      collection: 'listingsAndReviews',
      description: '',
      isActiveSource: false
    };
    this.fields = [];
    this.error = null;
    this.editorVisible = true;
  }

  startEdit(summary: SchemaSummary): void {
    this.schemasService.get(summary.id).subscribe({
      next: (response) => {
        const schema = response.schema;
        this.editorMode = 'edit';
        this.selected = schema;
        this.schemaForm = {
          name: schema.name,
          dataSource: schema.dataSource,
          database: schema.database,
          collection: schema.collection,
          description: schema.description ?? '',
          isActiveSource: schema.isActiveSource
        };
        this.fields = schema.fields.map((field) => ({ ...field, dataOwnerRoles: [...field.dataOwnerRoles] }));
        this.error = null;
        this.editorVisible = true;
      },
      error: () => (this.error = 'Unable to load schema details.')
    });
  }

  closeEditor(): void {
    this.editorVisible = false;
    this.saving = false;
  }

  saveSchema(): void {
    this.error = null;

    if (!this.schemaForm.name.trim()) {
      this.error = 'Schema name is required.';
      return;
    }

    if (!this.schemaForm.database.trim() || !this.schemaForm.collection.trim()) {
      this.error = 'Database and collection are required.';
      return;
    }

    if (this.fields.length === 0) {
      this.error = 'Add at least one field before saving.';
      return;
    }

    if (this.fields.some((field) => !field.path.trim())) {
      this.error = 'Every field needs a path.';
      return;
    }

    const paths = this.fields.map((field) => field.path.trim());
    if (new Set(paths).size !== paths.length) {
      this.error = 'Duplicate field paths are not allowed.';
      return;
    }

    const requests: SchemaFieldRequest[] = this.fields.map((field) => ({
      path: field.path.trim(),
      dataType: field.dataType,
      isSensitive: field.isSensitive,
      requiresApproval: field.requiresApproval,
      dataOwnerRoles: field.dataOwnerRoles,
      description: field.description
    }));

    this.saving = true;

    if (this.editorMode === 'create') {
      const request: CreateSchemaRequest = {
        name: this.schemaForm.name.trim(),
        dataSource: this.schemaForm.dataSource,
        database: this.schemaForm.database.trim(),
        collection: this.schemaForm.collection.trim(),
        fields: requests,
        description: this.schemaForm.description.trim() || undefined,
        isActiveSource: this.schemaForm.isActiveSource
      };

      this.schemasService.create(request).subscribe({
        next: () => {
          this.saving = false;
          this.editorVisible = false;
          this.load();
        },
        error: (err) => {
          this.saving = false;
          this.error = err?.error?.error ?? 'Unable to create schema.';
        }
      });
      return;
    }

    if (!this.selected) {
      this.saving = false;
      return;
    }

    const request: UpdateSchemaRequest = {
      name: this.schemaForm.name.trim(),
      dataSource: this.schemaForm.dataSource,
      database: this.schemaForm.database.trim(),
      collection: this.schemaForm.collection.trim(),
      fields: requests,
      version: this.selected.version,
      isActiveSource: this.schemaForm.isActiveSource,
      description: this.schemaForm.description.trim() || undefined
    };

    this.schemasService.update(this.selected.id, request).subscribe({
      next: () => {
        this.saving = false;
        this.editorVisible = false;
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.error = err?.error?.error ?? 'Unable to update schema.';
      }
    });
  }

  openAddField(): void {
    this.fieldEditIndex = null;
    this.fieldDraft = this.emptyField();
    this.fieldError = '';
    this.fieldDialogVisible = true;
  }

  openEditField(index: number): void {
    const field = this.fields[index];
    this.fieldEditIndex = index;
    this.fieldDraft = { ...field, dataOwnerRoles: [...field.dataOwnerRoles] };
    this.fieldError = '';
    this.fieldDialogVisible = true;
  }

  saveField(): void {
    const path = this.fieldDraft.path.trim();
    if (!path) {
      this.fieldError = 'Field path is required.';
      return;
    }

    const duplicate = this.fields.some(
      (field, index) => index !== this.fieldEditIndex && field.path.trim() === path
    );
    if (duplicate) {
      this.fieldError = `Field '${path}' already exists.`;
      return;
    }

    const field: SchemaField = {
      ...this.fieldDraft,
      path,
      dataOwnerRoles: [...this.fieldDraft.dataOwnerRoles]
    };

    if (this.fieldEditIndex === null) {
      this.fields = [...this.fields, field];
    } else {
      this.fields = this.fields.map((current, index) => (index === this.fieldEditIndex ? field : current));
    }

    this.fieldDialogVisible = false;
  }

  removeField(index: number): void {
    const field = this.fields[index];
    this.confirmation.confirm({
      header: 'Delete field',
      message: `Remove '${field.path}' from this schema?`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.fields = this.fields.filter((_, current) => current !== index);
      }
    });
  }

  activate(summary: SchemaSummary): void {
    this.confirmation.confirm({
      header: 'Activate source',
      message: `Set "${summary.name}" as the active source? Guardrails and result columns will switch immediately.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Activate',
      rejectLabel: 'Cancel',
      accept: () => {
        this.schemasService.activate(summary.id).subscribe({
          next: () => this.load(),
          error: () => (this.error = 'Unable to activate schema.')
        });
      }
    });
  }

  deleteSchema(summary: SchemaSummary): void {
    if (summary.isActiveSource) {
      this.error = 'The active schema cannot be deleted. Activate another schema first.';
      return;
    }

    this.confirmation.confirm({
      header: 'Delete schema',
      message: `Delete "${summary.name}"? This cannot be undone.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Delete',
      rejectLabel: 'Cancel',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        this.schemasService.delete(summary.id).subscribe({
          next: () => this.load(),
          error: (err) => (this.error = err?.error?.error ?? 'Unable to delete schema.')
        });
      }
    });
  }

  openImport(): void {
    this.importText = '';
    this.importJson = '';
    this.importPreview = [];
    this.importError = '';
    this.importVisible = true;
  }

  previewImport(): void {
    try {
      this.importPreview = this.parseImportFields();
      this.importError = this.importPreview.length === 0 ? 'No fields found in the import payload.' : '';
    } catch (err) {
      this.importPreview = [];
      this.importError = err instanceof Error ? err.message : 'Unable to parse the import payload.';
    }
  }

  commitImport(): void {
    if (this.importPreview.length === 0) {
      this.previewImport();
    }

    if (this.importPreview.length === 0) {
      return;
    }

    this.importing = true;
    this.importError = '';

    const request: SchemaFieldRequest[] = this.importPreview.map((field) => ({
      path: field.path,
      dataType: field.dataType,
      isSensitive: field.isSensitive,
      requiresApproval: field.requiresApproval,
      dataOwnerRoles: field.dataOwnerRoles,
      description: field.description
    }));

    this.schemasService.import({ fieldsJson: JSON.stringify(request) }).subscribe({
      next: () => {
        this.importing = false;
        this.importVisible = false;
        this.load();
      },
      error: (err) => {
        this.importing = false;
        this.importError = err?.error?.error ?? 'Unable to import schema.';
      }
    });
  }

  private parseImportFields(): SchemaField[] {
    if (this.importJson.trim()) {
      return this.parseJsonFields(this.importJson.trim());
    }

    if (!this.importText.trim()) {
      throw new Error('Paste schema.txt lines or a JSON field array first.');
    }

    return this.parseSchemaText(this.importText);
  }

  private parseJsonFields(raw: string): SchemaField[] {
    let parsed: unknown;
    try {
      parsed = JSON.parse(raw);
    } catch {
      throw new Error('The JSON payload is not valid JSON.');
    }

    if (!Array.isArray(parsed)) {
      throw new Error('The JSON payload must be an array of fields.');
    }

    return parsed.map((item, index) => {
      if (typeof item !== 'object' || item === null) {
        throw new Error(`Entry ${index + 1} is not a field object.`);
      }

      const record = item as Record<string, unknown>;
      const path = typeof record['path'] === 'string' ? record['path'].trim() : '';
      if (!path) {
        throw new Error(`Entry ${index + 1} is missing a path.`);
      }

      return {
        path,
        dataType: this.toFieldType(record['dataType']),
        isSensitive: record['isSensitive'] === true,
        requiresApproval: record['requiresApproval'] === true,
        dataOwnerRoles: Array.isArray(record['dataOwnerRoles'])
          ? record['dataOwnerRoles'].filter((role): role is string => typeof role === 'string')
          : [],
        description: typeof record['description'] === 'string' ? record['description'] : ''
      };
    });
  }

  private parseSchemaText(raw: string): SchemaField[] {
    const fields: SchemaField[] = [];

    for (const rawLine of raw.split('\n')) {
      const line = rawLine.trim();
      if (!line.startsWith('- ')) {
        continue;
      }

      const entry = line.slice(2);
      const colon = entry.indexOf(':');
      if (colon <= 0) {
        continue;
      }

      const path = entry.slice(0, colon).trim();
      const rest = entry.slice(colon + 1).trim();
      if (!path) {
        continue;
      }

      fields.push({
        path,
        dataType: this.toFieldType(rest),
        isSensitive: false,
        requiresApproval: false,
        dataOwnerRoles: [],
        description: ''
      });
    }

    return fields;
  }

  private toFieldType(value: unknown): SchemaFieldType {
    const normalized = typeof value === 'string' ? value.trim().toLowerCase() : '';
    if (normalized.startsWith('array')) {
      return SchemaFieldType.Array;
    }

    switch (normalized) {
      case 'number':
        return SchemaFieldType.Number;
      case 'boolean':
        return SchemaFieldType.Boolean;
      case 'date':
        return SchemaFieldType.Date;
      case 'object':
        return SchemaFieldType.Object;
      default:
        return SchemaFieldType.String;
    }
  }

  private emptyField(): SchemaField {
    return {
      path: '',
      dataType: SchemaFieldType.String,
      isSensitive: false,
      requiresApproval: false,
      dataOwnerRoles: [],
      description: ''
    };
  }
}
