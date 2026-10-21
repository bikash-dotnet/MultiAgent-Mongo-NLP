import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MenuItem } from 'primeng/api';
import { AppMenuitem } from './app.menuitem';

@Component({
  selector: 'app-menu',
  standalone: true,
  imports: [CommonModule, AppMenuitem, RouterModule],
  template: `<ul class="layout-menu">
    @for (item of model; track item.label) {
      @if (!item.separator) {
        <li app-menuitem [item]="item" [root]="true"></li>
      } @else {
        <li class="menu-separator"></li>
      }
    }
  </ul> `,
})
export class AppMenu {
  model: MenuItem[] = [];

  ngOnInit() {
    this.model = [
      {
        label: 'Workspace',
        items: [
          { label: 'Chat', icon: 'pi pi-fw pi-comments', routerLink: ['/'] },
          { label: 'Governance', icon: 'pi pi-fw pi-shield', routerLink: ['/governance'] },
          { label: 'Admin', icon: 'pi pi-fw pi-chart-bar', routerLink: ['/admin'] },
          { label: 'Schemas', icon: 'pi pi-fw pi-database', routerLink: ['/schemas'] },
          { label: 'Configuration', icon: 'pi pi-fw pi-cog', routerLink: ['/configuration'] },
          { label: 'Business Rules', icon: 'pi pi-fw pi-sliders-h', routerLink: ['/business-rules'] },
          { label: 'MCP Servers', icon: 'pi pi-fw pi-server', routerLink: ['/mcp-servers'] }
        ]
      }
    ];
  }
}
