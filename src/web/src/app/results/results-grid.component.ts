import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ExecutionPayload } from '../models/execution';

@Component({
  selector: 'app-results-grid',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './results-grid.component.html',
  styleUrl: './results-grid.component.scss'
})
export class ResultsGridComponent {
  @Input() execution: ExecutionPayload | null = null;

  value(row: Record<string, string | null>, column: string): string {
    return row[column] ?? '';
  }
}
