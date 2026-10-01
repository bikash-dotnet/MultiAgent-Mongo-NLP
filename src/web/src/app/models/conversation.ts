import { ExecutionPayload } from './execution';

export interface ColumnOption {
  name: string;
  selected: boolean;
}

export interface ConversationTurn {
  conversationId: string;
  step: string;
  kind: string;
  assistantMessage: string;
  control: string;
  emailPrefill?: string | null;
  columns?: ColumnOption[] | null;
  deliveryOptions: string[];
  accessRequestId?: string | null;
  approvalRequired: boolean;
  downloadable: boolean;
  demoReport: boolean;
  result?: unknown;
  validationError?: string | null;
  execution?: ExecutionPayload | null;
}

export interface ConversationAnswer {
  text?: string;
  email?: string;
  purpose?: string;
  projectCode?: string;
  businessImpact?: string;
  managerEmail?: string;
  columns?: string[];
  delivery?: string;
}

export interface ApprovalFlag {
  enabled: boolean;
}
