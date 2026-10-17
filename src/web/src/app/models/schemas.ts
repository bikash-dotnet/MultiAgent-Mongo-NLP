export enum SchemaDataSource {
  Mongo = 'Mongo',
  EnterpriseCoreREST = 'EnterpriseCoreREST',
  Elasticsearch = 'Elasticsearch'
}

export enum SchemaFieldType {
  String = 'String',
  Number = 'Number',
  Boolean = 'Boolean',
  Date = 'Date',
  Object = 'Object',
  Array = 'Array'
}

export interface SchemaField {
  path: string;
  dataType: SchemaFieldType;
  isSensitive: boolean;
  requiresApproval: boolean;
  dataOwnerRoles: string[];
  description: string;
}

export interface SchemaDefinition {
  id: string;
  name: string;
  dataSource: SchemaDataSource;
  database: string;
  collection: string;
  fields: SchemaField[];
  isActiveSource: boolean;
  version: number;
  description?: string | null;
  createdAt?: string | null;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface SchemaSummary {
  id: string;
  name: string;
  dataSource: SchemaDataSource;
  fieldCount: number;
  isActiveSource: boolean;
  version: number;
  updatedAt?: string | null;
  updatedBy?: string | null;
}

export interface SchemaListResponse {
  schemas: SchemaSummary[];
}

export interface SchemaResponse {
  schema: SchemaDefinition;
}

export interface CreateSchemaRequest {
  name: string;
  dataSource: SchemaDataSource;
  database: string;
  collection: string;
  fields: SchemaFieldRequest[];
  description?: string | null;
  isActiveSource?: boolean;
}

export interface UpdateSchemaRequest {
  name: string;
  dataSource: SchemaDataSource;
  database: string;
  collection: string;
  fields: SchemaFieldRequest[];
  version: number;
  isActiveSource: boolean;
  description?: string | null;
}

export interface SchemaFieldRequest {
  path: string;
  dataType: SchemaFieldType;
  isSensitive: boolean;
  requiresApproval: boolean;
  dataOwnerRoles?: string[] | null;
  description?: string;
}

export interface ImportSchemaRequest {
  fieldsJson?: string | null;
  schemaTxt?: string | null;
}
