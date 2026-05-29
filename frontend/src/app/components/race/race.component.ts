import { CommonModule } from "@angular/common";
import { Component } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouteResponse } from "../../models/race.model";
import { RaceService } from "../../services/race.service";

@Component({
  selector: 'race',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './race.component.html',
})
export class RaceComponent {
  amount: number = 1000;
  country: string = 'USA';
  apiResponse: RouteResponse | null = null;
  isLoading: boolean = false; 

  constructor(public raceService: RaceService) {}

  onCalculate(): void {
    let currencyCode = 'USD';
    
    if (this.country === 'Mexiko') {
      currencyCode = 'MXN';
    } else if (this.country === 'Colombia') {
      currencyCode = 'COP';
    }

    this.isLoading = true;
    this.apiResponse = null;

    this.raceService.calculateRoutes({ 
      amountSEK: this.amount, 
      destinationCountry: this.country,
      targetCurrency: currencyCode
    })
    .subscribe({
      next: (res) => {
        this.apiResponse = res;
        this.isLoading = false;
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
      }
    });
  }
}