import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { providePrimeNG } from 'primeng/config';
import Aura from '@primeuix/themes/aura';
import { AppComponent } from './app.component';

describe('AppComponent', () => {
  it('creates successfully', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        providePrimeNG({
          theme: {
            preset: Aura,
            options: { prefix: 'p', darkModeSelector: '.app-dark', cssLayer: false }
          }
        })
      ]
    });
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeTruthy();
  });

  it('provides router', () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        providePrimeNG({
          theme: {
            preset: Aura,
            options: { prefix: 'p', darkModeSelector: '.app-dark', cssLayer: false }
          }
        })
      ]
    });
    const fixture = TestBed.createComponent(AppComponent);
    expect(fixture.componentInstance).toBeDefined();
  });
});
