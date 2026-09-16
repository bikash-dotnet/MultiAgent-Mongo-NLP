export interface NlpQueryResponse {
  kind: string;
  mql: string | null;
  question: string | null;
  semanticCacheHit: boolean;
  slotExtractionUsed: boolean;
  intent: string;
  justRunIt: boolean;
  llmTokensConsumed: number;
  llmAttempts: number;
  error: string | null;
}
