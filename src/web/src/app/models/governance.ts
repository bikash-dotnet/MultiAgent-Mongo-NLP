export interface RequesterInfo {
  userId: string;
  name: string;
  role: string;
  leadUserId?: string | null;
}

export interface RequestedFlag {
  fieldPath: string;
  flag: string;
}

export interface GovernanceJustification {
  businessReason: string;
  businessImpact: string;
  projectCode?: string | null;
}

export interface ApprovalResolution {
  assignedLeadId: string;
  resolvedByUserId: string;
  resolvedByName: string;
  resolvedByRole: string;
  overrideInvoked: boolean;
  overrideType?: string | null;
  resolvedAt: string;
  notes?: string | null;
}

export interface AccessRequest {
  id: string;
  sessionId: string;
  mql: string;
  sensitiveFields: string[];
  status: string;
  justification?: string | null;
  createdAt: string;
  requester?: RequesterInfo | null;
  assignedLeadId?: string | null;
  requestedFlags?: RequestedFlag[] | null;
  justificationDetails?: GovernanceJustification | null;
  resolution?: ApprovalResolution | null;
}
