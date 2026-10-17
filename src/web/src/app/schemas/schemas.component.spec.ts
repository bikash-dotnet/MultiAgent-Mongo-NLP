import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ConfirmationService } from 'primeng/api';
import { SchemasComponent } from './schemas.component';
import { SchemasService } from '../services/schemas.service';
import { SessionService } from '../services/session.service';
import {
  CreateSchemaRequest,
  SchemaDataSource,
  SchemaDefinition,
  SchemaFieldType,
  SchemaSummary,
  UpdateSchemaRequest
} from '../models/schemas';

describe('SchemasComponent', () => {
  const summaries: SchemaSummary[] = [
    {
      id: 'sch_airbnb',
      name: 'Airbnb Listings',
      dataSource: SchemaDataSource.Mongo,
      fieldCount: 2,
      isActiveSource: true,
      version: 3,
      updatedAt: '2026-10-01T12:00:00Z',
      updatedBy: 'usr_owner'
    },
    {
      id: 'sch_other',
      name: 'Other Source',
      dataSource: SchemaDataSource.EnterpriseCoreREST,
      fieldCount: 1,
      isActiveSource: false,
      version: 1,
      updatedAt: '2026-10-02T12:00:00Z',
      updatedBy: 'usr_owner'
    }
  ];

  const detail: SchemaDefinition = {
    id: 'sch_airbnb',
    name: 'Airbnb Listings',
    dataSource: SchemaDataSource.Mongo,
    database: 'sample_airbnb',
    collection: 'listingsAndReviews',
    fields: [
      {
        path: 'price',
        dataType: SchemaFieldType.Number,
        isSensitive: false,
        requiresApproval: false,
        dataOwnerRoles: [],
        description: ''
      }
    ],
    isActiveSource: true,
    version: 3
  };

  let created: CreateSchemaRequest[];
  let updated: UpdateSchemaRequest[];
  let activated: string[];
  let deleted: string[];
  let imported: string[];

  function configure(role = 'Data Owner / Admin') {
    created = [];
    updated = [];
    activated = [];
    deleted = [];
    imported = [];

    const service = {
      list: () => of({ schemas: summaries }),
      get: () => of({ schema: detail }),
      create: (request: CreateSchemaRequest) => {
        created.push(request);
        return of({ schema: detail });
      },
      update: (id: string, request: UpdateSchemaRequest) => {
        updated.push(request);
        return of({ schema: { ...detail, id, version: request.version + 1 } });
      },
      delete: (id: string) => {
        deleted.push(id);
        return of(void 0);
      },
      activate: (id: string) => {
        activated.push(id);
        return of({ id, isActiveSource: true });
      },
      import: (request: { fieldsJson?: string | null; schemaTxt?: string | null }) => {
        imported.push(request.fieldsJson ?? request.schemaTxt ?? '');
        return of({ schema: detail });
      }
    };

    TestBed.configureTestingModule({
      imports: [SchemasComponent],
      providers: [
        { provide: SchemasService, useValue: service },
        { provide: SessionService, useValue: { role: () => role } },
        {
          provide: ConfirmationService,
          useValue: {
            confirm: (options: { accept?: () => void }) => options.accept?.(),
            requireConfirmation$: of(null)
          }
        }
      ]
    });
  }

  it('renders schemas and highlights the active source', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Airbnb Listings');
    expect(text).toContain('Other Source');
    expect(text).toContain('Active');
  });

  it('requires at least one field before creating a schema', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.startCreate();
    component.schemaForm.name = 'New Schema';
    component.saveSchema();

    expect(created).toEqual([]);
    expect(component.error).toContain('at least one field');
  });

  it('creates a schema with the fields added in the editor', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.startCreate();
    component.schemaForm.name = 'New Schema';
    component.openAddField();
    component.fieldDraft.path = 'price';
    component.fieldDraft.dataType = SchemaFieldType.Number;
    component.saveField();
    component.saveSchema();

    expect(created.length).toBe(1);
    expect(created[0].name).toBe('New Schema');
    expect(created[0].fields.length).toBe(1);
    expect(created[0].fields[0].path).toBe('price');
    expect(created[0].isActiveSource).toBe(false);
  });

  it('rejects duplicate field paths in the editor', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.startCreate();
    component.openAddField();
    component.fieldDraft.path = 'price';
    component.saveField();

    component.openAddField();
    component.fieldDraft.path = 'price';
    component.saveField();

    expect(component.fieldError).toContain('already exists');
    expect(component.fields.length).toBe(1);
  });

  it('activates a non-active schema', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    fixture.componentInstance.activate(summaries[1]);

    expect(activated).toEqual(['sch_other']);
  });

  it('refuses to delete the active schema', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.deleteSchema(summaries[0]);

    expect(deleted).toEqual([]);
    expect(component.error).toContain('active schema cannot be deleted');
  });

  it('deletes an inactive schema after confirmation', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    fixture.componentInstance.deleteSchema(summaries[1]);

    expect(deleted).toEqual(['sch_other']);
  });

  it('previews and imports pasted schema.txt fields', () => {
    configure();
    const fixture = TestBed.createComponent(SchemasComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.openImport();
    component.importText = '- name: string\n- price: number';
    component.previewImport();

    expect(component.importPreview.length).toBe(2);
    expect(component.importPreview[1].dataType).toBe(SchemaFieldType.Number);

    component.commitImport();

    expect(imported.length).toBe(1);
    expect(imported[0]).toContain('"path":"price"');
  });
});
