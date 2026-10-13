import { Component } from '@angular/core';
import { PlaceholderComponent } from '../shared/placeholder/placeholder.component';

@Component({
  selector: 'app-configuration',
  standalone: true,
  imports: [PlaceholderComponent],
  template: `
    <app-placeholder
      title="Configuration"
      description="Edit governed runtime settings such as execution timeout, cache threshold, and NLP defaults without a restart."
      sprint="11">
    </app-placeholder>
  `
})
export class ConfigurationComponent {}
