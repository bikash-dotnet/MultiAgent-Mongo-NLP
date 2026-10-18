export enum SettingsSource {
  Default = 'Default',
  Database = 'Database'
}

export enum SettingType {
  String = 'String',
  Number = 'Number',
  Double = 'Double',
  Boolean = 'Boolean',
  Select = 'Select',
  Url = 'Url',
  Password = 'Password',
  Port = 'Port'
}

export interface SettingValueResponse {
  key: string;
  label: string;
  type: SettingType;
  value: any;
  source: SettingsSource;
  editable: boolean;
  secret: boolean;
  description?: string | null;
}

export interface ConfigSectionResponse {
  sectionName: string;
  label: string;
  adminOnly: boolean;
  values: SettingValueResponse[];
}

export interface SettingsFieldSchema {
  key: string;
  label: string;
  type: string;
  editable: boolean;
  secret: boolean;
  default: any;
  min?: number | null;
  max?: number | null;
  minD?: number | null;
  maxD?: number | null;
  allowedValues?: string[] | null;
  description?: string | null;
}

export interface SettingsSectionSchema {
  sectionName: string;
  label: string;
  adminOnly: boolean;
  settings: SettingsFieldSchema[];
}

export interface ConfigSchemaResponse {
  sections: SettingsSectionSchema[];
}

export interface UpdateConfigSectionRequest {
  values: { [key: string]: any };
  version: number;
  notes?: string | null;
}

export interface ConfigResetRequest {
  confirm: boolean;
}
