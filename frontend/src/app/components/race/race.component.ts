import { CommonModule } from "@angular/common";
import { Component, inject } from "@angular/core";
import { RaceService } from "../../services/race.service";

@Component({
  selector: 'race',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './race.component.html',
})
export class RaceComponent {

  private raceService = inject(RaceService);
  races$ = this.raceService.getRaces();
}