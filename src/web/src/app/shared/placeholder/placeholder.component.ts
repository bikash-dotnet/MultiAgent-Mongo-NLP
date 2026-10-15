import { Component, Input } from '@angular/core';

@Component({
  selector: 'app-placeholder',
  standalone: true,
  template: `
    <section class="placeholder" [attr.aria-labelledby]="headingId">
      <h2 [id]="headingId">{{ title }}</h2>
      <p class="description">{{ description }}</p>
      <p class="hint">Planned for Sprint {{ sprint }}.</p>
    </section>
  `,
  styles: [
    `
      .placeholder {
        max-width: 640px;
        margin: 48px auto;
        padding: 32px;
        border-radius: 16px;
        border: 1px solid var(--p-content-border-color, #e5e7eb);
        background: var(--p-content-background, #fff);
      }
      h2 {
        margin: 0 0 12px;
        font-size: 22px;
        color: var(--p-text-color, #1e293b);
      }
      .description {
        margin: 0 0 8px;
        color: var(--p-text-muted-color, #64748b);
      }
      .hint {
        margin: 0;
        font-size: 13px;
        color: var(--p-text-muted-color, #94a3b8);
      }
    `
  ]
})
export class PlaceholderComponent {
  @Input({ required: true }) title = '';
  @Input() description = '';
  @Input() sprint = '';

  get headingId(): string {
    return `placeholder-${this.title.toLowerCase().replace(/[^a-z0-9]+/g, '-')}`;
  }
}
