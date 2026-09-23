import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ConversationService } from './conversation.service';

describe('ConversationService', () => {
  let service: ConversationService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ConversationService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts a conversation', () => {
    service.start('average price by market').subscribe((turn) => {
      expect(turn.step).toBe('Email');
      expect(turn.control).toBe('email');
    });

    const req = http.expectOne('/api/conversations');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ utterance: 'average price by market' });
    req.flush({ conversationId: 'c1', step: 'Email', kind: 'ComplexLlmRequired', assistantMessage: 'Email?', control: 'email', deliveryOptions: ['EMAIL', 'CSV'] });
  });

  it('answers and reads the approval flag', () => {
    service.answer('c1', { delivery: 'CSV' }).subscribe();
    http.expectOne('/api/conversations/c1/answers').flush({ conversationId: 'c1', step: 'Complete' });

    service.setApproval(false).subscribe((state) => expect(state.enabled).toBe(false));
    const put = http.expectOne('/api/governance/approval');
    expect(put.request.method).toBe('PUT');
    put.flush({ enabled: false });
  });
});
