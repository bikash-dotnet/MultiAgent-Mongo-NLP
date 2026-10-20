import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { LayoutService, LayoutPreferences } from './layout.service';

describe('LayoutService', () => {
  let service: LayoutService;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    });
    service = TestBed.inject(LayoutService);
  });

  afterEach(() => {
    localStorage.clear();
  });

  it('starts with default preferences', () => {
    expect(service.darkMode).toBe(false);
    expect(service.sidebarVisible).toBe(true);
    expect(service.layoutPreset).toBe('sagga');
    expect(service.surface).toBe('light');
  });

  it('persists dark mode to localStorage', () => {
    service.setDarkMode(true);

    expect(service.darkMode).toBe(true);
    const stored: Partial<LayoutPreferences> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.darkMode).toBe(true);
    expect(stored.sidebarVisible).toBe(true);
  });

  it('persists sidebar visibility to localStorage', () => {
    service.setSidebarVisible(false);

    expect(service.sidebarVisible).toBe(false);
    const stored: Partial<LayoutPreferences> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.sidebarVisible).toBe(false);
  });

  it('persists layout preset to localStorage', () => {
    service.setPreset('vela');

    expect(service.layoutPreset).toBe('vela');
    const stored: Partial<LayoutPreferences> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.layoutPreset).toBe('vela');
  });

  it('persists surface to localStorage', () => {
    service.setSurface('dark');

    expect(service.surface).toBe('dark');
    const stored: Partial<LayoutPreferences> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.surface).toBe('dark');
  });

  it('resets all preferences to defaults', () => {
    service.setDarkMode(true);
    service.setSidebarVisible(false);
    service.setPreset('arya');
    service.setSurface('dark');

    service.reset();

    expect(service.darkMode).toBe(false);
    expect(service.sidebarVisible).toBe(true);
    expect(service.layoutPreset).toBe('sagga');
    expect(service.surface).toBe('light');
    const stored: LayoutPreferences = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored).toEqual({
      darkMode: false,
      sidebarVisible: true,
      layoutPreset: 'sagga',
      surface: 'light'
    });
  });

  it('ignores stale or invalid localStorage JSON', () => {
    localStorage.setItem('multi-agent-nlp.layout', 'not-json{{{');

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    });
    const fresh = TestBed.inject(LayoutService);

    expect(fresh.darkMode).toBe(false);
    expect(fresh.sidebarVisible).toBe(true);
  });

  it('restores preferences from valid localStorage JSON', () => {
    localStorage.setItem('multi-agent-nlp.layout', JSON.stringify({
      darkMode: true,
      sidebarVisible: false,
      layoutPreset: 'arya',
      surface: 'dark'
    }));

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    });
    const fresh = TestBed.inject(LayoutService);

    expect(fresh.darkMode).toBe(true);
    expect(fresh.sidebarVisible).toBe(false);
    expect(fresh.layoutPreset).toBe('arya');
    expect(fresh.surface).toBe('dark');
  });

  it('emits darkMode changes on darkMode$', () => {
    const values: boolean[] = [];
    service.darkMode$.subscribe((v) => values.push(v));

    service.setDarkMode(true);
    service.setDarkMode(false);

    expect(values).toEqual([false, true, false]);
  });
});
