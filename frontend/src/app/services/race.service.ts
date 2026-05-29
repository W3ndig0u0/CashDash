import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { Observable } from "rxjs/internal/Observable";
import { Race } from "../models/race.model";

@Injectable({
  providedIn: 'root'
})

export class RaceService {
  private apiUrl = 'http://localhost:5184/races';

  constructor(private http: HttpClient) {}
    
  getRaces(): Observable<Race[]> {
    return this.http.get<Race[]>(this.apiUrl);
  }
}