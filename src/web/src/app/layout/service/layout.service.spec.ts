import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { LayoutService, LayoutConfig } from './layout.service';

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

  it('starts with default layout config', () => {
    expect(service.layoutConfig().preset).toBe('Aura');
    expect(service.layoutConfig().primary).toBe('emerald');
    expect(service.layoutConfig().surface).toBeNull();
    expect(service.layoutConfig().darkTheme).toBe(false);
    expect(service.layoutConfig().menuMode).toBe('static');
  });

  it('starts with default layout state', () => {
    expect(service.layoutState().overlayMenuActive).toBe(false);
    expect(service.layoutState().mobileMenuActive).toBe(false);
    expect(service.layoutState().staticMenuDesktopInactive).toBe(false);
    expect(service.layoutState().configSidebarVisible).toBe(false);
    expect(service.layoutState().menuHoverActive).toBe(false);
    expect(service.layoutState().activePath).toBeNull();
  });

  it('isDarkTheme reflects layoutConfig darkTheme', () => {
    expect(service.isDarkTheme()).toBe(false);

    service.layoutConfig.update((c) => ({ ...c, darkTheme: true }));
    expect(service.isDarkTheme()).toBe(true);
  });

  it('isOverlay reflects menuMode overlay', () => {
    expect(service.isOverlay()).toBe(false);

    service.layoutConfig.update((c) => ({ ...c, menuMode: 'overlay' }));
    expect(service.isOverlay()).toBe(true);
  });

  it('persists darkTheme to localStorage', async () => {
    service.layoutConfig.update((c) => ({ ...c, darkTheme: true }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const stored: Partial<LayoutConfig> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.darkTheme).toBe(true);
  });

  it('persists preset to localStorage', async () => {
    service.layoutConfig.update((c) => ({ ...c, preset: 'Lara' }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const stored: Partial<LayoutConfig> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.preset).toBe('Lara');
  });

  it('persists surface to localStorage', async () => {
    service.layoutConfig.update((c) => ({ ...c, surface: 'slate' }));
    await new Promise((resolve) => setTimeout(resolve, 0));

    const stored: Partial<LayoutConfig> = JSON.parse(localStorage.getItem('multi-agent-nlp.layout') ?? '{}');
    expect(stored.surface).toBe('slate');
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

    expect(fresh.layoutConfig().darkTheme).toBe(false);
    expect(fresh.layoutConfig().preset).toBe('Aura');
  });

  it('restores preferences from valid localStorage JSON', () => {
    localStorage.setItem(
      'multi-agent-nlp.layout',
      JSON.stringify({ preset: 'Nora', primary: 'indigo', surface: 'zinc', darkTheme: true, menuMode: 'overlay' })
    );

    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        { provide: PLATFORM_ID, useValue: 'browser' }
      ]
    });
    const fresh = TestBed.inject(LayoutService);

    expect(fresh.layoutConfig().preset).toBe('Nora');
    expect(fresh.layoutConfig().primary).toBe('indigo');
    expect(fresh.layoutConfig().surface).toBe('zinc');
    expect(fresh.layoutConfig().darkTheme).toBe(true);
    expect(fresh.layoutConfig().menuMode).toBe('overlay');
  });

  it('onMenuToggle switches overlay menu active in overlay mode', () => {
    service.layoutConfig.update((c) => ({ ...c, menuMode: 'overlay' }));

    expect(service.layoutState().overlayMenuActive).toBe(false);
    service.onMenuToggle();
    expect(service.layoutState().overlayMenuActive).toBe(true);
  });

  it('onMenuToggle switches mobile menu active on mobile', () => {
    Object.defineProperty(window, 'innerWidth', { value: 500, configurable: true });

    expect(service.layoutState().mobileMenuActive).toBe(false);
    service.onMenuToggle();
    expect(service.layoutState().mobileMenuActive).toBe(true);
  });

  it('showConfigSidebar and hideConfigSidebar toggle configSidebarVisible', () => {
    expect(service.layoutState().configSidebarVisible).toBe(false);
    service.showConfigSidebar();
    expect(service.layoutState().configSidebarVisible).toBe(true);
    service.hideConfigSidebar();
    expect(service.layoutState().configSidebarVisible).toBe(false);
  });

  it('isDesktop returns true in non-browser platform', () => {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        LayoutService,
        { provide: PLATFORM_ID, useValue: 'server' }
      ]
    });
    const srv = TestBed.inject(LayoutService);
    expect(srv.isDesktop()).toBe(true);
  });
});
