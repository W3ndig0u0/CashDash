import { Component, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { RaceComponent } from './components/race/race.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RaceComponent],
  templateUrl: './app.html',
})

export class App {
  protected readonly title = signal('frontend');
}