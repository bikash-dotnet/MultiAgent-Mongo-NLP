import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { GovernanceQueueComponent } from './governance-queue.component';
import { GovernanceService } from '../services/governance.service';
import { SessionService } from '../services/session.service';

describe('GovernanceQueueComponent', () => {
  const pending = {
    id: 'req_1',
    sessionId: 'sess_1',
    mql: '[]',
    sensitiveFields: ['address.location.coordinates'],
    status: 'PENDING_LEAD',
    createdAt: '2026-09-25T00:00:00Z',
    requester: { userId: 'usr_analyst', name: 'Analyst', role: 'Business Analyst', leadUserId: 'usr_lead' },
    assignedLeadId: 'usr_lead',
    justificationDetails: { businessReason: 'Geo analysis', businessImpact: 'Market planning', projectCode: 'PROJ-1' }
  };

  let approved: string[];
  let overridden: string[];

  function configure(role: string) {
    approved = [];
    overridden = [];
    TestBed.configureTestingModule({
      imports: [GovernanceQueueComponent],
      providers: [
        {
          provide: GovernanceService,
          useValue: {
            list: () => of([pending]),
            approve: (id: string) => {
              approved.push(id);
              return of({ ...pending, status: 'APPROVED' });
            },
            reject: () => of({ ...pending, status: 'REJECTED' }),
            override: (id: string) => {
              overridden.push(id);
              return of({ ...pending, status: 'APPROVED' });
            }
          }
        },
        { provide: SessionService, useValue: { role: () => role } }
      ]
    });
  }

  it('renders pending requests and approves as the assigned lead', () => {
    configure('Team Lead');
    const fixture = TestBed.createComponent(GovernanceQueueComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Geo analysis');
    fixture.componentInstance.approve(pending);
    expect(approved).toEqual(['req_1']);
  });

  it('requires notes before overriding', () => {
    configure('Engineering Manager');
    const fixture = TestBed.createComponent(GovernanceQueueComponent);
    fixture.detectChanges();

    fixture.componentInstance.overrideNotes = '';
    fixture.componentInstance.override(pending);
    expect(overridden).toEqual([]);

    fixture.componentInstance.overrideNotes = 'urgent';
    fixture.componentInstance.override(pending);
    expect(overridden).toEqual(['req_1']);
  });
});
