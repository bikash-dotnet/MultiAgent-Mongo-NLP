# Angular Project Contributing Guidelines

## Package Management

Always use `npm install` (or `npm remove`) when adding or removing packages. Never edit `package-lock.json` manually — let npm resolve dependencies.

### Adding a package

```bash
# From the web project directory
npm install <package-name>
```

### Adding a dev dependency

```bash
npm install --save-dev <package-name>
```

### Removing a package

```bash
npm remove <package-name>
# Or for dev dependencies
npm remove --save-dev <package-name>
```

### After any package change

Always run:

```bash
npm install
```

to regenerate `package-lock.json` and ensure dependency consistency. Then verify the build:

```bash
ng build --configuration production
```

and the tests:

```bash
ng test --watch=false
```

## Standard Angular Coding Guidelines

### 1. File Structure

Place component files in a dedicated directory under `src/app/`:

```
src/app/
  feature-name/
    feature.component.html
    feature.component.ts
    feature.component.scss
    feature.component.spec.ts
```

### 2. Component Style (Standalone)

All new components must be **standalone** with `standalone: true`. Import required dependencies in the component file.

```typescript
// feature.component.ts
import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-feature',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './feature.component.html',
  styleUrls: ['./feature.component.scss']
})
export class FeatureComponent {
  // component logic
}
```

### 3. Naming Conventions

- **Components**: `kebab-case` selector, `PascalCase` class name (`FeatureComponent`)
- **Templates**: Use **hyphenated** class names in HTML, not camelCase
- **SCSS files**: Use **BEM** methodology (`__block`, `__element`, `--modifier`)
- **Variables**: `camelCase` for TypeScript, `kebab-case` for CSS variables

### 4. Services

Services should be **injectable** and provided in `root` unless otherwise required.

```typescript
// service.injectable pattern
import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class DataService {
  // service logic
}
```

### 5. Reactive Forms

Use ** reactive forms** with `FormGroup`, `FormControl`, and validation.

```typescript
import { Component } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';

@Component({ ... })
export class FormComponent {
  form: FormGroup;

  constructor(private fb: FormBuilder) {
    this.form = this.fb.group({
      email: ['', [Validators.required, Validators.email]]
    });
  }
}
```

### 6. Routing

Define routes in `routing.ts` files, using **lazy loading** for feature modules.

```typescript
// app.routes.ts
import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { ChatComponent } from './chat/chat.component';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'chat', component: ChatComponent },
  {
    path: 'features',
    loadChildren: () => import('./features/features.routes').then(m => m.routes)
  }
];
```

### 7. HTTP & API Calls

Use the **`httpClient`** injected via `HttpClientModule`. Return observables and handle errors gracefully.

```typescript
import { HttpClient } from '@angular/common/http';
import { catchError, map } from 'rxjs/operators';
import { of } from 'rxjs';

constructor(private http: HttpClient) {}

loadData() {
  return this.http.get('/api/data').pipe(
    map((res: any) => res),
    catchError(err => {
      console.error('API error', err);
      return of(null);
    })
  );
}
```

### 8. Observables & async Pipe

Prefer the **`async`** pipe in templates to subscribe/unsubscribe automatically.

```html
<!-- template -->
<div *ngIf="data$ | asyn as data">
  {{ data.title }}
</div>
```

### 9. Commit Messages

Follow **conventional commits** format:

```
feat: add new component
fix: correct typo in header
docs: update README
style: format code with prettier
refactor: rename variable x to y
test: add unit test for component
chore: update dependencies
```

### 10. TypeScript Strict Mode

The project uses strict TypeScript settings. Ensure:

- All public functions have explicit return types
- `any` usage is minimized and justified
- Interfaces are used for data contracts

### 11. Accessibility

- Use native HTML elements whenever possible
- Add `aria-label` or `aria-describedby` for custom controls
- Ensure sufficient color contrast (project uses Aura theme with approved contrasts)

### 12. Testing

- Write **unit tests** (.spec.ts) for all new components and services
- Use **vitest** with the Angular testing library
- Mock services via `provideMocks` or `jest.spyOn`

```typescript
// example.spec.ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component } from '@angular/core';
import { FeatureComponent } from './feature.component';

describe('FeatureComponent', () => {
  let component: FeatureComponent;
  let fixture: ComponentFixture<FeatureComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [FeatureComponent]
    }).compileComponents();
  });

  beforeEach(() => {
    fixture = TestBed.createComponent(FeatureComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });
});
```