import { Component } from '@angular/core';
import { AppLayout } from './layout/component/app.layout';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [AppLayout],
  template: `<app-layout></app-layout>`
})
export class AppComponent {}
