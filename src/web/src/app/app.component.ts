import { CommonModule } from '@angular/common';
import { Component, NO_ERRORS_SCHEMA } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { PanelMenuModule } from 'primeng/panelmenu';
import { ButtonModule } from 'primeng/button';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, PanelMenuModule, ButtonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss',
  schemas: [NO_ERRORS_SCHEMA]
})
export class AppComponent {
  isMenuCollapsed = false;
  isMobile = false;
  menuItems = [
    { label: 'Chat', command: ['/'] },
    { label: 'Governance', command: ['/governance'] },
    { label: 'Admin', command: ['/admin'] },
    { label: 'Schemas', command: ['/schemas'] },
    { label: 'Configuration', command: ['/configuration'] },
    { label: 'Business Rules', command: ['/business-rules'] }
  ];

  constructor() {
    if (typeof window !== 'undefined') {
      this.isMobile = window.innerWidth < 768;
    }
  }

  onMenuCollapsedChange(event: any): void {
    this.isMenuCollapsed = event.value;
  }

  toggleMenu(): void {
    this.isMenuCollapsed = !this.isMenuCollapsed;
  }
}