import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SchemasService } from '../services/schemas.service';
import { SessionService } from '../services/session.service';
import {
  SchemaDefinition,
  SchemaSummary,
  SchemaField,
  SchemaFieldType,
  SchemaDataSource,
  CreateSchemaRequest,
  UpdateSchemaRequest,
  SchemaFieldRequest
} from '../models/schemas';

@Component({
  selector: 'app-schemas',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './schemas.component.html',
  styleUrl: './schemas.component.scss'
})
export class SchemasComponent implements OnInit {
  schemas: SchemaSummary[] = [];
  selected: SchemaDefinition | null = null;
  editing = false;
  creating = false;
  loading = false;
  saving = false;
  error: string | null = null;

  newName = '';
  newDatabase = '';
  newCollection = '';
  newDescription = '';

  editName = '';
  editDatabase = '';
  editCollection = '';
  editDescription = '';
  editDataSource = SchemaDataSource.Mongo;
  editIsActive = false;

  newFieldPath = '';
  newFieldType = SchemaFieldType.String;
  newFieldSensitive = false;
  newFieldRequiresApproval = false;
  newFieldRoles = '';
  newFieldDescription = '';

  editFieldPath = '';
  editFieldType = SchemaFieldType.String;
  editFieldSensitive = false;
  editFieldRequiresApproval = false;
  editFieldRoles = '';
  editFieldDescription = '';
  editingFieldIndex: number | null = null;

  dataSourceOptions = [
    { value: SchemaDataSource.Mongo, label: 'MongoDB' },
    { value: SchemaDataSource.EnterpriseCoreREST, label: 'Enterprise Core REST' },
    { value: SchemaDataSource.Elasticsearch, label: 'Elasticsearch' }
  ];

  fieldTypeOptions = [
    SchemaFieldType.String,
    SchemaFieldType.Number,
    SchemaFieldType.Boolean,
    SchemaFieldType.Date,
    SchemaFieldType.Object,
    SchemaFieldType.Array
  ];

