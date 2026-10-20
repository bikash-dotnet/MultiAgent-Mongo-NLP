import { Inject, Injectable, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { BehaviorSubject, Observable, map } from 'rxjs';

export interface LayoutPreferences {
  darkMode: boolean;
  sidebarVisible: boolean;
  layoutPreset: string;
  surface: string;
}

const STORAGE_KEY = 'multi-agent-nlp.layout';

const DEFAULTS: LayoutPreferences = {
  darkMode: false,
  sidebarVisible: true,
  layoutPreset: 'sagga',
  surface: 'light'
};

@Injectable({ providedIn: 'root' })
export class LayoutService {
  private readonly preferences = new BehaviorSubject<LayoutPreferences>(DEFAULTS);
  readonly preferences$: Observable<LayoutPreferences> = this.preferences.asObservable();

  readonly darkMode$: Observable<boolean> = this.preferences.pipe(
    map((pref) => pref.darkMode)
  );
  readonly sidebarVisible$: Observable<boolean> = this.preferences.pipe(
    map((pref) => pref.sidebarVisible)
  );

  constructor(@Inject(PLATFORM_ID) private readonly platformId: object) {
    this.load();
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
      const parsed = JSON.parse(raw) as Partial<LayoutPreferences>;
      if (parsed && typeof parsed === 'object') {
        this.preferences.next({ ...DEFAULTS, ...parsed });
      }
    } catch {
      // stale or invalid JSON: keep defaults
    }
  }

  private persist(next: LayoutPreferences): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(next));
    } catch {
      // storage quota or private mode: ignore
    }
  }

  get darkMode(): boolean {
    return this.preferences.value.darkMode;
  }

  get sidebarVisible(): boolean {
    return this.preferences.value.sidebarVisible;
  }

  get layoutPreset(): string {
    return this.preferences.value.layoutPreset;
  }

  get surface(): string {
    return this.preferences.value.surface;
  }

  setDarkMode(value: boolean): void {
    const next = { ...this.preferences.value, darkMode: value };
    this.preferences.next(next);
    this.persist(next);
  }

  setSidebarVisible(value: boolean): void {
    const next = { ...this.preferences.value, sidebarVisible: value };
    this.preferences.next(next);
    this.persist(next);
  }

  setPreset(preset: string): void {
    const next = { ...this.preferences.value, layoutPreset: preset };
    this.preferences.next(next);
    this.persist(next);
  }

  setSurface(surface: string): void {
    const next = { ...this.preferences.value, surface };
    this.preferences.next(next);
    this.persist(next);
  }

  reset(): void {
    this.preferences.next(DEFAULTS);
    this.persist(DEFAULTS);
  }
}
