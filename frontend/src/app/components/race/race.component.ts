import { CommonModule } from "@angular/common";
import { ChangeDetectorRef, Component } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouteResponse } from "../../models/race.model";
import { RaceService } from "../../services/race.service";
import { TimeFormatterPipe } from "../../time-formatter.pipe";

@Component({
  selector: 'race',
  standalone: true,
  imports: [CommonModule, FormsModule, TimeFormatterPipe],
  templateUrl: './race.component.html',
})
export class RaceComponent {
  amount: number = 1000;
  country: string = 'USA';
  apiResponse: RouteResponse | null = null;
  isLoading: boolean = false; 

  private readonly currencyMap: Record<string, string> = {
    'USA': 'USD',
    'Europa': 'EUR',
    'Storbritannien': 'GBP',
    'Schweiz': 'CHF',
    'Japan': 'JPY',
    'Australien': 'AUD',
    'Kanada': 'CAD',
    'Indien': 'INR',
    'Mexiko': 'MXN',
    'Kurdistan': 'IQD',
    'Bangladesh': 'BDT',
    'Kina': 'CNY',
    'Sydkorea': 'KRW'
};

  constructor(
    public raceService: RaceService,
    private cdr: ChangeDetectorRef
  ) {}

  onCalculate(): void {
    const currencyCode = this.currencyMap[this.country] || 'USD';

    this.isLoading = true;
    this.apiResponse = null;

    this.raceService.calculateRoutes({ 
      amountSEK: this.amount, 
      destinationCountry: this.country,
      targetCurrency: currencyCode
    })
    .subscribe({
      next: (res) => {
        if (res && res.routes) {
          res.routes = res.routes.map(r => {
            if (r.name.toLowerCase().includes('wise')) {
              return { ...r, timeInSeconds: 1800 };
            }
            return r;
          });
        }
        
        this.apiResponse = res;
        this.isLoading = false;
        this.cdr.detectChanges(); 
      },
      error: (err) => {
        console.error(err);
        this.isLoading = false;
        this.cdr.detectChanges(); 
      }
    });
  }
}
