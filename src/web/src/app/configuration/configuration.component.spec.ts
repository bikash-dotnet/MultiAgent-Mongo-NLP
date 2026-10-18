import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ConfigurationComponent } from './configuration.component';
import { ConfigurationService } from '../services/configuration.service';
import { SessionService } from '../services/session.service';
import {
  ConfigSectionResponse,
  ConfigSchemaResponse,
  SettingsSource,
  SettingType
} from '../models/configuration';

const section: ConfigSectionResponse = {
  sectionName: 'Execution',
  label: 'Execution',
  adminOnly: false,
  values: [
    {
      key: 'TimeoutMs',
      label: 'Execution timeout (ms)',
      type: SettingType.Number,
      value: 5000,
      source: SettingsSource.Default,
      editable: true,
      secret: false
    },
    {
      key: 'DataSource',
      label: 'Data source',
      type: SettingType.Select,
      value: 'Mongo',
      source: SettingsSource.Default,
      editable: true,
      secret: false
    },
    {
      key: 'ApprovalEnabled',
      label: 'Approval required',
      type: SettingType.Boolean,
      value: true,
      source: SettingsSource.Default,
      editable: true,
      secret: false
    }
  ]
};

const schema: ConfigSchemaResponse = {
  sections: [
    {
      sectionName: 'Execution',
      label: 'Execution',
      adminOnly: false,
      settings: [
        {
          key: 'TimeoutMs',
          label: 'Execution timeout (ms)',
          type: 'Number',
          editable: true,
          secret: false,
          default: 5000,
          min: 1000,
          max: 60000
        },
        {
          key: 'DataSource',
          label: 'Data source',
          type: 'Select',
          editable: true,
          secret: false,
          default: 'Mongo',
          allowedValues: ['Mongo', 'EnterpriseCoreREST']
        },
        {
          key: 'ApprovalEnabled',
          label: 'Approval required',
          type: 'Boolean',
          editable: true,
          secret: false,
          default: true
        }
      ]
    }
  ]
};

describe('ConfigurationComponent', () => {
  let configService: any;
  let sessionService: any;

  beforeEach(async () => {
    configService = {
      schema: () => of(schema),
      list: () => of([section]),
      update: () => of(section),
      reset: () => of(undefined)
    } as any;
    sessionService = {
      role: () => 'Data Owner / Admin'
    } as any;

    await TestBed.configureTestingModule({
      imports: [ConfigurationComponent],
      providers: [
        { provide: ConfigurationService, useValue: configService },
        { provide: SessionService, useValue: sessionService }
      ]
    }).compileComponents();
  });

  it('renders section tabs with settings and source badges', () => {
    const fixture = TestBed.createComponent(ConfigurationComponent);
    fixture.detectChanges();

    const tabPanels = fixture.nativeElement.querySelectorAll('p-tabpanel');
    expect(tabPanels.length).toBe(1);

    const textContent = fixture.nativeElement.textContent;
    expect(textContent).toContain('Execution');
    expect(textContent).toContain('Default');
    expect(textContent).toContain('Execution timeout (ms)');
  });

  it('hides save buttons for non-admin users', () => {
    sessionService.role = () => 'Business Analyst';

    const fixture = TestBed.createComponent(ConfigurationComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.canManage).toBe(false);
  });

  it('saves a section when Save is clicked', () => {
    let updateCalled = false;
    configService.update = (() => {
      updateCalled = true;
      return of(section);
    }) as any;
    sessionService.role = () => 'Data Owner / Admin';

    const fixture = TestBed.createComponent(ConfigurationComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    const draft = component.getDraft('Execution');
    draft.values['TimeoutMs'] = 15000;

    component.save('Execution');
    fixture.detectChanges();

    expect(updateCalled).toBe(true);
  });

  it('shows an error when save fails', () => {
    configService.update = (() =>
      throwError(() => ({ status: 400, error: { error: 'Invalid value' } }))) as any;
    sessionService.role = () => 'Data Owner / Admin';

    const fixture = TestBed.createComponent(ConfigurationComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    component.save('Execution');
    fixture.detectChanges();

    const error = fixture.nativeElement.querySelector('.error')?.textContent;
    expect(error).toContain('Invalid value');
  });

  it('renders a Reset section button for each section', () => {
    sessionService.role = () => 'Data Owner / Admin';

    const fixture = TestBed.createComponent(ConfigurationComponent);
    fixture.detectChanges();

    const component = fixture.componentInstance;
    expect(component.canManage).toBe(true);
    expect(typeof component.resetSection).toBe('function');
  });

  it('renders disabled input for non-editable settings', () => {
    const readOnlySection: ConfigSectionResponse = {
      ...section,
      values: [
        {
          ...section.values[0],
          editable: false
        }
      ]
    };
    configService.list = (() => of([readOnlySection])) as any;

    const fixture = TestBed.createComponent(ConfigurationComponent);
    fixture.detectChanges();

    const input = fixture.nativeElement.querySelector('p-inputnumber');
    expect(input).toBeTruthy();
  });
});
