interface GameSearchPayload {
  gameId: number;
}

interface SeasonSearchPayload {
  seriesId: number;
  seasonNumber: number;
}

type InteractiveSearchPayload = GameSearchPayload | SeasonSearchPayload;

export default InteractiveSearchPayload;
