import { Component } from '@angular/core';
import { PlaceholderComponent } from '../shared/placeholder/placeholder.component';

@Component({
  selector: 'app-schemas',
  standalone: true,
  imports: [PlaceholderComponent],
  template: `
    <app-placeholder
      title="Schema registry"
      description="Create, view, edit, and activate data-source schemas that drive guardrails, validation, and result columns."
      sprint="10">
    </app-placeholder>
  `
})
export class SchemasComponent {}
