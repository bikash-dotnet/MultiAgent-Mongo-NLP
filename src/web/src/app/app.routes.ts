import { Routes } from '@angular/router';
import { WorkspaceComponent } from './workspace/workspace.component';
import { GovernanceQueueComponent } from './governance/governance-queue.component';
import { AdminAnalyticsComponent } from './admin/admin-analytics.component';

export const routes: Routes = [
  { path: '', component: WorkspaceComponent },
  { path: 'governance', component: GovernanceQueueComponent },
  { path: 'admin', component: AdminAnalyticsComponent }
];
