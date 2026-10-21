import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { NoopAnimationsModule } from '@angular/platform-browser/animations';
import { ConfirmationService, MessageService, PrimeTemplate } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { TableModule } from 'primeng/table';
import Aura from '@primeuix/themes/aura';
import { BusinessRulesComponent } from './business-rules.component';
import { BusinessRulesService } from '../services/business-rules.service';
import { SessionService } from '../services/session.service';
import {
  BusinessRuleResponse,
  ColumnDataType,
  ColumnDefinitionResponse,
  RuleScope
} from '../models/business-rules';

const rules: BusinessRuleResponse[] = [
  {
    id: 'rule_1',
    name: 'Cap export rows',
    description: 'Limit exported rows',
    scope: RuleScope.Global,
    scopeValue: null,
    priority: 10,
    enabled: true,
    condition: { field: 'rows', operator: 'gt', values: ['1000'], combine: 'all' },
    action: { type: 'capRows', parameters: { max: '1000' } },
    version: 1
  }
];

const columns: ColumnDefinitionResponse[] = [
  {
    id: 'col_1',
    field: 'customer_name',
    label: 'Customer',
    dataType: ColumnDataType.String,
    format: null,
    order: 1,
    visibleByDefault: true,
    exportable: true,
    sensitive: false,
    width: null,
    version: 1
  }
];

function setup(role = 'Data Owner / Admin') {
  const service = {
    listRules: () => of(rules),
    listColumnDefinitions: () => of(columns)
  };

  TestBed.configureTestingModule({
    imports: [BusinessRulesComponent, NoopAnimationsModule, TableModule, PrimeTemplate],
    providers: [
      providePrimeNG({
        theme: {
          preset: Aura,
          options: { prefix: 'p', darkModeSelector: '.app-dark', cssLayer: false }
        }
      }),
      { provide: BusinessRulesService, useValue: service },
      { provide: SessionService, useValue: { role: () => role } },
      { provide: ConfirmationService, useValue: { confirm: () => {} } },
      { provide: MessageService, useValue: { add: () => {} } }
    ]
  });

  const fixture = TestBed.createComponent(BusinessRulesComponent);
  return { fixture };
}

describe('BusinessRulesComponent', () => {
  it('renders rule rows returned by the API', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Cap export rows');
    expect(text).toContain('Cap rows: 1000');
  });

  it('renders column definition rows returned by the API', async () => {
    const { fixture } = setup();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('customer_name');

    const labelValues = Array.from(
      fixture.nativeElement.querySelectorAll('input') as NodeListOf<HTMLInputElement>
    ).map((input) => input.value);
    expect(labelValues).toContain('Customer');
  });
});
