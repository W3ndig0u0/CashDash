export interface Race {
  date: string;
  temperatureC: number;
  summery: string | null;
}

export interface RouteRequest {
  amountSEK: number;
  destinationCountry: string;
  targetCurrency: string;
}

export interface RouteDetails {
  name: string;
  description: string;
  timeInSeconds: number;
  feeSEK: number;
}

export interface RouteResponse {
  targetCountry: string;
  amountSEK: number;
  routes: RouteDetails[];
  savingsMessage: string;
}