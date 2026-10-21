import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ToastModule } from 'primeng/toast';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { BadgeModule } from 'primeng/badge';
import { ButtonModule } from 'primeng/button';
import { TabsModule } from 'primeng/tabs';
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
    ToggleSwitchModule,
    SelectModule,
    TagModule,
    BadgeModule,
    ButtonModule,
    TabsModule
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
  activeSection = '';

  constructor(
    private readonly configurationService: ConfigurationService,
    private readonly session: SessionService,
    private readonly message: MessageService,
    private readonly cdr: ChangeDetectorRef
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
        this.cdr.markForCheck();
      },
      error: () => {
        this.error = 'Unable to load configuration schema.';
        this.loading = false;
      }
    });

    this.configurationService.list().subscribe({
      next: (sections) => {
        this.sections = sections;
        if (!this.activeSection && sections.length) {
          this.activeSection = sections[0].sectionName;
        }
        for (const section of sections) {
          this.drafts.set(section.sectionName, {
            values: { ...this.initialValues(section) },
            version: section.version ?? 0,
            errors: {},
            saving: false
          });
        }
        this.loading = false;
        this.cdr.markForCheck();
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

  private validateField(sectionName: string, key: string, value: any): string | null {
    const schema = this.getFieldSchema(sectionName, key);
    if (!schema) return null;

    if (schema.type === SettingType.Url || schema.type === SettingType.String || schema.type === SettingType.Select) {
      if (typeof value === 'string') {
        if (schema.type === SettingType.Url && value.length > 0) {
          try {
            new URL(value);
          } catch {
            return 'Enter a valid URL.';
          }
        }
      }
    }

    if (this.isNumberTypeString(schema.type)) {
      const num = Number(value);
      if (!Number.isFinite(num)) {
        return 'Enter a valid number.';
      }
      if (schema.min != null && num < schema.min) {
        return `Must be at least ${schema.min}.`;
      }
      if (schema.max != null && num > schema.max) {
        return `Must be at most ${schema.max}.`;
      }
      if (schema.minD != null && num < schema.minD) {
        return `Must be at least ${schema.minD}.`;
      }
      if (schema.maxD != null && num > schema.maxD) {
        return `Must be at most ${schema.maxD}.`;
      }
    }

    return null;
  }

  private isNumberTypeString(type: string): boolean {
    return type === SettingType.Number || type === SettingType.Double || type === SettingType.Port;
  }

  private validateDraft(sectionName: string): boolean {
    const draft = this.drafts.get(sectionName);
    const section = this.sections.find((s) => s.sectionName === sectionName);
    if (!draft || !section) return false;

    const errors: { [key: string]: string } = {};
    let hasError = false;

    for (const setting of section.values) {
      if (!setting.editable) continue;
      const err = this.validateField(sectionName, setting.key, draft.values[setting.key]);
      if (err) {
        errors[setting.key] = err;
        hasError = true;
      }
    }

    draft.errors = errors;
    return !hasError;
  }

  save(sectionName: string): void {
    const draft = this.drafts.get(sectionName);
    const section = this.sections.find((s) => s.sectionName === sectionName);
    if (!draft || !section) return;

    if (!this.validateDraft(sectionName)) return;

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
        const updatedSection = this.sections.find((s) => s.sectionName === sectionName);
        const updatedValues: { [key: string]: any } = {};
        for (const v of updatedSection?.values ?? []) {
          updatedValues[v.key] = v.value;
        }
        draft.values = updatedValues;
        draft.version = updated.version ?? (draft.version ?? 0) + 1;
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

    this.configurationService.remove(sectionName).subscribe({
      next: () => {
        this.load();
        this.message.add({
          severity: 'info',
          summary: 'Reset',
          detail: `${section.label} reverted to bootstrap defaults.`
        });
      },
      error: (err) => {
        const serverError = err.error?.error ?? 'Unable to reset section.';
        this.error = serverError;
        this.message.add({ severity: 'error', summary: 'Reset failed', detail: serverError });
      }
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
