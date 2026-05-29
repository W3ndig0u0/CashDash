import { Component, signal } from '@angular/core';
import { RaceComponent } from './components/race/race.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RaceComponent],
  templateUrl: './app.html',
})

export class App {
  protected readonly title = signal('frontend');
}