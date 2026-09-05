import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { SessionService } from './services/session.service';
import { AgentStreamService } from './services/agent-stream.service';

describe('AppComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        {
          provide: SessionService,
          useValue: {
            sessionId: () => 'sess_test',
            bootstrap: () => of({ token: 't' }),
            greeting: () =>
              of({
                displayName: 'Bikash',
                period: 'morning',
                message: 'Hi Bikash, Good morning',
                chips: ['Just run it']
              })
          }
        },
        {
          provide: AgentStreamService,
          useValue: {
            connect: () => of({ event: 'agent.idle', data: '{"status":"idle"}' })
          }
        }
      ]
    }).compileComponents();
  });

  it('renders the personalized greeting and chips', () => {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('h1')?.textContent).toContain('Hi Bikash, Good morning');
    expect(compiled.querySelector('.chip')?.textContent).toContain('Just run it');
  });
});
