# Sprint 5 — RBAC Exemptions, Lead Approval, and Manager Override

**Duration:** 2 weeks  
**Sprint goal:** Complete the hierarchical governance workflow: Data Owner bypass, Team Lead approval, and managerial override.

**BRD version:** 4.2  
**Depends on:** Sprint 4

---

## Traceability

| BRD ID | Priority | Coverage in this sprint |
| --- | --- | --- |
| BRD-FR-06 | High | Bypass approval when user holds `data_owner_roles` |
| BRD-FR-07 | High | Managers claim and sign off pending lead queues |
| BRD-NFR-13 | — | Persist paused agent state for the hold duration |

---

## Tech stack

- ASP.NET Core RBAC middleware / policies
- Semantic Kernel paused-state store
- Collections: `access_requests` (written), `audit_logs` (override tags; full execution audit in Sprint 6)
- Angular Governance Approvals & Override Portal (first vertical)

---

## Stories

### S5-01 Role matrix
- Business Analyst: must enter justification; no approval authority
- Team Lead: approves direct subordinates
- Engineering Manager: waiver on standard fields; full override of Team Lead queues
- Data Owner / Admin: auto-approve; master override

### S5-02 Data Owner exemption (BRD-FR-06)
- If user role is in `data_owner_roles` for the flagged field: auto-approve
- Set `exemption_type = EXEMPTION_OWNER_ACCESS`
- Do not create a `PENDING_LEAD` queue item

### S5-03 Lead approval queue
- Default assignee = `requester.lead_user_id`
- Analyst must supply `business_reason`, `business_impact`, `project_code`
- Team Lead approve → status `APPROVED`; resume paused SK state
- Lead reject → terminal rejected state; do not execute

### S5-04 Managerial override (BRD-FR-07)
- Engineering Manager, Director, or Data Owner can claim a `PENDING_LEAD` item
- Set `override_invoked = true`, `override_type = HIERARCHICAL_MANAGEMENT_OVERRIDE`
- Store resolver user id, timestamp, notes on `access_requests`
- Dual-write override evidence fields (full `audit_logs` execution row in Sprint 6)

### S5-05 Governance SPA
- Justification form on sensitive pause
- Team Lead queue view
- Manager override portal (claim + sign-off)

---

## Acceptance criteria

- [ ] Data Owner / Admin executes sensitive queries without a pending request (BRD-FR-06)
- [ ] Business Analyst sensitive query creates `PENDING_LEAD` and requires justification
- [ ] Team Lead can approve subordinate requests and resume execution
- [ ] Engineering Manager / Director / Data Owner can claim and approve `PENDING_LEAD` items (BRD-FR-07)
- [ ] Override records `override_invoked`, actor, timestamp, justification
- [ ] Paused SK state survives the hold and resumes after `APPROVED`

---

## Out of scope

- Running the approved pipeline against MongoDB (Sprint 6)
- Admin analytics dashboard (Sprint 7)

---

## Exit artifacts

- Governance service + Angular approval/override portal
- `access_requests` lifecycle: `PENDING_LEAD` → `APPROVED` / rejected
- Exemption and override tagging
