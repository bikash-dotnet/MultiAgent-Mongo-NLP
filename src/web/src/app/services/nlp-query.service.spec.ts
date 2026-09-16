import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { NlpQueryService } from './nlp-query.service';

describe('NlpQueryService', () => {
  let service: NlpQueryService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(NlpQueryService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the utterance and returns the pipeline', () => {
    service.query('average price by market').subscribe((result) => {
      expect(result.kind).toBe('ComplexLlmRequired');
      expect(result.mql).toContain('$group');
    });

    const req = http.expectOne('/api/nlp/query');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ utterance: 'average price by market' });
    req.flush({
      kind: 'ComplexLlmRequired',
      mql: '[{"$group":{"_id":"$address.market"}}]',
      question: null,
      semanticCacheHit: false,
      slotExtractionUsed: false,
      intent: 'Search',
      justRunIt: false,
      llmTokensConsumed: 42,
      llmAttempts: 1,
      error: null
    });
  });
});
