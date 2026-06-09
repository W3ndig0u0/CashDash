import { Pipe, PipeTransform } from '@angular/core';

@Pipe({
  name: 'timeFormatter',
  standalone: true
})
export class TimeFormatterPipe implements PipeTransform {
  transform(seconds: number): string {
    if (seconds < 60) {
      return `${seconds} sek`;
    }
    
    if (seconds < 3600) {
      const minutes = Math.floor(seconds / 60);
      return `${minutes} min`;
    }
    
    if (seconds < 86400) {
      const hours = Math.floor(seconds / 3600);
      return `${hours} timmar`;
    }
    
    const days = Math.floor(seconds / 86400);
    return `${days} dagar`;
  }
}