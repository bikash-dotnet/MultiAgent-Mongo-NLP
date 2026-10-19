export interface ExecutionPayload {
  columns: string[];
  columnLabels?: Record<string, string> | null;
  rows: Record<string, string | null>[];
  dataSource: string;
  rowCount: number;
  durationMs: number;
  timedOut: boolean;
  error?: string | null;
}
