import { CommonModule } from '@angular/common';
import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { PanelMenuModule } from 'primeng/panelmenu';
import { ButtonModule } from 'primeng/button';
import { MenuItem } from 'primeng/api';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, PanelMenuModule, ButtonModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  isMenuCollapsed = false;
  menuItems: MenuItem[] = [
    { label: 'Chat', icon: 'pi pi-comments', routerLink: '/' },
    { label: 'Governance', icon: 'pi pi-shield', routerLink: '/governance' },
    { label: 'Admin', icon: 'pi pi-chart-bar', routerLink: '/admin' },
    { label: 'Schemas', icon: 'pi pi-database', routerLink: '/schemas' },
    { label: 'Configuration', icon: 'pi pi-cog', routerLink: '/configuration' },
    { label: 'Business Rules', icon: 'pi pi-sliders-h', routerLink: '/business-rules' }
  ];

  toggleMenu(): void {
    this.isMenuCollapsed = !this.isMenuCollapsed;
  }
}