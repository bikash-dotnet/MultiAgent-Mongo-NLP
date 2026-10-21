import { Component, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';
import { StyleClassModule } from 'primeng/styleclass';
import { LayoutService } from '../service/layout.service';
import { SessionService } from '../../services/session.service';

@Component({
  selector: 'app-topbar',
  standalone: true,
  imports: [RouterModule, CommonModule, StyleClassModule],
  template: `
    <div class="layout-topbar">
      <div class="layout-topbar-logo-container">
        <button class="layout-menu-button layout-topbar-action" (click)="layoutService.onMenuToggle()">
          <i class="pi pi-bars"></i>
        </button>
        <a class="layout-topbar-logo" routerLink="/">
          <span class="logo-text">Multi-Agent NLP</span>
        </a>
      </div>

      <div class="layout-topbar-actions">
        <button class="layout-topbar-menu-button layout-topbar-action" pStyleClass="@next" enterFromClass="hidden" enterActiveClass="animate-scalein" leaveToClass="hidden" leaveActiveClass="animate-fadeout" [hideOnOutsideClick]="true">
          <i class="pi pi-ellipsis-v"></i>
        </button>

        <span class="layout-topbar-role-chip">{{ sessionService.role() ?? 'Guest' }}</span>
      </div>
    </div>
  `
})
export class AppTopbar {
  layoutService = inject(LayoutService);
  sessionService = inject(SessionService);
}
