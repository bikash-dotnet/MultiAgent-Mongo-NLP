export interface ExecutionPayload {
  columns: string[];
  rows: Record<string, string | null>[];
  dataSource: string;
  rowCount: number;
  durationMs: number;
  timedOut: boolean;
  error?: string | null;
}
