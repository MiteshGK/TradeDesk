import { registerLocaleData } from '@angular/common';
import localeEnIn from '@angular/common/locales/en-IN';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';

// Indian digit grouping (1,00,00,000) for amounts on the blotter.
registerLocaleData(localeEnIn);

bootstrapApplication(AppComponent, appConfig).catch(err => console.error(err));
