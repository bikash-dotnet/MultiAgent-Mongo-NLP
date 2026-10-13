import { Component } from '@angular/core';
import { PlaceholderComponent } from '../shared/placeholder/placeholder.component';

@Component({
  selector: 'app-business-rules',
  standalone: true,
  imports: [PlaceholderComponent],
  template: `
    <app-placeholder
      title="Business rules"
      description="Define ordered, scoped rules and column definitions that shape defaults, row caps, masking, and exports."
      sprint="12">
    </app-placeholder>
  `
})
export class BusinessRulesComponent {}
