import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { RouteRequest, RouteResponse } from '../models/race.model';

@Injectable({
  providedIn: 'root'
})
export class RaceService {
  private backendUrl = 'http://localhost:5184';

  constructor(private http: HttpClient) {}

  public calculateRoutes(request: RouteRequest): Observable<RouteResponse> {
    return this.http.post<RouteResponse>(`${this.backendUrl}/api/route/calculate`, request);
  }
}