  constructor(
    private readonly schemasService: SchemasService,
    private readonly session: SessionService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  get canManage(): boolean {
    return this.session.role() === 'Data Owner / Admin';
  }

  get activeSchema(): SchemaSummary | undefined {
    return this.schemas.find((s) => s.isActiveSource);
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
    this.creating = true;
    this.editing = false;
    this.selected = null;
    this.error = null;
    this.newName = '';
    this.newDatabase = 'sample_airbnb';
    this.newCollection = 'listingsAndReviews';
    this.newDescription = '';
  }

  startEdit(schema: SchemaSummary): void {
    this.schemasService.get(schema.id).subscribe({
      next: (response) => {
        this.selected = response.schema;
        this.editing = true;
        this.creating = false;
        this.error = null;
        this.editName = this.selected.name;
        this.editDatabase = this.selected.database;
        this.editCollection = this.selected.collection;
        this.editDescription = this.selected.description ?? '';
        this.editDataSource = this.selected.dataSource;
        this.editIsActive = this.selected.isActiveSource;
        this.resetEditField();
      },
      error: () => (this.error = 'Unable to load schema details.')
    });
  }

  cancel(): void {
    this.selected = null;
    this.editing = false;
    this.creating = false;
    this.error = null;
  }

  saveCreate(): void {
    if (!this.newName.trim()) {
      this.error = 'Schema name is required.';
      return;
    }

    const fields: SchemaFieldRequest[] = [];

    const request: CreateSchemaRequest = {
      name: this.newName.trim(),
      dataSource: SchemaDataSource.Mongo,
      database: this.newDatabase.trim(),
      collection: this.newCollection.trim(),
      fields,
      description: this.newDescription.trim() || undefined
    };

    this.saving = true;
    this.schemasService.create(request).subscribe({
      next: () => {
        this.saving = false;
        this.creating = false;
        this.load();
      },
      error: () => {
        this.saving = false;
        this.error = 'Unable to create schema.';
      }
    });
  }

  saveEdit(): void {
    if (!this.selected || !this.editName.trim()) {
      this.error = 'Schema name is required.';
      return;
    }

    const fields: SchemaFieldRequest[] = this.selected.fields.map((f) => ({
      path: f.path,
      dataType: f.dataType,
      isSensitive: f.isSensitive,
      requiresApproval: f.requiresApproval,
      dataOwnerRoles: f.dataOwnerRoles,
      description: f.description
    }));

    const request: UpdateSchemaRequest = {
      name: this.editName.trim(),
      dataSource: this.editDataSource,
      database: this.editDatabase.trim(),
      collection: this.editCollection.trim(),
      fields,
      version: this.selected.version,
      isActiveSource: this.editIsActive,
      description: this.editDescription.trim() || undefined
    };

    this.saving = true;
    this.schemasService.update(this.selected.id, request).subscribe({
      next: () => {
        this.saving = false;
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.error = err?.error?.error ?? 'Unable to update schema.';
      }
    });
  }

  deleteSchema(id: string): void {
    if (!confirm('Delete this schema?')) {
      return;
    }
    // DELETE /api/schemas/{id} is not exposed; removal is via store only.
    this.error = 'Schema deletion is not supported through the UI.';
  }

  activate(id: string): void {
    this.schemasService.activate(id).subscribe({
      next: () => this.load(),
      error: () => (this.error = 'Unable to activate schema.')
    });
  }

  addField(): void {
    if (!this.newFieldPath.trim()) {
      this.error = 'Field path is required.';
      return;
    }
    if (this.selected!.fields.some((f) => f.path === this.newFieldPath.trim())) {
      this.error = 'Field already exists.';
      return;
    }

    const field: SchemaField = {
      path: this.newFieldPath.trim(),
      dataType: this.newFieldType,
      isSensitive: this.newFieldSensitive,
      requiresApproval: this.newFieldRequiresApproval,
      dataOwnerRoles: this.newFieldRoles.trim() ? this.newFieldRoles.trim().split(',').map((r) => r.trim()) : [],
      description: this.newFieldDescription.trim()
    };

    const request: SchemaFieldRequest = {
      path: field.path,
      dataType: field.dataType,
      isSensitive: field.isSensitive,
      requiresApproval: field.requiresApproval,
      dataOwnerRoles: field.dataOwnerRoles,
      description: field.description
    };

    this.schemasService.addField(this.selected!.id, request).subscribe({
      next: (response) => {
        this.selected = response.schema;
        this.error = null;
        this.resetNewField();
      },
      error: () => (this.error = 'Unable to add field.')
    });
  }

  startEditField(index: number): void {
    if (!this.selected) {
      return;
    }
    const field = this.selected.fields[index];
    this.editFieldPath = field.path;
    this.editFieldType = field.dataType;
    this.editFieldSensitive = field.isSensitive;
    this.editFieldRequiresApproval = field.requiresApproval;
    this.editFieldRoles = field.dataOwnerRoles.join(', ');
    this.editFieldDescription = field.description;
    this.editingFieldIndex = index;
  }

  saveEditField(): void {
    if (!this.selected || this.editingFieldIndex === null) {
      return;
    }
    if (!this.editFieldPath.trim()) {
      this.error = 'Field path is required.';
      return;
    }

    const request: SchemaFieldRequest = {
      path: this.editFieldPath.trim(),
      dataType: this.editFieldType,
      isSensitive: this.editFieldSensitive,
      requiresApproval: this.editFieldRequiresApproval,
      dataOwnerRoles: this.editFieldRoles.trim() ? this.editFieldRoles.trim().split(',').map((r) => r.trim()) : [],
      description: this.editFieldDescription.trim()
    };

    this.schemasService.updateField(this.selected.id, this.selected.fields[this.editingFieldIndex].path, request).subscribe({
      next: (response) => {
        this.selected = response.schema;
        this.error = null;
        this.resetEditField();
      },
      error: () => (this.error = 'Unable to update field.')
    });
  }

  deleteField(path: string): void {
    if (!this.selected) {
      return;
    }
    if (!confirm(`Delete field '${path}'?`)) {
      return;
    }

    this.schemasService.deleteField(this.selected.id, path).subscribe({
      next: (response) => {
        this.selected = response.schema;
        this.error = null;
      },
      error: () => (this.error = 'Unable to delete field.')
    });
  }

  private resetNewField(): void {
    this.newFieldPath = '';
    this.newFieldType = SchemaFieldType.String;
    this.newFieldSensitive = false;
    this.newFieldRequiresApproval = false;
    this.newFieldRoles = '';
    this.newFieldDescription = '';
  }

  resetEditField(): void {
    this.editFieldPath = '';
    this.editFieldType = SchemaFieldType.String;
    this.editFieldSensitive = false;
    this.editFieldRequiresApproval = false;
    this.editFieldRoles = '';
    this.editFieldDescription = '';
    this.editingFieldIndex = null;
  }

  fieldTypeLabel(type: SchemaFieldType): string {
    return type;
  }
}
