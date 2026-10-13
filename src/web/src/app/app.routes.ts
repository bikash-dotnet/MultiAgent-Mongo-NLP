import { Routes } from '@angular/router';
import { WorkspaceComponent } from './workspace/workspace.component';

export const routes: Routes = [
  { path: '', component: WorkspaceComponent },
  {
    path: 'governance',
    loadComponent: () => import('./governance/governance-queue.component').then((m) => m.GovernanceQueueComponent)
  },
  {
    path: 'admin',
    loadComponent: () => import('./admin/admin-analytics.component').then((m) => m.AdminAnalyticsComponent)
  },
  {
    path: 'schemas',
    loadComponent: () => import('./schemas/schemas.component').then((m) => m.SchemasComponent)
  },
  {
    path: 'configuration',
    loadComponent: () => import('./configuration/configuration.component').then((m) => m.ConfigurationComponent)
  },
  {
    path: 'business-rules',
    loadComponent: () =>
      import('./business-rules/business-rules.component').then((m) => m.BusinessRulesComponent)
  }
];
