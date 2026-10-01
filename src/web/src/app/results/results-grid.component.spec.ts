import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ResultsGridComponent } from './results-grid.component';
import { ExecutionPayload } from '../models/execution';

describe('ResultsGridComponent', () => {
  let fixture: ComponentFixture<ResultsGridComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [ResultsGridComponent] }).compileComponents();
    fixture = TestBed.createComponent(ResultsGridComponent);
  });

  it('renders columns and rows', () => {
    const execution: ExecutionPayload = {
      columns: ['name', 'price'],
      rows: [{ name: 'a', price: '10' }],
      dataSource: 'Mongo',
      rowCount: 1,
      durationMs: 4,
      timedOut: false
    };

    fixture.componentRef.setInput('execution', execution);
    fixture.detectChanges();

    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('name');
    expect(text).toContain('price');
    expect(text).toContain('a');
    expect(text).toContain('1 rows');
  });

  it('shows an empty state without a payload', () => {
    fixture.componentRef.setInput('execution', null);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('No rows');
  });
});
