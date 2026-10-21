import { Inject, Injectable, PLATFORM_ID, signal, computed, effect } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

export interface LayoutConfig {
  preset: string;
  primary: string;
  surface: string | null;
  darkTheme: boolean;
  menuMode: string;
}

interface LayoutState {
  staticMenuDesktopInactive: boolean;
  overlayMenuActive: boolean;
  configSidebarVisible: boolean;
  mobileMenuActive: boolean;
  menuHoverActive: boolean;
  activePath: string | null;
}

const STORAGE_KEY = 'multi-agent-nlp.layout';

const DEFAULTS: LayoutConfig = {
  preset: 'Aura',
  primary: 'emerald',
  surface: null,
  darkTheme: false,
  menuMode: 'static'
};

@Injectable({ providedIn: 'root' })
export class LayoutService {
  layoutConfig = signal<LayoutConfig>(DEFAULTS);

  layoutState = signal<LayoutState>({
    staticMenuDesktopInactive: false,
    overlayMenuActive: false,
    configSidebarVisible: false,
    mobileMenuActive: false,
    menuHoverActive: false,
    activePath: null
  });

  readonly theme = computed(() => (this.layoutConfig().darkTheme ? 'dark' : 'light'));
  readonly isSidebarActive = computed(
    () => this.layoutState().overlayMenuActive || this.layoutState().mobileMenuActive
  );
  readonly isDarkTheme = computed(() => this.layoutConfig().darkTheme);
  readonly getPrimary = computed(() => this.layoutConfig().primary);
  readonly getSurface = computed(() => this.layoutConfig().surface);
  readonly isOverlay = computed(() => this.layoutConfig().menuMode === 'overlay');

  constructor(@Inject(PLATFORM_ID) private readonly platformId: object) {
    this.load();

    effect(() => {
      const config = this.layoutConfig();
      if (isPlatformBrowser(this.platformId)) {
        if (config.darkTheme) {
          document.documentElement.classList.add('app-dark');
        } else {
          document.documentElement.classList.remove('app-dark');
        }
        // Sync text color CSS variables with dark mode so toggling from topbar
        // doesn't leave light-mode text colors stuck (which would override
        // PrimeNG's dark theme variables set via the .app-dark selector).
        const text = config.darkTheme ? '#ffffff' : '#111827';
        const muted = config.darkTheme ? '#9ca3af' : '#6b7280';
        document.documentElement.style.setProperty('--p-text-color', text);
        document.documentElement.style.setProperty('--p-text-muted-color', muted);
        document.documentElement.style.setProperty('--text-color', text);
        document.documentElement.style.setProperty('--text-color-secondary', muted);
      }
    });

    effect(() => {
      this.persist();
    });
  }

  private load(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (!raw) {
        return;
      }
      const parsed = JSON.parse(raw) as Partial<LayoutConfig>;
      if (parsed && typeof parsed === 'object') {
        this.layoutConfig.set({ ...DEFAULTS, ...parsed });
      }
    } catch {
      // stale or invalid JSON: keep defaults
    }
  }

  private persist(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this.layoutConfig()));
    } catch {
      // storage quota or private mode: ignore
    }
  }

  onMenuToggle(): void {
    if (this.isOverlay()) {
      this.layoutState.update((prev) => ({ ...prev, overlayMenuActive: !prev.overlayMenuActive }));
    }

    if (this.isDesktop()) {
      this.layoutState.update((prev) => ({ ...prev, staticMenuDesktopInactive: !prev.staticMenuDesktopInactive }));
    } else {
      this.layoutState.update((prev) => ({ ...prev, mobileMenuActive: !prev.mobileMenuActive }));
    }
  }

  showConfigSidebar(): void {
    this.layoutState.update((prev) => ({ ...prev, configSidebarVisible: true }));
  }

  hideConfigSidebar(): void {
    this.layoutState.update((prev) => ({ ...prev, configSidebarVisible: false }));
  }

  isDesktop(): boolean {
    if (!isPlatformBrowser(this.platformId)) {
      return true;
    }
    return window.innerWidth > 991;
  }

  isMobile(): boolean {
    return !this.isDesktop();
  }
}
