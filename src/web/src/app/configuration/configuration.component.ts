import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { ConfigurationService } from '../services/configuration.service';
import { SessionService } from '../services/session.service';
import {
  ConfigSectionResponse,
  ConfigSchemaResponse,
  SettingsFieldSchema,
  SettingsSource,
  SettingType,
  UpdateConfigSectionRequest
} from '../models/configuration';

interface SectionDraft {
  values: { [key: string]: any };
  version: number;
  errors: { [key: string]: string };
  saving: boolean;
}

@Component({
  selector: 'app-configuration',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ToastModule,
    CardModule,
    InputTextModule,
    InputNumberModule,
    CheckboxModule,
    SelectModule,
    TagModule,
    BadgeModule,
    ButtonModule
  ],
  providers: [MessageService],
  templateUrl: './configuration.component.html',
  styleUrl: './configuration.component.scss'
})
export class ConfigurationComponent implements OnInit {
  sections: ConfigSectionResponse[] = [];
  schema: ConfigSchemaResponse | null = null;
  loading = false;
  error: string | null = null;

  drafts = new Map<string, SectionDraft>();
  canManage = false;

  constructor(
    private readonly configurationService: ConfigurationService,
    private readonly session: SessionService,
    private readonly message: MessageService
  ) {}

  ngOnInit(): void {
    this.canManage = this.session.role() === 'Data Owner / Admin';
    this.load();
  }

  load(): void {
    this.loading = true;
    this.error = null;

    this.configurationService.schema().subscribe({
      next: (schema) => {
        this.schema = schema;
      },
      error: () => {
        this.error = 'Unable to load configuration schema.';
        this.loading = false;
      }
    });

    this.configurationService.list().subscribe({
      next: (sections) => {
        this.sections = sections;
        for (const section of sections) {
          this.drafts.set(section.sectionName, {
            values: { ...this.initialValues(section) },
            version: 0,
            errors: {},
            saving: false
          });
        }
        this.loading = false;
      },
      error: () => {
        this.error = 'Unable to load configuration.';
        this.loading = false;
      }
    });
  }

  private initialValues(section: ConfigSectionResponse): { [key: string]: any } {
    const values: { [key: string]: any } = {};
    for (const setting of section.values) {
      values[setting.key] = setting.value;
    }
    return values;
  }

  getFieldSchema(sectionName: string, key: string): SettingsFieldSchema | null {
    if (!this.schema) return null;
    const section = this.schema.sections.find((s) => s.sectionName === sectionName);
    return section?.settings.find((s) => s.key === key) ?? null;
  }

  isNumberType(type: SettingType): boolean {
    return type === SettingType.Number || type === SettingType.Double || type === SettingType.Port;
  }

  isBooleanType(type: SettingType): boolean {
    return type === SettingType.Boolean;
  }

  isSelectType(type: SettingType): boolean {
    return type === SettingType.Select;
  }

  getDraft(sectionName: string): SectionDraft {
    return this.drafts.get(sectionName)!;
  }

  save(sectionName: string): void {
    const draft = this.drafts.get(sectionName);
    const section = this.sections.find((s) => s.sectionName === sectionName);
    if (!draft || !section) return;

    draft.errors = {};
    draft.saving = true;

    const request: UpdateConfigSectionRequest = {
      values: { ...draft.values },
      version: draft.version
    };

    this.configurationService.update(sectionName, request).subscribe({
      next: (updated) => {
        this.sections = this.sections.map((s) =>
          s.sectionName === sectionName ? updated : s
        );
        const updatedValues: { [key: string]: any } = {};
        for (const v of updated.values) {
          updatedValues[v.key] = v.value;
        }
        draft.values = updatedValues;
        draft.version = (draft.version ?? 0) + 1;
        draft.saving = false;
        this.message.add({
          severity: 'success',
          summary: 'Saved',
          detail: `${section.label} configuration updated.`
        });
      },
      error: (err) => {
        draft.saving = false;
        const serverError = err.error?.error ?? 'Unable to save configuration.';
        if (err.status === 409) {
          this.error = serverError;
          this.message.add({ severity: 'error', summary: 'Conflict', detail: serverError });
          return;
        }
        this.error = serverError;
        this.message.add({ severity: 'error', summary: 'Validation failed', detail: serverError });
      }
    });
  }

  resetSection(sectionName: string): void {
    const section = this.sections.find((s) => s.sectionName === sectionName);
    if (!section) return;

    const draft = this.drafts.get(sectionName)!;
    draft.values = { ...this.initialValues(section) };
    draft.errors = {};
    this.error = null;
    this.message.add({
      severity: 'info',
      summary: 'Reset',
      detail: `${section.label} reverted to local defaults.`
    });
  }

  async resetAll(): Promise<void> {
    if (!confirm('This will clear all database-backed configuration overrides and restore bootstrap defaults. Continue?')) {
      return;
    }
    try {
      await this.configurationService.reset({ confirm: true }).toPromise();
      this.load();
      this.message.add({
        severity: 'info',
        summary: 'Reset all',
        detail: 'All configuration overrides cleared.'
      });
    } catch {
      this.message.add({ severity: 'error', summary: 'Reset failed', detail: 'Unable to reset configuration.' });
    }
  }

  isInvalid(sectionName: string, key: string): boolean {
    return !!this.drafts.get(sectionName)?.errors[key];
  }

  errorMessage(sectionName: string, key: string): string | null {
    return this.drafts.get(sectionName)?.errors[key] ?? null;
  }

  protected readonly SettingType = SettingType;
  protected readonly SettingsSource = SettingsSource;
}
