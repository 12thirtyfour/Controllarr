import Game from 'Game/Game';

export interface CalendarItem extends Omit<Game, 'airDateUtc'> {
  airDateUtc: string;
}

export interface CalendarEvent extends CalendarItem {
  isGroup: false;
}

export interface CalendarEventGroup {
  isGroup: true;
  seriesId: number;
  seasonNumber: number;
  gameIds: number[];
  events: CalendarItem[];
}

export type CalendarStatus =
  | 'downloaded'
  | 'downloading'
  | 'unmonitored'
  | 'onAir'
  | 'missing'
  | 'unaired';
