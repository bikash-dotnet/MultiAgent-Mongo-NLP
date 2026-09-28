import { Routes } from '@angular/router';
import { WorkspaceComponent } from './workspace/workspace.component';
import { GovernanceQueueComponent } from './governance/governance-queue.component';

export const routes: Routes = [
  { path: '', component: WorkspaceComponent },
  { path: 'governance', component: GovernanceQueueComponent }
];
